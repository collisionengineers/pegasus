using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfMarketResearchAiJobCompletionStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IDocumentContentStore contentStore,
    TimeProvider timeProvider) : IMarketResearchAiJobCompletionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MarketResearchAiJobCompletion> CompleteAsync(
        CompleteMarketResearchAiJobCommand command,
        CancellationToken cancellationToken)
    {
        var details = ValuationPolicy.ValidateAutomationMarketResearch(new(
            ValuationSource.AiMarketResearch,
            command.RecordedDate,
            command.RecordedTime,
            command.Mileage,
            command.RetailValue,
            command.TradeValue));
        var documentCommand = new AddCaseDocumentCommand(
            command.CaseId,
            command.FileName,
            command.MediaType,
            command.Content,
            DocumentSemanticRole.Other,
            DocumentSource.Automation,
            $"ai-market-research:{command.JobId:D}",
            command.Actor,
            command.OperationKey,
            command.ExpectedCaseVersion,
            command.EditLeaseToken);
        EfDocumentCustodyStore.ValidateAddCommand(documentCommand);
        var contentHash = EfDocumentCustodyStore.ComputeSha256(command.Content.Span);
        var completionHash = Hash(command, contentHash);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var job = await context.AiJobs.SingleOrDefaultAsync(
            item => item.JobId == command.JobId,
            cancellationToken)
            ?? throw new KeyNotFoundException("The AI job was not found.");
        if (string.Equals(job.LastOperationKey, command.OperationKey, StringComparison.Ordinal))
        {
            if (!string.Equals(job.MarketResearchCompletionHash, completionHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The market research operation key was reused with different inputs.");
            }

            return await ReplayAsync(context, job, cancellationToken);
        }

        RequireTakenJob(job, command);
        var workflow = await context.CaseWorkflows
            .Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.CaseId == command.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException("The case was not found.");
        CaseMutationGuard.Require(
            workflow,
            command.Actor,
            command.ExpectedCaseVersion,
            command.EditLeaseToken,
            Now());
        var now = Now();
        var pending = await EfDocumentCustodyStore.PrepareAddAsync(
            context,
            contentStore,
            workflow,
            documentCommand,
            contentHash,
            now,
            cancellationToken);
        try
        {
            var valuationEntity = new CaseValuationEntity
            {
                Id = Guid.NewGuid(),
                CaseId = command.CaseId,
                Case = workflow.Case,
                Source = details.Source.ToString(),
                Date = details.Date,
                Time = details.Time,
                Mileage = details.Mileage,
                RetailValue = details.RetailValue,
                TradeValue = details.TradeValue,
                RecordedBy = command.Actor.SubjectId,
                RecordedAtUtc = now
            };
            context.CaseValuations.Add(valuationEntity);
            var valuation = EfValuationStore.Map(valuationEntity);

            CaseMutationGuard.Complete(workflow);
            EfValuationStore.AddHistory(
                context,
                workflow,
                command.Actor,
                command.OperationKey,
                "AI market research completed.",
                "valuation_created",
                completionHash,
                valuation,
                before: null,
                engineersValue: null,
                now);

            job.State = nameof(AiJobState.DraftReady);
            job.Version++;
            job.LastOperationKey = command.OperationKey;
            job.ResultKind = nameof(AiJobResultKind.MarketResearch);
            job.ResultReference = pending.Result.Occurrence.Id.ToString("D");
            job.ResultText = null;
            job.LeaseExpiresAtUtc = null;
            job.MarketResearchDocumentOccurrenceId = pending.Result.Occurrence.Id;
            job.MarketResearchDocumentVersionId = pending.Result.Version.Id;
            job.MarketResearchValuationId = valuation.ValuationId;
            job.MarketResearchRecordedDate = details.Date;
            job.MarketResearchRecordedTime = details.Time;
            job.MarketResearchMileage = details.Mileage;
            job.MarketResearchRetailValue = details.RetailValue;
            job.MarketResearchTradeValue = details.TradeValue;
            job.MarketResearchCompletionHash = completionHash;
            EfAiJobStore.AddHistory(
                context,
                job,
                "ai_job_draft_ready",
                command.Actor,
                command.OperationKey,
                reason: null,
                now);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(
                EfAiJobStore.Map(job, now),
                pending.Result,
                valuation,
                false);
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            if (pending.ContentWrite.Disposition == DocumentContentWriteDisposition.Created)
            {
                await DocumentContentRollback.RemoveOrphanAsync(
                    contextFactory,
                    contentStore,
                    command.CaseId,
                    workflow.Case.Reference,
                    pending.Version.Id,
                    exception);
            }

            throw;
        }
    }

    private async Task<MarketResearchAiJobCompletion> ReplayAsync(
        PegasusDbContext context,
        AiJobEntity job,
        CancellationToken cancellationToken)
    {
        if (job.MarketResearchDocumentOccurrenceId is not { } occurrenceId
            || job.MarketResearchDocumentVersionId is not { } versionId
            || job.MarketResearchValuationId is not { } valuationId)
        {
            throw new InvalidDataException("The persisted market research result is incomplete.");
        }

        var occurrence = await context.Set<DocumentOccurrenceEntity>().AsNoTracking()
            .SingleAsync(item => item.Id == occurrenceId, cancellationToken);
        var version = await context.Set<DocumentVersionEntity>().AsNoTracking()
            .SingleAsync(item => item.Id == versionId, cancellationToken);
        var valuation = await context.CaseValuations.AsNoTracking()
            .SingleAsync(item => item.Id == valuationId, cancellationToken);
        return new(
            EfAiJobStore.Map(job, Now()),
            new(
                EfDocumentCustodyStore.ToOccurrence(occurrence),
                EfDocumentCustodyStore.ToVersion(version),
                true),
            EfValuationStore.Map(valuation),
            true);
    }

    private void RequireTakenJob(AiJobEntity job, CompleteMarketResearchAiJobCommand command)
    {
        if (!string.Equals(job.Kind, nameof(AiJobKind.MarketResearch), StringComparison.Ordinal)
            || !string.Equals(job.SubjectKind, nameof(AiJobSubjectKind.Case), StringComparison.Ordinal)
            || job.SubjectId != command.CaseId)
        {
            throw new InvalidOperationException(
                "The AI job is not market research for the supplied case.");
        }
        if (!string.Equals(job.State, nameof(AiJobState.Taken), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The market research job is not taken.");
        }
        if (job.LeaseExpiresAtUtc is null || job.LeaseExpiresAtUtc <= Now())
        {
            throw new InvalidOperationException("The market research job lease has expired.");
        }
        if (!string.Equals(job.TakenBy, command.Actor.SubjectId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The AI job is taken by another client.");
        }
        if (job.Version != command.ExpectedJobVersion)
        {
            throw new InvalidOperationException("The AI job changed concurrently; reload and retry.");
        }
    }

    private DateTimeOffset Now()
    {
        var now = timeProvider.GetUtcNow();
        return now.Offset == TimeSpan.Zero ? now : now.ToUniversalTime();
    }

    private static string Hash(CompleteMarketResearchAiJobCommand command, string contentHash)
    {
        var material = JsonSerializer.Serialize(new
        {
            command.JobId,
            command.CaseId,
            command.OperationKey,
            FileName = EfDocumentCustodyStore.GetSafeFileName(command.FileName),
            MediaType = command.MediaType.Trim(),
            ContentHash = contentHash,
            command.RecordedDate,
            command.RecordedTime,
            command.Mileage,
            command.RetailValue,
            command.TradeValue,
            ActorKind = command.Actor.Kind.ToString(),
            command.Actor.SubjectId
        }, JsonOptions);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }
}

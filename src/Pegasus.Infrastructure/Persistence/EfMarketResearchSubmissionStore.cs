using Microsoft.EntityFrameworkCore;
using Pegasus.Core;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Custody;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Files a submission of market research from Pegasus Desktop (issue 2166)
/// in one transaction under the Case's workflow lock: the report in the
/// Case's folder, the Advert Evidence Pack and the screenshots in its
/// <c>market-research</c> subfolder (R1238, R1309), the one research card for
/// the guide month, the submission's own rows with the Case history line, and
/// the completion of a market research request waiting on the Case (R1308).
/// <para>
/// The files go to the document store inside the transaction, as every
/// document add's does, so nothing else moves the Case between the check
/// that it is open and the commit. A submission has many files, so their
/// rows are staged first and the bytes are then written a few at a time
/// under one proof of each folder; a failure anywhere rolls the rows back
/// and removes from custody every file this call created there.
/// </para>
/// </summary>
internal sealed class EfMarketResearchSubmissionStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IDocumentContentStore contentStore,
    TimeProvider timeProvider,
    IDocumentContentCachePublisher? cachePublisher = null) : IMarketResearchSubmissionStore
{
    private const string RequestCompletedEventKind = "ai_job_completed";

    /// <summary>
    /// How many of a submission's files go to the document store at the same
    /// time, the number the Worker's custody processor files at: Box limits
    /// each application by request, and the Case's lock is held meanwhile.
    /// </summary>
    private const int MaximumConcurrentWrites = 3;

    public async Task<MarketResearchSubmission?> FindAsync(
        Guid caseId,
        string operationKey,
        string inputHash,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await FindAsync(context, caseId, operationKey, inputHash, cancellationToken);
    }

    public async Task<MarketResearchSubmission> SubmitAsync(
        MarketResearchSubmissionFiling filing,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filing);
        var command = filing.Command;
        var operationKey = command.OperationKey.Trim();
        var submissionId = Guid.NewGuid();
        var documents = new AddCaseDocumentCommand[command.Files.Count];
        for (var index = 0; index < documents.Length; index++)
        {
            documents[index] = DocumentCommand(filing, index, submissionId, operationKey);
            EfDocumentCustodyStore.ValidateAddCommand(documents[index]);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        await EfCaseWorkflowStore.AcquireWorkflowMutationLockAsync(context, command.CaseId, cancellationToken);
        // A second call under the key answers with the first submission before
        // anything else is looked at, whatever has become of the Case since.
        var replay = await FindAsync(context, command.CaseId, operationKey, filing.InputHash, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var workflow = await context.CaseWorkflows
            .Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.CaseId == command.CaseId, cancellationToken)
            ?? throw new InvalidOperationException("The case was not found.");
        // Research is filed in every state but Closed, Report sent included
        // (R1242, R1244), and without the Case edit lease or a Case version:
        // it is no Case field edit and the Case is not moved.
        CaseMutationGuard.RequireOpen(workflow, command.Actor);
        var subfolders = await SubfoldersAsync(workflow.Case, command.Files, cancellationToken);
        var now = Now();
        List<EfDocumentCustodyStore.PendingDocumentAdd> written = [];
        MarketResearchSubmission submission;
        try
        {
            var ordinal = await EfDocumentCustodyStore.NextDocumentOrdinalAsync(
                context, command.CaseId, cancellationToken);
            var staged = new EfDocumentCustodyStore.StagedDocumentAdd[documents.Length];
            for (var index = 0; index < staged.Length; index++)
            {
                var contentHash = EfDocumentCustodyStore.ComputeSha256(documents[index].Content.Span);
                staged[index] = await EfDocumentCustodyStore.StageAddAsync(
                    context,
                    workflow,
                    documents[index],
                    contentHash,
                    now,
                    subfolders[index],
                    ordinal++,
                    cancellationToken);
            }
            await WriteAsync(staged, documents, written, cancellationToken);

            var recordedAt = LondonCalendar.TimeAt(now);
            var valuation = await MarketResearchFiling.WriteCardAsync(
                context,
                workflow,
                MarketResearchSubmissionPolicy.Card(
                    command,
                    DateOnly.FromDateTime(recordedAt),
                    TimeOnly.FromDateTime(recordedAt)),
                command.Actor,
                operationKey,
                MarketResearchSubmissionPolicy.SubmittedEventType,
                MarketResearchSubmissionPolicy.HistoryReason,
                MarketResearchSubmissionPolicy.HistoryReason,
                filing.InputHash,
                now,
                cancellationToken);
            var answered = await CompleteWaitingRequestAsync(context, command, operationKey, now, cancellationToken);
            context.MarketResearchSubmissions.Add(new()
            {
                Id = submissionId,
                CaseId = command.CaseId,
                OperationKey = operationKey,
                InputHash = filing.InputHash,
                SubmittedBy = command.Actor.SubjectId,
                SubmittedAtUtc = now,
                Review = command.Review.ToString(),
                GuideMonth = command.GuideMonth,
                ProposedValue = valuation.Details.RetailValue!.Value,
                RecordJson = filing.RecordJson,
                ValuationId = valuation.ValuationId,
                ClosedAiJobId = answered
            });
            var files = new MarketResearchSubmittedFile[staged.Length];
            for (var index = 0; index < staged.Length; index++)
            {
                var file = command.Files[index];
                context.MarketResearchSubmissionFiles.Add(new()
                {
                    Id = Guid.NewGuid(),
                    SubmissionId = submissionId,
                    Position = index + 1,
                    Kind = file.Kind.ToString(),
                    AdvertNumber = MarketResearchSubmissionPolicy.AdvertNumberOf(command.Record, file),
                    OccurrenceId = staged[index].Occurrence.Id,
                    VersionId = staged[index].Version.Id
                });
                files[index] = new(
                    staged[index].Version.FileName,
                    file.Kind,
                    staged[index].Occurrence.Id,
                    staged[index].Version.Id);
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            submission = new(
                submissionId,
                command.CaseId,
                now,
                command.Actor.SubjectId,
                valuation.ValuationId,
                files,
                IsReplay: false);
        }
        catch (Exception exception)
        {
            await MarketResearchFiling.RollBackAsync(
                contextFactory,
                contentStore,
                transaction,
                command.CaseId,
                workflow.Case.Reference,
                written,
                exception);
            throw;
        }

        // The files are committed and in custody. The cache copies come last,
        // outside the catch above, so they can never undo the submission.
        await PublishAsync(written, documents, cancellationToken);
        return submission;
    }

    private static async Task<MarketResearchSubmission?> FindAsync(
        PegasusDbContext context,
        Guid caseId,
        string operationKey,
        string inputHash,
        CancellationToken cancellationToken)
    {
        var submission = await context.MarketResearchSubmissions.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CaseId == caseId && item.OperationKey == operationKey,
                cancellationToken);
        if (submission is null)
        {
            return null;
        }
        if (!string.Equals(submission.InputHash, inputHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The market research operation key was reused with different inputs.");
        }

        var files = await (
                from file in context.MarketResearchSubmissionFiles.AsNoTracking()
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on file.VersionId equals version.Id
                where file.SubmissionId == submission.Id
                orderby file.Position
                select new { file.Kind, file.OccurrenceId, file.VersionId, version.FileName })
            .ToListAsync(cancellationToken);
        return new(
            submission.Id,
            submission.CaseId,
            submission.SubmittedAtUtc,
            submission.SubmittedBy,
            submission.ValuationId,
            [.. files.Select(file => new MarketResearchSubmittedFile(
                file.FileName,
                Enum.Parse<MarketResearchFileKind>(file.Kind),
                file.OccurrenceId,
                file.VersionId))],
            IsReplay: true);
    }

    /// <summary>
    /// One file as the Case document it is filed as. The report is Valuation
    /// Evidence marked as the AI's, in the Case's folder and its Documents
    /// list, as an AI market research document from the job list is. The pack
    /// and each screenshot are the member of staff's own uploads. Each takes
    /// an identity and an operation key of its own from the submission's.
    /// </summary>
    private static AddCaseDocumentCommand DocumentCommand(
        MarketResearchSubmissionFiling filing,
        int index,
        Guid submissionId,
        string operationKey)
    {
        var command = filing.Command;
        var file = command.Files[index];
        var identity = $"market-research:{submissionId:D}:{index + 1}";
        var documentKey = $"{operationKey}:market-research:{index + 1}";
        return file.Kind == MarketResearchFileKind.Report
            ? MarketResearchFiling.ReportDocument(
                command.CaseId,
                file.FileName,
                file.MediaType,
                filing.Contents[index],
                identity,
                command.Actor,
                documentKey)
            : new AddCaseDocumentCommand(
                command.CaseId,
                file.FileName,
                file.MediaType,
                filing.Contents[index],
                DocumentSemanticRole.ValuationEvidence,
                DocumentSource.StaffUpload,
                identity,
                command.Actor,
                documentKey,
                0,
                string.Empty);
    }

    /// <summary>
    /// The subfolder each file is kept in, in the files' order: none for the
    /// report, <c>market-research</c> for the pack and the screenshots. The
    /// subfolder is made in the Case's folder the first time one is needed
    /// and remembered on the Case. Making it is not undone when the
    /// submission fails: an empty folder is found again by the next.
    /// </summary>
    private async Task<string?[]> SubfoldersAsync(
        CaseEntity caseEntity,
        IReadOnlyList<MarketResearchSubmissionFile> files,
        CancellationToken cancellationToken)
    {
        var subfolders = new string?[files.Count];
        if (files.Count == 0)
        {
            return subfolders;
        }
        var caseRoot = caseEntity.CustodyRootRemoteId;
        if (string.IsNullOrWhiteSpace(caseRoot))
        {
            throw new CaseFolderUnavailableException(
                string.Equals(caseEntity.CustodyState, "pending", StringComparison.Ordinal)
                    ? CaseCustodyState.Pending
                    : CaseCustodyState.Failed);
        }

        for (var index = 0; index < subfolders.Length; index++)
        {
            if (files[index].Kind != MarketResearchFileKind.Report)
            {
                subfolders[index] = CaseCustodyFolders.MarketResearch;
            }
        }
        if (subfolders.Any(subfolder => subfolder is not null)
            && string.IsNullOrWhiteSpace(caseEntity.MarketResearchCustodyRemoteId))
        {
            caseEntity.MarketResearchCustodyRemoteId = await contentStore.EnsureCaseSubfolderAsync(
                caseRoot,
                CaseCustodyFolders.MarketResearch,
                cancellationToken);
        }

        return subfolders;
    }

    /// <summary>
    /// Writes the staged files' bytes, a few at a time, and records each one
    /// written in <paramref name="written"/> whether or not the others
    /// succeed, so a failed submission can remove them.
    /// </summary>
    private async Task WriteAsync(
        EfDocumentCustodyStore.StagedDocumentAdd[] staged,
        AddCaseDocumentCommand[] documents,
        List<EfDocumentCustodyStore.PendingDocumentAdd> written,
        CancellationToken cancellationToken)
    {
        using var proofs = contentStore is BoxDocumentContentStore boxStore
            ? boxStore.BeginFolderProofBatch()
            : NoCustodyBatch.Instance;
        var results = new DocumentContentWriteResult?[staged.Length];
        try
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, staged.Length),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = MaximumConcurrentWrites,
                    CancellationToken = cancellationToken
                },
                async (index, token) => results[index] = await contentStore.StoreVersionAsync(
                    staged[index].Address,
                    documents[index].Content,
                    staged[index].Version.Sha256,
                    token));
        }
        finally
        {
            for (var index = 0; index < results.Length; index++)
            {
                if (results[index] is { } result)
                {
                    written.Add(staged[index].Written(result));
                }
            }
        }
    }

    /// <summary>
    /// Completes the market research request waiting on the Case, queued or
    /// taken, with the reason the AI job list shows (R1308), and answers with
    /// its identifier. A request already expired is left as it is.
    /// </summary>
    private static async Task<Guid?> CompleteWaitingRequestAsync(
        PegasusDbContext context,
        SubmitMarketResearchCommand command,
        string operationKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var jobs = await context.AiJobs
            .Where(item => item.SubjectId == command.CaseId
                && item.SubjectKind == nameof(AiJobSubjectKind.Case)
                && item.Kind == nameof(AiJobKind.MarketResearch)
                && (item.State == nameof(AiJobState.Queued) || item.State == nameof(AiJobState.Taken)))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.JobId)
            .ToListAsync(cancellationToken);
        Guid? answered = null;
        foreach (var job in jobs)
        {
            var waiting = EfAiJobStore.Map(job, now);
            if (!AiJobPolicy.IsAnsweredByMarketResearchSubmission(waiting.Kind, waiting.State))
            {
                continue;
            }

            job.State = nameof(AiJobState.Completed);
            job.ClosedAtUtc = now;
            job.ClosureReason = MarketResearchSubmissionPolicy.RequestAnsweredReason;
            job.Version++;
            job.LastOperationKey = operationKey;
            job.LeaseExpiresAtUtc = null;
            EfAiJobStore.AddHistory(
                context,
                job,
                RequestCompletedEventKind,
                command.Actor,
                operationKey,
                MarketResearchSubmissionPolicy.RequestAnsweredReason,
                now);
            answered ??= job.JobId;
        }

        return answered;
    }

    private async Task PublishAsync(
        List<EfDocumentCustodyStore.PendingDocumentAdd> written,
        AddCaseDocumentCommand[] documents,
        CancellationToken cancellationToken)
    {
        if (cachePublisher is null)
        {
            return;
        }

        // Every file was written, so the two lists are in the files' order.
        await Parallel.ForEachAsync(
            Enumerable.Range(0, written.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaximumConcurrentWrites,
                CancellationToken = cancellationToken
            },
            (index, token) => new ValueTask(EfDocumentCustodyStore.PublishAddedAsync(
                cachePublisher,
                written[index],
                documents[index].Content,
                token)));
    }

    private DateTimeOffset Now()
    {
        var now = timeProvider.GetUtcNow();
        return now.Offset == TimeSpan.Zero ? now : now.ToUniversalTime();
    }
}

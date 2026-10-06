using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Reads the report dispatch facts of a Case: the Principal and its report
/// sending rules, the retained instruction e-mail and the mailbox holding it,
/// the Case's Claim Source, Repairer and outcome, and the filed estimates.
/// </summary>
public sealed class EfReportRecipientSuggestionQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : IReportRecipientSuggestionQueries
{
    private static readonly JsonSerializerOptions RetainedJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ReportDispatchFacts?> GetAsync(
        Guid caseId,
        CaseWorkSelector work,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await (from @case in context.Cases.AsNoTracking()
                         join principal in context.Principals.AsNoTracking() on @case.PrincipalId equals principal.Id
                         join receipt in context.Set<IntakeReceiptEntity>().AsNoTracking() on @case.OriginIntakeReceiptId equals receipt.Id into receipts
                         from receipt in receipts.DefaultIfEmpty()
                         join route in context.IntakeMailRouteDecisions.AsNoTracking() on receipt.Id equals route.IntakeReceiptId into routes
                         from route in routes.DefaultIfEmpty()
                         where @case.Id == caseId
                         select new
                         {
                             @case.Reference,
                             @case.AuditReference,
                             @case.Year,
                             @case.Sequence,
                             PrincipalCode = principal.Code,
                             PrincipalName = principal.Organization.Name,
                             principal.ReportSendingRulesJson,
                             ReceiptChannel = receipt == null ? null : receipt.SourceChannel,
                             ReceiptToken = receipt == null ? null : receipt.ExternalReceiptToken,
                             // The route's own effective sender only: an unresolved
                             // sender adds no address (FRD-11), as the chaser reads it.
                             EffectiveSender = route == null
                                 ? null
                                 : route.EffectiveSenderAddress
                         })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        // The email names the report of the work it delivers.
        var workKind = (await CaseWorkScope.LoadSetAsync(context, caseId, cancellationToken)).Select(work).Kind;
        var reportReference = CaseReferenceFormat.ReportReference(
            new CaseIdentity(caseId, row.PrincipalCode, row.Year, row.Sequence, row.Reference, row.AuditReference),
            workKind);
        var rules = EfOrganizationAdministration.ReadReportSending(row.ReportSendingRulesJson);

        // The instruction the Case was opened from. A message in a polled
        // mailbox is replied to; an uploaded e-mail sits in no mailbox, so it
        // gives its text and nothing to reply from.
        ReportInstructionMessage? instruction = null;
        string? instructionText = null;
        if (!string.IsNullOrEmpty(row.ReceiptToken))
        {
            var retained = await context.RetainedMailboxMessages.AsNoTracking()
                .Where(message => message.ExternalReceiptToken == row.ReceiptToken)
                .OrderBy(message => message.MailboxId == null)
                .ThenBy(message => message.Id)
                .Select(message => new
                {
                    message.Id,
                    message.MailboxId,
                    message.MailboxAddress,
                    message.ImmutableMessageId,
                    message.InternetMessageIdentity,
                    message.ConversationIdentity,
                    message.CcAddressesJson,
                    message.Subject,
                    message.BodyPlainText
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (retained is not null)
            {
                var text = string.Join('\n', new[] { retained.Subject, retained.BodyPlainText }
                    .Where(part => !string.IsNullOrWhiteSpace(part)));
                instructionText = text.Length == 0 ? null : text;
                if (row.ReceiptChannel == "mailbox" && retained.MailboxId is { } mailboxId)
                {
                    instruction = new ReportInstructionMessage(
                        retained.Id,
                        mailboxId,
                        retained.MailboxAddress,
                        retained.ImmutableMessageId,
                        retained.InternetMessageIdentity,
                        retained.ConversationIdentity,
                        row.EffectiveSender,
                        JsonSerializer.Deserialize<string[]>(retained.CcAddressesJson, RetainedJsonOptions) ?? [],
                        retained.Subject);
                }
            }
        }

        // The Case's own facts, for the work being delivered: each is the
        // accepted value, confirmed or else the intake fact.
        var workIds = CaseWorkScope.SelectedIds(context, caseId, work);
        var fields = await context.CaseDataFields.AsNoTracking()
            .Where(field => workIds.Contains(field.WorkId)
                && (field.FieldName == CaseDataFieldNames.ClaimSourceId
                    || field.FieldName == CaseDataFieldNames.RepairerId
                    || field.FieldName == CaseDataFieldNames.VehicleRegistration))
            .ToArrayAsync(cancellationToken);
        var claimSourceId = ContactId(CaseDataFieldValues.Accepted(fields, CaseDataFieldNames.ClaimSourceId));
        var repairerId = ContactId(CaseDataFieldValues.Accepted(fields, CaseDataFieldNames.RepairerId));
        var registration = CaseDataFieldValues.Accepted(fields, CaseDataFieldNames.VehicleRegistration);
        var outcome = await context.CaseAssessmentFields.AsNoTracking()
            .Where(field => workIds.Contains(field.WorkId) && field.FieldPath == AssessmentVocabulary.Outcome)
            .Select(field => field.Value)
            .FirstOrDefaultAsync(cancellationToken);

        // The names of the contacts the Case chose and the rules mention.
        var contactIds = rules.Rules
            .SelectMany(rule => rule.If)
            .Where(condition => condition.Kind is ReportSendingConditionKind.ClaimSource or ReportSendingConditionKind.Repairer)
            .SelectMany(condition => condition.Values)
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Concat([claimSourceId ?? Guid.Empty, repairerId ?? Guid.Empty])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        var names = contactIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await context.Organizations.AsNoTracking()
                .Where(organization => contactIds.Contains(organization.Id))
                .ToDictionaryAsync(organization => organization.Id, organization => organization.Name, cancellationToken);

        var defaultMailbox = await context.ApprovedMailboxes.AsNoTracking()
            .Where(mailbox => mailbox.IsDefaultStaffSend)
            .Select(mailbox => mailbox.Address)
            .FirstOrDefaultAsync(cancellationToken);

        return new ReportDispatchFacts(
            reportReference,
            registration,
            row.PrincipalName,
            rules,
            instruction,
            claimSourceId,
            claimSourceId is { } claimSource && names.TryGetValue(claimSource, out var claimSourceName) ? claimSourceName : null,
            repairerId,
            repairerId is { } repairer && names.TryGetValue(repairer, out var repairerName) ? repairerName : null,
            outcome)
        {
            OriginalSender = row.EffectiveSender,
            DefaultMailboxAddress = defaultMailbox,
            InstructionText = instructionText,
            FiledEstimates = await EfFiledEstimateAttachmentQueries.ReadAsync(context, caseId, cancellationToken),
            ContactNames = names
        };
    }

    private static Guid? ContactId(string? value) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
}

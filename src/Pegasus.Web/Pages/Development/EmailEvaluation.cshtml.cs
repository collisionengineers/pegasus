using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;

namespace Pegasus.Web.Pages.Development;

[RequestFormLimits(MultipartBodyLengthLimit = 53_477_376)]
[RequestSizeLimit(53_477_376)]
public sealed class EmailEvaluationModel(
    IIntakeSourceReader sourceReader,
    IInstructionExtractionPolicy extractionPolicy,
    IMailRoutePolicy mailRoutePolicy,
    IIntakeEvaluationReportStore reportStore,
    TimeProvider timeProvider) : PageModel
{
    private const int MaximumFileCount = 50;
    private const long MaximumFileLength = 10 * 1024 * 1024;
    private const long MaximumCampaignLength = 50 * 1024 * 1024;
    private const string ReportSchemaVersion = "local-email-evaluation-report-v1";

    private static readonly JsonSerializerOptions ReportJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    [BindProperty]
    public List<IFormFile> Upload { get; set; } = [];

    public IReadOnlyList<EmailEvaluationItem> Evaluations { get; private set; } = [];
    public string? CampaignId { get; private set; }
    public string? ReportKey { get; private set; }

    public bool ActivationBlocked => Evaluations.Count > 0;
    public int AcceptedRouteCount => Evaluations.Count(item =>
        item.RouteResult?.Disposition == MailRouteDisposition.Accepted);

    public int ApplicableInstructionCount => Evaluations.Count(item =>
        item.ExtractionResult?.Applicability == InstructionPolicyApplicability.Applicable);

    public int DuplicateSourceCount => CountDuplicateSources(Evaluations);

    public void OnGet()
    {
        Response.Headers.CacheControl = "no-store";
    }

    public async Task<IActionResult> OnGetReportAsync(
        string reportKey,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        if (string.IsNullOrWhiteSpace(reportKey))
        {
            return BadRequest();
        }

        ReadOnlyMemory<byte>? report;
        try
        {
            report = await reportStore.ReadReportAsync(reportKey, cancellationToken);
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }
        catch (IntakeArtifactIntegrityException)
        {
            return StatusCode(StatusCodes.Status409Conflict);
        }

        if (report is null)
        {
            return NotFound();
        }

        var reportHash = Path.GetFileNameWithoutExtension(reportKey);
        return File(
            report.Value.ToArray(),
            "application/json",
            $"email-evaluation-{reportHash}.json");
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        ValidateCampaign();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var evaluatedAtUtc = timeProvider.GetUtcNow();
        var results = new List<EmailEvaluationItem>(Upload.Count);
        foreach (var upload in Upload)
        {
            results.Add(await EvaluateAsync(upload, evaluatedAtUtc, cancellationToken));
        }

        Evaluations = results
            .OrderBy(item => item.ContentSha256, StringComparer.Ordinal)
            .ThenBy(item => item.FileName, StringComparer.Ordinal)
            .ToArray();
        CampaignId = CreateCampaignId(Evaluations);

        if (Evaluations.Count > 1)
        {
            var report = CreateReport(CampaignId, evaluatedAtUtc, Evaluations);
            try
            {
                ReportKey = await reportStore.StoreReportAsync(report, cancellationToken);
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The deterministic campaign report could not be retained; no acceptance claim was created.");
            }
        }

        return Page();
    }

    private void ValidateCampaign()
    {
        if (Upload.Count == 0)
        {
            ModelState.AddModelError(nameof(Upload), "Choose an .eml email to evaluate.");
            return;
        }

        if (Upload.Count > MaximumFileCount)
        {
            ModelState.AddModelError(
                nameof(Upload),
                $"A campaign may contain no more than {MaximumFileCount} emails.");
        }

        long campaignLength = 0;
        foreach (var upload in Upload)
        {
            var fileName = Path.GetFileName(upload.FileName);
            if (fileName.Length == 0
                || !fileName.EndsWith(".eml", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(Upload), "The selected file must be an .eml email.");
            }
            else if (upload.Length == 0)
            {
                ModelState.AddModelError(nameof(Upload), "The selected file is empty.");
            }
            else if (upload.Length > MaximumFileLength)
            {
                ModelState.AddModelError(nameof(Upload), "The selected file must be 10 MB or smaller.");
            }

            if (long.MaxValue - campaignLength < upload.Length)
            {
                campaignLength = long.MaxValue;
            }
            else
            {
                campaignLength += upload.Length;
            }
        }

        if (campaignLength > MaximumCampaignLength)
        {
            ModelState.AddModelError(
                nameof(Upload),
                "The campaign must be 50 MB or smaller.");
        }
    }

    private async Task<EmailEvaluationItem> EvaluateAsync(
        IFormFile upload,
        DateTimeOffset evaluatedAtUtc,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(upload.FileName);
        var content = new byte[checked((int)upload.Length)];
        await using var uploadStream = upload.OpenReadStream();
        await uploadStream.ReadExactlyAsync(content, cancellationToken);
        var contentHash = Convert.ToHexString(SHA256.HashData(content));
        var replayIdentity = $"local-email-evaluation:{contentHash}";
        var source = new IntakeSource(
            fileName,
            "message/rfc822",
            content,
            evaluatedAtUtc,
            "Local email evaluation campaign",
            new(IntakeSourceChannel.ManualUpload, replayIdentity));

        IntakeSourceReadResult readResult;
        try
        {
            readResult = await sourceReader.ReadAsync(source, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return new(
                fileName,
                contentHash,
                replayIdentity,
                null,
                null,
                null,
                "The email could not be evaluated because of a technical failure.");
        }

        MailRouteEvaluationResult? routeResult = null;
        InstructionExtractionResult? extractionResult = null;
        if (readResult.Status == IntakeSourceReadStatus.Readable && !readResult.IsIncomplete)
        {
            routeResult = mailRoutePolicy.Evaluate(readResult);
            if (routeResult is
                {
                    Disposition: MailRouteDisposition.Accepted,
                    SelectedRoute: not null
                })
            {
                extractionResult = extractionPolicy.Extract(readResult, evaluatedAtUtc);
            }
        }

        return new(
            fileName,
            contentHash,
            replayIdentity,
            readResult,
            routeResult,
            extractionResult,
            null);
    }

    private static string CreateCampaignId(IReadOnlyList<EmailEvaluationItem> evaluations)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var evaluation in evaluations)
        {
            AppendIdentityValue(hash, evaluation.ContentSha256);
            AppendIdentityValue(hash, evaluation.FileName);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void AppendIdentityValue(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static int CountDuplicateSources(IReadOnlyList<EmailEvaluationItem> evaluations) =>
        evaluations
            .GroupBy(item => item.ContentSha256, StringComparer.Ordinal)
            .Sum(group => group.Count() - 1);

    private static List<string> CreateBlockingReasons(
        IReadOnlyList<EmailEvaluationItem> evaluations)
    {
        var reasons = new List<string>
        {
            "Exact external approval for the evaluation cohort has not been provided.",
            "Independent human review evidence and an approved acceptance threshold have not been provided.",
            "This local campaign is evidence only and cannot create cases, mutate triage, or activate mailbox processing."
        };
        if (CountDuplicateSources(evaluations) > 0)
        {
            reasons.Add(
                "Duplicate source content is present and remains visibly unresolved in this campaign.");
        }

        return reasons;
    }

    private static byte[] CreateReport(
        string campaignId,
        DateTimeOffset evaluatedAtUtc,
        IReadOnlyList<EmailEvaluationItem> evaluations)
    {
        var report = new CampaignReport(
            ReportSchemaVersion,
            campaignId,
            evaluatedAtUtc,
            Summary: new(
                Total: evaluations.Count,
                Readable: evaluations.Count(item =>
                    item.ReadResult?.Status == IntakeSourceReadStatus.Readable),
                AcceptedRoutes: evaluations.Count(item =>
                    item.RouteResult?.Disposition == MailRouteDisposition.Accepted),
                ApplicableInstructions: evaluations.Count(item =>
                    item.ExtractionResult?.Applicability
                        == InstructionPolicyApplicability.Applicable),
                TechnicalFailures: evaluations.Count(item =>
                    item.TechnicalFailureReason is not null
                    || item.ReadResult?.Status == IntakeSourceReadStatus.TechnicalFailure),
                DuplicateSources: CountDuplicateSources(evaluations)),
            ActivationAllowed: false,
            ApprovalEvidenceStatus: "not-provided",
            BlockingReasons: CreateBlockingReasons(evaluations),
            Items: evaluations.Select(item => new CampaignReportItem(
                FileName: item.FileName,
                ContentSha256: item.ContentSha256,
                ReplayIdentity: item.ReplayIdentity,
                ReadStatus: item.ReadResult?.Status.ToString() ?? "TechnicalFailure",
                ReaderKey: item.ReadResult?.ReaderKey,
                ReaderVersion: item.ReadResult?.ReaderVersion,
                IsIncomplete: item.ReadResult?.IsIncomplete,
                RequiresOcr: item.ReadResult?.RequiresOcr,
                FailureCode: item.ReadResult?.FailureCode,
                FailureReason: item.ReadResult?.FailureReason ?? item.TechnicalFailureReason,
                TransportEvidence: item.ReadResult?.TransportEvidence.Select(evidence =>
                    new CampaignSourceEvidence(
                        evidence.Source.ToString(),
                        evidence.Value)).ToArray() ?? [],
                ReaderIssues: item.ReadResult?.Issues.Select(issue =>
                    new CampaignReaderIssue(
                        issue.Code,
                        issue.Reason,
                        issue.Source.ToString())).ToArray() ?? [],
                RouteDisposition: item.RouteResult?.Disposition.ToString() ?? "NotEvaluated",
                RouteOwnerCode: item.RouteResult?.SelectedRoute?.RouteOwnerCode,
                WorkProviderCode: item.RouteResult?.SelectedRoute?.WorkProviderCode,
                RouteReason: item.RouteResult?.Reason,
                RoutePolicyKey: item.RouteResult?.PolicyKey,
                RoutePolicyVersion: item.RouteResult?.PolicyVersion,
                RoutePredicates: item.RouteResult?.Predicates.Select(predicate =>
                    new CampaignRoutePredicate(
                        predicate.Key,
                        predicate.Matched,
                        predicate.Detail)).ToArray() ?? [],
                ExtractionApplicability:
                    item.ExtractionResult?.Applicability.ToString() ?? "NotEvaluated",
                ExtractionPolicyKey: item.ExtractionResult?.PolicyKey,
                ExtractionPolicyVersion: item.ExtractionResult?.PolicyVersion,
                ExtractionEvidence: item.ExtractionResult?.Evidence.Select(evidence =>
                    new CampaignEvidence(
                        evidence.Source.ToString(),
                        evidence.Strength.ToString(),
                        evidence.Finding.ToString(),
                        evidence.Signal,
                        evidence.Detail)).ToArray() ?? [])).ToArray());

        return JsonSerializer.SerializeToUtf8Bytes(report, ReportJsonOptions);
    }

    public sealed record EmailEvaluationItem(
        string FileName,
        string ContentSha256,
        string ReplayIdentity,
        IntakeSourceReadResult? ReadResult,
        MailRouteEvaluationResult? RouteResult,
        InstructionExtractionResult? ExtractionResult,
        string? TechnicalFailureReason);

    private sealed record CampaignReport(
        string SchemaVersion,
        string CampaignId,
        DateTimeOffset EvaluatedAtUtc,
        CampaignSummary Summary,
        bool ActivationAllowed,
        string ApprovalEvidenceStatus,
        IReadOnlyList<string> BlockingReasons,
        IReadOnlyList<CampaignReportItem> Items);

    private sealed record CampaignSummary(
        int Total,
        int Readable,
        int AcceptedRoutes,
        int ApplicableInstructions,
        int TechnicalFailures,
        int DuplicateSources);

    private sealed record CampaignReportItem(
        string FileName,
        string ContentSha256,
        string ReplayIdentity,
        string ReadStatus,
        string? ReaderKey,
        string? ReaderVersion,
        bool? IsIncomplete,
        bool? RequiresOcr,
        string? FailureCode,
        string? FailureReason,
        IReadOnlyList<CampaignSourceEvidence> TransportEvidence,
        IReadOnlyList<CampaignReaderIssue> ReaderIssues,
        string RouteDisposition,
        string? RouteOwnerCode,
        string? WorkProviderCode,
        string? RouteReason,
        string? RoutePolicyKey,
        int? RoutePolicyVersion,
        IReadOnlyList<CampaignRoutePredicate> RoutePredicates,
        string ExtractionApplicability,
        string? ExtractionPolicyKey,
        int? ExtractionPolicyVersion,
        IReadOnlyList<CampaignEvidence> ExtractionEvidence);

    private sealed record CampaignSourceEvidence(string Source, string Value);

    private sealed record CampaignReaderIssue(string Code, string Reason, string Source);

    private sealed record CampaignRoutePredicate(string Key, bool Matched, string Detail);

    private sealed record CampaignEvidence(
        string Source,
        string Strength,
        string Finding,
        string Signal,
        string Detail);
}

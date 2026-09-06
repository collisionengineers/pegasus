using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake.ThirdPartyReports;

/// <summary>
/// The third-party engineer report families the reference corpus proves. A
/// family is an issuer's report layout, never a folder or a file name: the
/// selector reads the document's own printed identity and report role.
/// </summary>
public enum ThirdPartyReportFamily
{
    Connexus,
    ExclusiveErehr,
    EvaBodyshop,
    Laird,
    Montgomery,
    SPrint,
    JohnRBell
}

/// <summary>
/// What a source document is. Only <see cref="EngineerReport"/> yields a
/// report candidate; every other role is routed to its own existing owner and
/// deliberately produces no report verdict.
/// </summary>
public enum ThirdPartyDocumentRole
{
    EngineerReport,
    Estimate,
    VehicleHistory,
    Invoice,
    ImageEvidence
}

/// <summary>Exactly one of three outcomes; there is no confidence score.</summary>
public enum ThirdPartySelectionOutcome
{
    Selected,
    NotApplicable,
    Ambiguous
}

public enum ThirdPartySelectionReason
{
    /// <summary>One report signature matched: <c>Family</c> carries it.</summary>
    DocumentSignatureMatched,

    /// <summary>The document carries readable text but no known signature.</summary>
    NoDocumentSignature,

    /// <summary>One signature matched and it is an explicit non-report role.</summary>
    NonReportDocumentRole,

    /// <summary>
    /// The source yielded no readable text at all (a scan-only PDF). No family
    /// and no document role can be read without OCR, and neither is guessed
    /// from the file name — the fields stay unavailable rather than invented.
    /// </summary>
    TextUnavailableRequiresOcr,

    /// <summary>More than one signature matched; every match is reported.</summary>
    MultipleDocumentSignatures
}

/// <summary>
/// Immutable identity of the source being read: the retained bytes' hash and
/// occurrence plus whichever logical document handles the caller already holds.
/// </summary>
public sealed record ThirdPartyReportSourceContext(
    Guid ReceiptId,
    string Sha256,
    int Occurrence,
    Guid? DocumentId = null,
    Guid? DocumentVersionId = null,
    Guid? IntakeAssetId = null,
    string ReaderVersion = "unspecified_reader");

/// <summary>
/// A finite, versioned document signature: required text signals that must all
/// appear, and negative signals none of which may. No priority, no ordering, no
/// first-match — every signature is evaluated and the outcome is the count.
/// </summary>
public sealed record ThirdPartyDocumentSignature(
    string Key,
    string Version,
    string? Issuer,
    ThirdPartyReportFamily? Family,
    ThirdPartyDocumentRole Role,
    string IssuerPattern,
    IReadOnlyList<string> Required,
    IReadOnlyList<string> Negative);

/// <summary>One signature that matched, with the page its issuer evidence sits on.</summary>
public sealed record ThirdPartyDocumentSignatureMatch(
    string SignatureKey,
    string? Issuer,
    ThirdPartyReportFamily? Family,
    ThirdPartyDocumentRole Role,
    int? Page,
    string SourceLabel,
    string Evidence);

/// <summary>
/// The selection verdict. <see cref="Issuer"/> is always present so the source
/// row records the outcome even when nothing was proved: Usable for a selected
/// family, Ambiguous when several signatures matched, Missing otherwise.
/// </summary>
public sealed record ThirdPartyReportSelection(
    ThirdPartySelectionOutcome Outcome,
    ThirdPartySelectionReason Reason,
    ThirdPartyReportFamily? Family,
    ThirdPartyDocumentRole? DocumentRole,
    SourceFieldCandidate Issuer,
    IReadOnlyList<ThirdPartyDocumentSignatureMatch> Matches);

/// <summary>
/// One page of readable source text, with the page number read from the
/// reader's fragment label (<c>"…, page 3"</c>) — the only place a page number
/// exists on <see cref="IntakeContentFragment"/>.
/// </summary>
internal sealed record ThirdPartySourcePage(
    int? Page,
    string SourceLabel,
    string Text,
    string Flat)
{
    private static readonly Regex PageLabel = new(
        @",\s*page\s+(?<n>\d+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex Whitespace = new(
        @"\s+",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public static IReadOnlyList<ThirdPartySourcePage> Read(IntakeSourceReadResult readResult)
    {
        var pages = new List<ThirdPartySourcePage>();
        foreach (var fragment in readResult.Content)
        {
            if (string.IsNullOrWhiteSpace(fragment.Text))
            {
                continue;
            }

            var text = fragment.Text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Replace('\u00A0', ' ')
                .Replace('\u2019', '\'');
            var label = fragment.SourceLabel ?? string.Empty;
            var match = PageLabel.Match(label);
            var page = match.Success
                ? int.Parse(match.Groups["n"].ValueSpan, provider: CultureInfo.InvariantCulture)
                : (int?)null;
            pages.Add(new(page, label, text, Whitespace.Replace(text, " ")));
        }

        return pages;
    }
}

/// <summary>
/// The Core-owned issuer and document-role selector for third-party engineer
/// reports (INTK-031). Selection reads the document's own printed evidence:
/// never the folder, the file name, the retained principal, or the position of
/// a value on a page.
/// </summary>
public static class ThirdPartyReportProfiles
{
    /// <summary>Versioned with the signature table; recorded on every candidate.</summary>
    public const string ProfileVersion = "third-party-report-profiles/1";

    /// <summary>The document role recorded on a selected report's candidates.</summary>
    public const string ReportDocumentRole = "third-party-engineer-report";

    private const string ExclusiveIssuer = @"Exclusive\s*Vehicle\s*Assessors";
    private const string ConnexusIssuer = @"Connexus\s*Vehicle\s*Assessors";
    private const string ErehrClaimReference = @"Your\s+Ref\s*:?\s*(?:ER)?EHR\s*\d{4,}";
    private const string RepairableReportTitle = @"REPAIRABLE\s+REPORT";

    /// <summary>
    /// The printed heading that distinguishes a Laird supplement from a full
    /// assessment report. The extraction rules gate on this same constant, so
    /// the document's own words are spelled in one place.
    /// </summary>
    internal const string SupplementaryReportTitle = @"Supplementary\s+Report";

    private static readonly ThirdPartyDocumentSignature[] SignatureTable =
    [
        // Connexus prints its own signature block; note that its body text also
        // names "Exclusive Repair Network" as the commissioning party, which is
        // why the Exclusive signatures require the assessor signature line and
        // not the bare word.
        new("connexus/1", "1", "Connexus Vehicle Assessors", ThirdPartyReportFamily.Connexus,
            ThirdPartyDocumentRole.EngineerReport, ConnexusIssuer,
            [@"Engineer\s+Repairable\s+Report"], []),

        // Exclusive issues both the EREHR-referenced reports and the bodyshop
        // reports; the printed claim-reference role is the only proved
        // difference, so it is required by one and denied to the other.
        new("exclusive-erehr/1", "1", "Exclusive Vehicle Assessors", ThirdPartyReportFamily.ExclusiveErehr,
            ThirdPartyDocumentRole.EngineerReport, ExclusiveIssuer,
            [RepairableReportTitle, ErehrClaimReference], [ConnexusIssuer]),

        new("eva-bodyshop/1", "1", "Exclusive Vehicle Assessors", ThirdPartyReportFamily.EvaBodyshop,
            ThirdPartyDocumentRole.EngineerReport, ExclusiveIssuer,
            [RepairableReportTitle], [ErehrClaimReference, ConnexusIssuer]),

        // Laird's footer domain is its issuer evidence: the vehicle-history PDF
        // names "Laird Assessors" as a dealer without being a Laird report.
        new("laird/1", "1", "Laird Assessors", ThirdPartyReportFamily.Laird,
            ThirdPartyDocumentRole.EngineerReport, @"laird-assessors\.com",
            [@"(?:Repairable\s+Damage\s+Assessment\s+Report|" + SupplementaryReportTitle + ")"], []),

        new("montgomery/1", "1", "Montgomery Assessors", ThirdPartyReportFamily.Montgomery,
            ThirdPartyDocumentRole.EngineerReport, @"Montgomery\s*Assessors",
            [@"Consulting\s+Motor\s+Engineers"], []),

        new("sprint/1", "1", "sPrint Assessors", ThirdPartyReportFamily.SPrint,
            ThirdPartyDocumentRole.EngineerReport, @"sprintassessors@btinternet\.com",
            [@"Automotive\s+Claims\s+Assessors"], []),

        // The one John R Bell original in the corpus is scan-only, so this
        // signature is proved by structural tests alone until OCR text reaches
        // the selector; it is never satisfied by a file name.
        new("john-r-bell/1", "1", "John R Bell", ThirdPartyReportFamily.JohnRBell,
            ThirdPartyDocumentRole.EngineerReport, @"John\s*R\.?\s*Bell",
            [@"(?:Engineer'?s?\s+Report|Repairable\s+Report|Assessment\s+Report)"], []),

        // Negative roles. Each is routed to its own existing owner and emits no
        // report verdict. The negatives are the overlaps the corpus proves:
        // engineer reports mention Audatex and carry a "vehicle history check"
        // section, and two report families embed their own fee invoice.
        new("audatex-estimate/1", "1", null, null,
            ThirdPartyDocumentRole.Estimate, @"Full\s+Estimate\s+Report",
            [@"Audatex"], [RepairableReportTitle, @"Vehicle\s*Assessors", @"laird-assessors\.com"]),

        new("motorcheck-vehicle-history/1", "1", null, null,
            ThirdPartyDocumentRole.VehicleHistory, @"Vehicle\s+History\s+Check",
            [@"Major\s+issues", @"Minor\s+issues"], [@"Vehicle\s*Assessors", RepairableReportTitle]),

        new("invoice/1", "1", null, null,
            ThirdPartyDocumentRole.Invoice, @"INVOICE",
            [@"(?:Total|Amount)\s+Due"],
            [
                RepairableReportTitle, @"Vehicle\s*Assessors", @"laird-assessors\.com",
                @"Automotive\s+Claims\s+Assessors", @"Montgomery\s*Assessors"
            ]),

        // The Laird domain and the Supplementary heading are denied here for
        // the same reason the invoice signature denies them: a report that
        // prints an appended image filename would otherwise match this
        // signature as well as its issuer's, become Ambiguous, and yield no
        // candidate at all. The negatives are the report signals every other
        // negative signature already denies.
        new("image-evidence/1", "1", null, null,
            ThirdPartyDocumentRole.ImageEvidence, @"\.(?:jpe?g|png)\b",
            [],
            [
                RepairableReportTitle, @"Vehicle\s*Assessors", @"Assessment\s+Report",
                @"Full\s+Estimate\s+Report", @"Vehicle\s+History\s+Check",
                @"Consulting\s+Motor\s+Engineers", @"Automotive\s+Claims\s+Assessors",
                @"laird-assessors\.com", SupplementaryReportTitle
            ])
    ];

    private static readonly CompiledSignature[] Compiled =
        [.. SignatureTable.Select(CompiledSignature.Compile)];

    /// <summary>The finite signature table, in declaration order.</summary>
    public static IReadOnlyList<ThirdPartyDocumentSignature> Signatures => SignatureTable;

    /// <summary>
    /// Selects the issuer family or the explicit non-report role for one source.
    /// Every signature is evaluated; the outcome is decided by how many matched.
    /// </summary>
    public static ThirdPartyReportSelection Select(
        IntakeSourceReadResult readResult,
        ThirdPartyReportSourceContext context)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentNullException.ThrowIfNull(context);

        var pages = ThirdPartySourcePage.Read(readResult);
        return Select(pages, context);
    }

    internal static ThirdPartyReportSelection Select(
        IReadOnlyList<ThirdPartySourcePage> pages,
        ThirdPartyReportSourceContext context)
    {
        if (pages.Count == 0)
        {
            return Verdict(
                ThirdPartySelectionOutcome.NotApplicable,
                ThirdPartySelectionReason.TextUnavailableRequiresOcr,
                null,
                null,
                context,
                [],
                SourceCandidateDisposition.Missing,
                null);
        }

        var document = string.Join(' ', pages.Select(page => page.Flat));
        var matches = new List<ThirdPartyDocumentSignatureMatch>();
        CompiledSignature? single = null;
        foreach (var candidate in Compiled)
        {
            if (!candidate.Matches(document))
            {
                continue;
            }

            single = candidate;
            matches.Add(candidate.Describe(pages));
        }

        if (matches.Count == 0)
        {
            return Verdict(
                ThirdPartySelectionOutcome.NotApplicable,
                ThirdPartySelectionReason.NoDocumentSignature,
                null,
                null,
                context,
                matches,
                SourceCandidateDisposition.Missing,
                null);
        }

        if (matches.Count > 1)
        {
            return Verdict(
                ThirdPartySelectionOutcome.Ambiguous,
                ThirdPartySelectionReason.MultipleDocumentSignatures,
                null,
                null,
                context,
                matches,
                SourceCandidateDisposition.Ambiguous,
                matches[0]);
        }

        var matched = matches[0];
        var signature = single!.Signature;
        return signature.Family is { } family
            ? Verdict(
                ThirdPartySelectionOutcome.Selected,
                ThirdPartySelectionReason.DocumentSignatureMatched,
                family,
                signature.Role,
                context,
                matches,
                SourceCandidateDisposition.Usable,
                matched)
            : Verdict(
                ThirdPartySelectionOutcome.NotApplicable,
                ThirdPartySelectionReason.NonReportDocumentRole,
                null,
                signature.Role,
                context,
                matches,
                SourceCandidateDisposition.Missing,
                matched);
    }

    /// <summary>The document-role string recorded on persisted source rows.</summary>
    public static string DocumentRoleCode(ThirdPartyDocumentRole? role) => role switch
    {
        ThirdPartyDocumentRole.EngineerReport => ReportDocumentRole,
        ThirdPartyDocumentRole.Estimate => "estimate",
        ThirdPartyDocumentRole.VehicleHistory => "vehicle-history",
        ThirdPartyDocumentRole.Invoice => "invoice",
        ThirdPartyDocumentRole.ImageEvidence => "image-evidence",
        _ => "unclassified"
    };

    private static ThirdPartyReportSelection Verdict(
        ThirdPartySelectionOutcome outcome,
        ThirdPartySelectionReason reason,
        ThirdPartyReportFamily? family,
        ThirdPartyDocumentRole? role,
        ThirdPartyReportSourceContext context,
        IReadOnlyList<ThirdPartyDocumentSignatureMatch> matches,
        SourceCandidateDisposition disposition,
        ThirdPartyDocumentSignatureMatch? evidence) =>
        new(
            outcome,
            reason,
            family,
            role,
            ThirdPartySourceCandidates.Create(
                context,
                ThirdPartyReportFields.Issuer,
                DocumentRoleCode(role),
                rawValue: evidence?.Evidence,
                normalizedValue: evidence?.Issuer,
                page: evidence?.Page,
                sourceLabel: evidence?.SourceLabel ?? string.Empty,
                policyVersion: ProfileVersion,
                disposition: disposition),
            matches);

    private sealed record CompiledSignature(
        ThirdPartyDocumentSignature Signature,
        Regex Issuer,
        IReadOnlyList<Regex> Required,
        IReadOnlyList<Regex> Negative)
    {
        public static CompiledSignature Compile(ThirdPartyDocumentSignature signature) =>
            new(
                signature,
                ThirdPartyRegex.Create(signature.IssuerPattern),
                [.. signature.Required.Select(ThirdPartyRegex.Create)],
                [.. signature.Negative.Select(ThirdPartyRegex.Create)]);

        public bool Matches(string document) =>
            Issuer.IsMatch(document)
            && Required.All(required => required.IsMatch(document))
            && !Negative.Any(negative => negative.IsMatch(document));

        public ThirdPartyDocumentSignatureMatch Describe(IReadOnlyList<ThirdPartySourcePage> pages)
        {
            foreach (var page in pages)
            {
                var match = Issuer.Match(page.Flat);
                if (match.Success)
                {
                    return new(
                        Signature.Key,
                        Signature.Issuer,
                        Signature.Family,
                        Signature.Role,
                        page.Page,
                        page.SourceLabel,
                        match.Value.Trim());
                }
            }

            // The signal spans a page boundary in the flattened document; the
            // signature still matched, so record it without a page locator.
            return new(
                Signature.Key,
                Signature.Issuer,
                Signature.Family,
                Signature.Role,
                null,
                pages.Count > 0 ? pages[0].SourceLabel : string.Empty,
                Signature.IssuerPattern);
        }
    }
}

/// <summary>
/// Every regular expression this profile family uses carries the repository's
/// 100 ms match timeout (DELIV-036) and is culture-invariant.
/// </summary>
internal static class ThirdPartyRegex
{
    public static Regex Create(string pattern) =>
        new(
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

    public static Regex CreateMultiline(string pattern) =>
        new(
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline,
            TimeSpan.FromMilliseconds(100));
}

/// <summary>
/// Builds the shared <see cref="SourceFieldCandidate"/> rows. Identifiers are
/// derived from the immutable source hash, occurrence, field role and locator,
/// so replaying the same bytes reproduces the same rows exactly.
/// </summary>
internal static class ThirdPartySourceCandidates
{
    public static SourceFieldCandidate Create(
        ThirdPartyReportSourceContext context,
        string field,
        string documentRole,
        string? rawValue,
        string? normalizedValue,
        int? page,
        string sourceLabel,
        string policyVersion,
        SourceCandidateDisposition disposition,
        string partyRole = "",
        string referenceRole = "",
        string? unit = null,
        string? currency = null,
        string? cell = null,
        string? region = null,
        int ordinal = 0) =>
        new(
            DeterministicId(context, field, partyRole, referenceRole, page, rawValue, disposition, ordinal),
            context.ReceiptId,
            context.DocumentId,
            context.DocumentVersionId,
            context.IntakeAssetId,
            context.Sha256,
            context.Occurrence,
            documentRole,
            partyRole,
            referenceRole,
            field,
            rawValue,
            normalizedValue,
            unit,
            currency,
            sourceLabel,
            page,
            cell,
            null,
            region,
            context.ReaderVersion,
            policyVersion,
            disposition);

    private static Guid DeterministicId(
        ThirdPartyReportSourceContext context,
        string field,
        string partyRole,
        string referenceRole,
        int? page,
        string? rawValue,
        SourceCandidateDisposition disposition,
        int ordinal)
    {
        var key = string.Join(
            '\u001F',
            context.Sha256,
            context.Occurrence.ToString(CultureInfo.InvariantCulture),
            field,
            partyRole,
            referenceRole,
            page?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            rawValue ?? string.Empty,
            disposition.ToString(),
            // Zero for every printed value: one row per (field, role, page,
            // value), so the printed values need nothing to tell them apart.
            // A finding passes its position in the raised order, because two
            // findings can legitimately state the same sentence about the same
            // page (C05-R-12).
            ordinal.ToString(CultureInfo.InvariantCulture));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}

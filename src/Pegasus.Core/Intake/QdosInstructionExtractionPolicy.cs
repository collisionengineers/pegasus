using System.Globalization;
using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

public sealed partial class QdosInstructionExtractionPolicy(
    IIntakeTriageMatcher? triageMatcher = null) : IInstructionExtractionPolicy, IMailRoutePolicy
{
    public const string Key = "qdos_instruction";
    public const int Version = 1;
    public const string MailRouteKey = "qdos_mail_route";
    public const int MailRouteVersion = 2;
    private const string PrincipalCode = "QDOS";
    private const string AcceptedDirectDomain = "qdosassist.co.uk";
    private const string StaffTransportDomain = "collisionengineers.co.uk";
    private readonly IIntakeTriageMatcher triageMatcher =
        triageMatcher ?? new NoAcceptedIntakeTriageMatcher();

    private static readonly InstructionFieldEngine.FieldDefinition[] FieldDefinitions =
    [
        new("Claimant name", ["Claimant Name", "Claimant"]),
        new("Claim number", ["Claim Number", "Claim No", "Claim Reference"]),
        new("Vehicle registration", ["Vehicle Registration", "Registration", "VRM"]),
        new("Vehicle make", ["Vehicle Make", "Make"]),
        new("Vehicle model", ["Vehicle Model", "Model"]),
        new("Vehicle mileage", ["Vehicle Mileage", "Mileage"]),
        new("Accident circumstances", ["Accident Circumstances", "Circumstances"]),
        new("Date of incident", ["Date of Incident", "Incident Date", "Accident Date"]),
        new("Instruction date", ["Instruction Date", "Date of Instruction"]),
        new("Inspection address", ["Inspection Address", "Vehicle Location", "Inspection Location"]),
        new("Inspection date", ["Inspection Date", "Date of Inspection", "Inspection Deadline", "Due By"], IsRequired: false)
    ];

    public MailRouteEvaluationResult Evaluate(IntakeSourceReadResult readResult)
    {
        EnsureReadable(readResult);

        var transportIdentities = SenderIdentities(
            readResult.TransportEvidence,
            IntakeSenderIdentityKind.Transport);
        var originalIdentities = SenderIdentities(
            readResult.TransportEvidence,
            IntakeSenderIdentityKind.AttachedOriginal);
        var hasOneTransportSender = transportIdentities.Length == 1;
        var transportDomain = string.Empty;
        var hasValidTransportSender = hasOneTransportSender
            && TryGetMailboxDomain(transportIdentities[0].Address, out transportDomain);
        var isStaffForward = hasValidTransportSender
            && string.Equals(
                transportDomain,
                StaffTransportDomain,
                StringComparison.OrdinalIgnoreCase);
        var hasOneOriginalSender = originalIdentities.Length == 1;
        var originalDomain = string.Empty;
        var hasValidOriginalSender = hasOneOriginalSender
            && TryGetMailboxDomain(originalIdentities[0].Address, out originalDomain);
        var hasExternalOriginalSender = hasValidOriginalSender
            && !string.Equals(
                originalDomain,
                StaffTransportDomain,
                StringComparison.OrdinalIgnoreCase);
        var effectiveSender = isStaffForward
            ? hasExternalOriginalSender
                ? originalIdentities[0]
                : null
            : hasValidTransportSender
                ? transportIdentities[0]
                : null;
        var effectiveDomain = string.Empty;
        var hasValidEffectiveSender = effectiveSender is not null
            && TryGetMailboxDomain(effectiveSender.Address, out effectiveDomain);
        var matchesDirectQdosDomain = hasValidEffectiveSender
            && string.Equals(
                effectiveDomain,
                AcceptedDirectDomain,
                StringComparison.OrdinalIgnoreCase);

        MailRoutePredicateResult[] predicates =
        [
            new(
                "direct.sender-exactly-one",
                hasOneTransportSender,
                hasOneTransportSender
                    ? "Exactly one consistent transport sender address was supplied."
                    : $"Expected one consistent transport sender address; found {transportIdentities.Length}."),
            new(
                "forward.staff-transport",
                isStaffForward,
                isStaffForward
                    ? "The transport sender uses the Collision Engineers staff domain."
                    : "The transport sender is not a Collision Engineers staff forward."),
            new(
                "forward.original-exactly-one",
                hasOneOriginalSender,
                hasOneOriginalSender
                    ? "Exactly one attached original sender address was supplied."
                    : $"Expected one attached original sender address for a staff forward; found {originalIdentities.Length}."),
            new(
                "forward.original-external",
                hasExternalOriginalSender,
                hasExternalOriginalSender
                    ? "The attached original sender is external to Collision Engineers."
                    : "No unambiguous external attached original sender was proved."),
            new(
                "direct.qdos-domain",
                matchesDirectQdosDomain,
                matchesDirectQdosDomain
                    ? "The effective sender uses the accepted direct QDOS domain."
                    : "The effective sender does not use the accepted direct QDOS domain."),
            new(
                "intermediary.accepted-policy",
                false,
                "No QDOS intermediary mail route has accepted evidence in this policy version.")
        ];

        if (!hasOneTransportSender)
        {
            return Result(
                MailRouteDisposition.NeedsSorting,
                null,
                predicates,
                "Mail route evaluation requires exactly one consistent transport sender address.",
                transportIdentities,
                originalIdentities,
                null);
        }

        if (!hasValidTransportSender)
        {
            return Result(
                MailRouteDisposition.NeedsSorting,
                null,
                predicates,
                "The transport sender address is malformed.",
                transportIdentities,
                originalIdentities,
                null);
        }

        if (isStaffForward && !hasOneOriginalSender)
        {
            return Result(
                MailRouteDisposition.NeedsSorting,
                null,
                predicates,
                "A staff-forwarded message requires exactly one consistent attached original sender.",
                transportIdentities,
                originalIdentities,
                null);
        }

        if (isStaffForward && !hasValidOriginalSender)
        {
            return Result(
                MailRouteDisposition.NeedsSorting,
                null,
                predicates,
                "The attached original sender address is malformed.",
                transportIdentities,
                originalIdentities,
                null);
        }

        if (isStaffForward && !hasExternalOriginalSender)
        {
            return Result(
                MailRouteDisposition.NeedsSorting,
                null,
                predicates,
                "The attached original sender does not prove an external mail route.",
                transportIdentities,
                originalIdentities,
                null);
        }

        if (!matchesDirectQdosDomain)
        {
            return Result(
                MailRouteDisposition.NoMatch,
                null,
                predicates,
                "The effective sender does not match an accepted QDOS mail route.",
                transportIdentities,
                originalIdentities,
                effectiveSender);
        }

        return Result(
            MailRouteDisposition.Accepted,
            new(PrincipalCode, MailRouteKind.DirectProvider, PrincipalCode),
            predicates,
            "The effective sender matches the accepted direct QDOS mail route.",
            transportIdentities,
            originalIdentities,
            effectiveSender);
    }

    public InstructionExtractionResult Extract(
        IntakeSourceReadResult readResult,
        DateTimeOffset processedAtUtc)
    {
        var route = Evaluate(readResult);
        var evidence = new List<IntakeEvidence>();
        var confirmingFragments = new List<IntakeContentFragment>();
        foreach (var fragment in readResult.Content)
        {
            var labelsFound = FieldDefinitions.Count(definition =>
                definition.Labels.Any(label => InstructionFieldEngine.ContainsLabel(fragment.Text, label)));
            var hasQdos = QdosMarkerRegex().IsMatch(fragment.Text);

            if (hasQdos)
            {
                evidence.Add(new(
                    fragment.Source,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.SupportsPrincipal,
                    "qdos-content-marker",
                    $"QDOS was identified in {fragment.SourceLabel}."));
            }

            if (hasQdos && labelsFound >= 2)
            {
                confirmingFragments.Add(fragment);
                evidence.Add(new(
                    fragment.Source,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.SupportsPrincipal,
                    "instruction-structure",
                    $"{fragment.SourceLabel} contains QDOS and {labelsFound} instruction field labels."));
            }
        }

        AddTransportEvidence(
            readResult.TransportEvidence,
            route,
            confirmingFragments.Count > 0,
            evidence);

        if (confirmingFragments.Count > 0)
        {
            var (fields, missingFields, fieldEvidence) = InstructionFieldEngine.ExtractFields(
                readResult.Content,
                FieldDefinitions,
                processedAtUtc);
            evidence.AddRange(fieldEvidence);
            var draft = CreateInstructionDraft(fields);
            var triageMatches = triageMatcher.Match(readResult, draft);
            ArgumentNullException.ThrowIfNull(triageMatches);
            foreach (var match in triageMatches)
            {
                ValidateTriageMatch(match);
                evidence.Add(new(
                    match.Source,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.AcceptedTriageMatch,
                    match.Signal.Trim(),
                    match.Detail.Trim(),
                    match.MatcherKey.Trim(),
                    match.MatcherVersion));
            }
            if (readResult.RequiresOcr)
            {
                evidence.Add(new(
                    IntakeEvidenceSource.PdfContent,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.Information,
                    "additional-scanned-content",
                    "A QDOS-shaped draft was extracted from readable content; additional scanned PDF content still requires review."));
            }

            return new(
                InstructionPolicyApplicability.Applicable,
                evidence,
                fields,
                draft,
                missingFields,
                Key,
                Version);
        }

        return new(
            evidence.Count == 0 && !readResult.RequiresOcr
                ? InstructionPolicyApplicability.NotApplicable
                : InstructionPolicyApplicability.Indeterminate,
            evidence,
            [],
            null,
            [],
            Key,
            Version);
    }

    private static InstructionDraft CreateInstructionDraft(IReadOnlyList<InstructionReviewField> fields)
    {
        var values = fields.ToDictionary(
            field => field.Name,
            field => field.SuggestedValue,
            StringComparer.Ordinal);
        return new(
            PrincipalCode,
            InstructionFieldEngine.TypedString(values["Claimant name"], 300),
            InstructionFieldEngine.TypedString(values["Claim number"], 100),
            InstructionFieldEngine.NormalizeRegistration(values["Vehicle registration"]),
            InstructionFieldEngine.TypedString(values["Vehicle make"], 100),
            InstructionFieldEngine.TypedString(values["Vehicle model"], 100),
            InstructionFieldEngine.ParseMileage(values["Vehicle mileage"]),
            InstructionFieldEngine.TypedString(values["Accident circumstances"], 2000),
            InstructionFieldEngine.ParseDate(values["Date of incident"]),
            InstructionFieldEngine.ParseDate(values["Instruction date"]),
            InstructionFieldEngine.TypedString(values["Inspection address"], 1000),
            InstructionFieldEngine.ParseDate(values["Inspection date"]));
    }

    private static void AddTransportEvidence(
        IReadOnlyList<IntakeTransportEvidence> transportEvidence,
        MailRouteEvaluationResult route,
        bool contentConfirmed,
        List<IntakeEvidence> evidence)
    {
        if (route.Disposition == MailRouteDisposition.Accepted)
        {
            evidence.Add(new(
                IntakeEvidenceSource.Sender,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.Information,
                "qdos-direct-mail-route",
                "The effective sender uses the accepted direct QDOS domain."));
        }

        foreach (var item in transportEvidence)
        {
            if (QdosMarkerRegex().IsMatch(item.Value))
            {
                evidence.Add(new(
                    item.Source,
                    IntakeEvidenceStrength.Weak,
                    IntakeEvidenceFinding.SupportsPrincipal,
                    "qdos-transport-marker",
                    $"{DisplaySource(item.Source)} contains a QDOS marker."));
            }
            else if (contentConfirmed && item.Source == IntakeEvidenceSource.Sender)
            {
                evidence.Add(new(
                    item.Source,
                    IntakeEvidenceStrength.Weak,
                    IntakeEvidenceFinding.ContradictsTransport,
                    "forwarded-sender",
                    "The retained transport sender is not the proved effective original sender."));
            }
        }
    }

    private static MailRouteIdentity[] SenderIdentities(
        IReadOnlyList<IntakeTransportEvidence> transportEvidence,
        IntakeSenderIdentityKind kind) =>
        transportEvidence
            .Where(item =>
                item.Source == IntakeEvidenceSource.Sender
                && item.SenderIdentityKind == kind)
            .Select(item => new MailRouteIdentity(
                item.Value.Trim(),
                string.IsNullOrWhiteSpace(item.SourceLabel)
                    ? kind == IntakeSenderIdentityKind.Transport
                        ? "outer message"
                        : "attached original message"
                    : item.SourceLabel.Trim()))
            .Where(item => item.Address.Length > 0)
            .DistinctBy(item => item.Address, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static MailRouteEvaluationResult Result(
        MailRouteDisposition disposition,
        MailRouteSelection? selectedRoute,
        IReadOnlyList<MailRoutePredicateResult> predicates,
        string reason,
        IReadOnlyList<MailRouteIdentity> transportIdentities,
        IReadOnlyList<MailRouteIdentity> originalIdentities,
        MailRouteIdentity? effectiveSender) =>
        new(
            disposition,
            selectedRoute,
            predicates,
            reason,
            MailRouteKey,
            MailRouteVersion,
            transportIdentities,
            originalIdentities,
            effectiveSender);

    private static void EnsureReadable(IntakeSourceReadResult readResult)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete)
        {
            throw new ArgumentException(
                "The QDOS extraction policy accepts only fully readable, complete reader results.",
                nameof(readResult));
        }
    }

    private static bool TryGetMailboxDomain(string address, out string domain)
    {
        domain = string.Empty;
        var separator = address.IndexOf('@');
        if (separator <= 0
            || separator != address.LastIndexOf('@')
            || separator == address.Length - 1
            || address.Any(char.IsWhiteSpace)
            || address.Contains('<')
            || address.Contains('>'))
        {
            return false;
        }

        domain = address[(separator + 1)..];
        return true;
    }

    private static void ValidateTriageMatch(IntakeTriageMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentException.ThrowIfNullOrWhiteSpace(match.Signal);
        ArgumentException.ThrowIfNullOrWhiteSpace(match.Detail);
        ArgumentException.ThrowIfNullOrWhiteSpace(match.MatcherKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(match.MatcherVersion);
    }

    private static string DisplaySource(IntakeEvidenceSource source) => source switch
    {
        IntakeEvidenceSource.Sender => "Sender",
        IntakeEvidenceSource.Subject => "Subject",
        IntakeEvidenceSource.FileName => "File name",
        IntakeEvidenceSource.MimeType => "File type",
        _ => "Transport metadata"
    };

    [GeneratedRegex(@"\bQDOS\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QdosMarkerRegex();
}

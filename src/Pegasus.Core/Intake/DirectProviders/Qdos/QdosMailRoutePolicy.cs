namespace Pegasus.Core.Intake;

/// <summary>
/// The QDOS direct-provider mail route: proves the provider route from the effective
/// sender (unwrapping a staff forward to the proved original sender). Route identity is a
/// different fact from message-type classification and case association, which stay with
/// their own policies.
/// </summary>
public sealed class QdosMailRoutePolicy : IMailRoutePolicy
{
    public const string Key = "qdos_mail_route";
    public const int Version = 2;
    private const string PrincipalCode = "QDOS";
    private const string AcceptedDirectDomain = "qdosassist.co.uk";
    private const string StaffTransportDomain = "collisionengineers.co.uk";

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
            Key,
            Version,
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
}

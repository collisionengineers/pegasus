using System.Collections.Immutable;

namespace Pegasus.Core.Intake;

/// <summary>
/// The evidenced principal mail routes: prove the provider route from the effective
/// sender (unwrapping a staff forward to the proved original sender). Route identity is a
/// different fact from message-type classification and case association, which stay with
/// their own policies.
/// </summary>
public sealed class PrincipalMailRoutePolicy : IMailRoutePolicy
{
    public const string Key = "principal_mail_route";
    public const int Version = 1;
    private const string StaffTransportDomain = "collisionengineers.co.uk";

    /// <summary>
    /// Exact direct sender identities from the supplied reference and original
    /// instructions. An identity containing @ is one mailbox, never its domain.
    /// This is the one runtime list, also read by customer Settings.
    /// </summary>
    public static readonly ImmutableDictionary<string, ImmutableArray<string>> AcceptedIdentities =
        new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal)
        {
            ["ALS"] = ["autologistic.co.uk"],
            ["AX"] = ["ax-uk.com"],
            ["BC"] = ["bakercoleman.co.uk"],
            ["BLACK"] = ["blackstone-legal.co.uk"],
            ["DFD"] = ["dfd-solicitors.co.uk"],
            ["FW"] = ["fairwaylegal.co.uk"],
            ["KBS"] = ["knightsbridgesolicitors.co.uk"],
            ["MP"] = ["montrealprestige.co.uk"],
            ["OAK"] = ["oakwoodscotland.co.uk", "oakwoodsolicitors.co.uk"],
            ["PCH"] = ["pch-ltd.com"],
            ["QCL"] = ["qc-law.co.uk"],
            ["QDOS"] = ["qdosassist.co.uk", "qdoslaw.co.uk", "qdosassists.co.uk"],
            ["RJS"] = ["robertjameslaw.co.uk"],
            ["SBL"] = ["smartbusinesslink.com"],
            ["YML"] = ["networkhduk@gmail.com"]
        }.ToImmutableDictionary(StringComparer.Ordinal);

    private static readonly ImmutableArray<string> PchIntermediaryDomains =
        ["connexus.co.uk", "ensurance-claims.co.uk"];

    /// <summary>
    /// Current document boundaries, including the proved original in a staff
    /// forward. An arbitrary attached old email is not a current instruction.
    /// Reuses the route's sender cardinality and the existing forward parser.
    /// </summary>
    internal static IEnumerable<IntakeContentFragment> CurrentInstructionContent(IntakeSourceReadResult readResult)
    {
        var transport = SenderIdentities(readResult.TransportEvidence, IntakeSenderIdentityKind.Transport);
        var originals = SenderIdentities(readResult.TransportEvidence,
            IntakeSenderIdentityKind.AttachedOriginal, IntakeSenderIdentityKind.InlineForwardedOriginal);
        var staffForward = transport.Length == 1 && originals.Length == 1
            && TryGetMailboxDomain(transport[0].Address, out var domain)
            && string.Equals(domain, StaffTransportDomain, StringComparison.OrdinalIgnoreCase);
        foreach (var fragment in readResult.Content)
        {
            var nested = fragment.SourceLabel.IndexOf(", attached email ", StringComparison.Ordinal);
            if (nested >= 0 && (!staffForward
                || !fragment.SourceLabel.StartsWith(originals[0].SourceLabel + ",", StringComparison.Ordinal)
                || fragment.SourceLabel.IndexOf(", attached email ", nested + 1, StringComparison.Ordinal) >= 0))
            {
                continue;
            }
            var forwardedBody = staffForward && fragment.Source == IntakeEvidenceSource.EmailBody
                && string.Equals(StaffForwardBodyCleaner.ForwardedSenderAddress(fragment.Text),
                    originals[0].Address, StringComparison.OrdinalIgnoreCase);
            if (fragment.Locator?.MessagePart == IntakeMessagePart.QuotedHistory && !forwardedBody)
            {
                continue;
            }
            if (forwardedBody)
            {
                var text = StaffForwardBodyCleaner.SplitForwardedHeader(
                    StaffForwardBodyCleaner.Clean(fragment.Text, isStaffForward: true)).Body;
                var olderMessage = StaffForwardBodyCleaner.ForwardedHeaderPattern.Match(text);
                yield return fragment with { Text = olderMessage.Success ? text[..olderMessage.Index] : text };
            }
            else
            {
                yield return fragment;
            }
        }
    }

    /// <summary>
    /// The effective sender for a retained message whose route decision
    /// intake processing has not written yet: the same staff-forward unwrap
    /// <see cref="Evaluate"/> performs, applied to the two facts retention
    /// already holds — the transport sender and the forwarded header inside
    /// the retained body. The rule lives here, beside the one it mirrors,
    /// so the two cannot drift; the route decision stays authoritative and
    /// supersedes this the moment it exists (MAIL-009).
    ///
    /// Fails closed. Anything that is not unambiguously a staff forward
    /// carrying a single external original sender yields null, so the
    /// surface shows a pending state rather than a wrong name.
    /// </summary>
    public static string? ProvisionalEffectiveSender(
        string? transportSenderAddress,
        string? retainedBodyPlainText)
    {
        if (string.IsNullOrWhiteSpace(transportSenderAddress)
            || string.IsNullOrWhiteSpace(retainedBodyPlainText)
            || !TryGetMailboxDomain(transportSenderAddress, out var transportDomain)
            || !string.Equals(
                transportDomain,
                StaffTransportDomain,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (StaffForwardBodyCleaner.ForwardedSenderAddress(retainedBodyPlainText)
                is not { } originalSender
            || !TryGetMailboxDomain(originalSender, out var originalDomain)
            || string.Equals(
                originalDomain,
                StaffTransportDomain,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return originalSender;
    }

    public MailRouteEvaluationResult Evaluate(
        IntakeSourceReadResult readResult,
        InstructionPolicySelection? instruction = null)
    {
        EnsureReadable(readResult);

        var transportIdentities = SenderIdentities(
            readResult.TransportEvidence,
            IntakeSenderIdentityKind.Transport);
        var originalIdentities = SenderIdentities(
            readResult.TransportEvidence,
            IntakeSenderIdentityKind.AttachedOriginal,
            IntakeSenderIdentityKind.InlineForwardedOriginal);
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
        var directMatch = AcceptedIdentities
            .SelectMany(entry => entry.Value.Select(identity => (Principal: entry.Key, Identity: identity)))
            .SingleOrDefault(entry => hasValidEffectiveSender && string.Equals(
                entry.Identity.Contains('@') ? effectiveSender!.Address : effectiveDomain,
                entry.Identity, StringComparison.OrdinalIgnoreCase));
        var matchedPrincipal = directMatch.Principal;
        var matchesDirectIdentity = matchedPrincipal is not null;
        var matchesIntermediary = hasValidEffectiveSender
            && PchIntermediaryDomains.Contains(effectiveDomain, StringComparer.OrdinalIgnoreCase)
            && instruction is { Outcome: InstructionPolicySelectionOutcome.Selected, Policy.PrincipalCode: "PCH" };

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
                    ? "Exactly one original sender address was supplied."
                    : $"Expected one original sender address for a staff forward; found {originalIdentities.Length}."),
            new(
                "forward.original-external",
                hasExternalOriginalSender,
                hasExternalOriginalSender
                    ? "The original sender is external to Collision Engineers."
                    : "No unambiguous external original sender was proved."),
            new(
                "direct.principal-identity",
                matchesDirectIdentity,
                matchesDirectIdentity
                    ? $"The effective sender uses the accepted identity '{directMatch.Identity}' for '{matchedPrincipal}'."
                    : "The effective sender does not match an accepted direct principal identity."),
            new(
                "intermediary.accepted-policy",
                matchesIntermediary,
                matchesIntermediary
                    ? "The evidenced intermediary and unique PCH instruction profile agree."
                    : "No evidenced intermediary with an agreeing instruction profile was proved.")
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
                "A staff-forwarded message requires exactly one consistent original sender.",
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
                "The original sender address is malformed.",
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
                "The original sender does not prove an external mail route.",
                transportIdentities,
                originalIdentities,
                null);
        }

        if (!matchesDirectIdentity && !matchesIntermediary)
        {
            return Result(
                MailRouteDisposition.NoMatch,
                null,
                predicates,
                "The effective sender does not match an accepted principal mail route.",
                transportIdentities,
                originalIdentities,
                effectiveSender);
        }

        return Result(
            MailRouteDisposition.Accepted,
            matchesDirectIdentity
                ? new(matchedPrincipal!, MailRouteKind.DirectProvider, matchedPrincipal!)
                : new(effectiveDomain, MailRouteKind.Intermediary, "PCH"),
            predicates,
            "The effective sender matches an evidenced principal mail route.",
            transportIdentities,
            originalIdentities,
            effectiveSender);
    }

    private static MailRouteIdentity[] SenderIdentities(
        IReadOnlyList<IntakeTransportEvidence> transportEvidence,
        params IntakeSenderIdentityKind[] kinds) =>
        transportEvidence
            .Where(item =>
                item.Source == IntakeEvidenceSource.Sender
                && kinds.Contains(item.SenderIdentityKind))
            .Select(item => new MailRouteIdentity(
                item.Value.Trim(),
                string.IsNullOrWhiteSpace(item.SourceLabel)
                    ? kinds.Length == 1 && kinds[0] == IntakeSenderIdentityKind.Transport
                        ? "outer message"
                        : "original message"
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
                "Mail routing accepts only fully readable, complete reader results.",
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

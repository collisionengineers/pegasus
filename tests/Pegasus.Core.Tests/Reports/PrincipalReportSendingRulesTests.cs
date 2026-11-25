using System.Text.Json;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Core.Tests.Reports;

/// <summary>
/// The per-Principal report sending rules, built as an every-Case list and
/// "If … Then …" rules (operator, 25 November 2026; R1267): what Normalize
/// accepts and refuses, how two values compare and how they are stored.
/// </summary>
public sealed class PrincipalReportSendingRulesTests
{
    private const string ContactId = "00000000-0000-4000-8000-00000000f001";
    private const string OtherContactId = "00000000-0000-4000-8000-00000000f002";

    private static PrincipalReportSendingRules Rules() => PrincipalReportSendingRules.Default;

    private static ReportSendingCondition Is(ReportSendingFact fact, IReadOnlyList<string> values) => new(fact, values);

    private static ReportSendingCondition IsNot(ReportSendingFact fact, IReadOnlyList<string> values) => new(fact, values, Not: true);

    private static ReportSendingAction Cc(string address) => new(ReportSendingActionKind.Cc, [address]);

    private static ReportSendingAction AddTask(string text) => new(ReportSendingActionKind.Task, Text: text);

    private static ReportSendingRule Rule(ReportSendingCondition condition, ReportSendingAction? then = null) =>
        new([[condition]], [then ?? Cc("cc@example.com")]);

    private static ReportSendingRule Outcome(ReportSendingAction then) =>
        Rule(Is(ReportSendingFact.Outcome, ["repairable"]), then);

    private static PrincipalReportSendingRules With(IReadOnlyList<ReportSendingRule> rules) => Rules() with { Rules = rules };

    private static PrincipalReportSendingRules EveryCase(IReadOnlyList<ReportSendingAction> actions) =>
        new(actions, []);

    private static ReportSendingRulesException Refused(PrincipalReportSendingRules rules) =>
        Assert.Throws<ReportSendingRulesException>(() => PrincipalReportSendingRules.Normalize(rules));

    [Fact]
    public void TheDefaultRepliesToAllWithTheFeeNoteSeparateAndNormalizesToItself()
    {
        var normalized = PrincipalReportSendingRules.Normalize(PrincipalReportSendingRules.Default);

        Assert.Equal(PrincipalReportSendingRules.Default, normalized);
        Assert.Equal(
            [ReportSendingActionKind.ReplyAll, ReportSendingActionKind.Attach],
            normalized.EveryCase.Select(action => action.Kind).ToArray());
        Assert.Equal(ReportSendingFile.FeeNoteSeparate, normalized.EveryCase[1].File);
        Assert.Empty(normalized.Rules);
    }

    [Fact]
    public void AnEmptyEveryCaseListIsKept()
    {
        var normalized = PrincipalReportSendingRules.Normalize(new([], []));

        Assert.Empty(normalized.EveryCase);
        Assert.Empty(normalized.Rules);
        Assert.NotEqual(PrincipalReportSendingRules.Default, normalized);
    }

    [Fact]
    public void AddressesAreTrimmedDroppedWhenBlankAndUniqueWithoutCase()
    {
        var normalized = PrincipalReportSendingRules.Normalize(EveryCase([
            new(ReportSendingActionKind.SendTo, [" A@example.com ", "", "a@EXAMPLE.com", "b@example.com"]),
            new(ReportSendingActionKind.Cc, ["c@example.com"]),
            new(ReportSendingActionKind.NeverCc, ["d@example.com"])]));

        Assert.Equal(["A@example.com", "b@example.com"], normalized.EveryCase[0].Addresses!.ToArray());
        Assert.Equal("c@example.com", Assert.Single(normalized.EveryCase[1].Addresses!));
        Assert.Equal("d@example.com", Assert.Single(normalized.EveryCase[2].Addresses!));
    }

    [Theory]
    [InlineData("SendTo", ReportSendingActionKind.SendTo)]
    [InlineData("Cc", ReportSendingActionKind.Cc)]
    [InlineData("NeverCc", ReportSendingActionKind.NeverCc)]
    public void AnAddressActionRefusesAnInvalidAddressAndNoAddress(string name, ReportSendingActionKind kind)
    {
        foreach (var addresses in new IReadOnlyList<string>?[] { ["not an address"], ["a@"], [], [" "], null })
        {
            var everyCase = Refused(EveryCase([new(kind, addresses)]));
            Assert.Equal(ReportSendingRulesRule.InvalidAddress, everyCase.Rule);
            Assert.Null(everyCase.RuleIndex);
            Assert.Equal(name, everyCase.Field);

            var inRule = Refused(With([Outcome(AddTask("ok")), Outcome(new(kind, addresses))]));
            Assert.Equal(ReportSendingRulesRule.InvalidAddress, inRule.Rule);
            Assert.Equal(1, inRule.RuleIndex);
            Assert.Equal(name, inRule.Field);
        }
    }

    [Fact]
    public void AnActionCarriesOnlyWhatItsKindUses()
    {
        var normalized = PrincipalReportSendingRules.Normalize(EveryCase([
            new(ReportSendingActionKind.SendTo, ["a@example.com"], ReportSendingFile.Estimate, "text"),
            new(ReportSendingActionKind.SendToOnly, ["a@example.com"], ReportSendingFile.Estimate, "text"),
            new(ReportSendingActionKind.ReplyAll, ["a@example.com"], ReportSendingFile.Estimate, "text"),
            new(ReportSendingActionKind.Attach, ["a@example.com"], ReportSendingFile.Estimate, "text"),
            new(ReportSendingActionKind.Hold, ["a@example.com"], ReportSendingFile.Estimate, " Hold. ")]));

        Assert.All(normalized.EveryCase.Skip(1).Take(2), action =>
        {
            Assert.Null(action.Addresses);
            Assert.Null(action.File);
            Assert.Null(action.Text);
        });
        Assert.Null(normalized.EveryCase[0].File);
        Assert.Null(normalized.EveryCase[0].Text);
        Assert.Null(normalized.EveryCase[3].Addresses);
        Assert.Null(normalized.EveryCase[3].Text);
        Assert.Equal(ReportSendingFile.Estimate, normalized.EveryCase[3].File);
        Assert.Null(normalized.EveryCase[4].Addresses);
        Assert.Null(normalized.EveryCase[4].File);
        Assert.Equal("Hold.", normalized.EveryCase[4].Text);
    }

    [Theory]
    [InlineData("Attach", ReportSendingActionKind.Attach)]
    [InlineData("DoNotAttach", ReportSendingActionKind.DoNotAttach)]
    public void AnAttachActionNeedsAFile(string name, ReportSendingActionKind kind)
    {
        var missing = Refused(EveryCase([new(kind)]));
        Assert.Equal(ReportSendingRulesRule.EmptyActions, missing.Rule);
        Assert.Equal(name, missing.Field);
        Assert.Null(missing.RuleIndex);

        var undefined = Refused(With([Outcome(new(kind, File: (ReportSendingFile)99))]));
        Assert.Equal(ReportSendingRulesRule.EmptyActions, undefined.Rule);
        Assert.Equal(0, undefined.RuleIndex);
    }

    [Theory]
    [InlineData("Hold", ReportSendingActionKind.Hold)]
    [InlineData("Stop", ReportSendingActionKind.Stop)]
    [InlineData("Task", ReportSendingActionKind.Task)]
    public void ATextActionNeedsTextAndKeepsLongTextTrimmed(string name, ReportSendingActionKind kind)
    {
        foreach (var text in new[] { null, "", "  " })
        {
            var refused = Refused(EveryCase([new(kind, Text: text)]));
            Assert.Equal(ReportSendingRulesRule.TextRequired, refused.Rule);
            Assert.Equal(name, refused.Field);
        }

        var longText = new string('x', 501);
        Assert.Equal(longText, Assert.Single(PrincipalReportSendingRules.Normalize(EveryCase([new(kind, Text: $" {longText} ")])).EveryCase).Text);
    }

    [Fact]
    public void AnUndefinedActionKindIsRefused()
    {
        Assert.Equal(
            ReportSendingRulesRule.EmptyActions,
            Refused(EveryCase([new((ReportSendingActionKind)99)])).Rule);
    }

    [Fact]
    public void SendToOnlyNeedsASendToInItsListOrForARuleOnEveryCase()
    {
        var everyCase = Refused(EveryCase([new(ReportSendingActionKind.SendToOnly)]));
        Assert.Equal(ReportSendingRulesRule.SendToOnlyNeedsAddresses, everyCase.Rule);
        Assert.Null(everyCase.RuleIndex);
        Assert.Equal(nameof(ReportSendingActionKind.SendTo), everyCase.Field);

        var inRule = Refused(With([Outcome(new(ReportSendingActionKind.SendToOnly))]));
        Assert.Equal(ReportSendingRulesRule.SendToOnlyNeedsAddresses, inRule.Rule);
        Assert.Equal(0, inRule.RuleIndex);

        // A Send to in the same list, or for a rule on every Case, is enough.
        _ = PrincipalReportSendingRules.Normalize(EveryCase([
            new(ReportSendingActionKind.SendTo, ["a@example.com"]),
            new(ReportSendingActionKind.SendToOnly)]));
        _ = PrincipalReportSendingRules.Normalize(new(
            [],
            [new([[Is(ReportSendingFact.Outcome, ["repairable"])]],
                [new(ReportSendingActionKind.SendToOnly), new(ReportSendingActionKind.SendTo, ["a@example.com"])])]));
        _ = PrincipalReportSendingRules.Normalize(new(
            [new(ReportSendingActionKind.SendTo, ["a@example.com"])],
            [Outcome(new(ReportSendingActionKind.SendToOnly))]));
    }

    [Fact]
    public void AnAddressCannotBeCopiedAndNeverCopiedInOneList()
    {
        var everyCase = Refused(EveryCase([
            new(ReportSendingActionKind.Cc, ["a@example.com"]),
            new(ReportSendingActionKind.NeverCc, ["A@EXAMPLE.com"])]));
        Assert.Equal(ReportSendingRulesRule.AddressInCcAndNeverCc, everyCase.Rule);
        Assert.Null(everyCase.RuleIndex);
        Assert.Equal(nameof(ReportSendingActionKind.NeverCc), everyCase.Field);

        var inRule = Refused(With([new([[Is(ReportSendingFact.Outcome, ["repairable"])]],
            [Cc("a@example.com"), new(ReportSendingActionKind.NeverCc, ["a@example.com"])])]));
        Assert.Equal(ReportSendingRulesRule.AddressInCcAndNeverCc, inRule.Rule);
        Assert.Equal(0, inRule.RuleIndex);

        // Two lists may disagree: a rule may keep out what every Case copies.
        _ = PrincipalReportSendingRules.Normalize(new(
            [Cc("a@example.com")],
            [Outcome(new(ReportSendingActionKind.NeverCc, ["a@example.com"]))]));
    }

    [Fact]
    public void ContactConditionsTakeContactIdsAndKeepThemInOneForm()
    {
        foreach (var fact in new[] { ReportSendingFact.ClaimSource, ReportSendingFact.Repairer })
        {
            var normalized = PrincipalReportSendingRules.Normalize(With([
                Rule(Is(fact, [ContactId.ToUpperInvariant(), ContactId, OtherContactId]))]));

            var condition = Assert.Single(Assert.Single(Assert.Single(normalized.Rules).If));
            Assert.Equal(fact, condition.Fact);
            Assert.Equal([ContactId, OtherContactId], condition.Values.ToArray());

            var named = Refused(With([Rule(Is(fact, ["Car 2 Go"]))]));
            Assert.Equal(ReportSendingRulesRule.UnknownContact, named.Rule);
            Assert.Equal(fact.ToString(), named.Field);
            Assert.Equal(
                ReportSendingRulesRule.UnknownContact,
                Refused(With([Rule(Is(fact, [Guid.Empty.ToString()]))])).Rule);
        }
    }

    [Fact]
    public void OutcomeConditionsTakeTheFourCodesOnly()
    {
        foreach (var code in new[] { "total_loss", "repairable", "cash_in_lieu", "contract_repair" })
        {
            var normalized = PrincipalReportSendingRules.Normalize(With([Rule(Is(ReportSendingFact.Outcome, [code]))]));
            Assert.Equal(code, Assert.Single(Assert.Single(Assert.Single(Assert.Single(normalized.Rules).If)).Values));
        }

        var error = Refused(With([Rule(Is(ReportSendingFact.Outcome, ["Repairable"]))]));
        Assert.Equal(ReportSendingRulesRule.UnknownOutcome, error.Rule);
        Assert.Equal(0, error.RuleIndex);
    }

    /// <summary>A Case type condition takes the types that send a report, by their Core names (operator, 13 October 2026).</summary>
    [Fact]
    public void CaseTypeConditionsTakeTheReportingCaseTypesOnly()
    {
        foreach (var type in new[] { "Inspection", "Audit", "InspectionAndAudit" })
        {
            var normalized = PrincipalReportSendingRules.Normalize(With([Rule(Is(ReportSendingFact.CaseType, [type]))]));
            Assert.Equal(type, Assert.Single(Assert.Single(Assert.Single(Assert.Single(normalized.Rules).If)).Values));
        }

        foreach (var unknown in new[] { "Triage", "audit", "Desktop" })
        {
            var error = Refused(With([Rule(Is(ReportSendingFact.CaseType, [unknown]))]));
            Assert.Equal(ReportSendingRulesRule.UnknownCaseType, error.Rule);
            Assert.Equal(0, error.RuleIndex);
        }
    }

    [Fact]
    public void SenderConditionsTakeAddresses()
    {
        var sender = PrincipalReportSendingRules.Normalize(With([Rule(IsNot(ReportSendingFact.Sender, [" a@example.com ", "A@example.com"]))]));
        var condition = Assert.Single(Assert.Single(Assert.Single(sender.Rules).If));
        Assert.Equal("a@example.com", Assert.Single(condition.Values));
        Assert.True(condition.Not);

        var refused = Refused(With([Rule(Is(ReportSendingFact.Sender, ["Luton"]))]));
        Assert.Equal(ReportSendingRulesRule.InvalidAddress, refused.Rule);
        Assert.Equal(nameof(ReportSendingFact.Sender), refused.Field);
    }

    /// <summary>A reference prefix is letters only, without the dot (Report Sending SOP v8).</summary>
    [Fact]
    public void ReferencePrefixesAreLettersTrimmedAndUniqueWithoutCase()
    {
        var normalized = PrincipalReportSendingRules.Normalize(With([Rule(Is(ReportSendingFact.RefPrefix, [" a ", "A", "ab"]))]));
        Assert.Equal(["a", "ab"], Assert.Single(Assert.Single(Assert.Single(normalized.Rules).If)).Values.ToArray());

        foreach (var refused in new[] { "a.", "a1", "a b" })
        {
            var error = Refused(With([
                Rule(Is(ReportSendingFact.Outcome, ["repairable"])),
                Rule(Is(ReportSendingFact.RefPrefix, [refused]))]));
            Assert.Equal(ReportSendingRulesRule.InvalidRefPrefix, error.Rule);
            Assert.Equal(1, error.RuleIndex);
        }
    }

    /// <summary>A previous job takes no value, so one with none is kept and any typed value is dropped; "is not" is kept.</summary>
    [Fact]
    public void APreviousJobConditionIsKeptWithNoValueAndItsNot()
    {
        var normalized = PrincipalReportSendingRules.Normalize(With([
            Rule(Is(ReportSendingFact.PreviousJob, [])),
            Rule(IsNot(ReportSendingFact.PreviousJob, ["typed"]))]));

        var first = Assert.Single(Assert.Single(normalized.Rules[0].If));
        var second = Assert.Single(Assert.Single(normalized.Rules[1].If));
        Assert.Empty(first.Values);
        Assert.False(first.Not);
        Assert.Empty(second.Values);
        Assert.True(second.Not);
    }

    [Fact]
    public void EveryRuleNeedsAConditionWithAValueAndAnAction()
    {
        var noGroups = Refused(With([new([], [Cc("a@example.com")])]));
        Assert.Equal(ReportSendingRulesRule.EmptyRule, noGroups.Rule);
        Assert.Equal(0, noGroups.RuleIndex);

        // Empty groups are dropped first, so a rule of only empty groups has no condition.
        var emptyGroups = Refused(With([new([[], []], [Cc("a@example.com")])]));
        Assert.Equal(ReportSendingRulesRule.EmptyRule, emptyGroups.Rule);

        var noValue = Refused(With([Rule(Is(ReportSendingFact.Outcome, [" ", ""]))]));
        Assert.Equal(ReportSendingRulesRule.EmptyCondition, noValue.Rule);
        Assert.Equal(nameof(ReportSendingFact.Outcome), noValue.Field);

        var noAction = Refused(With([Rule(Is(ReportSendingFact.Outcome, ["repairable"])), new([[Is(ReportSendingFact.Outcome, ["repairable"])]], [])]));
        Assert.Equal(ReportSendingRulesRule.EmptyActions, noAction.Rule);
        Assert.Equal(1, noAction.RuleIndex);
    }

    [Fact]
    public void EmptyGroupsAreDroppedAndTheRestKeptInOrder()
    {
        var normalized = PrincipalReportSendingRules.Normalize(With([new(
            [[], [Is(ReportSendingFact.Outcome, ["repairable"]), IsNot(ReportSendingFact.PreviousJob, [])], [], [Is(ReportSendingFact.RefPrefix, ["a"])]],
            [Cc("a@example.com"), AddTask(" Tell Andy. ")])]));

        var rule = Assert.Single(normalized.Rules);
        Assert.Equal(2, rule.If.Count);
        Assert.Equal(
            [ReportSendingFact.Outcome, ReportSendingFact.PreviousJob],
            rule.If[0].Select(condition => condition.Fact).ToArray());
        Assert.Equal(ReportSendingFact.RefPrefix, Assert.Single(rule.If[1]).Fact);
        Assert.Equal("Tell Andy.", rule.Then[1].Text);
    }

    [Fact]
    public void NormalizeIsIdempotent()
    {
        var once = PrincipalReportSendingRules.Normalize(new(
            [new(ReportSendingActionKind.SendTo, [" a@example.com "]), Cc("B@example.com")],
            [Rule(Is(ReportSendingFact.ClaimSource, [ContactId.ToUpperInvariant()]))]));

        Assert.Equal(once, PrincipalReportSendingRules.Normalize(once));
        Assert.Equal(once.Canonical(), PrincipalReportSendingRules.Normalize(once).Canonical());
    }

    [Fact]
    public void ValuesCompareByContentAndAddressesWithoutCase()
    {
        var one = new PrincipalReportSendingRules([Cc("a@example.com")], [Rule(Is(ReportSendingFact.RefPrefix, ["a"]))]);
        var same = new PrincipalReportSendingRules([Cc("A@EXAMPLE.com")], [Rule(Is(ReportSendingFact.RefPrefix, ["a"]))]);
        var different = new PrincipalReportSendingRules([Cc("a@example.com")], [Rule(Is(ReportSendingFact.RefPrefix, ["b"]))]);

        Assert.Equal(one, same);
        Assert.Equal(one.GetHashCode(), same.GetHashCode());
        Assert.Equal(one.Canonical(), same.Canonical());
        Assert.NotEqual(one, different);
        Assert.NotEqual(one.Canonical(), different.Canonical());
        Assert.False(one.Equals(null));
    }

    [Fact]
    public void EveryPartMovesTheCanonicalText()
    {
        var outcome = Is(ReportSendingFact.Outcome, ["repairable"]);
        var refPrefix = Is(ReportSendingFact.RefPrefix, ["a"]);
        var baseline = new PrincipalReportSendingRules([Cc("a@example.com")], [new([[outcome]], [AddTask("One.")])]);
        var changes = new[]
        {
            baseline with { EveryCase = [] },
            baseline with { EveryCase = [Cc("b@example.com")] },
            baseline with { EveryCase = [new(ReportSendingActionKind.NeverCc, ["a@example.com"])] },
            baseline with { EveryCase = [Cc("a@example.com"), new(ReportSendingActionKind.ReplyAll)] },
            baseline with { EveryCase = [new(ReportSendingActionKind.ReplyAll), Cc("a@example.com")] },
            baseline with { Rules = [] },
            baseline with { Rules = [new([[IsNot(ReportSendingFact.Outcome, ["repairable"])]], [AddTask("One.")])] },
            baseline with { Rules = [new([[outcome, refPrefix]], [AddTask("One.")])] },
            baseline with { Rules = [new([[outcome], [refPrefix]], [AddTask("One.")])] },
            baseline with { Rules = [new([[refPrefix], [outcome]], [AddTask("One.")])] },
            baseline with { Rules = [new([[outcome]], [AddTask("Two.")])] },
            baseline with { Rules = [new([[outcome]], [new(ReportSendingActionKind.Hold, Text: "One.")])] },
            baseline with { Rules = [new([[outcome]], [new(ReportSendingActionKind.Attach, File: ReportSendingFile.Estimate)])] },
            baseline with { Rules = [new([[outcome]], [new(ReportSendingActionKind.DoNotAttach, File: ReportSendingFile.Estimate)])] },
            baseline with { Rules = [new([[outcome]], [new(ReportSendingActionKind.Attach, File: ReportSendingFile.Audatex)])] },
            baseline with { Rules = [new([[outcome]], [AddTask("One.")]), new([[outcome]], [AddTask("One.")])] }
        };

        Assert.All(changes, changed => Assert.NotEqual(baseline.Canonical(), changed.Canonical()));
        Assert.Equal(changes.Length, changes.Select(changed => changed.Canonical()).Distinct().Count());
    }

    [Fact]
    public void NamesAndMayAttachReadEveryList()
    {
        var rules = new PrincipalReportSendingRules(
            [new(ReportSendingActionKind.Attach, File: ReportSendingFile.OfferExternalEstimates)],
            [
                new([[Is(ReportSendingFact.Outcome, ["repairable"])], [IsNot(ReportSendingFact.PreviousJob, [])]],
                    [new(ReportSendingActionKind.Attach, File: ReportSendingFile.OfferRepairInvoices),
                     new(ReportSendingActionKind.DoNotAttach, File: ReportSendingFile.Estimate)])
            ]);

        Assert.True(rules.Names(ReportSendingFact.Outcome));
        Assert.True(rules.Names(ReportSendingFact.PreviousJob));
        Assert.False(rules.Names(ReportSendingFact.ClaimSource));
        Assert.True(rules.MayAttach(ReportSendingFile.OfferExternalEstimates));
        Assert.True(rules.MayAttach(ReportSendingFile.OfferRepairInvoices));
        // Do not attach never attaches.
        Assert.False(rules.MayAttach(ReportSendingFile.Estimate));
        Assert.False(PrincipalReportSendingRules.Default.Names(ReportSendingFact.PreviousJob));
        Assert.True(PrincipalReportSendingRules.Default.MayAttach(ReportSendingFile.FeeNoteSeparate));
    }

    /// <summary>The stored text is the built shape: names for every enum, nothing a value does not carry.</summary>
    [Fact]
    public void TheStoredTextIsTheBuiltShapeAndRoundTrips()
    {
        Assert.Equal(
            """{"everyCase":[{"kind":"ReplyAll"},{"kind":"Attach","file":"FeeNoteSeparate"}],"rules":[]}""",
            EfOrganizationAdministration.ToReportSendingJson(PrincipalReportSendingRules.Default));
        Assert.Equal(
            EfOrganizationAdministration.ToReportSendingJson(PrincipalReportSendingRules.Default),
            EfOrganizationAdministration.DefaultReportSendingJson);

        var built = new PrincipalReportSendingRules(
            [
                new(ReportSendingActionKind.ReplyAll),
                new(ReportSendingActionKind.Attach, File: ReportSendingFile.FeeNoteSeparate),
                Cc("a@b.c")
            ],
            [
                new(
                    [[Is(ReportSendingFact.Repairer, [ContactId]), Is(ReportSendingFact.Outcome, ["repairable"])], [IsNot(ReportSendingFact.PreviousJob, [])]],
                    [AddTask("Authorise the garage")])
            ]);
        var json = EfOrganizationAdministration.ToReportSendingJson(built);

        Assert.Equal(
            $$"""{"everyCase":[{"kind":"ReplyAll"},{"kind":"Attach","file":"FeeNoteSeparate"},{"kind":"Cc","addresses":["a@b.c"]}],"rules":[{"if":[[{"fact":"Repairer","values":["{{ContactId}}"]},{"fact":"Outcome","values":["repairable"]}],[{"fact":"PreviousJob","values":[],"not":true}]],"then":[{"kind":"Task","text":"Authorise the garage"}]}]}""",
            json);
        Assert.Equal(built, EfOrganizationAdministration.ReadReportSending(json));
    }

    /// <summary>
    /// History and receipts are written with the general serializer, which
    /// writes enums as numbers; the rules' enums carry their own names.
    /// </summary>
    [Fact]
    public void HistoryWritesTheRulesEnumsByName()
    {
        var rules = new PrincipalReportSendingRules(
            [new(ReportSendingActionKind.Attach, File: ReportSendingFile.Audatex)],
            [Rule(IsNot(ReportSendingFact.Sender, ["a@example.com"]), AddTask("One."))]);

        var json = JsonSerializer.Serialize(rules, EfOrganizationAdministration.SerializerOptions);

        Assert.Contains("\"kind\":\"Attach\"", json, StringComparison.Ordinal);
        Assert.Contains("\"file\":\"Audatex\"", json, StringComparison.Ordinal);
        Assert.Contains("\"fact\":\"Sender\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("null", json, StringComparison.Ordinal);
        Assert.Equal(rules, JsonSerializer.Deserialize<PrincipalReportSendingRules>(json, EfOrganizationAdministration.SerializerOptions));
    }

    /// <summary>No reader for the earlier shape remains (R1267): its text fails as JSON.</summary>
    [Fact]
    public void TheEarlierShapeIsNotRead()
    {
        Assert.ThrowsAny<JsonException>(() => EfOrganizationAdministration.ReadReportSending(
            """{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}"""));
        Assert.ThrowsAny<JsonException>(() => EfOrganizationAdministration.ReadReportSending(
            """{"everyCase":[],"rules":[{"match":"All","if":[{"kind":"Outcome","values":["repairable"]}],"then":{"ccAdd":["a@example.com"],"ccRemove":[]}}]}"""));
    }
}

using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

/// <summary>
/// The one report dispatch plan (Report Sending SOP v8, rule R12; operator,
/// 6 October 2026), built from an every-Case list and "If … Then …" rules
/// (operator, 25 November 2026; R1267): nothing happens unless an action says
/// so, the every-Case actions apply first and then each applying rule's in
/// order, an "is" condition reads only what the Case holds (R1024) and its
/// "is not" holds on an unset fact (R1269), removals win, the first Stop is
/// kept and tasks are created after sending (R1270). Pure: every case here is
/// facts in, plan out.
/// </summary>
public sealed class ReportDispatchPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid MailboxId = Guid.NewGuid();
    private static readonly Guid RetainedId = Guid.NewGuid();
    private static readonly Guid CarId = Guid.NewGuid();
    private static readonly Guid SmcId = Guid.NewGuid();
    private static readonly Guid EasdonsId = Guid.NewGuid();

    private const string Sender = "handler@insurer.example";

    // ---- R12: recipients ----------------------------------------------------

    [Fact]
    public void TheDefaultRulesReplyToTheOriginalSenderAndKeepTheInstructionsCopies()
    {
        var plan = Plan(Rules(), instruction: Instruction(cc: ["a@insurer.example", "b@insurer.example"]));

        var to = Assert.Single(plan.To);
        Assert.Equal(Sender, to.Address);
        Assert.Equal("Original sender", to.Source);
        Assert.Equal(["a@insurer.example", "b@insurer.example"], plan.Cc.Select(copy => copy.Address));
        Assert.All(plan.Cc, copy => Assert.Equal("Instruction Cc", copy.Source));
        Assert.Empty(plan.Removed);
        Assert.Empty(plan.Excluded);
        Assert.Equal(new ReportSendingAttachments(FeeNoteSeparate: true), plan.Attach);
    }

    /// <summary>
    /// Nothing happens that no action says: with an empty every-Case list the
    /// report still goes to the original sender, but no copy is kept and
    /// nothing is attached, so no separate fee note is expected.
    /// </summary>
    [Fact]
    public void AnEmptyEveryCaseListKeepsNoCopyAndAttachesNothing()
    {
        var plan = Plan(Only([]), instruction: Instruction(cc: ["a@insurer.example"]));

        Assert.Equal(Sender, Assert.Single(plan.To).Address);
        Assert.Empty(plan.Cc);
        Assert.Equal(ReportSendingAttachments.None, plan.Attach);
        Assert.Empty(plan.Warnings);
        Assert.Empty(plan.Holds);
        Assert.Null(plan.Stop);
        Assert.Empty(plan.AfterSendTasks);
    }

    [Fact]
    public void ASendToOnEveryCaseReplacesTheOriginalSender()
    {
        var plan = Plan(Every([SendTo(["fixed@principal.example", "other@principal.example"])]));

        Assert.Equal(["fixed@principal.example", "other@principal.example"], plan.To.Select(item => item.Address));
        Assert.All(plan.To, item => Assert.Equal(ReportDispatchPolicy.EveryCaseSource, item.Source));
        Assert.Equal("On every Case", ReportDispatchPolicy.EveryCaseSource);
    }

    /// <summary>
    /// A later Send to replaces the To list (R1272 item O): a rule's replaces
    /// the every-Case one, and a later applying rule's replaces an earlier
    /// one's; a rule that does not apply changes nothing.
    /// </summary>
    [Fact]
    public void ALaterSendToReplacesTheToList()
    {
        var rules = Every(
            [SendTo(["every@principal.example"])],
            [
                Rule(ClaimSource(CarId), [SendTo(["first@rule.example", "second@rule.example"])]),
                Rule(ClaimSource(SmcId), [SendTo(["not@rule.example"])]),
                Rule(ClaimSource(CarId), [SendTo(["last@rule.example"])])
            ]);

        var applied = Plan(rules, claimSource: CarId, claimSourceName: "Car 2 Go");
        var none = Plan(rules, claimSource: EasdonsId);

        var to = Assert.Single(applied.To);
        Assert.Equal("last@rule.example", to.Address);
        Assert.Equal("Rule 3: Claim Source Car 2 Go", to.Source);
        Assert.Equal("every@principal.example", Assert.Single(none.To).Address);
    }

    /// <summary>
    /// A Case with no mailbox instruction (an uploaded e-mail) still addresses
    /// the sender of the instruction that opened it.
    /// </summary>
    [Fact]
    public void WithoutAMailboxInstructionTheUploadedInstructionsSenderIsAddressed()
    {
        var facts = Facts(Every([Cc(["always@principal.example"])]), withInstruction: false)
            with { OriginalSender = "uploaded@insurer.example" };

        var plan = ReportDispatchPolicy.Plan(facts, Now);

        var to = Assert.Single(plan.To);
        Assert.Equal("uploaded@insurer.example", to.Address);
        Assert.Equal("Original sender", to.Source);
        // Reply all keeps an instruction's copies; with no instruction only the every-Case Cc remains.
        Assert.Equal("always@principal.example", Assert.Single(plan.Cc).Address);
    }

    [Fact]
    public void SendToOnlyEmptiesEveryCopy()
    {
        var plan = Plan(
            Every(
                [SendTo(["fixed@principal.example"]), new(ReportSendingActionKind.SendToOnly), Cc(["always@principal.example"])],
                [Rule(ClaimSource(CarId), [Cc(["car@rule.example"])])]),
            instruction: Instruction(cc: ["a@insurer.example"]),
            claimSource: CarId);

        Assert.Empty(plan.Cc);
        Assert.Equal("fixed@principal.example", Assert.Single(plan.To).Address);
    }

    /// <summary>A rule's Send to only switches every copy off for that send alone.</summary>
    [Fact]
    public void ARulesSendToOnlyEmptiesEveryCopyWhenItApplies()
    {
        var rules = Every(
            [SendTo(["fixed@principal.example"]), Cc(["always@principal.example"])],
            [Rule(ClaimSource(CarId), [new(ReportSendingActionKind.SendToOnly)])]);

        Assert.Empty(Plan(rules, claimSource: CarId).Cc);
        Assert.Equal(2, Plan(rules, instruction: Instruction(cc: ["a@insurer.example"]), claimSource: SmcId).Cc.Count);
    }

    [Fact]
    public void WithoutReplyAllTheInstructionsCopiesAreDropped()
    {
        var plan = Plan(
            Only([Cc(["always@principal.example"])]),
            instruction: Instruction(cc: ["a@insurer.example"]));

        var copy = Assert.Single(plan.Cc);
        Assert.Equal("always@principal.example", copy.Address);
        Assert.Equal("On every Case", copy.Source);
    }

    /// <summary>A rule's Reply all keeps the instruction's copies for that send alone.</summary>
    [Fact]
    public void ARulesReplyAllKeepsTheInstructionsCopiesWhenItApplies()
    {
        var rules = Only([], [Rule(ClaimSource(CarId), [new(ReportSendingActionKind.ReplyAll)])]);

        Assert.Equal(
            "a@insurer.example",
            Assert.Single(Plan(rules, instruction: Instruction(cc: ["a@insurer.example"]), claimSource: CarId).Cc).Address);
        Assert.Empty(Plan(rules, instruction: Instruction(cc: ["a@insurer.example"]), claimSource: SmcId).Cc);
    }

    [Fact]
    public void CopiesComeFromTheInstructionEveryCaseAndEveryRuleThatApplies()
    {
        var rules = Every(
            [Cc(["principal@principal.example"])],
            [
                Rule(ClaimSource(CarId), [Cc(["car@rule.example"])]),
                Rule(ClaimSource(SmcId), [Cc(["smc@rule.example"])])
            ]);

        var plan = Plan(rules, instruction: Instruction(cc: ["a@insurer.example"]), claimSource: CarId, claimSourceName: "Car 2 Go");

        Assert.Equal(
            ["a@insurer.example", "principal@principal.example", "car@rule.example"],
            plan.Cc.Select(copy => copy.Address));
        Assert.Equal(
            ["Instruction Cc", "On every Case", "Rule 1: Claim Source Car 2 Go"],
            plan.Cc.Select(copy => copy.Source));
    }

    [Fact]
    public void NeverCcAndRuleRemovalsAlwaysWinAndAreRecordedWithTheirReason()
    {
        var rules = Every(
            [Cc(["principal@principal.example"]), NeverCc(["never@insurer.example"])],
            [Rule(ClaimSource(CarId), [NeverCc(["Removed@insurer.example"])])]);

        var plan = Plan(
            rules,
            instruction: Instruction(cc: ["never@insurer.example", "removed@insurer.example", "keep@insurer.example"]),
            claimSource: CarId, claimSourceName: "Car 2 Go");

        Assert.Equal(
            ["keep@insurer.example", "principal@principal.example"],
            plan.Cc.Select(copy => copy.Address));
        Assert.Equal(
            [("never@insurer.example", "never cc"), ("Removed@insurer.example", "Rule 1: Claim Source Car 2 Go")],
            plan.Removed.Select(removal => (removal.Address, removal.Reason)));
        Assert.Equal(
            plan.Removed.Select(removal => (removal.Address, removal.Reason)),
            plan.Excluded.Select(removal => (removal.Address, removal.Reason)));
    }

    /// <summary>
    /// Excluded is every address the rules keep out, so one staff type in
    /// later is taken out too; Removed is only what left the plan's own copies.
    /// </summary>
    [Fact]
    public void ExcludedListsEveryAddressKeptOutEvenOneThePlanNeverCopied()
    {
        var rules = Every(
            [NeverCc(["never@insurer.example"])],
            [
                Rule(ClaimSource(CarId), [NeverCc(["gone@insurer.example"])]),
                Rule(ClaimSource(SmcId), [NeverCc(["kept@insurer.example"])])
            ]);

        var plan = Plan(rules, claimSource: CarId, claimSourceName: "Car 2 Go");

        Assert.Empty(plan.Removed);
        Assert.Equal(
            [("never@insurer.example", "never cc"), ("gone@insurer.example", "Rule 1: Claim Source Car 2 Go")],
            plan.Excluded.Select(removal => (removal.Address, removal.Reason)));
    }

    [Fact]
    public void ARuleThatAddsAndAnotherThatRemovesTheSameAddressLeaveItRemoved()
    {
        var rules = Every(
            [],
            [
                Rule(ClaimSource(CarId), [Cc(["x@rule.example"])]),
                Rule(ClaimSource(CarId), [NeverCc(["x@rule.example"])])
            ]);

        var plan = Plan(rules, claimSource: CarId, claimSourceName: "Car 2 Go");

        Assert.Empty(plan.Cc);
        Assert.Equal("x@rule.example", Assert.Single(plan.Removed).Address);
    }

    [Fact]
    public void CopiesAreDeduplicatedWithoutCaseAndNeverRepeatATo()
    {
        var plan = Plan(
            Every([Cc(["A@Insurer.example", "handler@insurer.example"])]),
            instruction: Instruction(cc: ["a@insurer.example", "HANDLER@insurer.example"]));

        Assert.Equal("a@insurer.example", Assert.Single(plan.Cc).Address);
        Assert.Equal(Sender, Assert.Single(plan.To).Address);
    }

    // ---- rule evaluation: a fact the Case holds -----------------------------

    [Fact]
    public void AClaimSourceTheCaseHasDecidesTheRule()
    {
        var rule = Rule(ClaimSource(CarId), [Cc(["x@rule.example"])]);

        var holds = Plan(Every([], [rule]), claimSource: CarId);
        var other = Plan(Every([], [rule]), claimSource: SmcId);

        Assert.Equal("x@rule.example", Assert.Single(holds.Cc).Address);
        Assert.Empty(other.Cc);
    }

    [Fact]
    public void AnUnsetClaimSourceMeetsNoIsCondition()
    {
        var rule = Rule(ClaimSource(CarId), [Cc(["x@rule.example"])]);
        var facts = Facts(Every([], [rule]));

        var plan = ReportDispatchPolicy.Plan(facts, Now);

        Assert.False(ReportDispatchPolicy.Evaluate(rule, facts));
        Assert.Empty(plan.Cc);
    }

    /// <summary>
    /// An "is" condition reads only what the Case holds: a Case with no Claim
    /// Source, Repairer, outcome, type or sender meets no "is" condition on
    /// that fact, so such a rule copies, holds, stops and tasks nothing
    /// (operator, 12 November 2026; R1023, R1024). Each "is not" of the same
    /// conditions holds (R1269).
    /// </summary>
    [Fact]
    public void AFactTheCaseHasNotSetMeetsNoIsConditionAndEveryIsNot()
    {
        ReportSendingCondition[] conditions =
        [
            ClaimSource(CarId),
            Repairer(EasdonsId),
            new(ReportSendingFact.Outcome, ["repairable", "total_loss"]),
            new(ReportSendingFact.CaseType, ["Inspection", "Audit", "InspectionAndAudit"]),
            new(ReportSendingFact.Sender, ["boss@insurer.example"]),
            new(ReportSendingFact.RefPrefix, ["a"]),
            new(ReportSendingFact.PreviousJob, [])
        ];
        IReadOnlyList<ReportSendingAction> then =
            [Cc(["x@rule.example"]), AddTask("Tell Andy."), Hold("Wait."), Stop("Stop.")];
        var rules = Every([], [.. conditions.Select(condition => Rule(condition, then))]);
        var negated = Every([], [.. conditions.Select(condition => Rule(condition with { Not = true }, then))]);
        // No instruction and no uploaded sender: every fact above is unset.
        var facts = Facts(rules, withInstruction: false);

        var plan = ReportDispatchPolicy.Plan(facts, Now);

        Assert.All(rules.Rules, rule => Assert.False(ReportDispatchPolicy.Evaluate(rule, facts)));
        Assert.Empty(plan.Cc);
        Assert.Empty(plan.Holds);
        Assert.Null(plan.Stop);
        Assert.Empty(plan.AfterSendTasks);

        Assert.All(negated.Rules, rule => Assert.True(ReportDispatchPolicy.Evaluate(rule, facts)));
        var negatedPlan = ReportDispatchPolicy.Plan(facts with { Rules = negated }, Now);
        Assert.Equal("x@rule.example", Assert.Single(negatedPlan.Cc).Address);
        Assert.Equal("Stop.", negatedPlan.Stop);
    }

    /// <summary>
    /// Each fact's "is" holds only when the Case holds a value among the
    /// condition's, and its "is not" is exactly the negation, so it holds on
    /// another value and on an unset fact (R1024, R1269).
    /// </summary>
    [Theory]
    [InlineData("ClaimSource")]
    [InlineData("Repairer")]
    [InlineData("Outcome")]
    [InlineData("CaseType")]
    [InlineData("Sender")]
    [InlineData("RefPrefix")]
    [InlineData("PreviousJob")]
    public void IsNotIsTheNegationOfIsOnASetAndAnUnsetFact(string fact)
    {
        var plain = Facts(Rules(), withInstruction: false);
        IReadOnlyList<string> values;
        ReportDispatchFacts matching, other, unset = plain;
        switch (fact)
        {
            case "ClaimSource":
                values = [CarId.ToString("D")];
                matching = plain with { ClaimSourceId = CarId };
                other = plain with { ClaimSourceId = SmcId };
                break;
            case "Repairer":
                values = [EasdonsId.ToString("D")];
                matching = plain with { RepairerId = EasdonsId };
                other = plain with { RepairerId = SmcId };
                break;
            case "Outcome":
                values = ["repairable"];
                matching = plain with { Outcome = "repairable" };
                other = plain with { Outcome = "total_loss" };
                break;
            case "CaseType":
                values = ["Audit"];
                matching = plain with { CaseType = CaseType.Audit };
                other = plain with { CaseType = CaseType.Inspection };
                break;
            case "Sender":
                values = ["boss@insurer.example"];
                matching = plain with { Instruction = Instruction(sender: "BOSS@insurer.example") };
                other = plain with { Instruction = Instruction() };
                break;
            case "RefPrefix":
                // A reference is always set; one without the prefix stands for unset.
                values = ["a"];
                matching = plain with { CaseReference = "a.PCH26118" };
                other = plain with { CaseReference = "b.PCH26118" };
                unset = plain with { CaseReference = "PCH26118" };
                break;
            default:
                // Previous job takes no value: false is its unset.
                values = [];
                matching = plain with { PreviousJob = true };
                other = plain;
                break;
        }
        var parsed = Enum.Parse<ReportSendingFact>(fact);
        var isCondition = new ReportSendingCondition(parsed, values);
        var notCondition = isCondition with { Not = true };

        Assert.True(ReportDispatchPolicy.Holds(isCondition, matching));
        Assert.False(ReportDispatchPolicy.Holds(isCondition, other));
        Assert.False(ReportDispatchPolicy.Holds(isCondition, unset));
        Assert.False(ReportDispatchPolicy.Holds(notCondition, matching));
        Assert.True(ReportDispatchPolicy.Holds(notCondition, other));
        Assert.True(ReportDispatchPolicy.Holds(notCondition, unset));
    }

    /// <summary>
    /// A rule applies when any group has every condition met: the groups are
    /// joined by or and a group's conditions by and. The Case's Claim Source
    /// is Car 2 Go and its Repairer unset: a condition names the Case's Claim
    /// Source for true, another for false, and the unset Repairer for null.
    /// </summary>
    [Theory]
    [InlineData("and-tt", false, true, true, true)]
    [InlineData("and-tf", false, true, false, false)]
    [InlineData("and-fn", false, false, null, false)]
    [InlineData("and-tn", false, true, null, false)]
    [InlineData("or-ff", true, false, false, false)]
    [InlineData("or-tf", true, true, false, true)]
    [InlineData("or-tn", true, true, null, true)]
    [InlineData("or-fn", true, false, null, false)]
    public void AGroupNeedsEveryConditionAndARuleAnyGroup(
        string row, bool anyGroup, bool? first, bool? second, bool expected)
    {
        Assert.NotEmpty(row);
        ReportSendingCondition Condition(bool? value) => value switch
        {
            true => ClaimSource(CarId),
            false => ClaimSource(SmcId),
            _ => Repairer(EasdonsId)
        };
        IReadOnlyList<IReadOnlyList<ReportSendingCondition>> groups = anyGroup
            ? [[Condition(first)], [Condition(second)]]
            : [[Condition(first), Condition(second)]];
        var rule = new ReportSendingRule(groups, [Cc(["x@rule.example"])]);
        var facts = Facts(Every([], [rule])) with { ClaimSourceId = CarId };

        Assert.Equal(expected, ReportDispatchPolicy.Evaluate(rule, facts));
    }

    [Fact]
    public void AnOutcomeRuleReadsTheRecordedOutcomeCode()
    {
        var rule = Rule(new ReportSendingCondition(ReportSendingFact.Outcome, ["total_loss", "cash_in_lieu"]), [Cc(["x@rule.example"])]);
        var rules = Every([], [rule]);

        Assert.True(ReportDispatchPolicy.Evaluate(rule, Facts(rules) with { Outcome = "cash_in_lieu" }));
        Assert.False(ReportDispatchPolicy.Evaluate(rule, Facts(rules) with { Outcome = "repairable" }));
        Assert.False(ReportDispatchPolicy.Evaluate(rule, Facts(rules)));
    }

    /// <summary>
    /// A Case type rule reads the Case's type: it holds for a named type and
    /// not for another, nor when the Case's type is not known.
    /// </summary>
    [Fact]
    public void ACaseTypeRuleReadsTheCasesType()
    {
        var rule = Rule(new ReportSendingCondition(ReportSendingFact.CaseType, ["Audit", "InspectionAndAudit"]), [Cc(["x@rule.example"])]);
        var rules = Every([], [rule]);

        Assert.True(ReportDispatchPolicy.Evaluate(rule, Facts(rules) with { CaseType = CaseType.Audit }));
        Assert.True(ReportDispatchPolicy.Evaluate(rule, Facts(rules) with { CaseType = CaseType.InspectionAndAudit }));
        Assert.False(ReportDispatchPolicy.Evaluate(rule, Facts(rules) with { CaseType = CaseType.Inspection }));
        Assert.False(ReportDispatchPolicy.Evaluate(rule, Facts(rules)));
    }

    /// <summary>
    /// Inspection and Audit name the report being sent, so an Inspection +
    /// Audit Case matches Inspection for its Inspection report and Audit for
    /// its Audit report, while InspectionAndAudit matches either (operator,
    /// 13 October 2026).
    /// </summary>
    [Fact]
    public void ACaseTypeRuleNamesTheReportBeingSentOnAnInspectionAndAuditCase()
    {
        static bool Holds(string value, CaseType? caseType, CaseWorkKind? reportWork)
        {
            var rule = Rule(new ReportSendingCondition(ReportSendingFact.CaseType, [value]), [Cc(["x@rule.example"])]);
            return ReportDispatchPolicy.Evaluate(
                rule, Facts(Every([], [rule])) with { CaseType = caseType, ReportWork = reportWork });
        }

        // The Inspection report of an Inspection + Audit Case.
        Assert.True(Holds("Inspection", CaseType.InspectionAndAudit, CaseWorkKind.Primary));
        Assert.True(Holds("InspectionAndAudit", CaseType.InspectionAndAudit, CaseWorkKind.Primary));
        Assert.False(Holds("Audit", CaseType.InspectionAndAudit, CaseWorkKind.Primary));

        // Its Audit report.
        Assert.True(Holds("Audit", CaseType.InspectionAndAudit, CaseWorkKind.Audit));
        Assert.True(Holds("InspectionAndAudit", CaseType.InspectionAndAudit, CaseWorkKind.Audit));
        Assert.False(Holds("Inspection", CaseType.InspectionAndAudit, CaseWorkKind.Audit));

        // A Case of one type has only that type's report.
        Assert.True(Holds("Inspection", CaseType.Inspection, CaseWorkKind.Primary));
        Assert.False(Holds("Audit", CaseType.Inspection, CaseWorkKind.Primary));
        Assert.False(Holds("InspectionAndAudit", CaseType.Inspection, CaseWorkKind.Primary));
        Assert.True(Holds("Audit", CaseType.Audit, CaseWorkKind.Primary));
        Assert.False(Holds("Inspection", CaseType.Audit, CaseWorkKind.Primary));

        // Triage sends no report, so no type a rule can name holds for it.
        Assert.False(Holds("Inspection", CaseType.Triage, CaseWorkKind.Primary));
        Assert.False(Holds("InspectionAndAudit", CaseType.Triage, CaseWorkKind.Primary));

        // Without the report's work an Inspection + Audit Case matches only
        // its own type; Inspection and Audit do not hold, nor does any type
        // when the Case's type is not known.
        Assert.True(Holds("InspectionAndAudit", CaseType.InspectionAndAudit, null));
        Assert.False(Holds("Inspection", CaseType.InspectionAndAudit, null));
        Assert.False(Holds("Audit", CaseType.InspectionAndAudit, null));
        Assert.False(Holds("Inspection", null, CaseWorkKind.Primary));
    }

    /// <summary>
    /// Sender reads the effective sender however the Case holds it; "is not"
    /// holds for another sender and, unlike the retired "Sender not", for a
    /// Case with no sender (R1269).
    /// </summary>
    [Fact]
    public void SenderReadsTheEffectiveSenderHoweverTheCaseHoldsIt()
    {
        var rule = Rule(
            new ReportSendingCondition(ReportSendingFact.Sender, ["boss@insurer.example"], Not: true),
            [Cc(["x@rule.example"])]);
        var rules = Every([], [rule]);

        Assert.True(ReportDispatchPolicy.Evaluate(rule, Facts(rules)));
        Assert.False(
            ReportDispatchPolicy.Evaluate(rule, Facts(rules, instruction: Instruction(sender: "BOSS@insurer.example"))));
        Assert.False(
            ReportDispatchPolicy.Evaluate(
                rule, Facts(rules, withInstruction: false) with { OriginalSender = "boss@insurer.example" }));
        Assert.True(ReportDispatchPolicy.Evaluate(rule, Facts(rules, withInstruction: false)));
    }

    // ---- Reference prefix and Previous job (Report Sending SOP v8) -----------

    /// <summary>A reference prefix is the report reference's letters before a dot.</summary>
    [Fact]
    public void AReferencePrefixReadsTheReportReference()
    {
        var rule = Rule(new ReportSendingCondition(ReportSendingFact.RefPrefix, ["a"]), [Cc(["x@rule.example"])]);
        var rules = Every([], [rule]);
        ReportDispatchFacts For(string reference) => Facts(rules) with { CaseReference = reference };

        Assert.True(ReportDispatchPolicy.Evaluate(rule, For("a.PCH26118")));
        Assert.True(ReportDispatchPolicy.Evaluate(rule, For("A.PCH26118")));
        Assert.False(ReportDispatchPolicy.Evaluate(rule, For("PCH26118")));
        Assert.False(ReportDispatchPolicy.Evaluate(rule, For("ab.PCH26118")));
    }

    /// <summary>
    /// A previous job is what the dispatch facts query reports: another Case
    /// of the same Principal with the same registration has a sent report
    /// (operator, 16 October 2026).
    /// </summary>
    [Fact]
    public void APreviousJobReadsTheCrossCaseLookup()
    {
        var rule = Rule(new ReportSendingCondition(ReportSendingFact.PreviousJob, []), [Cc(["x@rule.example"])]);
        var facts = Facts(Every([], [rule]));

        Assert.True(ReportDispatchPolicy.Evaluate(rule, facts with { PreviousJob = true }));
        Assert.False(ReportDispatchPolicy.Evaluate(rule, facts with { PreviousJob = false }));
        Assert.Empty(ReportDispatchPolicy.Plan(facts, Now).Cc);
        Assert.Equal("x@rule.example", Assert.Single(ReportDispatchPolicy.Plan(facts with { PreviousJob = true }, Now).Cc).Address);
    }

    /// <summary>
    /// PCH under Report Sending SOP v8, as built: a repairable iRevolution job
    /// copies iRevolution and adds its tasks; an audit job (the reference
    /// carries the a. prefix, or the vehicle is a previous job) keeps that
    /// copy out, and the removal wins. "The instruction says audit" was an
    /// Instruction mentions condition and is gone (R1268).
    /// </summary>
    [Theory]
    [InlineData("repairable job", "PCH26118", false, true)]
    [InlineData("audit reference", "a.PCH26118", false, false)]
    [InlineData("previous job", "PCH26118", true, false)]
    public void TheAuditRuleRemovesTheCopyTheRepairableRuleAdds(
        string row, string reference, bool previousJob, bool copied)
    {
        Assert.NotEmpty(row);
        var rules = Only(
            [SendTo(["SBL@connexus.example"])],
            [
                new ReportSendingRule(
                    [[ClaimSource(CarId), new ReportSendingCondition(ReportSendingFact.Outcome, ["repairable"])]],
                    [Cc(["repairs@irevolution.example"]), AddTask("Authorise the garage"), AddTask(ReportDispatchPolicy.RateCardTask)]),
                new ReportSendingRule(
                    [[new ReportSendingCondition(ReportSendingFact.RefPrefix, ["a"])], [new ReportSendingCondition(ReportSendingFact.PreviousJob, [])]],
                    [NeverCc(["repairs@irevolution.example"])])
            ]);
        var facts = Facts(rules, instruction: Instruction(cc: ["handler@pch.example"])) with
        {
            CaseReference = reference,
            ClaimSourceId = CarId,
            ClaimSourceName = "iRevolution Repair Network",
            Outcome = "repairable",
            PreviousJob = previousJob
        };

        var plan = ReportDispatchPolicy.Plan(facts, Now);

        Assert.Equal("SBL@connexus.example", Assert.Single(plan.To).Address);
        Assert.Equal(copied, plan.Cc.Any(copy => copy.Address == "repairs@irevolution.example"));
        Assert.DoesNotContain(plan.Cc, copy => copy.Address == "handler@pch.example");
        Assert.Equal(["Authorise the garage", ReportDispatchPolicy.RateCardTask], plan.AfterSendTasks);
        if (!copied)
        {
            var removal = Assert.Single(plan.Removed);
            Assert.Equal("repairs@irevolution.example", removal.Address);
            Assert.Equal("Rule 2: Reference prefix a or Previous job", removal.Reason);
        }
    }

    // ---- rule labels ---------------------------------------------------------

    /// <summary>
    /// A rule's recipient is labelled "Rule n:" and its conditions, a group's
    /// joined by ", " and the groups by " or "; a met Claim Source is the
    /// Case's own (R1272 item S).
    /// </summary>
    [Fact]
    public void ARulesLabelReadsItsGroupsJoinedByOr()
    {
        var rules = Every(
            [],
            [
                new ReportSendingRule(
                    [
                        [ClaimSource(CarId), new ReportSendingCondition(ReportSendingFact.Outcome, ["repairable", "total_loss"])],
                        [new ReportSendingCondition(ReportSendingFact.PreviousJob, [], Not: true)]
                    ],
                    [Cc(["x@rule.example"])])
            ]);

        var plan = Plan(Facts(rules) with { ClaimSourceId = CarId, ClaimSourceName = "Car 2 Go", Outcome = "repairable" });

        Assert.Equal(
            "Rule 1: Claim Source Car 2 Go, Outcome Repairable or Total loss or No previous job",
            Assert.Single(plan.Cc, copy => copy.Address == "x@rule.example").Source);
    }

    /// <summary>
    /// An "is not" condition reads "{fact} not {values}"; a Claim Source or
    /// Repairer it names is labelled by the contact's name where the facts
    /// hold it, else by its id.
    /// </summary>
    [Fact]
    public void AnIsNotConditionIsLabelledWithNot()
    {
        const string unknownId = "00000000-0000-4000-8000-0000000000aa";
        var rules = Every(
            [],
            [
                new ReportSendingRule(
                    [
                        [new ReportSendingCondition(ReportSendingFact.ClaimSource, [SmcId.ToString("D"), unknownId], Not: true)],
                        [
                            new ReportSendingCondition(ReportSendingFact.Repairer, [EasdonsId.ToString("D")], Not: true),
                            new ReportSendingCondition(ReportSendingFact.Outcome, ["total_loss"], Not: true),
                            new ReportSendingCondition(ReportSendingFact.Sender, ["boss@insurer.example"], Not: true),
                            new ReportSendingCondition(ReportSendingFact.CaseType, ["InspectionAndAudit"], Not: true),
                            new ReportSendingCondition(ReportSendingFact.RefPrefix, ["a"], Not: true)
                        ]
                    ],
                    [Cc(["x@rule.example"])])
            ]);

        var plan = Plan(Facts(rules) with
        {
            ClaimSourceId = CarId,
            ContactNames = new Dictionary<Guid, string> { [SmcId] = "SMC", [EasdonsId] = "Easdons" }
        });

        Assert.Equal(
            $"Rule 1: Claim Source not SMC or {unknownId} or Repairer not Easdons, Outcome not Total loss, "
                + "Sender not boss@insurer.example, Case type not Inspection and audit, Reference prefix not a",
            Assert.Single(plan.Cc, copy => copy.Address == "x@rule.example").Source);
    }

    // ---- subject ---------------------------------------------------------------

    // Which mailbox a report leaves from, and whether it is a reply, is
    // ResolveCaseMailSender's (operator, 12 October 2026), not the plan's.

    [Fact]
    public void AReplyKeepsTheInstructionsSubject()
    {
        var plan = Plan(Rules(), instruction: Instruction(subject: "Claim AB12 CDE"));

        Assert.Equal("Re: Claim AB12 CDE", plan.Subject);
        Assert.Null(plan.Stop);
    }

    /// <summary>
    /// A staff forward is answered as though the original had arrived
    /// directly (operator, 3 November 2026): the subject drops the forward's "FW:".
    /// </summary>
    [Fact]
    public void AnAnswerToAStaffForwardDropsTheForwardPrefix()
    {
        var plan = Plan(
            Rules(),
            instruction: Instruction(subject: "FW: Claim AB12 CDE") with { SenderAddress = "desk@collisionengineers.co.uk" });

        Assert.Equal("Re: Claim AB12 CDE", plan.Subject);
    }

    [Theory]
    [InlineData("Re: Claim AB12 CDE", "Re: Claim AB12 CDE")]
    [InlineData("RE: Claim", "RE: Claim")]
    [InlineData("  Claim  ", "Re: Claim")]
    public void AReplyNeverDoublesTheRe(string original, string expected) =>
        Assert.Equal(expected, Plan(Rules(), instruction: Instruction(subject: original)).Subject);

    [Fact]
    public void AnInstructionWithNoSubjectTakesTheNewMessageSubject() =>
        Assert.Equal("AB12 CDE Report", Plan(Rules(), instruction: Instruction(subject: " ")).Subject);

    [Fact]
    public void WithNoInstructionTheReportIsANewMessageNamedForTheRegistration()
    {
        var facts = Facts(Every([SendTo(["fixed@principal.example"])]), withInstruction: false);

        var plan = ReportDispatchPolicy.Plan(facts, Now);

        Assert.Equal("AB12 CDE Report", plan.Subject);
        Assert.Null(plan.Stop);
    }

    [Fact]
    public void ANewMessageIsNamedForTheReferenceWhenTheCaseHasNoRegistration()
    {
        var facts = Facts(Every([SendTo(["fixed@principal.example"])]), withInstruction: false) with { Registration = null };

        Assert.Equal("DVR-31001 Report", ReportDispatchPolicy.Plan(facts, Now).Subject);
    }

    // ---- stops ----------------------------------------------------------------

    /// <summary>
    /// Only a Stop action stops a delivery: a Case with no instruction and
    /// nobody to send to is not stopped by the plan.
    /// </summary>
    [Fact]
    public void NoInstructionAndNoRecipientAreNotStops()
    {
        var plan = Plan(Rules(), withInstruction: false);

        Assert.Empty(plan.To);
        Assert.Null(plan.Stop);
    }

    [Fact]
    public void TheFirstStopThatAppliesStopsTheDeliveryAndAnUnsetFactStopsNothing()
    {
        var rules = Every(
            [],
            [
                Rule(ClaimSource(SmcId), [Stop("Never sent.")]),
                Rule(ClaimSource(CarId), [Stop("First stop.")]),
                Rule(ClaimSource(CarId), [Stop("Second stop.")])
            ]);

        Assert.Equal("First stop.", Plan(rules, claimSource: CarId).Stop);
        // Claim Source unset: no stop rule holds.
        Assert.Null(ReportDispatchPolicy.Plan(Facts(rules), Now).Stop);
        // An every-Case Stop is met first.
        Assert.Equal(
            "Every Case.",
            Plan(rules with { EveryCase = [Stop("Every Case.")] }, claimSource: CarId).Stop);
    }

    // ---- holds and after-send tasks ------------------------------------------

    [Fact]
    public void HoldsAreTheEveryCaseHoldsAndEveryRuleHoldThatApplies()
    {
        var rules = Every(
            [Hold("Authorise the garage.")],
            [
                Rule(ClaimSource(CarId), [Hold("Check the WhatsApp group.")]),
                Rule(ClaimSource(SmcId), [Hold("Not this one.")]),
                Rule(ClaimSource(CarId), [Hold("Authorise the garage.")])
            ]);

        var plan = Plan(rules, claimSource: CarId);

        Assert.Equal(["Authorise the garage.", "Check the WhatsApp group."], plan.Holds);
    }

    /// <summary>
    /// Tasks are the every-Case tasks then each applying rule's, in order and
    /// once each; Garage figures is an ordinary rule, "If Outcome is
    /// Repairable, then Add a Case task" (R1270).
    /// </summary>
    [Fact]
    public void AfterSendTasksAreEveryTaskThatAppliesInOrderAndOnce()
    {
        var garageFigures = Rule(
            new ReportSendingCondition(ReportSendingFact.Outcome, ["repairable"]),
            [AddTask(ReportDispatchPolicy.GarageFiguresTask)]);
        var withoutGarageFigures = Every(
            [AddTask("Send the WhatsApp."), AddTask("Send the WhatsApp.")],
            [
                Rule(ClaimSource(CarId), [AddTask("Tell Andy.")]),
                Rule(ClaimSource(SmcId), [AddTask("Not this one.")]),
                Rule(ClaimSource(CarId), [AddTask("Send the WhatsApp.")])
            ]);
        var rules = withoutGarageFigures with { Rules = [.. withoutGarageFigures.Rules, garageFigures] };

        var repairable = ReportDispatchPolicy.Plan(
            Facts(rules) with { ClaimSourceId = CarId, Outcome = "repairable" }, Now);
        var totalLoss = ReportDispatchPolicy.Plan(
            Facts(rules) with { ClaimSourceId = CarId, Outcome = "total_loss" }, Now);
        var without = ReportDispatchPolicy.Plan(
            Facts(withoutGarageFigures) with { ClaimSourceId = CarId, Outcome = "repairable" }, Now);

        Assert.Equal(
            ["Send the WhatsApp.", "Tell Andy.", ReportDispatchPolicy.GarageFiguresTask],
            repairable.AfterSendTasks);
        Assert.Equal(["Send the WhatsApp.", "Tell Andy."], totalLoss.AfterSendTasks);
        Assert.Equal(["Send the WhatsApp.", "Tell Andy."], without.AfterSendTasks);
    }

    /// <summary>
    /// The rate card check is an ordinary task: after the rule's own task,
    /// and once however many rules add it.
    /// </summary>
    [Fact]
    public void ARateCardTaskJoinsTheTasksInRuleOrderOnce()
    {
        var rules = Every(
            [AddTask("Send the WhatsApp.")],
            [
                Rule(ClaimSource(CarId), [AddTask("Tell Andy."), AddTask(ReportDispatchPolicy.RateCardTask)]),
                Rule(ClaimSource(SmcId), [AddTask(ReportDispatchPolicy.RateCardTask)]),
                Rule(ClaimSource(CarId), [AddTask("Call the garage."), AddTask(ReportDispatchPolicy.RateCardTask)])
            ]);

        var plan = ReportDispatchPolicy.Plan(Facts(rules) with { ClaimSourceId = CarId }, Now);
        var other = ReportDispatchPolicy.Plan(Facts(rules) with { ClaimSourceId = EasdonsId }, Now);

        Assert.Equal(
            ["Send the WhatsApp.", "Tell Andy.", ReportDispatchPolicy.RateCardTask, "Call the garage."],
            plan.AfterSendTasks);
        Assert.Equal(["Send the WhatsApp."], other.AfterSendTasks);
    }

    // ---- companions, estimates, fee note -------------------------------------

    [Fact]
    public void AnImagesDocumentAndAFigureBreakdownAreRequiredAndMissingUntilConfirmed()
    {
        var rules = Every([Attach(ReportSendingFile.VehicleImagesDocument), Attach(ReportSendingFile.FigureBreakdown)]);

        var missing = Plan(rules);
        var partly = ReportDispatchPolicy.Plan(
            Facts(rules) with { ConfirmedArtifacts = [CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.ImagePack] }, Now);
        var complete = ReportDispatchPolicy.Plan(
            Facts(rules) with
            {
                ConfirmedArtifacts = [CaseReportArtifactKind.ImagePack, CaseReportArtifactKind.RepairSpecification]
            }, Now);

        Assert.Equal(
            [CaseReportArtifactKind.ImagePack, CaseReportArtifactKind.RepairSpecification],
            missing.RequiredCompanions);
        Assert.Equal(missing.RequiredCompanions, missing.MissingCompanions);
        Assert.Equal([CaseReportArtifactKind.RepairSpecification], partly.MissingCompanions);
        Assert.Empty(complete.MissingCompanions);
        Assert.Empty(Plan(Rules()).RequiredCompanions);
    }

    /// <summary>
    /// Attach and Do not attach switch one file for the send, the last action
    /// that names it winning, every-Case first and then the applying rules in
    /// order (R1272 item P). Companions, filed estimates, warnings and the
    /// plan's flags all read the result.
    /// </summary>
    [Fact]
    public void TheLastActionNamingAFileDecidesTheCompanionsEstimatesAndWarnings()
    {
        var rules = Every(
            [Attach(ReportSendingFile.VehicleImagesDocument), Attach(ReportSendingFile.Audatex)],
            [
                Rule(ClaimSource(CarId),
                [
                    DoNotAttach(ReportSendingFile.VehicleImagesDocument),
                    Attach(ReportSendingFile.FigureBreakdown),
                    DoNotAttach(ReportSendingFile.FeeNoteSeparate)
                ]),
                Rule(ClaimSource(CarId), [DoNotAttach(ReportSendingFile.Audatex), Attach(ReportSendingFile.Estimate)]),
                Rule(ClaimSource(CarId), [DoNotAttach(ReportSendingFile.Estimate)]),
                Rule(ClaimSource(SmcId), [DoNotAttach(ReportSendingFile.FigureBreakdown)])
            ]);
        var facts = Facts(rules) with { FiledEstimates = [AudatexFiled, GlassFiled] };

        var everyCase = ReportDispatchPolicy.Plan(facts with { ClaimSourceId = EasdonsId }, Now);
        var applied = ReportDispatchPolicy.Plan(facts with { ClaimSourceId = CarId }, Now);

        Assert.Equal([CaseReportArtifactKind.ImagePack], everyCase.RequiredCompanions);
        Assert.Equal([CaseReportArtifactKind.ImagePack], everyCase.MissingCompanions);
        Assert.Equal([AudatexFiled], everyCase.FiledEstimates);
        Assert.Contains(ReportDispatchPolicy.NoSeparateFeeNoteWarning, everyCase.Warnings);
        Assert.Equal(
            new ReportSendingAttachments(FeeNoteSeparate: true, Audatex: true, VehicleImagesDocument: true),
            everyCase.Attach);

        // The images document is no longer required and the figure breakdown is;
        // Audatex is off, and the third rule's Estimate off beats the second's on.
        Assert.Equal([CaseReportArtifactKind.RepairSpecification], applied.RequiredCompanions);
        Assert.Equal([CaseReportArtifactKind.RepairSpecification], applied.MissingCompanions);
        Assert.Empty(applied.FiledEstimates);
        Assert.Empty(applied.Warnings);
        Assert.Equal(new ReportSendingAttachments(FigureBreakdown: true), applied.Attach);
    }

    [Fact]
    public void TheAudatexFileAttachesTheAudatexEstimatesAndTheEstimateFileTheOthers()
    {
        var facts = Facts(Rules()) with { FiledEstimates = [AudatexFiled, GlassFiled, UnknownFiled] };

        IReadOnlyList<StaffMailAttachment> Filed(bool estimate, bool audatex) =>
            [.. ReportDispatchPolicy.Plan(facts with { Rules = Files(estimate, audatex) }, Now).FiledEstimates.Select(filed => filed.Attachment)];

        Assert.Equal([AudatexFiled.Attachment], Filed(estimate: false, audatex: true));
        Assert.Equal([GlassFiled.Attachment, UnknownFiled.Attachment], Filed(estimate: true, audatex: false));
        Assert.Equal(
            [AudatexFiled.Attachment, GlassFiled.Attachment, UnknownFiled.Attachment],
            Filed(estimate: true, audatex: true));
        Assert.Empty(Filed(estimate: false, audatex: false));
        Assert.DoesNotContain(
            ReportDispatchPolicy.NoFiledEstimateWarning,
            ReportDispatchPolicy.Plan(facts with { Rules = Files(true, true) }, Now).Warnings);
    }

    [Theory]
    [InlineData("estimate, Audatex filed", true, false, "Audatex")]
    [InlineData("Audatex, other filed", false, true, "Glass's")]
    [InlineData("estimate, nothing filed", true, false, null)]
    [InlineData("Audatex, nothing filed", false, true, null)]
    public void AFileWithNoMatchingFiledEstimateWarns(string row, bool estimate, bool audatex, string? filedProvider)
    {
        Assert.NotEmpty(row);
        var facts = Facts(Files(estimate, audatex)) with
        {
            FiledEstimates = filedProvider is null
                ? []
                : [new FiledEstimateAttachment(EstimateAttachment("filed.pdf"), filedProvider)]
        };

        var plan = ReportDispatchPolicy.Plan(facts, Now);

        Assert.Empty(plan.FiledEstimates);
        Assert.Contains(ReportDispatchPolicy.NoFiledEstimateWarning, plan.Warnings);
        Assert.Empty(plan.Holds);
    }

    [Fact]
    public void WithoutEstimateOrAudatexNoFiledEstimateIsAttachedOrWarnedAbout()
    {
        var plan = ReportDispatchPolicy.Plan(
            Facts(Rules()) with { FiledEstimates = [AudatexFiled, GlassFiled] }, Now);

        Assert.Empty(plan.FiledEstimates);
        Assert.DoesNotContain(ReportDispatchPolicy.NoFiledEstimateWarning, plan.Warnings);
    }

    // ---- R1151, R1158: offered Case files -----------------------------------

    /// <summary>
    /// Each Offer file offers the Case's files of its own type, in the Case's
    /// order, and nothing while no action attaches it (R1151).
    /// </summary>
    [Fact]
    public void EachOfferFileOffersOnlyTheCasesFilesOfItsType()
    {
        var facts = Facts(Rules()) with { OfferableFiles = [ExternalOffered, InvoiceOffered, SecondExternalOffered] };

        PrincipalReportSendingRules Offers(bool estimates, bool invoices) =>
            Files(estimate: false, audatex: false, offerEstimates: estimates, offerInvoices: invoices);
        IReadOnlyList<OfferedCaseFile> Offered(bool estimates, bool invoices) =>
            ReportDispatchPolicy.Plan(facts with { Rules = Offers(estimates, invoices) }, Now).OfferedFiles;

        Assert.Equal([ExternalOffered, SecondExternalOffered], Offered(estimates: true, invoices: false));
        Assert.Equal([InvoiceOffered], Offered(estimates: false, invoices: true));
        Assert.Equal([ExternalOffered, InvoiceOffered, SecondExternalOffered], Offered(estimates: true, invoices: true));
        Assert.Empty(Offered(estimates: false, invoices: false));
        // Offering adds no warning and no hold.
        var plan = ReportDispatchPolicy.Plan(facts with { Rules = Offers(true, true) }, Now);
        Assert.DoesNotContain(ReportDispatchPolicy.NoFiledEstimateWarning, plan.Warnings);
        Assert.Empty(plan.Holds);
    }

    /// <summary>
    /// A file the Estimate file already attaches is not offered a second time
    /// (R1158); without it the file is offered.
    /// </summary>
    [Fact]
    public void AFileTheEstimateAttachesIsNotOfferedASecondTime()
    {
        var recognised = new FiledEstimateAttachment(ExternalOffered.Attachment, "Glass's");
        var facts = Facts(Files(estimate: true, audatex: false, offerEstimates: true)) with
        {
            FiledEstimates = [recognised],
            OfferableFiles = [ExternalOffered, SecondExternalOffered]
        };

        var plan = ReportDispatchPolicy.Plan(facts, Now);
        Assert.Equal([recognised], plan.FiledEstimates);
        Assert.Equal([SecondExternalOffered], plan.OfferedFiles);

        var estimateOff = ReportDispatchPolicy.Plan(
            facts with { Rules = Files(estimate: false, audatex: false, offerEstimates: true) }, Now);
        Assert.Empty(estimateOff.FiledEstimates);
        Assert.Equal([ExternalOffered, SecondExternalOffered], estimateOff.OfferedFiles);
    }

    /// <summary>An applying rule turns an Offer file on or off for the send (R1151).</summary>
    [Fact]
    public void AnApplyingRuleTurnsAnOfferFileOnOrOff()
    {
        var rules = Every(
            [Attach(ReportSendingFile.OfferExternalEstimates)],
            [
                Rule(ClaimSource(CarId),
                    [DoNotAttach(ReportSendingFile.OfferExternalEstimates), Attach(ReportSendingFile.OfferRepairInvoices)])
            ]);
        var facts = Facts(rules) with { OfferableFiles = [ExternalOffered, InvoiceOffered] };

        Assert.Equal([ExternalOffered], ReportDispatchPolicy.Plan(facts with { ClaimSourceId = SmcId }, Now).OfferedFiles);
        Assert.Equal([InvoiceOffered], ReportDispatchPolicy.Plan(facts with { ClaimSourceId = CarId }, Now).OfferedFiles);
    }

    [Fact]
    public void ASeparateFeeNoteNotConfirmedIsAWarningNotAHold()
    {
        var plan = Plan(Rules());
        var confirmed = ReportDispatchPolicy.Plan(
            Facts(Rules()) with { ConfirmedArtifacts = [CaseReportArtifactKind.FeeNote] }, Now);
        var notSeparate = Plan(Only([new(ReportSendingActionKind.ReplyAll)]));

        Assert.Contains(ReportDispatchPolicy.NoSeparateFeeNoteWarning, plan.Warnings);
        Assert.Empty(plan.Holds);
        Assert.DoesNotContain(ReportDispatchPolicy.NoSeparateFeeNoteWarning, confirmed.Warnings);
        Assert.Empty(notSeparate.Warnings);
    }

    // ---- the greeting --------------------------------------------------------

    [Theory]
    // BST: London is an hour ahead of UTC until 25 October 2026.
    [InlineData("2026-10-05T07:00:00Z", "morning")]
    [InlineData("2026-10-05T10:59:00Z", "morning")]
    [InlineData("2026-10-05T11:00:00Z", "afternoon")]
    [InlineData("2026-10-05T22:30:00Z", "afternoon")]
    // GMT in winter.
    [InlineData("2026-12-10T11:59:00Z", "morning")]
    [InlineData("2026-12-10T12:00:00Z", "afternoon")]
    public void TheGreetingTurnsAtNoonInLondon(string instant, string expected) =>
        Assert.Equal(expected, ReportDispatchPolicy.Greeting(DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void ThePlanCarriesTheGreetingForItsMoment()
    {
        Assert.Equal("morning", ReportDispatchPolicy.Plan(Facts(Rules()), Now).Greeting);
        Assert.Equal(
            "afternoon",
            ReportDispatchPolicy.Plan(Facts(Rules()), new DateTimeOffset(2026, 10, 5, 11, 0, 0, TimeSpan.Zero)).Greeting);
    }

    // ---- the fingerprint -----------------------------------------------------

    [Theory]
    [InlineData("rules")]
    [InlineData("instruction")]
    [InlineData("sender")]
    [InlineData("claim source")]
    [InlineData("repairer")]
    [InlineData("outcome")]
    [InlineData("task")]
    [InlineData("attach")]
    [InlineData("not")]
    public void TheFingerprintChangesWithEveryFactTheRulesRead(string fact)
    {
        var facts = FingerprintFacts();
        ReportDispatchFacts WithRule(ReportSendingRule rule) =>
            facts with { Rules = facts.Rules with { Rules = [rule] } };
        var changed = fact switch
        {
            "rules" => facts with { Rules = facts.Rules with { EveryCase = [.. facts.Rules.EveryCase, Hold("Wait.")] } },
            "task" => WithRule(Rule(ClaimSource(CarId), [Cc(["x@rule.example"]), AddTask(ReportDispatchPolicy.RateCardTask)])),
            "attach" => WithRule(Rule(ClaimSource(CarId), [Cc(["x@rule.example"]), Attach(ReportSendingFile.Estimate)])),
            "not" => WithRule(Rule(ClaimSource(CarId) with { Not = true }, [Cc(["x@rule.example"])])),
            "instruction" => facts with { Instruction = facts.Instruction! with { RetainedMessageId = Guid.NewGuid() } },
            "sender" => facts with { Instruction = facts.Instruction! with { EffectiveSender = "other@insurer.example" } },
            "claim source" => facts with { ClaimSourceId = SmcId },
            "repairer" => facts with { RepairerId = Guid.NewGuid() },
            _ => facts with { Outcome = "total_loss" }
        };

        Assert.NotEqual(facts.Fingerprint, changed.Fingerprint);
    }

    [Fact]
    public void TheFingerprintIsStableWhenNothingItReadsChanges()
    {
        var facts = FingerprintFacts();

        Assert.Equal(ReportDispatchPolicy.Fingerprint(facts), facts.Fingerprint);
        Assert.Equal(facts.Fingerprint, FingerprintFacts().Fingerprint);
        Assert.Equal(
            facts.Fingerprint,
            (facts with
            {
                History = new(2, new DateOnly(2026, 9, 1)),
                ConfirmedArtifacts = [CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.FeeNote],
                FiledEstimates = [AudatexFiled],
                ContactNames = new Dictionary<Guid, string> { [CarId] = "Car 2 Go" }
            }).Fingerprint);
    }

    [Fact]
    public void WithoutAMailboxInstructionTheUploadedSenderIsInTheFingerprint()
    {
        var facts = Facts(Rules(), withInstruction: false) with { OriginalSender = "uploaded@insurer.example" };

        Assert.NotEqual(facts.Fingerprint, (facts with { OriginalSender = "other@insurer.example" }).Fingerprint);
    }

    // ---- reconciling what staff submitted -------------------------------------

    [Fact]
    public void ReconcileLeavesOutACopyStaffRemovedThatTheFormShowed()
    {
        var facts = Facts(Every([Cc(["always@principal.example"])]));
        var plan = ReportDispatchPolicy.Plan(facts, Now);

        var (review, removed) = ReportDispatchPolicy.Reconcile(plan, new([Sender], []));

        Assert.Equal([Sender], review.To);
        Assert.Empty(review.Cc);
        Assert.Empty(removed);
    }

    [Fact]
    public void ReconcileStripsEveryExcludedAddressWhereverItCameFrom()
    {
        var rules = Every(
            [NeverCc(["never@insurer.example"])],
            [Rule(ClaimSource(CarId), [NeverCc(["gone@insurer.example"])])]);
        var facts = Facts(rules) with { ClaimSourceId = CarId, ClaimSourceName = "Car 2 Go" };
        var plan = ReportDispatchPolicy.Plan(facts, Now);

        // Neither address was copied by the plan; staff typed both.
        var (review, removed) = ReportDispatchPolicy.Reconcile(
            plan,
            new([Sender, " Never@Insurer.example "], ["gone@insurer.example", "ok@insurer.example", "never@insurer.example"]));

        Assert.Empty(plan.Removed);
        Assert.Equal([Sender], review.To);
        Assert.Equal(["ok@insurer.example"], review.Cc);
        Assert.Equal(
            [("never@insurer.example", "never cc"), ("gone@insurer.example", "Rule 1: Claim Source Car 2 Go")],
            removed.Select(removal => (removal.Address, removal.Reason)));
    }

    [Fact]
    public void ReconcileReportsTheCopiesThePlanItselfRemoved()
    {
        var facts = Facts(
            Every([NeverCc(["never@insurer.example"])]),
            instruction: Instruction(cc: ["never@insurer.example", "ok@insurer.example"]));
        var plan = ReportDispatchPolicy.Plan(facts, Now);

        var (review, removed) = ReportDispatchPolicy.Reconcile(plan, CaseReportDeliveryPolicy.SuggestedReview(plan));

        Assert.Equal(["ok@insurer.example"], review.Cc);
        var removal = Assert.Single(removed);
        Assert.Equal("never@insurer.example", removal.Address);
        Assert.Equal("never cc", removal.Reason);
    }

    // ---- outcome words ---------------------------------------------------------

    [Fact]
    public void OutcomeWordsReadTheCodeAsStaffDo()
    {
        Assert.Equal("Total loss", ReportDispatchPolicy.OutcomeWords("total_loss"));
        Assert.Equal("Cash in lieu", ReportDispatchPolicy.OutcomeWords("cash_in_lieu"));
        Assert.Equal("Repairable", ReportDispatchPolicy.OutcomeWords(" repairable "));
        Assert.Null(ReportDispatchPolicy.OutcomeWords(" "));
        Assert.Null(ReportDispatchPolicy.OutcomeWords(null));
    }

    // ---- fixtures ------------------------------------------------------------

    private static readonly FiledEstimateAttachment AudatexFiled =
        new(EstimateAttachment("audatex.pdf"), EstimateFormats.AudatexProvider.ToUpperInvariant());

    private static readonly FiledEstimateAttachment GlassFiled = new(EstimateAttachment("glass.pdf"), "Glass's");

    private static readonly FiledEstimateAttachment UnknownFiled = new(EstimateAttachment("estimate.pdf"), null);

    private static readonly OfferedCaseFile ExternalOffered =
        new(Guid.NewGuid(), DocumentSemanticRole.ExternalEstimate, EstimateAttachment("garage estimate.pdf"));

    private static readonly OfferedCaseFile InvoiceOffered =
        new(Guid.NewGuid(), DocumentSemanticRole.RepairInvoice, EstimateAttachment("repair invoice.pdf"));

    private static readonly OfferedCaseFile SecondExternalOffered =
        new(Guid.NewGuid(), DocumentSemanticRole.ExternalEstimate, EstimateAttachment("supplementary.pdf"));

    private static StaffMailAttachment EstimateAttachment(string fileName) => new(
        Guid.NewGuid(), Guid.NewGuid(), new string('e', 64), 500, fileName, "application/pdf");

    /// <summary>The default rules: Reply all, with the separate fee note.</summary>
    private static PrincipalReportSendingRules Rules() => PrincipalReportSendingRules.Default;

    /// <summary>The default every-Case actions and then <paramref name="more"/>, with <paramref name="rules"/>.</summary>
    private static PrincipalReportSendingRules Every(
        IReadOnlyList<ReportSendingAction> more,
        IReadOnlyList<ReportSendingRule>? rules = null) =>
        new([.. PrincipalReportSendingRules.Default.EveryCase, .. more], rules ?? []);

    /// <summary>Exactly <paramref name="everyCase"/> on every Case, with <paramref name="rules"/>.</summary>
    private static PrincipalReportSendingRules Only(
        IReadOnlyList<ReportSendingAction> everyCase,
        IReadOnlyList<ReportSendingRule>? rules = null) =>
        new(everyCase, rules ?? []);

    /// <summary>The default rules with the estimate and offer files switched on as asked.</summary>
    private static PrincipalReportSendingRules Files(
        bool estimate, bool audatex, bool offerEstimates = false, bool offerInvoices = false)
    {
        var actions = new List<ReportSendingAction>();
        if (estimate) actions.Add(Attach(ReportSendingFile.Estimate));
        if (audatex) actions.Add(Attach(ReportSendingFile.Audatex));
        if (offerEstimates) actions.Add(Attach(ReportSendingFile.OfferExternalEstimates));
        if (offerInvoices) actions.Add(Attach(ReportSendingFile.OfferRepairInvoices));
        return Every(actions);
    }

    private static ReportSendingAction SendTo(IReadOnlyList<string> addresses) => new(ReportSendingActionKind.SendTo, addresses);

    private static ReportSendingAction Cc(IReadOnlyList<string> addresses) => new(ReportSendingActionKind.Cc, addresses);

    private static ReportSendingAction NeverCc(IReadOnlyList<string> addresses) => new(ReportSendingActionKind.NeverCc, addresses);

    private static ReportSendingAction Attach(ReportSendingFile file) => new(ReportSendingActionKind.Attach, File: file);

    private static ReportSendingAction DoNotAttach(ReportSendingFile file) => new(ReportSendingActionKind.DoNotAttach, File: file);

    private static ReportSendingAction Hold(string text) => new(ReportSendingActionKind.Hold, Text: text);

    private static ReportSendingAction Stop(string text) => new(ReportSendingActionKind.Stop, Text: text);

    private static ReportSendingAction AddTask(string text) => new(ReportSendingActionKind.Task, Text: text);

    private static ReportSendingCondition ClaimSource(Guid id) =>
        new(ReportSendingFact.ClaimSource, [id.ToString("D")]);

    private static ReportSendingCondition Repairer(Guid id) =>
        new(ReportSendingFact.Repairer, [id.ToString("D")]);

    /// <summary>A rule of one condition.</summary>
    private static ReportSendingRule Rule(ReportSendingCondition condition, IReadOnlyList<ReportSendingAction> then) =>
        new([[condition]], then);

    private static ReportInstructionMessage Instruction(
        string? sender = Sender,
        IReadOnlyList<string>? cc = null,
        string? subject = "Claim AB12 CDE") => new(
        RetainedId, MailboxId, "engineers@collisionengineers.example", "immutable-1",
        "<instruction@insurer.example>", "conversation-1", sender, cc ?? [], subject, SenderAddress: null);

    private static ReportDispatchFacts Facts(
        PrincipalReportSendingRules rules,
        ReportInstructionMessage? instruction = null,
        bool withInstruction = true) => new(
        "DVR-31001", "AB12 CDE", "Principal Ltd", rules,
        withInstruction ? instruction ?? Instruction() : null);

    private static ReportDispatchFacts FingerprintFacts() =>
        Facts(Every([], [Rule(ClaimSource(CarId), [Cc(["x@rule.example"])])]))
            with
            {
                ClaimSourceId = CarId,
                RepairerId = EasdonsId,
                Outcome = "repairable"
            };

    private static ReportDispatchPlan Plan(ReportDispatchFacts facts) => ReportDispatchPolicy.Plan(facts, Now);

    private static ReportDispatchPlan Plan(
        PrincipalReportSendingRules rules,
        ReportInstructionMessage? instruction = null,
        bool withInstruction = true,
        Guid? claimSource = null,
        string? claimSourceName = null) => ReportDispatchPolicy.Plan(
        Facts(rules, instruction, withInstruction) with
        {
            ClaimSourceId = claimSource,
            ClaimSourceName = claimSourceName
        },
        Now);
}

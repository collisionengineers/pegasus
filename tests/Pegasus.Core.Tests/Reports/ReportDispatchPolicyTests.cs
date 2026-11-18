using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

/// <summary>
/// The one report dispatch plan (Report Sending SOP v8, rule R12; operator,
/// 6 October 2026): recipients in the SOP's order with removals winning,
/// rules that read only what the Case holds (operator, 12 November 2026;
/// R1023, R1024), the reply or new message, required companions and filed
/// estimates, holds, a rule's Stop and after-send tasks. Pure: every case
/// here is facts in, plan out.
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
    }

    [Fact]
    public void FixedSendToAddressesReplaceTheOriginalSender()
    {
        var plan = Plan(Rules() with { SendTo = ["fixed@principal.example", "other@principal.example"] });

        Assert.Equal(["fixed@principal.example", "other@principal.example"], plan.To.Select(item => item.Address));
        Assert.All(plan.To, item => Assert.Equal("Principal To", item.Source));
    }

    /// <summary>
    /// A Case with no mailbox instruction (an uploaded e-mail) still addresses
    /// the sender of the instruction that opened it.
    /// </summary>
    [Fact]
    public void WithoutAMailboxInstructionTheUploadedInstructionsSenderIsAddressed()
    {
        var facts = Facts(Rules() with { Cc = ["always@principal.example"] }, withInstruction: false)
            with { OriginalSender = "uploaded@insurer.example" };

        var plan = ReportDispatchPolicy.Plan(facts, Now);

        var to = Assert.Single(plan.To);
        Assert.Equal("uploaded@insurer.example", to.Address);
        Assert.Equal("Original sender", to.Source);
        // Reply all keeps an instruction's copies; with no instruction only the Principal's Cc remains.
        Assert.Equal("always@principal.example", Assert.Single(plan.Cc).Address);
    }

    [Fact]
    public void SendToOnlyEmptiesEveryCopy()
    {
        var plan = Plan(
            Rules() with
            {
                SendTo = ["fixed@principal.example"],
                SendToOnly = true,
                Cc = ["always@principal.example"],
                Rules = [Rule(ReportSendingRuleMatch.All, new(["car@rule.example"], []), ClaimSource(CarId))]
            },
            instruction: Instruction(cc: ["a@insurer.example"]),
            claimSource: CarId);

        Assert.Empty(plan.Cc);
        Assert.Equal("fixed@principal.example", Assert.Single(plan.To).Address);
    }

    [Fact]
    public void WithoutReplyAllTheInstructionsCopiesAreDropped()
    {
        var plan = Plan(
            Rules() with { ReplyAll = false, Cc = ["always@principal.example"] },
            instruction: Instruction(cc: ["a@insurer.example"]));

        var copy = Assert.Single(plan.Cc);
        Assert.Equal("always@principal.example", copy.Address);
        Assert.Equal("Principal Cc", copy.Source);
    }

    [Fact]
    public void CopiesComeFromTheInstructionThePrincipalAndEveryRuleThatHolds()
    {
        var rules = Rules() with
        {
            Cc = ["principal@principal.example"],
            Rules =
            [
                Rule(ReportSendingRuleMatch.All, new(["car@rule.example"], []), ClaimSource(CarId)),
                Rule(ReportSendingRuleMatch.All, new(["smc@rule.example"], []), ClaimSource(SmcId))
            ]
        };

        var plan = Plan(rules, instruction: Instruction(cc: ["a@insurer.example"]), claimSource: CarId, claimSourceName: "Car 2 Go");

        Assert.Equal(
            ["a@insurer.example", "principal@principal.example", "car@rule.example"],
            plan.Cc.Select(copy => copy.Address));
        Assert.Equal(
            ["Instruction Cc", "Principal Cc", "Rule 1: Claim Source Car 2 Go"],
            plan.Cc.Select(copy => copy.Source));
    }

    [Fact]
    public void NeverCcAndRuleRemovalsAlwaysWinAndAreRecordedWithTheirReason()
    {
        var rules = Rules() with
        {
            Cc = ["principal@principal.example"],
            NeverCc = ["never@insurer.example"],
            Rules =
            [
                Rule(ReportSendingRuleMatch.All, new([], ["Removed@insurer.example"]), ClaimSource(CarId))
            ]
        };

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
        var rules = Rules() with
        {
            NeverCc = ["never@insurer.example"],
            Rules =
            [
                Rule(ReportSendingRuleMatch.All, new([], ["gone@insurer.example"]), ClaimSource(CarId)),
                Rule(ReportSendingRuleMatch.All, new([], ["kept@insurer.example"]), ClaimSource(SmcId))
            ]
        };

        var plan = Plan(rules, claimSource: CarId, claimSourceName: "Car 2 Go");

        Assert.Empty(plan.Removed);
        Assert.Equal(
            [("never@insurer.example", "never cc"), ("gone@insurer.example", "Rule 1: Claim Source Car 2 Go")],
            plan.Excluded.Select(removal => (removal.Address, removal.Reason)));
    }

    [Fact]
    public void ARuleThatAddsAndAnotherThatRemovesTheSameAddressLeaveItRemoved()
    {
        var rules = Rules() with
        {
            Rules =
            [
                Rule(ReportSendingRuleMatch.All, new(["x@rule.example"], []), ClaimSource(CarId)),
                Rule(ReportSendingRuleMatch.All, new([], ["x@rule.example"]), ClaimSource(CarId))
            ]
        };

        var plan = Plan(rules, claimSource: CarId, claimSourceName: "Car 2 Go");

        Assert.Empty(plan.Cc);
        Assert.Equal("x@rule.example", Assert.Single(plan.Removed).Address);
    }

    [Fact]
    public void CopiesAreDeduplicatedWithoutCaseAndNeverRepeatATo()
    {
        var plan = Plan(
            Rules() with { Cc = ["A@Insurer.example", "handler@insurer.example"] },
            instruction: Instruction(cc: ["a@insurer.example", "HANDLER@insurer.example"]));

        Assert.Equal("a@insurer.example", Assert.Single(plan.Cc).Address);
        Assert.Equal(Sender, Assert.Single(plan.To).Address);
    }

    // ---- rule evaluation: a fact the Case holds -----------------------------

    [Fact]
    public void AClaimSourceTheCaseHasDecidesTheRule()
    {
        var rule = Rule(ReportSendingRuleMatch.All, new(["x@rule.example"], []), ClaimSource(CarId));

        var holds = Plan(Rules() with { Rules = [rule] }, claimSource: CarId);
        var other = Plan(Rules() with { Rules = [rule] }, claimSource: SmcId);

        Assert.Equal("x@rule.example", Assert.Single(holds.Cc).Address);
        Assert.Empty(other.Cc);
    }

    [Fact]
    public void AnUnsetClaimSourceMeetsNoRule()
    {
        var rule = Rule(ReportSendingRuleMatch.All, new(["x@rule.example"], []), ClaimSource(CarId));
        var facts = Facts(Rules() with { Rules = [rule] });

        var plan = ReportDispatchPolicy.Plan(facts, Now);

        Assert.False(ReportDispatchPolicy.Evaluate(rule, facts));
        Assert.Empty(plan.Cc);
    }

    /// <summary>
    /// A rule reads only what the Case holds: a Case with no Claim Source,
    /// Repairer, outcome, type, sender or instruction text meets no condition
    /// on that fact, so such a rule copies, holds, stops and reminds nothing
    /// (operator, 12 November 2026; R1023, R1024).
    /// </summary>
    [Fact]
    public void AFactTheCaseHasNotSetMeetsNoCondition()
    {
        ReportSendingCondition[] conditions =
        [
            ClaimSource(CarId),
            Repairer(EasdonsId),
            new(ReportSendingConditionKind.Outcome, ["repairable", "total_loss"]),
            new(ReportSendingConditionKind.CaseType, ["Inspection", "Audit", "InspectionAndAudit"]),
            new(ReportSendingConditionKind.SenderNot, ["boss@insurer.example"]),
            Mentions("Luton")
        ];
        var rules = Rules() with
        {
            Rules =
            [
                .. conditions.Select(condition => Rule(
                    ReportSendingRuleMatch.Any,
                    new(["x@rule.example"], [], Remind: "Tell Andy.", Hold: "Wait.", Stop: "Stop."),
                    condition))
            ]
        };
        // No instruction and no uploaded sender: every fact above is unset.
        var facts = Facts(rules, withInstruction: false);

        var plan = ReportDispatchPolicy.Plan(facts, Now);

        Assert.All(rules.Rules, rule => Assert.False(ReportDispatchPolicy.Evaluate(rule, facts)));
        Assert.Empty(plan.Cc);
        Assert.Empty(plan.Holds);
        Assert.Null(plan.Stop);
        Assert.Empty(plan.AfterSendTasks);
    }

    [Theory]
    [InlineData("all-tt", ReportSendingRuleMatch.All, true, true, true)]
    [InlineData("all-tf", ReportSendingRuleMatch.All, true, false, false)]
    [InlineData("all-fn", ReportSendingRuleMatch.All, false, null, false)]
    [InlineData("all-tn", ReportSendingRuleMatch.All, true, null, false)]
    [InlineData("any-ff", ReportSendingRuleMatch.Any, false, false, false)]
    [InlineData("any-tf", ReportSendingRuleMatch.Any, true, false, true)]
    [InlineData("any-tn", ReportSendingRuleMatch.Any, true, null, true)]
    [InlineData("any-fn", ReportSendingRuleMatch.Any, false, null, false)]
    public void AllNeedsEveryConditionAndAnyOneAndAnUnsetFactHoldsForNeither(
        string row, ReportSendingRuleMatch match, bool? first, bool? second, bool expected)
    {
        Assert.NotEmpty(row);
        // The Case's Claim Source is Car 2 Go; Repairer is unset. A condition
        // is true when it names the Case's Claim Source, false when it names
        // another, and reads the unset Repairer for null, which does not hold.
        ReportSendingCondition Condition(bool? value) => value switch
        {
            true => ClaimSource(CarId),
            false => ClaimSource(SmcId),
            _ => Repairer(EasdonsId)
        };
        var rule = Rule(match, new(["x@rule.example"], []), Condition(first), Condition(second));
        var facts = Facts(Rules() with { Rules = [rule] }) with { ClaimSourceId = CarId };

        Assert.Equal(expected, ReportDispatchPolicy.Evaluate(rule, facts));
    }

    // ---- Instruction mentions is searched -----------------------------------

    [Fact]
    public void InstructionMentionsIsSearchedIgnoringCaseAndSpaces()
    {
        var rules = Rules() with
        {
            Rules = [Rule(ReportSendingRuleMatch.All, new(["claims@rule.example"], []), Mentions("Car Claims"))]
        };
        var mentioned = Facts(rules) with { InstructionText = "Instruction from CARCLAIMS Ltd" };
        var notMentioned = Facts(rules) with { InstructionText = "Instruction from Luton" };

        var holds = ReportDispatchPolicy.Plan(mentioned, Now);
        var fails = ReportDispatchPolicy.Plan(notMentioned, Now);

        Assert.Equal("claims@rule.example", Assert.Single(holds.Cc).Address);
        Assert.Empty(fails.Cc);
    }

    [Theory]
    [InlineData("none", null)]
    [InlineData("blank", "  \n ")]
    public void InstructionMentionsDoesNotHoldWhenTheCaseHoldsNoInstructionText(string row, string? text)
    {
        Assert.NotEmpty(row);
        var rule = Rule(ReportSendingRuleMatch.All, new(["claims@rule.example"], []), Mentions("Car Claims"));
        var facts = Facts(Rules() with { Rules = [rule] }) with { InstructionText = text };

        var plan = ReportDispatchPolicy.Plan(facts, Now);

        Assert.False(ReportDispatchPolicy.Evaluate(rule, facts));
        Assert.Empty(plan.Cc);
    }

    [Fact]
    public void MentionsIgnoresCaseAndEveryWhitespace()
    {
        Assert.True(ReportDispatchPolicy.Mentions("Instruction for CarClaims", ["car claims"]));
        Assert.True(ReportDispatchPolicy.Mentions("Car\tClaims\nLtd", ["CARCLAIMS"]));
        Assert.True(ReportDispatchPolicy.Mentions("From Luton", ["Swinton", "luton"]));
        Assert.False(ReportDispatchPolicy.Mentions("Instruction", ["Luton", "  "]));
        Assert.False(ReportDispatchPolicy.Mentions("Instruction", []));
    }

    [Fact]
    public void AnOutcomeRuleReadsTheRecordedOutcomeCode()
    {
        var rule = Rule(ReportSendingRuleMatch.All, new(["x@rule.example"], []),
            new ReportSendingCondition(ReportSendingConditionKind.Outcome, ["total_loss", "cash_in_lieu"]));
        var rules = Rules() with { Rules = [rule] };

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
        var rule = Rule(ReportSendingRuleMatch.All, new(["x@rule.example"], []),
            new ReportSendingCondition(ReportSendingConditionKind.CaseType, ["Audit", "InspectionAndAudit"]));
        var rules = Rules() with { Rules = [rule] };

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
            var rule = Rule(ReportSendingRuleMatch.All, new(["x@rule.example"], []),
                new ReportSendingCondition(ReportSendingConditionKind.CaseType, [value]));
            return ReportDispatchPolicy.Evaluate(
                rule, Facts(Rules() with { Rules = [rule] }) with { CaseType = caseType, ReportWork = reportWork });
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

    [Fact]
    public void SenderNotReadsTheEffectiveSenderHoweverTheCaseHoldsIt()
    {
        var rule = Rule(ReportSendingRuleMatch.All, new(["x@rule.example"], []),
            new ReportSendingCondition(ReportSendingConditionKind.SenderNot, ["boss@insurer.example"]));
        var rules = Rules() with { Rules = [rule] };

        Assert.True(ReportDispatchPolicy.Evaluate(rule, Facts(rules)));
        Assert.False(
            ReportDispatchPolicy.Evaluate(rule, Facts(rules, instruction: Instruction(sender: "BOSS@insurer.example"))));
        Assert.False(
            ReportDispatchPolicy.Evaluate(
                rule, Facts(rules, withInstruction: false) with { OriginalSender = "boss@insurer.example" }));
        Assert.False(ReportDispatchPolicy.Evaluate(rule, Facts(rules, withInstruction: false)));
    }

    // ---- Reference prefix and Previous job (Report Sending SOP v8) -----------

    /// <summary>A reference prefix is the report reference's letters before a dot.</summary>
    [Fact]
    public void AReferencePrefixReadsTheReportReference()
    {
        var rule = Rule(ReportSendingRuleMatch.All, new(["x@rule.example"], []),
            new ReportSendingCondition(ReportSendingConditionKind.RefPrefix, ["a"]));
        var rules = Rules() with { Rules = [rule] };
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
        var rule = Rule(ReportSendingRuleMatch.All, new(["x@rule.example"], []),
            new ReportSendingCondition(ReportSendingConditionKind.PreviousJob, []));
        var facts = Facts(Rules() with { Rules = [rule] });

        Assert.True(ReportDispatchPolicy.Evaluate(rule, facts with { PreviousJob = true }));
        Assert.False(ReportDispatchPolicy.Evaluate(rule, facts with { PreviousJob = false }));
        Assert.Empty(ReportDispatchPolicy.Plan(facts, Now).Cc);
        Assert.Equal("x@rule.example", Assert.Single(ReportDispatchPolicy.Plan(facts with { PreviousJob = true }, Now).Cc).Address);
    }

    /// <summary>
    /// PCH under Report Sending SOP v8: a repairable iRevolution job copies
    /// iRevolution, reminds and checks the rate card; an audit job (the
    /// instruction says audit, the reference carries the a. prefix, or the
    /// vehicle is a previous job) removes that copy, and the removal wins.
    /// </summary>
    [Theory]
    [InlineData("repairable job", "PCH26118", "Instruction for AB12 CDE", false, true)]
    [InlineData("audit mentioned", "PCH26118", "Audit instruction for AB12 CDE", false, false)]
    [InlineData("audit reference", "a.PCH26118", "Instruction for AB12 CDE", false, false)]
    [InlineData("previous job", "PCH26118", "Instruction for AB12 CDE", true, false)]
    public void TheAuditRuleRemovesTheCopyTheRepairableRuleAdds(
        string row, string reference, string text, bool previousJob, bool copied)
    {
        Assert.NotEmpty(row);
        var rules = Rules() with
        {
            SendTo = ["SBL@connexus.example"],
            ReplyAll = false,
            Rules =
            [
                Rule(ReportSendingRuleMatch.All,
                    new(["repairs@irevolution.example"], [], Remind: "Authorise the garage", RateCard: true),
                    ClaimSource(CarId),
                    new ReportSendingCondition(ReportSendingConditionKind.Outcome, ["repairable"])),
                Rule(ReportSendingRuleMatch.Any,
                    new([], ["repairs@irevolution.example"]),
                    Mentions("audit"),
                    new ReportSendingCondition(ReportSendingConditionKind.RefPrefix, ["a"]),
                    new ReportSendingCondition(ReportSendingConditionKind.PreviousJob, []))
            ]
        };
        var facts = Facts(rules, instruction: Instruction(cc: ["handler@pch.example"])) with
        {
            CaseReference = reference,
            InstructionText = text,
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
            Assert.Equal("Rule 2: Mentions audit, Reference prefix a, Previous job", removal.Reason);
        }
    }

    // ---- subject ---------------------------------------------------------------

    // Which mailbox a report leaves from, and whether it is a reply, is
    // ResolveCaseMailSender's (operator, 12 October 2026), not the plan's.

    [Fact]
    public void AReplyKeepsTheInstructionsSubject()
    {
        var plan = Plan(
            Rules() with { SendFromMailbox = "info@collisionengineers.example" },
            instruction: Instruction(subject: "Claim AB12 CDE"));

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
        var facts = Facts(
            Rules() with { SendTo = ["fixed@principal.example"], SendFromMailbox = "info@collisionengineers.example" },
            withInstruction: false);

        var plan = ReportDispatchPolicy.Plan(facts, Now);

        Assert.Equal("AB12 CDE Report", plan.Subject);
        Assert.Null(plan.Stop);
    }

    [Fact]
    public void ANewMessageIsNamedForTheReferenceWhenTheCaseHasNoRegistration()
    {
        var facts = Facts(
            Rules() with { SendTo = ["fixed@principal.example"], SendFromMailbox = "info@collisionengineers.example" },
            withInstruction: false) with { Registration = null };

        Assert.Equal("DVR-31001 Report", ReportDispatchPolicy.Plan(facts, Now).Subject);
    }

    // ---- stops ----------------------------------------------------------------

    /// <summary>
    /// Only a rule's Stop stops a delivery: a Case with no instruction and
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
    public void TheFirstStopThatHoldsStopsTheDeliveryAndAnUnsetFactStopsNothing()
    {
        var rules = Rules() with
        {
            Rules =
            [
                Rule(ReportSendingRuleMatch.All, new([], [], Stop: "Never sent."), ClaimSource(SmcId)),
                Rule(ReportSendingRuleMatch.All, new([], [], Stop: "First stop."), ClaimSource(CarId)),
                Rule(ReportSendingRuleMatch.All, new([], [], Stop: "Second stop."), ClaimSource(CarId))
            ]
        };

        Assert.Equal("First stop.", Plan(rules, claimSource: CarId).Stop);
        // Claim Source unset: no stop rule holds.
        Assert.Null(ReportDispatchPolicy.Plan(Facts(rules), Now).Stop);
    }

    // ---- holds and after-send tasks ------------------------------------------

    [Fact]
    public void HoldsAreThePrincipalsHoldAndEveryRuleHoldThatApplies()
    {
        var rules = Rules() with
        {
            Hold = "Authorise the garage.",
            Rules =
            [
                Rule(ReportSendingRuleMatch.All, new([], [], Hold: "Check the WhatsApp group."), ClaimSource(CarId)),
                Rule(ReportSendingRuleMatch.All, new([], [], Hold: "Not this one."), ClaimSource(SmcId)),
                Rule(ReportSendingRuleMatch.All, new([], [], Hold: "Authorise the garage."), ClaimSource(CarId))
            ]
        };

        var plan = Plan(rules, claimSource: CarId);

        Assert.Equal(["Authorise the garage.", "Check the WhatsApp group."], plan.Holds);
    }

    [Fact]
    public void AfterSendTasksAreTheRemindersTheRuleRemindsAndTheGarageFigures()
    {
        var rules = Rules() with
        {
            Reminders = ["Send the WhatsApp.", "Send the WhatsApp."],
            GarageFigures = true,
            Rules =
            [
                Rule(ReportSendingRuleMatch.All, new([], [], Remind: "Tell Andy."), ClaimSource(CarId)),
                Rule(ReportSendingRuleMatch.All, new([], [], Remind: "Not this one."), ClaimSource(SmcId)),
                Rule(ReportSendingRuleMatch.All, new([], [], Remind: "Send the WhatsApp."), ClaimSource(CarId))
            ]
        };

        var repairable = ReportDispatchPolicy.Plan(
            Facts(rules) with { ClaimSourceId = CarId, Outcome = "repairable" }, Now);
        var totalLoss = ReportDispatchPolicy.Plan(
            Facts(rules) with { ClaimSourceId = CarId, Outcome = "total_loss" }, Now);
        var unticked = ReportDispatchPolicy.Plan(
            Facts(rules with { GarageFigures = false }) with { ClaimSourceId = CarId, Outcome = "repairable" }, Now);

        Assert.Equal(
            ["Send the WhatsApp.", "Tell Andy.", ReportDispatchPolicy.GarageFiguresTask],
            repairable.AfterSendTasks);
        Assert.Equal(["Send the WhatsApp.", "Tell Andy."], totalLoss.AfterSendTasks);
        Assert.Equal(["Send the WhatsApp.", "Tell Andy."], unticked.AfterSendTasks);
    }

    /// <summary>
    /// A rule's Rate card is the after-sending reminder to check the estimate
    /// against the card, after that rule's own reminder and once however many
    /// rules ask for it (operator, 16 October 2026).
    /// </summary>
    [Fact]
    public void ARulesRateCardJoinsTheTasksAfterItsReminderOnce()
    {
        var rules = Rules() with
        {
            Reminders = ["Send the WhatsApp."],
            Rules =
            [
                Rule(ReportSendingRuleMatch.All, new([], [], Remind: "Tell Andy.", RateCard: true), ClaimSource(CarId)),
                Rule(ReportSendingRuleMatch.All, new([], [], RateCard: true), ClaimSource(SmcId)),
                Rule(ReportSendingRuleMatch.All, new([], [], Remind: "Call the garage.", RateCard: true), ClaimSource(CarId))
            ]
        };

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
        var rules = Rules() with
        {
            Attach = ReportSendingAttachments.Default with { VehicleImagesDocument = true, FigureBreakdown = true }
        };

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
    /// A rule that holds replaces the attach flags it names for this send, in
    /// rule order so a later rule wins, and a flag it leaves unnamed keeps the
    /// Principal's (Report Sending SOP v8, rule R14). Companions, filed
    /// estimates and warnings all read the result.
    /// </summary>
    [Fact]
    public void AnAppliedRulesAttachOverrideChangesTheCompanionsEstimatesAndWarnings()
    {
        var rules = Rules() with
        {
            Attach = ReportSendingAttachments.Default with { VehicleImagesDocument = true, Audatex = true },
            Rules =
            [
                Rule(ReportSendingRuleMatch.All,
                    new([], [], Attach: new(VehicleImagesDocument: false, FigureBreakdown: true, FeeNoteSeparate: false)),
                    ClaimSource(CarId)),
                Rule(ReportSendingRuleMatch.All, new([], [], Attach: new(Audatex: false, Estimate: true)), ClaimSource(CarId)),
                Rule(ReportSendingRuleMatch.All, new([], [], Attach: new(Estimate: false)), ClaimSource(CarId)),
                Rule(ReportSendingRuleMatch.All, new([], [], Attach: new(FigureBreakdown: false)), ClaimSource(SmcId))
            ]
        };
        var facts = Facts(rules) with { FiledEstimates = [AudatexFiled, GlassFiled] };

        var principal = ReportDispatchPolicy.Plan(facts with { ClaimSourceId = EasdonsId }, Now);
        var overridden = ReportDispatchPolicy.Plan(facts with { ClaimSourceId = CarId }, Now);

        Assert.Equal([CaseReportArtifactKind.ImagePack], principal.RequiredCompanions);
        Assert.Equal([CaseReportArtifactKind.ImagePack], principal.MissingCompanions);
        Assert.Equal([AudatexFiled.Attachment], principal.FiledEstimates);
        Assert.Contains(ReportDispatchPolicy.NoSeparateFeeNoteWarning, principal.Warnings);

        // The images document is no longer required and the figure breakdown is;
        // Audatex is off, and the third rule's Estimate off beats the second's on.
        Assert.Equal([CaseReportArtifactKind.RepairSpecification], overridden.RequiredCompanions);
        Assert.Equal([CaseReportArtifactKind.RepairSpecification], overridden.MissingCompanions);
        Assert.Empty(overridden.FiledEstimates);
        Assert.Empty(overridden.Warnings);
    }

    [Fact]
    public void TheAudatexTickAttachesTheAudatexEstimatesAndTheEstimateTickTheOthers()
    {
        var facts = Facts(Rules()) with { FiledEstimates = [AudatexFiled, GlassFiled, UnknownFiled] };

        PrincipalReportSendingRules Ticks(bool estimate, bool audatex) =>
            Rules() with { Attach = ReportSendingAttachments.Default with { Estimate = estimate, Audatex = audatex } };
        IReadOnlyList<StaffMailAttachment> Filed(bool estimate, bool audatex) =>
            ReportDispatchPolicy.Plan(facts with { Rules = Ticks(estimate, audatex) }, Now).FiledEstimates;

        Assert.Equal([AudatexFiled.Attachment], Filed(estimate: false, audatex: true));
        Assert.Equal([GlassFiled.Attachment, UnknownFiled.Attachment], Filed(estimate: true, audatex: false));
        Assert.Equal(
            [AudatexFiled.Attachment, GlassFiled.Attachment, UnknownFiled.Attachment],
            Filed(estimate: true, audatex: true));
        Assert.Empty(Filed(estimate: false, audatex: false));
        Assert.DoesNotContain(
            ReportDispatchPolicy.NoFiledEstimateWarning,
            ReportDispatchPolicy.Plan(facts with { Rules = Ticks(true, true) }, Now).Warnings);
    }

    [Theory]
    [InlineData("estimate tick, Audatex filed", true, false, "Audatex")]
    [InlineData("Audatex tick, other filed", false, true, "Glass's")]
    [InlineData("estimate tick, nothing filed", true, false, null)]
    [InlineData("Audatex tick, nothing filed", false, true, null)]
    public void ATickWithNoMatchingFiledEstimateWarns(string row, bool estimate, bool audatex, string? filedProvider)
    {
        Assert.NotEmpty(row);
        var rules = Rules() with
        {
            Attach = ReportSendingAttachments.Default with { Estimate = estimate, Audatex = audatex }
        };
        var facts = Facts(rules) with
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
    /// Each Offer tick offers the Case's files of its own type, in the Case's
    /// order, and nothing while it is off (R1151).
    /// </summary>
    [Fact]
    public void EachOfferTickOffersOnlyTheCasesFilesOfItsType()
    {
        var facts = Facts(Rules()) with { OfferableFiles = [ExternalOffered, InvoiceOffered, SecondExternalOffered] };

        PrincipalReportSendingRules Ticks(bool estimates, bool invoices) =>
            Rules() with
            {
                Attach = ReportSendingAttachments.Default with
                {
                    OfferExternalEstimates = estimates,
                    OfferRepairInvoices = invoices
                }
            };
        IReadOnlyList<OfferedCaseFile> Offered(bool estimates, bool invoices) =>
            ReportDispatchPolicy.Plan(facts with { Rules = Ticks(estimates, invoices) }, Now).OfferedFiles;

        Assert.Equal([ExternalOffered, SecondExternalOffered], Offered(estimates: true, invoices: false));
        Assert.Equal([InvoiceOffered], Offered(estimates: false, invoices: true));
        Assert.Equal([ExternalOffered, InvoiceOffered, SecondExternalOffered], Offered(estimates: true, invoices: true));
        Assert.Empty(Offered(estimates: false, invoices: false));
        // Offering adds no warning and no hold.
        var plan = ReportDispatchPolicy.Plan(facts with { Rules = Ticks(true, true) }, Now);
        Assert.DoesNotContain(ReportDispatchPolicy.NoFiledEstimateWarning, plan.Warnings);
        Assert.Empty(plan.Holds);
    }

    /// <summary>
    /// A file the Estimate or Audatex tick already attaches is not offered a
    /// second time (R1158); with that tick off it is offered.
    /// </summary>
    [Fact]
    public void AFileTheEstimateTickAttachesIsNotOfferedASecondTime()
    {
        var recognised = new FiledEstimateAttachment(ExternalOffered.Attachment, "Glass's");
        var rules = Rules() with
        {
            Attach = ReportSendingAttachments.Default with { Estimate = true, OfferExternalEstimates = true }
        };
        var facts = Facts(rules) with
        {
            FiledEstimates = [recognised],
            OfferableFiles = [ExternalOffered, SecondExternalOffered]
        };

        var plan = ReportDispatchPolicy.Plan(facts, Now);
        Assert.Equal([ExternalOffered.Attachment], plan.FiledEstimates);
        Assert.Equal([SecondExternalOffered], plan.OfferedFiles);

        var estimateOff = ReportDispatchPolicy.Plan(
            facts with { Rules = rules with { Attach = rules.Attach with { Estimate = false } } }, Now);
        Assert.Empty(estimateOff.FiledEstimates);
        Assert.Equal([ExternalOffered, SecondExternalOffered], estimateOff.OfferedFiles);
    }

    /// <summary>An applied rule's override turns an Offer tick on or off for the send (R1151).</summary>
    [Fact]
    public void AnAppliedRulesOverrideTurnsAnOfferTickOnOrOff()
    {
        var rules = Rules() with
        {
            Attach = ReportSendingAttachments.Default with { OfferExternalEstimates = true },
            Rules =
            [
                Rule(ReportSendingRuleMatch.All,
                    new([], [], Attach: new(OfferExternalEstimates: false, OfferRepairInvoices: true)),
                    ClaimSource(CarId))
            ]
        };
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
        var notSeparate = Plan(Rules() with { Attach = ReportSendingAttachments.Default with { FeeNoteSeparate = false } });

        Assert.Contains(ReportDispatchPolicy.NoSeparateFeeNoteWarning, plan.Warnings);
        Assert.Empty(plan.Holds);
        Assert.DoesNotContain(ReportDispatchPolicy.NoSeparateFeeNoteWarning, confirmed.Warnings);
        Assert.Empty(notSeparate.Warnings);
    }

    [Fact]
    public void AReportWithoutVehicleImagesRaisesNoWarning()
    {
        var plan = ReportDispatchPolicy.Plan(
            Facts(Rules() with { Attach = ReportSendingAttachments.Default with { ReportImages = false } })
                with { ConfirmedArtifacts = [CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.FeeNote] }, Now);

        Assert.Empty(plan.Warnings);
        Assert.Empty(plan.Holds);
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
    [InlineData("instruction text")]
    [InlineData("rate card")]
    [InlineData("attach override")]
    public void TheFingerprintChangesWithEveryFactTheRulesRead(string fact)
    {
        var facts = FingerprintFacts();
        ReportDispatchFacts WithActions(ReportSendingActions then) =>
            facts with { Rules = facts.Rules with { Rules = [Rule(ReportSendingRuleMatch.All, then, ClaimSource(CarId))] } };
        var changed = fact switch
        {
            "rules" => facts with { Rules = facts.Rules with { Hold = "Wait." } },
            "rate card" => WithActions(new(["x@rule.example"], [], RateCard: true)),
            "attach override" => WithActions(new(["x@rule.example"], [], Attach: new(Estimate: true))),
            "instruction" => facts with { Instruction = facts.Instruction! with { RetainedMessageId = Guid.NewGuid() } },
            "sender" => facts with { Instruction = facts.Instruction! with { EffectiveSender = "other@insurer.example" } },
            "claim source" => facts with { ClaimSourceId = SmcId },
            "repairer" => facts with { RepairerId = Guid.NewGuid() },
            "outcome" => facts with { Outcome = "total_loss" },
            _ => facts with { InstructionText = "Instruction from Luton" }
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
                FiledEstimates = [AudatexFiled]
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
        var facts = Facts(Rules() with { Cc = ["always@principal.example"] });
        var plan = ReportDispatchPolicy.Plan(facts, Now);

        var (review, removed) = ReportDispatchPolicy.Reconcile(plan, new([Sender], []));

        Assert.Equal([Sender], review.To);
        Assert.Empty(review.Cc);
        Assert.Empty(removed);
    }

    [Fact]
    public void ReconcileStripsEveryExcludedAddressWhereverItCameFrom()
    {
        var rules = Rules() with
        {
            NeverCc = ["never@insurer.example"],
            Rules = [Rule(ReportSendingRuleMatch.All, new([], ["gone@insurer.example"]), Mentions("Luton"))]
        };
        var facts = Facts(rules) with { InstructionText = "Instruction from Luton" };
        var plan = ReportDispatchPolicy.Plan(facts, Now);

        // Neither address was copied by the plan; staff typed both.
        var (review, removed) = ReportDispatchPolicy.Reconcile(
            plan,
            new([Sender, " Never@Insurer.example "], ["gone@insurer.example", "ok@insurer.example", "never@insurer.example"]));

        Assert.Empty(plan.Removed);
        Assert.Equal([Sender], review.To);
        Assert.Equal(["ok@insurer.example"], review.Cc);
        Assert.Equal(
            [("never@insurer.example", "never cc"), ("gone@insurer.example", "Rule 1: Mentions Luton")],
            removed.Select(removal => (removal.Address, removal.Reason)));
    }

    [Fact]
    public void ReconcileReportsTheCopiesThePlanItselfRemoved()
    {
        var facts = Facts(
            Rules() with { NeverCc = ["never@insurer.example"] },
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

    private static PrincipalReportSendingRules Rules() => PrincipalReportSendingRules.Default;

    private static ReportSendingCondition ClaimSource(Guid id) =>
        new(ReportSendingConditionKind.ClaimSource, [id.ToString("D")]);

    private static ReportSendingCondition Repairer(Guid id) =>
        new(ReportSendingConditionKind.Repairer, [id.ToString("D")]);

    private static ReportSendingCondition Mentions(string text) =>
        new(ReportSendingConditionKind.Mentions, [text]);

    private static ReportSendingRule Rule(
        ReportSendingRuleMatch match, ReportSendingActions then, params ReportSendingCondition[] conditions) =>
        new(match, conditions, then);

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
        Facts(Rules() with { Rules = [Rule(ReportSendingRuleMatch.All, new(["x@rule.example"], []), ClaimSource(CarId))] })
            with
            {
                ClaimSourceId = CarId,
                RepairerId = EasdonsId,
                Outcome = "repairable",
                InstructionText = "Instruction for AB12 CDE"
            };

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

using Pegasus.Core.Assessment;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

/// <summary>
/// The one report dispatch plan (Report Sending SOP v5, rule R12; operator,
/// 6 October 2026): recipients in the SOP's order with removals winning,
/// three-valued rule evaluation, the reply or new message, required
/// companions and filed estimates, holds, a rule's Stop, questions and
/// after-send tasks. Pure: every case here is facts in, plan out.
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

        var plan = ReportDispatchPolicy.Plan(facts, [], Now);

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

    // ---- rule evaluation: three values --------------------------------------

    [Fact]
    public void AClaimSourceTheCaseHasDecidesTheRuleWithoutAskingStaff()
    {
        var rule = Rule(ReportSendingRuleMatch.All, new(["x@rule.example"], []), ClaimSource(CarId));

        var holds = Plan(Rules() with { Rules = [rule] }, claimSource: CarId);
        var other = Plan(Rules() with { Rules = [rule] }, claimSource: SmcId);

        Assert.Equal("x@rule.example", Assert.Single(holds.Cc).Address);
        Assert.Empty(holds.Undecided);
        Assert.Empty(other.Cc);
        Assert.Empty(other.Undecided);
    }

    [Fact]
    public void AnUnsetClaimSourceAsksStaffByName()
    {
        var facts = Facts(Rules() with
        {
            Rules = [Rule(ReportSendingRuleMatch.All, new(["x@rule.example"], []), ClaimSource(CarId))]
        }) with { ContactNames = new Dictionary<Guid, string> { [CarId] = "Car 2 Go" } };

        var plan = ReportDispatchPolicy.Plan(facts, [], Now);

        var question = Assert.Single(plan.Undecided);
        Assert.Equal(0, question.RuleIndex);
        Assert.Equal(0, question.ConditionIndex);
        Assert.Equal("Is the Claim Source Car 2 Go?", question.Question);
        Assert.Empty(plan.Cc);
    }

    [Fact]
    public void EachQuestionNamesWhatItAsks()
    {
        var rules = Rules() with
        {
            Rules =
            [
                Rule(ReportSendingRuleMatch.All, new(["a@rule.example"], []), Repairer(EasdonsId)),
                Rule(ReportSendingRuleMatch.All, new(["b@rule.example"], []), ClaimSource(SmcId)),
                Rule(ReportSendingRuleMatch.All, new(["c@rule.example"], []),
                    new ReportSendingCondition(ReportSendingConditionKind.Outcome, ["total_loss", "cash_in_lieu"])),
                Rule(ReportSendingRuleMatch.All, new(["d@rule.example"], []),
                    new ReportSendingCondition(ReportSendingConditionKind.SenderNot, ["boss@insurer.example"]))
            ]
        };
        var facts = Facts(rules, withInstruction: false)
            with { ContactNames = new Dictionary<Guid, string> { [EasdonsId] = "Easdons" } };

        var plan = ReportDispatchPolicy.Plan(facts, [], Now);

        Assert.Equal(
            [
                "Is the Repairer Easdons?",
                "Is the Claim Source that contact?",
                "Is the outcome Total loss or Cash in lieu?",
                "Was the instruction sent by someone other than boss@insurer.example?"
            ],
            plan.Undecided.Select(question => question.Question));
    }

    [Fact]
    public void AnAnswerResolvesAnUnsetFactButNeverOverridesOneTheCaseHas()
    {
        var rules = Rules() with
        {
            Rules = [Rule(ReportSendingRuleMatch.All, new(["x@rule.example"], []), ClaimSource(CarId))]
        };

        var answered = ReportDispatchPolicy.Plan(Facts(rules), [new(0, 0, true)], Now);
        var declined = ReportDispatchPolicy.Plan(Facts(rules), [new(0, 0, false)], Now);
        var factWins = ReportDispatchPolicy.Plan(Facts(rules) with { ClaimSourceId = SmcId }, [new(0, 0, true)], Now);

        Assert.Equal("x@rule.example", Assert.Single(answered.Cc).Address);
        Assert.Empty(answered.Undecided);
        Assert.Empty(declined.Cc);
        Assert.Empty(declined.Undecided);
        Assert.Empty(factWins.Cc);
    }

    [Theory]
    [InlineData("all-tt", ReportSendingRuleMatch.All, true, true, true)]
    [InlineData("all-tf", ReportSendingRuleMatch.All, true, false, false)]
    [InlineData("all-fn", ReportSendingRuleMatch.All, false, null, false)]
    [InlineData("all-tn", ReportSendingRuleMatch.All, true, null, null)]
    [InlineData("any-ff", ReportSendingRuleMatch.Any, false, false, false)]
    [InlineData("any-tf", ReportSendingRuleMatch.Any, true, false, true)]
    [InlineData("any-tn", ReportSendingRuleMatch.Any, true, null, true)]
    [InlineData("any-fn", ReportSendingRuleMatch.Any, false, null, null)]
    public void AllAndAnyAreThreeValued(
        string row, ReportSendingRuleMatch match, bool? first, bool? second, bool? expected)
    {
        Assert.NotEmpty(row);
        // The Case's Claim Source is Car 2 Go; Repairer is unset. A condition
        // is true when it names the Case's Claim Source, false when it names
        // another, and null when it reads the unset Repairer.
        ReportSendingCondition Condition(bool? value) => value switch
        {
            true => ClaimSource(CarId),
            false => ClaimSource(SmcId),
            _ => Repairer(EasdonsId)
        };
        var rule = Rule(match, new(["x@rule.example"], []), Condition(first), Condition(second));
        var facts = Facts(Rules() with { Rules = [rule] }) with { ClaimSourceId = CarId };

        Assert.Equal(expected, ReportDispatchPolicy.Evaluate(0, rule, facts, []));
    }

    [Fact]
    public void OnlyTheConditionsOfARuleThatIsStillOpenAreAsked()
    {
        var rules = Rules() with
        {
            Rules =
            [
                // false (Claim Source is SMC) with an open Repairer: decided, nothing asked.
                Rule(ReportSendingRuleMatch.All, new(["a@rule.example"], []),
                    ClaimSource(CarId), Repairer(EasdonsId)),
                // true Claim Source with an open Repairer: asks only the Repairer.
                Rule(ReportSendingRuleMatch.All, new(["b@rule.example"], []),
                    ClaimSource(SmcId), Repairer(EasdonsId))
            ]
        };

        var plan = ReportDispatchPolicy.Plan(Facts(rules) with { ClaimSourceId = SmcId }, [], Now);

        var question = Assert.Single(plan.Undecided);
        Assert.Equal(1, question.RuleIndex);
        Assert.Equal(1, question.ConditionIndex);
    }

    [Fact]
    public void EvaluateTakesTheAnswersOfTheRuleAtItsOwnIndex()
    {
        var first = Rule(ReportSendingRuleMatch.All, new(["x@rule.example"], []), Mentions("Luton"));
        var second = Rule(ReportSendingRuleMatch.All, new(["x@rule.example"], []), Mentions("Luton"));
        var facts = Facts(Rules() with { Rules = [first, second] });

        Assert.True(ReportDispatchPolicy.Evaluate(1, second, facts, [new(1, 0, true)]));
        Assert.Null(ReportDispatchPolicy.Evaluate(0, first, facts, [new(1, 0, true)]));
    }

    // ---- Instruction mentions is searched -----------------------------------

    [Fact]
    public void InstructionMentionsIsSearchedIgnoringCaseAndSpacesAndNeverAsked()
    {
        var rules = Rules() with
        {
            Rules = [Rule(ReportSendingRuleMatch.All, new(["claims@rule.example"], []), Mentions("Car Claims"))]
        };
        var mentioned = Facts(rules) with { InstructionText = "Instruction from CARCLAIMS Ltd" };
        var notMentioned = Facts(rules) with { InstructionText = "Instruction from Luton" };

        var holds = ReportDispatchPolicy.Plan(mentioned, [], Now);
        var fails = ReportDispatchPolicy.Plan(notMentioned, [], Now);

        Assert.Empty(holds.Undecided);
        Assert.Equal("claims@rule.example", Assert.Single(holds.Cc).Address);
        Assert.Empty(fails.Undecided);
        Assert.Empty(fails.Cc);
        // A decision never overrides what the instruction text says.
        Assert.Equal("claims@rule.example", Assert.Single(ReportDispatchPolicy.Plan(mentioned, [new(0, 0, false)], Now).Cc).Address);
        Assert.Empty(ReportDispatchPolicy.Plan(notMentioned, [new(0, 0, true)], Now).Cc);
    }

    [Theory]
    [InlineData("none", null)]
    [InlineData("blank", "  \n ")]
    public void InstructionMentionsIsAskedOnlyWhenTheCaseHoldsNoInstructionText(string row, string? text)
    {
        Assert.NotEmpty(row);
        var rules = Rules() with
        {
            Rules = [Rule(ReportSendingRuleMatch.All, new(["claims@rule.example"], []), Mentions("Car Claims"))]
        };
        var facts = Facts(rules) with { InstructionText = text };

        var plan = ReportDispatchPolicy.Plan(facts, [], Now);
        var answered = ReportDispatchPolicy.Plan(facts, [new(0, 0, true)], Now);

        Assert.Equal("Does the instruction mention \"Car Claims\"?", Assert.Single(plan.Undecided).Question);
        Assert.Empty(answered.Undecided);
        Assert.Equal("claims@rule.example", Assert.Single(answered.Cc).Address);
    }

    [Fact]
    public void ImagesFromAndBodyshopMentionsAreAlwaysAskedUntilAnswered()
    {
        var rules = Rules() with
        {
            Rules =
            [
                Rule(ReportSendingRuleMatch.All, new(["a@rule.example"], []),
                    new ReportSendingCondition(ReportSendingConditionKind.ImagesFrom, ["SWINTON"])),
                Rule(ReportSendingRuleMatch.All, new(["b@rule.example"], []),
                    new ReportSendingCondition(ReportSendingConditionKind.BodyshopMentions, ["Smith's"]))
            ]
        };
        // The instruction text names both, but neither condition reads it.
        var facts = Facts(rules) with { InstructionText = "Images from SWINTON; bodyshop Smith's" };

        var plan = ReportDispatchPolicy.Plan(facts, [], Now);
        var answered = ReportDispatchPolicy.Plan(facts, [new(0, 0, true), new(1, 0, false)], Now);

        Assert.Equal(
            ["Are the images from SWINTON?", "Do the bodyshop details mention Smith's?"],
            plan.Undecided.Select(question => question.Question));
        Assert.Empty(answered.Undecided);
        Assert.Equal("a@rule.example", Assert.Single(answered.Cc).Address);
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

        Assert.True(ReportDispatchPolicy.Evaluate(0, rule, Facts(rules) with { Outcome = "cash_in_lieu" }, []));
        Assert.False(ReportDispatchPolicy.Evaluate(0, rule, Facts(rules) with { Outcome = "repairable" }, []));
        Assert.Null(ReportDispatchPolicy.Evaluate(0, rule, Facts(rules), []));
    }

    [Fact]
    public void SenderNotReadsTheEffectiveSenderHoweverTheCaseHoldsIt()
    {
        var rule = Rule(ReportSendingRuleMatch.All, new(["x@rule.example"], []),
            new ReportSendingCondition(ReportSendingConditionKind.SenderNot, ["boss@insurer.example"]));
        var rules = Rules() with { Rules = [rule] };

        Assert.True(ReportDispatchPolicy.Evaluate(0, rule, Facts(rules), []));
        Assert.False(
            ReportDispatchPolicy.Evaluate(0, rule, Facts(rules, instruction: Instruction(sender: "BOSS@insurer.example")), []));
        Assert.False(
            ReportDispatchPolicy.Evaluate(
                0, rule, Facts(rules, withInstruction: false) with { OriginalSender = "boss@insurer.example" }, []));
        Assert.Null(ReportDispatchPolicy.Evaluate(0, rule, Facts(rules, withInstruction: false), []));
    }

    // ---- mode, mailbox, subject ---------------------------------------------

    [Fact]
    public void AnInstructionIsRepliedToFromTheMailboxHoldingItKeepingItsSubject()
    {
        var plan = Plan(
            Rules() with { SendFromMailbox = "info@collisionengineers.example" },
            instruction: Instruction(subject: "Claim AB12 CDE"));

        Assert.Equal(ReportDispatchMode.ReplyInThread, plan.Mode);
        Assert.Equal("engineers@collisionengineers.example", plan.MailboxAddress);
        Assert.Equal(MailboxId, plan.MailboxId);
        Assert.Equal("Re: Claim AB12 CDE", plan.Subject);
        Assert.Null(plan.Stop);
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
    public void WithNoInstructionTheReportIsANewMessageFromTheSendFromMailbox()
    {
        var facts = Facts(
            Rules() with { SendTo = ["fixed@principal.example"], SendFromMailbox = "info@collisionengineers.example" },
            withInstruction: false) with { DefaultMailboxAddress = "default@collisionengineers.example" };

        var plan = ReportDispatchPolicy.Plan(facts, [], Now);

        Assert.Equal(ReportDispatchMode.NewMessage, plan.Mode);
        Assert.Equal("info@collisionengineers.example", plan.MailboxAddress);
        Assert.Null(plan.MailboxId);
        Assert.Equal("AB12 CDE Report", plan.Subject);
        Assert.Null(plan.Stop);
    }

    [Fact]
    public void WithNoSendFromMailboxANewMessageLeavesFromTheDefaultMailbox()
    {
        var facts = Facts(Rules() with { SendTo = ["fixed@principal.example"] }, withInstruction: false)
            with { DefaultMailboxAddress = "default@collisionengineers.example" };

        var plan = ReportDispatchPolicy.Plan(facts, [], Now);

        Assert.Equal(ReportDispatchMode.NewMessage, plan.Mode);
        Assert.Equal("default@collisionengineers.example", plan.MailboxAddress);
        Assert.Null(plan.MailboxId);
    }

    [Fact]
    public void ANewMessageIsNamedForTheReferenceWhenTheCaseHasNoRegistration()
    {
        var facts = Facts(
            Rules() with { SendTo = ["fixed@principal.example"], SendFromMailbox = "info@collisionengineers.example" },
            withInstruction: false) with { Registration = null };

        Assert.Equal("DVR-31001 Report", ReportDispatchPolicy.Plan(facts, [], Now).Subject);
    }

    // ---- stops ----------------------------------------------------------------

    /// <summary>
    /// Only a rule's Stop stops a delivery: a Case with no instruction, no
    /// mailbox to send from and nobody to send to is not stopped by the plan.
    /// </summary>
    [Fact]
    public void NoInstructionNoMailboxAndNoRecipientAreNotStops()
    {
        var plan = Plan(Rules(), withInstruction: false);

        Assert.Empty(plan.To);
        Assert.Null(plan.MailboxAddress);
        Assert.Null(plan.Stop);
        Assert.False(plan.StopPossible);
    }

    [Fact]
    public void TheFirstStopThatHoldsStopsTheDeliveryAndAnOpenOneOnlyMakesAStopPossible()
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

        var stopped = Plan(rules, claimSource: CarId);
        Assert.Equal("First stop.", stopped.Stop);
        Assert.False(stopped.StopPossible);

        // Claim Source unset: the stop rules are questions, not stops.
        var open = ReportDispatchPolicy.Plan(Facts(rules), [], Now);
        Assert.Null(open.Stop);
        Assert.True(open.StopPossible);
        Assert.Equal(3, open.Undecided.Count);

        var declined = ReportDispatchPolicy.Plan(Facts(rules), [new(0, 0, false), new(1, 0, false), new(2, 0, false)], Now);
        Assert.Null(declined.Stop);
        Assert.False(declined.StopPossible);
    }

    [Fact]
    public void AnOpenRuleWithoutAStopMakesNoStopPossible()
    {
        var rules = Rules() with
        {
            Rules = [Rule(ReportSendingRuleMatch.All, new(["x@rule.example"], [], Hold: "Wait."), ClaimSource(CarId))]
        };

        var plan = ReportDispatchPolicy.Plan(Facts(rules), [], Now);

        Assert.Single(plan.Undecided);
        Assert.False(plan.StopPossible);
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
        Assert.Empty(plan.PossibleHolds);
    }

    [Fact]
    public void PossibleHoldsAreTheHoldsOfRulesStillUndecided()
    {
        var rules = Rules() with
        {
            Hold = "Authorise the garage.",
            Rules =
            [
                Rule(ReportSendingRuleMatch.All, new([], [], Hold: "Check the WhatsApp group."), ClaimSource(CarId)),
                Rule(ReportSendingRuleMatch.All, new([], [], Hold: "Authorise the garage."), ClaimSource(CarId)),
                Rule(ReportSendingRuleMatch.All, new([], [], Hold: "Decided against."), Mentions("Luton"))
            ]
        };
        var facts = Facts(rules) with { InstructionText = "Instruction from Swinton" };

        var open = ReportDispatchPolicy.Plan(facts, [], Now);
        var answered = ReportDispatchPolicy.Plan(facts, [new(0, 0, true), new(1, 0, false)], Now);

        Assert.Equal(["Authorise the garage."], open.Holds);
        // A possible hold already in Holds is not offered twice; a decided rule offers none.
        Assert.Equal(["Check the WhatsApp group."], open.PossibleHolds);
        Assert.Equal(["Authorise the garage.", "Check the WhatsApp group."], answered.Holds);
        Assert.Empty(answered.PossibleHolds);
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
            Facts(rules) with { ClaimSourceId = CarId, Outcome = "repairable" }, [], Now);
        var totalLoss = ReportDispatchPolicy.Plan(
            Facts(rules) with { ClaimSourceId = CarId, Outcome = "total_loss" }, [], Now);
        var unticked = ReportDispatchPolicy.Plan(
            Facts(rules with { GarageFigures = false }) with { ClaimSourceId = CarId, Outcome = "repairable" }, [], Now);

        Assert.Equal(
            ["Send the WhatsApp.", "Tell Andy.", ReportDispatchPolicy.GarageFiguresTask],
            repairable.AfterSendTasks);
        Assert.Equal(["Send the WhatsApp.", "Tell Andy."], totalLoss.AfterSendTasks);
        Assert.Equal(["Send the WhatsApp.", "Tell Andy."], unticked.AfterSendTasks);
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
            Facts(rules) with { ConfirmedArtifacts = [CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.ImagePack] },
            [], Now);
        var complete = ReportDispatchPolicy.Plan(
            Facts(rules) with
            {
                ConfirmedArtifacts = [CaseReportArtifactKind.ImagePack, CaseReportArtifactKind.RepairSpecification]
            },
            [], Now);

        Assert.Equal(
            [CaseReportArtifactKind.ImagePack, CaseReportArtifactKind.RepairSpecification],
            missing.RequiredCompanions);
        Assert.Equal(missing.RequiredCompanions, missing.MissingCompanions);
        Assert.Equal([CaseReportArtifactKind.RepairSpecification], partly.MissingCompanions);
        Assert.Empty(complete.MissingCompanions);
        Assert.Empty(Plan(Rules()).RequiredCompanions);
    }

    [Fact]
    public void TheAudatexTickAttachesTheAudatexEstimatesAndTheEstimateTickTheOthers()
    {
        var facts = Facts(Rules()) with { FiledEstimates = [AudatexFiled, GlassFiled, UnknownFiled] };

        PrincipalReportSendingRules Ticks(bool estimate, bool audatex) =>
            Rules() with { Attach = ReportSendingAttachments.Default with { Estimate = estimate, Audatex = audatex } };
        IReadOnlyList<StaffMailAttachment> Filed(bool estimate, bool audatex) =>
            ReportDispatchPolicy.Plan(facts with { Rules = Ticks(estimate, audatex) }, [], Now).FiledEstimates;

        Assert.Equal([AudatexFiled.Attachment], Filed(estimate: false, audatex: true));
        Assert.Equal([GlassFiled.Attachment, UnknownFiled.Attachment], Filed(estimate: true, audatex: false));
        Assert.Equal(
            [AudatexFiled.Attachment, GlassFiled.Attachment, UnknownFiled.Attachment],
            Filed(estimate: true, audatex: true));
        Assert.Empty(Filed(estimate: false, audatex: false));
        Assert.DoesNotContain(
            ReportDispatchPolicy.NoFiledEstimateWarning,
            ReportDispatchPolicy.Plan(facts with { Rules = Ticks(true, true) }, [], Now).Warnings);
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

        var plan = ReportDispatchPolicy.Plan(facts, [], Now);

        Assert.Empty(plan.FiledEstimates);
        Assert.Contains(ReportDispatchPolicy.NoFiledEstimateWarning, plan.Warnings);
        Assert.Empty(plan.Holds);
    }

    [Fact]
    public void WithoutEstimateOrAudatexNoFiledEstimateIsAttachedOrWarnedAbout()
    {
        var plan = ReportDispatchPolicy.Plan(
            Facts(Rules()) with { FiledEstimates = [AudatexFiled, GlassFiled] }, [], Now);

        Assert.Empty(plan.FiledEstimates);
        Assert.DoesNotContain(ReportDispatchPolicy.NoFiledEstimateWarning, plan.Warnings);
    }

    [Fact]
    public void ASeparateFeeNoteNotConfirmedIsAWarningNotAHold()
    {
        var plan = Plan(Rules());
        var confirmed = ReportDispatchPolicy.Plan(
            Facts(Rules()) with { ConfirmedArtifacts = [CaseReportArtifactKind.FeeNote] }, [], Now);
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
                with { ConfirmedArtifacts = [CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.FeeNote] },
            [], Now);

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
        Assert.Equal("morning", ReportDispatchPolicy.Plan(Facts(Rules()), [], Now).Greeting);
        Assert.Equal(
            "afternoon",
            ReportDispatchPolicy.Plan(Facts(Rules()), [], new DateTimeOffset(2026, 10, 5, 11, 0, 0, TimeSpan.Zero)).Greeting);
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
    public void TheFingerprintChangesWithEveryFactTheQuestionsDependOn(string fact)
    {
        var facts = FingerprintFacts();
        var changed = fact switch
        {
            "rules" => facts with { Rules = facts.Rules with { Hold = "Wait." } },
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
                FiledEstimates = [AudatexFiled],
                DefaultMailboxAddress = "default@collisionengineers.example",
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

    /// <summary>
    /// The form is drawn before staff answer, so it cannot show a copy an
    /// answer makes: the send adds it to the reviewed Cc.
    /// </summary>
    [Fact]
    public void ReconcileAddsACopyAnAnswerMadeThatTheFormCouldNotShow()
    {
        var rules = Rules() with
        {
            Rules = [Rule(ReportSendingRuleMatch.All, new(["luton@rule.example"], []), Mentions("Luton"))]
        };
        var facts = Facts(rules, instruction: Instruction(cc: ["a@insurer.example"]));
        var seeded = ReportDispatchPolicy.Plan(facts, [], Now);
        var answered = ReportDispatchPolicy.Plan(facts, [new(0, 0, true)], Now);

        var (review, removed) = ReportDispatchPolicy.Reconcile(
            answered, seeded, CaseReportDeliveryPolicy.SuggestedReview(seeded));
        var (typed, _) = ReportDispatchPolicy.Reconcile(
            answered, seeded, new([Sender], ["LUTON@rule.example"]));

        Assert.Equal([Sender], review.To);
        Assert.Equal(["a@insurer.example", "luton@rule.example"], review.Cc);
        Assert.Empty(removed);
        // A copy staff already typed is not added twice.
        Assert.Equal(["LUTON@rule.example"], typed.Cc);
    }

    [Fact]
    public void ReconcileLeavesOutACopyStaffRemovedThatTheFormShowed()
    {
        var facts = Facts(Rules() with { Cc = ["always@principal.example"] });
        var plan = ReportDispatchPolicy.Plan(facts, [], Now);

        var (review, removed) = ReportDispatchPolicy.Reconcile(plan, plan, new([Sender], []));

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
        var plan = ReportDispatchPolicy.Plan(facts, [], Now);

        // Neither address was copied by the plan; staff typed both.
        var (review, removed) = ReportDispatchPolicy.Reconcile(
            plan,
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
        var plan = ReportDispatchPolicy.Plan(facts, [], Now);

        var (review, removed) = ReportDispatchPolicy.Reconcile(plan, plan, CaseReportDeliveryPolicy.SuggestedReview(plan));

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
        "<instruction@insurer.example>", "conversation-1", sender, cc ?? [], subject);

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
        [], Now);
}

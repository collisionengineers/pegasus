using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

/// <summary>
/// The built rules send as the fixed rules did (operator, 25 November 2026;
/// R1267). Each representative seeded Principal's rules are restated here by
/// hand exactly as migration <c>SendingRulesAreBuilt</c> writes them: the
/// every-Case actions in the order Send to, Send to only, Reply all, Cc, Never
/// cc, one Attach per file that was on, Hold, one task per reminder; each
/// rule in its place, "All" one group and "Any" one group per condition,
/// "Instruction mentions" dropped; Rate card a task after the rule's own;
/// Garage figures a last rule "If Outcome is Repairable". The expected
/// recipients, holds, Stop, tasks and files are what the fixed rules gave for
/// the same facts (their values are in the seed test's SopRules). The intended
/// differences are noted where a Principal had one: "Instruction mentions" is
/// gone (R1268), an "is not" holds on an unset fact (R1269; no seeded
/// Principal has one), and recipients are labelled "On every Case" (R1272).
/// </summary>
public sealed class ReportSendingRulesParityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    private const string Handler = "handler@insurer.example";
    private const string Copy = "copy@insurer.example";

    // The contacts the seeded rules name, by fixed ids.
    private const string Guardian = "00000000-0000-4000-8000-00000000f007";
    private const string James = "00000000-0000-4000-8000-00000000f009";
    private const string Swinton = "00000000-0000-4000-8000-00000000f00a";
    private const string Easdons = "00000000-0000-4000-8000-00000000f00c";
    private const string IRevolution = "00000000-0000-4000-8000-00000000f00d";
    private const string Car2Go = "00000000-0000-4000-8000-0000000000c1";
    private const string Smc = "00000000-0000-4000-8000-0000000000c2";
    private const string CarClaims = "00000000-0000-4000-8000-0000000000c3";
    private const string ExpertClaims = "00000000-0000-4000-8000-0000000000c4";
    private const string RapidRental = "00000000-0000-4000-8000-0000000000c5";

    private const string PMandy = "p.mandy@oakwoodscotland.co.uk";
    private const string IRevolutionRepairs = "repairs@irevolutionrepairnetwork.co.uk";

    [Theory]
    [InlineData("default")]
    [InlineData("ax-met")]
    [InlineData("ax-half")]
    [InlineData("ax-unset")]
    [InlineData("dfd-car2go")]
    [InlineData("dfd-smc")]
    [InlineData("dfd-carclaims")]
    [InlineData("dfd-unset")]
    [InlineData("kbs-expert")]
    [InlineData("kbs-rapid")]
    [InlineData("kbs-unset")]
    [InlineData("pch-repairable")]
    [InlineData("pch-prefix")]
    [InlineData("pch-previous")]
    [InlineData("pch-total-loss")]
    [InlineData("pch-unset")]
    [InlineData("sbl-repairable")]
    [InlineData("sbl-total-loss")]
    [InlineData("yml-swinton")]
    [InlineData("yml-unset")]
    [InlineData("als-repairable")]
    [InlineData("als-total-loss")]
    [InlineData("midas")]
    [InlineData("frz")]
    [InlineData("qcl")]
    public void TheBuiltRulesSendAsTheFixedRulesDid(string row)
    {
        var (facts, expected) = Row(row);

        // The restated rules are a shape Normalize keeps.
        Assert.Equal(facts.Rules, PrincipalReportSendingRules.Normalize(facts.Rules));

        var plan = ReportDispatchPolicy.Plan(facts, Now);

        Assert.Equal(expected.To, plan.To.Select(recipient => recipient.Address).ToArray());
        Assert.Equal(expected.Cc, plan.Cc.Select(recipient => recipient.Address).ToArray());
        Assert.Equal(expected.Excluded, plan.Excluded.Select(removal => removal.Address).ToArray());
        Assert.Equal(expected.Holds, plan.Holds.ToArray());
        Assert.Equal(expected.Stop, plan.Stop);
        Assert.Equal(expected.Tasks, plan.AfterSendTasks.ToArray());
        Assert.Equal(expected.Attach, plan.Attach);
    }

    /// <summary>
    /// PCH's second rule was "Any of: the instruction mentions audit, the
    /// reference prefix a, a previous job". Mentions is gone (R1268), so an
    /// instruction that only says "audit" no longer keeps iRevolution out:
    /// the one intended difference among these Principals. The other two
    /// alternatives still do.
    /// </summary>
    [Fact]
    public void PchsAuditRuleKeepsItsPrefixAndPreviousJobAlternativesOnly()
    {
        var audit = Assert.Single(Pch.Rules, rule => rule.Then.Any(action => action.Kind == ReportSendingActionKind.NeverCc));

        Assert.Equal(
            [ReportSendingFact.RefPrefix, ReportSendingFact.PreviousJob],
            audit.If.Select(group => Assert.Single(group).Fact).ToArray());
    }

    // ---- the rows --------------------------------------------------------------

    private static (ReportDispatchFacts Facts, Expected Expected) Row(string name) => name switch
    {
        "default" => (
            Facts(PrincipalReportSendingRules.Default, outcome: "repairable"),
            new Expected([Handler], [Copy], Attach: Files(feeNote: true))),

        // AX: Repairer Easdons and Claim Source Guardian or James keeps P Mandy out.
        "ax-met" => (
            Facts(Ax, outcome: "repairable", repairer: Easdons, claimSource: Guardian),
            new Expected(AxTo, [Copy], Excluded: [PMandy], Tasks: [ReportDispatchPolicy.GarageFiguresTask])),
        "ax-half" => (
            Facts(Ax, outcome: "repairable", repairer: Easdons),
            new Expected(AxTo, [Copy, PMandy], Tasks: [ReportDispatchPolicy.GarageFiguresTask])),
        "ax-unset" => (
            Facts(Ax, outcome: "total_loss"),
            new Expected(AxTo, [Copy, PMandy])),

        "dfd-car2go" => (
            Facts(Dfd, claimSource: Car2Go),
            new Expected([Handler], [Copy, "richard.pownall@dfd-solicitors.co.uk", "info@car2go-nw.co.uk"], Attach: Files(feeNote: true))),
        "dfd-smc" => (
            Facts(Dfd, claimSource: Smc),
            new Expected([Handler], [Copy, "richard.pownall@dfd-solicitors.co.uk", "tony@shaunmarnell.co.uk"], Attach: Files(feeNote: true))),
        "dfd-carclaims" => (
            Facts(Dfd, claimSource: CarClaims),
            new Expected([Handler], [Copy, "richard.pownall@dfd-solicitors.co.uk"],
                Stop: "CarClaims job. This needs checking with Andy before it is sent.", Attach: Files(feeNote: true))),
        "dfd-unset" => (
            Facts(Dfd),
            new Expected([Handler], [Copy, "richard.pownall@dfd-solicitors.co.uk"], Attach: Files(feeNote: true))),

        "kbs-expert" => (
            Facts(Kbs, claimSource: ExpertClaims),
            new Expected([Handler], [Copy, "info@knightsbridgesolicitors.co.uk", "gabbas@knightsbridgesolicitors.co.uk"], Attach: Files(feeNote: true))),
        "kbs-rapid" => (
            Facts(Kbs, claimSource: RapidRental),
            new Expected([Handler], [Copy, "info@knightsbridgesolicitors.co.uk", "Rapidlongton@gmail.com"], Attach: Files(feeNote: true))),
        "kbs-unset" => (
            Facts(Kbs),
            new Expected([Handler], [Copy, "info@knightsbridgesolicitors.co.uk"], Attach: Files(feeNote: true))),

        // PCH: no Reply all, so the instruction's copies are not kept.
        "pch-repairable" => (
            Facts(Pch, outcome: "repairable", repairer: IRevolution),
            new Expected(["SBL@connexus.co.uk"], [IRevolutionRepairs], Tasks: PchTasks, Attach: Files(figureBreakdown: true))),
        "pch-prefix" => (
            Facts(Pch, outcome: "repairable", repairer: IRevolution, reference: "a.PCH26118"),
            new Expected(["SBL@connexus.co.uk"], [], Excluded: [IRevolutionRepairs], Tasks: PchTasks, Attach: Files(figureBreakdown: true))),
        "pch-previous" => (
            Facts(Pch, outcome: "repairable", repairer: IRevolution, previousJob: true),
            new Expected(["SBL@connexus.co.uk"], [], Excluded: [IRevolutionRepairs], Tasks: PchTasks, Attach: Files(figureBreakdown: true))),
        "pch-total-loss" => (
            Facts(Pch, outcome: "total_loss", repairer: IRevolution),
            new Expected(["SBL@connexus.co.uk"], [], Attach: Files(figureBreakdown: true))),
        "pch-unset" => (
            Facts(Pch, outcome: "repairable"),
            new Expected(["SBL@connexus.co.uk"], [], Attach: Files(figureBreakdown: true))),

        "sbl-repairable" => (
            Facts(Sbl, outcome: "repairable", instructionCc: [Copy, "SBL@connexus.co.uk"]),
            new Expected(["Claims@smartbusinesslink.com"], [Copy], Excluded: ["SBL@connexus.co.uk"],
                Tasks: ["Check with the engineer that the garage has been authorised."])),
        "sbl-total-loss" => (
            Facts(Sbl, outcome: "total_loss"),
            new Expected(["Claims@smartbusinesslink.com"], [Copy], Excluded: ["SBL@connexus.co.uk"])),

        "yml-swinton" => (
            Facts(Yml, claimSource: Swinton),
            new Expected([Handler], [Copy], Tasks: ["send Swinton a copy of the report on WhatsApp."], Attach: Files(feeNote: true))),
        "yml-unset" => (
            Facts(Yml),
            new Expected([Handler], [Copy], Attach: Files(feeNote: true))),

        "als-repairable" => (
            Facts(Als, outcome: "repairable"),
            new Expected([Handler], [Copy, "claims@autologistic.co.uk", PMandy], Tasks: [ReportDispatchPolicy.GarageFiguresTask], Attach: Files(estimate: true))),
        "als-total-loss" => (
            Facts(Als, outcome: "total_loss"),
            new Expected([Handler], [Copy, "claims@autologistic.co.uk", PMandy], Attach: Files(estimate: true))),

        "midas" => (
            Facts(Midas, outcome: "repairable"),
            new Expected([Handler], [Copy], Holds: ["WhatsApp the report to Midas to check before it is sent."])),

        "frz" => (
            Facts(Frz),
            new Expected(["Ontrack789@gmail.com"], [Copy], Tasks: ["WhatsApp Swade to say the report has been sent."], Attach: Files(feeNote: true))),

        _ => (
            Facts(Qcl, instructionCc: [Copy, "credithire@hackneysolutions.co.uk"]),
            new Expected([Handler], [Copy], Excluded: ["credithire@hackneysolutions.co.uk"]))
    };

    private static readonly string[] AxTo = ["engineersreports@ax-uk.com", "engineersinspections@ax-uk.com"];

    private static readonly string[] PchTasks = ["Authorise the garage", ReportDispatchPolicy.RateCardTask];

    // ---- the seeded rules as the migration restates them -----------------------

    private static PrincipalReportSendingRules Ax => new(
        [
            new(ReportSendingActionKind.SendTo, ["engineersreports@ax-uk.com", "engineersinspections@ax-uk.com"]),
            ReplyAll,
            new(ReportSendingActionKind.Cc, [PMandy])
        ],
        [
            new([[Is(ReportSendingFact.Repairer, [Easdons]), Is(ReportSendingFact.ClaimSource, [Guardian, James])]],
                [new(ReportSendingActionKind.NeverCc, [PMandy])]),
            GarageFigures
        ]);

    private static PrincipalReportSendingRules Dfd => new(
        [ReplyAll, new(ReportSendingActionKind.Cc, ["richard.pownall@dfd-solicitors.co.uk"]), FeeNote],
        [
            new([[Is(ReportSendingFact.ClaimSource, [Car2Go])]], [new(ReportSendingActionKind.Cc, ["info@car2go-nw.co.uk"])]),
            new([[Is(ReportSendingFact.ClaimSource, [Smc])]], [new(ReportSendingActionKind.Cc, ["tony@shaunmarnell.co.uk"])]),
            new([[Is(ReportSendingFact.ClaimSource, [CarClaims])]],
                [new(ReportSendingActionKind.Stop, Text: "CarClaims job. This needs checking with Andy before it is sent.")])
        ]);

    private static PrincipalReportSendingRules Kbs => new(
        [ReplyAll, new(ReportSendingActionKind.Cc, ["info@knightsbridgesolicitors.co.uk"]), FeeNote],
        [
            new([[Is(ReportSendingFact.ClaimSource, [ExpertClaims])]], [new(ReportSendingActionKind.Cc, ["gabbas@knightsbridgesolicitors.co.uk"])]),
            new([[Is(ReportSendingFact.ClaimSource, [RapidRental])]], [new(ReportSendingActionKind.Cc, ["Rapidlongton@gmail.com"])])
        ]);

    /// <summary>PCH: its second rule's "Instruction mentions audit" alternative is dropped (R1268).</summary>
    private static PrincipalReportSendingRules Pch => new(
        [
            new(ReportSendingActionKind.SendTo, ["SBL@connexus.co.uk"]),
            new(ReportSendingActionKind.Attach, File: ReportSendingFile.FigureBreakdown)
        ],
        [
            new([[Is(ReportSendingFact.Repairer, [IRevolution]), Is(ReportSendingFact.Outcome, ["repairable"])]],
                [
                    new(ReportSendingActionKind.Cc, [IRevolutionRepairs]),
                    new(ReportSendingActionKind.Task, Text: "Authorise the garage"),
                    new(ReportSendingActionKind.Task, Text: ReportDispatchPolicy.RateCardTask)
                ]),
            new([[Is(ReportSendingFact.RefPrefix, ["a"])], [Is(ReportSendingFact.PreviousJob, [])]],
                [new(ReportSendingActionKind.NeverCc, [IRevolutionRepairs])])
        ]);

    private static PrincipalReportSendingRules Sbl => new(
        [
            new(ReportSendingActionKind.SendTo, ["Claims@smartbusinesslink.com"]),
            ReplyAll,
            new(ReportSendingActionKind.NeverCc, ["SBL@connexus.co.uk"])
        ],
        [
            new([[Is(ReportSendingFact.Outcome, ["repairable"])]],
                [new(ReportSendingActionKind.Task, Text: "Check with the engineer that the garage has been authorised.")])
        ]);

    private static PrincipalReportSendingRules Yml => new(
        [ReplyAll, FeeNote],
        [
            new([[Is(ReportSendingFact.ClaimSource, [Swinton])]],
                [new(ReportSendingActionKind.Task, Text: "send Swinton a copy of the report on WhatsApp.")])
        ]);

    private static PrincipalReportSendingRules Als => new(
        [
            ReplyAll,
            new(ReportSendingActionKind.Cc, ["claims@autologistic.co.uk", PMandy]),
            new(ReportSendingActionKind.Attach, File: ReportSendingFile.Estimate)
        ],
        [GarageFigures]);

    private static PrincipalReportSendingRules Midas => new(
        [ReplyAll, new(ReportSendingActionKind.Hold, Text: "WhatsApp the report to Midas to check before it is sent.")],
        []);

    private static PrincipalReportSendingRules Frz => new(
        [
            new(ReportSendingActionKind.SendTo, ["Ontrack789@gmail.com"]),
            ReplyAll,
            FeeNote,
            new(ReportSendingActionKind.Task, Text: "WhatsApp Swade to say the report has been sent.")
        ],
        []);

    private static PrincipalReportSendingRules Qcl => new(
        [ReplyAll, new(ReportSendingActionKind.NeverCc, ["credithire@hackneysolutions.co.uk"])],
        []);

    private static ReportSendingAction ReplyAll => new(ReportSendingActionKind.ReplyAll);

    private static ReportSendingAction FeeNote => new(ReportSendingActionKind.Attach, File: ReportSendingFile.FeeNoteSeparate);

    /// <summary>Garage figures, the last rule the migration appends.</summary>
    private static ReportSendingRule GarageFigures => new(
        [[Is(ReportSendingFact.Outcome, ["repairable"])]],
        [new(ReportSendingActionKind.Task, Text: ReportDispatchPolicy.GarageFiguresTask)]);

    private static ReportSendingCondition Is(ReportSendingFact fact, IReadOnlyList<string> values) => new(fact, values);

    // ---- facts and expectations ------------------------------------------------

    private sealed record Expected(
        IReadOnlyList<string> To,
        IReadOnlyList<string> Cc,
        IReadOnlyList<string>? Excluded = null,
        IReadOnlyList<string>? Holds = null,
        string? Stop = null,
        IReadOnlyList<string>? Tasks = null,
        ReportSendingAttachments? Attach = null)
    {
        public IReadOnlyList<string> Excluded { get; init; } = Excluded ?? [];
        public IReadOnlyList<string> Holds { get; init; } = Holds ?? [];
        public IReadOnlyList<string> Tasks { get; init; } = Tasks ?? [];
        public ReportSendingAttachments Attach { get; init; } = Attach ?? ReportSendingAttachments.None;
    }

    private static ReportSendingAttachments Files(bool feeNote = false, bool estimate = false, bool figureBreakdown = false) =>
        new(FeeNoteSeparate: feeNote, Estimate: estimate, FigureBreakdown: figureBreakdown);

    private static ReportDispatchFacts Facts(
        PrincipalReportSendingRules rules,
        string? outcome = null,
        string? claimSource = null,
        string? repairer = null,
        string reference = "XYZ26118",
        bool previousJob = false,
        IReadOnlyList<string>? instructionCc = null) => new(
        reference,
        "AB12 CDE",
        "Principal Ltd",
        rules,
        new ReportInstructionMessage(
            Guid.Parse("00000000-0000-4000-8000-0000000000d1"),
            Guid.Parse("00000000-0000-4000-8000-0000000000d2"),
            "engineers@collisionengineers.example",
            "immutable-1",
            null,
            null,
            Handler,
            instructionCc ?? [Copy],
            "Claim AB12 CDE",
            SenderAddress: null),
        claimSource is null ? null : Guid.Parse(claimSource),
        null,
        repairer is null ? null : Guid.Parse(repairer),
        null,
        outcome)
    {
        PreviousJob = previousJob
    };
}

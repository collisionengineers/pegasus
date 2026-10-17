using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Persistence;
using Kind = Pegasus.Core.Reports.ReportSendingConditionKind;
using RuleMatch = Pegasus.Core.Reports.ReportSendingRuleMatch;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Migrations 20261012190000_PrincipalReportSendingRules and
/// 20261017120000_ReportSendingSopV8: the SOP Principals Pegasus did not
/// have, the claim sources rule conditions point at, every SOP Principal's
/// rules and the TL thresholds appended to Notes on every Case (Report
/// Sending SOP v8, 15 October 2026; operator, 6 October 2026 and 15 and 16
/// October 2026). Every Principal has rules.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class PrincipalReportSendingSeedTests
{
    private const string SopYamlSha256 = "83ced76c64f9d8cea7c02babb4f9a643ec7d26a0fae677c50256f2924f07f16c";

    /// <summary>
    /// The rules each Principal Report Sending SOP v8 names has, derived from
    /// docs/principal-profiles/sop-guides/report_sending_sop.v5.yaml (version
    /// 8) by a one-off script. <c>send_to: original_sender</c> is no Send to and
    /// <c>send_from</c> names the mailbox. A <c>source</c> condition is that
    /// Claim Source by name, except ACSP and CS: they are Principals only, so the
    /// instruction is matched on their <c>match</c> text
    /// (20261013090000_ContactSingleType). <c>bodyshop_mentions</c> and
    /// <c>images_from</c> take the source's <c>match</c> text,
    /// <c>report_status</c> is the Outcome, a nested <c>any</c> of one kind is
    /// one condition with alternatives (AX) and <c>rate_card</c> is Rate card.
    /// PCH's notes name its report.
    /// </summary>
    private static Dictionary<string, PrincipalReportSendingRules> SopRules(Func<string, string> claimSource)
    {
        var sop = PrincipalReportSendingRules.Default;
        var attach = ReportSendingAttachments.Default;
        return new()
        {
            ["ACSP"] = sop with { Cc = ["admin@accidentspecialist.co.uk"] },
            ["ALISON"] = sop,
            ["ALS"] = sop with
            {
                Cc = ["claims@autologistic.co.uk", "p.mandy@oakwoodscotland.co.uk"],
                Attach = attach with { FeeNoteSeparate = false, Estimate = true },
                GarageFigures = true
            },
            ["AMS"] = sop,
            ["AX"] = sop with
            {
                SendTo = ["engineersreports@ax-uk.com", "engineersinspections@ax-uk.com"],
                Cc = ["p.mandy@oakwoodscotland.co.uk"],
                Attach = attach with { FeeNoteSeparate = false },
                GarageFigures = true,
                Rules =
                [
                    new(RuleMatch.All,
                        [new(Kind.BodyshopMentions, ["Easdons"]), new(Kind.BodyshopMentions, ["Guardian", "James Claims"])],
                        new([], ["p.mandy@oakwoodscotland.co.uk"]))
                ]
            },
            ["BC"] = sop with { Cc = ["a.nawaz@bakercoleman.co.uk", "h.ahmed@bakercoleman.co.uk"] },
            ["BLACK"] = sop with { Attach = attach with { Estimate = true } },
            ["CS"] = sop with
            {
                SendFromMailbox = "engineers@collisionengineers.co.uk",
                SendTo = ["info@claimsspecialists.co.uk", "accounts@claimsspecialists.co.uk"]
            },
            ["DFD"] = sop with
            {
                Cc = ["richard.pownall@dfd-solicitors.co.uk"],
                Rules =
                [
                    new(RuleMatch.All, [new(Kind.ClaimSource, [claimSource("Car 2 Go")])], new(["info@car2go-nw.co.uk"], [])),
                    new(RuleMatch.All, [new(Kind.ClaimSource, [claimSource("SMC")])], new(["tony@shaunmarnell.co.uk"], [])),
                    new(RuleMatch.All, [new(Kind.ClaimSource, [claimSource("CarClaims")])],
                        new([], [], Stop: "CarClaims job. This needs checking with Andy before it is sent."))
                ]
            },
            ["FRZ"] = sop with
            {
                SendTo = ["Ontrack789@gmail.com"],
                Reminders = ["WhatsApp Swade to say the report has been sent."]
            },
            ["FW"] = sop with { Cc = ["info@fairwaylegal.co.uk"], Attach = attach with { FeeNoteSeparate = false } },
            ["HTU"] = sop,
            ["KBS"] = sop with
            {
                Cc = ["info@knightsbridgesolicitors.co.uk"],
                Rules =
                [
                    new(RuleMatch.All, [new(Kind.ClaimSource, [claimSource("Expert Claims")])],
                        new(["gabbas@knightsbridgesolicitors.co.uk"], [])),
                    new(RuleMatch.All, [new(Kind.ClaimSource, [claimSource("Rapid Rental Solutions")])],
                        new(["Rapidlongton@gmail.com"], []))
                ]
            },
            ["KERR"] = sop,
            ["KMR"] = sop with { Cc = ["info@kmrlaw.co.uk"] },
            ["MIDAS"] = sop with
            {
                Attach = attach with { FeeNoteSeparate = false },
                Hold = "WhatsApp the report to Midas to check before it is sent."
            },
            ["MOTORX"] = sop with { Attach = attach with { FeeNoteSeparate = false } },
            ["MP"] = sop with
            {
                Cc = ["mdakri@montrealprestige.co.uk", "info@montrealprestige.co.uk"],
                Attach = attach with
                {
                    FeeNoteSeparate = false, ReportImages = false, VehicleImagesDocument = true, FigureBreakdown = true
                }
            },
            ["OAK"] = sop with { Cc = ["p.mandy@oakwoodscotland.co.uk"], GarageFigures = true },
            ["PCH"] = sop with
            {
                SendTo = ["SBL@connexus.co.uk"],
                ReplyAll = false,
                Attach = attach with { FeeNoteSeparate = false, FigureBreakdown = true },
                Rules =
                [
                    new(RuleMatch.All,
                        [new(Kind.ClaimSource, [claimSource("iRevolution Repair Network")]), new(Kind.Outcome, ["repairable"])],
                        new(["repairs@irevolutionrepairnetwork.co.uk"], [], Remind: "Authorise the garage", RateCard: true)),
                    new(RuleMatch.Any,
                        [new(Kind.Mentions, ["audit"]), new(Kind.RefPrefix, ["a"]), new(Kind.PreviousJob, [])],
                        new([], ["repairs@irevolutionrepairnetwork.co.uk"]))
                ],
                AttachmentName = new("{reg} Initial", "{reg} Supplementary")
            },
            ["QCL"] = sop with
            {
                NeverCc = ["credithire@hackneysolutions.co.uk"],
                Attach = attach with { FeeNoteSeparate = false }
            },
            ["QDOS"] = sop with
            {
                Cc = ["engineers@qdosassist.co.uk"],
                Attach = attach with { FeeNoteSeparate = false },
                GarageFigures = true
            },
            ["RJS"] = sop with
            {
                Cc = ["engreport@robertjameslaw.co.uk"],
                Rules =
                [
                    new(RuleMatch.Any,
                        [new(Kind.Mentions, ["Luton"]), new(Kind.Mentions, ["Claim Specialist", "Claims Specialist", "claimsspecialists.co.uk"])],
                        new(["accounts@claimsspecialists.co.uk"], [])),
                    new(RuleMatch.All, [new(Kind.Mentions, ["Accident Specialist"])], new(["admin@accidentspecialist.co.uk"], []))
                ]
            },
            ["SBL"] = sop with
            {
                SendTo = ["Claims@smartbusinesslink.com"],
                Cc = ["SBL@connexus.co.uk"],
                Attach = attach with { FeeNoteSeparate = false },
                Rules =
                [
                    new(RuleMatch.All, [new(Kind.Outcome, ["repairable"])],
                        new([], [], Remind: "Check with the engineer that the garage has been authorised."))
                ]
            },
            ["SIX"] = sop with { SendTo = ["info@sixwaysclaims.co.uk"] },
            ["SS"] = sop with { Attach = attach with { FeeNoteSeparate = false } },
            ["SWAN"] = sop,
            ["TEN"] = sop with { Attach = attach with { FeeNoteSeparate = false } },
            ["TP"] = sop with { Cc = ["info@taylorprice.co.uk"] },
            ["WALKER"] = sop,
            ["WLS"] = sop,
            ["YML"] = sop with
            {
                Rules =
                [
                    new(RuleMatch.All, [new(Kind.ImagesFrom, ["Swinton"])],
                        new([], [], Remind: "send Swinton a copy of the report on WhatsApp."))
                ]
            }
        };
    }

    /// <summary>The SOP v8 TL thresholds, by the same script.</summary>
    private static readonly Dictionary<string, string> TlThresholds = new()
    {
        ["ALS"] = "78%",
        ["AX"] = "74%",
        ["BLACK"] = "74%",
        ["OAK"] = "65%",
        ["PCH"] = "66% (always)",
        ["QDOS"] = "78%",
        ["RJS"] = "70%",
        ["SBL"] = "66%"
    };

    private static readonly string[] NewCodes =
    [
        "AMS", "ACSP", "ALISON", "CS", "HTU", "KERR", "KMR", "MIDAS", "MOTORX", "SIX", "SS", "SWADE", "SWAN", "TEN", "TP", "WALKER", "WLS"
    ];

    private static readonly (string Name, string Role)[] SourceOrganizations =
    [
        ("Car 2 Go", "claim_source"), ("SMC", "claim_source"), ("CarClaims", "claim_source"), ("Expert Claims", "claim_source"),
        ("Rapid Rental Solutions", "claim_source"), ("iRevolution Repair Network", "claim_source")
    ];

    private sealed record SeededPrincipal(PrincipalEntity Principal, PrincipalReportSendingRules Rules);

    private static async Task<(Dictionary<string, SeededPrincipal> Principals, Dictionary<Guid, OrganizationEntity> Organizations)> LoadAsync(
        IntakeWebApplicationFactory factory)
    {
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var organizations = await context.Organizations.AsNoTracking()
            .ToDictionaryAsync(item => item.Id);
        var principals = (await context.Principals.AsNoTracking().ToArrayAsync())
            .ToDictionary(
                item => item.Code,
                item => new SeededPrincipal(item, EfOrganizationAdministration.ReadReportSending(item.ReportSendingRulesJson)));
        return (principals, organizations);
    }

    [Fact]
    public async Task EveryPrincipalHasRulesThatNormalizeAndRoundTrip()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var (principals, _) = await LoadAsync(factory);

        foreach (var (code, seeded) in principals)
        {
            Assert.False(string.IsNullOrWhiteSpace(seeded.Principal.ReportSendingRulesJson), code);
            // Stored in exactly the form the serializer writes it.
            Assert.Equal(seeded.Rules, EfOrganizationAdministration.ReadReportSending(
                EfOrganizationAdministration.ToReportSendingJson(seeded.Rules)));
            Assert.Equal(seeded.Rules, PrincipalReportSendingRules.Normalize(seeded.Rules));
        }
    }

    /// <summary>
    /// Every Principal Report Sending SOP v8 names has the rules and TL
    /// threshold the SOP gives it (operator, 15 October 2026). FRZ is created
    /// inactive and its route activates it; SWADE, which v8 drops, keeps its
    /// rules for its existing Cases (operator, 16 October 2026).
    /// </summary>
    [Fact]
    public async Task LiveRulesEqualTheSopYaml()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var (principals, organizations) = await LoadAsync(factory);
        string ClaimSource(string name) =>
            Assert.Single(organizations.Values, item => item.Name == name && item.Role == "claim_source").Id.ToString("D");
        string Notes(string code) => organizations[principals[code].Principal.OrganizationId].NotesOnEveryCase ?? string.Empty;

        foreach (var (code, rules) in SopRules(ClaimSource))
        {
            Assert.True(principals.ContainsKey(code), $"{code} must be seeded.");
            Assert.True(principals[code].Principal.IsActive == (code != "FRZ"), code);
            Assert.Equal(
                $"{code} {EfOrganizationAdministration.ToReportSendingJson(rules)}",
                $"{code} {EfOrganizationAdministration.ToReportSendingJson(principals[code].Rules)}");
        }
        foreach (var (code, threshold) in TlThresholds)
        {
            Assert.Equal(
                [$"TL threshold: {threshold}"],
                Notes(code).Split("\r\n").Where(line => line.StartsWith("TL threshold:", StringComparison.Ordinal)).ToArray());
        }

        var frz = principals["FRZ"].Principal;
        Assert.Equal(Guid.Parse("00000000-0000-4000-8000-00000000c035"), frz.Id);
        Assert.Equal(Guid.Parse("00000000-0000-4000-8000-00000000d035"), frz.OrganizationId);
        Assert.Equal(Guid.Parse("00000000-0000-4000-8000-00000000e035"), frz.SequenceLineageId);
        Assert.Equal("Fraz", organizations[frz.OrganizationId].Name);
        Assert.Equal("00000000-0000-4000-8000-00000000f00b", ClaimSource("iRevolution Repair Network"));
        Assert.Equal("PCH / Connexus", organizations[principals["PCH"].Principal.OrganizationId].Name);
        Assert.Equal(["Ontrack789@gmail.com"], principals["SWADE"].Rules.SendTo.ToArray());
    }

    /// <summary>
    /// The rules and TL threshold tables were derived from this file, so an
    /// edit to the SOP that does not regenerate them fails here. The file is
    /// compared with LF line endings, as committed.
    /// </summary>
    [Fact]
    public void TheSopYamlIsTheOneTheTableWasDerivedFrom()
    {
        var bytes = File.ReadAllBytes(Path.Combine(
            CorpusPackage.RepositoryRoot, "docs", "principal-profiles", "sop-guides", "report_sending_sop.v5.yaml"));

        Assert.Equal(
            SopYamlSha256,
            Convert.ToHexStringLower(SHA256.HashData(bytes.Where(value => value != (byte)'\r').ToArray())));
    }

    [Fact]
    public async Task TheSeededPrincipalsAreWhatANewPrincipalContactSaves()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var (principals, organizations) = await LoadAsync(factory);

        foreach (var code in NewCodes)
        {
            var principal = principals[code].Principal;
            Assert.True(principal.IsActive, code);
            Assert.Equal("physical_address", principal.InspectionMode);
            Assert.Equal(0, principal.Version);
            Assert.Null(principal.PredecessorId);
            Assert.Null(principal.SuccessorId);
            var organization = organizations[principal.OrganizationId];
            Assert.Equal("principal", organization.Role);
            Assert.True(organization.Active, code);
            Assert.Equal(0, organization.Version);
        }
        Assert.Equal(
            NewCodes.Length,
            NewCodes.Select(code => principals[code].Principal.SequenceLineageId).Distinct().Count());
        Assert.Equal(
            "Wentworth Building\r\n1b Fairways Office Park\r\nPittman Way",
            organizations[principals["AMS"].Principal.OrganizationId].Address);
        Assert.Equal("PR2 9LF", organizations[principals["AMS"].Principal.OrganizationId].Postcode);
        Assert.Equal("Swade Solutions Ltd", organizations[principals["SWADE"].Principal.OrganizationId].Name);
        // 20261013090000_ContactSingleType: a contact has one type, so ACSP and CS are Principals only.
        foreach (var code in new[] { "ACSP", "CS" })
        {
            Assert.Equal("principal", organizations[principals[code].Principal.OrganizationId].Role);
        }
    }

    [Fact]
    public async Task TheClaimSourcesRuleConditionsPointAtExistWithTheirRole()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var (principals, organizations) = await LoadAsync(factory);

        foreach (var (name, role) in SourceOrganizations)
        {
            var organization = Assert.Single(organizations.Values, item => item.Name == name);
            Assert.Equal(role, organization.Role);
        }

        // Every contact a condition names is a real organisation with the right role.
        foreach (var seeded in principals.Values)
        {
            foreach (var condition in seeded.Rules.Rules.SelectMany(rule => rule.If))
            {
                var role = condition.Kind switch
                {
                    ReportSendingConditionKind.ClaimSource => "claim_source",
                    ReportSendingConditionKind.Repairer => "repairer",
                    _ => null
                };
                if (role is null) continue;
                Assert.All(condition.Values, id =>
                {
                    Assert.Equal(role, organizations[Guid.Parse(id)].Role);
                });
            }
        }
    }

    [Fact]
    public async Task TheSopRulesCarryTheirConditionsAndActions()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var (principals, organizations) = await LoadAsync(factory);
        string IdOf(string name) => Assert.Single(organizations.Values, item => item.Name == name).Id.ToString("D");
        PrincipalReportSendingRules Rules(string code) => principals[code].Rules;

        // AX: the nested all/any flattens to Match All with two Bodyshop mentions conditions.
        var ax = Assert.Single(Rules("AX").Rules);
        Assert.Equal(ReportSendingRuleMatch.All, ax.Match);
        Assert.Equal(2, ax.If.Count);
        Assert.All(ax.If, condition => Assert.Equal(ReportSendingConditionKind.BodyshopMentions, condition.Kind));
        Assert.Equal(["Easdons"], ax.If[0].Values.ToArray());
        Assert.Equal(["Guardian", "James Claims"], ax.If[1].Values.ToArray());
        Assert.Equal("p.mandy@oakwoodscotland.co.uk", Assert.Single(ax.Then.CcRemove));
        Assert.Equal(2, Rules("AX").SendTo.Count);
        Assert.False(Rules("AX").Attach.FeeNoteSeparate);
        Assert.True(Rules("AX").GarageFigures);

        // DFD: SMC, Car 2 Go and a CarClaims stop.
        Assert.Equal(3, Rules("DFD").Rules.Count);
        Assert.Equal(
            "CarClaims job. This needs checking with Andy before it is sent.",
            Rules("DFD").Rules[2].Then.Stop);
        Assert.Equal([IdOf("CarClaims")], Rules("DFD").Rules[2].If[0].Values.ToArray());

        // CS has no instruction e-mail, so it names its mailbox.
        Assert.Equal("engineers@collisionengineers.co.uk", Rules("CS").SendFromMailbox);
        Assert.Equal(2, Rules("CS").SendTo.Count);

        // MP: the images document and figure breakdown are required companions.
        Assert.False(Rules("MP").Attach.ReportImages);
        Assert.True(Rules("MP").Attach.VehicleImagesDocument);
        Assert.True(Rules("MP").Attach.FigureBreakdown);
        Assert.Equal("WhatsApp the report to Midas to check before it is sent.", Rules("MIDAS").Hold);
        Assert.Equal("credithire@hackneysolutions.co.uk", Assert.Single(Rules("QCL").NeverCc));
        Assert.True(Rules("BLACK").Attach.Estimate);
        Assert.True(Rules("ALS").Attach.Estimate);
    }

    [Fact]
    public async Task TlThresholdsAreAppendedToNotesOnEveryCase()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var (principals, organizations) = await LoadAsync(factory);
        string Notes(string code) => organizations[principals[code].Principal.OrganizationId].NotesOnEveryCase ?? string.Empty;

        Assert.Contains("TL threshold: 78%", Notes("QDOS"), StringComparison.Ordinal);
        Assert.Contains("TL threshold: 78%", Notes("ALS"), StringComparison.Ordinal);
        Assert.Contains("TL threshold: 74%", Notes("AX"), StringComparison.Ordinal);
        Assert.Contains("TL threshold: 74%", Notes("BLACK"), StringComparison.Ordinal);
        Assert.Contains("TL threshold: 70%", Notes("RJS"), StringComparison.Ordinal);
        Assert.Contains("TL threshold: 66%", Notes("SBL"), StringComparison.Ordinal);
        Assert.StartsWith("TL threshold: 66% (always)", Notes("PCH"), StringComparison.Ordinal);
        Assert.Contains("Contract repair at 73%.", Notes("QCL"), StringComparison.Ordinal);
        // Release 103 kept only the TL thresholds and QCL's contract repair line;
        // 20261013091000_ContactSopSeed adds the job sheet, not these v5 notes.
        Assert.DoesNotContain("Engineers (Ben)", Notes("HTU"), StringComparison.Ordinal);
        Assert.DoesNotContain("three PDFs", Notes("MP"), StringComparison.Ordinal);
        Assert.DoesNotContain("Open question", Notes("SWADE"), StringComparison.Ordinal);
        // A Principal the SOP gives nothing to keep has no appended notes.
        Assert.DoesNotContain("TL threshold", Notes("WLS"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheContactSopSeedAddsTheJobSheetPrincipalsTheV5ClaimSourcesAndTheirNotes()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var (principals, organizations) = await LoadAsync(factory);
        string Notes(string code) => organizations[principals[code].Principal.OrganizationId].NotesOnEveryCase ?? string.Empty;

        string[] jobSheetCodes =
        [
            "ABRAHAMS", "ASLS", "ALL", "AS", "AVI", "BAKER", "CASTLE", "CW", "GG", "LEX",
            "LPS", "MATT", "MBH", "RL", "RELAY", "ROZZII", "STALLION", "TA", "WIL", "ZEN"
        ];
        foreach (var code in jobSheetCodes)
        {
            var principal = principals[code].Principal;
            Assert.True(principal.IsActive, code);
            Assert.Equal("physical_address", principal.InspectionMode);
            Assert.Equal(PrincipalReportSendingRules.Default, principals[code].Rules);
            Assert.Equal("principal", organizations[principal.OrganizationId].Role);
            Assert.Contains("Inbox: ", Notes(code), StringComparison.Ordinal);
        }
        Assert.Equal("Graham Coffey", organizations[principals["GG"].Principal.OrganizationId].Name);
        // Address and Postcode from contactseva_combined.csv, as written.
        Assert.Equal(
            "C/O Lex Solicitors\r\n6 St Mark's Street\r\nBolton",
            organizations[principals["LEX"].Principal.OrganizationId].Address);
        Assert.Equal("BL3 6NR", organizations[principals["LEX"].Principal.OrganizationId].Postcode);
        Assert.Equal("Ace Claims & Bodyworks Ltd", organizations[principals["STALLION"].Principal.OrganizationId].Address);
        Assert.Null(organizations[principals["STALLION"].Principal.OrganizationId].Postcode);
        foreach (var name in new[] { "Easdons", "Guardian Accident Claims", "Hackney Solutions", "James Claims", "Swinton" })
        {
            Assert.Equal("claim_source", Assert.Single(organizations.Values, item => item.Name == name).Role);
        }

        // Appended after what a record already held, one block per job-sheet row.
        Assert.StartsWith("TL threshold: 70%", Notes("RJS"), StringComparison.Ordinal);
        Assert.Contains("Solicitors Instructions: Claim Specialists", Notes("RJS"), StringComparison.Ordinal);
        Assert.Contains("Solicitors Instructions: Accident Specialist", Notes("RJS"), StringComparison.Ordinal);
        Assert.Contains("Copy in mdakri@montrealprestige.co.uk", Notes("MP"), StringComparison.Ordinal);
        // The v5 notes and received-via the sheet lacks.
        Assert.Contains("\"[reg] Initial\" or \"[reg] Supplementary\"", Notes("PCH"), StringComparison.Ordinal);
        Assert.Equal("Inbox: Engineers", Notes("SWADE"));
    }
}

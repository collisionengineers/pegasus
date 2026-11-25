using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Persistence.Migrations;
using Kind = Pegasus.Core.Reports.ReportSendingConditionKind;
using RuleMatch = Pegasus.Core.Reports.ReportSendingRuleMatch;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Report Sending SOP seed (12 and 17 October 2026): the SOP Principals
/// Pegasus did not have, the claim sources rule conditions point at, every SOP Principal's
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
    /// (a contact has one type since 13 October 2026).
    /// <c>report_status</c> is the Outcome and <c>rate_card</c> is Rate card.
    /// PCH's notes name its report. Two rules no longer follow the YAML: its
    /// <c>bodyshop_mentions</c> (AX) and <c>images_from</c> (YML) conditions
    /// had no Case fact and were only ever asked of staff, so the operator
    /// restated them on the Case's Repairer and Claim Source (12 November
    /// 2026; R1023, R1025, R1026). Two more follow the Report Sending Quick
    /// Reference of 29 October 2026 instead: SBL never copies
    /// SBL@connexus.co.uk, and PCH's iRevolution rule reads the Case's
    /// Repairer (13 November 2026; R1031, R1033). The YAML stays as supplied
    /// evidence (R3).
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
                        [
                            new(Kind.Repairer, [SendingRulesReadCaseFacts.EasdonsBodyshopId]),
                            new(Kind.ClaimSource, [claimSource("Guardian Accident Claims"), claimSource("James Claims")])
                        ],
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
                        [new(Kind.Repairer, [ContactsFollowQuickReference.IRevolutionRepairerId]), new(Kind.Outcome, ["repairable"])],
                        new(["repairs@irevolutionrepairnetwork.co.uk"], [], Remind: "Authorise the garage", RateCard: true)),
                    new(RuleMatch.Any,
                        [new(Kind.Mentions, ["audit"]), new(Kind.RefPrefix, ["a"]), new(Kind.PreviousJob, [])],
                        new([], ["repairs@irevolutionrepairnetwork.co.uk"]))
                ]
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
                NeverCc = ["SBL@connexus.co.uk"],
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
                    new(RuleMatch.All, [new(Kind.ClaimSource, [claimSource("Swinton")])],
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
        ("Rapid Rental Solutions", "claim_source"), ("iRevolution Repair Network", "claim_source"),
        ("iRevolution Repair Network", "repairer")
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
    /// threshold the SOP gives it (operator, 15 October 2026). FRZ was created
    /// inactive for its route to activate; it is active without one (operator,
    /// 13 November 2026; R1036). SWADE, which v8 drops, keeps its rules for
    /// its existing Cases (operator, 16 October 2026).
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
            Assert.True(principals[code].Principal.IsActive, code);
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
        Assert.True(organizations[frz.OrganizationId].Active);
        Assert.Equal("00000000-0000-4000-8000-00000000f00b", ClaimSource("iRevolution Repair Network"));
        Assert.Equal("PCH / Connexus", organizations[principals["PCH"].Principal.OrganizationId].Name);
        Assert.Equal(["Ontrack789@gmail.com"], principals["SWADE"].Rules.SendTo.ToArray());

        // PCH's seeded attachment name became its Inspection report names, in
        // the new tokens (R740); its Audit report keeps the standard name
        // (R755). No other Principal names any.
        Assert.Equal(
            new PrincipalReportNames("{REG} Initial", "{REG} Supplementary", null, null),
            EfOrganizationAdministration.ReadReportNames(principals["PCH"].Principal));
        Assert.All(
            principals.Where(pair => pair.Key != "PCH"),
            pair => Assert.Equal(PrincipalReportNames.None, EfOrganizationAdministration.ReadReportNames(pair.Value.Principal)));
    }

    /// <summary>
    /// The restatement of the retired kinds guesses at nothing (R1023): met a
    /// second time it changes no rule, and a rule that names a retired kind
    /// in any shape it does not know stops it, so that rule is restated by
    /// hand and never dropped.
    /// </summary>
    [Fact]
    public async Task TheRestatementChangesNothingTwiceAndRefusesARetiredKindItCannotRestate()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
        // ExecuteSqlRaw reads braces as placeholders, and the rules are JSON.
        var restate = SendingRulesReadCaseFacts.RestateSql
            .Replace("{", "{{", StringComparison.Ordinal)
            .Replace("}", "}}", StringComparison.Ordinal);
        Task<string[]> StoredAsync() => context.Principals.AsNoTracking()
            .OrderBy(principal => principal.Code)
            .Select(principal => principal.ReportSendingRulesJson)
            .ToArrayAsync();
        var migrated = await StoredAsync();

        await context.Database.ExecuteSqlRawAsync(restate);

        Assert.Equal(migrated, await StoredAsync());

        var unknown = EfOrganizationAdministration.DefaultReportSendingJson.Replace(
            "\"rules\":[]",
            "\"rules\":[{\"match\":\"All\",\"if\":[{\"kind\":\"ImagesFrom\",\"values\":[\"Garage\"]}],\"then\":{\"ccAdd\":[\"garage@principal.example\"],\"ccRemove\":[]}}]",
            StringComparison.Ordinal);
        Assert.Contains("ImagesFrom", unknown, StringComparison.Ordinal);
        await context.Principals
            .Where(principal => principal.Code == "WLS")
            .ExecuteUpdateAsync(setters => setters.SetProperty(principal => principal.ReportSendingRulesJson, unknown));

        var refused = await Assert.ThrowsAnyAsync<DbException>(() => context.Database.ExecuteSqlRawAsync(restate));

        Assert.Contains("restate that rule on the Principal contact", refused.Message, StringComparison.Ordinal);
        Assert.Equal(
            unknown,
            await context.Principals.AsNoTracking()
                .Where(principal => principal.Code == "WLS")
                .Select(principal => principal.ReportSendingRulesJson)
                .SingleAsync());
    }

    /// <summary>
    /// The Contact records follow the Report Sending Quick Reference of 29
    /// October 2026 (operator, 13 November 2026; R1030 to R1036). A fresh
    /// database is left with the introducers Oakwood's instructions name as
    /// Claim Sources linked to OAK and the iRevolution Repairer linked to
    /// PCH. Met again over records it did not seed, the restatement retires
    /// the placeholder Claim Source, takes a passage out only where it ends
    /// its block, restores nothing staff reworded, and changes nothing the
    /// second time.
    /// </summary>
    [Fact]
    public async Task TheQuickReferenceRestatementFollowsTheRulingsAndLeavesHandEditsAlone()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
        // ExecuteSqlRaw reads braces as placeholders, and the rules are JSON.
        var restate = ContactsFollowQuickReference.RestateSql
            .Replace("{", "{{", StringComparison.Ordinal)
            .Replace("}", "}}", StringComparison.Ordinal);
        async Task<string[]> LinkedAsync(string code, string role) => [.. (await context.Organizations.AsNoTracking()
            .Where(item => item.Role == role && item.Active && item.PrincipalLinks.Any(link => link.Principal.Code == code))
            .Select(item => item.Name)
            .ToArrayAsync()).Order(StringComparer.Ordinal)];
        Task<string?> NotesAsync(string code) => context.Organizations.AsNoTracking()
            .Where(item => item.Principals.Any(principal => principal.Code == code))
            .Select(item => item.NotesOnEveryCase)
            .SingleAsync();
        Task<int> SetNotesAsync(string code, string notes) => context.Organizations
            .Where(item => item.Principals.Any(principal => principal.Code == code))
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.NotesOnEveryCase, notes));
        async Task<string> StoredAsync() => JsonSerializer.Serialize(new
        {
            Organizations = await context.Organizations.AsNoTracking().OrderBy(item => item.Id)
                .Select(item => new { item.Id, item.Name, item.Role, item.Active, item.NotesOnEveryCase }).ToArrayAsync(),
            Principals = await context.Principals.AsNoTracking().OrderBy(item => item.Code)
                .Select(item => new { item.Code, item.IsActive, item.ReportSendingRulesJson }).ToArrayAsync(),
            Links = await context.Set<ContactPrincipalLinkEntity>().AsNoTracking()
                .OrderBy(item => item.PrincipalId).ThenBy(item => item.OrganizationId)
                .Select(item => new { item.PrincipalId, item.OrganizationId }).ToArrayAsync()
        });

        Assert.Equal(
            ContactsFollowQuickReference.OakIntroducers.Select(item => item.Name).Order(StringComparer.Ordinal).ToArray(),
            await LinkedAsync("OAK", "claim_source"));
        Assert.Equal(["iRevolution Repair Network"], await LinkedAsync("PCH", "repairer"));

        // The records production holds and a fresh database does not.
        const string placeholder = "Paul Mandy / instructed garages";
        context.Organizations.Add(new OrganizationEntity { Id = Guid.NewGuid(), Name = placeholder, Role = "claim_source" });
        await context.SaveChangesAsync();
        const string reworded =
            "Inbox: Engineers\r\nSending Report: Fee note separate - copy in engreport@robertjameslaw.co.uk and someone@principal.example";
        await SetNotesAsync("RJS", reworded);
        await SetNotesAsync(
            "TEN",
            "Inbox: Engineers\r\nSending Report: Fee note separate return to file handler\r\n\r\nInbox: Andrew");
        await SetNotesAsync("QCL", "Contract repair at 75%.\r\n\r\nInbox: Engineers");
        var seededSbl = await context.Principals.AsNoTracking()
            .Where(principal => principal.Code == "SBL")
            .Select(principal => principal.ReportSendingRulesJson)
            .SingleAsync();
        var copied = seededSbl.Replace(
            "\"cc\":[],\"neverCc\":[\"SBL@connexus.co.uk\"]",
            "\"cc\":[\"SBL@connexus.co.uk\"],\"neverCc\":[]",
            StringComparison.Ordinal);
        Assert.NotEqual(seededSbl, copied);
        await context.Principals
            .Where(principal => principal.Code == "SBL")
            .ExecuteUpdateAsync(setters => setters.SetProperty(principal => principal.ReportSendingRulesJson, copied));
        await context.Principals
            .Where(principal => principal.Code == "FRZ")
            .ExecuteUpdateAsync(setters => setters.SetProperty(principal => principal.IsActive, false));

        await context.Database.ExecuteSqlRawAsync(restate);

        Assert.False(await context.Organizations.AsNoTracking()
            .Where(item => item.Name == placeholder)
            .Select(item => item.Active)
            .SingleAsync());
        Assert.Equal(reworded, await NotesAsync("RJS"));
        Assert.Equal("Inbox: Engineers\r\n\r\nInbox: Andrew", await NotesAsync("TEN"));
        Assert.Equal("Contract repair at 75%.\r\n\r\nInbox: Engineers", await NotesAsync("QCL"));
        Assert.Equal(seededSbl, await context.Principals.AsNoTracking()
            .Where(principal => principal.Code == "SBL")
            .Select(principal => principal.ReportSendingRulesJson)
            .SingleAsync());
        Assert.True(await context.Principals.AsNoTracking()
            .Where(principal => principal.Code == "FRZ")
            .Select(principal => principal.IsActive)
            .SingleAsync());

        var restated = await StoredAsync();
        await context.Database.ExecuteSqlRawAsync(restate);

        Assert.Equal(restated, await StoredAsync());
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
        // A contact has one type (13 October 2026), so ACSP and CS are Principals only.
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
            Assert.Single(organizations.Values, item => item.Name == name && item.Role == role);
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

        // AX: Repairer is Easdons Bodyshop and Claim Source is Guardian
        // Accident Claims or James Claims (R1025).
        var ax = Assert.Single(Rules("AX").Rules);
        Assert.Equal(ReportSendingRuleMatch.All, ax.Match);
        Assert.Equal(
            [ReportSendingConditionKind.Repairer, ReportSendingConditionKind.ClaimSource],
            ax.If.Select(condition => condition.Kind).ToArray());
        Assert.Equal([IdOf("Easdons Bodyshop")], ax.If[0].Values.ToArray());
        Assert.Equal([IdOf("Guardian Accident Claims"), IdOf("James Claims")], ax.If[1].Values.ToArray());
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

        // YML: Claim Source is Swinton (R1026).
        var yml = Assert.Single(Rules("YML").Rules);
        Assert.Equal(ReportSendingConditionKind.ClaimSource, Assert.Single(yml.If).Kind);
        Assert.Equal([IdOf("Swinton")], yml.If[0].Values.ToArray());
        Assert.Equal("send Swinton a copy of the report on WhatsApp.", yml.Then.Remind);

        // CS has no instruction e-mail, so it names its mailbox.
        Assert.Equal("engineers@collisionengineers.co.uk", Rules("CS").SendFromMailbox);
        Assert.Equal(2, Rules("CS").SendTo.Count);

        // MP: the images document and figure breakdown are required companions.
        Assert.False(Rules("MP").Attach.ReportImages);
        Assert.True(Rules("MP").Attach.VehicleImagesDocument);
        Assert.True(Rules("MP").Attach.FigureBreakdown);
        Assert.Equal("WhatsApp the report to Midas to check before it is sent.", Rules("MIDAS").Hold);
        Assert.Equal("credithire@hackneysolutions.co.uk", Assert.Single(Rules("QCL").NeverCc));

        // PCH: Repairer is iRevolution Repair Network and the outcome is
        // repairable (R1033). SBL never copies its Connexus address (R1031).
        var pch = Rules("PCH").Rules[0];
        Assert.Equal(
            [ReportSendingConditionKind.Repairer, ReportSendingConditionKind.Outcome],
            pch.If.Select(condition => condition.Kind).ToArray());
        Assert.Equal([ContactsFollowQuickReference.IRevolutionRepairerId], pch.If[0].Values.ToArray());
        Assert.Equal("repairs@irevolutionrepairnetwork.co.uk", Assert.Single(pch.Then.CcAdd));
        Assert.Empty(Rules("SBL").Cc);
        Assert.Equal("SBL@connexus.co.uk", Assert.Single(Rules("SBL").NeverCc));
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
        // QCL's contract repair line is the quick reference's QCL note (R1035).
        Assert.StartsWith(ContactsFollowQuickReference.QclNote + "\r\n", Notes("QCL"), StringComparison.Ordinal);
        // Release 103 kept only the TL thresholds and QCL's contract repair line;
        // the contact SOP seed of 13 October 2026 adds the job sheet, not these v5 notes.
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
        // The Report sending tab says how a report goes, so the job sheet's
        // "Sending Report:" passages are gone from the Principals the quick
        // reference names (R1034) and stay on the others.
        Assert.EndsWith("Image based or address: Inspection address listed in report", Notes("MP"), StringComparison.Ordinal);
        Assert.All(
            ContactsFollowQuickReference.SendingPassages.Select(item => item.Code).Distinct(),
            code => Assert.DoesNotContain("Sending Report:", Notes(code), StringComparison.Ordinal));
        Assert.Contains("Sending Report: ", Notes("LEX"), StringComparison.Ordinal);
        // The v5 notes and received-via the sheet lacks.
        Assert.Contains("\"[reg] Initial\" or \"[reg] Supplementary\"", Notes("PCH"), StringComparison.Ordinal);
        Assert.Equal("Inbox: Engineers", Notes("SWADE"));
    }

    /// <summary>
    /// The standing Private customer record (operator, 31 October 2026;
    /// R574): one active Principal, code PRIVATE, referenced by registration,
    /// with the standard default fee and the default sending rules, as a new
    /// Principal Contact saves; every other Principal keeps its sequence.
    /// </summary>
    [Fact]
    public async Task TheStandingPrivateCustomerRecordIsReferencedByRegistration()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var (principals, organizations) = await LoadAsync(factory);

        var principal = principals["PRIVATE"].Principal;
        Assert.Equal(Guid.Parse("00000000-0000-4000-8000-00000000C036"), principal.Id);
        Assert.Equal("registration", principal.ReferenceScheme);
        Assert.True(principal.IsActive);
        Assert.Equal("physical_address", principal.InspectionMode);
        Assert.Equal(180.00m, principal.DefaultFee);
        Assert.Null(principal.PredecessorId);
        Assert.Equal(PrincipalReportSendingRules.Default, principals["PRIVATE"].Rules);
        var organization = organizations[principal.OrganizationId];
        Assert.Equal("Private customer", organization.Name);
        Assert.Equal("principal", organization.Role);
        Assert.True(organization.Active);
        Assert.Null(organization.Address);
        Assert.Equal(
            ["PRIVATE"],
            principals.Values
                .Where(item => item.Principal.ReferenceScheme == "registration")
                .Select(item => item.Principal.Code));
    }

    /// <summary>
    /// United Legal Assistance (operator, 17 November 2026; R1145, R1152):
    /// one active Principal, code ULA, always Image Based Assessment, on the
    /// Garage authorisation and invoice Audit workflow with the ULA Invoice,
    /// the standard default fee and report names, the default sending rules
    /// with both Offer ticks on (R1151), and no Invoice VAT number, payment
    /// terms or notes. No other Principal
    /// is on that workflow or has an invoice of its own, and the seed met
    /// again changes nothing.
    /// </summary>
    [Fact]
    public async Task UnitedLegalAssistanceIsSeededOnTheGarageAuthorisationWorkflow()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var (principals, organizations) = await LoadAsync(factory);

        var principal = principals["ULA"].Principal;
        Assert.Equal(Guid.Parse(AuditWorkflowAndUnitedLegalAssistance.PrincipalId), principal.Id);
        Assert.True(principal.IsActive);
        Assert.Equal("principal_sequence", principal.ReferenceScheme);
        Assert.Equal("image_based_assessment", principal.InspectionMode);
        Assert.Equal("garage_authorisation_and_invoice", principal.AuditWorkflow);
        Assert.Equal("ula_invoice", principal.PrincipalInvoice);
        Assert.Null(principal.InvoiceVatNumber);
        Assert.Null(principal.InvoicePaymentTerms);
        Assert.Equal(180.00m, principal.DefaultFee);
        Assert.Null(principal.InspectionReportNameFirst);
        Assert.Null(principal.AuditReportNameFirst);
        var offering = PrincipalReportSendingRules.Default with
        {
            Attach = ReportSendingAttachments.Default with { OfferExternalEstimates = true, OfferRepairInvoices = true }
        };
        Assert.Equal(offering, principals["ULA"].Rules);
        // Stored as the serializer writes it, the two Offer ticks after the rest.
        Assert.Equal(EfOrganizationAdministration.ToReportSendingJson(offering), principal.ReportSendingRulesJson);
        var organization = organizations[principal.OrganizationId];
        Assert.Equal("United Legal Assistance Limited", organization.Name);
        Assert.Equal("principal", organization.Role);
        Assert.True(organization.Active);
        Assert.Equal("130c Lord Street\r\nSouthport", organization.Address);
        Assert.Equal("PR9 0AE", organization.Postcode);
        Assert.Equal("0330 999 9000", organization.Telephone);
        Assert.Equal("claims@united-legal.co.uk", organization.Email);
        Assert.Null(organization.NotesOnEveryCase);
        Assert.Equal(
            ["ULA"],
            principals.Values
                .Where(item => item.Principal.AuditWorkflow != "original_engineer_report"
                    || item.Principal.PrincipalInvoice != "none")
                .Select(item => item.Principal.Code));

        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        // ExecuteSqlRaw reads braces as placeholders, and the rules are JSON.
        await context.Database.ExecuteSqlRawAsync(AuditWorkflowAndUnitedLegalAssistance.SeedSql
            .Replace("{", "{{", StringComparison.Ordinal)
            .Replace("}", "}}", StringComparison.Ordinal));
        Assert.Equal(principals.Count, await context.Principals.CountAsync());
        Assert.Equal(organizations.Count, await context.Organizations.CountAsync());
    }

    /// <summary>
    /// Parkhouse (operator, 18 November 2026; R1191, R1192, R1194, R1195):
    /// one active Principal, code PHOUSE, with a physical inspection address,
    /// the standard default fee and the default sending rules, and the
    /// Repairer "Parkhouse Bodyshop" linked to it with the address Parkhouse
    /// Assist's letter prints. The seed met again changes nothing.
    /// </summary>
    [Fact]
    public async Task ParkhouseAndItsRepairerAreSeeded()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var (principals, organizations) = await LoadAsync(factory);

        var principal = principals["PHOUSE"].Principal;
        Assert.Equal(Guid.Parse(ParkhousePrincipalAndRepairer.PrincipalId), principal.Id);
        Assert.True(principal.IsActive);
        Assert.Equal("principal_sequence", principal.ReferenceScheme);
        Assert.Equal("physical_address", principal.InspectionMode);
        Assert.Equal("original_engineer_report", principal.AuditWorkflow);
        Assert.Equal("none", principal.PrincipalInvoice);
        Assert.Equal(180.00m, principal.DefaultFee);
        Assert.Equal(PrincipalReportSendingRules.Default, principals["PHOUSE"].Rules);
        var organization = organizations[principal.OrganizationId];
        Assert.Equal("Parkhouse", organization.Name);
        Assert.Equal("principal", organization.Role);
        Assert.True(organization.Active);
        Assert.Equal("C/O Parkhouse Assist Ltd, Brulimar House, Jubilee Road, Middleton", organization.Address);
        Assert.Equal("M24 2LX", organization.Postcode);
        Assert.Equal("0161 524 0000", organization.Telephone);
        Assert.Equal("office@parkhouseassist.com", organization.Email);
        Assert.Null(organization.NotesOnEveryCase);

        var repairer = organizations[Guid.Parse(ParkhousePrincipalAndRepairer.RepairerId)];
        Assert.Equal("Parkhouse Bodyshop", repairer.Name);
        Assert.Equal("repairer", repairer.Role);
        Assert.True(repairer.Active);
        Assert.Equal(
            "Unit 7, Park House Bridge Estate, 7 Langley Rd, Manchester, Pendlebury, Salford",
            repairer.Address);
        Assert.Equal("M6 6JQ", repairer.Postcode);
        Assert.Null(repairer.Email);
        Assert.Null(repairer.Telephone);

        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var links = context.Set<ContactPrincipalLinkEntity>().AsNoTracking();
        Assert.Equal(principal.Id, (await links.SingleAsync(link => link.OrganizationId == repairer.Id)).PrincipalId);
        var linkCount = await links.CountAsync();

        // ExecuteSqlRaw reads braces as placeholders, and the rules are JSON.
        await context.Database.ExecuteSqlRawAsync(ParkhousePrincipalAndRepairer.SeedSql
            .Replace("{", "{{", StringComparison.Ordinal)
            .Replace("}", "}}", StringComparison.Ordinal));
        Assert.Equal(principals.Count, await context.Principals.CountAsync());
        Assert.Equal(organizations.Count, await context.Organizations.CountAsync());
        Assert.Equal(linkCount, await links.CountAsync());
    }
}

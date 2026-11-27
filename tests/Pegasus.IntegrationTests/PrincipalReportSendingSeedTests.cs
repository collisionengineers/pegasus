using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Persistence.Migrations;
using Act = Pegasus.Core.Reports.ReportSendingActionKind;
using SendingFact = Pegasus.Core.Reports.ReportSendingFact;
using SendingFile = Pegasus.Core.Reports.ReportSendingFile;

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

    private static readonly ReportSendingAction ReplyAll = new(Act.ReplyAll);
    private static readonly ReportSendingAction FeeNoteSeparate = Attach(SendingFile.FeeNoteSeparate);

    /// <summary>The rule Garage figures became, last of a Principal's rules (R1270).</summary>
    private static readonly ReportSendingRule GarageFigures = new(
        [[new(SendingFact.Outcome, ["repairable"])]],
        [AddTask(ReportDispatchPolicy.GarageFiguresTask)]);

    private static ReportSendingAction SendTo(IReadOnlyList<string> addresses) => new(Act.SendTo, addresses);
    private static ReportSendingAction Cc(IReadOnlyList<string> addresses) => new(Act.Cc, addresses);
    private static ReportSendingAction NeverCc(IReadOnlyList<string> addresses) => new(Act.NeverCc, addresses);
    private static ReportSendingAction Attach(SendingFile file) => new(Act.Attach, File: file);
    private static ReportSendingAction AddTask(string text) => new(Act.Task, Text: text);

    private static PrincipalReportSendingRules EveryCase(IReadOnlyList<ReportSendingAction> actions) => new(actions, []);

    /// <summary>
    /// The rules each Principal Report Sending SOP v8 names has, derived from
    /// docs/principal-profiles/sop-guides/report_sending_sop.v5.yaml (version
    /// 8) by a one-off script and built as rules by the migration
    /// SendingRulesAreBuilt (R1267 to R1270). <c>send_to: original_sender</c>
    /// is no Send to and <c>send_from</c> is the Principal's Send from, a
    /// column of its own. A <c>source</c> condition is that Claim Source by
    /// name, except ACSP and CS: they are Principals only.
    /// <c>report_status</c> is the Outcome and <c>rate_card</c> the Rate card
    /// task. PCH's notes name its report. Two rules no longer follow the YAML:
    /// its <c>bodyshop_mentions</c> (AX) and <c>images_from</c> (YML)
    /// conditions had no Case fact and were only ever asked of staff, so the
    /// operator restated them on the Case's Repairer and Claim Source (12
    /// November 2026; R1023, R1025, R1026). Two more follow the Report Sending
    /// Quick Reference of 29 October 2026 instead: SBL never copies
    /// SBL@connexus.co.uk, and PCH's iRevolution rule reads the Case's
    /// Repairer (13 November 2026; R1031, R1033). "Instruction mentions" is
    /// retired (R1268): PCH's audit rule keeps its other two conditions, and
    /// RJS's two rules read the Claim Sources Claim Specialists and Accident
    /// Specialists where a database has them, and are gone where it has not
    /// (R1265). The YAML stays as supplied evidence (R3).
    /// </summary>
    private static Dictionary<string, PrincipalReportSendingRules> SopRules(
        Func<string, string> claimSource, Func<string, string?> claimSourceIfAny)
    {
        var sop = PrincipalReportSendingRules.Default;
        var rjsRules = new List<ReportSendingRule>();
        if (claimSourceIfAny("Claim Specialists") is { } claimSpecialists)
        {
            rjsRules.Add(new([[new(SendingFact.ClaimSource, [claimSpecialists])]], [Cc(["accounts@claimsspecialists.co.uk"])]));
        }
        if (claimSourceIfAny("Accident Specialists") is { } accidentSpecialists)
        {
            rjsRules.Add(new([[new(SendingFact.ClaimSource, [accidentSpecialists])]], [Cc(["admin@accidentspecialist.co.uk"])]));
        }
        return new()
        {
            ["ACSP"] = EveryCase([ReplyAll, Cc(["admin@accidentspecialist.co.uk"]), FeeNoteSeparate]),
            ["ALISON"] = sop,
            ["ALS"] = new(
                [ReplyAll, Cc(["claims@autologistic.co.uk", "p.mandy@oakwoodscotland.co.uk"]), Attach(SendingFile.Estimate)],
                [GarageFigures]),
            ["AMS"] = sop,
            ["AX"] = new(
                [
                    SendTo(["engineersreports@ax-uk.com", "engineersinspections@ax-uk.com"]),
                    ReplyAll,
                    Cc(["p.mandy@oakwoodscotland.co.uk"])
                ],
                [
                    new(
                        [[
                            new(SendingFact.Repairer, [SendingRulesReadCaseFacts.EasdonsBodyshopId]),
                            new(SendingFact.ClaimSource, [claimSource("Guardian Accident Claims"), claimSource("James Claims")])
                        ]],
                        [NeverCc(["p.mandy@oakwoodscotland.co.uk"])]),
                    GarageFigures
                ]),
            ["BC"] = EveryCase([ReplyAll, Cc(["a.nawaz@bakercoleman.co.uk", "h.ahmed@bakercoleman.co.uk"]), FeeNoteSeparate]),
            ["BLACK"] = EveryCase([ReplyAll, FeeNoteSeparate, Attach(SendingFile.Estimate)]),
            ["CS"] = EveryCase([SendTo(["info@claimsspecialists.co.uk", "accounts@claimsspecialists.co.uk"]), ReplyAll, FeeNoteSeparate]),
            ["DFD"] = new(
                [ReplyAll, Cc(["richard.pownall@dfd-solicitors.co.uk"]), FeeNoteSeparate],
                [
                    new([[new(SendingFact.ClaimSource, [claimSource("Car 2 Go")])]], [Cc(["info@car2go-nw.co.uk"])]),
                    new([[new(SendingFact.ClaimSource, [claimSource("SMC")])]], [Cc(["tony@shaunmarnell.co.uk"])]),
                    new([[new(SendingFact.ClaimSource, [claimSource("CarClaims")])]],
                        [new(Act.Stop, Text: "CarClaims job. This needs checking with Andy before it is sent.")])
                ]),
            ["FRZ"] = EveryCase(
                [SendTo(["Ontrack789@gmail.com"]), ReplyAll, FeeNoteSeparate, AddTask("WhatsApp Swade to say the report has been sent.")]),
            ["FW"] = EveryCase([ReplyAll, Cc(["info@fairwaylegal.co.uk"])]),
            ["HTU"] = sop,
            ["KBS"] = new(
                [ReplyAll, Cc(["info@knightsbridgesolicitors.co.uk"]), FeeNoteSeparate],
                [
                    new([[new(SendingFact.ClaimSource, [claimSource("Expert Claims")])]], [Cc(["gabbas@knightsbridgesolicitors.co.uk"])]),
                    new([[new(SendingFact.ClaimSource, [claimSource("Rapid Rental Solutions")])]], [Cc(["Rapidlongton@gmail.com"])])
                ]),
            ["KERR"] = sop,
            ["KMR"] = EveryCase([ReplyAll, Cc(["info@kmrlaw.co.uk"]), FeeNoteSeparate]),
            ["MIDAS"] = EveryCase([ReplyAll, new(Act.Hold, Text: "WhatsApp the report to Midas to check before it is sent.")]),
            ["MOTORX"] = EveryCase([ReplyAll]),
            ["MP"] = EveryCase(
                [
                    ReplyAll,
                    Cc(["mdakri@montrealprestige.co.uk", "info@montrealprestige.co.uk"]),
                    Attach(SendingFile.VehicleImagesDocument),
                    Attach(SendingFile.FigureBreakdown)
                ]),
            ["OAK"] = new([ReplyAll, Cc(["p.mandy@oakwoodscotland.co.uk"]), FeeNoteSeparate], [GarageFigures]),
            ["PCH"] = new(
                [SendTo(["SBL@connexus.co.uk"]), Attach(SendingFile.FigureBreakdown)],
                [
                    new(
                        [[new(SendingFact.Repairer, [ContactsFollowQuickReference.IRevolutionRepairerId]), new(SendingFact.Outcome, ["repairable"])]],
                        [
                            Cc(["repairs@irevolutionrepairnetwork.co.uk"]),
                            AddTask("Authorise the garage"),
                            AddTask(ReportDispatchPolicy.RateCardTask)
                        ]),
                    new(
                        [[new(SendingFact.RefPrefix, ["a"])], [new(SendingFact.PreviousJob, [])]],
                        [NeverCc(["repairs@irevolutionrepairnetwork.co.uk"])])
                ]),
            ["QCL"] = EveryCase([ReplyAll, NeverCc(["credithire@hackneysolutions.co.uk"])]),
            ["QDOS"] = new([ReplyAll, Cc(["engineers@qdosassist.co.uk"])], [GarageFigures]),
            ["RJS"] = new([ReplyAll, Cc(["engreport@robertjameslaw.co.uk"]), FeeNoteSeparate], rjsRules),
            ["SBL"] = new(
                [SendTo(["Claims@smartbusinesslink.com"]), ReplyAll, NeverCc(["SBL@connexus.co.uk"])],
                [new([[new(SendingFact.Outcome, ["repairable"])]], [AddTask("Check with the engineer that the garage has been authorised.")])]),
            ["SIX"] = EveryCase([SendTo(["info@sixwaysclaims.co.uk"]), ReplyAll, FeeNoteSeparate]),
            ["SS"] = EveryCase([ReplyAll]),
            ["SWAN"] = sop,
            ["TEN"] = EveryCase([ReplyAll]),
            ["TP"] = EveryCase([ReplyAll, Cc(["info@taylorprice.co.uk"]), FeeNoteSeparate]),
            ["WALKER"] = sop,
            ["WLS"] = sop,
            ["YML"] = new(
                sop.EveryCase,
                [new([[new(SendingFact.ClaimSource, [claimSource("Swinton")])]], [AddTask("send Swinton a copy of the report on WhatsApp.")])])
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
        string? ClaimSourceIfAny(string name) =>
            organizations.Values.SingleOrDefault(item => item.Name == name && item.Role == "claim_source" && item.Active)?.Id.ToString("D");
        string Notes(string code) => organizations[principals[code].Principal.OrganizationId].NotesOnEveryCase ?? string.Empty;

        foreach (var (code, rules) in SopRules(ClaimSource, ClaimSourceIfAny))
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
        Assert.Equal(
            ["Ontrack789@gmail.com"],
            principals["SWADE"].Rules.EveryCase.Single(action => action.Kind == Act.SendTo).Addresses!.ToArray());
        // Send from and Report images left the rules for their own columns
        // (R1267): CS names its mailbox and MP's reports carry no images; no
        // other Principal does either.
        Assert.Equal(
            ["CS"],
            principals.Where(pair => pair.Value.Principal.SendFromMailbox is not null).Select(pair => pair.Key).ToArray());
        Assert.Equal("engineers@collisionengineers.co.uk", principals["CS"].Principal.SendFromMailbox);
        Assert.Equal(
            ["MP"],
            principals.Where(pair => !pair.Value.Principal.ReportImages).Select(pair => pair.Key).ToArray());

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
    /// Rules in the earlier shape are built (R1267 to R1270), exactly as the
    /// migration SendingRulesAreBuilt words them: the fixed settings become
    /// every-Case actions in their fixed order, Send from and Report images
    /// move to their columns, "All" is one group and "Any" one per condition,
    /// "Sender is not" is Sender with "is not", "Instruction mentions" goes
    /// with a rule left with nothing else, Rate card and Garage figures become
    /// tasks, and RJS's two rules read the Claim Sources that exist and active
    /// (R1268). Met again it changes nothing, and an old condition kind it
    /// does not know, or a rule adding and removing the same Cc address,
    /// stops it with the row unchanged.
    /// </summary>
    [Fact]
    public async Task TheBuiltRulesRestatementBuildsEveryOldSettingAndChangesNothingTwice()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
        // ExecuteSqlRaw reads braces as placeholders, and the rules are JSON.
        var build = SendingRulesAreBuilt.Sql
            .Replace("{", "{{", StringComparison.Ordinal)
            .Replace("}", "}}", StringComparison.Ordinal);
        const string repairerId = "6b1c3c9e-1d7e-4b8a-9a51-3f0f5a0b7c11";
        const string claimSpecialistsId = "6b1c3c9e-1d7e-4b8a-9a51-3f0f5a0b7c12";
        const string accidentSpecialistsId = "6b1c3c9e-1d7e-4b8a-9a51-3f0f5a0b7c13";
        context.Organizations.Add(new OrganizationEntity { Id = Guid.Parse(claimSpecialistsId), Name = "Claim Specialists", Role = "claim_source" });
        context.Organizations.Add(new OrganizationEntity { Id = Guid.Parse(accidentSpecialistsId), Name = "Accident Specialists", Role = "claim_source" });
        await context.SaveChangesAsync();
        // Inactive, so RJS's second rule has no Claim Source to read and goes.
        await context.Organizations
            .Where(item => item.Id == Guid.Parse(accidentSpecialistsId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Active, false));
        Task SetRulesAsync(string code, string json) => context.Principals
            .Where(principal => principal.Code == code)
            .ExecuteUpdateAsync(setters => setters.SetProperty(principal => principal.ReportSendingRulesJson, json));
        Task<PrincipalEntity> PrincipalAsync(string code) => context.Principals.AsNoTracking()
            .SingleAsync(principal => principal.Code == code);
        async Task<string> StoredAsync() => JsonSerializer.Serialize(
            await context.Principals.AsNoTracking().OrderBy(item => item.Code)
                .Select(item => new { item.Code, item.ReportSendingRulesJson, item.SendFromMailbox, item.ReportImages })
                .ToArrayAsync());

        var wls = """
            {"sendFromMailbox":"desk@collisionengineers.co.uk","sendTo":["to@principal.example"],"sendToOnly":true,"replyAll":true,"cc":["cc@principal.example"],"neverCc":["never@principal.example"],"attach":{"feeNoteSeparate":true,"estimate":true,"audatex":false,"reportImages":false,"vehicleImagesDocument":true,"figureBreakdown":false,"offerRepairInvoices":true},"garageFigures":true,"hold":"Ring the \"handler\" first.","reminders":["First task","Second task"],"attachmentName":"stray","rules":[{"match":"All","if":[{"kind":"Repairer","values":["<repairerId>"]},{"kind":"Outcome","values":["repairable"]},{"kind":"Mentions","values":["audit"]}],"then":{"ccAdd":["garage@principal.example"],"ccRemove":[],"remind":"Authorise the garage","rateCard":true}},{"match":"Any","if":[{"kind":"SenderNot","values":["handler@principal.example"]},{"kind":"RefPrefix","values":["a"]},{"kind":"PreviousJob","values":[]}],"then":{"ccAdd":[],"ccRemove":["cc@principal.example"],"hold":"Check it","stop":"Stop it","attach":{"estimate":false,"audatex":true}}},{"match":"Any","if":[{"kind":"Mentions","values":["Luton"]}],"then":{"ccAdd":["luton@principal.example"],"ccRemove":[]}}]}
            """.Replace("<repairerId>", repairerId, StringComparison.Ordinal);
        const string rjs = """
            {"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["engreport@robertjameslaw.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[{"match":"Any","if":[{"kind":"Mentions","values":["Luton"]},{"kind":"Mentions","values":["Claim Specialist","Claims Specialist","claimsspecialists.co.uk"]}],"then":{"ccAdd":["accounts@claimsspecialists.co.uk"],"ccRemove":[]}},{"match":"All","if":[{"kind":"Mentions","values":["Accident Specialist"]}],"then":{"ccAdd":["admin@accidentspecialist.co.uk"],"ccRemove":[]}}]}
            """;
        await SetRulesAsync("WLS", wls);
        await SetRulesAsync("RJS", rjs);
        var untouched = (await PrincipalAsync("ALS")).ReportSendingRulesJson;

        await context.Database.ExecuteSqlRawAsync(build);

        var builtWls = await PrincipalAsync("WLS");
        Assert.Equal(
            """
            {"everyCase":[{"kind":"SendTo","addresses":["to@principal.example"]},{"kind":"SendToOnly"},{"kind":"ReplyAll"},{"kind":"Cc","addresses":["cc@principal.example"]},{"kind":"NeverCc","addresses":["never@principal.example"]},{"kind":"Attach","file":"FeeNoteSeparate"},{"kind":"Attach","file":"Estimate"},{"kind":"Attach","file":"VehicleImagesDocument"},{"kind":"Attach","file":"OfferRepairInvoices"},{"kind":"Hold","text":"Ring the \"handler\" first."},{"kind":"Task","text":"First task"},{"kind":"Task","text":"Second task"}],"rules":[{"if":[[{"fact":"Repairer","values":["<repairerId>"]},{"fact":"Outcome","values":["repairable"]}]],"then":[{"kind":"Cc","addresses":["garage@principal.example"]},{"kind":"Task","text":"Authorise the garage"},{"kind":"Task","text":"Check the report's estimate against the Connexus Repair Rate Card."}]},{"if":[[{"fact":"Sender","values":["handler@principal.example"],"not":true}],[{"fact":"RefPrefix","values":["a"]}],[{"fact":"PreviousJob","values":[]}]],"then":[{"kind":"NeverCc","addresses":["cc@principal.example"]},{"kind":"DoNotAttach","file":"Estimate"},{"kind":"Attach","file":"Audatex"},{"kind":"Hold","text":"Check it"},{"kind":"Stop","text":"Stop it"}]},{"if":[[{"fact":"Outcome","values":["repairable"]}]],"then":[{"kind":"Task","text":"Send the figures to the garage."}]}]}
            """.Replace("<repairerId>", repairerId, StringComparison.Ordinal),
            builtWls.ReportSendingRulesJson);
        Assert.Equal("desk@collisionengineers.co.uk", builtWls.SendFromMailbox);
        Assert.False(builtWls.ReportImages);
        // The text is the built shape the store reads, and the migration's
        // task texts are the policy's.
        var read = EfOrganizationAdministration.ReadReportSending(builtWls.ReportSendingRulesJson);
        Assert.Equal(ReportDispatchPolicy.RateCardTask, read.Rules[0].Then[2].Text);
        Assert.Equal(ReportDispatchPolicy.GarageFiguresTask, Assert.Single(read.Rules[2].Then).Text);
        Assert.Equal(SendingRulesAreBuilt.DefaultRulesJson, EfOrganizationAdministration.DefaultReportSendingJson);

        var builtRjs = await PrincipalAsync("RJS");
        Assert.Equal(
            """
            {"everyCase":[{"kind":"ReplyAll"},{"kind":"Cc","addresses":["engreport@robertjameslaw.co.uk"]},{"kind":"Attach","file":"FeeNoteSeparate"}],"rules":[{"if":[[{"fact":"ClaimSource","values":["<claimSpecialistsId>"]}]],"then":[{"kind":"Cc","addresses":["accounts@claimsspecialists.co.uk"]}]}]}
            """.Replace("<claimSpecialistsId>", claimSpecialistsId, StringComparison.Ordinal),
            builtRjs.ReportSendingRulesJson);
        Assert.Null(builtRjs.SendFromMailbox);
        Assert.True(builtRjs.ReportImages);
        Assert.Equal(untouched, (await PrincipalAsync("ALS")).ReportSendingRulesJson);
        Assert.Contains(
            "everyCase",
            await context.Database.SqlQueryRaw<string>(
                    "SELECT [d].[definition] AS [Value] FROM sys.default_constraints AS [d] "
                    + "INNER JOIN sys.columns AS [c] ON [c].[object_id] = [d].[parent_object_id] AND [c].[column_id] = [d].[parent_column_id] "
                    + "WHERE [d].[parent_object_id] = OBJECT_ID(N'[dbo].[Principals]') AND [c].[name] = N'ReportSendingRulesJson'")
                .SingleAsync(),
            StringComparison.Ordinal);

        var built = await StoredAsync();
        await context.Database.ExecuteSqlRawAsync(build);

        Assert.Equal(built, await StoredAsync());

        var unknown = wls.Replace("\"kind\":\"Outcome\"", "\"kind\":\"ImagesFrom\"", StringComparison.Ordinal);
        Assert.NotEqual(wls, unknown);
        await SetRulesAsync("WLS", unknown);

        var refused = await Assert.ThrowsAnyAsync<DbException>(() => context.Database.ExecuteSqlRawAsync(build));

        Assert.Contains("restate that rule on the Principal contact", refused.Message, StringComparison.Ordinal);
        Assert.Equal(unknown, (await PrincipalAsync("WLS")).ReportSendingRulesJson);

        // A rule that adds and removes the same Cc address would be built
        // into one the store refuses to read, so it stops the statement too.
        var overlap = wls.Replace(
            "\"ccAdd\":[],\"ccRemove\":[\"cc@principal.example\"]",
            "\"ccAdd\":[\"CC@principal.example\"],\"ccRemove\":[\"cc@principal.example\"]",
            StringComparison.Ordinal);
        Assert.NotEqual(wls, overlap);
        await SetRulesAsync("WLS", overlap);

        var overlapRefused = await Assert.ThrowsAnyAsync<DbException>(() => context.Database.ExecuteSqlRawAsync(build));

        Assert.Contains("both adds and removes the same Cc address", overlapRefused.Message, StringComparison.Ordinal);
        Assert.Equal(overlap, (await PrincipalAsync("WLS")).ReportSendingRulesJson);
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
        // Its SBL and PCH steps read the rules' earlier shape, so over built
        // rules (R1267) they change nothing.
        var seededSbl = await context.Principals.AsNoTracking()
            .Where(principal => principal.Code == "SBL")
            .Select(principal => principal.ReportSendingRulesJson)
            .SingleAsync();
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
            foreach (var condition in seeded.Rules.Rules.SelectMany(rule => rule.If.SelectMany(group => group)))
            {
                var role = condition.Fact switch
                {
                    SendingFact.ClaimSource => "claim_source",
                    SendingFact.Repairer => "repairer",
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

        IReadOnlyList<string> Addresses(PrincipalReportSendingRules rules, Act kind) =>
            [.. rules.EveryCase.Where(action => action.Kind == kind).SelectMany(action => action.Addresses ?? [])];

        // AX: Repairer is Easdons Bodyshop and Claim Source is Guardian
        // Accident Claims or James Claims (R1025), then Garage figures last.
        Assert.Equal(2, Rules("AX").Rules.Count);
        var ax = Rules("AX").Rules[0];
        var axGroup = Assert.Single(ax.If);
        Assert.Equal([SendingFact.Repairer, SendingFact.ClaimSource], axGroup.Select(condition => condition.Fact).ToArray());
        Assert.Equal([IdOf("Easdons Bodyshop")], axGroup[0].Values.ToArray());
        Assert.Equal([IdOf("Guardian Accident Claims"), IdOf("James Claims")], axGroup[1].Values.ToArray());
        Assert.Equal("p.mandy@oakwoodscotland.co.uk", Assert.Single(Assert.Single(ax.Then).Addresses!));
        Assert.Equal(2, Addresses(Rules("AX"), Act.SendTo).Count);
        Assert.False(Rules("AX").MayAttach(SendingFile.FeeNoteSeparate));
        Assert.Equal(new PrincipalReportSendingRules([], [GarageFigures]), new PrincipalReportSendingRules([], [Rules("AX").Rules[1]]));

        // DFD: SMC, Car 2 Go and a CarClaims stop.
        Assert.Equal(3, Rules("DFD").Rules.Count);
        var dfdStop = Assert.Single(Rules("DFD").Rules[2].Then);
        Assert.Equal(Act.Stop, dfdStop.Kind);
        Assert.Equal("CarClaims job. This needs checking with Andy before it is sent.", dfdStop.Text);
        Assert.Equal([IdOf("CarClaims")], Assert.Single(Assert.Single(Rules("DFD").Rules[2].If)).Values.ToArray());

        // YML: Claim Source is Swinton (R1026), an after-sending task.
        var yml = Assert.Single(Rules("YML").Rules);
        var ymlCondition = Assert.Single(Assert.Single(yml.If));
        Assert.Equal(SendingFact.ClaimSource, ymlCondition.Fact);
        Assert.Equal([IdOf("Swinton")], ymlCondition.Values.ToArray());
        Assert.Equal(AddTask("send Swinton a copy of the report on WhatsApp."), Assert.Single(yml.Then));

        // CS has no instruction e-mail, so it names its mailbox (its own column).
        Assert.Equal("engineers@collisionengineers.co.uk", principals["CS"].Principal.SendFromMailbox);
        Assert.Equal(2, Addresses(Rules("CS"), Act.SendTo).Count);

        // MP: no report images, and the images document and figure breakdown
        // are required companions.
        Assert.False(principals["MP"].Principal.ReportImages);
        Assert.True(Rules("MP").MayAttach(SendingFile.VehicleImagesDocument));
        Assert.True(Rules("MP").MayAttach(SendingFile.FigureBreakdown));
        Assert.Contains(
            new ReportSendingAction(Act.Hold, Text: "WhatsApp the report to Midas to check before it is sent."),
            Rules("MIDAS").EveryCase);
        Assert.Equal("credithire@hackneysolutions.co.uk", Assert.Single(Addresses(Rules("QCL"), Act.NeverCc)));

        // PCH: Repairer is iRevolution Repair Network and the outcome is
        // repairable (R1033); its audit rule lost "mentions audit" (R1268) and
        // keeps reference prefix a or a previous job. SBL never copies its
        // Connexus address (R1031).
        var pch = Assert.Single(Rules("PCH").Rules[0].If);
        Assert.Equal([SendingFact.Repairer, SendingFact.Outcome], pch.Select(condition => condition.Fact).ToArray());
        Assert.Equal([ContactsFollowQuickReference.IRevolutionRepairerId], pch[0].Values.ToArray());
        Assert.Equal("repairs@irevolutionrepairnetwork.co.uk", Assert.Single(Rules("PCH").Rules[0].Then[0].Addresses!));
        Assert.Equal(
            [SendingFact.RefPrefix, SendingFact.PreviousJob],
            Rules("PCH").Rules[1].If.Select(group => Assert.Single(group).Fact).ToArray());
        Assert.Empty(Addresses(Rules("SBL"), Act.Cc));
        Assert.Equal("SBL@connexus.co.uk", Assert.Single(Addresses(Rules("SBL"), Act.NeverCc)));
        Assert.True(Rules("BLACK").MayAttach(SendingFile.Estimate));
        Assert.True(Rules("ALS").MayAttach(SendingFile.Estimate));
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
        var offering = EveryCase(
            [ReplyAll, FeeNoteSeparate, Attach(SendingFile.OfferExternalEstimates), Attach(SendingFile.OfferRepairInvoices)]);
        Assert.Equal(offering, principals["ULA"].Rules);
        Assert.True(principal.ReportImages);
        Assert.Null(principal.SendFromMailbox);
        // Stored as the serializer writes it, the two Offer files after the rest.
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
        Assert.Equal(EfOrganizationAdministration.DefaultReportSendingJson, principal.ReportSendingRulesJson);
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

    /// <summary>
    /// Easy Claim (operator, 26 November 2026; R1302): one active Claim
    /// Source with its name and nothing else, linked to RJS and no other
    /// Principal, and RJS's sending rules do not copy it. The seed met again
    /// changes nothing.
    /// </summary>
    [Fact]
    public async Task EasyClaimIsAClaimSourceLinkedToRjsOnly()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var (principals, organizations) = await LoadAsync(factory);

        var easyClaim = organizations[Guid.Parse(EasyClaimIsAnRjsClaimSource.ClaimSourceId)];
        Assert.Equal("Easy Claim", easyClaim.Name);
        Assert.Equal("claim_source", easyClaim.Role);
        Assert.True(easyClaim.Active);
        Assert.Null(easyClaim.Address);
        Assert.Null(easyClaim.Postcode);
        Assert.Null(easyClaim.Email);
        Assert.Null(easyClaim.Telephone);
        Assert.Null(easyClaim.NotesOnEveryCase);
        Assert.Single(organizations.Values, item => item.Role == "claim_source" && item.NormalizedName == "EASY CLAIM");
        var rjs = principals["RJS"].Principal;
        Assert.DoesNotContain(
            easyClaim.Id.ToString("D"), rjs.ReportSendingRulesJson, StringComparison.OrdinalIgnoreCase);

        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var links = context.Set<ContactPrincipalLinkEntity>().AsNoTracking();
        Assert.Equal(rjs.Id, (await links.SingleAsync(link => link.OrganizationId == easyClaim.Id)).PrincipalId);
        var linkCount = await links.CountAsync();

        await context.Database.ExecuteSqlRawAsync(EasyClaimIsAnRjsClaimSource.SeedSql);
        Assert.Equal(organizations.Count, await context.Organizations.CountAsync());
        Assert.Equal(linkCount, await links.CountAsync());
    }
}

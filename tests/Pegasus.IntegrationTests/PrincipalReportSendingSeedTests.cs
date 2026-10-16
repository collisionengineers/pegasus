using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Migration 20261012190000_PrincipalReportSendingRules: the 17 SOP
/// Principals Pegasus did not have, the claim sources rule conditions point
/// at, every SOP Principal's rules and the TL thresholds appended to Notes on
/// every Case (Report Sending SOP v5, 2 October 2026; operator, 6 October
/// 2026). Every Principal has rules.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class PrincipalReportSendingSeedTests
{
    private static readonly string[] SopCodes =
    [
        "ACSP", "ALISON", "ALS", "AMS", "AX", "BC", "BLACK", "CS", "DFD", "FW", "HTU", "KBS", "KERR", "KMR", "MIDAS", "MOTORX",
        "MP", "OAK", "PCH", "QCL", "QDOS", "RJS", "SBL", "SIX", "SS", "SWADE", "SWAN", "TEN", "TP", "WALKER", "WLS", "YML"
    ];

    private static readonly string[] NewCodes =
    [
        "AMS", "ACSP", "ALISON", "CS", "HTU", "KERR", "KMR", "MIDAS", "MOTORX", "SIX", "SS", "SWADE", "SWAN", "TEN", "TP", "WALKER", "WLS"
    ];

    private static readonly (string Name, string Role)[] SourceOrganizations =
    [
        ("Car 2 Go", "claim_source"), ("SMC", "claim_source"), ("CarClaims", "claim_source"), ("Expert Claims", "claim_source"),
        ("Rapid Rental Solutions", "claim_source")
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

        Assert.Equal(32, SopCodes.Length);
        Assert.All(SopCodes, code => Assert.True(principals.ContainsKey(code), $"{code} must be seeded."));
        Assert.Equal(15 + NewCodes.Length, SopCodes.Count(principals.ContainsKey));
        Assert.All(SopCodes, code => Assert.True(principals[code].Principal.IsActive, code));
        foreach (var (code, seeded) in principals)
        {
            Assert.False(string.IsNullOrWhiteSpace(seeded.Principal.ReportSendingRulesJson), code);
            // Stored in exactly the form the serializer writes it.
            Assert.Equal(seeded.Rules, EfOrganizationAdministration.ReadReportSending(
                EfOrganizationAdministration.ToReportSendingJson(seeded.Rules)));
            Assert.Equal(seeded.Rules, PrincipalReportSendingRules.Normalize(seeded.Rules));
        }
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

        // RJS: any of "mentions Luton" or Claim Specialists. ACSP and CS are Principals
        // only, so the instruction is matched on the v5 SOP's own text for them.
        var rjs = Rules("RJS").Rules[0];
        Assert.Equal(ReportSendingRuleMatch.Any, rjs.Match);
        Assert.All(rjs.If, condition => Assert.Equal(ReportSendingConditionKind.Mentions, condition.Kind));
        Assert.Equal(
            [["Luton"], ["Claim Specialist", "Claims Specialist", "claimsspecialists.co.uk"]],
            rjs.If.Select(condition => condition.Values.ToArray()).ToArray());
        Assert.Equal(2, rjs.Then.CcAdd.Count);
        var rjsAcsp = Rules("RJS").Rules[1];
        Assert.Equal(["Accident Specialist"], Assert.Single(rjsAcsp.If, condition => condition.Kind == ReportSendingConditionKind.Mentions).Values.ToArray());
        Assert.Equal(["admin@accidentspecialist.co.uk"], rjsAcsp.Then.CcAdd.ToArray());

        // YML: images from Swinton is a question for staff.
        var yml = Assert.Single(Rules("YML").Rules);
        Assert.Equal(ReportSendingConditionKind.ImagesFrom, Assert.Single(yml.If).Kind);
        Assert.Equal("Swinton", Assert.Single(yml.If[0].Values));
        Assert.Equal("send Swinton a copy of the report.", yml.Then.Remind);

        // FW: copied unless the sender is Fairway already.
        var fw = Assert.Single(Rules("FW").Rules);
        Assert.Equal(ReportSendingConditionKind.SenderNot, Assert.Single(fw.If).Kind);
        Assert.Equal("info@fairwaylegal.co.uk", Assert.Single(fw.Then.CcAdd));

        // DFD: SMC, Car 2 Go and a CarClaims stop.
        Assert.Equal(3, Rules("DFD").Rules.Count);
        Assert.Equal(
            "CarClaims job. This needs checking with Andy before it is sent.",
            Rules("DFD").Rules[2].Then.Stop);
        Assert.Equal([IdOf("CarClaims")], Rules("DFD").Rules[2].If[0].Values.ToArray());

        // CS has no instruction e-mail, so it names its mailbox.
        Assert.Equal("engineers@collisionengineers.co.uk", Rules("CS").SendFromMailbox);
        Assert.Equal(2, Rules("CS").SendTo.Count);
        Assert.All(
            SopCodes.Where(code => code != "CS"),
            code => Assert.Null(Rules(code).SendFromMailbox));

        // PCH: its own report names, and only the fixed address.
        Assert.Equal("{reg} Initial", Rules("PCH").AttachmentName!.First);
        Assert.Equal("{reg} Supplementary", Rules("PCH").AttachmentName!.Resend);
        Assert.True(Rules("PCH").SendToOnly);
        Assert.True(Rules("PCH").Attach.Audatex);

        // MP: the images document and figure breakdown are required companions.
        Assert.False(Rules("MP").Attach.ReportImages);
        Assert.True(Rules("MP").Attach.VehicleImagesDocument);
        Assert.True(Rules("MP").Attach.FigureBreakdown);
        Assert.Equal("WhatsApp the report to Midas to check before it is sent.", Rules("MIDAS").Hold);
        Assert.Equal("Authorise the garage.", Assert.Single(Rules("SBL").Reminders));
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
        Assert.Contains("TL threshold: 74%", Notes("OAK"), StringComparison.Ordinal);
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

using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.ReferenceDataLoader;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The one-off reference-data loader: "One-off operator-run loader through
/// <c>IContactDirectoryAdministration.SaveAsync</c> from reviewed CSVs under
/// <c>artifacts/</c>" (operator, 15 October 2026). Every row here is synthetic
/// and written to a temporary folder; the reviewed register is never read.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ReferenceDataLoaderTests
{
    private const string FirstBatch = "rdl1";
    private const string ReviewedAt = "2026-10-17";

    private static readonly ActionActor Reader = ActionActor.Staff(
        Guid.Parse("2b7e5c1d-8a4f-4c3e-9d6b-1f0a2e3c4d5b"),
        [StaffRole.Administrator]);

    private static readonly char[] QuotedCharacters = [',', '"', '\r', '\n'];

    [Theory]
    [InlineData("refused", ReferenceFiles.Telephone, "Fax 0161 496 0000", null)]
    [InlineData("collapsed", ReferenceFiles.Telephone, "0161  496   0000", "0161 496 0000")]
    [InlineData("too-long", ReferenceFiles.Postcode, "LD1 1AA LD1 1AA LD1 1AA", null)]
    public async Task HygieneKeepsTheFirstValidValueAndReportsTheRest(string row, string column, string value, string? kept)
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var administrator = await SeedStaffAsync(database, StaffRole.Administrator, enabled: true);
        using var root = new LoadRoot();
        var data = root.Write(Row(
            ReferenceFiles.Repairers,
            "R0001",
            $"Loader Hygiene Repairer {row}",
            "add",
            (column, value),
            (ReferenceFiles.RestListed, "telephone=0161 496 0001;email=second%3Bthird@loader.example.invalid")));

        var planned = await RunAsync(database.ConnectionString, DryRun(data, root.Report("dry-run"), administrator));

        Assert.True(planned.Exit == 0, planned.Error);
        var plan = ReadReport(root.Report("dry-run")).GetProperty("plan");
        var fields = Assert.Single(plan.GetProperty("contactWrites").EnumerateArray()).GetProperty("fields");
        Assert.Equal(kept, fields.GetProperty(column).GetString());
        string?[] dropped = kept is null ? [column] : [];
        Assert.Equal(
            dropped,
            plan.GetProperty("hygiene").EnumerateArray().Select(drop => drop.GetProperty("column").GetString()).ToArray());
        Assert.Equal(
            ["telephone=0161 496 0001", "email=second;third@loader.example.invalid"],
            plan.GetProperty("restListed").EnumerateArray().Select(rest => rest.GetProperty("entry").GetString()!).ToArray());
    }

    [Fact]
    public async Task DryRunWritesNothingAndReportsRejects()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var administrator = await SeedStaffAsync(database, StaffRole.Administrator, enabled: true);
        await SaveContactAsync(database, NewContact(ContactRole.Repairer, "Loader Held Repairer"));
        using var root = new LoadRoot();
        var data = root.Write(
            PrincipalRow("P0001", "Loader Principal Alpha", "RDLA", "add-inactive", "false", "physical_address"),
            PrincipalRow("P0002", "Loader Principal Coded Badly", "R&D", "add-inactive", "false", "physical_address"),
            Row(ReferenceFiles.Repairers, "R0001", "Loader Repairer One", "add"),
            Row(ReferenceFiles.Repairers, "R0002", "Loader Repairer One", "add"),
            Row(ReferenceFiles.Repairers, "R0003", "Loader Held Repairer", "add"),
            Link("R0001", "repairer", "RDLA"),
            Link("R0001", "repairer", "RDLX"),
            Location("X0001", "Loader Body Centre", string.Empty, "LD1 1AA", "RDLA"),
            Location("X0002", "Loader Paint Centre", "2 Loader Street, LD2 2BB", "LD2 2BB", "RDLA"));
        var before = await WrittenRowsAsync(database);

        var planned = await RunAsync(database.ConnectionString, DryRun(data, root.Report("dry-run"), administrator));

        Assert.True(planned.Exit == 0, planned.Error);
        Assert.Equal(before, await WrittenRowsAsync(database));
        var plan = ReadReport(root.Report("dry-run")).GetProperty("plan");
        Assert.Equal(
            ["inspection-locations.csv X0001", "links.csv R0001/RDLX", "principals.csv P0002", "repairers.csv R0002", "repairers.csv R0003"],
            plan.GetProperty("rejects").EnumerateArray()
                .Select(reject => $"{reject.GetProperty("file").GetString()} {reject.GetProperty("rowId").GetString()}")
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            ["P0001", "R0001"],
            plan.GetProperty("contactWrites").EnumerateArray().Select(write => write.GetProperty("rowId").GetString()!).ToArray());
        var location = Assert.Single(plan.GetProperty("locationWrites").EnumerateArray());
        Assert.Equal("X0002", location.GetProperty("rowId").GetString());
    }

    [Fact]
    public async Task ApplyLoadsEveryKindWithLinksInOrder()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var administrator = await SeedStaffAsync(database, StaffRole.Administrator, enabled: true);
        var live = await SaveContactAsync(database, NewContact(ContactRole.Principal, "Loader Live Principal", "RDLL"));
        using var root = new LoadRoot();
        var data = root.Write(
            PrincipalRow(
                "P0001", "Loader Live Principal", "RDLL", "update-live", "true", "image_based_assessment",
                (ReferenceFiles.Email, "desk@loader.example.invalid")),
            PrincipalRow("P0002", "Loader Inactive Principal", "RDLI", "add-inactive", "false", "physical_address"),
            Row(ReferenceFiles.ClaimSources, "C0001", "Loader Claim Source", "add", (ReferenceFiles.NotesOnEveryCase, "Loader note")),
            Row(ReferenceFiles.Repairers, "R0001", "Loader Repairer", "add"),
            Row(ReferenceFiles.Storage, "S0001", "Loader Storage", "add"),
            Row(ReferenceFiles.Engineers, "E0001", "Loader Engineer", "add"),
            Link("C0001", "claim_source", "RDLL"),
            Link("C0001", "claim_source", "RDLI"),
            Link("R0001", "repairer", "RDLI"),
            Link("S0001", "storage", "RDLL"),
            Link("E0001", "third_party_engineer", "RDLL"),
            Location("X0001", "Loader Body Centre", "1 Loader Street, Testtown, LD1 1AA", "ld1 1aa", "RDLI;RDLL"));

        var applied = await LoadAsync(database, root, data, administrator);

        Assert.Equal(7, applied.GetProperty("outcome").GetProperty("written").GetInt32());
        var livePrincipal = live.OwnPrincipalIds!.Single();
        var updated = await GetContactAsync(database, live.OrganizationId);
        Assert.NotNull(updated);
        Assert.Equal(CaseInspectionMode.ImageBasedAssessment, updated.PrincipalInspectionMode);
        Assert.Equal("desk@loader.example.invalid", updated.Email);

        // The Principal R15 loads inactive is created inactive and linked like any other.
        var inactive = await GetContactAsync(database, ReferenceFiles.OrganizationId("principal", "P0002"));
        Assert.NotNull(inactive);
        Assert.False(inactive.Active);
        Assert.Equal("RDLI", inactive.PrincipalCode);
        var inactivePrincipal = inactive.OwnPrincipalIds!.Single();
        var claimSource = await GetContactAsync(database, ReferenceFiles.OrganizationId("claim_source", "C0001"));
        Assert.NotNull(claimSource);
        Assert.Equal(new[] { livePrincipal, inactivePrincipal }.Order(), claimSource.LinkedPrincipalIds);
        Assert.Equal("Loader note", claimSource.NotesOnEveryCase);
        Assert.Equal([inactivePrincipal], (await GetContactAsync(database, ReferenceFiles.OrganizationId("repairer", "R0001")))!.LinkedPrincipalIds);
        Assert.Equal([livePrincipal], (await GetContactAsync(database, ReferenceFiles.OrganizationId("storage", "S0001")))!.LinkedPrincipalIds);
        Assert.Equal(
            [livePrincipal],
            (await GetContactAsync(database, ReferenceFiles.OrganizationId("third_party_engineer", "E0001")))!.LinkedPrincipalIds);
        Assert.Equal(
            "1 Loader Street, Testtown, LD1 1AA|LD1 1AA|2",
            await database.ScalarAsync<string>(
                """
                SELECT loc.Address + N'|' + loc.Postcode + N'|'
                    + CONVERT(nvarchar(10), (SELECT COUNT(*) FROM InspectionLocationPrincipalLinks AS lnk WHERE lnk.InspectionLocationId = loc.Id))
                FROM InspectionLocations AS loc
                WHERE loc.Source = N'register' AND loc.SourceRecordId = N'X0001'
                """));

        // (a) the Principals, then (b) every other contact, then (c) the locations.
        var steps = (await database.ScalarAsync<string>(
                "SELECT STRING_AGG(CorrelationId, N';') WITHIN GROUP (ORDER BY OccurredAtUtc, Id) FROM ActionHistory WHERE CorrelationId LIKE N'ref04:rdl1:%'"))
            .Split(';')
            .Select(key => key.Split(':')[2] switch { "principal" => 0, "inspection_location" => 2, _ => 1 })
            .ToArray();
        Assert.Equal(7, steps.Length);
        Assert.Equal(steps.Order(), steps);
    }

    [Fact]
    public async Task SecondApplyWritesNothing()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var administrator = await SeedStaffAsync(database, StaffRole.Administrator, enabled: true);
        using var root = new LoadRoot();
        var data = root.Write(
            PrincipalRow("P0001", "Loader Second Principal", "RDLS", "add-inactive", "false", "physical_address"),
            Row(ReferenceFiles.Repairers, "R0001", "Loader Second Repairer", "add", (ReferenceFiles.Telephone, "0161 496 0000")),
            Link("R0001", "repairer", "RDLS"),
            Location("X0001", "Loader Second Centre", "1 Loader Street, LD1 1AA", "LD1 1AA", "RDLS"));
        await LoadAsync(database, root, data, administrator);
        var before = await WrittenRowsAsync(database);

        var second = await LoadAsync(database, root, data, administrator, "rdl2");

        Assert.Equal(before, await WrittenRowsAsync(database));
        var plan = second.GetProperty("plan");
        Assert.Empty(plan.GetProperty("contactWrites").EnumerateArray());
        Assert.All(plan.GetProperty("counts").EnumerateArray(), count =>
            Assert.Equal(0, count.GetProperty("create").GetInt32() + count.GetProperty("update").GetInt32()));
        var outcome = second.GetProperty("outcome");
        Assert.Equal(0, outcome.GetProperty("written").GetInt32());
        Assert.Equal(1, outcome.GetProperty("locations").GetProperty("keptExisting").GetInt32());
    }

    [Fact]
    public async Task ApplyRefusesWithoutTheApprovedHashOrTarget()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var administrator = await SeedStaffAsync(database, StaffRole.Administrator, enabled: true);
        using var root = new LoadRoot();
        var data = root.Write(Row(ReferenceFiles.Repairers, "R0001", "Loader Refused Repairer", "add"));
        var planned = await RunAsync(database.ConnectionString, DryRun(data, root.Report("dry-run"), administrator));
        Assert.True(planned.Exit == 0, planned.Error);
        var report = ReadReport(root.Report("dry-run"));
        var target = report.GetProperty("plan").GetProperty("target").GetString()!;
        var planHash = report.GetProperty("planHash").GetString()!;
        var before = await WrittenRowsAsync(database);
        var applyReport = root.Report("apply");

        var wrongHash = await RunAsync(
            database.ConnectionString, Apply(data, applyReport, administrator, target, new string('0', 64)));
        var wrongTarget = await RunAsync(
            database.ConnectionString, Apply(data, applyReport, administrator, "elsewhere/Pegasus", planHash));
        var noHash = await RunAsync(
            database.ConnectionString, Apply(data, applyReport, administrator, target, planHash)[..^2]);

        Assert.Equal((1, 1, 1), (wrongHash.Exit, wrongTarget.Exit, noHash.Exit));
        Assert.Contains("nothing was written", wrongHash.Error, StringComparison.Ordinal);
        Assert.Contains("nothing was written", wrongTarget.Error, StringComparison.Ordinal);
        Assert.Contains("--approved-report-sha256 is required", noHash.Error, StringComparison.Ordinal);
        Assert.Equal(before, await WrittenRowsAsync(database));
        Assert.False(File.Exists(applyReport));
        Assert.Null(await GetContactAsync(database, ReferenceFiles.OrganizationId("repairer", "R0001")));
    }

    [Fact]
    public async Task ApplyRefusesAnActorThatIsNotAnEnabledAdministrator()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var disabled = await SeedStaffAsync(database, StaffRole.Administrator, enabled: false);
        var user = await SeedStaffAsync(database, StaffRole.User, enabled: true);
        using var root = new LoadRoot();
        var data = root.Write(Row(ReferenceFiles.Repairers, "R0001", "Loader Actor Repairer", "add"));
        var target = LoaderCommand.TargetOf(database.ConnectionString);
        var before = await WrittenRowsAsync(database);

        foreach (var actor in new[] { disabled, user, Guid.NewGuid() })
        {
            var planned = await RunAsync(database.ConnectionString, DryRun(data, root.Report("dry-run"), actor));
            var applied = await RunAsync(
                database.ConnectionString, Apply(data, root.Report("apply"), actor, target, new string('0', 64)));

            Assert.Equal((1, 1), (planned.Exit, applied.Exit));
            Assert.Contains("is not an enabled Administrator", planned.Error, StringComparison.Ordinal);
            Assert.Contains("is not an enabled Administrator", applied.Error, StringComparison.Ordinal);
        }

        Assert.Equal(before, await WrittenRowsAsync(database));
        Assert.Null(await GetContactAsync(database, ReferenceFiles.OrganizationId("repairer", "R0001")));
    }

    [Fact]
    public async Task MissingColumnOrRefusedFolderStopsBeforeTheDatabaseIsRead()
    {
        // No server answers this connection string, so a load that read the
        // database would fail on the connection, not on the files or folders.
        var unread = new SqlConnectionStringBuilder
        {
            DataSource = "unread.invalid",
            InitialCatalog = "Unread",
            ConnectTimeout = 1
        }.ConnectionString;
        using var root = new LoadRoot();
        var data = root.Write(Row(ReferenceFiles.Repairers, "R0001", "Loader Unread Repairer", "add"));
        File.WriteAllText(Path.Combine(data, ReferenceFiles.Principals), "row_id,name\r\n");
        var actor = Guid.NewGuid();

        var missingColumn = await RunAsync(unread, DryRun(data, root.Report("dry-run"), actor));
        var corpus = await RunAsync(
            unread, DryRun(Path.Combine(root.Root, "corpus", "artifacts", "load"), root.Report("dry-run"), actor));
        var reference = await RunAsync(
            unread, DryRun(Path.Combine(root.Root, "reference", "load"), root.Report("dry-run"), actor));
        var referenceReport = await RunAsync(
            unread, DryRun(data, Path.Combine(root.Root, "artifacts", "reference", "report.json"), actor));

        Assert.Equal((1, 1, 1, 1), (missingColumn.Exit, corpus.Exit, reference.Exit, referenceReport.Exit));
        Assert.Contains("inspection_mode", missingColumn.Error, StringComparison.Ordinal);
        Assert.Contains("--data must be under artifacts/", corpus.Error, StringComparison.Ordinal);
        Assert.Contains("--data must be under artifacts/", reference.Error, StringComparison.Ordinal);
        Assert.Contains("--report must be under artifacts/", referenceReport.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(root.Root, "artifacts", "reports")));
    }

    [Fact]
    public async Task OnlyApprovedRowsAreLoaded()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var administrator = await SeedStaffAsync(database, StaffRole.Administrator, enabled: true);
        using var root = new LoadRoot();
        var proposedLocation = Location("X0001", "Loader Proposed Centre", "1 Loader Street, LD1 1AA", "LD1 1AA", string.Empty);
        proposedLocation.Cells[ReferenceFiles.Review] = "proposed";
        var data = root.Write(
            Row(ReferenceFiles.Repairers, "R0001", "Loader Approved Repairer", "add"),
            Row(ReferenceFiles.Repairers, "R0002", "Loader Proposed Repairer", "add", (ReferenceFiles.Review, "proposed")),
            Row(ReferenceFiles.Repairers, "R0003", "Loader Held Repairer", "hold"),
            Row(ReferenceFiles.Repairers, "R0004", "Loader Struck Repairer", "add", (ReferenceFiles.Review, "struck")),
            PrincipalRow("P0001", "Loader Left Principal", "RDLT", "leave", "true", "physical_address"),
            proposedLocation);

        var applied = await LoadAsync(database, root, data, administrator);

        Assert.NotNull(await GetContactAsync(database, ReferenceFiles.OrganizationId("repairer", "R0001")));
        foreach (var (kind, rowId) in new[] { ("repairer", "R0002"), ("repairer", "R0003"), ("repairer", "R0004"), ("principal", "P0001") })
        {
            Assert.Null(await GetContactAsync(database, ReferenceFiles.OrganizationId(kind, rowId)));
        }

        Assert.Equal(0, await database.ScalarAsync<int>("SELECT COUNT(*) FROM InspectionLocations WHERE SourceRecordId = N'X0001'"));
        Assert.Equal(
            [
                "inspection-locations.csv add proposed 1",
                "principals.csv leave approved 1",
                "repairers.csv add proposed 1",
                "repairers.csv add struck 1",
                "repairers.csv hold approved 1"
            ],
            applied.GetProperty("plan").GetProperty("skipped").EnumerateArray()
                .Select(skipped => string.Join(
                    ' ',
                    skipped.GetProperty("file").GetString(),
                    skipped.GetProperty("action").GetString(),
                    skipped.GetProperty("review").GetString(),
                    skipped.GetProperty("rows").GetInt32().ToString(CultureInfo.InvariantCulture)))
                .ToArray());
    }

    [Fact]
    public async Task BlankCellsKeepStoredValues()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var administrator = await SeedStaffAsync(database, StaffRole.Administrator, enabled: true);
        var stored = await SaveContactAsync(database, NewContact(ContactRole.ClaimSource, "Loader Stored Source") with
        {
            Email = "claims@loader.example.invalid",
            Telephone = "0161 496 0000",
            Address = "1 Stored Street"
        });
        using var root = new LoadRoot();
        var data = root.Write(Row(
            ReferenceFiles.ClaimSources,
            "C0001",
            "Loader Stored Source",
            "update-live",
            (ReferenceFiles.Telephone, "0161 496 0999"),
            (ReferenceFiles.ContactPerson, "Loader Contact")));

        var applied = await LoadAsync(database, root, data, administrator);

        // The stored Claim Source is matched by name and keeps its id.
        var reread = await GetContactAsync(database, stored.OrganizationId);
        Assert.NotNull(reread);
        Assert.Equal(
            ("claims@loader.example.invalid", "1 Stored Street", "0161 496 0999", "Loader Contact"),
            (reread.Email, reread.Address, reread.Telephone, reread.ContactPerson));
        Assert.Null(await GetContactAsync(database, ReferenceFiles.OrganizationId("claim_source", "C0001")));
        var overwrite = Assert.Single(applied.GetProperty("plan").GetProperty("overwrites").EnumerateArray());
        Assert.Equal(
            ("telephone", "0161 496 0000", "0161 496 0999"),
            (overwrite.GetProperty("field").GetString(), overwrite.GetProperty("before").GetString(), overwrite.GetProperty("after").GetString()));
    }

    [Fact]
    public async Task HistoryNamesTheAdministratorActor()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var administrator = await SeedStaffAsync(database, StaffRole.Administrator, enabled: true);
        using var root = new LoadRoot();
        var data = root.Write(
            PrincipalRow("P0001", "Loader History Principal", "RDLH", "add-inactive", "false", "physical_address"),
            Row(ReferenceFiles.Repairers, "R0001", "Loader History Repairer", "add"),
            Link("R0001", "repairer", "RDLH"),
            Location("X0001", "Loader History Centre", "1 Loader Street, LD1 1AA", "LD1 1AA", "RDLH"));

        var applied = await LoadAsync(database, root, data, administrator);

        var written = applied.GetProperty("outcome").GetProperty("written").GetInt32();
        Assert.Equal(3, written);
        Assert.Equal(written, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM ActionHistory WHERE CorrelationId LIKE N'ref04:rdl1:%'"));
        Assert.Equal(written, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE CorrelationId LIKE N'ref04:rdl1:%'
              AND ActorKind = N'Staff'
              AND ActorSubjectId = N'{administrator:D}'
              AND ActorRolesJson = N'["Administrator"]'
            """));
    }

    private static async Task<JsonElement> LoadAsync(
        LocalDbTestDatabase database,
        LoadRoot root,
        string data,
        Guid administrator,
        string batch = FirstBatch)
    {
        var planned = await RunAsync(database.ConnectionString, DryRun(data, root.Report($"{batch}-dry-run"), administrator, batch));
        Assert.True(planned.Exit == 0, planned.Error);
        var report = ReadReport(root.Report($"{batch}-dry-run"));
        var applied = await RunAsync(
            database.ConnectionString,
            Apply(
                data,
                root.Report($"{batch}-apply"),
                administrator,
                report.GetProperty("plan").GetProperty("target").GetString()!,
                report.GetProperty("planHash").GetString()!,
                batch));
        Assert.True(applied.Exit == 0, applied.Error);
        return ReadReport(root.Report($"{batch}-apply"));
    }

    private static async Task<(int Exit, string Output, string Error)> RunAsync(string connectionString, string[] args)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var exit = await LoaderCommand.RunAsync(args, connectionString, output, error, CancellationToken.None);
        return (exit, output.ToString(), error.ToString());
    }

    private static string[] DryRun(string data, string report, Guid actor, string batch = FirstBatch) =>
    [
        "dry-run", "--data", data, "--actor-staff-id", actor.ToString("D"), "--batch", batch,
        "--reviewed-at", ReviewedAt, "--report", report
    ];

    private static string[] Apply(string data, string report, Guid actor, string target, string planHash, string batch = FirstBatch) =>
    [
        "apply", "--data", data, "--actor-staff-id", actor.ToString("D"), "--batch", batch,
        "--reviewed-at", ReviewedAt, "--report", report, "--target", target, "--approved-report-sha256", planHash
    ];

    private static JsonElement ReadReport(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        return document.RootElement.Clone();
    }

    /// <summary>Every row a load could write, so an unchanged sum means nothing was written.</summary>
    private static Task<int> WrittenRowsAsync(LocalDbTestDatabase database) => database.ScalarAsync<int>(
        """
        SELECT (SELECT COUNT(*) FROM Organizations) + (SELECT COUNT(*) FROM Principals)
            + (SELECT COUNT(*) FROM ContactPrincipalLinks) + (SELECT COUNT(*) FROM InspectionLocations)
            + (SELECT COUNT(*) FROM InspectionLocationPrincipalLinks) + (SELECT COUNT(*) FROM ActionHistory)
            + (SELECT COUNT(*) FROM OrganizationAdministrationOperations)
        """);

    private static async Task<Guid> SeedStaffAsync(LocalDbTestDatabase database, StaffRole role, bool enabled)
    {
        var staffId = Guid.NewGuid();
        await using var context = await database.CreateContextAsync();
        var roleName = role.ToString();
        var normalizedRoleName = roleName.ToUpperInvariant();
        var identityRole = await context.Roles.SingleOrDefaultAsync(item => item.NormalizedName == normalizedRoleName);
        if (identityRole is null)
        {
            identityRole = new IdentityRole<Guid>
            {
                Id = Guid.NewGuid(),
                Name = roleName,
                NormalizedName = normalizedRoleName,
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
            context.Roles.Add(identityRole);
        }

        var userName = $"loader-{staffId:N}";
        context.Users.Add(new PegasusIdentityUser
        {
            Id = staffId,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            IsEnabled = enabled,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        });
        context.UserRoles.Add(new IdentityUserRole<Guid> { UserId = staffId, RoleId = identityRole.Id });
        await context.SaveChangesAsync();
        return staffId;
    }

    private static SaveContactRequest NewContact(ContactRole role, string name, string? code = null) => new(
        Reader,
        Guid.NewGuid(),
        0,
        name,
        null,
        null,
        null,
        null,
        null,
        true,
        role,
        code,
        CaseInspectionMode.PhysicalAddress,
        [],
        Guid.NewGuid().ToString("N"));

    private static async Task<ContactDirectoryRecord> SaveContactAsync(LocalDbTestDatabase database, SaveContactRequest request)
    {
        await using var scope = database.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IContactDirectoryAdministration>()
            .SaveAsync(request, CancellationToken.None);
    }

    private static async Task<ContactDirectoryRecord?> GetContactAsync(LocalDbTestDatabase database, Guid organizationId)
    {
        await using var scope = database.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IContactDirectoryQueries>()
            .GetAsync(Reader, organizationId, CancellationToken.None);
    }

    private static (string File, Dictionary<string, string> Cells) Row(
        string file,
        string rowId,
        string name,
        string action,
        params (string Column, string Value)[] cells)
    {
        var row = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ReferenceFiles.RowId] = rowId,
            [ReferenceFiles.Name] = name,
            [ReferenceFiles.Action] = action,
            [ReferenceFiles.Active] = "true",
            [ReferenceFiles.Review] = ReferenceFiles.Approved
        };
        foreach (var (column, value) in cells)
        {
            row[column] = value;
        }

        return (file, row);
    }

    private static (string File, Dictionary<string, string> Cells) PrincipalRow(
        string rowId,
        string name,
        string code,
        string action,
        string active,
        string mode,
        params (string Column, string Value)[] cells) =>
        Row(
            ReferenceFiles.Principals,
            rowId,
            name,
            action,
            [(ReferenceFiles.PrincipalCode, code), (ReferenceFiles.Active, active), (ReferenceFiles.InspectionMode, mode), .. cells]);

    private static (string File, Dictionary<string, string> Cells) Link(string organisationRowId, string organisationKind, string code) =>
        (ReferenceFiles.Links, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ReferenceFiles.RowId] = $"{organisationRowId}/{code}",
            [ReferenceFiles.OrganisationRowId] = organisationRowId,
            [ReferenceFiles.OrganisationKind] = organisationKind,
            [ReferenceFiles.PrincipalCode] = code,
            [ReferenceFiles.LinkableAtLoad] = ReferenceFiles.Yes,
            [ReferenceFiles.Review] = ReferenceFiles.Approved
        });

    private static (string File, Dictionary<string, string> Cells) Location(
        string rowId,
        string name,
        string fullAddress,
        string postcode,
        string codes) =>
        Row(
            ReferenceFiles.InspectionLocations,
            rowId,
            name,
            ReferenceFiles.LocationAdd,
            (ReferenceFiles.FullAddress, fullAddress),
            (ReferenceFiles.Postcode, postcode),
            (ReferenceFiles.PrincipalCodes, codes));

    private static string Quote(string value) =>
        value.IndexOfAny(QuotedCharacters) >= 0
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;

    /// <summary>A temporary folder holding an <c>artifacts</c> segment, as the loader requires.</summary>
    private sealed class LoadRoot : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "pegasus-reference-load-" + Guid.NewGuid().ToString("N"));

        public string Report(string name) => Path.Combine(Root, "artifacts", "reports", name + ".json");

        /// <summary>Every load file, with its header and the given rows, as UTF-8 with a BOM and CRLF records.</summary>
        public string Write(params (string File, Dictionary<string, string> Cells)[] rows)
        {
            var folder = Path.Combine(Root, "artifacts", "load");
            Directory.CreateDirectory(folder);
            foreach (var (file, columns) in ReferenceFiles.AllFiles())
            {
                var lines = new List<string> { string.Join(',', columns) };
                lines.AddRange(rows
                    .Where(row => row.File == file)
                    .Select(row => string.Join(',', columns.Select(column => Quote(row.Cells.GetValueOrDefault(column, string.Empty))))));
                File.WriteAllText(
                    Path.Combine(folder, file),
                    string.Join("\r\n", lines) + "\r\n",
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            }

            return folder;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}

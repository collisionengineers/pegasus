using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Vehicle;
using Pegasus.Web.Authentication;
using Pegasus.Infrastructure.Persistence;
using Xunit.Abstractions;
using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Reproduces the pre-v1 incident through the composed Web host. The accepted
/// registration stays a Fact while the Case workspace records a corrected Make.
/// Details, the Cases queue and Search must all remain readable afterwards.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseVehicleSaveWebTests(ITestOutputHelper output)
{
    [Fact]
    public async Task SavingThenClearingMakeKeepsAcceptedRegistrationAndCaseSurfacesReadable()
    {
        using var factory = new IntakeWebApplicationFactory(
            useIntegrationTestAuthentication: true);
        await AllocationTestData.SeedPrincipalAsync(factory.Services, QdosPrincipal.Code);
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            QdosPrincipal.Code);
        await SeedAcceptedWorkspaceValuesAsync(factory.Services, receipt.Id);
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await scope.ServiceProvider.GetRequiredService<IAcceptIntake>()
            .ExecuteAsync(
                new(
                    receipt.Id,
                    0,
                    actor,
                    "accept-case-vehicle-save-web",
                    CaseType.Inspection,
                    QdosPrincipal.Code,
                    new(true, true),
                    AcceptedInspectionDeadline: new DateOnly(2031, 5, 20)),
                CancellationToken.None);
        var caseId = accepted.Identity.CaseId;

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        var available = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        using (var claim = await client.PostAsync(
                   $"/Cases/{caseId:D}?handler=ClaimLease",
                   Form(
                       AntiforgeryValue(available),
                       ("id", caseId.ToString("D")),
                       ("expectedVersion", InputValue(available, "expectedVersion")),
                       ("operationKey", InputValue(available, "operationKey")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, claim.StatusCode);
        }

        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.Equal("AB12CDE", InputValue(editing, "vehicleRegistration"));
        Assert.Equal(string.Empty, InputValue(editing, "vehicleMake"));
        var before = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.NotNull(before);
        Assert.Equal("Jane Example", before!.Claimant.Name.Fact?.Value);
        Assert.Equal("QDOS-123", before.Claim.Number.Fact?.Value);
        Assert.Equal(CaseDataSourceKind.IntakeEvidence, before.Claimant.Name.Fact?.Source.Kind);
        Assert.Equal(CaseDataSourceKind.IntakeEvidence, before.Claim.Number.Fact?.Source.Kind);
        using (var save = await client.PostAsync(
                   $"/Cases/{caseId:D}?handler=Save",
                   Form(
                       AntiforgeryValue(editing),
                       CurrentCaseSaveValues(editing, caseId))))
        {
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }

        var clearing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.Contains("data-case-editing=\"true\"", clearing, StringComparison.Ordinal);
        Assert.Equal("Ford", InputValue(clearing, "vehicleMake"));
        using (var clear = await client.PostAsync(
                   $"/Cases/{caseId:D}?handler=Save",
                   Form(
                       AntiforgeryValue(clearing),
                       CurrentCaseSaveValues(
                           clearing,
                           caseId,
                           vehicleMake: string.Empty,
                           reason: "Cleared an incorrectly recorded vehicle make."))))
        {
            Assert.Equal(HttpStatusCode.Redirect, clear.StatusCode);
        }

        var unchanged = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.Contains("data-case-editing=\"true\"", unchanged, StringComparison.Ordinal);
        Assert.Equal(string.Empty, InputValue(unchanged, "vehicleMake"));
        using (var save = await client.PostAsync(
                   $"/Cases/{caseId:D}?handler=Save",
                   Form(
                       AntiforgeryValue(unchanged),
                       CurrentCaseSaveValues(
                           unchanged,
                           caseId,
                           vehicleMake: string.Empty,
                           reason: "Recorded vehicle facts remain unchanged."))))
        {
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }

        var details = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.Contains("AB12CDE", details, StringComparison.Ordinal);
        var vehicle = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=vehicle");
        Assert.Contains("AB12CDE", vehicle, StringComparison.Ordinal);

        var cases = await GetHtmlAsync(client, $"/Cases?tab=review&selected={caseId:D}");
        Assert.Contains(caseId.ToString("D"), cases, StringComparison.Ordinal);

        var search = await GetHtmlAsync(client, "/Search?registration=AB12CDE");
        Assert.Contains(caseId.ToString("D"), search, StringComparison.Ordinal);

        var data = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        var evidence = await scope.ServiceProvider.GetRequiredService<IVehicleEvidenceQueries>()
            .GetAsync(caseId, CancellationToken.None);
        Assert.NotNull(data);
        Assert.Equal(before.Claimant, data!.Claimant);
        Assert.Equal(before.Claim, data.Claim);
        Assert.Equal(before.Accident, data.Accident);
        Assert.Equal(before.Instruction, data.Instruction);
        Assert.Equal(before.Inspection, data.Inspection);
        Assert.Equal("AB12CDE", data!.Vehicle.Registration.Fact?.Value);
        Assert.Null(data.Vehicle.Registration.Confirmed);
        Assert.Equal(before.Vehicle.Model, data.Vehicle.Model);
        Assert.Equal(before.Vehicle.Mileage, data.Vehicle.Mileage);
        Assert.Equal(before.Vehicle.MileageUnit, data.Vehicle.MileageUnit);
        Assert.Null(data.Vehicle.Make.Confirmed);
        Assert.NotNull(evidence);
        Assert.Null(evidence!.Confirmed);
    }

    /// <summary>
    /// What the Case page costs in SQL commands in an edit session, and what one
    /// accepted single-field save costs, against the real stores (Roadmap Lane
    /// D, parts D2 and D5). Every save-as-you-go commit is that save followed by
    /// the page, so both are the price of one field. Both counts are ceilings,
    /// so growth in either is seen, and a failure lists every command sent.
    /// </summary>
    [Fact]
    public async Task TheCasePageInAnEditSessionAndOneAcceptedSaveSendTheirPinnedStatements()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, useIntegrationTestAuthentication: true, commandInterceptor: counter);
        var caseId = await AcceptCaseAsync(
            factory, "accept-case-statement-count", withMileage: false);
        using var client = IntakeWebDriver.CreateClient(factory);
        await ClaimLeaseAsync(client, caseId, await GetHtmlAsync(client, $"/Cases/{caseId:D}"));
        // The first render of each shape pays one-off work; the counts are later ones.
        _ = await GetHtmlAsync(client, $"/Cases/{caseId:D}");

        counter.Reset();
        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        var pageCommands = counter.Count;
        var pageDescription = counter.Describe();
        var aiJobReads = counter.CountMentioning("[AiJobs]");
        var vehicleObservationReads = counter.CountMentioning("[VehicleLookupObservations]");
        Assert.Contains("data-case-editing=\"true\"", editing, StringComparison.Ordinal);

        counter.Reset();
        using (var save = await client.PostAsync(
                   $"/Cases/{caseId:D}?handler=Save",
                   Form(
                       AntiforgeryValue(editing),
                       CurrentCaseSaveValues(
                           editing,
                           caseId,
                           vehicleMake: "Vauxhall",
                           reason: "Statement count fixture."))))
        {
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }
        var saveCommands = counter.Count;
        var saveDescription = counter.Describe();
        // The save keeps the session's lease and issues its next token in its own
        // transaction (R770): no claim follows it, so no lease operation is written
        // and the Case's workflow row is locked once, by the save.
        var leaseOperationWrites = counter.CountContaining("INSERT", "[CaseEditLeaseOperations]");
        var workflowLocks = counter.CountContaining("[CaseWorkflows] WITH (UPDLOCK");

        Assert.True(
            pageCommands <= CasePageCommands,
            $"The Case page in an edit session sent {pageCommands} SQL commands; at most {CasePageCommands} are allowed."
            + Environment.NewLine + pageDescription);
        Assert.Equal(1, aiJobReads);
        Assert.Equal(1, vehicleObservationReads);
        Assert.True(
            saveCommands <= CaseSaveCommands,
            $"One accepted save sent {saveCommands} SQL commands; at most {CaseSaveCommands} are allowed."
            + Environment.NewLine + saveDescription);
        Assert.True(
            leaseOperationWrites == 0,
            $"A session save wrote {leaseOperationWrites} lease operations; it claims nothing after it."
            + Environment.NewLine + saveDescription);
        Assert.True(
            workflowLocks <= 1,
            $"A session save locked the Case's workflow {workflowLocks} times; the save's own lock is the only one."
            + Environment.NewLine + saveDescription);
    }

    /// <summary>
    /// 37 before #1950. The measured GET is the second under a lease, so the
    /// reference data the page reads is already kept: the labour-rate cards,
    /// the unroadworthy bank, the sign-off list, the Claim Source list, the
    /// Repairer list and the Send to AI switch (six statements) are no longer
    /// sent, so 37 - 6 = 31. It is a ceiling; if a reference read still runs,
    /// the failure's <see cref="SqlStatementCounter.Describe"/> output in CI
    /// names it and corrects this figure.
    /// </summary>
    private const int CasePageCommands = 31;

    /// <summary>
    /// #1950: a read-only open of a Case sends no reference read once the first
    /// open has kept them. The fixture's default sign-off Engineer makes the
    /// page resolve a signatory (the Case names none, so the resolver takes the
    /// default), so the cold open reads the sign-off list and that Engineer's
    /// signature; the warm open reads neither, nor anything else that names the
    /// signature column.
    /// </summary>
    [Fact]
    public async Task TheReadOnlyCasePageReadsNoReferenceDataOnceWarm()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, useIntegrationTestAuthentication: true, commandInterceptor: counter);
        var caseId = await AcceptCaseAsync(
            factory, "accept-case-read-only-warm", withMileage: false);
        await SeedDefaultSignOffEngineerAsync(factory.Services);
        using var client = IntakeWebDriver.CreateClient(factory);

        counter.Reset();
        _ = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        var coldSignatureMentions = counter.CountMentioning("[SignOffSignature]");
        var coldDescription = counter.Describe();

        counter.Reset();
        var readOnly = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        var warmCommands = counter.Count;
        var warmDescription = counter.Describe();
        var warmSignatureMentions = counter.CountMentioning("[SignOffSignature]");

        Assert.True(
            coldSignatureMentions >= 1,
            "The cold Case page read neither the sign-off list nor the signature." + Environment.NewLine + coldDescription);
        Assert.DoesNotContain("data-case-editing=\"true\"", readOnly, StringComparison.Ordinal);
        Assert.True(
            warmCommands <= ReadOnlyCasePageCommands,
            $"The warm read-only Case page sent {warmCommands} SQL commands; at most {ReadOnlyCasePageCommands} are allowed."
            + Environment.NewLine + warmDescription);
        Assert.Equal(0, warmSignatureMentions);
    }

    /// <summary>
    /// A ceiling set by arithmetic, not measured: the edit-session figure
    /// (<see cref="CasePageCommands"/>, 31), less the Claim Source and Repairer
    /// lists the read-only page does not read (29), plus one for the signature
    /// read the fixture's sign-off Engineer adds to a cold page (30). The first
    /// CI run's <see cref="SqlStatementCounter.Describe"/> output corrects it.
    /// </summary>
    private const int ReadOnlyCasePageCommands = 30;

    /// <summary>
    /// #1950: the Repairer list the edit-session page reads is one statement
    /// whatever its length. Each seed goes through the contact writer, which
    /// evicts the kept list, so both measured opens read it cold and growth
    /// with the rows would show; the open after them reads it kept.
    /// </summary>
    [Fact]
    public async Task TheCasePageInAnEditSessionReadsTheSameForThreeRepairersAndSix()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, useIntegrationTestAuthentication: true, commandInterceptor: counter);
        var caseId = await AcceptCaseAsync(
            factory, "accept-case-repairer-rows", withMileage: false);
        using var client = IntakeWebDriver.CreateClient(factory);
        await ClaimLeaseAsync(client, caseId, await GetHtmlAsync(client, $"/Cases/{caseId:D}"));
        // The first render of each shape pays one-off work; the counts are later ones.
        _ = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Guid[] repairerIds = [.. Enumerable.Range(0, 6).Select(_ => Guid.NewGuid())];

        await SeedRepairersAsync(factory.Services, repairerIds[..3]);
        counter.Reset();
        _ = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        var withThree = counter.Count;
        var withThreeDescription = counter.Describe();

        await SeedRepairersAsync(factory.Services, repairerIds[3..]);
        counter.Reset();
        var withSixHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        var withSix = counter.Count;
        var withSixDescription = counter.Describe();

        counter.Reset();
        _ = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        var warm = counter.Count;
        var warmDescription = counter.Describe();

        Assert.True(
            withThree == withSix,
            $"The Case page grew with the Repairer rows: {withThree} commands for three and {withSix} for six."
            + Environment.NewLine + withThreeDescription
            + Environment.NewLine + "---" + Environment.NewLine + withSixDescription);
        var repairerSelect = Regex.Match(
            withSixHtml,
            "<select id=\"repairer-directory\"[^>]*>.*?</select>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(repairerSelect.Success, "The Case page in an edit session must render the Repairer select.");
        foreach (var repairerId in repairerIds)
        {
            Assert.Contains($"value=\"{repairerId:D}\"", repairerSelect.Value, StringComparison.Ordinal);
        }
        Assert.True(
            warm < withSix,
            $"The Case page sent {warm} SQL commands with the directory lists kept, no fewer than the {withSix} it sent reading them."
            + Environment.NewLine + warmDescription);
    }

    private static async Task SeedRepairersAsync(IServiceProvider services, IEnumerable<Guid> repairerIds)
    {
        await using var scope = services.CreateAsyncScope();
        var administration = scope.ServiceProvider.GetRequiredService<IContactDirectoryAdministration>();
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        foreach (var repairerId in repairerIds)
        {
            var name = $"Repairer Rows Fixture {repairerId:N}";
            await administration.SaveAsync(
                new(
                    actor, repairerId, 0, name, null, null, null, null, null, true, ContactRole.Repairer, null,
                    CaseInspectionMode.PhysicalAddress, [], $"case-repairer-rows:{repairerId:N}"),
                CancellationToken.None);
        }
    }

    /// <summary>
    /// One enabled default sign-off Engineer with a signature (the PNG
    /// signature bytes), written straight to the database so no account
    /// writer evicts anything.
    /// </summary>
    private static async Task SeedDefaultSignOffEngineerAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var engineerRole = await context.Roles.SingleAsync(role => role.NormalizedName == "ENGINEER");
        var staffId = Guid.NewGuid();
        var signature = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
        context.Users.Add(new PegasusIdentityUser
        {
            Id = staffId,
            UserName = $"read-only-signatory-{staffId:N}",
            NormalizedUserName = $"READ-ONLY-SIGNATORY-{staffId:N}",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            IsEnabled = true,
            IsSignOffEngineer = true,
            IsDefaultSignOffEngineer = true,
            SignOffPrintedName = "Read Only Signatory",
            SignOffSignature = signature,
            SignOffSignatureDigest = Convert.ToHexStringLower(SHA256.HashData(signature))
        });
        context.UserRoles.Add(new IdentityUserRole<Guid> { UserId = staffId, RoleId = engineerRole.Id });
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// 27 before #1951 (B3-04 brought it from 30 to 26, and the R466 note's
    /// session read made it 27). The measured POST has no script header, so
    /// it is the save and what used to follow it. Now the committed save
    /// projects its answer from the rows it tracks, so its three post-commit
    /// re-reads (the snapshot, the workflow and the assessment fields; this
    /// fixture posts no estimate, so there is no estimate re-read) are gone:
    /// -3. Core passes the sign-off Engineers in, a kept list (#1950), so the
    /// store no longer reads them under its lock: -1. The save keeps the
    /// session's lease and issues its next token itself (R770), so the reclaim
    /// that followed (the page's workflow read, the claim transaction's lock,
    /// its workflow read, two lease-operation replay probes and its write) is
    /// gone: -6. 27 - 3 - 1 - 6 = 17. It is a ceiling; the failure's
    /// <see cref="SqlStatementCounter.Describe"/> output in CI names every
    /// command and corrects this figure.
    /// </summary>
    private const int CaseSaveCommands = 17;

    /// <summary>
    /// What a spec tab, New repair spec or Compare switch costs in SQL commands
    /// in an edit session. The script fetches the Repair Spec fragment alone
    /// (R304) on the narrow section frame, so the count is a ceiling well under
    /// the Case page's (13 against 38 for the same request through the full
    /// page), and that request is measured beside it. A failure lists every
    /// command sent.
    /// </summary>
    [Fact]
    public async Task TheEstimateFragmentInAnEditSessionSendsFewerStatementsThanTheCasePage()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, useIntegrationTestAuthentication: true, commandInterceptor: counter);
        var caseId = await AcceptCaseAsync(
            factory, "accept-case-estimate-fragment", withMileage: false);
        using var client = IntakeWebDriver.CreateClient(factory);
        await ClaimLeaseAsync(client, caseId, await GetHtmlAsync(client, $"/Cases/{caseId:D}"));
        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.Contains("data-case-editing=\"true\"", editing, StringComparison.Ordinal);
        var lease = InputValue(editing, "editLeaseToken");
        var query = "section=estimate&estimate=new";

        async Task<string> FragmentAsync()
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"/Cases/{caseId:D}/Section?{query}");
            request.Headers.Add("X-Pegasus-Edit-Lease", lease);
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, body);
            return body;
        }

        // The first render of each shape pays one-off work; the counts are later ones.
        _ = await GetHtmlAsync(client, $"/Cases/{caseId:D}?{query}");
        _ = await FragmentAsync();

        counter.Reset();
        _ = await GetHtmlAsync(client, $"/Cases/{caseId:D}?{query}");
        var pageCommands = counter.Count;

        counter.Reset();
        var fragment = await FragmentAsync();
        var fragmentCommands = counter.Count;
        var fragmentDescription = counter.Describe();
        output.WriteLine($"Estimate fragment: {fragmentCommands} commands; the Case page: {pageCommands}.");

        Assert.Contains("id=\"section-estimate\"", fragment, StringComparison.Ordinal);
        Assert.True(
            fragmentCommands <= EstimateFragmentCommands,
            $"The Repair Spec fragment in an edit session sent {fragmentCommands} SQL commands; at most {EstimateFragmentCommands} are allowed."
            + Environment.NewLine + fragmentDescription);
        Assert.True(
            fragmentCommands < pageCommands,
            $"The Repair Spec fragment sent {fragmentCommands} SQL commands, no fewer than the Case page's {pageCommands}.");
    }

    private const int EstimateFragmentCommands = 13;

    /// <summary>
    /// Roadmap Lane H (FRD-16): a commit the page script posts is answered with
    /// the parts it swaps, not with a redirect to the whole page. Three commits
    /// walk the real stores the way the script does: each carries the version,
    /// lease and operation key the answer before it returned, so an answer that
    /// carried the wrong authority fails the next save. The first two are
    /// answered in place; the third has no script header and keeps its redirect,
    /// which prices what the script used to follow. The answered commit's count
    /// is a ceiling, so growth is seen, and a failure lists every command sent.
    /// The byte figures are printed.
    /// </summary>
    [Fact]
    public async Task ACommitAnsweredInPlaceCarriesTheNextAuthorityAndCostsLessThanTheRedirectAndPage()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, useIntegrationTestAuthentication: true, commandInterceptor: counter);
        var caseId = await AcceptCaseAsync(
            factory, "accept-case-commit-answer", withMileage: false);
        using var client = IntakeWebDriver.CreateClient(factory);
        await ClaimLeaseAsync(client, caseId, await GetHtmlAsync(client, $"/Cases/{caseId:D}"));
        // The first render of each shape pays one-off work; the counts are later ones.
        _ = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        var startVersion = long.Parse(InputValue(editing, "expectedVersion"), CultureInfo.InvariantCulture);

        // The script keeps every section as it is and takes only the authority from an answer.
        counter.Reset();
        var first = await CommitAsync(
            client, editing, caseId, CurrentCaseSaveValues(editing, caseId, vehicleMake: "Vauxhall"), script: true);
        var firstCommands = counter.Count;
        var firstDescription = counter.Describe();
        var firstSnapshotReads = counter.CountContaining("FROM [CaseDataSnapshots]");
        Assert.Equal(HttpStatusCode.OK, first.Status);
        // The save kept the lease and issued its next token (R770); the answer carries that one.
        Assert.NotEqual(InputValue(editing, "editLeaseToken"), SaveFormValue(first.Body, "editLeaseToken"));
        Assert.True(
            firstSnapshotReads <= 3,
            $"An answered commit read the Case data snapshot {firstSnapshotReads} times; the edit basis, the save's"
            + " tracked read and the answer's workspace are the most allowed."
            + Environment.NewLine + firstDescription);
        var firstCommit = EditorCommit(first.Body);
        Assert.Equal(startVersion, firstCommit.ExpectedVersion);
        Assert.Equal(startVersion + 1, firstCommit.Version);
        Assert.Contains("data-case-editing=\"true\"", first.Body, StringComparison.Ordinal);
        Assert.Contains($"data-case-version=\"{startVersion + 1}\"", first.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"case-main\"", first.Body, StringComparison.Ordinal);

        counter.Reset();
        var second = await CommitAsync(
            client,
            editing,
            caseId,
            WithAuthority(CurrentCaseSaveValues(editing, caseId, vehicleMake: "Renault"), first.Body),
            script: true);
        Assert.Equal(HttpStatusCode.OK, second.Status);
        var secondCommit = EditorCommit(second.Body);
        Assert.Equal(startVersion + 1, secondCommit.ExpectedVersion);
        Assert.Equal(startVersion + 2, secondCommit.Version);

        counter.Reset();
        var third = await CommitAsync(
            client,
            editing,
            caseId,
            WithAuthority(CurrentCaseSaveValues(editing, caseId, vehicleMake: "Skoda"), second.Body),
            script: false);
        var plainPostCommands = counter.Count;
        Assert.Equal(HttpStatusCode.Redirect, third.Status);
        counter.Reset();
        var page = await GetHtmlAsync(client, third.Location!);
        var pageCommands = counter.Count;
        Assert.Contains("Case saved.", page, StringComparison.Ordinal);
        Assert.Equal((startVersion + 3).ToString(CultureInfo.InvariantCulture), SaveFormValue(page, "expectedVersion"));

        await using var scope = factory.Services.CreateAsyncScope();
        var data = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.Equal("Skoda", data!.Vehicle.Make.Confirmed?.Value);

        var answerBytes = Encoding.UTF8.GetByteCount(first.Body);
        var pageBytes = Encoding.UTF8.GetByteCount(page);
        var redirectedCommands = plainPostCommands + pageCommands;
        output.WriteLine(
            $"Lane H: an answered commit sent {firstCommands} SQL commands and {answerBytes} bytes; "
            + $"the same commit with its redirect sent {redirectedCommands} ({plainPostCommands} + {pageCommands}) "
            + $"and {pageBytes} bytes.");
        Assert.True(
            firstCommands < redirectedCommands,
            $"An answered commit sent {firstCommands} SQL commands; the save and its redirected page send {redirectedCommands}.");
        Assert.True(
            firstCommands <= CaseSaveCommands + CommitAnswerCommands,
            $"An answered commit sent {firstCommands} SQL commands; at most {CaseSaveCommands + CommitAnswerCommands} are allowed"
            + $" (the save {CaseSaveCommands} and the answer {CommitAnswerCommands})."
            + Environment.NewLine + firstDescription);
        Assert.True(
            answerBytes < pageBytes * 6 / 10,
            $"The answer is {answerBytes} bytes; the page it replaces is {pageBytes}.");
    }

    /// <summary>
    /// The answer's own reads for this Case, after the save's <see cref="CaseSaveCommands"/>:
    /// the frame and the workspace, the report's readiness and current generation, the Case's
    /// AI jobs and the workflow configuration, the valuation opening, and the workspace extras.
    /// The report snapshot takes the Case's works from the frame, as the page's does. The
    /// frame includes the Next action's linked-cancellation read and the Linked cases card's
    /// linked-Triage read, one statement each. 28 before #1951: the answer hands the frame the
    /// Case data the save projected, so the frame no longer reads the data row (-1), and the
    /// report snapshot's sign-off list is a kept one since #1950 (-1; this fixture resolves no
    /// sign-off Engineer, so no signature is read): 28 - 2 = 26. It is a ceiling; CI's
    /// <see cref="SqlStatementCounter.Describe"/> output corrects it.
    /// </summary>
    private const int CommitAnswerCommands = 26;

    /// <summary>
    /// R770: a save keeps the session's lease and issues its next token, so the token
    /// it was made under is spent. A commit that still sends that token, at the
    /// version the save answered, is refused as a lost lease is: the refused shape
    /// with the shared refusal as its notice, and nothing is written to the Case.
    /// </summary>
    [Fact]
    public async Task ACommitThatStillSendsTheTokenTheSaveReplacedIsRefused()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, useIntegrationTestAuthentication: true, commandInterceptor: counter);
        var caseId = await AcceptCaseAsync(
            factory, "accept-case-replaced-session-token", withMileage: false);
        using var client = IntakeWebDriver.CreateClient(factory);
        await ClaimLeaseAsync(client, caseId, await GetHtmlAsync(client, $"/Cases/{caseId:D}"));
        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        var spentToken = InputValue(editing, "editLeaseToken");
        var startVersion = long.Parse(InputValue(editing, "expectedVersion"), CultureInfo.InvariantCulture);

        var first = await CommitAsync(
            client, editing, caseId, CurrentCaseSaveValues(editing, caseId, vehicleMake: "Vauxhall"), script: true);
        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(startVersion + 1, EditorCommit(first.Body).Version);
        Assert.NotEqual(spentToken, SaveFormValue(first.Body, "editLeaseToken"));

        // The answer's version and operation key, with the token the first page carried.
        (string Name, string Value)[] stale =
        [
            .. WithAuthority(CurrentCaseSaveValues(editing, caseId, vehicleMake: "Renault"), first.Body)
                .Select(field => field.Name == "editLeaseToken" ? (field.Name, spentToken) : field)
        ];
        counter.Reset();
        var refused = await CommitAsync(client, editing, caseId, stale, script: true);
        var workflowEventWrites = counter.CountContaining("INSERT", "[CaseWorkflowEvents]");
        var refusedDescription = counter.Describe();

        Assert.Equal(HttpStatusCode.OK, refused.Status);
        Assert.DoesNotMatch("data-editor-commit=\"[^\"]+\"", refused.Body);
        Assert.Contains("role=\"alert\"", refused.Body, StringComparison.Ordinal);
        Assert.Contains(
            "The case action was not applied because the case changed, edit mode was lost, or the action is not permitted.",
            refused.Body,
            StringComparison.Ordinal);
        Assert.Contains($"data-case-version=\"{startVersion + 1}\"", refused.Body, StringComparison.Ordinal);
        Assert.True(
            workflowEventWrites == 0,
            $"A commit under a spent token wrote {workflowEventWrites} workflow events."
            + Environment.NewLine + refusedDescription);

        await using var scope = factory.Services.CreateAsyncScope();
        var data = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.Equal("Vauxhall", data!.Vehicle.Make.Confirmed?.Value);
    }

    private static async Task<(HttpStatusCode Status, string Body, string? Location)> CommitAsync(
        HttpClient client,
        string antiforgeryHtml,
        Guid caseId,
        (string Name, string Value)[] fields,
        bool script)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Cases/{caseId:D}?handler=Save")
        {
            Content = Form(AntiforgeryValue(antiforgeryHtml), fields)
        };
        if (script)
        {
            request.Headers.Add("X-Requested-With", "fetch");
        }
        using var response = await client.SendAsync(request);
        return (
            response.StatusCode,
            await response.Content.ReadAsStringAsync(),
            response.Headers.Location?.OriginalString);
    }

    /// <summary>The Save form's own inputs, taken from an answer as the script's carry-forward takes them.</summary>
    private static (string Name, string Value)[] WithAuthority(
        (string Name, string Value)[] fields,
        string answer) =>
    [
        .. fields.Select(field => field.Name is "expectedVersion" or "operationKey" or "editLeaseToken"
            ? (field.Name, SaveFormValue(answer, field.Name))
            : field)
    ];

    private static string SaveFormValue(string html, string name)
    {
        var start = html.IndexOf("id=\"case-edit-form\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The answer must draw the Save form.");
        var end = html.IndexOf("</form>", start, StringComparison.Ordinal);
        return InputValue(html[start..end], name);
    }

    private static (string OperationKey, long ExpectedVersion, long Version) EditorCommit(string html)
    {
        var attribute = Regex.Match(html, "data-editor-commit=\"(?<value>[^\"]+)\"");
        Assert.True(attribute.Success, "The answer must confirm the commit.");
        using var json = JsonDocument.Parse(WebUtility.HtmlDecode(attribute.Groups["value"].Value));
        var commit = json.RootElement;
        return (
            commit.GetProperty("operationKey").GetString()!,
            commit.GetProperty("expectedVersion").GetInt64(),
            commit.GetProperty("version").GetInt64());
    }

    /// <summary>
    /// The record form defaults the unit to miles when the Case carries neither
    /// part of an odometer reading.
    /// </summary>
    [Fact]
    public async Task TypingAMileageSavesItInMiles()
    {
        using var factory = new IntakeWebApplicationFactory(
            useIntegrationTestAuthentication: true);
        var caseId = await AcceptCaseAsync(
            factory, "accept-case-vehicle-mileage-typed", withMileage: false);
        using var client = IntakeWebDriver.CreateClient(factory);
        await ClaimLeaseAsync(client, caseId, await GetHtmlAsync(client, $"/Cases/{caseId:D}"));

        await SaveMileageAsync(client, caseId, "51234", "Recorded the mileage read at inspection.");

        await using var scope = factory.Services.CreateAsyncScope();
        var data = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.NotNull(data);
        Assert.Equal(51234L, data!.Vehicle.Mileage.Confirmed?.Value);
        Assert.Equal("miles", data.Vehicle.MileageUnit.Confirmed?.Value);
    }

    [Fact]
    public async Task ChangingMileageUnitToKilometresDoesNotConvertTheMileageValue()
    {
        using var factory = new IntakeWebApplicationFactory(
            useIntegrationTestAuthentication: true);
        var caseId = await AcceptCaseAsync(
            factory, "accept-case-vehicle-mileage-kilometres", withMileage: false);
        using var client = IntakeWebDriver.CreateClient(factory);
        await ClaimLeaseAsync(client, caseId, await GetHtmlAsync(client, $"/Cases/{caseId:D}"));

        await SaveMileageAsync(client, caseId, "42000", "Recorded the mileage read at inspection.");
        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.Equal("miles", SelectValue(editing, "vehicleMileageUnit"));
        await SaveMileageAsync(
            client,
            caseId,
            "42000",
            "Changed the odometer unit to kilometres.",
            "kilometres");

        await using var scope = factory.Services.CreateAsyncScope();
        var data = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.NotNull(data);
        Assert.Equal(42000L, data!.Vehicle.Mileage.Confirmed?.Value);
        Assert.Equal("kilometres", data.Vehicle.MileageUnit.Confirmed?.Value);
    }

    /// <summary>
    /// Emptying the box clears the unit with it. A unit standing behind a
    /// mileage that is gone is half an odometer reading, and the case record
    /// refuses one.
    /// </summary>
    [Fact]
    public async Task ClearingAMileageRemovesItsUnitToo()
    {
        using var factory = new IntakeWebApplicationFactory(
            useIntegrationTestAuthentication: true);
        var caseId = await AcceptCaseAsync(
            factory, "accept-case-vehicle-mileage-cleared", withMileage: false);
        using var client = IntakeWebDriver.CreateClient(factory);
        await ClaimLeaseAsync(client, caseId, await GetHtmlAsync(client, $"/Cases/{caseId:D}"));
        await SaveMileageAsync(client, caseId, "51234", "Recorded the mileage read at inspection.");

        await SaveMileageAsync(
            client, caseId, string.Empty, "Removed a mileage recorded against the wrong vehicle.");

        await using var scope = factory.Services.CreateAsyncScope();
        var data = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.NotNull(data);
        Assert.Null(data!.Vehicle.Mileage.Confirmed);
        Assert.Null(data.Vehicle.Mileage.Fact);
        Assert.Null(data.Vehicle.MileageUnit.Confirmed);
        Assert.Null(data.Vehicle.MileageUnit.Fact);
    }

    /// <summary>
    /// A registration staff record on a Case that came from an instruction
    /// reads on the ribbon and is what Search finds the Case by: the confirmed
    /// value reads before the instruction draft's (MP26001-p, 12 November 2026).
    /// </summary>
    [Fact]
    public async Task ARegistrationStaffRecordReadsOnTheRibbonAndInSearch()
    {
        using var factory = new IntakeWebApplicationFactory(
            useIntegrationTestAuthentication: true);
        var caseId = await AcceptCaseAsync(
            factory, "accept-case-vehicle-registration-recorded", withMileage: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        await ClaimLeaseAsync(client, caseId, await GetHtmlAsync(client, $"/Cases/{caseId:D}"));
        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.Contains(">AB12CDE</span>", RibbonRegistrationCell(editing), StringComparison.Ordinal);

        using (var save = await client.PostAsync(
                   $"/Cases/{caseId:D}?handler=Save",
                   Form(
                       AntiforgeryValue(editing),
                       CurrentCaseSaveValues(
                           editing,
                           caseId,
                           vehicleMake: InputValue(editing, "vehicleMake"),
                           reason: "Corrected the registration from the vehicle's plate.",
                           vehicleRegistration: "CD34EFG"))))
        {
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }

        var saved = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.Contains(">CD34EFG</span>", RibbonRegistrationCell(saved), StringComparison.Ordinal);
        var search = await GetHtmlAsync(client, "/Search?registration=CD34EFG");
        Assert.Contains(caseId.ToString("D"), search, StringComparison.Ordinal);
    }

    private static string RibbonRegistrationCell(string html)
    {
        var start = html.IndexOf("<div class=\"ribbon-item ribbon-registration\">", StringComparison.Ordinal);
        Assert.True(start >= 0, "The ribbon has a Registration cell.");
        return html[start..html.IndexOf("<div class=\"ribbon-item\">", start, StringComparison.Ordinal)];
    }

    private static async Task<Guid> AcceptCaseAsync(
        IntakeWebApplicationFactory factory,
        string operationKey,
        bool withMileage)
    {
        await AllocationTestData.SeedPrincipalAsync(factory.Services, QdosPrincipal.Code);
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            QdosPrincipal.Code);
        await SeedAcceptedWorkspaceValuesAsync(factory.Services, receipt.Id, withMileage);
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await scope.ServiceProvider.GetRequiredService<IAcceptIntake>()
            .ExecuteAsync(
                new(
                    receipt.Id,
                    0,
                    ActionActor.Staff(
                        DevelopmentOfflineIdentity.AdministratorId,
                        [StaffRole.Administrator]),
                    operationKey,
                    CaseType.Inspection,
                    QdosPrincipal.Code,
                    new(true, true),
                    AcceptedInspectionDeadline: new DateOnly(2031, 5, 20)),
                CancellationToken.None);
        return accepted.Identity.CaseId;
    }

    private static async Task SaveMileageAsync(
        HttpClient client,
        Guid caseId,
        string mileage,
        string reason,
        string? mileageUnit = null)
    {
        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.Contains("data-case-editing=\"true\"", editing, StringComparison.Ordinal);
        using var save = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=Save",
            Form(
                AntiforgeryValue(editing),
                CurrentCaseSaveValues(
                    editing,
                    caseId,
                    vehicleMake: InputValue(editing, "vehicleMake"),
                    reason: reason,
                    vehicleMileage: mileage,
                    vehicleMileageUnit: mileageUnit)));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
    }

    [Fact]
    public async Task ASecondStaffClientCannotClaimOrSaveOverAnActiveVehicleEditor()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.PostConfigure<PolicySchemeOptions>(
                    "Pegasus",
                    options => options.ForwardDefaultSelector = static _ =>
                        IdentityConstants.ApplicationScheme)));
        await AllocationTestData.SeedPrincipalAsync(factory.Services, QdosPrincipal.Code);
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            QdosPrincipal.Code);
        await SeedAcceptedWorkspaceValuesAsync(factory.Services, receipt.Id);
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await scope.ServiceProvider.GetRequiredService<IAcceptIntake>()
            .ExecuteAsync(
                new(
                    receipt.Id,
                    0,
                    actor,
                    "accept-case-vehicle-two-editors",
                    CaseType.Inspection,
                    QdosPrincipal.Code,
                    new(true, true),
                    AcceptedInspectionDeadline: new DateOnly(2031, 5, 20)),
                CancellationToken.None);
        var caseId = accepted.Identity.CaseId;

        await CreateEngineerAsync(factory.Services, "vehicle-editor-one", "Password-1");
        await CreateEngineerAsync(factory.Services, "vehicle-editor-two", "Password-1");
        using var firstClient = await SignInAsync(factory, "vehicle-editor-one", "Password-1");
        using var secondClient = await SignInAsync(factory, "vehicle-editor-two", "Password-1");

        var firstAvailable = await GetHtmlAsync(firstClient, $"/Cases/{caseId:D}");
        var secondAvailable = await GetHtmlAsync(secondClient, $"/Cases/{caseId:D}");
        Assert.Contains("<strong>vehicle-editor-one</strong>", firstAvailable, StringComparison.Ordinal);
        Assert.Contains("<strong>vehicle-editor-two</strong>", secondAvailable, StringComparison.Ordinal);
        await ClaimLeaseAsync(firstClient, caseId, firstAvailable);
        var firstEditing = await GetHtmlAsync(firstClient, $"/Cases/{caseId:D}");

        using (var claim = await secondClient.PostAsync(
                   $"/Cases/{caseId:D}?handler=ClaimLease",
                   Form(
                       AntiforgeryValue(secondAvailable),
                       ("id", caseId.ToString("D")),
                       ("expectedVersion", InputValue(secondAvailable, "expectedVersion")),
                       ("operationKey", InputValue(secondAvailable, "operationKey")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, claim.StatusCode);
        }

        using (var overwrite = await secondClient.PostAsync(
                   $"/Cases/{caseId:D}?handler=Save",
                   Form(
                       AntiforgeryValue(secondAvailable),
                       CurrentCaseSaveValues(
                           firstEditing,
                           caseId,
                           vehicleMake: "Renault",
                           reason: "Attempted competing vehicle correction."))))
        {
            Assert.Equal(HttpStatusCode.Redirect, overwrite.StatusCode);
        }

        var beforeFirstSave = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.NotNull(beforeFirstSave);
        Assert.Null(beforeFirstSave!.Vehicle.Make.Confirmed);

        using (var firstSave = await firstClient.PostAsync(
                   $"/Cases/{caseId:D}?handler=Save",
                   Form(
                       AntiforgeryValue(firstEditing),
                       CurrentCaseSaveValues(
                           firstEditing,
                           caseId,
                           vehicleMake: "Ford"))))
        {
            Assert.Equal(HttpStatusCode.Redirect, firstSave.StatusCode);
        }

        var saved = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal("Ford", saved!.Vehicle.Make.Confirmed?.Value);
        Assert.Equal("AB12CDE", saved.Vehicle.Registration.Fact?.Value);
    }

    private static async Task SeedAcceptedWorkspaceValuesAsync(
        IServiceProvider services,
        Guid receiptId,
        bool withMileage = true)
    {
        var fields = new[]
        {
            Field("Claimant name", "Jane Example"),
            Field("Claim number", "QDOS-123"),
            Field("Vehicle registration", "AB12CDE"),
            Field("Vehicle model", "Focus"),
            Field("Vehicle mileage", "42000"),
            Field("Vehicle mileage unit", "miles"),
            Field("Accident circumstances", "Rear-end impact at a roundabout."),
            Field("Incident date", "2031-04-01"),
            Field("Inspection date", "2031-05-20"),
            Field("Inspection address", "1 Test Street, London"),
            Field("Claimant contact number", "07700 900123"),
            Field("Claimant address", "12 Example Street, Leeds, LS1 1AA"),
            Field("VAT status", "VAT registered")
        };
        if (!withMileage)
        {
            fields = [.. fields.Where(field =>
                !field.Name.StartsWith("Vehicle mileage", StringComparison.Ordinal))];
        }

        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var draft = await context.InstructionDrafts.SingleAsync(item => item.IntakeReceiptId == receiptId);
        draft.ClaimantName = "Jane Example";
        draft.ClaimNumber = "QDOS-123";
        draft.VehicleModel = "Focus";
        draft.VehicleMileage = withMileage ? 42000 : null;
        draft.VehicleMileageUnit = withMileage ? "miles" : null;
        draft.AccidentCircumstances = "Rear-end impact at a roundabout.";
        draft.DateOfIncident = new DateOnly(2031, 4, 1);
        draft.InspectionDate = new DateOnly(2031, 5, 20);
        draft.InspectionAddress = "1 Test Street, London";
        draft.ClaimantContactNumber = "07700 900123";
        draft.ClaimantAddress = "12 Example Street, Leeds, LS1 1AA";
        draft.VatStatus = "VAT registered";
        var receipt = await context.IntakeReceipts.SingleAsync(item => item.Id == receiptId);
        receipt.FieldsJson = EfIntakeReceiptStore.SerializeFields(fields);
        await context.SaveChangesAsync();
    }

    private static InstructionReviewField Field(string name, string value) => new(
        name,
        value,
        [new(value, IntakeEvidenceSource.DocumentContent, "retained vehicle-save test instruction")],
        IsDefaulted: false,
        HasConflict: false);

    private static (string Name, string Value)[] CurrentCaseSaveValues(
        string html,
        Guid caseId,
        string vehicleMake = "Ford",
        string reason = "Corrected vehicle make from retained instruction.",
        string? vehicleMileage = null,
        string? vehicleMileageUnit = null,
        string? vehicleRegistration = null) =>
    [
        ("id", caseId.ToString("D")),
        ("expectedVersion", InputValue(html, "expectedVersion")),
        ("operationKey", InputValue(html, "operationKey")),
        ("editLeaseToken", InputValue(html, "editLeaseToken")),
        ("reason", reason),
        ("claimantName", InputValue(html, "claimantName")),
        ("claimantContactNumber", InputValue(html, "claimantContactNumber")),
        ("claimantAddress", InputValue(html, "claimantAddress")),
        ("claimNumber", InputValue(html, "claimNumber")),
        ("vehicleRegistration", vehicleRegistration ?? InputValue(html, "vehicleRegistration")),
        ("vehicleMake", vehicleMake),
        ("vehicleModel", InputValue(html, "vehicleModel")),
        ("vehicleMileage", vehicleMileage ?? InputValue(html, "vehicleMileage")),
        ("vehicleMileageUnit", vehicleMileageUnit ?? SelectValue(html, "vehicleMileageUnit")),
        ("accidentCircumstances", TextareaValue(html, "accidentCircumstances")),
        ("incidentDate", InputValue(html, "incidentDate")),
        ("dueBy", InputValue(html, "dueBy")),
        ("claimSourceContactName", InputValue(html, "claimSourceContactName")),
        ("claimSourceContactTelephone", InputValue(html, "claimSourceContactTelephone")),
        ("claimSourceContactEmail", InputValue(html, "claimSourceContactEmail")),
        ("vatStatus", InputValue(html, "vatStatus")),
        ("inspectionDate", InputValue(html, "inspectionDate")),
        ("inspectionDeadline", InputValue(html, "inspectionDeadline")),
        ("inspectionAddress", InputValue(html, "inspectionAddress")),
        ("inspectionMode", InputValue(html, "inspectionMode")),
        ("storageLocation", InputValue(html, "storageLocation"))
    ];

    private static async Task ClaimLeaseAsync(HttpClient client, Guid caseId, string html)
    {
        using var claim = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(html),
                ("id", caseId.ToString("D")),
                ("expectedVersion", InputValue(html, "expectedVersion")),
                ("operationKey", InputValue(html, "operationKey"))));
        Assert.Equal(HttpStatusCode.Redirect, claim.StatusCode);
    }

    private static async Task<HttpClient> SignInAsync(
        WebApplicationFactory<Program> factory,
        string userName,
        string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });
        var signIn = await GetHtmlAsync(client, "/Account/SignIn");
        using var response = await client.PostAsync(
            "/Account/SignIn",
            Form(
                AntiforgeryValue(signIn),
                ("UserName", userName),
                ("Password", password)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-Pegasus=", StringComparison.Ordinal));
        return client;
    }

    private static async Task CreateEngineerAsync(
        IServiceProvider services,
        string userName,
        string password)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var user = new PegasusIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            IsEnabled = true,
            MustChangePassword = false
        };
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, StaffRole.Engineer.ToString())).Succeeded);
    }

    private static FormUrlEncodedContent Form(
        string antiforgeryToken,
        params (string Name, string Value)[] values) =>
        new(values
            .Select(value => KeyValuePair.Create(value.Name, value.Value))
            .Append(KeyValuePair.Create("__RequestVerificationToken", antiforgeryToken)));

    private static string InputValue(string html, string name)
    {
        var input = Regex.Match(
            html,
            $"<input[^>]*name=\\\"{Regex.Escape(name)}\\\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(input.Success, $"The Case form must render '{name}'.");
        var value = Regex.Match(
            input.Value,
            "value=\\\"(?<value>[^\\\"]*)\\\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return value.Success
            ? WebUtility.HtmlDecode(value.Groups["value"].Value)
            : string.Empty;
    }

    private static string SelectValue(string html, string name)
    {
        var select = Regex.Match(
            html,
            $"<select[^>]*name=\\\"{Regex.Escape(name)}\\\"[^>]*>.*?</select>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        Assert.True(select.Success, $"The Case form must render '{name}' as a select.");
        var selectedOption = Regex.Match(
            select.Value,
            "<option[^>]*\\sselected(?:=\\\"[^\\\"]*\\\")?[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(selectedOption.Success, $"The Case form select '{name}' must have a selected option.");
        var value = Regex.Match(
            selectedOption.Value,
            "value=\\\"(?<value>[^\\\"]*)\\\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(value.Success, $"The selected option for '{name}' must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private static string TextareaValue(string html, string name)
    {
        var textarea = Regex.Match(
            html,
            $"<textarea[^>]*name=\\\"{Regex.Escape(name)}\\\"[^>]*>(?<value>.*?)</textarea>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        Assert.True(textarea.Success, $"The Case form must render '{name}'.");
        return WebUtility.HtmlDecode(textarea.Groups["value"].Value);
    }

    private static string AntiforgeryValue(string html)
    {
        var value = InputValue(html, "__RequestVerificationToken");
        Assert.False(string.IsNullOrEmpty(value), "The Case antiforgery token must have a value.");
        return value;
    }
}

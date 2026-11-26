using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class OrganizationAdministrationPersistenceTests
{
    private static readonly ActionActor Administrator = ActionActor.Staff(
        Guid.Parse("0f149cac-e1d4-4a57-925f-7c35d33d7f5b"),
        [StaffRole.Administrator]);
    private static readonly DateTimeOffset RecordedAtUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExistingPrincipalSettingsAdvanceAndCheckTheOwningContactVersion()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var principal = await context.Principals.AsNoTracking()
            .Where(item => item.Code == QdosPrincipal.Code)
            .Select(item => new { item.Id, item.Version, item.OrganizationId, ContactVersion = item.Organization.Version })
            .SingleAsync();
        var update = services.GetRequiredService<IUpdatePrincipalReportSettings>();

        UpdatePrincipalReportSettingsRequest Request(long principalVersion, long contactVersion, string operationKey) => new(
            principal.Id,
            principalVersion,
            Administrator,
            operationKey,
            "Confirm contact-owned report settings",
            false,
            contactVersion,
            new PrincipalReportNames(null, null, " {REG}_{CASE REF}_AUDIT ", null),
            " GB 123 4567 89 ",
            AuditWorkflowKind.GarageAuthorisationAndInvoice,
            PrincipalInvoiceKind.UlaInvoice,
            " GB 987 6543 21 ",
            " 30 days ");

        var updated = await update.ExecuteAsync(
            Request(principal.Version, principal.ContactVersion, "principal:report-settings:direct"), default);
        Assert.Equal(principal.Version + 1, updated.Version);
        // Report images is saved with the report settings (R1267).
        Assert.False(updated.ReportImages);
        // The report names are saved with them, trimmed (R740).
        Assert.Equal(new PrincipalReportNames(null, null, "{REG}_{CASE REF}_AUDIT", null), updated.ReportNames);
        Assert.Equal("{REG}_{CASE REF}_AUDIT", await (await factory.DatabaseAsync).ScalarAsync<string>(
            $"SELECT AuditReportNameFirst FROM Principals WHERE Id = '{principal.Id:D}';"));
        Assert.False(await (await factory.DatabaseAsync).ScalarAsync<bool>(
            $"SELECT ReportImages FROM Principals WHERE Id = '{principal.Id:D}';"));
        // The fee note VAT number is saved with them, trimmed and as typed (R1006, R1007).
        Assert.Equal("GB 123 4567 89", updated.FeeNoteVatNumber);
        Assert.Equal("GB 123 4567 89", await (await factory.DatabaseAsync).ScalarAsync<string>(
            $"SELECT FeeNoteVatNumber FROM Principals WHERE Id = '{principal.Id:D}';"));
        // The Audit workflow and the Principal's own invoice are saved with
        // them, by their codes, the two invoice values trimmed (R1138, R1147, R1148).
        Assert.Equal(AuditWorkflowKind.GarageAuthorisationAndInvoice, updated.AuditWorkflow);
        Assert.Equal(PrincipalInvoiceKind.UlaInvoice, updated.PrincipalInvoice);
        Assert.Equal("GB 987 6543 21", updated.InvoiceVatNumber);
        Assert.Equal("30 days", updated.InvoicePaymentTerms);
        Assert.Equal(
            "garage_authorisation_and_invoice|ula_invoice|GB 987 6543 21|30 days",
            await (await factory.DatabaseAsync).ScalarAsync<string>(
                $"SELECT AuditWorkflow + '|' + PrincipalInvoice + '|' + InvoiceVatNumber + '|' + InvoicePaymentTerms FROM Principals WHERE Id = '{principal.Id:D}';"));
        Assert.Equal(
            principal.ContactVersion + 1,
            await (await factory.DatabaseAsync).ScalarAsync<long>(
                $"SELECT Version FROM Organizations WHERE Id = '{principal.OrganizationId:D}';"));

        var staleContact = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            update.ExecuteAsync(
                Request(updated.Version, principal.ContactVersion, "principal:report-settings:stale-contact"), default));
        Assert.Equal(OrganizationAdministrationError.StaleVersion, staleContact.Error);

        var stalePrincipal = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            update.ExecuteAsync(
                Request(principal.Version, principal.ContactVersion + 1, "principal:report-settings:stale-principal"), default));
        Assert.Equal(OrganizationAdministrationError.StaleVersion, stalePrincipal.Error);
    }

    [Fact]
    public async Task ReportSendingRulesAreKeptOnceReplayedAndRecordedInHistory()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var principal = await context.Principals.AsNoTracking()
            .Where(item => item.Code == QdosPrincipal.Code)
            .Select(item => new { item.Id, item.Version, item.OrganizationId, ContactVersion = item.Organization.Version })
            .SingleAsync();
        var update = services.GetRequiredService<IUpdatePrincipalReportSending>();
        var getPrincipal = services.GetRequiredService<IGetPrincipal>();
        var rules = Rules("Check first.");

        UpdatePrincipalReportSendingRequest Request(
            long principalVersion, long contactVersion, string operationKey, PrincipalReportSendingRules value) => new(
            principal.Id, principalVersion, Administrator, operationKey, value, contactVersion);

        // The seed gave QDOS its rules; the update replaces them.
        var before = await getPrincipal.ExecuteAsync(Administrator, principal.Id, default);
        Assert.NotEqual(PrincipalReportSendingRules.Default, before!.Principal.ReportSending);

        var request = Request(principal.Version, principal.ContactVersion, "principal:report-sending:first", rules);
        var updated = await update.ExecuteAsync(request, default);
        var replay = await update.ExecuteAsync(request, default);

        Assert.Equal(updated, replay);
        Assert.Equal(principal.Version + 1, updated.Version);
        Assert.Equal(rules, updated.ReportSending);
        Assert.Equal(
            principal.ContactVersion + 1,
            await (await factory.DatabaseAsync).ScalarAsync<long>(
                $"SELECT Version FROM Organizations WHERE Id = '{principal.OrganizationId:D}';"));
        Assert.Equal(
            1,
            await (await factory.DatabaseAsync).ScalarAsync<int>(
                "SELECT COUNT(*) FROM OrganizationAdministrationOperations WHERE OperationKey = 'principal:report-sending:first' AND CommandKind = 'update_principal_report_sending';"));
        Assert.Equal(
            1,
            await (await factory.DatabaseAsync).ScalarAsync<int>(
                "SELECT COUNT(*) FROM ActionHistory WHERE CorrelationId = 'principal:report-sending:first' AND EventKind = 'principal_report_sending_updated';"));
        var after = await getPrincipal.ExecuteAsync(Administrator, principal.Id, default);
        Assert.Equal(rules, after!.Principal.ReportSending);
        // Stored in the built shape, enums by name (R1267).
        Assert.Equal(
            "Check first.",
            await (await factory.DatabaseAsync).ScalarAsync<string>(
                $"SELECT JSON_VALUE(ReportSendingRulesJson, '$.everyCase[4].text') FROM Principals WHERE Id = '{principal.Id:D}';"));
        Assert.Equal(
            "Sender",
            await (await factory.DatabaseAsync).ScalarAsync<string>(
                $"SELECT JSON_VALUE(ReportSendingRulesJson, '$.rules[0].if[1][0].fact') FROM Principals WHERE Id = '{principal.Id:D}';"));
        // The history and the receipt write the rules' enums by name too, so
        // the record reads as the stored rules do.
        var historyAfter = await (await factory.DatabaseAsync).ScalarAsync<string>(
            "SELECT AfterJson FROM ActionHistory WHERE CorrelationId = 'principal:report-sending:first' AND EventKind = 'principal_report_sending_updated';");
        Assert.Contains("\"kind\":\"SendTo\"", historyAfter, StringComparison.Ordinal);
        Assert.Contains("\"fact\":\"Outcome\"", historyAfter, StringComparison.Ordinal);
        Assert.Contains("\"file\":\"Estimate\"", historyAfter, StringComparison.Ordinal);
        Assert.Contains(
            "\"kind\":\"Stop\"",
            await (await factory.DatabaseAsync).ScalarAsync<string>(
                "SELECT ResultJson FROM OrganizationAdministrationOperations WHERE OperationKey = 'principal:report-sending:first';"),
            StringComparison.Ordinal);

        var conflict = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            update.ExecuteAsync(
                Request(principal.Version, principal.ContactVersion, "principal:report-sending:first", Rules("Different.")),
                default));
        Assert.Equal(OrganizationAdministrationError.OperationConflict, conflict.Error);

        var staleContact = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            update.ExecuteAsync(
                Request(updated.Version, principal.ContactVersion, "principal:report-sending:stale-contact", Rules("Other.")),
                default));
        Assert.Equal(OrganizationAdministrationError.StaleVersion, staleContact.Error);

        var stalePrincipal = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            update.ExecuteAsync(
                Request(principal.Version, principal.ContactVersion + 1, "principal:report-sending:stale-principal", Rules("Other.")),
                default));
        Assert.Equal(OrganizationAdministrationError.StaleVersion, stalePrincipal.Error);

        // Saving the same rules again changes nothing about the Principal.
        var unchanged = await update.ExecuteAsync(
            Request(updated.Version, principal.ContactVersion + 1, "principal:report-sending:unchanged", rules), default);
        Assert.Equal(updated.Version, unchanged.Version);

        var refused = await Assert.ThrowsAsync<ReportSendingRulesException>(() =>
            update.ExecuteAsync(
                Request(updated.Version, principal.ContactVersion + 2, "principal:report-sending:refused",
                    rules with { EveryCase = [new(ReportSendingActionKind.Cc, Addresses: NotAnAddress)] }),
                default));
        Assert.Equal(ReportSendingRulesRule.InvalidAddress, refused.Rule);
        Assert.Null(refused.RuleIndex);
    }

    private static readonly string[] NotAnAddress = ["not an address"];

    /// <summary>
    /// Every-Case actions with a Hold of <paramref name="hold"/>, and one rule
    /// of two groups: Outcome is Repairable, or Sender is not the Principal's
    /// own address.
    /// </summary>
    private static PrincipalReportSendingRules Rules(string hold) => new(
        [
            new(ReportSendingActionKind.SendTo, Addresses: ["to@example.com"]),
            new(ReportSendingActionKind.Cc, Addresses: ["cc@example.com"]),
            new(ReportSendingActionKind.ReplyAll),
            new(ReportSendingActionKind.Attach, File: ReportSendingFile.Estimate),
            new(ReportSendingActionKind.Hold, Text: hold)
        ],
        [
            new(
                [
                    [new(ReportSendingFact.Outcome, ["repairable"])],
                    [new(ReportSendingFact.Sender, ["principal@example.com"], Not: true)]
                ],
                [
                    new(ReportSendingActionKind.Cc, Addresses: ["rule@example.com"]),
                    new(ReportSendingActionKind.Stop, Text: "Stop.")
                ])
        ]);

    /// <summary>
    /// A Principal contact's save carries its Case defaults (operator, 25
    /// November 2026; R1266, R1267): the fee, the default inspection location
    /// and the Send from mailbox are written to the current Principal, its
    /// version and the contact's both move, and the save's history records
    /// them. Image Based Assessment clears the location, a physical address
    /// may have none, and an existing Principal must post a fee.
    /// </summary>
    [Fact]
    public async Task APrincipalContactSaveWritesItsCaseDefaults()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contacts = services.GetRequiredService<IContactDirectoryAdministration>();
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var principal = await context.Principals.AsNoTracking()
            .Where(item => item.Code == QdosPrincipal.Code)
            .Select(item => new { item.Id, item.Version, item.OrganizationId })
            .SingleAsync();
        var contact = await context.Organizations.AsNoTracking()
            .SingleAsync(item => item.Id == principal.OrganizationId);

        SaveContactRequest Save(
            long version,
            string operationKey,
            CaseInspectionMode mode,
            decimal? fee,
            string? label,
            string? address,
            string? postcode,
            string? sendFrom) => new(
            Administrator, contact.Id, version, contact.Name, contact.Email, contact.Telephone, contact.Address,
            contact.Postcode, contact.Active, ContactRole.Principal, QdosPrincipal.Code, mode, [], operationKey,
            contact.NotesOnEveryCase, fee, label, address, postcode, sendFrom);
        Task<PrincipalEntity> StoredAsync() => context.Principals.AsNoTracking().SingleAsync(item => item.Id == principal.Id);

        var saved = await contacts.SaveAsync(
            Save(contact.Version, "contact:case-defaults:physical", CaseInspectionMode.PhysicalAddress,
                212.50m, " Yard ", " 1 Directory Way ", " DW1 2EF ", " engineers@collisionengineers.co.uk "),
            default);

        Assert.Equal(contact.Version + 1, saved.Version);
        Assert.Equal(212.50m, saved.DefaultFee);
        Assert.Equal("Yard", saved.DefaultInspectionLocationLabel);
        Assert.Equal("1 Directory Way", saved.DefaultInspectionAddress);
        Assert.Equal("DW1 2EF", saved.DefaultInspectionPostcode);
        Assert.Equal("engineers@collisionengineers.co.uk", saved.SendFromMailbox);
        var stored = await StoredAsync();
        Assert.Equal(principal.Version + 1, stored.Version);
        Assert.Equal(212.50m, stored.DefaultFee);
        Assert.Equal(
            ("Yard", "1 Directory Way", "DW1 2EF", "manual"),
            (stored.DefaultInspectionLocationLabel, stored.DefaultInspectionAddress, stored.DefaultInspectionPostcode, stored.DefaultInspectionSourceKind));
        Assert.Null(stored.DefaultInspectionSourceRecordId);
        Assert.Null(stored.DefaultInspectionSourceVersion);
        Assert.Equal("engineers@collisionengineers.co.uk", stored.SendFromMailbox);
        var history = await (await factory.DatabaseAsync).ScalarAsync<string>(
            "SELECT AfterJson FROM ActionHistory WHERE CorrelationId = 'contact:case-defaults:physical' AND EventKind = 'contact_saved';");
        Assert.Contains("\"defaultFee\":212.5", history, StringComparison.Ordinal);
        Assert.Contains("\"defaultInspectionAddress\":\"1 Directory Way\"", history, StringComparison.Ordinal);
        Assert.Contains("\"sendFromMailbox\":\"engineers@collisionengineers.co.uk\"", history, StringComparison.Ordinal);

        // Saved again unchanged, neither version moves.
        var unchanged = await contacts.SaveAsync(
            Save(saved.Version, "contact:case-defaults:unchanged", CaseInspectionMode.PhysicalAddress,
                212.50m, "Yard", "1 Directory Way", "DW1 2EF", "engineers@collisionengineers.co.uk"),
            default);
        Assert.Equal(saved.Version, unchanged.Version);
        Assert.Equal(stored.Version, (await StoredAsync()).Version);

        // Image Based Assessment clears the location; Send from may be cleared.
        var imageBased = await contacts.SaveAsync(
            Save(unchanged.Version, "contact:case-defaults:iba", CaseInspectionMode.ImageBasedAssessment,
                212.50m, "Yard", "1 Directory Way", "DW1 2EF", null),
            default);
        Assert.Equal(CaseInspectionMode.ImageBasedAssessment, imageBased.PrincipalInspectionMode);
        Assert.Null(imageBased.DefaultInspectionAddress);
        var cleared = await StoredAsync();
        Assert.Equal(stored.Version + 1, cleared.Version);
        Assert.Equal(PrincipalInspectionModePolicy.ImageBasedAssessmentCode, cleared.InspectionMode);
        Assert.Equal(
            ((string?)null, (string?)null, (string?)null, (string?)null, (string?)null),
            (cleared.DefaultInspectionLocationLabel, cleared.DefaultInspectionAddress, cleared.DefaultInspectionPostcode,
                cleared.DefaultInspectionSourceKind, cleared.SendFromMailbox));

        // A physical address may have no location, but a label needs one.
        var noLocation = await contacts.SaveAsync(
            Save(imageBased.Version, "contact:case-defaults:no-location", CaseInspectionMode.PhysicalAddress,
                212.50m, null, null, null, null),
            default);
        Assert.Equal(CaseInspectionMode.PhysicalAddress, noLocation.PrincipalInspectionMode);
        Assert.Null(noLocation.DefaultInspectionAddress);
        var labelOnly = await Assert.ThrowsAsync<ArgumentException>(() => contacts.SaveAsync(
            Save(noLocation.Version, "contact:case-defaults:label-only", CaseInspectionMode.PhysicalAddress,
                212.50m, "Yard", null, null, null),
            default));
        Assert.Equal(nameof(SaveContactRequest.DefaultInspectionAddress), labelOnly.ParamName);

        // An existing Principal must post a fee.
        var noFee = await Assert.ThrowsAsync<ArgumentException>(() => contacts.SaveAsync(
            Save(noLocation.Version, "contact:case-defaults:no-fee", CaseInspectionMode.PhysicalAddress,
                null, null, null, null, null),
            default));
        Assert.Equal(nameof(SaveContactRequest.DefaultFee), noFee.ParamName);
        Assert.Equal(212.50m, (await StoredAsync()).DefaultFee);
    }

    /// <summary>
    /// A successor code keeps the way its Cases are referenced: replacing the
    /// standing Private customer record's code still gives registration
    /// references, and no other Principal gains them (R574).
    /// </summary>
    [Fact]
    public async Task ReplacingThePrivateCustomerCodeKeepsRegistrationReferences()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var replacePrincipal = scope.ServiceProvider.GetRequiredService<IReplacePrincipal>();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var predecessor = await context.Principals.AsNoTracking().SingleAsync(item => item.Code == "PRIVATE");
        Assert.Equal("registration", predecessor.ReferenceScheme);
        var contactVersion = await context.Organizations.AsNoTracking()
            .Where(item => item.Id == predecessor.OrganizationId)
            .Select(item => item.Version)
            .SingleAsync();

        var successor = await replacePrincipal.ExecuteAsync(
            new ReplacePrincipalRequest(
                predecessor.Id,
                predecessor.Version,
                "PRIVATE2",
                Administrator,
                "principal:replace:private",
                "Code replaced",
                contactVersion),
            default);

        Assert.Equal(
            "registration",
            await context.Principals.AsNoTracking()
                .Where(item => item.Id == successor.Id)
                .Select(item => item.ReferenceScheme)
                .SingleAsync());
        Assert.Equal(
            "principal_sequence",
            await context.Principals.AsNoTracking()
                .Where(item => item.Code == QdosPrincipal.Code)
                .Select(item => item.ReferenceScheme)
                .SingleAsync());
    }

    [Fact]
    public async Task ReplacementDisablesAndLinksPredecessorWithoutChangingAllocatedCaseIdentity()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var replacePrincipal = scope.ServiceProvider.GetRequiredService<IReplacePrincipal>();
        var getPrincipal = scope.ServiceProvider.GetRequiredService<IGetPrincipal>();
        var acceptIntake = scope.ServiceProvider.GetRequiredService<IAcceptIntake>();

        // The foundation migration (C-F05) already seeds the QDOS principal
        // and its owning work-provider organization exactly once; a fresh
        // create with the same principal code would collide with it
        // globally (Principal.Code has no per-organization scope), so the
        // predecessor here is the seeded row itself, replaced within its
        // own already-seeded, already-eligible organization.
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var seedContext = await contextFactory.CreateDbContextAsync();
        var predecessor = await seedContext.Principals.AsNoTracking()
            .SingleAsync(item => item.Code == QdosPrincipal.Code);
        await seedContext.Principals
            .Where(item => item.Id == predecessor.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.DefaultFee, 210.00m)
                .SetProperty(item => item.InspectionReportNameFirst, "{REG} Initial")
                .SetProperty(item => item.FeeNoteVatNumber, "GB 123 4567 89")
                .SetProperty(item => item.AuditWorkflow, PrincipalAuditWorkflowPolicy.GarageAuthorisationAndInvoiceCode)
                .SetProperty(item => item.PrincipalInvoice, PrincipalInvoicePolicy.UlaInvoiceCode)
                .SetProperty(item => item.InvoicePaymentTerms, "30 days")
                .SetProperty(item => item.SendFromMailbox, "engineers@collisionengineers.co.uk")
                .SetProperty(item => item.ReportImages, false)
                .SetProperty(item => item.DefaultInspectionLocationLabel, "Directory Web Caller Yard")
                .SetProperty(item => item.DefaultInspectionAddress, "1 Directory Way, DW1 2EF")
                .SetProperty(item => item.DefaultInspectionPostcode, "DW1 2EF")
                .SetProperty(item => item.DefaultInspectionSourceKind, "manual"));
        var initialContactVersion = await seedContext.Organizations.AsNoTracking()
            .Where(item => item.Id == predecessor.OrganizationId)
            .Select(item => item.Version)
            .SingleAsync();
        var receipt = await CreateReadyReceiptAsync(factory.Services);
        var receiptVersion = await (await factory.DatabaseAsync).ScalarAsync<long>(
            $"SELECT Version FROM IntakeReceipts WHERE Id = '{receipt.Id:D}';");
        var accepted = await acceptIntake.ExecuteAsync(
            new(
                receipt.Id,
                receiptVersion,
                Administrator,
                "case:accept:replacement-test",
                CaseType.Inspection,
                predecessor.Code,
                new(true, true)),
            default);
        var originalReference = accepted.Identity.Reference;
        var replacementContactVersion = await (await factory.DatabaseAsync).ScalarAsync<long>(
            $"SELECT Version FROM Organizations WHERE Id = '{predecessor.OrganizationId:D}';");
        var replacementRequest = new ReplacePrincipalRequest(
            predecessor.Id,
            predecessor.Version,
            "QDOSNEXT",
            Administrator,
            "principal:replace:qdos",
            "Provider issued a successor code",
            replacementContactVersion);

        var successor = await replacePrincipal.ExecuteAsync(replacementRequest, default);
        var replay = await replacePrincipal.ExecuteAsync(replacementRequest, default);

        Assert.Equal(successor, replay);
        Assert.Equal(predecessor.SequenceLineageId, successor.SequenceLineageId);
        Assert.Equal(predecessor.Id, successor.PredecessorId);
        Assert.True(successor.IsActive);
        var predecessorDetails = await getPrincipal.ExecuteAsync(Administrator, predecessor.Id, default);
        var successorDetails = await getPrincipal.ExecuteAsync(Administrator, successor.Id, default);
        Assert.NotNull(predecessorDetails);
        Assert.NotNull(successorDetails);
        var persistedPredecessor = predecessorDetails.Principal;
        var persistedSuccessor = successorDetails.Principal;
        Assert.Equal(predecessorDetails.Name, successorDetails.Name);
        Assert.Equal(persistedPredecessor.OrganizationId, persistedSuccessor.OrganizationId);
        Assert.Equal(
            ("Directory Web Caller Yard",
                "1 Directory Way, DW1 2EF",
                "DW1 2EF",
                "manual",
                (Guid?)null,
                (long?)null),
            (persistedSuccessor.DefaultInspectionLocationLabel,
                persistedSuccessor.DefaultInspectionAddress,
                persistedSuccessor.DefaultInspectionPostcode,
                persistedSuccessor.DefaultInspectionSourceKind,
                persistedSuccessor.DefaultInspectionSourceRecordId,
                persistedSuccessor.DefaultInspectionSourceVersion));
        Assert.Equal(210.00m, persistedSuccessor.DefaultFee);
        // And starts on its predecessor's Send from and Report images (R1267).
        Assert.Equal("engineers@collisionengineers.co.uk", persistedSuccessor.SendFromMailbox);
        Assert.False(persistedSuccessor.ReportImages);
        // A successor code is named as its predecessor was (R740).
        Assert.Equal("{REG} Initial", persistedSuccessor.ReportNames.InspectionFirst);
        // And prints the fee note VAT number its predecessor did (R1006).
        Assert.Equal("GB 123 4567 89", persistedSuccessor.FeeNoteVatNumber);
        // And keeps its Audit workflow and its own invoice (R1138, R1147).
        Assert.Equal(AuditWorkflowKind.GarageAuthorisationAndInvoice, persistedSuccessor.AuditWorkflow);
        Assert.Equal(PrincipalInvoiceKind.UlaInvoice, persistedSuccessor.PrincipalInvoice);
        Assert.Equal("30 days", persistedSuccessor.InvoicePaymentTerms);
        Assert.False(persistedPredecessor.IsActive);
        Assert.Equal(successor.Id, persistedPredecessor.SuccessorId);
        Assert.Equal("QDOS", persistedPredecessor.Code);
        Assert.Equal(1, persistedPredecessor.AllocatedCaseCount);
        Assert.Equal(0, persistedSuccessor.AllocatedCaseCount);

        Assert.Equal(
            predecessor.Id,
            await (await factory.DatabaseAsync).ScalarAsync<Guid>(
                $"SELECT PrincipalId FROM Cases WHERE Id = '{accepted.Identity.CaseId:D}';"));
        Assert.Equal(
            originalReference,
            await (await factory.DatabaseAsync).ScalarAsync<string>(
                $"SELECT Reference FROM Cases WHERE Id = '{accepted.Identity.CaseId:D}';"));
        Assert.Equal(
            2,
            await (await factory.DatabaseAsync).ScalarAsync<int>(
                "SELECT COUNT(*) FROM ActionHistory WHERE CorrelationId = 'principal:replace:qdos';"));
        Assert.Equal(
            1,
            await (await factory.DatabaseAsync).ScalarAsync<int>(
                "SELECT COUNT(*) FROM OrganizationAdministrationOperations WHERE OperationKey = 'principal:replace:qdos';"));
        Assert.Equal(
            initialContactVersion + 1,
            await (await factory.DatabaseAsync).ScalarAsync<long>(
                $"SELECT Version FROM Organizations WHERE Id = '{predecessor.OrganizationId:D}';"));

        var conflict = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            replacePrincipal.ExecuteAsync(
                replacementRequest with { SuccessorCode = "CHANGED" },
                default));
        Assert.Equal(OrganizationAdministrationError.OperationConflict, conflict.Error);
        var currentPrincipalVersion = await (await factory.DatabaseAsync).ScalarAsync<long>(
            $"SELECT Version FROM Principals WHERE Id = '{predecessor.Id:D}';");
        var stale = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            replacePrincipal.ExecuteAsync(
                replacementRequest with
                {
                    OperationKey = "principal:replace:stale",
                    SuccessorCode = "ANOTHER",
                    ExpectedVersion = currentPrincipalVersion
                },
                default));
        Assert.Equal(OrganizationAdministrationError.StaleVersion, stale.Error);
    }

    /// <summary>
    /// The successor offers the predecessor's reviewed inspection locations,
    /// and the predecessor keeps its own links, so its open Cases keep their
    /// list.
    /// </summary>
    [Fact]
    public async Task ReplacementCarriesTheInspectionLocationLinks()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = await services.GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var predecessor = await context.Principals.AsNoTracking()
            .Where(item => item.Code == QdosPrincipal.Code)
            .Select(item => new { item.Id, item.Version, ContactVersion = item.Organization.Version })
            .SingleAsync();
        var location = await services.GetRequiredService<IInspectionLocationAdministration>().SaveAsync(
            new(
                Administrator,
                "Test Body Centre",
                "1 Test Street, Testtown",
                "TE1 1ST",
                true,
                "register",
                "row-1",
                RecordedAtUtc,
                [predecessor.Id],
                "inspection-location:replacement"),
            default);

        var successor = await services.GetRequiredService<IReplacePrincipal>().ExecuteAsync(
            new(
                predecessor.Id,
                predecessor.Version,
                "QDOSNEXT",
                Administrator,
                "principal:replace:locations",
                "The Principal issued a successor code",
                predecessor.ContactVersion),
            default);

        Assert.Equal(
            new[] { predecessor.Id, successor.Id }.Order(),
            (await context.InspectionLocationPrincipalLinks.AsNoTracking()
                .Where(link => link.InspectionLocationId == location.Id)
                .Select(link => link.PrincipalId)
                .ToArrayAsync()).Order());
    }

    private static async Task<IntakeReceipt> CreateReadyReceiptAsync(IServiceProvider services)
    {
        var token = Guid.NewGuid().ToString("N");
        var sourceHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>();
        return await store.StoreAsync(
            new(
                "principal-replacement.eml",
                "message/rfc822",
                1,
                sourceHash,
                new(IntakeSourceChannel.ManualUpload, token),
                RecordedAtUtc,
                RecordedAtUtc,
                "Principal replacement test",
                IntakeDecision.CaseCreated,
                "Ready for staff review",
                [],
                [
                    new(
                        "Claimant name",
                        "Replacement claimant",
                        [
                            new(
                                "Replacement claimant",
                                IntakeEvidenceSource.EmailBody,
                                "principal-replacement.eml")
                        ],
                        false,
                        false),
                    new(
                        "Claim number",
                        "REPLACEMENT-001",
                        [
                            new(
                                "REPLACEMENT-001",
                                IntakeEvidenceSource.EmailBody,
                                "principal-replacement.eml")
                        ],
                        false,
                        false),
                    new(
                        "Vehicle registration",
                        "AB12CDE",
                        [
                            new(
                                "AB12CDE",
                                IntakeEvidenceSource.EmailBody,
                                "principal-replacement.eml")
                        ],
                        false,
                        false),
                    new(
                        "Inspection address",
                        "Image Based Assessment",
                        [
                            new(
                                "Image Based Assessment",
                                IntakeEvidenceSource.EmailBody,
                                "principal-replacement.eml")
                        ],
                        false,
                        false)
                ],
                new(
                    QdosPrincipal.Code,
                    "Replacement claimant",
                    "REPLACEMENT-001",
                    "AB12CDE",
                    null,
                    null,
                    null,
                    null,
                    null,
                    "Image Based Assessment"),
                [],
                null,
                null,
                "replacement_test_reader",
                "1",
                "replacement_test_policy",
                1),
            default);
    }

    /// <summary>
    /// C-F05/EXT-18 item 1: the fresh-database migration seeds the current
    /// top-15 principal identities exactly once, by the exact code/GUID the
    /// foundation handoff froze — and YML (the confirmed HDUK-branded route)
    /// stays a distinct principal role rather than merging with a document
    /// issuer or any other principal. The Report Sending SOP seed adds the
    /// other 17 SOP Principals, the contact SOP seed the 20 job-sheet
    /// Principals, the SOP v8 sync FRZ, the one-off migration the standing
    /// Private customer record PRIVATE (R574) and the ULA seed United Legal
    /// Assistance (R1145), so a fresh database holds 55.
    /// </summary>
    [Fact]
    public async Task FreshDatabaseSeedsTheFifteenFrozenPrincipalsOnce()
    {
        var expected = new (string Code, Guid PrincipalId)[]
        {
            ("QDOS", Guid.Parse("00000000-0000-4000-8000-00000000c001")),
            ("PCH", Guid.Parse("00000000-0000-4000-8000-00000000c002")),
            ("AX", Guid.Parse("00000000-0000-4000-8000-00000000c003")),
            ("FW", Guid.Parse("00000000-0000-4000-8000-00000000c004")),
            ("QCL", Guid.Parse("00000000-0000-4000-8000-00000000c005")),
            ("OAK", Guid.Parse("00000000-0000-4000-8000-00000000c006")),
            ("SBL", Guid.Parse("00000000-0000-4000-8000-00000000c007")),
            ("BLACK", Guid.Parse("00000000-0000-4000-8000-00000000c008")),
            ("RJS", Guid.Parse("00000000-0000-4000-8000-00000000c009")),
            ("DFD", Guid.Parse("00000000-0000-4000-8000-00000000c00a")),
            ("KBS", Guid.Parse("00000000-0000-4000-8000-00000000c00b")),
            ("MP", Guid.Parse("00000000-0000-4000-8000-00000000c00c")),
            ("YML", Guid.Parse("00000000-0000-4000-8000-00000000c00d")),
            ("ALS", Guid.Parse("00000000-0000-4000-8000-00000000c00e")),
            ("BC", Guid.Parse("00000000-0000-4000-8000-00000000c00f"))
        };

        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();

        var seeded = await context.Principals
            .AsNoTracking()
            .Select(principal => new { principal.Id, principal.Code })
            .ToListAsync();

        Assert.Equal(56, seeded.Count);
        foreach (var (code, principalId) in expected)
        {
            var matches = seeded.Where(row => row.Id == principalId).ToArray();
            Assert.True(
                matches.Length == 1,
                $"Expected exactly one seeded principal with id {principalId}, found {matches.Length}.");
            Assert.Equal(code, matches[0].Code);
        }

        // YML is the confirmed HDUK-branded route, but HDUK itself is a
        // document issuer, never a second principal identity: no seeded
        // principal may carry that code, and every seeded code above is
        // unique.
        Assert.DoesNotContain(seeded, row => row.Code == "HDUK");
        Assert.Equal(seeded.Count, seeded.Select(row => row.Code).Distinct().Count());
        Assert.Equal(seeded.Count, seeded.Select(row => row.Id).Distinct().Count());
    }
}

using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// 20261007090000_PrincipalReportSendingRules: every Principal has report
/// sending rules, so a Principal the SOP does not name gets the default
/// rules, the retired report recipient settings' two columns are dropped and
/// the migration cannot be reverted.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class PrincipalReportSendingRulesMigrationTests
{
    private const string PreviousMigration = "20261006150000_DropCaseReportDeliveryIntents";
    private const string Migration = "20261007090000_PrincipalReportSendingRules";
    private const string PrincipalId = "b3000000-0000-0000-0000-000000000003";
    private const string Recorded = "2031-05-06T10:30:00+00:00";

    [Fact]
    public async Task APrincipalOutsideTheSopGetsTheDefaultRulesAndTheOldColumnsGo()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(SeedSql);

        await context.Database.MigrateAsync(Migration);

        Assert.Equal(Migration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        var json = await database.ScalarAsync<string>(
            $"SELECT ReportSendingRulesJson FROM Principals WHERE Id = '{PrincipalId}'");
        Assert.Equal(EfOrganizationAdministration.ToReportSendingJson(PrincipalReportSendingRules.Default), json);
        Assert.Equal(PrincipalReportSendingRules.Default, EfOrganizationAdministration.ReadReportSending(json));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM Principals WHERE ReportSendingRulesJson IS NULL"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'Principals') AND name = N'ReportSendingRulesJson' AND is_nullable = 0"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM sys.columns
            WHERE object_id = OBJECT_ID(N'Principals')
              AND name IN (N'IncludeOriginalInstructionSender', N'ReportRecipientAddressesJson')
            """));
    }

    [Fact]
    public async Task MigrationCannotBeReverted()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false, useTemplate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(Migration);

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => context.Database.MigrateAsync(PreviousMigration));

        Assert.StartsWith("PrincipalReportSendingRules is forward-only", exception.Message, StringComparison.Ordinal);
        Assert.Equal(Migration, (await context.Database.GetAppliedMigrationsAsync()).Last());
    }

    // One Principal the Report Sending SOP does not name, at the schema before
    // PrincipalReportSendingRules, with a report recipient address of its own.
    private const string SeedSql =
        $"""
        INSERT INTO Organizations (Id, Name, Version)
        VALUES ('b3000000-0000-0000-0000-000000000001', N'Report sending rules provider', 0);

        INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc)
        VALUES ('b3000000-0000-0000-0000-000000000002', '{Recorded}');

        INSERT INTO Principals
            (Id, OrganizationId, Code, SequenceLineageId, PredecessorId, SuccessorId, IsActive,
             IncludeOriginalInstructionSender, ReportRecipientAddressesJson, Version)
        VALUES
            ('{PrincipalId}', 'b3000000-0000-0000-0000-000000000001', N'RSRT',
             'b3000000-0000-0000-0000-000000000002', NULL, NULL, 1,
             1, N'["reports@example.test"]', 0);
        """;
}

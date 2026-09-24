using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Raw-SQL fixtures insert Cases directly, so they give each one its primary
/// work themselves, as every Case creator's SaveChanges does (the primary
/// work's id is the Case id). Idempotent: Cases that already have their
/// primary work are left alone.
/// </summary>
internal static class CaseWorkFixture
{
    public const string PrimaryWorksSql =
        "INSERT INTO [dbo].[CaseWorks] ([Id], [CaseId], [Kind], [CreatedAtUtc]) " +
        "SELECT c.[Id], c.[Id], N'primary', c.[CreatedAtUtc] FROM [dbo].[Cases] AS c " +
        "WHERE NOT EXISTS (SELECT 1 FROM [dbo].[CaseWorks] AS w WHERE w.[Id] = c.[Id]);";

    public static Task InsertPrimaryWorksAsync(DbContext context, CancellationToken cancellationToken = default) =>
        context.Database.ExecuteSqlRawAsync(PrimaryWorksSql, cancellationToken);
}

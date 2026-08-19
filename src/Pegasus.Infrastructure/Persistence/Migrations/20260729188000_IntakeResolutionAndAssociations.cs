using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729188000_IntakeResolutionAndAssociations")]
public partial class IntakeResolutionAndAssociations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // no-runtime-grant: IntakeManualAssociations - granted via the consolidated 20260729199000_RuntimeRoleReconciliation least-privilege migration
        // no-runtime-grant: IntakeMutationHistory - granted via the consolidated 20260729199000_RuntimeRoleReconciliation least-privilege migration
        migrationBuilder.AddColumn<string>(
            name: "ActorKind",
            table: "CaseIntakeLinks",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ActorSubjectId",
            table: "CaseIntakeLinks",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ActorRolesJson",
            table: "CaseIntakeLinks",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Reason",
            table: "CaseIntakeLinks",
            maxLength: 500,
            nullable: true);

        migrationBuilder.Sql(
            """
            IF EXISTS (
                SELECT 1
                FROM [CaseIntakeLinks] AS [link]
                WHERE TRY_CONVERT(uniqueidentifier, [link].[Actor]) IS NULL
                   OR NOT EXISTS (
                       SELECT 1
                       FROM [AspNetUserRoles] AS [userRole]
                       INNER JOIN [AspNetRoles] AS [role] ON [role].[Id] = [userRole].[RoleId]
                       WHERE [userRole].[UserId] = TRY_CONVERT(uniqueidentifier, [link].[Actor])
                         AND [role].[Name] IN (N'Administrator', N'Engineer', N'User'))
                   OR NOT EXISTS (
                       SELECT 1
                       FROM [CaseHistory] AS [history]
                       WHERE [history].[CaseId] = [link].[CaseId]
                         AND [history].[OperationKey] = [link].[OperationKey]
                         AND [history].[EventType] = N'case_accepted'
                         AND LEN(LTRIM(RTRIM([history].[Reason]))) BETWEEN 1 AND 500))
                THROW 51000, 'Existing intake acceptance actors cannot be migrated without an attributable current staff role.', 1;

            UPDATE [link]
            SET [ActorKind] = N'Staff',
                [ActorSubjectId] = [link].[Actor],
                [ActorRolesJson] = [roles].[RoleJson],
                [Reason] = [history].[Reason]
            FROM [CaseIntakeLinks] AS [link]
            INNER JOIN [CaseHistory] AS [history]
                ON [history].[CaseId] = [link].[CaseId]
               AND [history].[OperationKey] = [link].[OperationKey]
               AND [history].[EventType] = N'case_accepted'
            CROSS APPLY (
                SELECT N'[' + STRING_AGG(
                    N'"' + STRING_ESCAPE([role].[Name], 'json') + N'"',
                    N',') WITHIN GROUP (ORDER BY [role].[Name]) + N']'
                    AS [RoleJson]
                FROM [AspNetUserRoles] AS [userRole]
                INNER JOIN [AspNetRoles] AS [role] ON [role].[Id] = [userRole].[RoleId]
                WHERE [userRole].[UserId] = TRY_CONVERT(uniqueidentifier, [link].[Actor])
                  AND [role].[Name] IN (N'Administrator', N'Engineer', N'User')
            ) AS [roles];
            """);

        migrationBuilder.AlterColumn<string>(
            name: "ActorKind",
            table: "CaseIntakeLinks",
            maxLength: 40,
            nullable: false,
            oldClrType: typeof(string),
            oldMaxLength: 40,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "ActorSubjectId",
            table: "CaseIntakeLinks",
            maxLength: 200,
            nullable: false,
            oldClrType: typeof(string),
            oldMaxLength: 200,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "ActorRolesJson",
            table: "CaseIntakeLinks",
            nullable: false,
            oldClrType: typeof(string),
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Reason",
            table: "CaseIntakeLinks",
            maxLength: 500,
            nullable: false,
            oldClrType: typeof(string),
            oldMaxLength: 500,
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "Actor",
            table: "CaseIntakeLinks");

        migrationBuilder.CreateTable(
            name: "IntakeManualAssociations",
            columns: table => new
            {
                IntakeReceiptId = table.Column<Guid>(nullable: false),
                CaseId = table.Column<Guid>(nullable: false),
                IsActive = table.Column<bool>(nullable: false),
                Version = table.Column<long>(nullable: false),
                LinkedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                UnlinkedAtUtc = table.Column<DateTimeOffset>(nullable: true),
                ActorKind = table.Column<string>(maxLength: 40, nullable: false),
                ActorSubjectId = table.Column<string>(maxLength: 200, nullable: false),
                ActorRolesJson = table.Column<string>(nullable: false),
                Reason = table.Column<string>(maxLength: 500, nullable: false),
                LastOperationKey = table.Column<string>(maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IntakeManualAssociations", x => x.IntakeReceiptId);
                table.CheckConstraint("CK_IntakeManualAssociations_Version", "[Version] >= 0");
                table.ForeignKey(
                    name: "FK_IntakeManualAssociations_Cases_CaseId",
                    column: x => x.CaseId,
                    principalTable: "Cases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_IntakeManualAssociations_IntakeReceipts_IntakeReceiptId",
                    column: x => x.IntakeReceiptId,
                    principalTable: "IntakeReceipts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "IntakeMutationHistory",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                IntakeReceiptId = table.Column<Guid>(nullable: false),
                CaseId = table.Column<Guid>(nullable: true),
                EventType = table.Column<string>(maxLength: 100, nullable: false),
                ActorKind = table.Column<string>(maxLength: 40, nullable: false),
                ActorSubjectId = table.Column<string>(maxLength: 200, nullable: false),
                ActorRolesJson = table.Column<string>(nullable: false),
                Reason = table.Column<string>(maxLength: 500, nullable: false),
                OperationKey = table.Column<string>(maxLength: 100, nullable: false),
                RequestFingerprint = table.Column<string>(fixedLength: true, maxLength: 64, nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(nullable: false),
                ExpectedIntakeVersion = table.Column<long>(nullable: false),
                BeforeIntakeVersion = table.Column<long>(nullable: false),
                AfterIntakeVersion = table.Column<long>(nullable: false),
                ExpectedCaseVersion = table.Column<long>(nullable: true),
                BeforeCaseVersion = table.Column<long>(nullable: true),
                AfterCaseVersion = table.Column<long>(nullable: true),
                BeforeJson = table.Column<string>(nullable: true),
                AfterJson = table.Column<string>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IntakeMutationHistory", x => x.Id);
                table.ForeignKey(
                    name: "FK_IntakeMutationHistory_Cases_CaseId",
                    column: x => x.CaseId,
                    principalTable: "Cases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_IntakeMutationHistory_IntakeReceipts_IntakeReceiptId",
                    column: x => x.IntakeReceiptId,
                    principalTable: "IntakeReceipts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_IntakeManualAssociations_CaseId",
            table: "IntakeManualAssociations",
            column: "CaseId");

        migrationBuilder.CreateIndex(
            name: "IX_IntakeManualAssociations_LastOperationKey",
            table: "IntakeManualAssociations",
            column: "LastOperationKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_IntakeMutationHistory_CaseId_OccurredAtUtc",
            table: "IntakeMutationHistory",
            columns: new[] { "CaseId", "OccurredAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_IntakeMutationHistory_IntakeReceiptId_OccurredAtUtc",
            table: "IntakeMutationHistory",
            columns: new[] { "IntakeReceiptId", "OccurredAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_IntakeMutationHistory_OperationKey",
            table: "IntakeMutationHistory",
            column: "OperationKey",
            unique: true);
        migrationBuilder.Sql(
            """
            IF EXISTS (
                SELECT 1
                FROM [CaseIntakeLinks]
                WHERE [ExpectedIntakeVersion] IS NULL
                   OR [AcceptanceCommandFingerprint] IS NULL
                   OR LEN([AcceptanceCommandFingerprint]) <> 64)
                THROW 51000, 'Existing accepted intake links do not contain complete replay evidence.', 1;

            INSERT INTO [IntakeManualAssociations] (
                [IntakeReceiptId],
                [CaseId],
                [IsActive],
                [Version],
                [LinkedAtUtc],
                [UnlinkedAtUtc],
                [ActorKind],
                [ActorSubjectId],
                [ActorRolesJson],
                [Reason],
                [LastOperationKey])
            SELECT
                [link].[IntakeReceiptId],
                [link].[CaseId],
                CAST(1 AS bit),
                CAST(0 AS bigint),
                [link].[LinkedAtUtc],
                NULL,
                [link].[ActorKind],
                [link].[ActorSubjectId],
                [link].[ActorRolesJson],
                [link].[Reason],
                [link].[OperationKey]
            FROM [CaseIntakeLinks] AS [link]
            WHERE NOT EXISTS (
                SELECT 1
                FROM [IntakeManualAssociations] AS [association]
                WHERE [association].[IntakeReceiptId] = [link].[IntakeReceiptId]);

            INSERT INTO [IntakeMutationHistory] (
                [Id],
                [IntakeReceiptId],
                [CaseId],
                [EventType],
                [ActorKind],
                [ActorSubjectId],
                [ActorRolesJson],
                [Reason],
                [OperationKey],
                [RequestFingerprint],
                [OccurredAtUtc],
                [ExpectedIntakeVersion],
                [BeforeIntakeVersion],
                [AfterIntakeVersion],
                [ExpectedCaseVersion],
                [BeforeCaseVersion],
                [AfterCaseVersion],
                [BeforeJson],
                [AfterJson])
            SELECT
                NEWID(),
                [link].[IntakeReceiptId],
                [link].[CaseId],
                N'intake_case_association_seeded',
                [link].[ActorKind],
                [link].[ActorSubjectId],
                [link].[ActorRolesJson],
                [link].[Reason],
                [link].[OperationKey],
                [link].[AcceptanceCommandFingerprint],
                [link].[LinkedAtUtc],
                [link].[ExpectedIntakeVersion],
                [link].[ExpectedIntakeVersion],
                [receipt].[Version],
                NULL,
                NULL,
                CAST(0 AS bigint),
                NULL,
                NULL
            FROM [CaseIntakeLinks] AS [link]
            INNER JOIN [IntakeReceipts] AS [receipt]
                ON [receipt].[Id] = [link].[IntakeReceiptId]
            WHERE NOT EXISTS (
                SELECT 1
                FROM [IntakeMutationHistory] AS [history]
                WHERE [history].[OperationKey] = [link].[OperationKey]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Actor",
            table: "CaseIntakeLinks",
            maxLength: 200,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE [CaseIntakeLinks]
            SET [Actor] = [ActorSubjectId];
            """);

        migrationBuilder.AlterColumn<string>(
            name: "Actor",
            table: "CaseIntakeLinks",
            maxLength: 200,
            nullable: false,
            oldClrType: typeof(string),
            oldMaxLength: 200,
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "ActorKind",
            table: "CaseIntakeLinks");

        migrationBuilder.DropColumn(
            name: "ActorSubjectId",
            table: "CaseIntakeLinks");

        migrationBuilder.DropColumn(
            name: "ActorRolesJson",
            table: "CaseIntakeLinks");

        migrationBuilder.DropColumn(
            name: "Reason",
            table: "CaseIntakeLinks");

        migrationBuilder.DropTable("IntakeManualAssociations");
        migrationBuilder.DropTable("IntakeMutationHistory");
    }
}

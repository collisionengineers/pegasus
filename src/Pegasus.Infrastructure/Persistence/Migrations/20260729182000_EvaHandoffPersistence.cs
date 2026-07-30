using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729182000_EvaHandoffPersistence")]
public sealed class EvaHandoffPersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EvaHandoffRevisions",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CaseId = table.Column<Guid>(nullable: false),
                Revision = table.Column<int>(nullable: false),
                AcceptedCaseVersion = table.Column<long>(nullable: false),
                SchemaVersion = table.Column<string>(maxLength: 50, nullable: false),
                InputFingerprint = table.Column<string>(fixedLength: true, maxLength: 64, nullable: false),
                FileName = table.Column<string>(maxLength: 260, nullable: false),
                BundleContent = table.Column<byte[]>(nullable: false),
                BundleSha256 = table.Column<string>(fixedLength: true, maxLength: 64, nullable: false),
                JsonContent = table.Column<byte[]>(nullable: false),
                JsonSha256 = table.Column<string>(fixedLength: true, maxLength: 64, nullable: false),
                ProvenanceContent = table.Column<byte[]>(nullable: false),
                ProvenanceSha256 = table.Column<string>(fixedLength: true, maxLength: 64, nullable: false),
                ManifestContent = table.Column<byte[]>(nullable: false),
                GeneratedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                GeneratedBy = table.Column<string>(maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EvaHandoffRevisions", item => item.Id);
                table.CheckConstraint(
                    "CK_EvaHandoffRevisions_AcceptedCaseVersion",
                    "[AcceptedCaseVersion] >= 0");
                table.CheckConstraint(
                    "CK_EvaHandoffRevisions_Revision",
                    "[Revision] > 0");
                table.ForeignKey(
                    name: "FK_EvaHandoffRevisions_Cases_CaseId",
                    column: item => item.CaseId,
                    principalTable: "Cases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EvaFirstHandoffProxies",
            columns: table => new
            {
                CaseId = table.Column<Guid>(nullable: false),
                RevisionId = table.Column<Guid>(nullable: false),
                AdapterKey = table.Column<string>(maxLength: 100, nullable: false),
                AdapterVersion = table.Column<string>(maxLength: 50, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                ActorSubjectId = table.Column<string>(maxLength: 200, nullable: false),
                OperationKey = table.Column<string>(maxLength: 100, nullable: false),
                ClaimsExternalDelivery = table.Column<bool>(nullable: false),
                ClaimsEngineerAssignment = table.Column<bool>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EvaFirstHandoffProxies", item => item.CaseId);
                table.CheckConstraint(
                    "CK_EvaFirstHandoffProxies_NoAssignmentClaim",
                    "[ClaimsEngineerAssignment] = 0");
                table.CheckConstraint(
                    "CK_EvaFirstHandoffProxies_NoDeliveryClaim",
                    "[ClaimsExternalDelivery] = 0");
                table.ForeignKey(
                    name: "FK_EvaFirstHandoffProxies_Cases_CaseId",
                    column: item => item.CaseId,
                    principalTable: "Cases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EvaFirstHandoffProxies_EvaHandoffRevisions_RevisionId",
                    column: item => item.RevisionId,
                    principalTable: "EvaHandoffRevisions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EvaHandoffOperations",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CaseId = table.Column<Guid>(nullable: false),
                OperationKey = table.Column<string>(maxLength: 100, nullable: false),
                RequestHash = table.Column<string>(fixedLength: true, maxLength: 64, nullable: false),
                RevisionId = table.Column<Guid>(nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                ActorSubjectId = table.Column<string>(maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EvaHandoffOperations", item => item.Id);
                table.ForeignKey(
                    name: "FK_EvaHandoffOperations_Cases_CaseId",
                    column: item => item.CaseId,
                    principalTable: "Cases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EvaHandoffOperations_EvaHandoffRevisions_RevisionId",
                    column: item => item.RevisionId,
                    principalTable: "EvaHandoffRevisions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EvaHandoffRevisions_CaseId_InputFingerprint",
            table: "EvaHandoffRevisions",
            columns: new[] { "CaseId", "InputFingerprint" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_EvaHandoffRevisions_CaseId_Revision",
            table: "EvaHandoffRevisions",
            columns: new[] { "CaseId", "Revision" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_EvaFirstHandoffProxies_RevisionId",
            table: "EvaFirstHandoffProxies",
            column: "RevisionId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_EvaHandoffOperations_CaseId_RecordedAtUtc",
            table: "EvaHandoffOperations",
            columns: new[] { "CaseId", "RecordedAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_EvaHandoffOperations_OperationKey",
            table: "EvaHandoffOperations",
            column: "OperationKey",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_EvaHandoffOperations_RevisionId",
            table: "EvaHandoffOperations",
            column: "RevisionId");

        migrationBuilder.Sql(
            """
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT, INSERT ON OBJECT::[dbo].[EvaHandoffRevisions] TO [pegasus_web_runtime_role];
                GRANT SELECT, INSERT ON OBJECT::[dbo].[EvaHandoffOperations] TO [pegasus_web_runtime_role];
                GRANT SELECT, INSERT ON OBJECT::[dbo].[EvaFirstHandoffProxies] TO [pegasus_web_runtime_role];
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
            BEGIN
                REVOKE SELECT, INSERT ON OBJECT::[dbo].[EvaFirstHandoffProxies] FROM [pegasus_web_runtime_role];
                REVOKE SELECT, INSERT ON OBJECT::[dbo].[EvaHandoffOperations] FROM [pegasus_web_runtime_role];
                REVOKE SELECT, INSERT ON OBJECT::[dbo].[EvaHandoffRevisions] FROM [pegasus_web_runtime_role];
            END;
            """);

        migrationBuilder.DropTable(name: "EvaFirstHandoffProxies");
        migrationBuilder.DropTable(name: "EvaHandoffOperations");
        migrationBuilder.DropTable(name: "EvaHandoffRevisions");
    }
}

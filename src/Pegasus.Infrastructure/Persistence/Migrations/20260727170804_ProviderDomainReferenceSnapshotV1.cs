using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProviderDomainReferenceSnapshotV1 : Migration
    {
        private const string Version = "provider-domains-v1";
        private const string PackageSha256 = "67c5b9959b3546f75aabd89511d8568ed7f594a8d905bfba47e3a23b828905c0";
        private const string SourcePath = "reference/workproviders-and-repairers/initial.xlsx";
        private const string SourceSha256 = "e4bf89b0aeef3f1106bf34ed50f74dffc44c5ed748e0ad0811b66ee099b6cd29";
        private static readonly string[] PackageColumns =
            ["Version", "SchemaVersion", "PackageSha256", "SourcePath", "SourceContentSha256", "SourceSheet", "SourceRowCount"];
        private static readonly string[] ProviderColumns = ["Version", "Code", "SourceRow"];
        private static readonly string[] EvidenceColumns = ["Version", "Code", "DomainSuffix"];
        private static readonly string[] ProviderEvidenceIndexColumns = ["Version", "DomainSuffix"];
        private static readonly string[] ProviderForeignKeyColumns = ["Version", "Code"];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProviderDomainPackages",
                columns: table => new
                {
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    PackageSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourcePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SourceContentSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceSheet = table.Column<string>(type: "nvarchar(31)", maxLength: 31, nullable: false),
                    SourceRowCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderDomainPackages", x => x.Version);
                    table.CheckConstraint("CK_ProviderDomainPackages_SchemaVersion", "[SchemaVersion] > 0");
                    table.CheckConstraint("CK_ProviderDomainPackages_SourceRowCount", "[SourceRowCount] > 0");
                });

            migrationBuilder.CreateTable(
                name: "ProviderReferences",
                columns: table => new
                {
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceRow = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderReferences", x => new { x.Version, x.Code });
                    table.CheckConstraint("CK_ProviderReferences_SourceRow", "[SourceRow] > 0");
                    table.ForeignKey(
                        name: "FK_ProviderReferences_ProviderDomainPackages_Version",
                        column: x => x.Version,
                        principalTable: "ProviderDomainPackages",
                        principalColumn: "Version",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderDomainEvidence",
                columns: table => new
                {
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DomainSuffix = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderDomainEvidence", x => new { x.Version, x.Code, x.DomainSuffix });
                    table.ForeignKey(
                        name: "FK_ProviderDomainEvidence_ProviderReferences_Version_Code",
                        columns: x => new { x.Version, x.Code },
                        principalTable: "ProviderReferences",
                        principalColumns: ProviderForeignKeyColumns,
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderDomainEvidence_Version_DomainSuffix",
                table: "ProviderDomainEvidence",
                columns: ProviderEvidenceIndexColumns);

            migrationBuilder.InsertData(
                table: "ProviderDomainPackages",
                columns: PackageColumns,
                values: new object[]
                {
                    Version,
                    1,
                    PackageSha256,
                    SourcePath,
                    SourceSha256,
                    "Sheet1",
                    11
                });

            migrationBuilder.InsertData(
                table: "ProviderReferences",
                columns: ProviderColumns,
                values: new object[,]
                {
                    { Version, "AX", 3 },
                    { Version, "BLACK", 8 },
                    { Version, "DFD", 9 },
                    { Version, "FW", 5 },
                    { Version, "KBS", 11 },
                    { Version, "MP", 10 },
                    { Version, "OAK", 6 },
                    { Version, "PCH", 2 },
                    { Version, "QCL", 4 },
                    { Version, "QDOS", 1 },
                    { Version, "RJS", 7 }
                });

            migrationBuilder.InsertData(
                table: "ProviderDomainEvidence",
                columns: EvidenceColumns,
                values: new object[,]
                {
                    { Version, "AX", "@ax-uk.com" },
                    { Version, "BLACK", "@blackstone-legal.co.uk" },
                    { Version, "DFD", "@dfd-solicitors.co.uk" },
                    { Version, "FW", "@fairwaylegal.co.uk" },
                    { Version, "KBS", "@knightsbridgesolicitors.co.uk" },
                    { Version, "MP", "@montrealprestige.co.uk" },
                    { Version, "OAK", "@oakwoodscotland.co.uk" },
                    { Version, "OAK", "@oakwoodsolicitors.co.uk" },
                    { Version, "PCH", "@connexus.co.uk" },
                    { Version, "PCH", "@ensurance-claims.co.uk" },
                    { Version, "PCH", "@pch-ltd.com" },
                    { Version, "QCL", "@qc-law.co.uk" },
                    { Version, "QDOS", "@qdosassist.co.uk" },
                    { Version, "QDOS", "@qdosassists.co.uk" },
                    { Version, "QDOS", "@qdoslaw.co.uk" },
                    { Version, "RJS", "@robertjameslaw.co.uk" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderDomainEvidence");

            migrationBuilder.DropTable(
                name: "ProviderReferences");

            migrationBuilder.DropTable(
                name: "ProviderDomainPackages");
        }
    }
}

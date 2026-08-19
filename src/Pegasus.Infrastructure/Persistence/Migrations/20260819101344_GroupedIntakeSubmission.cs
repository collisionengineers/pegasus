using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GroupedIntakeSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntakeSubmissionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceChannel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SubmissionToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeSubmissionGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntakeSubmissionGroupMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    StagedReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeSubmissionGroupMembers", x => x.Id);
                    table.CheckConstraint("CK_IntakeSubmissionGroupMembers_Ordinal", "[Ordinal] >= 0");
                    table.ForeignKey(
                        name: "FK_IntakeSubmissionGroupMembers_IntakeStagedReceipts_StagedReceiptId",
                        column: x => x.StagedReceiptId,
                        principalTable: "IntakeStagedReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeSubmissionGroupMembers_IntakeSubmissionGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "IntakeSubmissionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeSubmissionGroupMembers_GroupId_Ordinal",
                table: "IntakeSubmissionGroupMembers",
                columns: new[] { "GroupId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeSubmissionGroupMembers_StagedReceiptId",
                table: "IntakeSubmissionGroupMembers",
                column: "StagedReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeSubmissionGroups_SourceChannel_SubmissionToken",
                table: "IntakeSubmissionGroups",
                columns: new[] { "SourceChannel", "SubmissionToken" },
                unique: true);

            if (string.Equals(
                    ActiveProvider,
                    "Microsoft.EntityFrameworkCore.SqlServer",
                    StringComparison.Ordinal))
            {
                // Web is the only runtime that touches these tables:
                // EfIntakeSubmissionGroupStore (Get/Find/GetOrCreate/List) only
                // reads and, via GetOrCreateAsync/AddMemberAsync, inserts —
                // no update or delete. Worker never references
                // IntakeSubmissionGroup(s) at all, so it gets no grant here.
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT ON OBJECT::[dbo].[IntakeSubmissionGroups] TO [pegasus_web_runtime_role];");
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT ON OBJECT::[dbo].[IntakeSubmissionGroupMembers] TO [pegasus_web_runtime_role];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeSubmissionGroupMembers");

            migrationBuilder.DropTable(
                name: "IntakeSubmissionGroups");
        }
    }
}

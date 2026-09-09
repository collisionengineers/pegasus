using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260909146000_ManualCaseCreation")]
public partial class ManualCaseCreation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "OriginIntakeReceiptId",
            table: "Cases",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.AlterColumn<string>(
            name: "SourceReaderVersion",
            table: "CaseDataSnapshots",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(200)",
            oldMaxLength: 200);
        migrationBuilder.AlterColumn<string>(
            name: "SourceReaderKey",
            table: "CaseDataSnapshots",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(100)",
            oldMaxLength: 100);
        migrationBuilder.AlterColumn<string>(
            name: "OriginSourceHash",
            table: "CaseDataSnapshots",
            type: "nchar(64)",
            fixedLength: true,
            maxLength: 64,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nchar(64)",
            oldFixedLength: true,
            oldMaxLength: 64);
        migrationBuilder.AlterColumn<string>(
            name: "OriginSourceChannel",
            table: "CaseDataSnapshots",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(40)",
            oldMaxLength: 40);
        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "OriginReceivedAtUtc",
            table: "CaseDataSnapshots",
            type: "datetimeoffset",
            nullable: true,
            oldClrType: typeof(DateTimeOffset),
            oldType: "datetimeoffset");
        migrationBuilder.AlterColumn<string>(
            name: "OriginExternalReceiptToken",
            table: "CaseDataSnapshots",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(200)",
            oldMaxLength: 200);
        migrationBuilder.AlterColumn<Guid>(
            name: "OriginIntakeReceiptId",
            table: "CaseDataSnapshots",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("This pre-release manual case creation migration is forward-only.");
}

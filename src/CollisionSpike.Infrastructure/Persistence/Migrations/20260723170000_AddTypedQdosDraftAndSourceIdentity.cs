using System;
using CollisionSpike.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CollisionSpike.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CollisionSpikeDbContext))]
[Migration("20260723170000_AddTypedQdosDraftAndSourceIdentity")]
public sealed class AddTypedQdosDraftAndSourceIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_QdosIntakeReceipts_SourceHash",
            table: "QdosIntakeReceipts");

        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalReceiptToken",
                table: "QdosIntakeReceipts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
        else if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalReceiptToken",
                table: "QdosIntakeReceipts",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
        else
        {
            throw new NotSupportedException($"The {ActiveProvider} database provider is not supported.");
        }

        migrationBuilder.AddColumn<string>(
            name: "SourceChannel",
            table: "QdosIntakeReceipts",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "ManualUpload");

        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql("""
                UPDATE [QdosIntakeReceipts]
                SET [ExternalReceiptToken] = LOWER(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''))
                WHERE [ExternalReceiptToken] IS NULL;
                """);
        }
        else if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            migrationBuilder.Sql("""
                UPDATE "QdosIntakeReceipts"
                SET "ExternalReceiptToken" = lower(hex(randomblob(16)))
                WHERE "ExternalReceiptToken" IS NULL OR "ExternalReceiptToken" = '';
                """);
        }
        else
        {
            throw new NotSupportedException($"The {ActiveProvider} database provider is not supported.");
        }

        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.AlterColumn<string>(
                name: "ExternalReceiptToken",
                table: "QdosIntakeReceipts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }

        migrationBuilder.CreateTable(
            name: "QdosTypedDrafts",
            columns: table => new
            {
                IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PrincipalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                ClaimantName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                ClaimNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                VehicleRegistration = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                VehicleMake = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                VehicleModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                VehicleMileage = table.Column<long>(type: "bigint", nullable: true),
                AccidentCircumstances = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                DateOfIncident = table.Column<DateOnly>(type: "date", nullable: true),
                InstructionDate = table.Column<DateOnly>(type: "date", nullable: true),
                InspectionAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QdosTypedDrafts", item => item.IntakeReceiptId);
                table.ForeignKey(
                    name: "FK_QdosTypedDrafts_QdosIntakeReceipts_IntakeReceiptId",
                    column: item => item.IntakeReceiptId,
                    principalTable: "QdosIntakeReceipts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql("""
                INSERT INTO [QdosTypedDrafts] ([IntakeReceiptId], [PrincipalCode])
                SELECT [Id], N'QDOS'
                FROM [QdosIntakeReceipts]
                WHERE [Decision] = N'ConfirmedQdos';
                """);
        }
        else if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            migrationBuilder.Sql("""
                INSERT INTO "QdosTypedDrafts" ("IntakeReceiptId", "PrincipalCode")
                SELECT "Id", 'QDOS'
                FROM "QdosIntakeReceipts"
                WHERE "Decision" = 'ConfirmedQdos';
                """);
        }
        else
        {
            throw new NotSupportedException($"The {ActiveProvider} database provider is not supported.");
        }

        migrationBuilder.CreateIndex(
            name: "IX_QdosIntakeReceipts_SourceChannel_ExternalReceiptToken",
            table: "QdosIntakeReceipts",
            columns: ["SourceChannel", "ExternalReceiptToken"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_QdosIntakeReceipts_SourceHash",
            table: "QdosIntakeReceipts",
            column: "SourceHash");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "QdosTypedDrafts");

        migrationBuilder.DropIndex(
            name: "IX_QdosIntakeReceipts_SourceChannel_ExternalReceiptToken",
            table: "QdosIntakeReceipts");

        migrationBuilder.DropIndex(
            name: "IX_QdosIntakeReceipts_SourceHash",
            table: "QdosIntakeReceipts");

        migrationBuilder.DropColumn(
            name: "ExternalReceiptToken",
            table: "QdosIntakeReceipts");

        migrationBuilder.DropColumn(
            name: "SourceChannel",
            table: "QdosIntakeReceipts");

        migrationBuilder.CreateIndex(
            name: "IX_QdosIntakeReceipts_SourceHash",
            table: "QdosIntakeReceipts",
            column: "SourceHash",
            unique: true);
    }
}

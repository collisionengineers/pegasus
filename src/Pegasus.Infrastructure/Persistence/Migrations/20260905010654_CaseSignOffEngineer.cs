using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaseSignOffEngineer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_EvaSubmissions_CaseDelivered",
                table: "EvaSubmissions");

            migrationBuilder.AddColumn<Guid>(
                name: "SignOffEngineerId",
                table: "CaseWorkflows",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignOffEngineerId",
                table: "CaseWorkflows");

            migrationBuilder.CreateIndex(
                name: "UX_EvaSubmissions_CaseDelivered",
                table: "EvaSubmissions",
                column: "CaseId",
                unique: true,
                filter: "[IsDelivered] = 1");
        }
    }
}

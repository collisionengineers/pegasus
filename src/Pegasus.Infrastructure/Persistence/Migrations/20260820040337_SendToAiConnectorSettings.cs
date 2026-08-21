using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SendToAiConnectorSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChannelBaseUrl",
                table: "SendToAiControl",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChannelTokenProtected",
                table: "SendToAiControl",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TimeoutSeconds",
                table: "SendToAiControl",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TokenRotatedAtUtc",
                table: "SendToAiControl",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChannelBaseUrl",
                table: "SendToAiControl");

            migrationBuilder.DropColumn(
                name: "ChannelTokenProtected",
                table: "SendToAiControl");

            migrationBuilder.DropColumn(
                name: "TimeoutSeconds",
                table: "SendToAiControl");

            migrationBuilder.DropColumn(
                name: "TokenRotatedAtUtc",
                table: "SendToAiControl");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A Case has no Case contact and no Notes from client (operator, 2 November 2026; v43 D and E,
/// superseding R462). The Case data facts that held them are deleted, and the Principal API's
/// file handler, whose only use was to fill the Case contact, loses its three instruction draft
/// columns. Destructive and forward-only: the deleted values cannot be restored.
/// </summary>
public partial class RemoveCaseContactAndClientNotes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DELETE FROM [CaseDataFields] WHERE [FieldName] IN "
            + "(N'contact_name', N'contact_email_address', N'contact_phone_number', N'client_notes');");

        migrationBuilder.DropColumn(
            name: "FileHandlerEmailAddress",
            table: "InstructionDrafts");

        migrationBuilder.DropColumn(
            name: "FileHandlerName",
            table: "InstructionDrafts");

        migrationBuilder.DropColumn(
            name: "FileHandlerPhoneNumber",
            table: "InstructionDrafts");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "The deleted Case contact and Notes from client values cannot be restored.");
}

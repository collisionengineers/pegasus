using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Work Centre's Open tasks read each task's creation instant and creator
/// from its <c>case_task_created</c> history row. A task now stores both: the
/// columns are added, filled from that history (the earliest row for the task;
/// the creator only when its actor was a staff member with a non-empty
/// identifier), and the instant is then made NOT NULL with no default, so a
/// task whose history is missing stops the migration rather than being given an
/// invented instant. Additive: two columns, no table recreated, no data lost.
/// Once it has run, a package that does not write the instant cannot add a task.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261018070000_CaseTaskCreation")]
public partial class CaseTaskCreation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CreatedAtUtc",
            table: "CaseTasks",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CreatedByStaffId",
            table: "CaseTasks",
            type: "uniqueidentifier",
            nullable: true);

        // The history's aggregate id is the task id in "D" form (lowercase); the
        // creator is read as StaffNotificationPolicy.StaffId reads an actor.
        migrationBuilder.Sql(
            """
            UPDATE task
            SET [CreatedAtUtc] = created.[OccurredAtUtc],
                [CreatedByStaffId] = CASE
                    WHEN created.[ActorKind] = N'Staff'
                        AND TRY_CONVERT(uniqueidentifier, created.[ActorSubjectId])
                            <> '00000000-0000-0000-0000-000000000000'
                    THEN TRY_CONVERT(uniqueidentifier, created.[ActorSubjectId])
                END
            FROM dbo.CaseTasks AS task
            JOIN (
                SELECT [AggregateId], [OccurredAtUtc], [ActorKind], [ActorSubjectId],
                    ROW_NUMBER() OVER (
                        PARTITION BY [AggregateId] ORDER BY [OccurredAtUtc], [Id]) AS [Position]
                FROM dbo.ActionHistory
                WHERE [AggregateType] = N'case_task' AND [EventKind] = N'case_task_created') AS created
                ON created.[AggregateId] = LOWER(CONVERT(nvarchar(36), task.[Id]))
                AND created.[Position] = 1;

            IF EXISTS (SELECT 1 FROM dbo.CaseTasks WHERE [CreatedAtUtc] IS NULL)
                THROW 51000, 'A case task has no case_task_created history row to take its creation from. Resolve it before applying CaseTaskCreation.', 1;
            """);

        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "CreatedAtUtc",
            table: "CaseTasks",
            type: "datetimeoffset",
            nullable: false,
            oldClrType: typeof(DateTimeOffset),
            oldType: "datetimeoffset",
            oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CreatedByStaffId",
            table: "CaseTasks");

        migrationBuilder.DropColumn(
            name: "CreatedAtUtc",
            table: "CaseTasks");
    }
}

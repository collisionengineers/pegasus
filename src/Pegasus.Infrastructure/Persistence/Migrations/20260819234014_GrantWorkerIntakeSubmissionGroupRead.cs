using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantWorkerIntakeSubmissionGroupRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 20260819101344_GroupedIntakeSubmission granted only
            // pegasus_web_runtime_role, reasoning "EfIntakeSubmissionGroupStore
            // (Get/Find/GetOrCreate/List) ... the Worker never references
            // either table". That reasoning was wrong: INTK-011's production
            // investigation confirmed ImageIntakeAutomation.TryApplyGroupAsync
            // (called from the Worker's ProcessQueuedIntake pipeline) calls
            // IIntakeSubmissionGroupStore.FindForMemberSourceAsync/ListMembersAsync
            // at runtime to apply a grouped image submission's outcome. Grant
            // the Worker read-only access to match its actual caller; it never
            // writes these tables (creation/append stays Web-only, from the
            // Upload page's grouped submission).
            if (string.Equals(
                    ActiveProvider,
                    "Microsoft.EntityFrameworkCore.SqlServer",
                    StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    "GRANT SELECT ON OBJECT::[dbo].[IntakeSubmissionGroups] TO [pegasus_worker_runtime_role];");
                migrationBuilder.Sql(
                    "GRANT SELECT ON OBJECT::[dbo].[IntakeSubmissionGroupMembers] TO [pegasus_worker_runtime_role];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (string.Equals(
                    ActiveProvider,
                    "Microsoft.EntityFrameworkCore.SqlServer",
                    StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    "REVOKE SELECT ON OBJECT::[dbo].[IntakeSubmissionGroupMembers] FROM [pegasus_worker_runtime_role];");
                migrationBuilder.Sql(
                    "REVOKE SELECT ON OBJECT::[dbo].[IntakeSubmissionGroups] FROM [pegasus_worker_runtime_role];");
            }
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnidentifiedWork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnidentifiedItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OriginKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OriginId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SafeDetail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedByActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedByActorRolesJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResolvedByActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ResolvedByActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResolvedByActorRolesJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResolutionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResolutionTargetKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ResolutionTargetId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResolutionTargetReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RegistrationOperationKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RegistrationFingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnidentifiedItems", x => x.Id);
                    table.CheckConstraint("CK_UnidentifiedItems_Sequence", "[Sequence] > 0");
                    table.CheckConstraint("CK_UnidentifiedItems_Version", "[Version] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "UnidentifiedSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LastAllocatedSequence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnidentifiedSequences", x => x.Id);
                    table.CheckConstraint("CK_UnidentifiedSequences_LastAllocatedSequence", "[LastAllocatedSequence] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "UnidentifiedHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnidentifiedItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NewState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorRolesJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TargetKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    TargetId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TargetReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnidentifiedHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnidentifiedHistory_UnidentifiedItems_UnidentifiedItemId",
                        column: x => x.UnidentifiedItemId,
                        principalTable: "UnidentifiedItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnidentifiedHistory_OperationKey",
                table: "UnidentifiedHistory",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnidentifiedHistory_UnidentifiedItemId_OccurredAtUtc",
                table: "UnidentifiedHistory",
                columns: new[] { "UnidentifiedItemId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UnidentifiedItems_OriginKind_OriginId",
                table: "UnidentifiedItems",
                columns: new[] { "OriginKind", "OriginId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnidentifiedItems_Reference",
                table: "UnidentifiedItems",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnidentifiedItems_RegistrationOperationKey",
                table: "UnidentifiedItems",
                column: "RegistrationOperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnidentifiedItems_Sequence",
                table: "UnidentifiedItems",
                column: "Sequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnidentifiedItems_State_CreatedAtUtc_Sequence",
                table: "UnidentifiedItems",
                columns: new[] { "State", "CreatedAtUtc", "Sequence" });

            migrationBuilder.InsertData(
                table: "UnidentifiedSequences",
                columns: new[] { "Id", "LastAllocatedSequence" },
                values: new object[] { 1, 0L });

            // Backfill the legacy durable destination once.  The old decision is
            // retained on the receipt for rolling compatibility, while the new
            // aggregate becomes the operator-facing identity.  Ordering by the
            // received timestamp and receipt GUID makes fixtures deterministic and
            // avoids MAX + 1 allocation.
            migrationBuilder.Sql(@"
;WITH Legacy AS
(
    SELECT
        Id,
        ReceivedAtUtc,
        Decision,
        CASE Decision
            WHEN 'unsupported' THEN 'UnsupportedContent'
            WHEN 'technical_failure' THEN 'TechnicalProcessingFailure'
            WHEN 'ocr_required' THEN 'NoUsableIdentification'
            ELSE 'NoUsableIdentification'
        END AS MappedReasonCode,
        -- Mirrors ProcessIntake.RegisterUnidentifiedIfTerminalAsync's live
        -- SafeDetail source (receipt.FailureReason ?? receipt.DecisionReason):
        -- FailureReason carries the precise bounded explanation for
        -- unsupported/ocr_required/technical_failure receipts, while
        -- DecisionReason is the generic outcome text.
        LEFT(COALESCE(
            NULLIF(LTRIM(RTRIM(FailureReason)), ''),
            NULLIF(LTRIM(RTRIM(DecisionReason)), ''),
            'Migrated legacy unidentified intake.'), 1000) AS SafeDetailValue,
        ROW_NUMBER() OVER (ORDER BY ReceivedAtUtc, Id) AS AllocationSequence
    FROM IntakeReceipts
    WHERE Decision IN ('needs_sorting', 'unsupported', 'ocr_required', 'technical_failure')
)
INSERT INTO UnidentifiedItems
(
    Id, Sequence, Reference, OriginKind, OriginId, ReasonCode, SafeDetail, State,
    CreatedAtUtc, ResolvedAtUtc, CreatedByActorKind, CreatedByActorSubjectId,
    CreatedByActorRolesJson, ResolvedByActorKind, ResolvedByActorSubjectId,
    ResolvedByActorRolesJson, ResolutionReason, ResolutionTargetKind,
    ResolutionTargetId, ResolutionTargetReference, RegistrationOperationKey,
    RegistrationFingerprint, Version
)
SELECT
    NEWID(),
    AllocationSequence,
    CONCAT('U', AllocationSequence),
    'Receipt',
    Id,
    MappedReasonCode,
    SafeDetailValue,
    'Open',
    ReceivedAtUtc,
    NULL,
    'SystemWorker',
    'unidentified-migration',
    '[]',
    NULL, NULL, NULL, NULL, NULL, NULL, NULL,
    CONCAT('unidentified-migration:', CONVERT(varchar(36), Id)),
    -- A real per-row fingerprint, not a shared placeholder: every backfilled
    -- row previously got the same all-zero value, so any row would collide
    -- with the very next one on the OriginKind/OriginId uniqueness check
    -- above, and a later reevaluation of the same receipt that reaches the
    -- same terminal outcome would always conflict instead of replaying.
    -- Computed the same way EfUnidentifiedStore.Fingerprint does (upper-case
    -- SHA-256 hex of the pipe-joined identity fields), using the runtime
    -- registration actor (SystemWorker / intake-processing, from
    -- ActionActor.SystemWorker(""intake-processing"") in ProcessIntake) so a
    -- live reevaluation's freshly computed fingerprint can match it -- not
    -- this migration's own audit actor above, which only records who ran
    -- the backfill.
    UPPER(CONVERT(varchar(64), HASHBYTES('SHA2_256', CAST(CONCAT(
        'Receipt', '|', CONVERT(varchar(36), Id), '|', MappedReasonCode, '|',
        SafeDetailValue, '|', 'SystemWorker', '|', 'intake-processing') AS varchar(max))), 2)),
    0
FROM Legacy;

UPDATE UnidentifiedSequences
SET LastAllocatedSequence = COALESCE((SELECT MAX(Sequence) FROM UnidentifiedItems), 0)
WHERE Id = 1;

INSERT INTO UnidentifiedHistory
(
    Id, UnidentifiedItemId, PreviousState, NewState, ActorKind, ActorSubjectId,
    ActorRolesJson, OccurredAtUtc, Reason, OperationKey, TargetKind, TargetId,
    TargetReference
)
SELECT
    NEWID(),
    item.Id,
    'Open', 'Open', 'SystemWorker', 'unidentified-migration', '[]',
    item.CreatedAtUtc,
    -- UnidentifiedHistory.Reason is nvarchar(500) but SafeDetail can be up to
    -- 1000 chars (see EfUnidentifiedStore.RegisterAsync's own history insert,
    -- which truncates the same way); a longer value would fail this INSERT.
    LEFT(item.SafeDetail, 500),
    item.RegistrationOperationKey,
    NULL, NULL, NULL
FROM UnidentifiedItems AS item
WHERE item.RegistrationOperationKey LIKE 'unidentified-migration:%';");

            // Per-object least privilege, evidenced against actual callers:
            // - UnidentifiedItems: Worker gets SELECT/INSERT/UPDATE.
            //   RegisterAsync (EfUnidentifiedStore.cs) is reached only through
            //   IRegisterUnidentified, which only ProcessIntake consumes, and
            //   ProcessIntake's sole production caller is
            //   ProcessQueuedIntake.ExecuteAsync (DurableIntake.cs, run by
            //   Worker's UnifiedWorkFunction) via ExecuteRetainedAsync — hence
            //   SELECT (its existing-by-operation/-origin lookups) and INSERT.
            //   Worker also gets UPDATE: ProcessQueuedIntake.SynchronizeUnidentifiedAsync
            //   (DurableIntake.cs) calls IResolveUnidentified.ExecuteAsync to
            //   reconcile a stale open item once a receipt reaches CaseCreated
            //   or ImageIntakeRegistered, and ResolveAsync mutates the entity.
            //   Web gets SELECT/UPDATE, not INSERT: nothing in Pegasus.Web
            //   calls IRegisterUnidentified (no manual-registration surface);
            //   Web reads via Unidentified/Index.cshtml.cs and Details.cshtml.cs
            //   and UnidentifiedMcpTools.cs, and gets UPDATE from the same
            //   IResolveUnidentified path via Details.cshtml.cs's resolve
            //   handler and UnidentifiedMcpTools.ResolveAsync.
            // - UnidentifiedSequences: only RegisterAsync ever touches this
            //   table (select-or-seed the row, then increment
            //   LastAllocatedSequence), and that is the Worker-only path
            //   above; Web never allocates a sequence, so it gets nothing here.
            // - UnidentifiedHistory: append-only, both roles write it.
            //   RegisterAsync's initial row and ResolveAsync's resolution row
            //   both insert here, and both are reached from Worker (as above)
            //   and from Web (Details.cshtml.cs's resolve handler and
            //   UnidentifiedMcpTools.ResolveAsync); ResolveAsync also SELECTs
            //   it for its own replay check, and Details.cshtml.cs and
            //   UnidentifiedMcpTools.GetAsync call HistoryAsync (SELECT)
            //   directly. No caller ever updates or deletes a row.
            if (string.Equals(
                    ActiveProvider,
                    "Microsoft.EntityFrameworkCore.SqlServer",
                    StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    "GRANT SELECT, UPDATE ON OBJECT::[dbo].[UnidentifiedItems] TO [pegasus_web_runtime_role];");
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[UnidentifiedItems] TO [pegasus_worker_runtime_role];");
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[UnidentifiedSequences] TO [pegasus_worker_runtime_role];");
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT ON OBJECT::[dbo].[UnidentifiedHistory] TO [pegasus_web_runtime_role];");
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT ON OBJECT::[dbo].[UnidentifiedHistory] TO [pegasus_worker_runtime_role];");
                migrationBuilder.Sql(
                    "DENY UPDATE, DELETE ON OBJECT::[dbo].[UnidentifiedHistory] TO [pegasus_web_runtime_role];");
                migrationBuilder.Sql(
                    "DENY UPDATE, DELETE ON OBJECT::[dbo].[UnidentifiedHistory] TO [pegasus_worker_runtime_role];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnidentifiedHistory");

            migrationBuilder.DropTable(
                name: "UnidentifiedSequences");

            migrationBuilder.DropTable(
                name: "UnidentifiedItems");
        }
    }
}

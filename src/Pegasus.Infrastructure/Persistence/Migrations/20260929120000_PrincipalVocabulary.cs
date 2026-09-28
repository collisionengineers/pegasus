using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The work-provider "Provider" vocabulary becomes the canonical "Principal"
/// (#857). Tables, columns, constraints and indexes are renamed, and every
/// persisted code the renamed source now writes is rewritten to it, so no row
/// keeps a spelling the application can no longer read.
///
/// Renamed: <c>ProviderSubmissions</c> (and its <c>ProviderReference</c>
/// column), <c>ProviderDomainPackages</c>, <c>ProviderReferences</c>,
/// <c>ProviderDomainEvidence</c>, <c>CaseMatchIndex.WorkProviderCode</c> and
/// <c>IntakeMailRouteDecisions.WorkProviderCode</c>. SQL Server keeps a table's
/// permission rows across a rename; the runtime grants are restated anyway so
/// the bootstrap permission matrix reads them under the new names.
///
/// Rewritten: source-channel, source-kind, route, evidence-source, reader,
/// policy, actor-kind, lifecycle, security-reason and operation-key codes,
/// plus the evidence JSON that names an evidence source. The two check
/// constraints that list codes (<c>CK_CaseDataFields_SourceKind</c> and
/// <c>CK_OrganizationRoles_Role</c>) are dropped and recreated around the
/// rewrite. The package version <c>provider-domains-v1</c> and the package's
/// <c>providers</c> JSON key are the immutable, hash-pinned package identity and
/// are not rewritten.
/// Forward-only.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260929120000_PrincipalVocabulary")]
public partial class PrincipalVocabulary : Migration
{
    private const string WebRole = "pegasus_web_runtime_role";
    private const string WorkerRole = "pegasus_worker_runtime_role";

    /// <summary>Every column that stores the intake source-channel code.</summary>
    private static readonly (string Table, string Column)[] SourceChannelColumns =
    [
        ("ImageIntakes", "SourceChannel"),
        ("IntakeReceipts", "SourceChannel"),
        ("IntakeStagedReceipts", "SourceChannel"),
        ("IntakeSubmissionGroups", "SourceChannel"),
        ("Triage", "SourceChannel"),
        ("CaseDataSnapshots", "OriginSourceChannel")
    ];

    /// <summary>Every column that stores an <c>ActorKind</c> name.</summary>
    private static readonly (string Table, string Column)[] ActorKindColumns =
    [
        ("ActionHistory", "ActorKind"),
        ("CaseEditLeaseOperations", "ActorKind"),
        ("CaseIntakeLinks", "ActorKind"),
        ("CaseManualChases", "ActorKind"),
        ("CaseWorkflowEvents", "ActorKind"),
        ("ImageIntakeLifecycleEvents", "ActorKind"),
        ("ImageIntakes", "CreatedByActorKind"),
        ("IntakeAllocationAttempts", "ActorKind"),
        ("IntakeManualAssociations", "ActorKind"),
        ("IntakeMutationHistory", "ActorKind"),
        ("IntakeSubmissionGroupHistory", "ActorKind"),
        ("IntakeSubmissionGroups", "DiscardedByActorKind"),
        ("SecurityEvents", "ActorKind"),
        ("TriageHistory", "ActorKind"),
        ("UnidentifiedHistory", "ActorKind"),
        ("UnidentifiedItems", "CreatedByActorKind"),
        ("UnidentifiedItems", "ResolvedByActorKind")
    ];

    /// <summary>Columns that store the reader key of an intake source.</summary>
    private static readonly (string Table, string Column)[] ReaderKeyColumns =
    [
        ("CaseDataSnapshots", "SourceReaderKey"),
        ("IntakeReceipts", "SourceReaderKey"),
        ("IntakeSourceCandidates", "ReaderKey")
    ];

    /// <summary>Columns that store an extraction or field policy key.</summary>
    private static readonly (string Table, string Column)[] PolicyKeyColumns =
    [
        ("CaseDataFields", "PolicyKey"),
        ("CaseDataSnapshots", "ExtractionPolicyKey"),
        ("IntakeReceipts", "ExtractionPolicyKey"),
        ("IntakeSourceCandidates", "PolicyKey")
    ];

    /// <summary>Columns that store an asset's source label.</summary>
    private static readonly (string Table, string Column)[] SourceLabelColumns =
    [
        ("IntakeAssets", "SourceLabel"),
        ("IntakeSearchDocuments", "SourceLabel"),
        ("IntakeMailClassificationDecisions", "StandaloneAuditReportAssetSourceLabel")
    ];

    /// <summary>Columns that store an operation key with a channel or kind prefix.</summary>
    private static readonly (string Table, string Column)[] OperationKeyColumns =
    [
        ("CaseHistory", "OperationKey"),
        ("ImageIntakes", "CreationOperationKey"),
        ("IntakeSubmissionGroupHistory", "OperationKey"),
        ("IntakeWorkItems", "OperationKey")
    ];

    /// <summary>
    /// Security-event reason codes. The Principal mismatch code drops its
    /// doubled word rather than becoming <c>principal_principal_mismatch</c>.
    /// </summary>
    private static readonly (string Old, string New)[] SecurityReasonCodes =
    [
        ("provider_credential_rejected", "principal_credential_rejected"),
        ("provider_credential_missing", "principal_credential_missing"),
        ("provider_credential_paused", "principal_credential_paused"),
        ("provider_principal_mismatch", "principal_mismatch"),
        ("provider_api_rate_limited", "principal_api_rate_limited")
    ];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.DropCheckConstraint(
            name: "CK_CaseDataFields_SourceKind",
            table: "CaseDataFields");

        migrationBuilder.DropCheckConstraint(
            name: "CK_OrganizationRoles_Role",
            table: "OrganizationRoles");

        RenameTables(migrationBuilder);
        RenameColumns(migrationBuilder);
        RewriteCodes(migrationBuilder);

        migrationBuilder.AddCheckConstraint(
            name: "CK_CaseDataFields_SourceKind",
            table: "CaseDataFields",
            sql: "[SourceKind] IN ('intake_evidence', 'mail_route', 'case_acceptance', 'staff_correction', 'vehicle_lookup', 'principal_setting', 'principal_api')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_OrganizationRoles_Role",
            table: "OrganizationRoles",
            sql: "[Role] IN ('principal', 'instruction_intermediary')");

        RestateRuntimeGrants(migrationBuilder);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "PrincipalVocabulary is forward-only: it rewrites persisted codes to the Principal vocabulary. Restore from an approved backup instead.");

    private static void RenameTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(name: "ProviderSubmissions", newName: "PrincipalSubmissions");
        migrationBuilder.RenameTable(name: "ProviderDomainPackages", newName: "PrincipalDomainPackages");
        migrationBuilder.RenameTable(name: "ProviderReferences", newName: "PrincipalReferences");
        migrationBuilder.RenameTable(name: "ProviderDomainEvidence", newName: "PrincipalDomainEvidence");

        // A table rename leaves its constraints and indexes under the old
        // names; the model now expects the names EF derives from the new ones.
        foreach (var (old, renamed) in new[]
        {
            ("PK_ProviderSubmissions", "PK_PrincipalSubmissions"),
            ("FK_ProviderSubmissions_Principals_PrincipalId", "FK_PrincipalSubmissions_Principals_PrincipalId"),
            ("PK_ProviderDomainPackages", "PK_PrincipalDomainPackages"),
            ("CK_ProviderDomainPackages_SchemaVersion", "CK_PrincipalDomainPackages_SchemaVersion"),
            ("CK_ProviderDomainPackages_SourceRowCount", "CK_PrincipalDomainPackages_SourceRowCount"),
            ("PK_ProviderReferences", "PK_PrincipalReferences"),
            ("CK_ProviderReferences_SourceRow", "CK_PrincipalReferences_SourceRow"),
            ("FK_ProviderReferences_ProviderDomainPackages_Version", "FK_PrincipalReferences_PrincipalDomainPackages_Version"),
            ("PK_ProviderDomainEvidence", "PK_PrincipalDomainEvidence"),
            ("FK_ProviderDomainEvidence_ProviderReferences_Version_Code", "FK_PrincipalDomainEvidence_PrincipalReferences_Version_Code")
        })
        {
            migrationBuilder.Sql($"EXEC sp_rename N'[dbo].[{old}]', N'{renamed}', N'OBJECT';");
        }

        migrationBuilder.RenameIndex(
            name: "IX_ProviderSubmissions_PrincipalId_IdempotencyKey",
            table: "PrincipalSubmissions",
            newName: "IX_PrincipalSubmissions_PrincipalId_IdempotencyKey");

        migrationBuilder.RenameIndex(
            name: "IX_ProviderDomainEvidence_Version_DomainSuffix",
            table: "PrincipalDomainEvidence",
            newName: "IX_PrincipalDomainEvidence_Version_DomainSuffix");
    }

    private static void RenameColumns(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "ProviderReference",
            table: "PrincipalSubmissions",
            newName: "PrincipalReference");

        migrationBuilder.RenameColumn(
            name: "WorkProviderCode",
            table: "CaseMatchIndex",
            newName: "PrincipalCode");

        migrationBuilder.RenameColumn(
            name: "WorkProviderCode",
            table: "IntakeMailRouteDecisions",
            newName: "PrincipalCode");

        migrationBuilder.RenameIndex(
            name: "IX_CaseMatchIndex_WorkProviderCode_DurableClaimToken",
            table: "CaseMatchIndex",
            newName: "IX_CaseMatchIndex_PrincipalCode_DurableClaimToken");

        migrationBuilder.RenameIndex(
            name: "IX_CaseMatchIndex_WorkProviderCode_NormalizedSurname",
            table: "CaseMatchIndex",
            newName: "IX_CaseMatchIndex_PrincipalCode_NormalizedSurname");

        migrationBuilder.RenameIndex(
            name: "IX_CaseMatchIndex_WorkProviderCode_NormalizedVrm",
            table: "CaseMatchIndex",
            newName: "IX_CaseMatchIndex_PrincipalCode_NormalizedVrm");
    }

    private static void RewriteCodes(MigrationBuilder migrationBuilder)
    {
        foreach (var (table, column) in SourceChannelColumns)
        {
            SetCode(migrationBuilder, table, column, "provider_api", "principal_api");
        }

        foreach (var (table, column) in ActorKindColumns)
        {
            SetCode(migrationBuilder, table, column, "Provider", "Principal");
        }

        foreach (var (table, column) in ReaderKeyColumns)
        {
            SetCode(migrationBuilder, table, column, "provider_api_declaration", "principal_api_declaration");
        }

        foreach (var (table, column) in PolicyKeyColumns)
        {
            SetCode(migrationBuilder, table, column, "provider_api_declared_instruction", "principal_api_declared_instruction");
            SetCode(migrationBuilder, table, column, "provider-inspection-mode", "principal-inspection-mode");
        }

        foreach (var (table, column) in SourceLabelColumns)
        {
            SetCode(migrationBuilder, table, column, "provider-original-report", "principal-original-report");
            SetPrefix(migrationBuilder, table, column, "provider-file:", "principal-file:");
        }

        foreach (var (table, column) in OperationKeyColumns)
        {
            SetPrefix(migrationBuilder, table, column, "provider-api:", "principal-api:");
            SetPrefix(migrationBuilder, table, column, "provider-mode:", "principal-mode:");
            SetPrefix(migrationBuilder, table, column, "provider-note:", "principal-note:");
        }

        // Case data: the source kinds, the field name, and the label that names
        // the setting a value came from.
        SetCode(migrationBuilder, "CaseDataFields", "SourceKind", "provider_setting", "principal_setting");
        SetCode(migrationBuilder, "CaseDataFields", "SourceKind", "provider_api", "principal_api");
        SetCode(migrationBuilder, "CaseDataFields", "FieldName", "work_provider_code", "principal_code");
        SetPrefix(migrationBuilder, "CaseDataFields", "SourceLabel", "provider setting:", "principal setting:");
        SetCode(migrationBuilder, "CaseDataFields", "SourceLabel", "staff-corrected wrong-principal work provider", "staff-corrected wrong-principal principal");

        // Organization role and mail routing.
        SetCode(migrationBuilder, "OrganizationRoles", "Role", "work_provider", "principal");
        SetCode(migrationBuilder, "IntakeMailRouteDecisions", "RouteKind", "direct_provider", "direct_principal");
        SetCode(migrationBuilder, "IntakeMailClassificationDecisions", "Subtype", "provider-chasing-for-update", "principal-chasing-for-update");

        // The actor packed as "{kind}:{subjectId}" beside a mail classification.
        SetPrefix(migrationBuilder, "IntakeMailClassificationDecisions", "DecidedByActor", "provider:", "principal:");
        SetPrefix(migrationBuilder, "IntakeMailClassificationHistory", "Actor", "provider:", "principal:");

        // Failure codes a Principal submission's intake can record.
        foreach (var table in new[] { "IntakeReceipts", "IntakeWorkItems" })
        {
            SetCode(migrationBuilder, table, "FailureCode", "provider_existing_case_match", "principal_existing_case_match");
            SetCode(migrationBuilder, table, "FailureCode", "provider_submission_unreadable", "principal_submission_unreadable");
        }

        // Audit history: the submission aggregate and the applied-mode event.
        SetCode(migrationBuilder, "ActionHistory", "AggregateType", "ProviderSubmission", "PrincipalSubmission");
        SetCode(migrationBuilder, "CaseHistory", "EventType", "provider_inspection_mode_applied", "principal_inspection_mode_applied");

        foreach (var (old, renamed) in SecurityReasonCodes)
        {
            SetCode(migrationBuilder, "SecurityEvents", "ReasonCode", old, renamed);
        }

        // The Case lifecycle state and closure outcome, persisted by name.
        SetCode(migrationBuilder, "CaseWorkflows", "State", "ProviderCancelled", "PrincipalCancelled");
        SetCode(migrationBuilder, "CaseWorkflows", "ClosureOutcome", "ProviderCancelled", "PrincipalCancelled");
        ReplaceText(migrationBuilder, "CaseWorkflowEvents", "ResultJson", "\"ProviderCancelled\"", "\"PrincipalCancelled\"");

        // Evidence and field JSON name the evidence source and its label; both
        // are parsed strictly on read.
        foreach (var column in new[] { "EvidenceJson", "FieldsJson" })
        {
            ReplaceText(migrationBuilder, "IntakeReceipts", column, "\"provider_declaration\"", "\"principal_declaration\"");
            ReplaceText(migrationBuilder, "IntakeReceipts", column, "\"provider-original-report\"", "\"principal-original-report\"");
            ReplaceText(migrationBuilder, "IntakeReceipts", column, "\"provider-file:", "\"principal-file:");
        }
    }

    // The runtime roles' permissions follow the renamed tables. They are
    // restated so a migrated database and a fresh one read the same under the
    // new names: the bootstrap matrix lists them as the grants of
    // 20260828111732_GrantProviderSubmissions,
    // 20260829212237_GrantProviderSubmissionAcceptRecovery and
    // 20260729199000_RuntimeRoleReconciliation.
    private static void RestateRuntimeGrants(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF NOT EXISTS (
                SELECT 1 FROM sys.database_principals
                WHERE name = N'pegasus_web_runtime_role'
                  AND [type] = 'R'
                  AND is_fixed_role = 0
                  AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                THROW 51000, 'The fixed Pegasus Web runtime role is missing or invalid.', 1;
            IF NOT EXISTS (
                SELECT 1 FROM sys.database_principals
                WHERE name = N'pegasus_worker_runtime_role'
                  AND [type] = 'R'
                  AND is_fixed_role = 0
                  AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                THROW 51000, 'The fixed Pegasus Worker runtime role is missing or invalid.', 1;
            """);
        migrationBuilder.Sql(
            $"GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[PrincipalSubmissions] TO [{WebRole}];");
        migrationBuilder.Sql(
            $"GRANT SELECT, UPDATE ON OBJECT::[dbo].[PrincipalSubmissions] TO [{WorkerRole}];");
        foreach (var table in new[] { "PrincipalDomainEvidence", "PrincipalDomainPackages", "PrincipalReferences" })
        {
            migrationBuilder.Sql($"GRANT SELECT ON OBJECT::[dbo].[{table}] TO [{WebRole}];");
            migrationBuilder.Sql($"GRANT SELECT ON OBJECT::[dbo].[{table}] TO [{WorkerRole}];");
        }
    }

    private static void SetCode(MigrationBuilder migrationBuilder, string table, string column, string old, string renamed) =>
        migrationBuilder.Sql(
            $"UPDATE [dbo].[{table}] SET [{column}] = N'{renamed}' WHERE [{column}] = N'{old}';");

    // A prefix is matched by length rather than LIKE, which treats the "_"
    // some prefixes contain as a wildcard.
    private static void SetPrefix(MigrationBuilder migrationBuilder, string table, string column, string old, string renamed) =>
        migrationBuilder.Sql(
            $"UPDATE [dbo].[{table}] SET [{column}] = N'{renamed}' + SUBSTRING([{column}], {old.Length + 1}, LEN([{column}])) "
            + $"WHERE LEFT([{column}], {old.Length}) = N'{old}';");

    private static void ReplaceText(MigrationBuilder migrationBuilder, string table, string column, string old, string renamed) =>
        migrationBuilder.Sql(
            $"UPDATE [dbo].[{table}] SET [{column}] = REPLACE([{column}], N'{old}', N'{renamed}') "
            + $"WHERE CHARINDEX(N'{old}', [{column}]) > 0;");
}

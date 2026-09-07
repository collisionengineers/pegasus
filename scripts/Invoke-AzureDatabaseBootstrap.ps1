[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Environment,
    [Parameter(Mandatory)][string] $ManifestPath,
    [Parameter(Mandatory)][string] $ManifestSha256
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$manifestFile = Resolve-Path -LiteralPath $ManifestPath
if ($ManifestSha256 -notmatch '^[0-9a-fA-F]{64}$') {
    throw 'ManifestSha256 must be the operator-approved 64-character SHA-256.'
}
$actualManifestSha256 = (Get-FileHash -LiteralPath $manifestFile -Algorithm SHA256).Hash
if (-not $actualManifestSha256.Equals($ManifestSha256, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'release-manifest.json does not match the operator-approved SHA-256.'
}
& (Join-Path $PSScriptRoot 'Test-AzureDeploymentPlan.ps1') -Mode PreMigration -Environment $Environment -ManifestPath $manifestFile -ManifestSha256 $ManifestSha256
$manifest = Get-Content -Raw -LiteralPath $manifestFile | ConvertFrom-Json -Depth 10
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$currentRevision = (& git -C $repositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Unable to identify the database-bootstrap source revision.' }
$sourceStatus = @(& git -C $repositoryRoot status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Unable to verify the database-bootstrap source status.' }
if ($currentRevision -ne $manifest.sourceRevision -or $sourceStatus.Count -ne 0) {
    throw 'Database bootstrap requires the exact clean source revision recorded by the approved release manifest.'
}

function Get-AzdValues([string] $Name) {
    $result = @{}
    foreach ($line in (& azd env get-values -e $Name --no-prompt)) {
        if ($line -match '^([A-Z0-9_]+)=(.*)$') {
            $result[$Matches[1]] = $Matches[2].Trim('"')
        }
    }
    return $result
}

function Convert-GuidToSqlSid([string] $Value) {
    $guid = [Guid]::ParseExact($Value, 'D')
    return '0x' + [Convert]::ToHexString($guid.ToByteArray())
}

function Get-MigrationPermissionMatrix {
    $migrationPath = Join-Path $PSScriptRoot '../src/Pegasus.Infrastructure/Persistence/Migrations/20260729199000_RuntimeRoleReconciliation.cs'
    $source = Get-Content -Raw -LiteralPath $migrationPath
    function Read-Block([string] $Name) {
        $match = [regex]::Match(
            $source,
            "private static readonly [^=]+ $Name\s*=\s*\[(?<body>.*?)\];",
            [Text.RegularExpressions.RegexOptions]::Singleline)
        if (-not $match.Success) { throw "Unable to read $Name from the runtime-role migration." }
        return $match.Groups['body'].Value
    }

    $removedTables = @(
        '20260730203141_ThirdPartyVehicleEvidenceAndRemoveBootstrap.cs',
        '20260730203833_RemoveDormantOpenIddict.cs',
        '20260814094632_DropBoxFileRequests.cs',
        '20260824123336_DropEvaHandoffTables.cs'
    ) | ForEach-Object {
        $terminalSource = Get-Content -Raw -LiteralPath (Join-Path (Split-Path -Parent $migrationPath) $_)
        [regex]::Matches($terminalSource, 'DropTable\(\s*name:\s*"(?<table>[A-Za-z0-9]+)"') |
            ForEach-Object { $_.Groups['table'].Value }
    }
    $tables = [regex]::Matches((Read-Block 'RuntimeTables'), '"(?<table>[A-Za-z0-9]+)"') |
        ForEach-Object { $_.Groups['table'].Value } |
        Where-Object { $_ -notin $removedTables }
    $expected = [Collections.Generic.List[string]]::new()
    foreach ($definition in @(
        @{ Role = 'pegasus_web_runtime_role'; Block = 'WebGrants' },
        @{ Role = 'pegasus_worker_runtime_role'; Block = 'WorkerGrants' })) {
        foreach ($grant in [regex]::Matches((Read-Block $definition.Block), '\("(?<table>[A-Za-z0-9]+)", "(?<permissions>[A-Z, ]+)"\)')) {
            if ($grant.Groups['table'].Value -in $removedTables) { continue }
            foreach ($permission in $grant.Groups['permissions'].Value.Split(',').Trim()) {
                $expected.Add("$($definition.Role)|G|$permission|$($grant.Groups['table'].Value)")
            }
        }
    }
    # 20260801220500_GrantWebMigrationHistoryRead.
    $expected.Add('pegasus_web_runtime_role|G|SELECT|__EFMigrationsHistory')
    $webDeleteTables = @('AspNetUserRoles', 'CaseDataFields', 'OrganizationRoles', 'TriageResponseEvidenceLinks')
    foreach ($table in $tables) {
        if ($table -notin $webDeleteTables) {
            $expected.Add("pegasus_web_runtime_role|D|DELETE|$table")
        }
        $expected.Add("pegasus_worker_runtime_role|D|DELETE|$table")
    }

    # Grant-carrying migrations after the 2026-07-29 reconciliation. Each block
    # mirrors its migration's SQL exactly; a later grant-carrying migration
    # must extend this matrix or the release verify step throws.
    # 20260803071539_ImageIntakeRegistration (both roles, DELETE denied).
    foreach ($role in @('pegasus_web_runtime_role', 'pegasus_worker_runtime_role')) {
        foreach ($grant in @(
            @{ Table = 'ImageIntakes'; Permissions = 'SELECT', 'INSERT' },
            @{ Table = 'ImageIntakeSequences'; Permissions = 'SELECT', 'INSERT', 'UPDATE' },
            @{ Table = 'ImageVrmSuggestions'; Permissions = 'SELECT', 'INSERT', 'UPDATE' })) {
            foreach ($permission in $grant.Permissions) {
                $expected.Add("$role|G|$permission|$($grant.Table)")
            }
            $expected.Add("$role|D|DELETE|$($grant.Table)")
        }
        # 20260803151159_AutomationActorOpenIddict denies DELETE to both roles.
        foreach ($table in @('OpenIddictApplications', 'OpenIddictAuthorizations', 'OpenIddictScopes', 'OpenIddictTokens')) {
            $expected.Add("$role|D|DELETE|$table")
        }
    }
    # 20260803123935_MailClassificationDecisions: Worker replaces the decision
    # row during re-evaluation, the Web reads only.
    $expected.Add('pegasus_web_runtime_role|G|SELECT|IntakeMailClassificationDecisions')
    $expected.Add('pegasus_web_runtime_role|D|DELETE|IntakeMailClassificationDecisions')
    foreach ($permission in @('SELECT', 'INSERT', 'UPDATE', 'DELETE')) {
        $expected.Add("pegasus_worker_runtime_role|G|$permission|IntakeMailClassificationDecisions")
    }
    # 20260803125915_CaseMatchDecisionsAndAssociationPolicy: the Web's
    # acceptance-path projector replaces CaseMatchIndex rows in place; the
    # Worker owns the decision rows and the automatic-association writes.
    foreach ($permission in @('SELECT', 'INSERT', 'UPDATE', 'DELETE')) {
        $expected.Add("pegasus_web_runtime_role|G|$permission|CaseMatchIndex")
        $expected.Add("pegasus_worker_runtime_role|G|$permission|IntakeCaseMatchDecisions")
    }
    $expected.Add('pegasus_worker_runtime_role|G|SELECT|CaseMatchIndex')
    $expected.Add('pegasus_worker_runtime_role|D|DELETE|CaseMatchIndex')
    $expected.Add('pegasus_web_runtime_role|G|SELECT|IntakeCaseMatchDecisions')
    $expected.Add('pegasus_web_runtime_role|D|DELETE|IntakeCaseMatchDecisions')
    foreach ($permission in @('SELECT', 'INSERT', 'UPDATE')) {
        $expected.Add("pegasus_worker_runtime_role|G|$permission|IntakeManualAssociations")
    }
    $expected.Add('pegasus_worker_runtime_role|D|DELETE|IntakeManualAssociations')
    $expected.Add('pegasus_worker_runtime_role|G|SELECT|IntakeMutationHistory')
    $expected.Add('pegasus_worker_runtime_role|G|INSERT|IntakeMutationHistory')
    $expected.Add('pegasus_worker_runtime_role|D|DELETE|IntakeMutationHistory')
    # 20260803151159_AutomationActorOpenIddict Web grants (Worker gets none).
    foreach ($table in @('OpenIddictApplications', 'OpenIddictAuthorizations', 'OpenIddictTokens')) {
        foreach ($permission in @('SELECT', 'INSERT', 'UPDATE')) {
            $expected.Add("pegasus_web_runtime_role|G|$permission|$table")
        }
    }
    $expected.Add('pegasus_web_runtime_role|G|SELECT|OpenIddictScopes')
    # 20260803205759_SendToAiAssessmentToolset: Web owns assessment edits and
    # request/control writes; both roles remain unable to delete queue state.
    foreach ($table in @('CaseAssessmentFields', 'CaseEstimateLines')) {
        foreach ($permission in @('SELECT', 'INSERT', 'UPDATE', 'DELETE')) {
            $expected.Add("pegasus_web_runtime_role|G|$permission|$table")
        }
    }
    foreach ($table in @('AiWorkRequests', 'SendToAiControl')) {
        foreach ($permission in @('SELECT', 'INSERT', 'UPDATE')) {
            $expected.Add("pegasus_web_runtime_role|G|$permission|$table")
        }
        $expected.Add("pegasus_web_runtime_role|D|DELETE|$table")
        $expected.Add("pegasus_worker_runtime_role|D|DELETE|$table")
    }
    # 20260805223036_RetainedMailboxMessages: retained evidence is immutable;
    # Web reads it and Worker can only append it.
    foreach ($table in @('RetainedMailboxMessages', 'RetainedMailboxAttachments')) {
        $expected.Add("pegasus_web_runtime_role|G|SELECT|$table")
        $expected.Add("pegasus_worker_runtime_role|G|SELECT|$table")
        $expected.Add("pegasus_worker_runtime_role|G|INSERT|$table")
    }
    # 20260811063940_QdosAllocationRecovery: both runtimes execute the
    # begin/complete/cancel allocation-attempt transaction.
    foreach ($role in @('pegasus_web_runtime_role', 'pegasus_worker_runtime_role')) {
        foreach ($permission in @('SELECT', 'INSERT', 'UPDATE', 'DELETE')) {
            $expected.Add("$role|G|$permission|IntakeAllocationAttempts")
        }
    }
    # 20260814092852_AddWorkerCaseCreationGrants moves automatic case
    # acceptance to the least-privilege Worker role. Read the migration's
    # canonical grant block so release verification cannot drift from it.
    $workerCaseCreationMigration = Get-Content -Raw -LiteralPath (
        Join-Path (Split-Path -Parent $migrationPath) '20260814092852_AddWorkerCaseCreationGrants.cs')
    $workerGrantBlock = [regex]::Match(
        $workerCaseCreationMigration,
        'WorkerGrants\s*=\s*\[(?<body>.*?)\];',
        [Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $workerGrantBlock.Success) {
        throw 'Unable to read WorkerGrants from the Worker case-creation migration.'
    }
    foreach ($grant in [regex]::Matches(
        $workerGrantBlock.Groups['body'].Value,
        '\("(?<table>[A-Za-z0-9]+)", "(?<permissions>[A-Z, ]+)"\)')) {
        foreach ($permission in $grant.Groups['permissions'].Value.Split(',').Trim()) {
            $expected.Add("pegasus_worker_runtime_role|G|$permission|$($grant.Groups['table'].Value)")
        }
    }
    # 20260819104953_MailClassificationCorrectionHistory: adds an UPDATE grant
    # to the pre-existing SELECT on IntakeMailClassificationDecisions (the
    # Web's OnPostCorrectClassificationAsync handler corrects it in place),
    # and grants the new IntakeMailClassificationHistory audit trail SELECT,
    # INSERT with UPDATE and DELETE both denied (it is append-only).
    $expected.Add('pegasus_web_runtime_role|G|UPDATE|IntakeMailClassificationDecisions')
    foreach ($permission in @('SELECT', 'INSERT')) {
        $expected.Add("pegasus_web_runtime_role|G|$permission|IntakeMailClassificationHistory")
    }
    foreach ($permission in @('UPDATE', 'DELETE')) {
        $expected.Add("pegasus_web_runtime_role|D|$permission|IntakeMailClassificationHistory")
    }
    # 20260819112640_VersionedRepairSpecifications: Web owns repair-specification
    # draft/accept writes; the Worker does not call the store, so it gets none.
    foreach ($permission in @('SELECT', 'INSERT', 'UPDATE')) {
        $expected.Add("pegasus_web_runtime_role|G|$permission|CaseRepairSpecifications")
    }
    $expected.Add('pegasus_web_runtime_role|D|DELETE|CaseRepairSpecifications')
    # 20260819115323_UnidentifiedWork, per-object least privilege:
    # - UnidentifiedItems: Worker (UnifiedWorkFunction -> ProcessQueuedIntake ->
    #   ProcessIntake.ExecuteRetainedAsync -> IRegisterUnidentified.RegisterAsync)
    #   gets SELECT/INSERT/UPDATE; the UPDATE is
    #   ProcessQueuedIntake.SynchronizeUnidentifiedAsync's reconciliation
    #   resolve. Web never registers (no IRegisterUnidentified caller in
    #   Pegasus.Web), so it gets SELECT/UPDATE only, the UPDATE from
    #   Unidentified/Details.cshtml.cs's resolve handler and
    #   UnidentifiedMcpTools.ResolveAsync.
    # - UnidentifiedSequences: only RegisterAsync's allocation touches this
    #   table, and that is Worker-only; Web gets nothing.
    # - UnidentifiedHistory: append-only; both roles insert (Register's
    #   initial row, Resolve's resolution row) and select (Resolve's own
    #   replay check, and Details.cshtml.cs/UnidentifiedMcpTools.GetAsync's
    #   HistoryAsync reads). No caller ever updates or deletes a row.
    foreach ($permission in @('SELECT', 'UPDATE')) {
        $expected.Add("pegasus_web_runtime_role|G|$permission|UnidentifiedItems")
    }
    foreach ($permission in @('SELECT', 'INSERT', 'UPDATE')) {
        $expected.Add("pegasus_worker_runtime_role|G|$permission|UnidentifiedItems")
        $expected.Add("pegasus_worker_runtime_role|G|$permission|UnidentifiedSequences")
    }
    foreach ($role in @('pegasus_web_runtime_role', 'pegasus_worker_runtime_role')) {
        foreach ($permission in @('SELECT', 'INSERT')) {
            $expected.Add("$role|G|$permission|UnidentifiedHistory")
        }
        foreach ($permission in @('UPDATE', 'DELETE')) {
            $expected.Add("$role|D|$permission|UnidentifiedHistory")
        }
    }
    # 20260820100724_RetainedMailSearchDocuments: Web searches the immutable
    # projection; Worker creates/replaces it in the existing receipt transaction.
    $expected.Add('pegasus_web_runtime_role|G|SELECT|IntakeSearchDocuments')
    foreach ($permission in @('SELECT', 'INSERT', 'DELETE')) {
        $expected.Add("pegasus_worker_runtime_role|G|$permission|IntakeSearchDocuments")
    }
    # 20260819180000_GrantEvaHandoffDownloadOperations granted the Web role
    # SELECT/INSERT on EvaHandoffDownloadOperations, closing a live production
    # gap. ENG-016 then dropped that table together with EvaHandoffOperations
    # and EvaHandoffRevisions, and SQL Server drops a table's permission rows
    # with the table -- so the matrix expects nothing for it. The migration is
    # still named here because it remains in the folder and still carries a
    # GRANT, which Test-AzureDeploymentPlan.ps1 requires this script to
    # account for by name. The three baseline-derived tables leave the matrix
    # through $removedTables above.
    # 20260819101344_GroupedIntakeSubmission: the Upload page's grouped
    # submission tables. EfIntakeSubmissionGroupStore only reads and appends
    # (no UPDATE, no Remove); the Web role gets SELECT and INSERT (it creates
    # and appends group/member rows from the Upload page).
    foreach ($table in @('IntakeSubmissionGroups', 'IntakeSubmissionGroupMembers')) {
        foreach ($permission in @('SELECT', 'INSERT')) {
            $expected.Add("pegasus_web_runtime_role|G|$permission|$table")
        }
    }
    # 20260819234014_GrantWorkerIntakeSubmissionGroupRead (INTK-011): the
    # original GroupedIntakeSubmission comment above claimed "the Worker never
    # references either table" -- that was wrong. ImageIntakeAutomation
    # .TryApplyGroupAsync, invoked from the Worker's ProcessQueuedIntake
    # pipeline, calls IIntakeSubmissionGroupStore.FindForMemberSourceAsync/
    # ListMembersAsync at runtime. The Worker only ever reads; it never
    # creates or appends a group/member row.
    foreach ($table in @('IntakeSubmissionGroups', 'IntakeSubmissionGroupMembers')) {
        $expected.Add("pegasus_worker_runtime_role|G|SELECT|$table")
    }
    # 20260825145216_MailboxImageIntake: mailbox processing now creates and
    # appends the same group/member rows as the Web upload route. The Worker
    # remains append-only; UPDATE and DELETE are still absent from the matrix.
    foreach ($table in @('IntakeSubmissionGroups', 'IntakeSubmissionGroupMembers')) {
        $expected.Add("pegasus_worker_runtime_role|G|INSERT|$table")
    }
    # 20260819112914_ImageInitiatedLifecycle and
    # 20260825121453_GrantWorkerImageIntakeLifecycleEvents: the Image-initiated
    # Case lifecycle event log is append-only. Web handles staff transitions
    # and the Worker completes automatic association/merge transitions, so both
    # runtime roles need SELECT for replay and INSERT for the new event.
    # Neither runtime role may rewrite or delete lifecycle history.
    foreach ($role in @('pegasus_web_runtime_role', 'pegasus_worker_runtime_role')) {
        $expected.Add("$role|G|SELECT|ImageIntakeLifecycleEvents")
        $expected.Add("$role|G|INSERT|ImageIntakeLifecycleEvents")
        $expected.Add("$role|D|UPDATE|ImageIntakeLifecycleEvents")
        $expected.Add("$role|D|DELETE|ImageIntakeLifecycleEvents")
    }
    # 20260820100056_ApprovedMailboxLogicalFolderBindings: the existing Web
    # mailbox-administration transaction reads and replaces the mailbox-owned
    # binding rows. The Worker has no caller and receives no grant.
    foreach ($permission in @('SELECT', 'INSERT', 'DELETE')) {
        $expected.Add("pegasus_web_runtime_role|G|$permission|ApprovedMailboxFolderBindings")
    }
    # 20260820114412_ApprovedOutlookCategoryCatalogue: Web administrators
    # maintain the global allowlist; disable replaces deletion. Worker has no caller.
    foreach ($permission in @('SELECT', 'INSERT', 'UPDATE')) {
        $expected.Add("pegasus_web_runtime_role|G|$permission|ApprovedOutlookCategories")
    }
    $expected.Add('pegasus_web_runtime_role|D|DELETE|ApprovedOutlookCategories')
    # 20260820144004_RetainedMailFolderMoves: Web owns the confirmed move
    # operation and its durable recovery state. Worker has no caller. Both
    # runtime roles are denied deletion so the operation history is permanent.
    foreach ($permission in @('SELECT', 'INSERT', 'UPDATE')) {
        $expected.Add("pegasus_web_runtime_role|G|$permission|RetainedMailFolderMoves")
    }
    $expected.Add('pegasus_web_runtime_role|D|DELETE|RetainedMailFolderMoves')
    $expected.Add('pegasus_worker_runtime_role|D|DELETE|RetainedMailFolderMoves')
    # 20260821095500_GrantWorkerVehicleLookupRequests: the Worker's
    # automatic vehicle-lookup sweep (CASE-008) inserts the request row;
    # the reconciliation baseline held only SELECT. DELETE stays denied
    # via the baseline matrix.
    $expected.Add('pegasus_worker_runtime_role|G|INSERT|VehicleLookupRequests')
    # 20260821100623_GrantImageIntakeLifecycleUpdates: both runtime roles
    # update ImageIntakes lifecycle state (PLAT-020); DELETE stays denied
    # via the baseline matrix.
    $expected.Add('pegasus_web_runtime_role|G|UPDATE|ImageIntakes')
    $expected.Add('pegasus_worker_runtime_role|G|UPDATE|ImageIntakes')
    # 20260822044425_GrantWorkerCaseDocuments: DOCS-007 moved case-document
    # registration into the Worker's custody processor, and the reconciliation
    # baseline granted these three tables to Web only, so every deployed case
    # was refused the record write after its evidence reached Box. UPDATE is
    # needed only on DocumentVersions, where a superseded version is cleared.
    # DELETE stays denied via the baseline matrix.
    foreach ($table in @('CaseDocuments', 'DocumentOccurrences')) {
        foreach ($permission in @('SELECT', 'INSERT')) {
            $expected.Add("pegasus_worker_runtime_role|G|$permission|$table")
        }
    }
    foreach ($permission in @('SELECT', 'INSERT', 'UPDATE')) {
        $expected.Add("pegasus_worker_runtime_role|G|$permission|DocumentVersions")
    }
    # 20260826151807_ApprovedMailboxStableIdentityAndSubscriptions: Web resolves
    # callbacks read-only; Worker owns subscription creation and maintenance.
    $expected.Add('pegasus_web_runtime_role|G|SELECT|ApprovedMailboxSubscriptions')
    foreach ($permission in @('INSERT', 'UPDATE', 'DELETE')) {
        $expected.Add("pegasus_web_runtime_role|D|$permission|ApprovedMailboxSubscriptions")
    }
    foreach ($permission in @('SELECT', 'INSERT', 'UPDATE')) {
        $expected.Add("pegasus_worker_runtime_role|G|$permission|ApprovedMailboxSubscriptions")
    }
    $expected.Add('pegasus_worker_runtime_role|D|DELETE|ApprovedMailboxSubscriptions')
    # 20260827143200_GrantEvaSubmissions: EXT-04 gave a case a second route to
    # EVA. Web writes an attempt when an operator sends by hand, the Worker
    # when a principal's automatic setting submits a case that reached Review,
    # and both read prior attempts for the once-per-case and replay checks. An
    # attempt is a fact about a moment and is never edited, so neither role is
    # granted UPDATE or DELETE.
    foreach ($role in @('pegasus_web_runtime_role', 'pegasus_worker_runtime_role')) {
        foreach ($permission in @('SELECT', 'INSERT')) {
            $expected.Add("$role|G|$permission|EvaSubmissions")
        }
    }
    # 20260828084644_GrantAiJobs: AUTO-011 added the pull-based AI job ledger
    # (ADR-0035). Only Web touches it — staff create, cancel and confirm from
    # the application and external AI clients claim and finish jobs through
    # the /mcp ingress that Web hosts; the Worker runs no AI timer. Rows are
    # created once and then move through their states in place, so Web holds
    # SELECT, INSERT and UPDATE. A job is a permanent record: no DELETE is
    # granted, and the Worker is granted nothing.
    foreach ($permission in @('SELECT', 'INSERT', 'UPDATE')) {
        $expected.Add("pegasus_web_runtime_role|G|$permission|AiJobs")
    }
    # 20260828104139_GrantPrincipalApiCredentials: TICK-061 added one Provider
    # API credential per Principal (API-04). Only Web touches it —
    # Administrators issue, reset, pause, resume and revoke from the
    # application and the Provider API verifies a presented secret in the
    # same process; the Worker never authenticates a provider. A row is
    # created once and then rotated or moved through its states in place, so
    # Web holds SELECT, INSERT and UPDATE. A revoked credential stays as the
    # record of what was revoked: no DELETE, and the Worker is granted nothing.
    foreach ($permission in @('SELECT', 'INSERT', 'UPDATE')) {
        $expected.Add("pegasus_web_runtime_role|G|$permission|PrincipalApiCredentials")
    }
    # 20260828111732_GrantProviderSubmissions: TICK-058 added the Provider API
    # submission record (API-01). Web hosts the API: it inserts one row per
    # accepted submission and reads rows back for idempotent replay and the
    # provider's own result lookup. The Worker processes the staged files and
    # reads the row to bind each one to the Principal whose credential
    # submitted it; it never writes one. The row is created when the
    # submission is received and completed in place once the request has been
    # durably retained — the staged receipt id the result lookup reads back —
    # so Web also holds UPDATE. A submission is never removed: no DELETE for
    # either role.
    foreach ($permission in @('SELECT', 'INSERT', 'UPDATE')) {
        $expected.Add("pegasus_web_runtime_role|G|$permission|ProviderSubmissions")
    }
    $expected.Add('pegasus_worker_runtime_role|G|SELECT|ProviderSubmissions')
    # 20260829212237_GrantProviderSubmissionAcceptRecovery: AUTO-012 made the
    # accept path recoverable. Web writes the four accept records in four
    # separate transactions, so a process loss between them used to leave a
    # submission whose staged receipt id was never written back — the result
    # lookup then answered Received forever, and the Accepted history row was
    # never appended. A Worker reconciliation pass now completes those records
    # in place, which is why the Worker holds UPDATE here and no longer only
    # SELECT. It still never inserts a submission and never removes one.
    $expected.Add('pegasus_worker_runtime_role|G|UPDATE|ProviderSubmissions')
    # 20260829095336_CaseValuations: the Web Case workspace creates and edits
    # valuation rows, and the Assessment workspace reads the current Engineer
    # value in the same process. Worker has no caller and no grant. Valuations
    # remain Case records, so DELETE is deliberately absent.
    foreach ($permission in @('SELECT', 'INSERT', 'UPDATE')) {
        $expected.Add("pegasus_web_runtime_role|G|$permission|CaseValuations")
    }
    # 20260904210022_EngineerNotes: the Web Case workspace appends and reads
    # Engineer notes. Worker has no caller; UPDATE and DELETE are absent.
    foreach ($permission in @('SELECT', 'INSERT')) {
        $expected.Add("pegasus_web_runtime_role|G|$permission|EngineerNotes")
    }
    # 20260906054658_V1PlatformFoundation: v1 schema owners and holding custody.
    $v1Tables = @('UserExternalCredentials','StaffMailSendOperations','ValuationPresets','LabourRateCards','AppliedValuationSnapshots','GlassRepairEstimateSessions','CaseReportGenerations','GeneratedCaseArtifacts','CaseReportDeliveryIntents','RetainedInstructionAnalyses','IntakeSourceCandidates','IntakeOcrOperations','TriageSequences','DocumentContentCacheEntries','ClaimSources','OrganizationDirectoryEntries','PublicUploadSessions','PublicUploadOccurrences')
    foreach ($table in $v1Tables) {
        $expected.Add("pegasus_web_runtime_role|D|DELETE|$table")
        if ($table -ne 'DocumentContentCacheEntries') {
            $expected.Add("pegasus_worker_runtime_role|D|DELETE|$table")
        }
    }
    $v1Migration = Get-Content -Raw -LiteralPath (Join-Path (Split-Path -Parent $migrationPath) '20260906054658_V1PlatformFoundation.cs')
    foreach ($grant in [regex]::Matches($v1Migration, 'GRANT (?<permissions>[A-Z,]+) ON OBJECT::\[dbo\]\.\[(?<table>[A-Za-z0-9]+)\] TO \[(?<role>pegasus_(?:web|worker)_runtime_role)\]')) {
        foreach ($permission in $grant.Groups['permissions'].Value.Split(',')) {
            $expected.Add("$($grant.Groups['role'].Value)|G|$permission|$($grant.Groups['table'].Value)")
        }
    }
    return @($expected | Sort-Object -Unique)
}

$values = Get-AzdValues $Environment
$expectedSubscriptionId = 'e6076573-23a5-46a8-acef-7e22d264e5db'
$expectedTenantId = '858cf5b3-aa0a-47a6-9b40-4851fd0afa94'
$account = & az account show --query '{subscription:id,tenant:tenantId}' --output json | ConvertFrom-Json
if (
    $LASTEXITCODE -ne 0 -or
    $account.subscription -ne $expectedSubscriptionId -or
    $account.tenant -ne $expectedTenantId
) {
    throw 'Azure SQL bootstrap refuses the current Azure CLI account context.'
}
$server = $values['AZURE_SQL_SERVER_FQDN']
$database = $values['AZURE_SQL_DATABASE_NAME']
$webSid = Convert-GuidToSqlSid $values['WEB_IDENTITY_CLIENT_ID']
$workerSid = Convert-GuidToSqlSid $values['WORKER_IDENTITY_CLIENT_ID']
if (-not (Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue)) {
    throw 'The SqlServer PowerShell module is required for Azure SQL runtime-principal bootstrap.'
}
$accessToken = (& az account get-access-token --resource https://database.windows.net/ --query accessToken --output tsv).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($accessToken)) {
    throw 'Unable to obtain an Azure SQL access token from the approved Azure CLI identity.'
}

function Invoke-AzureSqlQuery([string] $Query) {
    @(Invoke-Sqlcmd `
        -ServerInstance "tcp:$server,1433" `
        -Database $database `
        -AccessToken $accessToken `
        -Query $Query `
        -AbortOnError `
        -OutputAs DataRows `
        -ErrorAction Stop)
}

$sql = @"
SET NOCOUNT ON;
IF EXISTS (
    SELECT 1 FROM sys.database_principals
    WHERE name = N'pegasus_web_runtime'
      AND (type <> 'E' OR sid <> $webSid))
    THROW 51000, 'The existing Web runtime principal does not match the approved managed identity SID and type.', 1;
IF EXISTS (
    SELECT 1 FROM sys.database_principals
    WHERE name = N'pegasus_worker_runtime'
      AND (type <> 'E' OR sid <> $workerSid))
    THROW 51001, 'The existing Worker runtime principal does not match the approved managed identity SID and type.', 1;
IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime') IS NULL
    CREATE USER [pegasus_web_runtime] WITH SID = $webSid, TYPE = E;
IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime') IS NULL
    CREATE USER [pegasus_worker_runtime] WITH SID = $workerSid, TYPE = E;
IF IS_ROLEMEMBER(N'pegasus_web_runtime_role', N'pegasus_web_runtime') <> 1
    ALTER ROLE [pegasus_web_runtime_role] ADD MEMBER [pegasus_web_runtime];
IF IS_ROLEMEMBER(N'pegasus_worker_runtime_role', N'pegasus_worker_runtime') <> 1
    ALTER ROLE [pegasus_worker_runtime_role] ADD MEMBER [pegasus_worker_runtime];
GRANT CONNECT TO [pegasus_web_runtime];
GRANT CONNECT TO [pegasus_worker_runtime];
IF EXISTS (
    SELECT 1
    FROM sys.database_role_members AS membership
    JOIN sys.database_principals AS member ON member.principal_id = membership.member_principal_id
    JOIN sys.database_principals AS role ON role.principal_id = membership.role_principal_id
    WHERE (member.name = N'pegasus_web_runtime' AND role.name <> N'pegasus_web_runtime_role')
       OR (member.name = N'pegasus_worker_runtime' AND role.name <> N'pegasus_worker_runtime_role'))
    THROW 51002, 'A runtime identity belongs to an unapproved database role.', 1;
IF EXISTS (
    SELECT 1
    FROM sys.database_role_members AS membership
    JOIN sys.database_principals AS member ON member.principal_id = membership.member_principal_id
    WHERE member.name IN (N'pegasus_web_runtime_role', N'pegasus_worker_runtime_role'))
    THROW 51003, 'A Pegasus runtime role is nested inside another database role.', 1;
IF EXISTS (
    SELECT 1
    FROM sys.database_permissions AS permission
    JOIN sys.database_principals AS principal ON principal.principal_id = permission.grantee_principal_id
    WHERE principal.name IN (N'pegasus_web_runtime', N'pegasus_worker_runtime')
      AND NOT (
          permission.class = 0
          AND permission.permission_name = N'CONNECT'
          AND permission.state = 'G'))
    THROW 51004, 'A runtime identity has a prohibited direct permission entry.', 1;
IF EXISTS (
    SELECT 1
    FROM sys.database_permissions AS permission
    JOIN sys.database_principals AS principal ON principal.principal_id = permission.grantee_principal_id
    WHERE principal.name = N'public'
      AND NOT (
          (permission.class = 0 AND permission.permission_name = N'CONNECT' AND permission.state = 'G')
          OR (
              permission.class = 1
              AND permission.permission_name = N'SELECT'
              AND permission.state = 'G'
              AND (permission.major_id < 0 OR OBJECT_SCHEMA_NAME(permission.major_id) = N'sys'))))
    THROW 51005, 'The public role grants or denies permissions outside the standard database CONNECT grant.', 1;
IF EXISTS (
    SELECT 1
    FROM sys.schemas AS owned
    JOIN sys.database_principals AS owner ON owner.principal_id = owned.principal_id
    WHERE owner.name IN (
        N'pegasus_web_runtime', N'pegasus_worker_runtime',
        N'pegasus_web_runtime_role', N'pegasus_worker_runtime_role'))
    THROW 51006, 'A runtime identity or role owns a database schema.', 1;
IF EXISTS (
    SELECT 1
    FROM sys.objects AS owned
    JOIN sys.database_principals AS owner ON owner.principal_id = owned.principal_id
    WHERE owner.name IN (
        N'pegasus_web_runtime', N'pegasus_worker_runtime',
        N'pegasus_web_runtime_role', N'pegasus_worker_runtime_role'))
    THROW 51007, 'A runtime identity or role owns a database object.', 1;
IF EXISTS (
    SELECT 1
    FROM sys.database_principals AS owned
    JOIN sys.database_principals AS owner ON owner.principal_id = owned.owning_principal_id
    WHERE owner.name IN (
        N'pegasus_web_runtime', N'pegasus_worker_runtime',
        N'pegasus_web_runtime_role', N'pegasus_worker_runtime_role'))
    THROW 51008, 'A runtime identity or role owns another database principal.', 1;
IF EXISTS (
    SELECT 1 FROM sys.databases
    WHERE name = DB_NAME() AND owner_sid IN ($webSid, $workerSid))
    THROW 51009, 'A runtime identity owns the production database.', 1;

CREATE TABLE #runtime_effective_database_permissions (
    principal_name sysname NOT NULL,
    permission_name nvarchar(128) NOT NULL
);
EXECUTE AS USER = N'pegasus_web_runtime';
INSERT INTO #runtime_effective_database_permissions (principal_name, permission_name)
SELECT N'pegasus_web_runtime', permission_name FROM fn_my_permissions(NULL, N'DATABASE');
REVERT;
EXECUTE AS USER = N'pegasus_worker_runtime';
INSERT INTO #runtime_effective_database_permissions (principal_name, permission_name)
SELECT N'pegasus_worker_runtime', permission_name FROM fn_my_permissions(NULL, N'DATABASE');
REVERT;
IF EXISTS (
    SELECT 1 FROM #runtime_effective_database_permissions
    WHERE permission_name <> N'CONNECT')
    THROW 51010, 'A runtime identity has an unapproved effective database-scoped permission.', 1;

SELECT name, type_desc, CONVERT(varchar(34), sid, 1) AS sid
FROM sys.database_principals
WHERE name IN (N'pegasus_web_runtime', N'pegasus_worker_runtime');
"@

Invoke-AzureSqlQuery $sql | Out-Null

$permissionQuery = @"
SET NOCOUNT ON;
SELECT CONCAT(
       principal.name COLLATE DATABASE_DEFAULT, N'|',
       permission.state COLLATE DATABASE_DEFAULT, N'|',
       permission.permission_name COLLATE DATABASE_DEFAULT, N'|',
       CASE
           WHEN permission.class = 1 AND permission.minor_id = 0 AND target.object_id IS NOT NULL
               THEN target.name COLLATE DATABASE_DEFAULT
           ELSE CONCAT(N'__UNAPPROVED_SCOPE_', permission.class, N'_', permission.major_id, N'_', permission.minor_id)
       END) AS permission_row
FROM sys.database_permissions AS permission
JOIN sys.database_principals AS principal ON principal.principal_id = permission.grantee_principal_id
LEFT JOIN sys.tables AS target ON target.object_id = permission.major_id
WHERE principal.name IN (N'pegasus_web_runtime_role', N'pegasus_worker_runtime_role')
ORDER BY principal.name, permission.state, permission.permission_name, target.name;
"@
$actualMatrix = @(Invoke-AzureSqlQuery $permissionQuery |
    ForEach-Object { ([string]$_.permission_row).Trim() } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Sort-Object -Unique)
$expectedMatrix = @(Get-MigrationPermissionMatrix)
$difference = @(Compare-Object -ReferenceObject $expectedMatrix -DifferenceObject $actualMatrix)
if ($difference.Count -ne 0) {
    throw "Azure SQL runtime permissions differ from the exhaustive migration-defined matrix: $($difference | Out-String)"
}

function Get-EffectiveTablePermissionMatrix([string] $UserName, [string] $RoleName) {
    $query = @"
SET NOCOUNT ON;
EXECUTE AS USER = N'$UserName';
SELECT CONCAT(N'$RoleName|G|', candidate.permission_name COLLATE DATABASE_DEFAULT, N'|', target.name COLLATE DATABASE_DEFAULT) AS permission_row
FROM sys.tables AS target
CROSS JOIN (VALUES (N'SELECT'), (N'INSERT'), (N'UPDATE'), (N'DELETE')) AS candidate(permission_name)
WHERE target.is_ms_shipped = 0
  AND HAS_PERMS_BY_NAME(
      QUOTENAME(SCHEMA_NAME(target.schema_id)) + N'.' + QUOTENAME(target.name),
      N'OBJECT',
      candidate.permission_name) = 1
ORDER BY candidate.permission_name, target.name;
REVERT;
"@
    $rows = @(Invoke-AzureSqlQuery $query |
        ForEach-Object { ([string]$_.permission_row).Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    return $rows
}

$expectedEffectiveMatrix = @($expectedMatrix | Where-Object { $_ -match '\|G\|' } | Sort-Object -Unique)
$actualEffectiveMatrix = @(
    Get-EffectiveTablePermissionMatrix 'pegasus_web_runtime' 'pegasus_web_runtime_role'
    Get-EffectiveTablePermissionMatrix 'pegasus_worker_runtime' 'pegasus_worker_runtime_role'
) | Sort-Object -Unique
$effectiveDifference = @(Compare-Object -ReferenceObject $expectedEffectiveMatrix -DifferenceObject $actualEffectiveMatrix)
if ($effectiveDifference.Count -ne 0) {
    throw "Azure SQL effective runtime DML differs from the exhaustive migration-defined allow matrix: $($effectiveDifference | Out-String)"
}
Write-Output "Verified $($actualMatrix.Count) catalogued permission/denial rows and $($actualEffectiveMatrix.Count) effective runtime DML rows."
$accessToken = $null

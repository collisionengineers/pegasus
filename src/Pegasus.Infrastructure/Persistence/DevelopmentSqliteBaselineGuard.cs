using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

public static class DevelopmentSqliteBaselineGuard
{
    private static readonly string[] ExpectedMigrations =
    [
        "20260724104624_InitialProviderNeutralIntake",
        "20260727170804_ProviderDomainReferenceSnapshotV1",
        "20260729150000_DocumentCustodyAndRequests",
        "20260729152105_WorkflowTriageEmailEvidence",
        "20260729160000_CaseWorkflowRuntime",
        "20260729170000_MailboxRouteAudit",
        "20260729171000_CaseAcceptanceReplay"
    ];

    private static readonly Dictionary<string, ColumnDefinition[]> ExpectedColumns =
        new Dictionary<string, ColumnDefinition[]>(StringComparer.Ordinal)
        {
            ["__EFMigrationsLock"] =
            [
                new("Id", "INTEGER", true, 1),
                new("Timestamp", "TEXT", true, 0)
            ],
            ["__EFMigrationsHistory"] =
            [
                new("MigrationId", "TEXT", true, 1),
                new("ProductVersion", "TEXT", true, 0)
            ],
            ["ApplicationInitializations"] =
            [
                new("Id", "TEXT", true, 1),
                new("ManifestSha256", "TEXT", true, 0),
                new("MigrationId", "TEXT", true, 0),
                new("CompletedAtUtc", "TEXT", true, 0)
            ],
            ["ActionHistory"] =
            [
                new("Id", "TEXT", true, 1),
                new("AggregateType", "TEXT", true, 0),
                new("AggregateId", "TEXT", true, 0),
                new("EventKind", "TEXT", true, 0),
                new("ActorKind", "TEXT", true, 0),
                new("ActorSubjectId", "TEXT", true, 0),
                new("ActorRolesJson", "TEXT", true, 0),
                new("OccurredAtUtc", "TEXT", true, 0),
                new("Outcome", "TEXT", true, 0),
                new("CorrelationId", "TEXT", true, 0),
                new("Reason", "TEXT", false, 0),
                new("BeforeJson", "TEXT", false, 0),
                new("AfterJson", "TEXT", false, 0),
                new("PolicyVersion", "TEXT", false, 0)
            ],
            ["SecurityEvents"] =
            [
                new("Id", "TEXT", true, 1),
                new("Type", "TEXT", true, 0),
                new("Outcome", "TEXT", true, 0),
                new("SubjectId", "TEXT", true, 0),
                new("OccurredAtUtc", "TEXT", true, 0),
                new("CorrelationId", "TEXT", true, 0),
                new("ReasonCode", "TEXT", false, 0)
            ],
            ["AspNetRoles"] =
            [
                new("Id", "TEXT", true, 1),
                new("Name", "TEXT", false, 0),
                new("NormalizedName", "TEXT", false, 0),
                new("ConcurrencyStamp", "TEXT", false, 0)
            ],
            ["AspNetUsers"] =
            [
                new("Id", "TEXT", true, 1),
                new("IsEnabled", "INTEGER", true, 0),
                new("MustChangePassword", "INTEGER", true, 0),
                new("UserName", "TEXT", false, 0),
                new("NormalizedUserName", "TEXT", false, 0),
                new("Email", "TEXT", false, 0),
                new("NormalizedEmail", "TEXT", false, 0),
                new("EmailConfirmed", "INTEGER", true, 0),
                new("PasswordHash", "TEXT", false, 0),
                new("SecurityStamp", "TEXT", false, 0),
                new("ConcurrencyStamp", "TEXT", false, 0),
                new("PhoneNumber", "TEXT", false, 0),
                new("PhoneNumberConfirmed", "INTEGER", true, 0),
                new("TwoFactorEnabled", "INTEGER", true, 0),
                new("LockoutEnd", "TEXT", false, 0),
                new("LockoutEnabled", "INTEGER", true, 0),
                new("AccessFailedCount", "INTEGER", true, 0)
            ],
            ["AspNetRoleClaims"] =
            [
                new("Id", "INTEGER", true, 1),
                new("RoleId", "TEXT", true, 0),
                new("ClaimType", "TEXT", false, 0),
                new("ClaimValue", "TEXT", false, 0)
            ],
            ["AspNetUserClaims"] =
            [
                new("Id", "INTEGER", true, 1),
                new("UserId", "TEXT", true, 0),
                new("ClaimType", "TEXT", false, 0),
                new("ClaimValue", "TEXT", false, 0)
            ],
            ["AspNetUserLogins"] =
            [
                new("LoginProvider", "TEXT", true, 1),
                new("ProviderKey", "TEXT", true, 2),
                new("ProviderDisplayName", "TEXT", false, 0),
                new("UserId", "TEXT", true, 0)
            ],
            ["AspNetUserRoles"] =
            [
                new("UserId", "TEXT", true, 1),
                new("RoleId", "TEXT", true, 2)
            ],
            ["AspNetUserTokens"] =
            [
                new("UserId", "TEXT", true, 1),
                new("LoginProvider", "TEXT", true, 2),
                new("Name", "TEXT", true, 3),
                new("Value", "TEXT", false, 0)
            ],
            ["OpenIddictApplications"] =
            [
                new("Id", "TEXT", true, 1),
                new("ApplicationType", "TEXT", false, 0),
                new("ClientId", "TEXT", false, 0),
                new("ClientSecret", "TEXT", false, 0),
                new("ClientType", "TEXT", false, 0),
                new("ConcurrencyToken", "TEXT", false, 0),
                new("ConsentType", "TEXT", false, 0),
                new("DisplayName", "TEXT", false, 0),
                new("DisplayNames", "TEXT", false, 0),
                new("JsonWebKeySet", "TEXT", false, 0),
                new("Permissions", "TEXT", false, 0),
                new("PostLogoutRedirectUris", "TEXT", false, 0),
                new("Properties", "TEXT", false, 0),
                new("RedirectUris", "TEXT", false, 0),
                new("Requirements", "TEXT", false, 0),
                new("Settings", "TEXT", false, 0)
            ],
            ["OpenIddictAuthorizations"] =
            [
                new("Id", "TEXT", true, 1),
                new("ApplicationId", "TEXT", false, 0),
                new("ConcurrencyToken", "TEXT", false, 0),
                new("CreationDate", "TEXT", false, 0),
                new("Properties", "TEXT", false, 0),
                new("Scopes", "TEXT", false, 0),
                new("Status", "TEXT", false, 0),
                new("Subject", "TEXT", false, 0),
                new("Type", "TEXT", false, 0)
            ],
            ["OpenIddictScopes"] =
            [
                new("Id", "TEXT", true, 1),
                new("ConcurrencyToken", "TEXT", false, 0),
                new("Description", "TEXT", false, 0),
                new("Descriptions", "TEXT", false, 0),
                new("DisplayName", "TEXT", false, 0),
                new("DisplayNames", "TEXT", false, 0),
                new("Name", "TEXT", false, 0),
                new("Properties", "TEXT", false, 0),
                new("Resources", "TEXT", false, 0)
            ],
            ["OpenIddictTokens"] =
            [
                new("Id", "TEXT", true, 1),
                new("ApplicationId", "TEXT", false, 0),
                new("AuthorizationId", "TEXT", false, 0),
                new("ConcurrencyToken", "TEXT", false, 0),
                new("CreationDate", "TEXT", false, 0),
                new("ExpirationDate", "TEXT", false, 0),
                new("Payload", "TEXT", false, 0),
                new("Properties", "TEXT", false, 0),
                new("RedemptionDate", "TEXT", false, 0),
                new("ReferenceId", "TEXT", false, 0),
                new("Status", "TEXT", false, 0),
                new("Subject", "TEXT", false, 0),
                new("Type", "TEXT", false, 0)
            ],
            ["IntakeReceipts"] =
            [
                new("Id", "TEXT", true, 1),
                new("SourceFileName", "TEXT", true, 0),
                new("MediaType", "TEXT", true, 0),
                new("SourceLength", "INTEGER", true, 0),
                new("SourceHash", "TEXT", true, 0),
                new("SourceChannel", "TEXT", true, 0),
                new("ExternalReceiptToken", "TEXT", true, 0),
                new("ReceivedAtUtc", "TEXT", true, 0),
                new("ProcessedAtUtc", "TEXT", true, 0),
                new("SourceReaderKey", "TEXT", true, 0),
                new("SourceReaderVersion", "TEXT", true, 0),
                new("ExtractionPolicyKey", "TEXT", false, 0),
                new("ExtractionPolicyVersion", "INTEGER", false, 0),
                new("Decision", "TEXT", true, 0),
                new("DecisionReason", "TEXT", true, 0),
                new("EvidenceJson", "TEXT", true, 0),
                new("FieldsJson", "TEXT", true, 0),
                new("FailureCode", "TEXT", false, 0),
                new("FailureReason", "TEXT", false, 0),
                new("OcrCandidatesJson", "TEXT", true, 0),
                new("Version", "INTEGER", true, 0)
            ],
            ["InstructionDrafts"] =
            [
                new("IntakeReceiptId", "TEXT", true, 1),
                new("SuggestedPrincipalCode", "TEXT", false, 0),
                new("ClaimantName", "TEXT", false, 0),
                new("ClaimNumber", "TEXT", false, 0),
                new("VehicleRegistration", "TEXT", false, 0),
                new("VehicleMake", "TEXT", false, 0),
                new("VehicleModel", "TEXT", false, 0),
                new("VehicleMileage", "INTEGER", false, 0),
                new("AccidentCircumstances", "TEXT", false, 0),
                new("DateOfIncident", "date", false, 0),
                new("InstructionDate", "date", false, 0),
                new("InspectionAddress", "TEXT", false, 0)
            ],
            ["IntakeAssets"] =
            [
                new("Id", "TEXT", true, 1),
                new("IntakeReceiptId", "TEXT", true, 0),
                new("SourceLabel", "TEXT", true, 0),
                new("FileName", "TEXT", true, 0),
                new("MediaType", "TEXT", true, 0),
                new("Kind", "TEXT", true, 0),
                new("Disposition", "TEXT", true, 0),
                new("ContentLength", "INTEGER", true, 0),
                new("ContentHash", "TEXT", true, 0),
                new("StorageKey", "TEXT", true, 0),
                new("PageNumber", "INTEGER", false, 0),
                new("BoundsJson", "TEXT", false, 0),
                new("WidthPixels", "INTEGER", false, 0),
                new("HeightPixels", "INTEGER", false, 0)
            ],
            ["IntakeReceiptEvents"] =
            [
                new("Id", "TEXT", true, 1),
                new("IntakeReceiptId", "TEXT", true, 0),
                new("EventType", "TEXT", true, 0),
                new("Actor", "TEXT", true, 0),
                new("OccurredAtUtc", "TEXT", true, 0),
                new("DetailsJson", "TEXT", true, 0)
            ]
            ,
            ["IntakeStagedReceipts"] =
            [
                new("Id", "TEXT", true, 1),
                new("SourceFileName", "TEXT", true, 0),
                new("MediaType", "TEXT", true, 0),
                new("SourceLength", "INTEGER", true, 0),
                new("SourceHash", "TEXT", true, 0),
                new("SourceChannel", "TEXT", true, 0),
                new("ExternalReceiptToken", "TEXT", true, 0),
                new("ReceivedAtUtc", "TEXT", true, 0),
                new("Actor", "TEXT", true, 0),
                new("StorageKey", "TEXT", true, 0),
                new("StagedAtUtc", "TEXT", true, 0)
            ],
            ["IntakeWorkItems"] =
            [
                new("Id", "TEXT", true, 1),
                new("StagedReceiptId", "TEXT", true, 0),
                new("OperationKey", "TEXT", true, 0),
                new("State", "TEXT", true, 0),
                new("AttemptCount", "INTEGER", true, 0),
                new("DueAtUtc", "TEXT", true, 0),
                new("LeaseToken", "TEXT", false, 0),
                new("LeaseExpiresAtUtc", "TEXT", false, 0),
                new("ProcessedReceiptId", "TEXT", false, 0),
                new("FailureCode", "TEXT", false, 0),
                new("CompletedAtUtc", "TEXT", false, 0)
            ]
            ,
            ["IntakeEvaluations"] =
            [
                new("Id", "TEXT", true, 1),
                new("StagedReceiptId", "TEXT", true, 0),
                new("ProcessedReceiptId", "TEXT", true, 0),
                new("Revision", "INTEGER", true, 0),
                new("EvaluatedAtUtc", "TEXT", true, 0)
            ]
            ,
            ["ApprovedInboxPollStates"] =
            [
                new("MailboxId", "TEXT", true, 1),
                new("MailboxAddress", "TEXT", true, 0),
                new("Cursor", "TEXT", false, 0),
                new("DueAtUtc", "TEXT", true, 0),
                new("LeaseToken", "TEXT", false, 0),
                new("LeaseExpiresAtUtc", "TEXT", false, 0),
                new("LastCompletedAtUtc", "TEXT", false, 0),
                new("LastFailureCode", "TEXT", false, 0)
            ],
            ["IntakeMailRouteDecisions"] =
            [
                new("IntakeReceiptId", "TEXT", true, 1),
                new("Disposition", "TEXT", true, 0),
                new("RouteOwnerCode", "TEXT", false, 0),
                new("RouteKind", "TEXT", false, 0),
                new("WorkProviderCode", "TEXT", false, 0),
                new("PredicatesJson", "TEXT", true, 0),
                new("Reason", "TEXT", true, 0),
                new("PolicyKey", "TEXT", true, 0),
                new("PolicyVersion", "INTEGER", true, 0),
                new("TransportIdentitiesJson", "TEXT", true, 0),
                new("OriginalIdentitiesJson", "TEXT", true, 0),
                new("EffectiveSenderAddress", "TEXT", false, 0),
                new("EffectiveSenderSourceLabel", "TEXT", false, 0)
            ],
            ["ProviderDomainPackages"] =
            [
                new("Version", "TEXT", true, 1),
                new("SchemaVersion", "INTEGER", true, 0),
                new("PackageSha256", "TEXT", true, 0),
                new("SourcePath", "TEXT", true, 0),
                new("SourceContentSha256", "TEXT", true, 0),
                new("SourceSheet", "TEXT", true, 0),
                new("SourceRowCount", "INTEGER", true, 0)
            ],
            ["ProviderReferences"] =
            [
                new("Version", "TEXT", true, 1),
                new("Code", "TEXT", true, 2),
                new("SourceRow", "INTEGER", true, 0)
            ],
            ["ProviderDomainEvidence"] =
            [
                new("Version", "TEXT", true, 1),
                new("Code", "TEXT", true, 2),
                new("DomainSuffix", "TEXT", true, 3)
            ],
            ["Organizations"] =
            [
                new("Id", "TEXT", true, 1),
                new("Name", "TEXT", true, 0),
                new("Version", "INTEGER", true, 0)
            ],
            ["OrganizationRoles"] =
            [
                new("OrganizationId", "TEXT", true, 1),
                new("Role", "TEXT", true, 2)
            ],
            ["PrincipalSequenceLineages"] =
            [
                new("Id", "TEXT", true, 1),
                new("CreatedAtUtc", "TEXT", true, 0)
            ],
            ["Principals"] =
            [
                new("Id", "TEXT", true, 1),
                new("OrganizationId", "TEXT", true, 0),
                new("Code", "TEXT", true, 0),
                new("SequenceLineageId", "TEXT", true, 0),
                new("PredecessorId", "TEXT", false, 0),
                new("SuccessorId", "TEXT", false, 0),
                new("IsActive", "INTEGER", true, 0),
                new("Version", "INTEGER", true, 0)
            ],
            ["CaseSequences"] =
            [
                new("SequenceLineageId", "TEXT", true, 1),
                new("Year", "INTEGER", true, 2),
                new("LastAllocatedSequence", "INTEGER", true, 0)
            ],
            ["Cases"] =
            [
                new("Id", "TEXT", true, 1),
                new("PrincipalId", "TEXT", true, 0),
                new("SequenceLineageId", "TEXT", true, 0),
                new("Year", "INTEGER", true, 0),
                new("Sequence", "INTEGER", true, 0),
                new("Reference", "TEXT", true, 0),
                new("AuditReference", "TEXT", false, 0),
                new("Type", "TEXT", true, 0),
                new("InitialState", "TEXT", true, 0),
                new("CustodyState", "TEXT", true, 0),
                new("OriginIntakeReceiptId", "TEXT", true, 0),
                new("StandaloneAuditAssessment", "TEXT", false, 0),
                new("InstructionComplete", "INTEGER", true, 0),
                new("ImagesComplete", "INTEGER", true, 0),
                new("InstructionConfirmedByStaff", "INTEGER", true, 0),
                new("ImagesConfirmedByStaff", "INTEGER", true, 0),
                new("CreatedAtUtc", "TEXT", true, 0),
                new("Version", "INTEGER", true, 0),
                new("ConcurrencyToken", "TEXT", true, 0),
                new("CustodyRootRemoteId", "TEXT", false, 0),
                new("CustodySourceRemoteId", "TEXT", false, 0),
                new("CustodySourceContentHash", "TEXT", false, 0),
                new("CustodySourceETag", "TEXT", false, 0),
                new("CustodyConfirmedAtUtc", "TEXT", false, 0)
            ],
            ["CaseHistory"] =
            [
                new("Id", "TEXT", true, 1),
                new("CaseId", "TEXT", true, 0),
                new("EventType", "TEXT", true, 0),
                new("Actor", "TEXT", true, 0),
                new("Reason", "TEXT", true, 0),
                new("OccurredAtUtc", "TEXT", true, 0),
                new("OperationKey", "TEXT", true, 0),
                new("BeforeVersion", "INTEGER", false, 0),
                new("AfterVersion", "INTEGER", true, 0)
            ],
            ["ExternalWorkItems"] =
            [
                new("Id", "TEXT", true, 1),
                new("CaseId", "TEXT", true, 0),
                new("Kind", "TEXT", true, 0),
                new("OperationKey", "TEXT", true, 0),
                new("State", "TEXT", true, 0),
                new("AttemptCount", "INTEGER", true, 0),
                new("DueAtUtc", "TEXT", true, 0),
                new("LeaseToken", "TEXT", false, 0),
                new("LeaseExpiresAtUtc", "TEXT", false, 0),
                new("ExternalReceipt", "TEXT", false, 0),
                new("FailureCode", "TEXT", false, 0),
                new("FailureReason", "TEXT", false, 0),
                new("CompletedAtUtc", "TEXT", false, 0)
            ],
            ["CaseIntakeLinks"] =
            [
                new("IntakeReceiptId", "TEXT", true, 1),
                new("CaseId", "TEXT", true, 0),
                new("CustodyWorkId", "TEXT", true, 0),
                new("LinkedAtUtc", "TEXT", true, 0),
                new("Actor", "TEXT", true, 0),
                new("OperationKey", "TEXT", true, 0),
                new("ExpectedIntakeVersion", "INTEGER", false, 0),
                new("AcceptanceCommandMaterialJson", "TEXT", false, 0),
                new("AcceptanceCommandFingerprint", "TEXT", false, 0)
            ],
            ["BoxFileRequests"] =
            [
                new("Id", "TEXT", true, 1),
                new("CaseId", "TEXT", true, 0),
                new("Status", "TEXT", true, 0),
                new("CreatedAtUtc", "TEXT", true, 0),
                new("ExpiresAtUtc", "TEXT", false, 0),
                new("DeactivatedAtUtc", "TEXT", false, 0),
                new("Version", "INTEGER", true, 0),
                new("CreateOperationKey", "TEXT", true, 0),
                new("RevokeOperationKey", "TEXT", false, 0),
                new("LinkTokenDigest", "TEXT", true, 0)
            ],
            ["CaseDocuments"] =
            [
                new("Id", "TEXT", true, 1),
                new("CaseId", "TEXT", true, 0),
                new("SourceOccurrenceIdentity", "TEXT", true, 0)
            ],
            ["RequestUploadLinks"] =
            [
                new("Id", "TEXT", true, 1),
                new("CaseId", "TEXT", true, 0),
                new("TokenDigest", "TEXT", true, 0),
                new("Status", "TEXT", true, 0),
                new("CreatedAtUtc", "TEXT", true, 0),
                new("ExpiresAtUtc", "TEXT", true, 0),
                new("RevokedAtUtc", "TEXT", false, 0),
                new("AcceptedFileCount", "INTEGER", true, 0),
                new("AcceptedByteCount", "INTEGER", true, 0),
                new("LimitsVersion", "TEXT", true, 0),
                new("Version", "INTEGER", true, 0),
                new("CreateOperationKey", "TEXT", true, 0),
                new("RevokeOperationKey", "TEXT", false, 0)
            ],
            ["DocumentVersions"] =
            [
                new("Id", "TEXT", true, 1),
                new("DocumentId", "TEXT", true, 0),
                new("Version", "INTEGER", true, 0),
                new("FileName", "TEXT", true, 0),
                new("MediaType", "TEXT", true, 0),
                new("ContentLength", "INTEGER", true, 0),
                new("Sha256", "TEXT", true, 0),
                new("CustodyStatus", "TEXT", true, 0),
                new("CreatedAtUtc", "TEXT", true, 0),
                new("CreatedBy", "TEXT", true, 0),
                new("IsCurrent", "INTEGER", true, 0),
                new("IsLogicallyRemoved", "INTEGER", true, 0),
                new("RemovalReason", "TEXT", false, 0),
                new("RemovalOperationKey", "TEXT", false, 0)
            ],
            ["DocumentOccurrences"] =
            [
                new("Id", "TEXT", true, 1),
                new("CaseId", "TEXT", true, 0),
                new("DocumentId", "TEXT", true, 0),
                new("VersionId", "TEXT", true, 0),
                new("SemanticRole", "TEXT", true, 0),
                new("Source", "TEXT", true, 0),
                new("SourceOccurrenceIdentity", "TEXT", true, 0),
                new("RecordedAtUtc", "TEXT", true, 0),
                new("OperationKey", "TEXT", true, 0)
            ],
            ["Triage"] =
            [
                new("Id", "TEXT", true, 1),
                new("OriginReceiptId", "TEXT", true, 0),
                new("SourceChannel", "TEXT", true, 0),
                new("ExternalReceiptToken", "TEXT", true, 0),
                new("SourceHash", "TEXT", true, 0),
                new("EvaluationRevisionId", "TEXT", true, 0),
                new("NormalizedVehicleRegistration", "TEXT", true, 0),
                new("State", "TEXT", true, 0),
                new("AssigneeId", "TEXT", false, 0),
                new("LinkedCaseId", "TEXT", false, 0),
                new("CreatedAtUtc", "TEXT", true, 0),
                new("CreationOperationKey", "TEXT", true, 0),
                new("Version", "INTEGER", true, 0),
                new("ConcurrencyToken", "TEXT", true, 0),
                new("EditLeaseTokenHash", "TEXT", false, 0),
                new("EditLeaseHolder", "TEXT", false, 0),
                new("EditLeaseOperationKey", "TEXT", false, 0),
                new("EditLeaseExpiresAtUtc", "TEXT", false, 0)
            ],
            ["SentEmailEvidence"] =
            [
                new("Id", "TEXT", true, 1),
                new("TriageId", "TEXT", true, 0),
                new("MessageIdentity", "TEXT", true, 0),
                new("Subject", "TEXT", true, 0),
                new("RecipientsJson", "TEXT", true, 0),
                new("MimeSha256", "TEXT", true, 0),
                new("SentAtUtc", "TEXT", true, 0),
                new("ChaseDueAtUtc", "TEXT", true, 0),
                new("Actor", "TEXT", true, 0),
                new("OperationKey", "TEXT", true, 0),
                new("RequestHash", "TEXT", true, 0),
                new("Version", "INTEGER", true, 0)
            ],
            ["TriageFindings"] =
            [
                new("Id", "TEXT", true, 1),
                new("TriageId", "TEXT", true, 0),
                new("Roadworthiness", "TEXT", false, 0),
                new("Assessment", "TEXT", false, 0),
                new("SupersedesFindingId", "TEXT", false, 0),
                new("Actor", "TEXT", true, 0),
                new("OperationKey", "TEXT", true, 0),
                new("Reason", "TEXT", true, 0),
                new("RecordedAtUtc", "TEXT", true, 0)
            ],
            ["TriageHistory"] =
            [
                new("Id", "TEXT", true, 1),
                new("TriageId", "TEXT", true, 0),
                new("EventType", "TEXT", true, 0),
                new("Actor", "TEXT", true, 0),
                new("Reason", "TEXT", true, 0),
                new("OperationKey", "TEXT", true, 0),
                new("RequestHash", "TEXT", true, 0),
                new("OccurredAtUtc", "TEXT", true, 0),
                new("BeforeVersion", "INTEGER", true, 0),
                new("AfterVersion", "INTEGER", true, 0),
                new("AfterState", "TEXT", true, 0),
                new("AfterAssigneeId", "TEXT", false, 0),
                new("AfterLinkedCaseId", "TEXT", false, 0)
            ],
            ["EmailResponseEvidence"] =
            [
                new("Id", "TEXT", true, 1),
                new("SentEvidenceId", "TEXT", true, 0),
                new("MessageIdentity", "TEXT", true, 0),
                new("MimeSha256", "TEXT", true, 0),
                new("ReceivedAtUtc", "TEXT", true, 0),
                new("Actor", "TEXT", true, 0),
                new("OperationKey", "TEXT", true, 0),
                new("RequestHash", "TEXT", true, 0)
            ],
            ["TriageResponseEvidenceLinks"] =
            [
                new("TriageId", "TEXT", true, 1),
                new("SentEvidenceId", "TEXT", true, 2),
                new("Actor", "TEXT", true, 0),
                new("OperationKey", "TEXT", true, 0),
                new("Reason", "TEXT", true, 0),
                new("LinkedAtUtc", "TEXT", true, 0)
            ],
            ["RequestUploadReceipts"] =
            [
                new("Id", "TEXT", true, 1),
                new("RequestId", "TEXT", true, 0),
                new("OccurrenceId", "TEXT", true, 0),
                new("VersionId", "TEXT", true, 0),
                new("OperationKey", "TEXT", true, 0),
                new("ContentHash", "TEXT", true, 0),
                new("ReceivedAtUtc", "TEXT", true, 0)
            ],
            ["CaseReportApprovals"] =
            [
                new("Id", "TEXT", true, 1), new("CaseId", "TEXT", true, 0),
                new("ArtifactIdentity", "TEXT", true, 0), new("ArtifactSha256", "TEXT", true, 0),
                new("ApprovedByKind", "TEXT", true, 0), new("ApprovedBySubjectId", "TEXT", true, 0),
                new("ApprovedByRolesJson", "TEXT", true, 0), new("ApprovedAtUtc", "TEXT", true, 0)
            ],
            ["CaseReportSentEvidence"] =
            [
                new("Id", "TEXT", true, 1), new("CaseId", "TEXT", true, 0),
                new("MailboxIdentity", "TEXT", true, 0), new("SentFolderIdentity", "TEXT", true, 0),
                new("ImmutableItemIdentity", "TEXT", true, 0), new("ConversationIdentity", "TEXT", true, 0),
                new("ReplyChainIdentity", "TEXT", true, 0), new("SentAtUtc", "TEXT", true, 0),
                new("LinkedAtUtc", "TEXT", true, 0), new("LinkedByKind", "TEXT", true, 0),
                new("LinkedBySubjectId", "TEXT", true, 0), new("LinkedByRolesJson", "TEXT", true, 0)
            ],
            ["CaseWorkflows"] =
            [
                new("CaseId", "TEXT", true, 1), new("State", "TEXT", true, 0),
                new("AssignedEngineerId", "TEXT", false, 0), new("ReportApprovalId", "TEXT", false, 0),
                new("ReportSentEvidenceId", "TEXT", false, 0), new("ClosureOutcome", "TEXT", false, 0),
                new("ReplacementCaseId", "TEXT", false, 0), new("Version", "INTEGER", true, 0),
                new("EditLeaseTokenHash", "TEXT", false, 0), new("EditLeaseHolder", "TEXT", false, 0),
                new("EditLeaseOperationKey", "TEXT", false, 0), new("EditLeaseExpiresAtUtc", "TEXT", false, 0),
                new("ConcurrencyToken", "TEXT", true, 0)
            ],
            ["CaseDueWork"] =
            [
                new("CaseId", "TEXT", true, 1), new("MissingMaterialReason", "TEXT", true, 0),
                new("DueBy", "TEXT", false, 0), new("State", "TEXT", true, 0),
                new("NextChaseAtUtc", "TEXT", false, 0), new("HeldAtUtc", "TEXT", false, 0),
                new("RemainingChaseIntervalTicks", "INTEGER", false, 0), new("MostRecentChannel", "TEXT", false, 0),
                new("MostRecentOutcome", "TEXT", false, 0), new("MostRecentNote", "TEXT", false, 0),
                new("Version", "INTEGER", true, 0), new("ConcurrencyToken", "TEXT", true, 0)
            ],
            ["CaseWorkflowEvents"] =
            [
                new("Id", "TEXT", true, 1), new("CaseId", "TEXT", true, 0),
                new("EventType", "TEXT", true, 0), new("OperationKey", "TEXT", true, 0),
                new("RequestHash", "TEXT", true, 0), new("ActorKind", "TEXT", true, 0),
                new("ActorSubjectId", "TEXT", true, 0), new("ActorRolesJson", "TEXT", true, 0),
                new("Reason", "TEXT", true, 0), new("OccurredAtUtc", "TEXT", true, 0),
                new("BeforeVersion", "INTEGER", true, 0), new("AfterVersion", "INTEGER", true, 0)
            ],
            ["CaseManualChases"] =
            [
                new("Id", "TEXT", true, 1), new("CaseId", "TEXT", true, 0),
                new("OperationKey", "TEXT", true, 0), new("RequestHash", "TEXT", true, 0),
                new("ActorKind", "TEXT", true, 0), new("ActorSubjectId", "TEXT", true, 0),
                new("ActorRolesJson", "TEXT", true, 0), new("Reason", "TEXT", true, 0),
                new("Channel", "TEXT", true, 0), new("TargetPartyOrAddress", "TEXT", true, 0),
                new("AttemptedAtUtc", "TEXT", true, 0), new("Outcome", "TEXT", true, 0),
                new("Note", "TEXT", false, 0), new("ResultingVersion", "INTEGER", true, 0)
            ]
        };

    private static readonly Dictionary<string, IndexDefinition[]> ExpectedIndexes =
        new Dictionary<string, IndexDefinition[]>(StringComparer.Ordinal)
        {
            ["__EFMigrationsLock"] = [],
            ["__EFMigrationsHistory"] = [new(null, true, "pk", ["MigrationId"])],
            ["ApplicationInitializations"] = [new(null, true, "pk", ["Id"])],
            ["ActionHistory"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_ActionHistory_AggregateType_AggregateId_OccurredAtUtc", false, "c", ["AggregateType", "AggregateId", "OccurredAtUtc"]),
                new("IX_ActionHistory_AggregateType_CorrelationId", false, "c", ["AggregateType", "CorrelationId"]),
                new("IX_ActionHistory_OccurredAtUtc", false, "c", ["OccurredAtUtc"])
            ],
            ["SecurityEvents"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_SecurityEvents_OccurredAtUtc", false, "c", ["OccurredAtUtc"]),
                new("IX_SecurityEvents_SubjectId_OccurredAtUtc", false, "c", ["SubjectId", "OccurredAtUtc"])
            ],
            ["AspNetRoles"] =
            [
                new(null, true, "pk", ["Id"]),
                new("RoleNameIndex", true, "c", ["NormalizedName"])
            ],
            ["AspNetUsers"] =
            [
                new(null, true, "pk", ["Id"]),
                new("EmailIndex", false, "c", ["NormalizedEmail"]),
                new("UserNameIndex", true, "c", ["NormalizedUserName"])
            ],
            ["AspNetRoleClaims"] =
            [
                new("IX_AspNetRoleClaims_RoleId", false, "c", ["RoleId"])
            ],
            ["AspNetUserClaims"] =
            [
                new("IX_AspNetUserClaims_UserId", false, "c", ["UserId"])
            ],
            ["AspNetUserLogins"] =
            [
                new(null, true, "pk", ["LoginProvider", "ProviderKey"]),
                new("IX_AspNetUserLogins_UserId", false, "c", ["UserId"])
            ],
            ["AspNetUserRoles"] =
            [
                new(null, true, "pk", ["UserId", "RoleId"]),
                new("IX_AspNetUserRoles_RoleId", false, "c", ["RoleId"])
            ],
            ["AspNetUserTokens"] = [new(null, true, "pk", ["UserId", "LoginProvider", "Name"])],
            ["OpenIddictApplications"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_OpenIddictApplications_ClientId", true, "c", ["ClientId"])
            ],
            ["OpenIddictAuthorizations"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type", false, "c", ["ApplicationId", "Status", "Subject", "Type"])
            ],
            ["OpenIddictScopes"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_OpenIddictScopes_Name", true, "c", ["Name"])
            ],
            ["OpenIddictTokens"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_OpenIddictTokens_ApplicationId_Status_Subject_Type", false, "c", ["ApplicationId", "Status", "Subject", "Type"]),
                new("IX_OpenIddictTokens_AuthorizationId", false, "c", ["AuthorizationId"]),
                new("IX_OpenIddictTokens_ReferenceId", true, "c", ["ReferenceId"])
            ],
            ["IntakeReceipts"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_IntakeReceipts_SourceChannel_ExternalReceiptToken", true, "c", ["SourceChannel", "ExternalReceiptToken"]),
                new("IX_IntakeReceipts_SourceHash", false, "c", ["SourceHash"])
            ],
            ["InstructionDrafts"] = [new(null, true, "pk", ["IntakeReceiptId"])],
            ["IntakeAssets"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_IntakeAssets_IntakeReceiptId_ContentHash", false, "c", ["IntakeReceiptId", "ContentHash"])
            ],
            ["IntakeReceiptEvents"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_IntakeReceiptEvents_IntakeReceiptId", false, "c", ["IntakeReceiptId"])
            ]
            ,
            ["IntakeStagedReceipts"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_IntakeStagedReceipts_SourceChannel_ExternalReceiptToken", true, "c", ["SourceChannel", "ExternalReceiptToken"]),
                new("IX_IntakeStagedReceipts_SourceHash", false, "c", ["SourceHash"])
            ],
            ["IntakeWorkItems"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_IntakeWorkItems_OperationKey", true, "c", ["OperationKey"]),
                new("IX_IntakeWorkItems_StagedReceiptId", true, "c", ["StagedReceiptId"]),
                new("IX_IntakeWorkItems_State_DueAtUtc", false, "c", ["State", "DueAtUtc"])
            ]
            ,
            ["IntakeEvaluations"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_IntakeEvaluations_StagedReceiptId_Revision", true, "c", ["StagedReceiptId", "Revision"])
            ]
            ,
            ["ApprovedInboxPollStates"] =
            [
                new(null, true, "pk", ["MailboxId"]),
                new("IX_ApprovedInboxPollStates_DueAtUtc", false, "c", ["DueAtUtc"]),
                new("IX_ApprovedInboxPollStates_MailboxAddress", true, "c", ["MailboxAddress"])
            ],
            ["IntakeMailRouteDecisions"] = [new(null, true, "pk", ["IntakeReceiptId"])],
            ["ProviderDomainPackages"] = [new(null, true, "pk", ["Version"])],
            ["ProviderReferences"] = [new(null, true, "pk", ["Version", "Code"])],
            ["ProviderDomainEvidence"] =
            [
                new(null, true, "pk", ["Version", "Code", "DomainSuffix"]),
                new("IX_ProviderDomainEvidence_Version_DomainSuffix", false, "c", ["Version", "DomainSuffix"])
            ],
            ["Organizations"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_Organizations_Name", false, "c", ["Name"])
            ],
            ["OrganizationRoles"] = [new(null, true, "pk", ["OrganizationId", "Role"])],
            ["PrincipalSequenceLineages"] = [new(null, true, "pk", ["Id"])],
            ["Principals"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_Principals_Code", true, "c", ["Code"]),
                new("IX_Principals_OrganizationId", false, "c", ["OrganizationId"]),
                new("IX_Principals_PredecessorId", true, "c", ["PredecessorId"]),
                new("IX_Principals_SequenceLineageId", false, "c", ["SequenceLineageId"]),
                new("IX_Principals_SuccessorId", true, "c", ["SuccessorId"])
            ],
            ["CaseSequences"] = [new(null, true, "pk", ["SequenceLineageId", "Year"])],
            ["Cases"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_Cases_AuditReference", true, "c", ["AuditReference"]),
                new("IX_Cases_OriginIntakeReceiptId", true, "c", ["OriginIntakeReceiptId"]),
                new("IX_Cases_PrincipalId", false, "c", ["PrincipalId"]),
                new("IX_Cases_Reference", true, "c", ["Reference"]),
                new("IX_Cases_SequenceLineageId_Year_Sequence", true, "c", ["SequenceLineageId", "Year", "Sequence"])
            ],
            ["CaseHistory"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_CaseHistory_CaseId_OccurredAtUtc", false, "c", ["CaseId", "OccurredAtUtc"]),
                new("IX_CaseHistory_OperationKey", true, "c", ["OperationKey"])
            ],
            ["ExternalWorkItems"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_ExternalWorkItems_CaseId", false, "c", ["CaseId"]),
                new("IX_ExternalWorkItems_OperationKey", true, "c", ["OperationKey"]),
                new("IX_ExternalWorkItems_State_DueAtUtc", false, "c", ["State", "DueAtUtc"])
            ],
            ["CaseIntakeLinks"] =
            [
                new(null, true, "pk", ["IntakeReceiptId"]),
                new("IX_CaseIntakeLinks_CaseId", false, "c", ["CaseId"]),
                new("IX_CaseIntakeLinks_CustodyWorkId", true, "c", ["CustodyWorkId"]),
                new("IX_CaseIntakeLinks_OperationKey", true, "c", ["OperationKey"])
            ],
            ["BoxFileRequests"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_BoxFileRequests_CaseId_CreateOperationKey", true, "c", ["CaseId", "CreateOperationKey"]),
                new("IX_BoxFileRequests_LinkTokenDigest", true, "c", ["LinkTokenDigest"])
            ],
            ["CaseDocuments"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_CaseDocuments_CaseId_SourceOccurrenceIdentity", true, "c", ["CaseId", "SourceOccurrenceIdentity"])
            ],
            ["RequestUploadLinks"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_RequestUploadLinks_CaseId_CreateOperationKey", true, "c", ["CaseId", "CreateOperationKey"]),
                new("IX_RequestUploadLinks_TokenDigest", true, "c", ["TokenDigest"])
            ],
            ["DocumentVersions"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_DocumentVersions_DocumentId_Version", true, "c", ["DocumentId", "Version"])
            ],
            ["DocumentOccurrences"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_DocumentOccurrences_CaseId_DocumentId", false, "c", ["CaseId", "DocumentId"]),
                new("IX_DocumentOccurrences_CaseId_OperationKey", true, "c", ["CaseId", "OperationKey"]),
                new("IX_DocumentOccurrences_DocumentId", false, "c", ["DocumentId"]),
                new("IX_DocumentOccurrences_VersionId", false, "c", ["VersionId"])
            ],
            ["Triage"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_Triage_CreationOperationKey", true, "c", ["CreationOperationKey"]),
                new("IX_Triage_LinkedCaseId", false, "c", ["LinkedCaseId"]),
                new("IX_Triage_OriginReceiptId", true, "c", ["OriginReceiptId"]),
                new("IX_Triage_SourceChannel_ExternalReceiptToken", true, "c", ["SourceChannel", "ExternalReceiptToken"]),
                new("IX_Triage_State_CreatedAtUtc", false, "c", ["State", "CreatedAtUtc"])
            ],
            ["SentEmailEvidence"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_SentEmailEvidence_ChaseDueAtUtc_TriageId", false, "c", ["ChaseDueAtUtc", "TriageId"]),
                new("IX_SentEmailEvidence_MessageIdentity", true, "c", ["MessageIdentity"]),
                new("IX_SentEmailEvidence_OperationKey", true, "c", ["OperationKey"]),
                new("IX_SentEmailEvidence_TriageId", false, "c", ["TriageId"])
            ],
            ["TriageFindings"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_TriageFindings_OperationKey", true, "c", ["OperationKey"]),
                new("IX_TriageFindings_SupersedesFindingId", true, "c", ["SupersedesFindingId"]),
                new("IX_TriageFindings_TriageId_RecordedAtUtc", false, "c", ["TriageId", "RecordedAtUtc"])
            ],
            ["TriageHistory"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_TriageHistory_OperationKey", true, "c", ["OperationKey"]),
                new("IX_TriageHistory_TriageId_OccurredAtUtc", false, "c", ["TriageId", "OccurredAtUtc"])
            ],
            ["EmailResponseEvidence"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_EmailResponseEvidence_MessageIdentity", true, "c", ["MessageIdentity"]),
                new("IX_EmailResponseEvidence_OperationKey", true, "c", ["OperationKey"]),
                new("IX_EmailResponseEvidence_SentEvidenceId", true, "c", ["SentEvidenceId"])
            ],
            ["TriageResponseEvidenceLinks"] =
            [
                new(null, true, "pk", ["TriageId", "SentEvidenceId"]),
                new("IX_TriageResponseEvidenceLinks_OperationKey", true, "c", ["OperationKey"]),
                new("IX_TriageResponseEvidenceLinks_SentEvidenceId", false, "c", ["SentEvidenceId"])
            ],
            ["RequestUploadReceipts"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_RequestUploadReceipts_OccurrenceId", false, "c", ["OccurrenceId"]),
                new("IX_RequestUploadReceipts_RequestId_OperationKey", true, "c", ["RequestId", "OperationKey"]),
                new("IX_RequestUploadReceipts_VersionId", false, "c", ["VersionId"])
            ],
            ["CaseReportApprovals"] = [new(null, true, "pk", ["Id"]), new("IX_CaseReportApprovals_CaseId_ArtifactIdentity_ArtifactSha256", true, "c", ["CaseId", "ArtifactIdentity", "ArtifactSha256"])],
            ["CaseReportSentEvidence"] = [new(null, true, "pk", ["Id"]), new("IX_CaseReportSentEvidence_CaseId_ImmutableItemIdentity", true, "c", ["CaseId", "ImmutableItemIdentity"])],
            ["CaseWorkflows"] =
            [
                new(null, true, "pk", ["CaseId"]),
                new("IX_CaseWorkflows_ReplacementCaseId", false, "c", ["ReplacementCaseId"]),
                new("IX_CaseWorkflows_ReportApprovalId", true, "c", ["ReportApprovalId"]),
                new("IX_CaseWorkflows_ReportSentEvidenceId", true, "c", ["ReportSentEvidenceId"])
            ],
            ["CaseDueWork"] = [new(null, true, "pk", ["CaseId"]), new("IX_CaseDueWork_State_NextChaseAtUtc", false, "c", ["State", "NextChaseAtUtc"])],
            ["CaseWorkflowEvents"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_CaseWorkflowEvents_CaseId_AfterVersion", true, "c", ["CaseId", "AfterVersion"]),
                new("IX_CaseWorkflowEvents_CaseId_OperationKey", true, "c", ["CaseId", "OperationKey"])
            ],
            ["CaseManualChases"] = [new(null, true, "pk", ["Id"]), new("IX_CaseManualChases_CaseId_OperationKey", true, "c", ["CaseId", "OperationKey"])]
        };

    private static readonly Dictionary<string, ForeignKeyDefinition[]> ExpectedForeignKeys =
        new Dictionary<string, ForeignKeyDefinition[]>(StringComparer.Ordinal)
        {
            ["__EFMigrationsLock"] = [],
            ["__EFMigrationsHistory"] = [],
            ["ApplicationInitializations"] = [],
            ["ActionHistory"] = [],
            ["SecurityEvents"] = [],
            ["AspNetRoles"] = [],
            ["AspNetUsers"] = [],
            ["AspNetRoleClaims"] = [new("RoleId", "AspNetRoles", "Id", "CASCADE")],
            ["AspNetUserClaims"] = [new("UserId", "AspNetUsers", "Id", "CASCADE")],
            ["AspNetUserLogins"] = [new("UserId", "AspNetUsers", "Id", "CASCADE")],
            ["AspNetUserRoles"] =
            [
                new("RoleId", "AspNetRoles", "Id", "CASCADE"),
                new("UserId", "AspNetUsers", "Id", "CASCADE")
            ],
            ["AspNetUserTokens"] = [new("UserId", "AspNetUsers", "Id", "CASCADE")],
            ["OpenIddictApplications"] = [],
            ["OpenIddictAuthorizations"] =
                [new("ApplicationId", "OpenIddictApplications", "Id", "NO ACTION")],
            ["OpenIddictScopes"] = [],
            ["OpenIddictTokens"] =
            [
                new("ApplicationId", "OpenIddictApplications", "Id", "NO ACTION"),
                new("AuthorizationId", "OpenIddictAuthorizations", "Id", "NO ACTION")
            ],
            ["IntakeReceipts"] = [],
            ["InstructionDrafts"] = [new("IntakeReceiptId", "IntakeReceipts", "Id", "CASCADE")],
            ["IntakeAssets"] = [new("IntakeReceiptId", "IntakeReceipts", "Id", "CASCADE")],
            ["IntakeReceiptEvents"] = [new("IntakeReceiptId", "IntakeReceipts", "Id", "RESTRICT")]
            ,
            ["IntakeStagedReceipts"] = [],
            ["IntakeWorkItems"] = [new("StagedReceiptId", "IntakeStagedReceipts", "Id", "RESTRICT")]
            ,
            ["IntakeEvaluations"] = [new("StagedReceiptId", "IntakeStagedReceipts", "Id", "RESTRICT")]
            ,
            ["ApprovedInboxPollStates"] = [],
            ["IntakeMailRouteDecisions"] =
                [new("IntakeReceiptId", "IntakeReceipts", "Id", "CASCADE")],
            ["ProviderDomainPackages"] = [],
            ["ProviderReferences"] = [new("Version", "ProviderDomainPackages", "Version", "RESTRICT")],
            ["ProviderDomainEvidence"] =
            [
                new("Code", "ProviderReferences", "Code", "RESTRICT"),
                new("Version", "ProviderReferences", "Version", "RESTRICT")
            ],
            ["Organizations"] = [],
            ["OrganizationRoles"] = [new("OrganizationId", "Organizations", "Id", "CASCADE")],
            ["PrincipalSequenceLineages"] = [],
            ["Principals"] =
            [
                new("OrganizationId", "Organizations", "Id", "RESTRICT"),
                new("PredecessorId", "Principals", "Id", "RESTRICT"),
                new("SequenceLineageId", "PrincipalSequenceLineages", "Id", "RESTRICT"),
                new("SuccessorId", "Principals", "Id", "RESTRICT")
            ],
            ["CaseSequences"] =
                [new("SequenceLineageId", "PrincipalSequenceLineages", "Id", "RESTRICT")],
            ["Cases"] =
            [
                new("OriginIntakeReceiptId", "IntakeReceipts", "Id", "RESTRICT"),
                new("PrincipalId", "Principals", "Id", "RESTRICT")
            ],
            ["CaseHistory"] = [new("CaseId", "Cases", "Id", "RESTRICT")],
            ["ExternalWorkItems"] = [new("CaseId", "Cases", "Id", "RESTRICT")],
            ["CaseIntakeLinks"] =
            [
                new("CaseId", "Cases", "Id", "RESTRICT"),
                new("CustodyWorkId", "ExternalWorkItems", "Id", "RESTRICT"),
                new("IntakeReceiptId", "IntakeReceipts", "Id", "RESTRICT")
            ],
            ["BoxFileRequests"] = [new("CaseId", "Cases", "Id", "RESTRICT")],
            ["CaseDocuments"] = [new("CaseId", "Cases", "Id", "RESTRICT")],
            ["RequestUploadLinks"] = [new("CaseId", "Cases", "Id", "RESTRICT")],
            ["DocumentVersions"] = [new("DocumentId", "CaseDocuments", "Id", "RESTRICT")],
            ["DocumentOccurrences"] =
            [
                new("CaseId", "Cases", "Id", "RESTRICT"),
                new("DocumentId", "CaseDocuments", "Id", "RESTRICT"),
                new("VersionId", "DocumentVersions", "Id", "RESTRICT")
            ],
            ["Triage"] =
            [
                new("LinkedCaseId", "Cases", "Id", "RESTRICT"),
                new("OriginReceiptId", "IntakeReceipts", "Id", "RESTRICT")
            ],
            ["SentEmailEvidence"] = [new("TriageId", "Triage", "Id", "RESTRICT")],
            ["TriageFindings"] =
            [
                new("SupersedesFindingId", "TriageFindings", "Id", "RESTRICT"),
                new("TriageId", "Triage", "Id", "RESTRICT")
            ],
            ["TriageHistory"] = [new("TriageId", "Triage", "Id", "RESTRICT")],
            ["EmailResponseEvidence"] =
                [new("SentEvidenceId", "SentEmailEvidence", "Id", "RESTRICT")],
            ["TriageResponseEvidenceLinks"] =
            [
                new("SentEvidenceId", "SentEmailEvidence", "Id", "RESTRICT"),
                new("TriageId", "Triage", "Id", "RESTRICT")
            ],
            ["RequestUploadReceipts"] =
            [
                new("OccurrenceId", "DocumentOccurrences", "Id", "RESTRICT"),
                new("RequestId", "RequestUploadLinks", "Id", "RESTRICT"),
                new("VersionId", "DocumentVersions", "Id", "RESTRICT")
            ],
            ["CaseReportApprovals"] = [new("CaseId", "Cases", "Id", "RESTRICT")],
            ["CaseReportSentEvidence"] = [new("CaseId", "Cases", "Id", "RESTRICT")],
            ["CaseWorkflows"] =
            [
                new("CaseId", "Cases", "Id", "RESTRICT"),
                new("ReplacementCaseId", "Cases", "Id", "RESTRICT"),
                new("ReportApprovalId", "CaseReportApprovals", "Id", "RESTRICT"),
                new("ReportSentEvidenceId", "CaseReportSentEvidence", "Id", "RESTRICT")
            ],
            ["CaseDueWork"] = [new("CaseId", "CaseWorkflows", "CaseId", "RESTRICT")],
            ["CaseWorkflowEvents"] = [new("CaseId", "CaseWorkflows", "CaseId", "RESTRICT")],
            ["CaseManualChases"] = [new("CaseId", "CaseDueWork", "CaseId", "RESTRICT")]
        };

    public static async Task ValidateAsync(
        PegasusDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Database.IsSqlite())
        {
            throw new InvalidOperationException("The Development baseline guard supports only SQLite.");
        }

        var migrations = context.Database.GetMigrations().ToArray();
        if (!migrations.SequenceEqual(ExpectedMigrations, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The Development SQLite baseline migration sequence does not match the current model.");
        }

        var connection = context.Database.GetDbConnection();
        var closeWhenComplete = connection.State == ConnectionState.Closed;
        if (closeWhenComplete)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var tables = await ReadTablesAsync(connection, cancellationToken);
            if (tables.Count == 0)
            {
                return;
            }

            if (!tables.SetEquals(ExpectedColumns.Keys))
            {
                throw IncompatibleSchema("table set");
            }

            var history = await ReadMigrationHistoryAsync(connection, cancellationToken);
            if (history.Count != ExpectedMigrations.Length)
            {
                throw IncompatibleSchema("migration history");
            }
            for (var index = 0; index < ExpectedMigrations.Length; index++)
            {
                if (!string.Equals(history[index].MigrationId, ExpectedMigrations[index], StringComparison.Ordinal)
                    || !string.Equals(history[index].ProductVersion, "10.0.10", StringComparison.Ordinal))
                {
                    throw IncompatibleSchema("migration history");
                }
            }

            foreach (var table in ExpectedColumns.Keys)
            {
                var columns = await ReadColumnsAsync(connection, table, cancellationToken);
                if (!columns.SequenceEqual(ExpectedColumns[table]))
                {
                    throw IncompatibleSchema($"columns for {table}");
                }

                var indexes = await ReadIndexesAsync(connection, table, cancellationToken);
                if (!EquivalentIndexes(indexes, ExpectedIndexes[table]))
                {
                    throw IncompatibleSchema($"indexes for {table}");
                }

                var foreignKeys = await ReadForeignKeysAsync(connection, table, cancellationToken);
                if (!foreignKeys.SequenceEqual(ExpectedForeignKeys[table]))
                {
                    throw IncompatibleSchema(
                        $"foreign keys for {table}: expected [{string.Join("; ", ExpectedForeignKeys[table])}], actual [{string.Join("; ", foreignKeys)}]");
                }
            }
        }
        finally
        {
            if (closeWhenComplete)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<HashSet<string>> ReadTablesAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var tables = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static async Task<List<MigrationHistory>> ReadMigrationHistoryAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT \"MigrationId\", \"ProductVersion\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<MigrationHistory>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(reader.GetString(0), reader.GetString(1)));
        }

        return rows;
    }

    private static async Task<ColumnDefinition[]> ReadColumnsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = new List<(int Ordinal, ColumnDefinition Definition)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add((
                reader.GetInt32(0),
                new(
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3) == 1,
                    reader.GetInt32(5))));
        }

        return columns.OrderBy(item => item.Ordinal).Select(item => item.Definition).ToArray();
    }

    private static async Task<IndexDefinition[]> ReadIndexesAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var indexes = new List<IndexDefinition>();
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_list(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<(string Name, bool Unique, string Origin)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((reader.GetString(1), reader.GetInt32(2) == 1, reader.GetString(3)));
        }

        await reader.DisposeAsync();
        foreach (var row in rows)
        {
            await using var columnCommand = connection.CreateCommand();
            columnCommand.CommandText = $"PRAGMA index_info(\"{row.Name}\");";
            await using var columnReader = await columnCommand.ExecuteReaderAsync(cancellationToken);
            var columns = new List<(int Ordinal, string Name)>();
            while (await columnReader.ReadAsync(cancellationToken))
            {
                columns.Add((columnReader.GetInt32(0), columnReader.GetString(2)));
            }

            indexes.Add(new(
                row.Origin == "c" ? row.Name : null,
                row.Unique,
                row.Origin,
                columns.OrderBy(item => item.Ordinal).Select(item => item.Name).ToArray()));
        }

        return indexes.ToArray();
    }

    private static async Task<ForeignKeyDefinition[]> ReadForeignKeysAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_key_list(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var keys = new List<ForeignKeyDefinition>();
        while (await reader.ReadAsync(cancellationToken))
        {
            keys.Add(new(reader.GetString(3), reader.GetString(2), reader.GetString(4), reader.GetString(6)));
        }

        return keys
            .OrderBy(item => item.From, StringComparer.Ordinal)
            .ThenBy(item => item.Table, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool EquivalentIndexes(
        IReadOnlyList<IndexDefinition> actual,
        IReadOnlyList<IndexDefinition> expected) =>
        actual.Count == expected.Count
        && expected.All(expectedIndex => actual.Any(actualIndex =>
            string.Equals(actualIndex.Name, expectedIndex.Name, StringComparison.Ordinal)
            && actualIndex.Unique == expectedIndex.Unique
            && string.Equals(actualIndex.Origin, expectedIndex.Origin, StringComparison.Ordinal)
            && actualIndex.Columns.SequenceEqual(expectedIndex.Columns)));

    private static InvalidOperationException IncompatibleSchema(string mismatch) => new(
        $"The local SQLite database does not exactly match the current Development baseline ({mismatch}). " +
        "The database was left unchanged; use the new configured Development database path.");

    private sealed record MigrationHistory(string MigrationId, string ProductVersion);
    private sealed record ColumnDefinition(string Name, string Type, bool NotNull, int PrimaryKeyOrdinal);
    private sealed record IndexDefinition(string? Name, bool Unique, string Origin, string[] Columns);
    private sealed record ForeignKeyDefinition(string From, string Table, string To, string OnDelete);
}

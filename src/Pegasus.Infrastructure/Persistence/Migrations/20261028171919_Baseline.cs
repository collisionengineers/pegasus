using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActionHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AggregateId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventKind = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorRolesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PolicyVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiJobs",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SubjectKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubjectReference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Instruction = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TargetPercentOfEngineerValue = table.Column<int>(type: "int", nullable: true),
                    EngineerValueAtSend = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedByKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TakenBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TakenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProgressNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResultKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ResultReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResultText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    MarketResearchDocumentOccurrenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MarketResearchDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MarketResearchValuationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MarketResearchCompletionHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    DraftReadyAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiJobs", x => x.JobId);
                    table.CheckConstraint("CK_AiJobs_Kind", "[Kind] IN ('Estimate', 'UnidentifiedResolution', 'QueryResponse', 'UnidentifiedQueuePass', 'MarketResearch')");
                    table.CheckConstraint("CK_AiJobs_MarketResearchResult", "([ResultKind] = 'MarketResearch' AND [MarketResearchDocumentOccurrenceId] IS NOT NULL AND [MarketResearchDocumentVersionId] IS NOT NULL AND [MarketResearchValuationId] IS NOT NULL AND [MarketResearchCompletionHash] IS NOT NULL) OR (([ResultKind] IS NULL OR [ResultKind] <> 'MarketResearch') AND [MarketResearchDocumentOccurrenceId] IS NULL AND [MarketResearchDocumentVersionId] IS NULL AND [MarketResearchValuationId] IS NULL AND [MarketResearchCompletionHash] IS NULL)");
                    table.CheckConstraint("CK_AiJobs_ResultKind", "[ResultKind] IS NULL OR [ResultKind] IN ('Estimate', 'ProposedResolution', 'DraftReply', 'MarketResearch')");
                    table.CheckConstraint("CK_AiJobs_State", "[State] IN ('Queued', 'Taken', 'DraftReady', 'Completed', 'Failed', 'Cancelled', 'Expired')");
                    table.CheckConstraint("CK_AiJobs_SubjectKind", "[SubjectKind] IN ('Case', 'Unidentified', 'Queue')");
                    table.CheckConstraint("CK_AiJobs_TargetPercent", "[TargetPercentOfEngineerValue] IS NULL OR [TargetPercentOfEngineerValue] BETWEEN 1 AND 100");
                });

            migrationBuilder.CreateTable(
                name: "ApprovedMailboxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    AllowIncoming = table.Column<bool>(type: "bit", nullable: false),
                    AllowOutgoing = table.Column<bool>(type: "bit", nullable: false),
                    MailboxGeneration = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MailboxIdentity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InboxFolderIdentity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SentFolderIdentity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ActivatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovedMailboxes", x => x.Id);
                    table.CheckConstraint("CK_ApprovedMailboxes_MailboxGeneration", "[MailboxGeneration] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ApprovedSentPollStates",
                columns: table => new
                {
                    MailboxId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MailboxAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    SentFolderIdentity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ScopeFingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Generation = table.Column<long>(type: "bigint", nullable: false),
                    StartBoundaryUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Cursor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastCompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastFailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovedSentPollStates", x => x.MailboxId);
                    table.CheckConstraint("CK_ApprovedSentPollStates_Generation", "[Generation] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsSignOffEngineer = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SignOffPrintedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SignOffQualifications = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SignOffSignature = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    WorkCentreLastSeenUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SignOffSignatureDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsDefaultSignOffEngineer = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.CheckConstraint("CK_AspNetUsers_DefaultSignOffEligibility", "[IsDefaultSignOffEngineer] = 0 OR [IsSignOffEngineer] = 1");
                    table.CheckConstraint("CK_AspNetUsers_Version", "[Version] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "CaseListPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ColumnKeysJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RemovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseListPresets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EditScopes",
                columns: table => new
                {
                    ScopeKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HolderKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Holder = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TokenHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ExpectedVersion = table.Column<long>(type: "bigint", nullable: false),
                    Generation = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditScopes", x => new { x.ScopeKind, x.RecordId });
                    table.CheckConstraint("CK_EditScopes_ExpectedVersion", "[ExpectedVersion] >= 0");
                    table.CheckConstraint("CK_EditScopes_Generation", "[Generation] > 0");
                });

            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                columns: table => new
                {
                    Purpose = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplates", x => x.Purpose);
                });

            migrationBuilder.CreateTable(
                name: "ImageIntakeSequences",
                columns: table => new
                {
                    NormalizedVehicleRegistration = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LastAllocatedSequence = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageIntakeSequences", x => x.NormalizedVehicleRegistration);
                    table.CheckConstraint("CK_ImageIntakeSequences_LastAllocatedSequence", "[LastAllocatedSequence] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ImageTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Colour = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreateOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InspectionLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Postcode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceRecordId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionLocations", x => x.Id);
                    table.CheckConstraint("CK_InspectionLocations_Address", "[Address] <> ''");
                    table.CheckConstraint("CK_InspectionLocations_Name", "[Name] <> ''");
                    table.CheckConstraint("CK_InspectionLocations_Postcode", "[Postcode] <> ''");
                    table.CheckConstraint("CK_InspectionLocations_Version", "[Version] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "IntakeReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceLength = table.Column<long>(type: "bigint", nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceChannel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExternalReceiptToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SourceReaderKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceReaderVersion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExtractionPolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ExtractionPolicyVersion = table.Column<int>(type: "int", nullable: true),
                    Decision = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OcrCandidatesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeclaredCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntakeStagedReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceLength = table.Column<long>(type: "bigint", nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceChannel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExternalReceiptToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StagedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeclaredCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeStagedReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabourRateCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PanelRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabourRateCards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictApplications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ApplicationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClientSecret = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ConsentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayNames = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JsonWebKeySet = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Permissions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PostLogoutRedirectUris = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RedirectUris = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Requirements = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Settings = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictScopes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Descriptions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayNames = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Resources = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictScopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationAdministrationOperations",
                columns: table => new
                {
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CommandKind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationAdministrationOperations", x => x.OperationKey);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false, computedColumnSql: "UPPER(LTRIM(RTRIM([Name])))", stored: true),
                    ContactPerson = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Telephone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NotesOnEveryCase = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                    table.CheckConstraint("CK_Organizations_Name", "[Name] <> ''");
                    table.CheckConstraint("CK_Organizations_Role", "[Role] IN ('principal', 'claim_source', 'repairer', 'storage', 'third_party_engineer')");
                    table.CheckConstraint("CK_Organizations_Version", "[Version] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "PrincipalSequenceLineages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrincipalSequenceLineages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProblemReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Route = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CaseReference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IssueNumber = table.Column<int>(type: "int", nullable: true),
                    IssueUrl = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Failure = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DispatchClaimExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DispatchClaimToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemReports", x => x.Id);
                    table.CheckConstraint("CK_ProblemReports_Status", "[Status] IN ('Sent', 'NotSent', 'Unknown')");
                });

            migrationBuilder.CreateTable(
                name: "ReleaseNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SourceSha = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CreatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseNotes", x => x.Id);
                    table.CheckConstraint("CK_ReleaseNotes_Status", "[Status] IN ('Draft', 'Published')");
                });

            migrationBuilder.CreateTable(
                name: "SecurityEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SendToAiControl",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SendToAiControl", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffMailSendOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MailboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MailboxGeneration = table.Column<long>(type: "bigint", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ContextId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContextVersion = table.Column<long>(type: "bigint", nullable: false),
                    SecondContextId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SecondContextVersion = table.Column<long>(type: "bigint", nullable: true),
                    ComposeMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OriginalRetainedMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalImmutableMessageId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalInternetMessageId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalConversationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolvesCaseQueries = table.Column<bool>(type: "bit", nullable: false),
                    RecipientsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttachmentsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AttemptStage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DraftImmutableId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProtectedUploadSession = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadSessionExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CorrelationMarker = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ObservedSentImmutableMessageId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProviderSentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ObservedSentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReconciliationContinuation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReportDispatchJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InstructionMoveState = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    InstructionMoveAttempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    InstructionMovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    InstructionMoveFailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffMailSendOperations", x => x.Id);
                    table.CheckConstraint("CK_StaffMailSendOperations_ActorKind", "[ActorKind] IN ('Staff', 'Automation')");
                    table.CheckConstraint("CK_StaffMailSendOperations_AttemptStage", "[AttemptStage] IS NULL OR [AttemptStage] IN ('CreateDraft', 'Attach', 'Send', 'ObserveSent')");
                    table.CheckConstraint("CK_StaffMailSendOperations_SecondReport", "([SecondContextId] IS NULL AND [SecondContextVersion] IS NULL) OR ([SecondContextId] IS NOT NULL AND [SecondContextVersion] > 0 AND [Purpose] = 'CaseReport' AND [SecondContextId] <> [ContextId])");
                    table.CheckConstraint("CK_StaffMailSendOperations_State", "[State] IN ('Prepared', 'DraftCreating', 'DraftReady', 'Sending', 'Submitted', 'Sent', 'Failed', 'Unknown', 'Cancelled')");
                });

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
                    ReconciledAssociationVersion = table.Column<long>(type: "bigint", nullable: true),
                    FileKind = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                name: "UnroadworthyReasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrincipalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnroadworthyReasons", x => x.Id);
                    table.CheckConstraint("CK_UnroadworthyReasons_Text", "[Text] <> ''");
                });

            migrationBuilder.CreateTable(
                name: "ValuationPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SuggestedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    RemovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValuationPresets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkCentreDismissals",
                columns: table => new
                {
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DismissedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DismissedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkCentreDismissals", x => x.RecordId);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowConfigurations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    RequireInstructions = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RequireImages = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ChaseIntervalDays = table.Column<int>(type: "int", nullable: false, defaultValue: 7),
                    UnidentifiedTargetDays = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TriageTargetDays = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    HeldTargetDays = table.Column<int>(type: "int", nullable: false, defaultValue: 7),
                    ReviewTargetDays = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    AiDraftTargetDays = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowConfigurations", x => x.Id);
                    table.CheckConstraint("CK_WorkflowConfigurations_ChaseIntervalDays", "[ChaseIntervalDays] BETWEEN 1 AND 365");
                    table.CheckConstraint("CK_WorkflowConfigurations_TargetDays", "[UnidentifiedTargetDays] BETWEEN 0 AND 365 AND [TriageTargetDays] BETWEEN 0 AND 365 AND [HeldTargetDays] BETWEEN 0 AND 365 AND [ReviewTargetDays] BETWEEN 0 AND 365 AND [AiDraftTargetDays] BETWEEN 0 AND 365");
                });

            migrationBuilder.CreateTable(
                name: "ApprovedInboxPollStates",
                columns: table => new
                {
                    ApprovedMailboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MailboxAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ScopeFingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Generation = table.Column<long>(type: "bigint", nullable: false),
                    ActivatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartBoundaryUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Cursor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastCompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastFailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PendingWakeAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovedInboxPollStates", x => x.ApprovedMailboxId);
                    table.CheckConstraint("CK_ApprovedInboxPollStates_Generation", "[Generation] >= 0");
                    table.ForeignKey(
                        name: "FK_ApprovedInboxPollStates_ApprovedMailboxes_ApprovedMailboxId",
                        column: x => x.ApprovedMailboxId,
                        principalTable: "ApprovedMailboxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApprovedMailboxSubscriptions",
                columns: table => new
                {
                    ApprovedMailboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Resource = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LifecycleState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Generation = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    LastMaintainedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastMaintenanceFailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovedMailboxSubscriptions", x => x.ApprovedMailboxId);
                    table.ForeignKey(
                        name: "FK_ApprovedMailboxSubscriptions_ApprovedMailboxes_ApprovedMailboxId",
                        column: x => x.ApprovedMailboxId,
                        principalTable: "ApprovedMailboxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RetainedMailboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MailboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MailboxAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    FolderScope = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FolderIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ImmutableMessageId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ConversationIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InternetMessageIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CanonicalInternetMessageIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, collation: "Latin1_General_100_BIN2"),
                    InReplyToIdentitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExternalReceiptToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SenderAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    SenderDisplayName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ToAddressesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CcAddressesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReplyToAddressesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BodyExcerpt = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    BodyPlainText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    SourceLength = table.Column<long>(type: "bigint", nullable: false),
                    SourceSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RetainedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DismissedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DismissedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetainedMailboxMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetainedMailboxMessages_ApprovedMailboxes_MailboxId",
                        column: x => x.MailboxId,
                        principalTable: "ApprovedMailboxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApprovedSentPollOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MailboxId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MailboxAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    SourceOccurrenceIdentity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    OriginalSourceSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    ObservedSourceSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    EvidenceMarker = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CurrentLocationIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ObservationKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SentFolderIdentity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ImmutableItemIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InternetMessageIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConversationIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReplyChainIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InReplyToIdentitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuthoritativeCaseIdentitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MimeSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    OutcomeKind = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RelatedEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CursorAfterItem = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovedSentPollOutcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovedSentPollOutcomes_ApprovedSentPollStates_MailboxId",
                        column: x => x.MailboxId,
                        principalTable: "ApprovedSentPollStates",
                        principalColumn: "MailboxId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserExternalCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NormalizedAccountKey = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CredentialGeneration = table.Column<long>(type: "bigint", nullable: false),
                    ProtectedCredential = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserExternalCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserExternalCredentials_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstructionDrafts",
                columns: table => new
                {
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SuggestedPrincipalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ClaimantName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ClaimNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VehicleRegistration = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VehicleMake = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VehicleModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VehicleMileage = table.Column<long>(type: "bigint", nullable: true),
                    AccidentCircumstances = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DateOfIncident = table.Column<DateOnly>(type: "date", nullable: true),
                    InspectionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InspectionAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VehicleMileageUnit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    VatStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClaimantAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ClaimantContactNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileHandlerName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FileHandlerEmailAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    FileHandlerPhoneNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstructionDrafts", x => x.IntakeReceiptId);
                    table.ForeignKey(
                        name: "FK_InstructionDrafts_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntakeAllocationAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExpectedReceiptVersion = table.Column<long>(type: "bigint", nullable: false),
                    CaseType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PrincipalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InstructionComplete = table.Column<bool>(type: "bit", nullable: false),
                    ImagesComplete = table.Column<bool>(type: "bit", nullable: false),
                    StandaloneAuditEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcceptedInspectionDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorRolesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CommandHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RecoveryDisposition = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SafeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CaseReference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    AuditReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeAllocationAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeAllocationAttempts_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntakeAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceLabel = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Disposition = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ContentLength = table.Column<long>(type: "bigint", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BoxFileId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BoxVersionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BoxParentFolderId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustodyStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PageNumber = table.Column<int>(type: "int", nullable: true),
                    BoundsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WidthPixels = table.Column<int>(type: "int", nullable: true),
                    HeightPixels = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeAssets_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntakeCaseMatchDecisions",
                columns: table => new
                {
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MatchedCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RedirectedFromCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchKeysJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CandidatesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeCaseMatchDecisions", x => x.IntakeReceiptId);
                    table.ForeignKey(
                        name: "FK_IntakeCaseMatchDecisions_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntakeMailClassificationDecisions",
                columns: table => new
                {
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CaseType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    IsReplyContext = table.Column<bool>(type: "bit", nullable: false),
                    OpensQuery = table.Column<bool>(type: "bit", nullable: false),
                    PredicatesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false),
                    StandaloneAuditReportAssetSourceLabel = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StandaloneAuditReportAssessment = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DecidedByActor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeMailClassificationDecisions", x => x.IntakeReceiptId);
                    table.ForeignKey(
                        name: "FK_IntakeMailClassificationDecisions_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntakeMailRouteDecisions",
                columns: table => new
                {
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Disposition = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RouteOwnerCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RouteKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PrincipalCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PredicatesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false),
                    TransportIdentitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginalIdentitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EffectiveSenderAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    EffectiveSenderSourceLabel = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeMailRouteDecisions", x => x.IntakeReceiptId);
                    table.ForeignKey(
                        name: "FK_IntakeMailRouteDecisions_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntakeReceiptEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeReceiptEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeReceiptEvents_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntakeSearchDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    AttachmentOrdinal = table.Column<int>(type: "int", nullable: true),
                    SourceLabel = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AttachmentFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeSearchDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeSearchDocuments_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntakeSubmissionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceChannel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SubmissionToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpectedMemberCount = table.Column<int>(type: "int", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ParentReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    DiscardedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DiscardedByActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DiscardedByActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DiscardOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DiscardRequestFingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    ImageMatchDeclinedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ImageMatchDeclinedByActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeSubmissionGroups", x => x.Id);
                    table.CheckConstraint("CK_IntakeSubmissionGroups_ExpectedMemberCount", "[ExpectedMemberCount] >= 1");
                    table.ForeignKey(
                        name: "FK_IntakeSubmissionGroups_IntakeReceipts_ParentReceiptId",
                        column: x => x.ParentReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntakeEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StagedReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessedReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    EvaluatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeEvaluations_IntakeStagedReceipts_StagedReceiptId",
                        column: x => x.StagedReceiptId,
                        principalTable: "IntakeStagedReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntakeWorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StagedReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProcessedReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeWorkItems", x => x.Id);
                    table.CheckConstraint("CK_IntakeWorkItems_AttemptCount", "[AttemptCount] >= 0");
                    table.ForeignKey(
                        name: "FK_IntakeWorkItems_IntakeStagedReceipts_StagedReceiptId",
                        column: x => x.StagedReceiptId,
                        principalTable: "IntakeStagedReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictAuthorizations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ApplicationId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Scopes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictAuthorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenIddictAuthorizations_OpenIddictApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CaseSequences",
                columns: table => new
                {
                    SequenceLineageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    LastAllocatedSequence = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseSequences", x => new { x.SequenceLineageId, x.Year });
                    table.CheckConstraint("CK_CaseSequences_LastAllocatedSequence", "[LastAllocatedSequence] >= 0");
                    table.CheckConstraint("CK_CaseSequences_Year", "[Year] >= 2000 AND [Year] <= 9999");
                    table.ForeignKey(
                        name: "FK_CaseSequences_PrincipalSequenceLineages_SequenceLineageId",
                        column: x => x.SequenceLineageId,
                        principalTable: "PrincipalSequenceLineages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Principals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SequenceLineageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PredecessorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SuccessorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    InspectionMode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "physical_address"),
                    DefaultInspectionLocationLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DefaultInspectionAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DefaultInspectionPostcode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DefaultInspectionSourceKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DefaultInspectionSourceRecordId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DefaultInspectionSourceVersion = table.Column<long>(type: "bigint", nullable: true),
                    SalvageMatrixJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 180.00m),
                    ReportSendingRulesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{\"sendTo\":[],\"sendToOnly\":false,\"replyAll\":true,\"cc\":[],\"neverCc\":[],\"attach\":{\"feeNoteSeparate\":true,\"estimate\":false,\"audatex\":false,\"reportImages\":true,\"vehicleImagesDocument\":false,\"figureBreakdown\":false},\"garageFigures\":false,\"reminders\":[],\"rules\":[]}"),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Principals", x => x.Id);
                    table.CheckConstraint("CK_Principals_Code", "[Code] <> ''");
                    table.CheckConstraint("CK_Principals_DefaultFee", "[DefaultFee] > 0");
                    table.CheckConstraint("CK_Principals_InspectionMode", "[InspectionMode] IN ('physical_address', 'image_based_assessment')");
                    table.CheckConstraint("CK_Principals_Version", "[Version] >= 0");
                    table.ForeignKey(
                        name: "FK_Principals_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Principals_PrincipalSequenceLineages_SequenceLineageId",
                        column: x => x.SequenceLineageId,
                        principalTable: "PrincipalSequenceLineages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Principals_Principals_PredecessorId",
                        column: x => x.PredecessorId,
                        principalTable: "Principals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Principals_Principals_SuccessorId",
                        column: x => x.SuccessorId,
                        principalTable: "Principals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseNoteAcknowledgements",
                columns: table => new
                {
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReleaseNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseNoteAcknowledgements", x => new { x.StaffId, x.ReleaseNoteId });
                    table.ForeignKey(
                        name: "FK_ReleaseNoteAcknowledgements_ReleaseNotes_ReleaseNoteId",
                        column: x => x.ReleaseNoteId,
                        principalTable: "ReleaseNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateTable(
                name: "ApprovedInboxPoisonMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedMailboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurrenceKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ImmutableMessageId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceLength = table.Column<long>(type: "bigint", nullable: true),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OriginalSourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    EvidenceMarker = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    StorageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CursorAfterMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuarantinedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovedInboxPoisonMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovedInboxPoisonMessages_ApprovedInboxPollStates_ApprovedMailboxId",
                        column: x => x.ApprovedMailboxId,
                        principalTable: "ApprovedInboxPollStates",
                        principalColumn: "ApprovedMailboxId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RetainedMailboxAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetainedMailboxMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentLength = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetainedMailboxAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetainedMailboxAttachments_RetainedMailboxMessages_RetainedMailboxMessageId",
                        column: x => x.RetainedMailboxMessageId,
                        principalTable: "RetainedMailboxMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImageVrmSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntakeAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ContentHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    EngineKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EngineVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ModelHashes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SuggestedRegistration = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Confidence = table.Column<double>(type: "float", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Disposition = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DispositionActor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DispositionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DispositionOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DisposedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageVrmSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageVrmSuggestions_IntakeAssets_IntakeAssetId",
                        column: x => x.IntakeAssetId,
                        principalTable: "IntakeAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImageVrmSuggestions_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntakeAssetPreparations",
                columns: table => new
                {
                    IntakeAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RotationDegrees = table.Column<short>(type: "smallint", nullable: false),
                    CropLeft = table.Column<decimal>(type: "decimal(8,7)", precision: 8, scale: 7, nullable: true),
                    CropTop = table.Column<decimal>(type: "decimal(8,7)", precision: 8, scale: 7, nullable: true),
                    CropWidth = table.Column<decimal>(type: "decimal(8,7)", precision: 8, scale: 7, nullable: true),
                    CropHeight = table.Column<decimal>(type: "decimal(8,7)", precision: 8, scale: 7, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    PreparedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PreparedByKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PreparedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeAssetPreparations", x => x.IntakeAssetId);
                    table.CheckConstraint("CK_IntakeAssetPreparations_Crop", "([CropLeft] IS NULL AND [CropTop] IS NULL AND [CropWidth] IS NULL AND [CropHeight] IS NULL) OR ([CropLeft] BETWEEN 0 AND 1 AND [CropTop] BETWEEN 0 AND 1 AND [CropWidth] > 0 AND [CropWidth] <= 1 AND [CropHeight] > 0 AND [CropHeight] <= 1 AND [CropLeft] + [CropWidth] <= 1 AND [CropTop] + [CropHeight] <= 1)");
                    table.CheckConstraint("CK_IntakeAssetPreparations_Rotation", "[RotationDegrees] IN (0, 90, 180, 270)");
                    table.ForeignKey(
                        name: "FK_IntakeAssetPreparations_IntakeAssets_IntakeAssetId",
                        column: x => x.IntakeAssetId,
                        principalTable: "IntakeAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntakeAssetTags",
                columns: table => new
                {
                    IntakeAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppliedByKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AppliedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeAssetTags", x => new { x.IntakeAssetId, x.TagId });
                    table.ForeignKey(
                        name: "FK_IntakeAssetTags_ImageTags_TagId",
                        column: x => x.TagId,
                        principalTable: "ImageTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeAssetTags_IntakeAssets_IntakeAssetId",
                        column: x => x.IntakeAssetId,
                        principalTable: "IntakeAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntakeOcrOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntakeAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    QualifiedPagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProviderOperationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetryAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeOcrOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeOcrOperations_IntakeAssets_IntakeAssetId",
                        column: x => x.IntakeAssetId,
                        principalTable: "IntakeAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RetainedInstructionAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntakeAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpectedReceiptVersion = table.Column<long>(type: "bigint", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetainedInstructionAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetainedInstructionAnalyses_IntakeAssets_IntakeAssetId",
                        column: x => x.IntakeAssetId,
                        principalTable: "IntakeAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RetainedInstructionAnalyses_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StandaloneAuditEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalReportAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Assessment = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ConfirmedByKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ConfirmedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ConfirmedByRolesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResultingReceiptVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandaloneAuditEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StandaloneAuditEvidence_IntakeAssets_OriginalReportAssetId",
                        column: x => x.OriginalReportAssetId,
                        principalTable: "IntakeAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StandaloneAuditEvidence_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntakeMailClassificationHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CorrectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeMailClassificationHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeMailClassificationHistory_IntakeMailClassificationDecisions_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeMailClassificationDecisions",
                        principalColumn: "IntakeReceiptId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntakeSubmissionGroupHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorRolesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    BeforeVersion = table.Column<long>(type: "bigint", nullable: false),
                    AfterVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeSubmissionGroupHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeSubmissionGroupHistory_IntakeSubmissionGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "IntakeSubmissionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntakeSubmissionGroupMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    StagedReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeSubmissionGroupMembers", x => x.Id);
                    table.CheckConstraint("CK_IntakeSubmissionGroupMembers_Ordinal", "[Ordinal] >= 0");
                    table.ForeignKey(
                        name: "FK_IntakeSubmissionGroupMembers_IntakeStagedReceipts_StagedReceiptId",
                        column: x => x.StagedReceiptId,
                        principalTable: "IntakeStagedReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeSubmissionGroupMembers_IntakeSubmissionGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "IntakeSubmissionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictTokens",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ApplicationId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    AuthorizationId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RedemptionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReferenceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenIddictTokens_OpenIddictApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId",
                        column: x => x.AuthorizationId,
                        principalTable: "OpenIddictAuthorizations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContactPrincipalLinks",
                columns: table => new
                {
                    PrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactPrincipalLinks", x => new { x.PrincipalId, x.OrganizationId });
                    table.ForeignKey(
                        name: "FK_ContactPrincipalLinks_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContactPrincipalLinks_Principals_PrincipalId",
                        column: x => x.PrincipalId,
                        principalTable: "Principals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImageIntakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceChannel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExternalReceiptToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    EvaluationRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NormalizedVehicleRegistration = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ImageIntakeReference = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedByActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreationOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    LifecycleState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LifecycleVersion = table.Column<long>(type: "bigint", nullable: false),
                    MergedIntoCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MergedIntoCaseReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClosureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CustodyState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CustodyRootRemoteId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustodyConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CustodyMergedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageIntakes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageIntakes_IntakeReceipts_OriginReceiptId",
                        column: x => x.OriginReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImageIntakes_IntakeSubmissionGroups_SubmissionGroupId",
                        column: x => x.SubmissionGroupId,
                        principalTable: "IntakeSubmissionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImageIntakes_Principals_PrincipalId",
                        column: x => x.PrincipalId,
                        principalTable: "Principals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionLocationPrincipalLinks",
                columns: table => new
                {
                    PrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionLocationPrincipalLinks", x => new { x.PrincipalId, x.InspectionLocationId });
                    table.ForeignKey(
                        name: "FK_InspectionLocationPrincipalLinks_InspectionLocations_InspectionLocationId",
                        column: x => x.InspectionLocationId,
                        principalTable: "InspectionLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionLocationPrincipalLinks_Principals_PrincipalId",
                        column: x => x.PrincipalId,
                        principalTable: "Principals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrincipalApiCredentials",
                columns: table => new
                {
                    PrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KeyId = table.Column<string>(type: "nchar(16)", fixedLength: true, maxLength: 16, nullable: false),
                    SecretHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RotatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PausedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrincipalApiCredentials", x => x.PrincipalId);
                    table.CheckConstraint("CK_PrincipalApiCredentials_State", "[State] IN ('Active', 'Paused', 'Revoked')");
                    table.CheckConstraint("CK_PrincipalApiCredentials_Version", "[Version] >= 1");
                    table.ForeignKey(
                        name: "FK_PrincipalApiCredentials_Principals_PrincipalId",
                        column: x => x.PrincipalId,
                        principalTable: "Principals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrincipalSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeclaredInstructionJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrincipalSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrincipalSubmissions_Principals_PrincipalId",
                        column: x => x.PrincipalId,
                        principalTable: "Principals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceLineageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AuditReference = table.Column<string>(type: "nvarchar(43)", maxLength: 43, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    InitialState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CustodyState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OriginIntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StandaloneAuditAssessment = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    StandaloneAuditEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcceptedInspectionDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    InstructionComplete = table.Column<bool>(type: "bit", nullable: false),
                    ImagesComplete = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustodyRootRemoteId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustodySourceRemoteId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustodySourceContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CustodySourceETag = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustodyConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AuditCustodyRemoteId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AuditCustodyConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cases", x => x.Id);
                    table.CheckConstraint("CK_Cases_AuditReference", "[AuditReference] IS NULL OR ([Type] = N'inspection_and_audit' AND [AuditReference] = N'a.' + [Reference])");
                    table.CheckConstraint("CK_Cases_InitialState", "([Type] = N'triage' AND [InitialState] IS NULL) OR ([Type] <> N'triage' AND [InitialState] IS NOT NULL)");
                    table.CheckConstraint("CK_Cases_Sequence", "[Sequence] >= 1");
                    table.CheckConstraint("CK_Cases_Version", "[Version] >= 0");
                    table.ForeignKey(
                        name: "FK_Cases_IntakeReceipts_OriginIntakeReceiptId",
                        column: x => x.OriginIntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cases_Principals_PrincipalId",
                        column: x => x.PrincipalId,
                        principalTable: "Principals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cases_StandaloneAuditEvidence_StandaloneAuditEvidenceId",
                        column: x => x.StandaloneAuditEvidenceId,
                        principalTable: "StandaloneAuditEvidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImageIntakeLifecycleEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImageIntakeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorRolesJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    BeforeVersion = table.Column<long>(type: "bigint", nullable: false),
                    AfterVersion = table.Column<long>(type: "bigint", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CaseReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageIntakeLifecycleEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageIntakeLifecycleEvents_ImageIntakes_ImageIntakeId",
                        column: x => x.ImageIntakeId,
                        principalTable: "ImageIntakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    SourceOccurrenceIdentity = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CustodyFolder = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "case")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseDocuments", x => x.Id);
                    table.CheckConstraint("CK_CaseDocuments_CustodyFolder", "[CustodyFolder] IN (N'case', N'audit')");
                    table.ForeignKey(
                        name: "FK_CaseDocuments_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BeforeVersion = table.Column<long>(type: "bigint", nullable: true),
                    AfterVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseHistory_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseMatchIndex",
                columns: table => new
                {
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrincipalCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DurableClaimToken = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NormalizedVrm = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NormalizedSurname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NormalizedFirstInitial = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    IncidentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MatchPolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MatchPolicyVersion = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseMatchIndex", x => x.CaseId);
                    table.ForeignKey(
                        name: "FK_CaseMatchIndex_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CaseReportApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArtifactIdentity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ArtifactSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ApprovedByKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ApprovedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ApprovedByRolesJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseReportApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseReportApprovals_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseReportSentEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MailboxIdentity = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    SentFolderIdentity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ImmutableItemIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    InternetMessageIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ConversationIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReplyChainIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceOccurrenceIdentity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    MimeSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DiscoveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DiscoveredByKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DiscoveredBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RetentionOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RetentionRequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    LinkedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LinkedByKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    LinkedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LinkedByRolesJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseReportSentEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseReportSentEvidence_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalWorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ImageIntakeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExternalReceipt = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CaseRootCreationToken = table.Column<string>(type: "nchar(26)", fixedLength: true, maxLength: 26, nullable: true),
                    AuditFolderCreationToken = table.Column<string>(type: "nchar(26)", fixedLength: true, maxLength: 26, nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalWorkItems", x => x.Id);
                    table.CheckConstraint("CK_ExternalWorkItems_AttemptCount", "[AttemptCount] >= 0");
                    table.ForeignKey(
                        name: "FK_ExternalWorkItems_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalWorkItems_Cases_SourceCaseId",
                        column: x => x.SourceCaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalWorkItems_ImageIntakes_ImageIntakeId",
                        column: x => x.ImageIntakeId,
                        principalTable: "ImageIntakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GlassRepairEstimateSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CredentialGeneration = table.Column<long>(type: "bigint", nullable: false),
                    NormalizedAccountKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActiveAccountKey = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    OperationKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProviderVehicleId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EreId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CallbackDigest = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CallbackConsumedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProtectedSession = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultArtifactsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlassRepairEstimateSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlassRepairEstimateSessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GlassRepairEstimateSessions_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntakeManualAssociations",
                columns: table => new
                {
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LinkedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UnlinkedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorRolesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MatchPolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MatchPolicyVersion = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeManualAssociations", x => x.IntakeReceiptId);
                    table.CheckConstraint("CK_IntakeManualAssociations_Version", "[Version] >= 0");
                    table.ForeignKey(
                        name: "FK_IntakeManualAssociations_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeManualAssociations_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntakeMutationHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorRolesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpectedIntakeVersion = table.Column<long>(type: "bigint", nullable: false),
                    BeforeIntakeVersion = table.Column<long>(type: "bigint", nullable: false),
                    AfterIntakeVersion = table.Column<long>(type: "bigint", nullable: false),
                    ExpectedCaseVersion = table.Column<long>(type: "bigint", nullable: true),
                    BeforeCaseVersion = table.Column<long>(type: "bigint", nullable: true),
                    AfterCaseVersion = table.Column<long>(type: "bigint", nullable: true),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeMutationHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeMutationHistory_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeMutationHistory_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Registration = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Cause = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Route = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RaisedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffNotifications", x => x.Id);
                    table.CheckConstraint("CK_StaffNotifications_Cause", "[Cause] IN ('AiDraftReady', 'CaseAssigned', 'EditedByOther', 'EmailReceived', 'QueryReceived', 'CancellationReceived', 'QueryAssigned')");
                    table.ForeignKey(
                        name: "FK_StaffNotifications_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Triage",
                columns: table => new
                {
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceChannel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ExternalReceiptToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    EvaluationRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NormalizedVehicleRegistration = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AssigneeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinkedInstructionCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreationOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Triage", x => x.CaseId);
                    table.CheckConstraint("CK_Triage_Origin", "([OriginReceiptId] IS NULL AND [SourceChannel] IS NULL AND [ExternalReceiptToken] IS NULL AND [SourceHash] IS NULL AND [EvaluationRevisionId] IS NULL) OR ([OriginReceiptId] IS NOT NULL AND [SourceChannel] IS NOT NULL AND [ExternalReceiptToken] IS NOT NULL AND [SourceHash] IS NOT NULL AND [EvaluationRevisionId] IS NOT NULL)");
                    table.CheckConstraint("CK_Triage_Version", "[Version] >= 0");
                    table.ForeignKey(
                        name: "FK_Triage_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Triage_Cases_LinkedInstructionCaseId",
                        column: x => x.LinkedInstructionCaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Triage_IntakeReceipts_OriginReceiptId",
                        column: x => x.OriginReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ContentLength = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    BoxFileId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BoxVersionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PendingContentStorageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustodyStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    IsLogicallyRemoved = table.Column<bool>(type: "bit", nullable: false),
                    RemovalReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RemovalOperationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsRecognisedEstimate = table.Column<bool>(type: "bit", nullable: true),
                    RecognisedEstimateProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVersions", x => x.Id);
                    table.CheckConstraint("CK_DocumentVersions_ContentLength", "[ContentLength] >= 0");
                    table.CheckConstraint("CK_DocumentVersions_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_DocumentVersions_CaseDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "CaseDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseWorkflows",
                columns: table => new
                {
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PreHoldState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    HeldAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    HoldReviewOn = table.Column<DateOnly>(type: "date", nullable: true),
                    StateEnteredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AssignedEngineerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SignOffEngineerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportApprovalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportSentEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosureOutcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    HasOpenQuery = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ReplacementCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ArchivedByKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ArchivedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ArchivedByRolesJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ArchiveReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    EditLeaseGeneration = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    EditLeaseToken = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    EditLeaseTokenHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    EditLeaseRequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    EditLeaseHolder = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EditLeaseHolderKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    EditLeaseOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EditLeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseWorkflows", x => x.CaseId);
                    table.CheckConstraint("CK_CaseWorkflows_ArchiveMetadata", "([ArchivedAtUtc] IS NULL AND [ArchivedByKind] IS NULL AND [ArchivedBySubjectId] IS NULL AND [ArchivedByRolesJson] IS NULL AND [ArchiveReason] IS NULL) OR ([ArchivedAtUtc] IS NOT NULL AND [ArchivedByKind] IS NOT NULL AND [ArchivedBySubjectId] IS NOT NULL AND [ArchivedByRolesJson] IS NOT NULL AND [ArchiveReason] IS NOT NULL AND [ArchiveReason] <> '')");
                    table.CheckConstraint("CK_CaseWorkflows_EditLeaseGeneration", "[EditLeaseGeneration] >= 0");
                    table.CheckConstraint("CK_CaseWorkflows_HoldReview", "[HoldReviewOn] IS NULL OR [HeldAtUtc] IS NOT NULL");
                    table.CheckConstraint("CK_CaseWorkflows_OriginalNotSelf", "[OriginalCaseId] IS NULL OR [OriginalCaseId] <> [CaseId]");
                    table.CheckConstraint("CK_CaseWorkflows_ReplacementNotSelf", "[ReplacementCaseId] IS NULL OR [ReplacementCaseId] <> [CaseId]");
                    table.CheckConstraint("CK_CaseWorkflows_Version", "[Version] >= 0");
                    table.ForeignKey(
                        name: "FK_CaseWorkflows_CaseReportApprovals_ReportApprovalId",
                        column: x => x.ReportApprovalId,
                        principalTable: "CaseReportApprovals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseWorkflows_CaseReportSentEvidence_ReportSentEvidenceId",
                        column: x => x.ReportSentEvidenceId,
                        principalTable: "CaseReportSentEvidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseWorkflows_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseWorkflows_Cases_OriginalCaseId",
                        column: x => x.OriginalCaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseWorkflows_Cases_ReplacementCaseId",
                        column: x => x.ReplacementCaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseWorks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReportApprovalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportSentEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseWorks", x => x.Id);
                    table.CheckConstraint("CK_CaseWorks_Kind", "[Kind] IN (N'primary', N'audit')");
                    table.CheckConstraint("CK_CaseWorks_PrimaryId", "[Kind] <> N'primary' OR [Id] = [CaseId]");
                    table.ForeignKey(
                        name: "FK_CaseWorks_CaseReportApprovals_ReportApprovalId",
                        column: x => x.ReportApprovalId,
                        principalTable: "CaseReportApprovals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseWorks_CaseReportSentEvidence_ReportSentEvidenceId",
                        column: x => x.ReportSentEvidenceId,
                        principalTable: "CaseReportSentEvidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseWorks_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseIntakeLinks",
                columns: table => new
                {
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustodyWorkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorRolesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExpectedIntakeVersion = table.Column<long>(type: "bigint", nullable: true),
                    AcceptanceCommandMaterialJson = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    AcceptanceCommandFingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseIntakeLinks", x => x.IntakeReceiptId);
                    table.ForeignKey(
                        name: "FK_CaseIntakeLinks_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseIntakeLinks_ExternalWorkItems_CustodyWorkId",
                        column: x => x.CustodyWorkId,
                        principalTable: "ExternalWorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseIntakeLinks_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VehicleLookupRequests",
                columns: table => new
                {
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Registration = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    RequestedByKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RequestedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestedByRolesJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResultingCaseVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleLookupRequests", x => x.WorkItemId);
                    table.CheckConstraint("CK_VehicleLookupRequests_ResultingCaseVersion", "[ResultingCaseVersion] >= 0");
                    table.ForeignKey(
                        name: "FK_VehicleLookupRequests_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleLookupRequests_ExternalWorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "ExternalWorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SentEmailEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TriageCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageIdentity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RecipientsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MimeSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChaseDueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SentEmailEvidence", x => x.Id);
                    table.CheckConstraint("CK_SentEmailEvidence_Version", "[Version] >= 0");
                    table.ForeignKey(
                        name: "FK_SentEmailEvidence_Triage_TriageCaseId",
                        column: x => x.TriageCaseId,
                        principalTable: "Triage",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TriageFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TriageCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Roadworthiness = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Assessment = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SupersedesFindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriageFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriageFindings_TriageFindings_SupersedesFindingId",
                        column: x => x.SupersedesFindingId,
                        principalTable: "TriageFindings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TriageFindings_Triage_TriageCaseId",
                        column: x => x.TriageCaseId,
                        principalTable: "Triage",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TriageHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TriageCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    BeforeVersion = table.Column<long>(type: "bigint", nullable: false),
                    AfterVersion = table.Column<long>(type: "bigint", nullable: false),
                    AfterState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AfterAssigneeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AfterLinkedInstructionCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriageHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriageHistory_Triage_TriageCaseId",
                        column: x => x.TriageCaseId,
                        principalTable: "Triage",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentContentCacheEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IntakeAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Variant = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BlobIdentity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ETag = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VerifiedSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    VerifiedSize = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadLeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastCleanupOutcome = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentContentCacheEntries", x => x.Id);
                    table.CheckConstraint("CK_DocumentContentCacheEntries_Source", "([DocumentVersionId] IS NULL AND [IntakeAssetId] IS NOT NULL) OR ([DocumentVersionId] IS NOT NULL AND [IntakeAssetId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_DocumentContentCacheEntries_DocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentContentCacheEntries_IntakeAssets_IntakeAssetId",
                        column: x => x.IntakeAssetId,
                        principalTable: "IntakeAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentOccurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    SemanticRole = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceOccurrenceIdentity = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    InReport = table.Column<bool>(type: "bit", nullable: false),
                    SupportingOrder = table.Column<int>(type: "int", nullable: true),
                    RotationDegrees = table.Column<short>(type: "smallint", nullable: false),
                    CropLeft = table.Column<decimal>(type: "decimal(8,7)", precision: 8, scale: 7, nullable: true),
                    CropTop = table.Column<decimal>(type: "decimal(8,7)", precision: 8, scale: 7, nullable: true),
                    CropWidth = table.Column<decimal>(type: "decimal(8,7)", precision: 8, scale: 7, nullable: true),
                    CropHeight = table.Column<decimal>(type: "decimal(8,7)", precision: 8, scale: 7, nullable: true),
                    PreparationVersion = table.Column<long>(type: "bigint", nullable: false),
                    PreparedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PreparedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PreparationFullPage = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentOccurrences", x => x.Id);
                    table.CheckConstraint("CK_DocumentOccurrences_Crop", "([CropLeft] IS NULL AND [CropTop] IS NULL AND [CropWidth] IS NULL AND [CropHeight] IS NULL) OR ([CropLeft] BETWEEN 0 AND 1 AND [CropTop] BETWEEN 0 AND 1 AND [CropWidth] > 0 AND [CropWidth] <= 1 AND [CropHeight] > 0 AND [CropHeight] <= 1 AND [CropLeft] + [CropWidth] <= 1 AND [CropTop] + [CropHeight] <= 1)");
                    table.CheckConstraint("CK_DocumentOccurrences_Rotation", "[RotationDegrees] IN (0, 90, 180, 270)");
                    table.ForeignKey(
                        name: "FK_DocumentOccurrences_CaseDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "CaseDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentOccurrences_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentOccurrences_DocumentVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntakeSourceCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IntakeAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Occurrence = table.Column<int>(type: "int", nullable: false),
                    DocumentRole = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PartyRole = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferenceRole = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RawValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NormalizedValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocatorJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReaderKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReaderVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PolicyKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Disposition = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeSourceCandidates", x => x.Id);
                    table.CheckConstraint("CK_IntakeSourceCandidates_Source", "([DocumentVersionId] IS NOT NULL AND [IntakeAssetId] IS NULL) OR ([DocumentVersionId] IS NULL AND [IntakeAssetId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_IntakeSourceCandidates_DocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeSourceCandidates_IntakeAssets_IntakeAssetId",
                        column: x => x.IntakeAssetId,
                        principalTable: "IntakeAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeSourceCandidates_RetainedInstructionAnalyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "RetainedInstructionAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseDueWork",
                columns: table => new
                {
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MissingMaterialReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DueBy = table.Column<DateOnly>(type: "date", nullable: true),
                    DueBySetByStaff = table.Column<bool>(type: "bit", nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NextChaseAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextChaseAtUtcTicks = table.Column<long>(type: "bigint", nullable: true),
                    HeldAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RemainingChaseIntervalTicks = table.Column<long>(type: "bigint", nullable: true),
                    MostRecentChannel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MostRecentOutcome = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MostRecentNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseDueWork", x => x.CaseId);
                    table.CheckConstraint("CK_CaseDueWork_NextChaseOrdering", "([NextChaseAtUtc] IS NULL AND [NextChaseAtUtcTicks] IS NULL) OR ([NextChaseAtUtc] IS NOT NULL AND [NextChaseAtUtcTicks] IS NOT NULL)");
                    table.CheckConstraint("CK_CaseDueWork_Version", "[Version] >= 0");
                    table.ForeignKey(
                        name: "FK_CaseDueWork_CaseWorkflows_CaseId",
                        column: x => x.CaseId,
                        principalTable: "CaseWorkflows",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseEditLeaseOperations",
                columns: table => new
                {
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OperationKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorRolesJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResultVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultTokenHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    ResultExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseEditLeaseOperations", x => new { x.CaseId, x.OperationKey });
                    table.CheckConstraint("CK_CaseEditLeaseOperations_ResultVersion", "[ResultVersion] >= 0");
                    table.ForeignKey(
                        name: "FK_CaseEditLeaseOperations_CaseWorkflows_CaseId",
                        column: x => x.CaseId,
                        principalTable: "CaseWorkflows",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseQueries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RaisedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RaisedByKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RaisedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RaisedOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClosedAs = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedByKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ClosedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResolvedByMailOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssigneeStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseQueries", x => x.Id);
                    table.CheckConstraint("CK_CaseQueries_Channel", "[Channel] IS NULL OR [Channel] IN ('Email', 'Phone', 'WhatsApp', 'Other')");
                    table.CheckConstraint("CK_CaseQueries_Closure", "([ClosedAs] IS NULL AND [ClosedAtUtc] IS NULL AND [ClosedByKind] IS NULL AND [ClosedBySubjectId] IS NULL AND [ResolvedByMailOperationId] IS NULL) OR ([ClosedAs] IN ('Resolved', 'Withdrawn') AND [ClosedAtUtc] IS NOT NULL AND [ClosedByKind] IS NOT NULL AND [ClosedBySubjectId] IS NOT NULL)");
                    table.CheckConstraint("CK_CaseQueries_Source", "[Source] IN ('Mail', 'Staff')");
                    table.CheckConstraint("CK_CaseQueries_SourceShape", "([Source] = 'Mail' AND [IntakeReceiptId] IS NOT NULL AND [Channel] IS NULL AND [Details] IS NULL) OR ([Source] = 'Staff' AND [IntakeReceiptId] IS NULL AND [Channel] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CaseQueries_CaseWorkflows_CaseId",
                        column: x => x.CaseId,
                        principalTable: "CaseWorkflows",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseQueries_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssigneeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseTasks", x => x.Id);
                    table.CheckConstraint("CK_CaseTasks_Description", "[Description] <> ''");
                    table.CheckConstraint("CK_CaseTasks_State", "[State] IN ('Open', 'Completed', 'Cancelled')");
                    table.CheckConstraint("CK_CaseTasks_Version", "[Version] >= 0");
                    table.ForeignKey(
                        name: "FK_CaseTasks_AspNetUsers_AssigneeId",
                        column: x => x.AssigneeId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseTasks_CaseWorkflows_CaseId",
                        column: x => x.CaseId,
                        principalTable: "CaseWorkflows",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseWorkflowEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorRolesJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    BeforeVersion = table.Column<long>(type: "bigint", nullable: false),
                    AfterVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseWorkflowEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseWorkflowEvents_CaseWorkflows_CaseId",
                        column: x => x.CaseId,
                        principalTable: "CaseWorkflows",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppliedValuationSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CalculationPolicyVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedByKind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedBySubjectId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    AcceptedEngineerValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AcceptedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppliedValuationSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppliedValuationSnapshots_CaseWorks_WorkId",
                        column: x => x.WorkId,
                        principalTable: "CaseWorks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseAssessmentFields",
                columns: table => new
                {
                    WorkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldPath = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RecordedByKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseAssessmentFields", x => new { x.WorkId, x.FieldPath });
                    table.CheckConstraint("CK_CaseAssessmentFields_FieldPath", "[FieldPath] <> '' AND LEN([FieldPath]) <= 60");
                    table.CheckConstraint("CK_CaseAssessmentFields_RecordedByKind", "[RecordedByKind] IN ('Staff', 'Automation')");
                    table.ForeignKey(
                        name: "FK_CaseAssessmentFields_CaseWorks_WorkId",
                        column: x => x.WorkId,
                        principalTable: "CaseWorks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseDataSnapshots",
                columns: table => new
                {
                    WorkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginIntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginSourceChannel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    OriginExternalReceiptToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OriginSourceHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    OriginReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SourceReaderKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceReaderVersion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExtractionPolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExtractionPolicyVersion = table.Column<int>(type: "int", nullable: true),
                    CompletenessPolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CompletenessPolicyVersion = table.Column<int>(type: "int", nullable: false),
                    CompletenessPolicySatisfied = table.Column<bool>(type: "bit", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClaimSourceOverrideContactName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ClaimSourceOverrideContactTelephone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClaimSourceOverrideContactEmailAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseDataSnapshots", x => x.WorkId);
                    table.CheckConstraint("CK_CaseDataSnapshots_CompletenessPolicyVersion", "[CompletenessPolicyVersion] > 0");
                    table.CheckConstraint("CK_CaseDataSnapshots_ExtractionPolicyVersion", "[ExtractionPolicyVersion] IS NULL OR [ExtractionPolicyVersion] > 0");
                    table.ForeignKey(
                        name: "FK_CaseDataSnapshots_CaseWorks_WorkId",
                        column: x => x.WorkId,
                        principalTable: "CaseWorks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseDataSnapshots_IntakeReceipts_OriginIntakeReceiptId",
                        column: x => x.OriginIntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseRepairSpecifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceRoute = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SourceArtifactReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SourceVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreationOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LabourRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RegionalUplift = table.Column<bool>(type: "bit", nullable: false),
                    OtherCosts = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RateCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RateCardVersion = table.Column<long>(type: "bigint", nullable: true),
                    PartsDiscountPercent = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    MaterialsDiscountPercent = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    SpecialistDiscountPercent = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    OverallDiscountPercent = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    LabourVatApplicable = table.Column<bool>(type: "bit", nullable: true),
                    PartsVatApplicable = table.Column<bool>(type: "bit", nullable: true),
                    MaterialsVatApplicable = table.Column<bool>(type: "bit", nullable: true),
                    SpecialistVatApplicable = table.Column<bool>(type: "bit", nullable: true),
                    RepairerVatStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VatPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    AiJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SupplementaryOfSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplementaryReason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SupplementaryExplainOnReport = table.Column<bool>(type: "bit", nullable: false),
                    SupplementaryStatement = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    GlassVehicleId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GlassEstimateId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GlassNatCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GlassPlaceholder = table.Column<bool>(type: "bit", nullable: true),
                    GlassRegistration = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GlassMileageMiles = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseRepairSpecifications", x => x.Id);
                    table.CheckConstraint("CK_CaseRepairSpecifications_Current", "[IsCurrent] = 0 OR [State] = 'Draft'");
                    table.CheckConstraint("CK_CaseRepairSpecifications_Glass", "([GlassVehicleId] IS NULL AND [GlassEstimateId] IS NULL AND [GlassNatCode] IS NULL AND [GlassPlaceholder] IS NULL AND [GlassRegistration] IS NULL AND [GlassMileageMiles] IS NULL) OR ([GlassVehicleId] IS NOT NULL AND [GlassEstimateId] IS NOT NULL AND [GlassNatCode] IS NOT NULL AND [GlassPlaceholder] IS NOT NULL AND [GlassRegistration] IS NOT NULL AND [GlassMileageMiles] IS NOT NULL)");
                    table.CheckConstraint("CK_CaseRepairSpecifications_SourceRoute", "[SourceRoute] IN ('Manual', 'Glasses', 'AudatexPdf', 'Json', 'AiDraft')");
                    table.CheckConstraint("CK_CaseRepairSpecifications_State", "[State] IN ('Draft')");
                    table.CheckConstraint("CK_CaseRepairSpecifications_VatPercent", "[VatPercent] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_CaseRepairSpecifications_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_CaseRepairSpecifications_CaseWorks_WorkId",
                        column: x => x.WorkId,
                        principalTable: "CaseWorks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseRepairSpecificationSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LinesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupplementaryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Gross = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SentOnReport = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseRepairSpecificationSnapshots", x => x.Id);
                    table.CheckConstraint("CK_CaseRepairSpecificationSnapshots_Kind", "[Kind] IN ('Imported', 'BeforeScaling', 'Scaled', 'ScalingRemoved', 'BeforeRestore', 'Restored', 'Sent', 'BeforeImport')");
                    table.CheckConstraint("CK_CaseRepairSpecificationSnapshots_Number", "[Number] > 0");
                    table.ForeignKey(
                        name: "FK_CaseRepairSpecificationSnapshots_CaseWorks_WorkId",
                        column: x => x.WorkId,
                        principalTable: "CaseWorks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseReportGenerations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseVersion = table.Column<long>(type: "bigint", nullable: false),
                    SnapshotHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TemplateVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RendererVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    SupersededById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseReportGenerations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseReportGenerations_CaseWorks_WorkId",
                        column: x => x.WorkId,
                        principalTable: "CaseWorks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseReportGenerations_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseReportWordings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlockKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: true),
                    Included = table.Column<bool>(type: "bit", nullable: false),
                    Manual = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseReportWordings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseReportWordings_CaseWorks_WorkId",
                        column: x => x.WorkId,
                        principalTable: "CaseWorks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseValuations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Time = table.Column<TimeOnly>(type: "time", nullable: false),
                    GuideMonth = table.Column<DateOnly>(type: "date", nullable: true),
                    Mileage = table.Column<long>(type: "bigint", nullable: true),
                    RetailValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TradeValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RecordedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastEditedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastEditedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseValuations", x => x.Id);
                    table.CheckConstraint("CK_CaseValuations_Mileage", "[Mileage] >= 0");
                    table.CheckConstraint("CK_CaseValuations_RetailValue", "[RetailValue] >= 0");
                    table.CheckConstraint("CK_CaseValuations_Source", "[Source] IN ('Glasses', 'Cazana', 'EngineersValue', 'AiMarketResearch', 'Brego', 'SuperCap', 'Cap')");
                    table.CheckConstraint("CK_CaseValuations_TradeValue", "[TradeValue] >= 0");
                    table.ForeignKey(
                        name: "FK_CaseValuations_CaseWorks_WorkId",
                        column: x => x.WorkId,
                        principalTable: "CaseWorks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VehicleLookupObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Registration = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderVersion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResponseIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RetrievedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SourceObservedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Make = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ManufactureYear = table.Column<int>(type: "int", nullable: true),
                    EngineCapacityCc = table.Column<int>(type: "int", nullable: true),
                    FuelType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TypeApproval = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Wheelplan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RevenueWeightKg = table.Column<int>(type: "int", nullable: true),
                    Colour = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TaxDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MotTestsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MileageValue = table.Column<long>(type: "bigint", nullable: true),
                    MileageUnit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    MileageObservedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    MileageMethodKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MileageMethodVersion = table.Column<int>(type: "int", nullable: true),
                    MileageSupportingObservationCount = table.Column<int>(type: "int", nullable: true),
                    MileageEstimateJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureRetryable = table.Column<bool>(type: "bit", nullable: true),
                    FailureRetryAfterTicks = table.Column<long>(type: "bigint", nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleLookupObservations", x => x.Id);
                    table.CheckConstraint("CK_VehicleLookupObservations_AttemptNumber", "[AttemptNumber] >= 1");
                    table.CheckConstraint("CK_VehicleLookupObservations_Mileage", "([MileageValue] IS NULL AND [MileageUnit] IS NULL AND [MileageObservedOn] IS NULL AND [MileageMethodKey] IS NULL AND [MileageMethodVersion] IS NULL AND [MileageSupportingObservationCount] IS NULL) OR ([MileageValue] >= 0 AND [MileageUnit] IS NOT NULL AND [MileageObservedOn] IS NOT NULL AND [MileageMethodKey] IS NOT NULL AND [MileageMethodVersion] > 0 AND [MileageSupportingObservationCount] > 0)");
                    table.ForeignKey(
                        name: "FK_VehicleLookupObservations_VehicleLookupRequests_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "VehicleLookupRequests",
                        principalColumn: "WorkItemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailResponseEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SentEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PollOutcomeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MailboxId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MailboxAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    SentFolderIdentity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ImmutableItemIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MessageIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ConversationIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReplyChainIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    InReplyToIdentitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceOccurrenceIdentity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    MimeSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DiscoveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailResponseEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailResponseEvidence_ApprovedSentPollOutcomes_PollOutcomeId",
                        column: x => x.PollOutcomeId,
                        principalTable: "ApprovedSentPollOutcomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmailResponseEvidence_SentEmailEvidence_SentEvidenceId",
                        column: x => x.SentEvidenceId,
                        principalTable: "SentEmailEvidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TriageResponseEvidenceLinks",
                columns: table => new
                {
                    TriageCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SentEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LinkedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriageResponseEvidenceLinks", x => new { x.TriageCaseId, x.SentEvidenceId });
                    table.ForeignKey(
                        name: "FK_TriageResponseEvidenceLinks_SentEmailEvidence_SentEvidenceId",
                        column: x => x.SentEvidenceId,
                        principalTable: "SentEmailEvidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TriageResponseEvidenceLinks_Triage_TriageCaseId",
                        column: x => x.TriageCaseId,
                        principalTable: "Triage",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentOccurrenceTags",
                columns: table => new
                {
                    OccurrenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppliedByKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AppliedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentOccurrenceTags", x => new { x.OccurrenceId, x.TagId });
                    table.ForeignKey(
                        name: "FK_DocumentOccurrenceTags_DocumentOccurrences_OccurrenceId",
                        column: x => x.OccurrenceId,
                        principalTable: "DocumentOccurrences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentOccurrenceTags_ImageTags_TagId",
                        column: x => x.TagId,
                        principalTable: "ImageTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseDueChasers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NextChaseAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CopyableText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    BeforeDueWorkVersion = table.Column<long>(type: "bigint", nullable: false),
                    AfterDueWorkVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseDueChasers", x => x.Id);
                    table.CheckConstraint("CK_CaseDueChasers_Versions", "[BeforeDueWorkVersion] >= 0 AND [AfterDueWorkVersion] = [BeforeDueWorkVersion] + 1");
                    table.ForeignKey(
                        name: "FK_CaseDueChasers_CaseDueWork_CaseId",
                        column: x => x.CaseId,
                        principalTable: "CaseDueWork",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseManualChases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorRolesJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetPartyOrAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AttemptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResultingVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseManualChases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseManualChases_CaseDueWork_CaseId",
                        column: x => x.CaseId,
                        principalTable: "CaseDueWork",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppliedValuationAdditions",
                columns: table => new
                {
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    PresetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PresetVersion = table.Column<long>(type: "bigint", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SuggestedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppliedValuationAdditions", x => new { x.SnapshotId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_AppliedValuationAdditions_AppliedValuationSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "AppliedValuationSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppliedValuationCalculations",
                columns: table => new
                {
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseVersion = table.Column<long>(type: "bigint", nullable: false),
                    GuideValuationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuideValuationStampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuideRetailValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CommercialVatApplied = table.Column<bool>(type: "bit", nullable: false),
                    CommercialVatAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValueIncludingVat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriorTotalLossPercentage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PriorTotalLossAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdditionsTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ConditionDeduction = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Proposal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppliedValuationCalculations", x => x.SnapshotId);
                    table.ForeignKey(
                        name: "FK_AppliedValuationCalculations_AppliedValuationSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "AppliedValuationSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseDataFields",
                columns: table => new
                {
                    WorkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ValueKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ValueType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    NormalizedRegistration = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, computedColumnSql: "CONVERT(nvarchar(20), CASE WHEN [FieldName] = N'vehicle_registration' AND LEN(REPLACE(REPLACE([Value], N' ', N''), N'-', N'')) <= 20 THEN REPLACE(REPLACE([Value], N' ', N''), N'-', N'') END)", stored: true),
                    SourceKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceLabel = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false),
                    ConfirmedByActor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseDataFields", x => new { x.WorkId, x.FieldName, x.ValueKind });
                    table.CheckConstraint("CK_CaseDataFields_Confirmation", "([ValueKind] = 'confirmed' AND [ConfirmedByActor] IS NOT NULL AND [ConfirmedAtUtc] IS NOT NULL) OR ([ValueKind] <> 'confirmed' AND [ConfirmedByActor] IS NULL AND [ConfirmedAtUtc] IS NULL)");
                    table.CheckConstraint("CK_CaseDataFields_FieldName", "[FieldName] <> '' AND LEN([FieldName]) <= 60");
                    table.CheckConstraint("CK_CaseDataFields_PolicyVersion", "[PolicyVersion] > 0");
                    table.CheckConstraint("CK_CaseDataFields_SourceKind", "[SourceKind] IN ('intake_evidence', 'mail_route', 'case_acceptance', 'staff_correction', 'vehicle_lookup', 'principal_setting', 'principal_api')");
                    table.CheckConstraint("CK_CaseDataFields_ValueKind", "[ValueKind] IN ('fact', 'suggestion', 'confirmed')");
                    table.CheckConstraint("CK_CaseDataFields_ValueType", "[ValueType] IN ('text', 'integer', 'date', 'inspection_mode')");
                    table.ForeignKey(
                        name: "FK_CaseDataFields_CaseDataSnapshots_WorkId",
                        column: x => x.WorkId,
                        principalTable: "CaseDataSnapshots",
                        principalColumn: "WorkId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseEstimateLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    LineType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GuideCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    WorkUnits = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    PaintWorkUnits = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Unpriced = table.Column<bool>(type: "bit", nullable: false),
                    PartNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Betterment = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EvidenceLabel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Justification = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecordedByKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Materials = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OriginalValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceDocumentIdentity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceDocumentSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    SourceRowIdentity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AmendedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AmendedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RepairSpecificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseEstimateLines", x => x.Id);
                    table.CheckConstraint("CK_CaseEstimateLines_EvidenceLabel", "[EvidenceLabel] IS NULL OR [EvidenceLabel] IN ('official', 'reference', 'case', 'judgement')");
                    table.CheckConstraint("CK_CaseEstimateLines_LineType", "[LineType] IN ('rnr', 'repair', 'new_part', 'check_labour', 'paint_new', 'paint_repair', 'paint_blend', 'paint_prep', 'specialist_fixed', 'specialist_wu')");
                    table.CheckConstraint("CK_CaseEstimateLines_Position", "[Position] > 0");
                    table.CheckConstraint("CK_CaseEstimateLines_Quantity", "[Quantity] IS NULL OR [Quantity] > 0");
                    table.CheckConstraint("CK_CaseEstimateLines_Unpriced", "[Unpriced] = 0 OR [Price] IS NULL");
                    table.ForeignKey(
                        name: "FK_CaseEstimateLines_CaseRepairSpecifications_RepairSpecificationId",
                        column: x => x.RepairSpecificationId,
                        principalTable: "CaseRepairSpecifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseEstimateLines_CaseWorks_WorkId",
                        column: x => x.WorkId,
                        principalTable: "CaseWorks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GeneratedCaseArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GenerationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Sha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FailureCode = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedCaseArtifacts", x => x.Id);
                    table.CheckConstraint("CK_GeneratedCaseArtifacts_Custody", "[State] <> 'Confirmed' OR ([VersionId] IS NOT NULL AND [Sha256] IS NOT NULL AND [FailureCode] IS NULL)");
                    table.ForeignKey(
                        name: "FK_GeneratedCaseArtifacts_CaseReportGenerations_GenerationId",
                        column: x => x.GenerationId,
                        principalTable: "CaseReportGenerations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeneratedCaseArtifacts_DocumentVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ApprovedMailboxes",
                columns: new[] { "Id", "ActivatedAtUtc", "Address", "AllowIncoming", "AllowOutgoing", "InboxFolderIdentity", "MailboxGeneration", "MailboxIdentity", "SentFolderIdentity", "State", "Version" },
                values: new object[] { new Guid("49f47eb9-c5b0-464f-b8f0-8c90ba061728"), null, "instructions@collisionengineers.co.uk", true, false, null, 1L, null, null, "Approved", 1 });

            migrationBuilder.InsertData(
                table: "ValuationPresets",
                columns: new[] { "Id", "Active", "ConcurrencyToken", "Label", "RemovedAtUtc", "SuggestedAmount", "UpdatedAtUtc", "UpdatedBy", "Version" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-4000-8000-00000000f001"), true, new Guid("00000000-0000-4000-8000-00000000f101"), "Tow bar", null, 300m, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:v1-foundation", 1L },
                    { new Guid("00000000-0000-4000-8000-00000000f002"), true, new Guid("00000000-0000-4000-8000-00000000f102"), "PCO plated", null, 1500m, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:v1-foundation", 1L },
                    { new Guid("00000000-0000-4000-8000-00000000f003"), true, new Guid("00000000-0000-4000-8000-00000000f103"), "Decals", null, 500m, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:v1-foundation", 1L },
                    { new Guid("00000000-0000-4000-8000-00000000f004"), true, new Guid("00000000-0000-4000-8000-00000000f104"), "Camper conversion", null, 0m, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:v1-foundation", 1L },
                    { new Guid("00000000-0000-4000-8000-00000000f005"), true, new Guid("00000000-0000-4000-8000-00000000f105"), "Driving tuition", null, 500m, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:v1-foundation", 1L }
                });

            migrationBuilder.InsertData(
                table: "WorkflowConfigurations",
                columns: new[] { "Id", "AiDraftTargetDays", "ChaseIntervalDays", "HeldTargetDays", "RequireImages", "RequireInstructions", "ReviewTargetDays", "TriageTargetDays", "Version" },
                values: new object[] { "case-workflow", 1, 7, 7, true, true, 1, 1, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_ActionHistory_AggregateType_AggregateId_OccurredAtUtc",
                table: "ActionHistory",
                columns: new[] { "AggregateType", "AggregateId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionHistory_AggregateType_CorrelationId",
                table: "ActionHistory",
                columns: new[] { "AggregateType", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionHistory_OccurredAtUtc",
                table: "ActionHistory",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AiJobs_CreatedAtUtc",
                table: "AiJobs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AiJobs_OperationKey",
                table: "AiJobs",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiJobs_State_LeaseExpiresAtUtc",
                table: "AiJobs",
                columns: new[] { "State", "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiJobs_SubjectId",
                table: "AiJobs",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppliedValuationSnapshots_WorkId_AcceptedAtUtc",
                table: "AppliedValuationSnapshots",
                columns: new[] { "WorkId", "AcceptedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AppliedValuationSnapshots_WorkId_SnapshotHash",
                table: "AppliedValuationSnapshots",
                columns: new[] { "WorkId", "SnapshotHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedInboxPoisonMessages_ApprovedMailboxId_OccurrenceKey",
                table: "ApprovedInboxPoisonMessages",
                columns: new[] { "ApprovedMailboxId", "OccurrenceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedInboxPoisonMessages_QuarantinedAtUtc_Id",
                table: "ApprovedInboxPoisonMessages",
                columns: new[] { "QuarantinedAtUtc", "Id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedInboxPollStates_DueAtUtc_ApprovedMailboxId",
                table: "ApprovedInboxPollStates",
                columns: new[] { "DueAtUtc", "ApprovedMailboxId" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedInboxPollStates_MailboxAddress",
                table: "ApprovedInboxPollStates",
                column: "MailboxAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedMailboxes_Address",
                table: "ApprovedMailboxes",
                column: "Address",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedMailboxes_MailboxIdentity",
                table: "ApprovedMailboxes",
                column: "MailboxIdentity",
                unique: true,
                filter: "[MailboxIdentity] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedMailboxSubscriptions_ExpiresAtUtc",
                table: "ApprovedMailboxSubscriptions",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedMailboxSubscriptions_SubscriptionId",
                table: "ApprovedMailboxSubscriptions",
                column: "SubscriptionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSentPollOutcomes_InternetMessageIdentity",
                table: "ApprovedSentPollOutcomes",
                column: "InternetMessageIdentity");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSentPollOutcomes_MailboxId_RecordedAtUtc_Id",
                table: "ApprovedSentPollOutcomes",
                columns: new[] { "MailboxId", "RecordedAtUtc", "Id" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSentPollOutcomes_OperationKey",
                table: "ApprovedSentPollOutcomes",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSentPollOutcomes_OutcomeKind_RecordedAtUtc_Id",
                table: "ApprovedSentPollOutcomes",
                columns: new[] { "OutcomeKind", "RecordedAtUtc", "Id" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSentPollOutcomes_RecordedAtUtc_Id",
                table: "ApprovedSentPollOutcomes",
                columns: new[] { "RecordedAtUtc", "Id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSentPollOutcomes_RelatedEvidenceId",
                table: "ApprovedSentPollOutcomes",
                column: "RelatedEvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSentPollStates_DueAtUtc_MailboxId",
                table: "ApprovedSentPollStates",
                columns: new[] { "DueAtUtc", "MailboxId" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSentPollStates_MailboxAddress",
                table: "ApprovedSentPollStates",
                column: "MailboxAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_UserId",
                table: "AspNetUserRoles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_IsDefaultSignOffEngineer",
                table: "AspNetUsers",
                column: "IsDefaultSignOffEngineer",
                unique: true,
                filter: "[IsDefaultSignOffEngineer] = 1");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CaseAssessmentFields_FieldPath",
                table: "CaseAssessmentFields",
                column: "FieldPath");

            migrationBuilder.CreateIndex(
                name: "IX_CaseDataFields_FieldName_ValueKind",
                table: "CaseDataFields",
                columns: new[] { "FieldName", "ValueKind" })
                .Annotation("SqlServer:Include", new[] { "NormalizedRegistration" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseDataSnapshots_OriginIntakeReceiptId",
                table: "CaseDataSnapshots",
                column: "OriginIntakeReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseDocuments_CaseId_Ordinal",
                table: "CaseDocuments",
                columns: new[] { "CaseId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseDocuments_CaseId_SourceOccurrenceIdentity",
                table: "CaseDocuments",
                columns: new[] { "CaseId", "SourceOccurrenceIdentity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseDueChasers_CaseId_GeneratedAtUtc",
                table: "CaseDueChasers",
                columns: new[] { "CaseId", "GeneratedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseDueChasers_CaseId_ScheduledAtUtc",
                table: "CaseDueChasers",
                columns: new[] { "CaseId", "ScheduledAtUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseDueChasers_OperationKey",
                table: "CaseDueChasers",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseDueWork_State_NextChaseAtUtcTicks",
                table: "CaseDueWork",
                columns: new[] { "State", "NextChaseAtUtcTicks" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseEstimateLines_RepairSpecificationId_Position",
                table: "CaseEstimateLines",
                columns: new[] { "RepairSpecificationId", "Position" },
                unique: true,
                filter: "[RepairSpecificationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CaseEstimateLines_WorkId",
                table: "CaseEstimateLines",
                column: "WorkId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseHistory_CaseId_OccurredAtUtc",
                table: "CaseHistory",
                columns: new[] { "CaseId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseHistory_OperationKey",
                table: "CaseHistory",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseIntakeLinks_CaseId",
                table: "CaseIntakeLinks",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseIntakeLinks_CustodyWorkId",
                table: "CaseIntakeLinks",
                column: "CustodyWorkId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseIntakeLinks_OperationKey",
                table: "CaseIntakeLinks",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseListPresets_Name",
                table: "CaseListPresets",
                column: "Name",
                unique: true,
                filter: "[RemovedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CaseManualChases_CaseId_OperationKey",
                table: "CaseManualChases",
                columns: new[] { "CaseId", "OperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseMatchIndex_PrincipalCode_DurableClaimToken",
                table: "CaseMatchIndex",
                columns: new[] { "PrincipalCode", "DurableClaimToken" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseMatchIndex_PrincipalCode_NormalizedSurname",
                table: "CaseMatchIndex",
                columns: new[] { "PrincipalCode", "NormalizedSurname" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseMatchIndex_PrincipalCode_NormalizedVrm",
                table: "CaseMatchIndex",
                columns: new[] { "PrincipalCode", "NormalizedVrm" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseQueries_AssigneeStaffId_RaisedAtUtc",
                table: "CaseQueries",
                columns: new[] { "AssigneeStaffId", "RaisedAtUtc" },
                filter: "[ClosedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CaseQueries_CaseId_IntakeReceiptId",
                table: "CaseQueries",
                columns: new[] { "CaseId", "IntakeReceiptId" },
                unique: true,
                filter: "[ClosedAtUtc] IS NULL AND [IntakeReceiptId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CaseQueries_CaseId_RaisedAtUtc",
                table: "CaseQueries",
                columns: new[] { "CaseId", "RaisedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseQueries_IntakeReceiptId",
                table: "CaseQueries",
                column: "IntakeReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseRepairSpecifications_AiJobId",
                table: "CaseRepairSpecifications",
                column: "AiJobId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseRepairSpecifications_WorkId",
                table: "CaseRepairSpecifications",
                column: "WorkId",
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CaseRepairSpecifications_WorkId_CreationOperationKey",
                table: "CaseRepairSpecifications",
                columns: new[] { "WorkId", "CreationOperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseRepairSpecifications_WorkId_Version",
                table: "CaseRepairSpecifications",
                columns: new[] { "WorkId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseRepairSpecificationSnapshots_SpecificationId_Number",
                table: "CaseRepairSpecificationSnapshots",
                columns: new[] { "SpecificationId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseRepairSpecificationSnapshots_WorkId",
                table: "CaseRepairSpecificationSnapshots",
                column: "WorkId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseReportApprovals_CaseId_ArtifactIdentity_ArtifactSha256",
                table: "CaseReportApprovals",
                columns: new[] { "CaseId", "ArtifactIdentity", "ArtifactSha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseReportGenerations_CaseId",
                table: "CaseReportGenerations",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseReportGenerations_WorkId_SnapshotHash",
                table: "CaseReportGenerations",
                columns: new[] { "WorkId", "SnapshotHash" },
                unique: true,
                filter: "[State] <> N'Stale'");

            migrationBuilder.CreateIndex(
                name: "IX_CaseReportSentEvidence_CaseId",
                table: "CaseReportSentEvidence",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseReportSentEvidence_DiscoveredAtUtc_Id",
                table: "CaseReportSentEvidence",
                columns: new[] { "DiscoveredAtUtc", "Id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_CaseReportSentEvidence_InternetMessageIdentity",
                table: "CaseReportSentEvidence",
                column: "InternetMessageIdentity");

            migrationBuilder.CreateIndex(
                name: "IX_CaseReportSentEvidence_MailboxIdentity_ImmutableItemIdentity",
                table: "CaseReportSentEvidence",
                columns: new[] { "MailboxIdentity", "ImmutableItemIdentity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseReportSentEvidence_RetentionOperationKey",
                table: "CaseReportSentEvidence",
                column: "RetentionOperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseReportWordings_WorkId_BlockKey",
                table: "CaseReportWordings",
                columns: new[] { "WorkId", "BlockKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cases_AuditReference",
                table: "Cases",
                column: "AuditReference",
                unique: true,
                filter: "[AuditReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_CreatedAtUtc",
                table: "Cases",
                column: "CreatedAtUtc")
                .Annotation("SqlServer:Include", new[] { "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_OriginIntakeReceiptId",
                table: "Cases",
                column: "OriginIntakeReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_PrincipalId",
                table: "Cases",
                column: "PrincipalId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_Reference",
                table: "Cases",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cases_SequenceLineageId_Year_Sequence",
                table: "Cases",
                columns: new[] { "SequenceLineageId", "Year", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cases_StandaloneAuditEvidenceId",
                table: "Cases",
                column: "StandaloneAuditEvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTasks_AssigneeId_State",
                table: "CaseTasks",
                columns: new[] { "AssigneeId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseTasks_CaseId_State",
                table: "CaseTasks",
                columns: new[] { "CaseId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseTasks_State",
                table: "CaseTasks",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_CaseValuations_WorkId_Date_Time",
                table: "CaseValuations",
                columns: new[] { "WorkId", "Date", "Time" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseWorkflowEvents_CaseId_AfterVersion",
                table: "CaseWorkflowEvents",
                columns: new[] { "CaseId", "AfterVersion" },
                unique: true,
                filter: "[EventType] <> 'operator_note' AND [EventType] <> 'case_guidance_applied' AND [EventType] <> 'case_report_draft_previewed' AND [EventType] <> 'case_report_artifact_downloaded' AND [EventType] <> 'case_estimate_document_previewed' AND [EventType] <> 'edit_lease_taken_over' AND [EventType] <> 'market_research_attached' AND [EventType] <> 'correspondence_sent'");

            migrationBuilder.CreateIndex(
                name: "IX_CaseWorkflowEvents_CaseId_OccurredAtUtc_Id",
                table: "CaseWorkflowEvents",
                columns: new[] { "CaseId", "OccurredAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_CaseWorkflowEvents_CaseId_OperationKey",
                table: "CaseWorkflowEvents",
                columns: new[] { "CaseId", "OperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseWorkflowEvents_EventType_OccurredAtUtc",
                table: "CaseWorkflowEvents",
                columns: new[] { "EventType", "OccurredAtUtc" })
                .Annotation("SqlServer:Include", new[] { "CaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseWorkflows_HasOpenQuery",
                table: "CaseWorkflows",
                column: "HasOpenQuery",
                filter: "[HasOpenQuery] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CaseWorkflows_OriginalCaseId",
                table: "CaseWorkflows",
                column: "OriginalCaseId",
                unique: true,
                filter: "[OriginalCaseId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CaseWorkflows_ReplacementCaseId",
                table: "CaseWorkflows",
                column: "ReplacementCaseId",
                unique: true,
                filter: "[ReplacementCaseId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CaseWorkflows_ReportApprovalId",
                table: "CaseWorkflows",
                column: "ReportApprovalId",
                unique: true,
                filter: "[ReportApprovalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CaseWorkflows_ReportSentEvidenceId",
                table: "CaseWorkflows",
                column: "ReportSentEvidenceId",
                unique: true,
                filter: "[ReportSentEvidenceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CaseWorkflows_State",
                table: "CaseWorkflows",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_CaseWorks_CaseId_Kind",
                table: "CaseWorks",
                columns: new[] { "CaseId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseWorks_ReportApprovalId",
                table: "CaseWorks",
                column: "ReportApprovalId",
                unique: true,
                filter: "[ReportApprovalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CaseWorks_ReportSentEvidenceId",
                table: "CaseWorks",
                column: "ReportSentEvidenceId",
                unique: true,
                filter: "[ReportSentEvidenceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPrincipalLinks_OrganizationId",
                table: "ContactPrincipalLinks",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentContentCacheEntries_DocumentVersionId_Variant",
                table: "DocumentContentCacheEntries",
                columns: new[] { "DocumentVersionId", "Variant" },
                unique: true,
                filter: "[DocumentVersionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentContentCacheEntries_IntakeAssetId_Variant",
                table: "DocumentContentCacheEntries",
                columns: new[] { "IntakeAssetId", "Variant" },
                unique: true,
                filter: "[IntakeAssetId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_CaseId_DocumentId",
                table: "DocumentOccurrences",
                columns: new[] { "CaseId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_CaseId_OperationKey",
                table: "DocumentOccurrences",
                columns: new[] { "CaseId", "OperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_CaseId_RecordedAtUtc_Id",
                table: "DocumentOccurrences",
                columns: new[] { "CaseId", "RecordedAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_DocumentId",
                table: "DocumentOccurrences",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_VersionId",
                table: "DocumentOccurrences",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrenceTags_TagId",
                table: "DocumentOccurrenceTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_CustodyStatus",
                table: "DocumentVersions",
                column: "CustodyStatus",
                filter: "[CustodyStatus] = N'Pending'")
                .Annotation("SqlServer:Include", new[] { "PendingContentStorageKey" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_DocumentId_Version",
                table: "DocumentVersions",
                columns: new[] { "DocumentId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EditScopes_ExpiresAtUtc",
                table: "EditScopes",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EditScopes_HolderKind_Holder",
                table: "EditScopes",
                columns: new[] { "HolderKind", "Holder" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailResponseEvidence_DiscoveredAtUtc_Id",
                table: "EmailResponseEvidence",
                columns: new[] { "DiscoveredAtUtc", "Id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_EmailResponseEvidence_MessageIdentity",
                table: "EmailResponseEvidence",
                column: "MessageIdentity",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailResponseEvidence_OperationKey",
                table: "EmailResponseEvidence",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailResponseEvidence_PollOutcomeId",
                table: "EmailResponseEvidence",
                column: "PollOutcomeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailResponseEvidence_SentEvidenceId",
                table: "EmailResponseEvidence",
                column: "SentEvidenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorkItems_CaseId",
                table: "ExternalWorkItems",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorkItems_CompletedAtUtc_Id",
                table: "ExternalWorkItems",
                columns: new[] { "CompletedAtUtc", "Id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorkItems_DueAtUtc_Id",
                table: "ExternalWorkItems",
                columns: new[] { "DueAtUtc", "Id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorkItems_ImageIntakeId",
                table: "ExternalWorkItems",
                column: "ImageIntakeId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorkItems_LeaseExpiresAtUtc_Id",
                table: "ExternalWorkItems",
                columns: new[] { "LeaseExpiresAtUtc", "Id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorkItems_OperationKey",
                table: "ExternalWorkItems",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorkItems_SourceCaseId",
                table: "ExternalWorkItems",
                column: "SourceCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorkItems_State_DueAtUtc",
                table: "ExternalWorkItems",
                columns: new[] { "State", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedCaseArtifacts_GenerationId_Kind",
                table: "GeneratedCaseArtifacts",
                columns: new[] { "GenerationId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedCaseArtifacts_OperationKey",
                table: "GeneratedCaseArtifacts",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedCaseArtifacts_VersionId",
                table: "GeneratedCaseArtifacts",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_GlassRepairEstimateSessions_ActiveAccountKey",
                table: "GlassRepairEstimateSessions",
                column: "ActiveAccountKey",
                unique: true,
                filter: "[ActiveAccountKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GlassRepairEstimateSessions_CallbackDigest",
                table: "GlassRepairEstimateSessions",
                column: "CallbackDigest");

            migrationBuilder.CreateIndex(
                name: "IX_GlassRepairEstimateSessions_CaseId",
                table: "GlassRepairEstimateSessions",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_GlassRepairEstimateSessions_OperationKey",
                table: "GlassRepairEstimateSessions",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlassRepairEstimateSessions_UserId",
                table: "GlassRepairEstimateSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakeLifecycleEvents_ImageIntakeId_OccurredAtUtc",
                table: "ImageIntakeLifecycleEvents",
                columns: new[] { "ImageIntakeId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakeLifecycleEvents_OperationKey",
                table: "ImageIntakeLifecycleEvents",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakes_CreationOperationKey",
                table: "ImageIntakes",
                column: "CreationOperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakes_ImageIntakeReference",
                table: "ImageIntakes",
                column: "ImageIntakeReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakes_LifecycleState_CreatedAtUtc",
                table: "ImageIntakes",
                columns: new[] { "LifecycleState", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakes_NormalizedVehicleRegistration_CreatedAtUtc",
                table: "ImageIntakes",
                columns: new[] { "NormalizedVehicleRegistration", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakes_OriginReceiptId",
                table: "ImageIntakes",
                column: "OriginReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakes_PrincipalId",
                table: "ImageIntakes",
                column: "PrincipalId");

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakes_SourceChannel_ExternalReceiptToken",
                table: "ImageIntakes",
                columns: new[] { "SourceChannel", "ExternalReceiptToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakes_SubmissionGroupId",
                table: "ImageIntakes",
                column: "SubmissionGroupId",
                unique: true,
                filter: "[SubmissionGroupId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ImageTags_CreateOperationKey",
                table: "ImageTags",
                column: "CreateOperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImageTags_NormalizedName",
                table: "ImageTags",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImageVrmSuggestions_IntakeAssetId",
                table: "ImageVrmSuggestions",
                column: "IntakeAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ImageVrmSuggestions_IntakeReceiptId_OccurredAtUtc",
                table: "ImageVrmSuggestions",
                columns: new[] { "IntakeReceiptId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ImageVrmSuggestions_OperationKey",
                table: "ImageVrmSuggestions",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionLocationPrincipalLinks_InspectionLocationId",
                table: "InspectionLocationPrincipalLinks",
                column: "InspectionLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionLocations_Source_SourceRecordId",
                table: "InspectionLocations",
                columns: new[] { "Source", "SourceRecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeAllocationAttempts_IntakeReceiptId_AttemptNumber",
                table: "IntakeAllocationAttempts",
                columns: new[] { "IntakeReceiptId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeAllocationAttempts_OperationKey",
                table: "IntakeAllocationAttempts",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeAssets_IntakeReceiptId_ContentHash",
                table: "IntakeAssets",
                columns: new[] { "IntakeReceiptId", "ContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeAssetTags_TagId",
                table: "IntakeAssetTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeEvaluations_StagedReceiptId_Revision",
                table: "IntakeEvaluations",
                columns: new[] { "StagedReceiptId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeMailClassificationHistory_IntakeReceiptId_Version",
                table: "IntakeMailClassificationHistory",
                columns: new[] { "IntakeReceiptId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeManualAssociations_ActorKind_LinkedAtUtc_IntakeReceiptId",
                table: "IntakeManualAssociations",
                columns: new[] { "ActorKind", "LinkedAtUtc", "IntakeReceiptId" },
                filter: "[IsActive] = 1")
                .Annotation("SqlServer:Include", new[] { "CaseId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeManualAssociations_CaseId",
                table: "IntakeManualAssociations",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeManualAssociations_LastOperationKey",
                table: "IntakeManualAssociations",
                column: "LastOperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeMutationHistory_CaseId_OccurredAtUtc",
                table: "IntakeMutationHistory",
                columns: new[] { "CaseId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeMutationHistory_IntakeReceiptId_OccurredAtUtc",
                table: "IntakeMutationHistory",
                columns: new[] { "IntakeReceiptId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeMutationHistory_OperationKey",
                table: "IntakeMutationHistory",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeOcrOperations_IntakeAssetId",
                table: "IntakeOcrOperations",
                column: "IntakeAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeOcrOperations_OperationKey",
                table: "IntakeOcrOperations",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReceiptEvents_IntakeReceiptId",
                table: "IntakeReceiptEvents",
                column: "IntakeReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReceipts_DeclaredCaseId",
                table: "IntakeReceipts",
                column: "DeclaredCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReceipts_SourceChannel_ExternalReceiptToken",
                table: "IntakeReceipts",
                columns: new[] { "SourceChannel", "ExternalReceiptToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReceipts_SourceChannel_ProcessedAtUtc_Id",
                table: "IntakeReceipts",
                columns: new[] { "SourceChannel", "ProcessedAtUtc", "Id" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReceipts_SourceChannel_ReceivedAtUtc",
                table: "IntakeReceipts",
                columns: new[] { "SourceChannel", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReceipts_SourceHash",
                table: "IntakeReceipts",
                column: "SourceHash");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeSearchDocuments_IntakeReceiptId_Ordinal",
                table: "IntakeSearchDocuments",
                columns: new[] { "IntakeReceiptId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeSourceCandidates_AnalysisId",
                table: "IntakeSourceCandidates",
                column: "AnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeSourceCandidates_DocumentVersionId",
                table: "IntakeSourceCandidates",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeSourceCandidates_IntakeAssetId",
                table: "IntakeSourceCandidates",
                column: "IntakeAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeStagedReceipts_DeclaredCaseId",
                table: "IntakeStagedReceipts",
                column: "DeclaredCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeStagedReceipts_SourceChannel_ExternalReceiptToken",
                table: "IntakeStagedReceipts",
                columns: new[] { "SourceChannel", "ExternalReceiptToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeStagedReceipts_SourceHash",
                table: "IntakeStagedReceipts",
                column: "SourceHash");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeSubmissionGroupHistory_GroupId_OccurredAtUtc",
                table: "IntakeSubmissionGroupHistory",
                columns: new[] { "GroupId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeSubmissionGroupHistory_OperationKey",
                table: "IntakeSubmissionGroupHistory",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeSubmissionGroupMembers_GroupId_Ordinal",
                table: "IntakeSubmissionGroupMembers",
                columns: new[] { "GroupId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeSubmissionGroupMembers_StagedReceiptId",
                table: "IntakeSubmissionGroupMembers",
                column: "StagedReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeSubmissionGroups_ParentReceiptId",
                table: "IntakeSubmissionGroups",
                column: "ParentReceiptId",
                unique: true,
                filter: "[ParentReceiptId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeSubmissionGroups_SourceChannel_SubmissionToken",
                table: "IntakeSubmissionGroups",
                columns: new[] { "SourceChannel", "SubmissionToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeWorkItems_OperationKey",
                table: "IntakeWorkItems",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeWorkItems_ProcessedReceiptId",
                table: "IntakeWorkItems",
                column: "ProcessedReceiptId")
                .Annotation("SqlServer:Include", new[] { "State", "StagedReceiptId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeWorkItems_StagedReceiptId",
                table: "IntakeWorkItems",
                column: "StagedReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeWorkItems_State_DueAtUtc",
                table: "IntakeWorkItems",
                columns: new[] { "State", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictApplications_ClientId",
                table: "OpenIddictApplications",
                column: "ClientId",
                unique: true,
                filter: "[ClientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type",
                table: "OpenIddictAuthorizations",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictScopes_Name",
                table: "OpenIddictScopes",
                column: "Name",
                unique: true,
                filter: "[Name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type",
                table: "OpenIddictTokens",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_AuthorizationId",
                table: "OpenIddictTokens",
                column: "AuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ReferenceId",
                table: "OpenIddictTokens",
                column: "ReferenceId",
                unique: true,
                filter: "[ReferenceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Name",
                table: "Organizations",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_NormalizedName_Role",
                table: "Organizations",
                columns: new[] { "NormalizedName", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrincipalApiCredentials_KeyId",
                table: "PrincipalApiCredentials",
                column: "KeyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Principals_Code",
                table: "Principals",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Principals_OrganizationId",
                table: "Principals",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Principals_PredecessorId",
                table: "Principals",
                column: "PredecessorId",
                unique: true,
                filter: "[PredecessorId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Principals_SequenceLineageId",
                table: "Principals",
                column: "SequenceLineageId");

            migrationBuilder.CreateIndex(
                name: "IX_Principals_SuccessorId",
                table: "Principals",
                column: "SuccessorId",
                unique: true,
                filter: "[SuccessorId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PrincipalSubmissions_PrincipalId",
                table: "PrincipalSubmissions",
                column: "PrincipalId");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemReports_CreatedAtUtc",
                table: "ProblemReports",
                column: "CreatedAtUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ProblemReports_OperationKey",
                table: "ProblemReports",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseNoteAcknowledgements_ReleaseNoteId",
                table: "ReleaseNoteAcknowledgements",
                column: "ReleaseNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseNotes_OperationKey",
                table: "ReleaseNotes",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseNotes_Status_PublishedAtUtc",
                table: "ReleaseNotes",
                columns: new[] { "Status", "PublishedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseNotes_UpdatedAtUtc",
                table: "ReleaseNotes",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RetainedInstructionAnalyses_IntakeAssetId",
                table: "RetainedInstructionAnalyses",
                column: "IntakeAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_RetainedInstructionAnalyses_IntakeReceiptId_IntakeAssetId_OperationKey",
                table: "RetainedInstructionAnalyses",
                columns: new[] { "IntakeReceiptId", "IntakeAssetId", "OperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxAttachments_RetainedMailboxMessageId_Ordinal",
                table: "RetainedMailboxAttachments",
                columns: new[] { "RetainedMailboxMessageId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_ConversationIdentity",
                table: "RetainedMailboxMessages",
                column: "ConversationIdentity");

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_ExternalReceiptToken",
                table: "RetainedMailboxMessages",
                column: "ExternalReceiptToken");

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_FolderScope_CanonicalInternetMessageIdentity",
                table: "RetainedMailboxMessages",
                columns: new[] { "FolderScope", "CanonicalInternetMessageIdentity" },
                filter: "[CanonicalInternetMessageIdentity] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_FolderScope_CanonicalInternetMessageIdentity",
                table: "RetainedMailboxMessages",
                columns: new[] { "MailboxId", "FolderScope", "CanonicalInternetMessageIdentity" },
                unique: true,
                filter: "[CanonicalInternetMessageIdentity] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_FolderScope_ReceivedAtUtc_Id",
                table: "RetainedMailboxMessages",
                columns: new[] { "MailboxId", "FolderScope", "ReceivedAtUtc", "Id" },
                descending: new[] { false, false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_ImmutableMessageId",
                table: "RetainedMailboxMessages",
                columns: new[] { "MailboxId", "ImmutableMessageId" },
                unique: true,
                filter: "[MailboxId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_ReceivedAtUtc_Id",
                table: "RetainedMailboxMessages",
                columns: new[] { "ReceivedAtUtc", "Id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_OccurredAtUtc",
                table: "SecurityEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_SubjectId_OccurredAtUtc",
                table: "SecurityEvents",
                columns: new[] { "SubjectId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SentEmailEvidence_ChaseDueAtUtc_TriageCaseId",
                table: "SentEmailEvidence",
                columns: new[] { "ChaseDueAtUtc", "TriageCaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_SentEmailEvidence_MessageIdentity",
                table: "SentEmailEvidence",
                column: "MessageIdentity",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SentEmailEvidence_OperationKey",
                table: "SentEmailEvidence",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SentEmailEvidence_SentAtUtc_Id",
                table: "SentEmailEvidence",
                columns: new[] { "SentAtUtc", "Id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_SentEmailEvidence_TriageCaseId",
                table: "SentEmailEvidence",
                column: "TriageCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMailSendOperations_ActorSubjectId_MailboxId_OperationKey",
                table: "StaffMailSendOperations",
                columns: new[] { "ActorSubjectId", "MailboxId", "OperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffMailSendOperations_Purpose_State",
                table: "StaffMailSendOperations",
                columns: new[] { "Purpose", "State" },
                filter: "[InstructionMoveState] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMailSendOperations_Purpose_State_ObservedSentAtUtc",
                table: "StaffMailSendOperations",
                columns: new[] { "Purpose", "State", "ObservedSentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffNotifications_CaseId",
                table: "StaffNotifications",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffNotifications_RaisedAtUtc",
                table: "StaffNotifications",
                column: "RaisedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StaffNotifications_StaffId_RaisedAtUtc",
                table: "StaffNotifications",
                columns: new[] { "StaffId", "RaisedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_StandaloneAuditEvidence_IntakeReceiptId",
                table: "StandaloneAuditEvidence",
                column: "IntakeReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StandaloneAuditEvidence_OperationKey",
                table: "StandaloneAuditEvidence",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StandaloneAuditEvidence_OriginalReportAssetId",
                table: "StandaloneAuditEvidence",
                column: "OriginalReportAssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Triage_CreationOperationKey",
                table: "Triage",
                column: "CreationOperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Triage_LinkedInstructionCaseId",
                table: "Triage",
                column: "LinkedInstructionCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Triage_OriginReceiptId",
                table: "Triage",
                column: "OriginReceiptId",
                unique: true,
                filter: "[OriginReceiptId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Triage_SourceChannel_ExternalReceiptToken",
                table: "Triage",
                columns: new[] { "SourceChannel", "ExternalReceiptToken" },
                unique: true,
                filter: "[SourceChannel] IS NOT NULL AND [ExternalReceiptToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Triage_State_CreatedAtUtc",
                table: "Triage",
                columns: new[] { "State", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TriageFindings_OperationKey",
                table: "TriageFindings",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TriageFindings_SupersedesFindingId",
                table: "TriageFindings",
                column: "SupersedesFindingId",
                unique: true,
                filter: "[SupersedesFindingId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TriageFindings_TriageCaseId_RecordedAtUtc",
                table: "TriageFindings",
                columns: new[] { "TriageCaseId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TriageHistory_OperationKey",
                table: "TriageHistory",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TriageHistory_TriageCaseId_OccurredAtUtc",
                table: "TriageHistory",
                columns: new[] { "TriageCaseId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TriageResponseEvidenceLinks_OperationKey",
                table: "TriageResponseEvidenceLinks",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TriageResponseEvidenceLinks_SentEvidenceId",
                table: "TriageResponseEvidenceLinks",
                column: "SentEvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_TriageResponseEvidenceLinks_TriageCaseId",
                table: "TriageResponseEvidenceLinks",
                column: "TriageCaseId",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_UnroadworthyReasons_PrincipalCode_Text",
                table: "UnroadworthyReasons",
                columns: new[] { "PrincipalCode", "Text" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserExternalCredentials_Provider_NormalizedAccountKey",
                table: "UserExternalCredentials",
                columns: new[] { "Provider", "NormalizedAccountKey" });

            migrationBuilder.CreateIndex(
                name: "IX_UserExternalCredentials_Provider_UserId",
                table: "UserExternalCredentials",
                columns: new[] { "Provider", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserExternalCredentials_UserId",
                table: "UserExternalCredentials",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationPresets_Label",
                table: "ValuationPresets",
                column: "Label",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleLookupObservations_Provider_ProviderVersion_ResponseIdentity",
                table: "VehicleLookupObservations",
                columns: new[] { "Provider", "ProviderVersion", "ResponseIdentity" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleLookupObservations_WorkItemId_AttemptNumber",
                table: "VehicleLookupObservations",
                columns: new[] { "WorkItemId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleLookupRequests_CaseId_OperationKey",
                table: "VehicleLookupRequests",
                columns: new[] { "CaseId", "OperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleLookupRequests_CaseId_Registration",
                table: "VehicleLookupRequests",
                columns: new[] { "CaseId", "Registration" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleLookupRequests_CaseId_RequestedAtUtc",
                table: "VehicleLookupRequests",
                columns: new[] { "CaseId", "RequestedAtUtc" });

            if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                // The two runtime roles, then every grant and denial in
                // RuntimePermissions.csv as the record stood when the Baseline
                // was generated. A later migration that changes a role's rights
                // carries its own statements and changes the record; this block
                // never changes (R481, R482).
                migrationBuilder.Sql("""
                    IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NULL CREATE ROLE [pegasus_web_runtime_role] AUTHORIZATION [dbo];
                    IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NULL CREATE ROLE [pegasus_worker_runtime_role] AUTHORIZATION [dbo];
                    """);
                migrationBuilder.Sql("""
                    DENY DELETE ON OBJECT::[dbo].[ActionHistory] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[ActionHistory] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ActionHistory] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[ActionHistory] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[AiJobs] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[AppliedValuationAdditions] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[AppliedValuationAdditions] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[AppliedValuationAdditions] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[AppliedValuationCalculations] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[AppliedValuationCalculations] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[AppliedValuationCalculations] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[AppliedValuationSnapshots] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[AppliedValuationSnapshots] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[AppliedValuationSnapshots] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ApprovedInboxPoisonMessages] TO [pegasus_web_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[ApprovedInboxPoisonMessages] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ApprovedInboxPoisonMessages] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[ApprovedInboxPoisonMessages] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ApprovedInboxPollStates] TO [pegasus_web_runtime_role];
                    GRANT SELECT, UPDATE ON OBJECT::[dbo].[ApprovedInboxPollStates] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ApprovedInboxPollStates] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ApprovedInboxPollStates] TO [pegasus_worker_runtime_role];
                    DENY INSERT, UPDATE, DELETE ON OBJECT::[dbo].[ApprovedMailboxSubscriptions] TO [pegasus_web_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[ApprovedMailboxSubscriptions] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ApprovedMailboxSubscriptions] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ApprovedMailboxSubscriptions] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ApprovedMailboxes] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ApprovedMailboxes] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ApprovedMailboxes] TO [pegasus_worker_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[ApprovedMailboxes] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ApprovedSentPollOutcomes] TO [pegasus_web_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[ApprovedSentPollOutcomes] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ApprovedSentPollOutcomes] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[ApprovedSentPollOutcomes] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ApprovedSentPollStates] TO [pegasus_web_runtime_role];
                    GRANT SELECT, UPDATE ON OBJECT::[dbo].[ApprovedSentPollStates] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ApprovedSentPollStates] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ApprovedSentPollStates] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[AspNetRoleClaims] TO [pegasus_web_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[AspNetRoleClaims] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[AspNetRoleClaims] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[AspNetRoles] TO [pegasus_web_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[AspNetRoles] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[AspNetRoles] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[AspNetUserClaims] TO [pegasus_web_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[AspNetUserClaims] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[AspNetUserClaims] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[AspNetUserLogins] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[AspNetUserLogins] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, DELETE ON OBJECT::[dbo].[AspNetUserRoles] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[AspNetUserRoles] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[AspNetUserTokens] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[AspNetUserTokens] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[AspNetUsers] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[AspNetUsers] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[CaseAssessmentFields] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[CaseAssessmentFields] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[CaseDataFields] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseDataFields] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseDataFields] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseDataSnapshots] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseDataSnapshots] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseDataSnapshots] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseDataSnapshots] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseDocuments] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[CaseDocuments] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseDocuments] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[CaseDocuments] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseDueChasers] TO [pegasus_web_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[CaseDueChasers] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseDueChasers] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseDueChasers] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseDueWork] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseDueWork] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseDueWork] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseDueWork] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseEditLeaseOperations] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[CaseEditLeaseOperations] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseEditLeaseOperations] TO [pegasus_worker_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[CaseEditLeaseOperations] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[CaseEstimateLines] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseHistory] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[CaseHistory] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseHistory] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[CaseHistory] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseIntakeLinks] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[CaseIntakeLinks] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseIntakeLinks] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[CaseIntakeLinks] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseListPresets] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseListPresets] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseListPresets] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseManualChases] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[CaseManualChases] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseManualChases] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[CaseManualChases] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[CaseMatchIndex] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseMatchIndex] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseMatchIndex] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseQueries] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseQueries] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseQueries] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseQueries] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, DELETE ON OBJECT::[dbo].[CaseRepairSpecificationSnapshots] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseRepairSpecificationSnapshots] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[CaseRepairSpecificationSnapshots] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[CaseRepairSpecifications] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseReportApprovals] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[CaseReportApprovals] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseReportApprovals] TO [pegasus_worker_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[CaseReportApprovals] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseReportGenerations] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseReportGenerations] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseReportGenerations] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, UPDATE ON OBJECT::[dbo].[CaseReportGenerations] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseReportSentEvidence] TO [pegasus_web_runtime_role];
                    GRANT SELECT, UPDATE ON OBJECT::[dbo].[CaseReportSentEvidence] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseReportSentEvidence] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseReportSentEvidence] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseReportWordings] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseReportWordings] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseReportWordings] TO [pegasus_worker_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[CaseReportWordings] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseSequences] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseSequences] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseSequences] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseSequences] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseTasks] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseTasks] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseTasks] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[CaseTasks] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseValuations] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseWorkflowEvents] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[CaseWorkflowEvents] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseWorkflowEvents] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[CaseWorkflowEvents] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseWorkflows] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseWorkflows] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseWorkflows] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseWorkflows] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseWorks] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseWorks] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseWorks] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseWorks] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[Cases] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[Cases] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[Cases] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[Cases] TO [pegasus_worker_runtime_role];
                    DENY UPDATE ON OBJECT::[dbo].[ContactPrincipalLinks] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, DELETE ON OBJECT::[dbo].[ContactPrincipalLinks] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ContactPrincipalLinks] TO [pegasus_worker_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[ContactPrincipalLinks] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[DocumentContentCacheEntries] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[DocumentContentCacheEntries] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[DocumentContentCacheEntries] TO [pegasus_worker_runtime_role];
                    DENY UPDATE ON OBJECT::[dbo].[DocumentOccurrenceTags] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, DELETE ON OBJECT::[dbo].[DocumentOccurrenceTags] TO [pegasus_web_runtime_role];
                    DENY UPDATE, DELETE ON OBJECT::[dbo].[DocumentOccurrenceTags] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[DocumentOccurrenceTags] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[DocumentOccurrences] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[DocumentOccurrences] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[DocumentOccurrences] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[DocumentOccurrences] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[DocumentVersions] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[DocumentVersions] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[DocumentVersions] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[DocumentVersions] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[EditScopes] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[EmailResponseEvidence] TO [pegasus_web_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[EmailResponseEvidence] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[EmailResponseEvidence] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[EmailResponseEvidence] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[EmailTemplates] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[EmailTemplates] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[EmailTemplates] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ExternalWorkItems] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ExternalWorkItems] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ExternalWorkItems] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ExternalWorkItems] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[GeneratedCaseArtifacts] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[GeneratedCaseArtifacts] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[GeneratedCaseArtifacts] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, UPDATE ON OBJECT::[dbo].[GeneratedCaseArtifacts] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[GlassRepairEstimateSessions] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[GlassRepairEstimateSessions] TO [pegasus_worker_runtime_role];
                    DENY UPDATE, DELETE ON OBJECT::[dbo].[ImageIntakeLifecycleEvents] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[ImageIntakeLifecycleEvents] TO [pegasus_web_runtime_role];
                    DENY UPDATE, DELETE ON OBJECT::[dbo].[ImageIntakeLifecycleEvents] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[ImageIntakeLifecycleEvents] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ImageIntakeSequences] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ImageIntakeSequences] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ImageIntakeSequences] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ImageIntakeSequences] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ImageIntakes] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ImageIntakes] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ImageIntakes] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ImageIntakes] TO [pegasus_worker_runtime_role];
                    DENY UPDATE, DELETE ON OBJECT::[dbo].[ImageTags] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[ImageTags] TO [pegasus_web_runtime_role];
                    DENY UPDATE, DELETE ON OBJECT::[dbo].[ImageTags] TO [pegasus_worker_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[ImageTags] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ImageVrmSuggestions] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ImageVrmSuggestions] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ImageVrmSuggestions] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ImageVrmSuggestions] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[InspectionLocationPrincipalLinks] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[InspectionLocationPrincipalLinks] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[InspectionLocationPrincipalLinks] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[InspectionLocations] TO [pegasus_web_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[InspectionLocations] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[InspectionLocations] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[InstructionDrafts] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[InstructionDrafts] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[InstructionDrafts] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[InstructionDrafts] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[IntakeAllocationAttempts] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[IntakeAllocationAttempts] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeAssetPreparations] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[IntakeAssetPreparations] TO [pegasus_web_runtime_role];
                    DENY UPDATE, DELETE ON OBJECT::[dbo].[IntakeAssetPreparations] TO [pegasus_worker_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[IntakeAssetPreparations] TO [pegasus_worker_runtime_role];
                    DENY UPDATE ON OBJECT::[dbo].[IntakeAssetTags] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, DELETE ON OBJECT::[dbo].[IntakeAssetTags] TO [pegasus_web_runtime_role];
                    DENY UPDATE, DELETE ON OBJECT::[dbo].[IntakeAssetTags] TO [pegasus_worker_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[IntakeAssetTags] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeAssets] TO [pegasus_web_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[IntakeAssets] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeAssets] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[IntakeAssets] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeCaseMatchDecisions] TO [pegasus_web_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[IntakeCaseMatchDecisions] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[IntakeCaseMatchDecisions] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeEvaluations] TO [pegasus_web_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[IntakeEvaluations] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeEvaluations] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[IntakeEvaluations] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeMailClassificationDecisions] TO [pegasus_web_runtime_role];
                    GRANT SELECT, UPDATE ON OBJECT::[dbo].[IntakeMailClassificationDecisions] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[IntakeMailClassificationDecisions] TO [pegasus_worker_runtime_role];
                    DENY UPDATE, DELETE ON OBJECT::[dbo].[IntakeMailClassificationHistory] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[IntakeMailClassificationHistory] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeMailRouteDecisions] TO [pegasus_web_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[IntakeMailRouteDecisions] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeMailRouteDecisions] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[IntakeMailRouteDecisions] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeManualAssociations] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[IntakeManualAssociations] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeManualAssociations] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[IntakeManualAssociations] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeMutationHistory] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[IntakeMutationHistory] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeMutationHistory] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[IntakeMutationHistory] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeOcrOperations] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[IntakeOcrOperations] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeOcrOperations] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[IntakeOcrOperations] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeReceiptEvents] TO [pegasus_web_runtime_role];
                    GRANT INSERT ON OBJECT::[dbo].[IntakeReceiptEvents] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeReceiptEvents] TO [pegasus_worker_runtime_role];
                    GRANT INSERT ON OBJECT::[dbo].[IntakeReceiptEvents] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeReceipts] TO [pegasus_web_runtime_role];
                    GRANT SELECT, UPDATE ON OBJECT::[dbo].[IntakeReceipts] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeReceipts] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[IntakeReceipts] TO [pegasus_worker_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[IntakeSearchDocuments] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, DELETE ON OBJECT::[dbo].[IntakeSearchDocuments] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeSourceCandidates] TO [pegasus_web_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[IntakeSourceCandidates] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeSourceCandidates] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[IntakeSourceCandidates] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeStagedReceipts] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[IntakeStagedReceipts] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeStagedReceipts] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[IntakeStagedReceipts] TO [pegasus_worker_runtime_role];
                    GRANT INSERT ON OBJECT::[dbo].[IntakeSubmissionGroupHistory] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[IntakeSubmissionGroupMembers] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[IntakeSubmissionGroupMembers] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[IntakeSubmissionGroups] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[IntakeSubmissionGroups] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeWorkItems] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[IntakeWorkItems] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[IntakeWorkItems] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[IntakeWorkItems] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[LabourRateCards] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[LabourRateCards] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[LabourRateCards] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[OpenIddictApplications] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[OpenIddictApplications] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[OpenIddictApplications] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[OpenIddictAuthorizations] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[OpenIddictAuthorizations] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[OpenIddictAuthorizations] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[OpenIddictScopes] TO [pegasus_web_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[OpenIddictScopes] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[OpenIddictScopes] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[OpenIddictTokens] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[OpenIddictTokens] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[OpenIddictTokens] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[OrganizationAdministrationOperations] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[OrganizationAdministrationOperations] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[OrganizationAdministrationOperations] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[Organizations] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[Organizations] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[Organizations] TO [pegasus_worker_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[Organizations] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[PrincipalApiCredentials] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[PrincipalSequenceLineages] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[PrincipalSequenceLineages] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[PrincipalSequenceLineages] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[PrincipalSequenceLineages] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[PrincipalSubmissions] TO [pegasus_web_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[PrincipalSubmissions] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[Principals] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[Principals] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[Principals] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, UPDATE ON OBJECT::[dbo].[Principals] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ProblemReports] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ProblemReports] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ProblemReports] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ReleaseNoteAcknowledgements] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[ReleaseNoteAcknowledgements] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ReleaseNoteAcknowledgements] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ReleaseNotes] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ReleaseNotes] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ReleaseNotes] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[RetainedInstructionAnalyses] TO [pegasus_web_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[RetainedInstructionAnalyses] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[RetainedInstructionAnalyses] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[RetainedInstructionAnalyses] TO [pegasus_worker_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[RetainedMailboxAttachments] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[RetainedMailboxAttachments] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, UPDATE ON OBJECT::[dbo].[RetainedMailboxMessages] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[RetainedMailboxMessages] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[SecurityEvents] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[SecurityEvents] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[SecurityEvents] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[SendToAiControl] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[SendToAiControl] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[SendToAiControl] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[SentEmailEvidence] TO [pegasus_web_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[SentEmailEvidence] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[SentEmailEvidence] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[SentEmailEvidence] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[StaffMailSendOperations] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[StaffMailSendOperations] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[StaffMailSendOperations] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[StaffMailSendOperations] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[StaffNotifications] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[StaffNotifications] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[StandaloneAuditEvidence] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[StandaloneAuditEvidence] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[StandaloneAuditEvidence] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[StandaloneAuditEvidence] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[Triage] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[Triage] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[Triage] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[Triage] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[TriageFindings] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[TriageFindings] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[TriageFindings] TO [pegasus_worker_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[TriageFindings] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[TriageHistory] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[TriageHistory] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[TriageHistory] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[TriageHistory] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, DELETE ON OBJECT::[dbo].[TriageResponseEvidenceLinks] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[TriageResponseEvidenceLinks] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[TriageResponseEvidenceLinks] TO [pegasus_worker_runtime_role];
                    DENY UPDATE, DELETE ON OBJECT::[dbo].[UnidentifiedHistory] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[UnidentifiedHistory] TO [pegasus_web_runtime_role];
                    DENY UPDATE, DELETE ON OBJECT::[dbo].[UnidentifiedHistory] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[UnidentifiedHistory] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, UPDATE ON OBJECT::[dbo].[UnidentifiedItems] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[UnidentifiedItems] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[UnidentifiedSequences] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[UnroadworthyReasons] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[UnroadworthyReasons] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[UnroadworthyReasons] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[UserExternalCredentials] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[UserExternalCredentials] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ValuationPresets] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ValuationPresets] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[ValuationPresets] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[VehicleLookupObservations] TO [pegasus_web_runtime_role];
                    GRANT SELECT, UPDATE ON OBJECT::[dbo].[VehicleLookupObservations] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[VehicleLookupObservations] TO [pegasus_worker_runtime_role];
                    GRANT INSERT ON OBJECT::[dbo].[VehicleLookupObservations] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[VehicleLookupRequests] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[VehicleLookupRequests] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[VehicleLookupRequests] TO [pegasus_worker_runtime_role];
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[VehicleLookupRequests] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[WorkCentreDismissals] TO [pegasus_web_runtime_role];
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[WorkCentreDismissals] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[WorkCentreDismissals] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[WorkflowConfigurations] TO [pegasus_web_runtime_role];
                    GRANT SELECT, UPDATE ON OBJECT::[dbo].[WorkflowConfigurations] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[WorkflowConfigurations] TO [pegasus_worker_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[WorkflowConfigurations] TO [pegasus_worker_runtime_role];
                    GRANT SELECT ON OBJECT::[dbo].[__EFMigrationsHistory] TO [pegasus_web_runtime_role];
                    """);

                // The reference rows a database built by the replaced chain held
                // after a fresh migration: the three Identity roles, the built-in
                // image tags, the two labour rate cards, the Unidentified sequence
                // row, and the organisations and Principals the chain seeded.
                // Rows the model seeds itself (HasData) are inserted above by the
                // generated InsertData calls.
                migrationBuilder.Sql("""
                    INSERT INTO [dbo].[AspNetRoles] ([ConcurrencyStamp], [Id], [Name], [NormalizedName]) VALUES (N'bootstrap-administrator', 'B6DC43DE-34A8-438D-BCE9-84DA67554DC0', N'Administrator', N'ADMINISTRATOR');
                    INSERT INTO [dbo].[AspNetRoles] ([ConcurrencyStamp], [Id], [Name], [NormalizedName]) VALUES (N'bootstrap-engineer', '63D5F2EB-C8E8-4D09-A625-3D9D04BF77FC', N'Engineer', N'ENGINEER');
                    INSERT INTO [dbo].[AspNetRoles] ([ConcurrencyStamp], [Id], [Name], [NormalizedName]) VALUES (N'bootstrap-user', '9B71B14E-E6F0-456C-8393-3543FD684EA6', N'User', N'USER');
                    """);
                migrationBuilder.Sql("""
                    INSERT INTO [dbo].[ImageTags] ([Colour], [CreateOperationKey], [CreatedAtUtc], [CreatedBy], [Id], [IsBuiltIn], [Name], [NormalizedName], [Version]) VALUES (N'Amber', N'image-tag-seed:third-party', '2026-10-28T17:19:19+00:00', N'system', '00000000-0000-4000-8000-0000000017A3', 1, N'Third party', N'THIRD PARTY', 1);
                    INSERT INTO [dbo].[ImageTags] ([Colour], [CreateOperationKey], [CreatedAtUtc], [CreatedBy], [Id], [IsBuiltIn], [Name], [NormalizedName], [Version]) VALUES (N'Blue', N'image-tag-seed:overview', '2026-10-28T17:19:19+00:00', N'system', '00000000-0000-4000-8000-0000000017A1', 1, N'Overview', N'OVERVIEW', 1);
                    INSERT INTO [dbo].[ImageTags] ([Colour], [CreateOperationKey], [CreatedAtUtc], [CreatedBy], [Id], [IsBuiltIn], [Name], [NormalizedName], [Version]) VALUES (N'Green', N'image-tag-seed:close-up', '2026-10-28T17:19:19+00:00', N'system', '00000000-0000-4000-8000-0000000017A2', 1, N'Close-up', N'CLOSE-UP', 1);
                    INSERT INTO [dbo].[ImageTags] ([Colour], [CreateOperationKey], [CreatedAtUtc], [CreatedBy], [Id], [IsBuiltIn], [Name], [NormalizedName], [Version]) VALUES (N'Navy', N'image-tag-seed:reflection', '2026-10-28T17:19:19+00:00', N'system', '00000000-0000-4000-8000-0000000017A4', 1, N'Reflection', N'REFLECTION', 1);
                    """);
                migrationBuilder.Sql("""
                    INSERT INTO [dbo].[LabourRateCards] ([ConcurrencyToken], [Id], [Label], [PanelRate], [UpdatedAtUtc], [UpdatedBy], [Version]) VALUES ('A2EDA80D-63FA-4211-B61C-B252EC1A851A', '5B0F3C1E-8F4A-4D2B-9A57-1C3E2F6A7B01', N'Standard', 83.28, '2026-10-28T17:19:19+00:00', N'system', 1);
                    INSERT INTO [dbo].[LabourRateCards] ([ConcurrencyToken], [Id], [Label], [PanelRate], [UpdatedAtUtc], [UpdatedBy], [Version]) VALUES ('FF4A9197-8C67-4E20-AFF1-5F18863EEBC7', '5B0F3C1E-8F4A-4D2B-9A57-1C3E2F6A7B02', N'Prestige/Aluminium', 103.06, '2026-10-28T17:19:19+00:00', N'system', 1);
                    """);
                migrationBuilder.Sql("""
                    SET IDENTITY_INSERT [dbo].[UnidentifiedSequences] ON;
                    INSERT INTO [dbo].[UnidentifiedSequences] ([Id], [LastAllocatedSequence]) VALUES (1, 0);
                    SET IDENTITY_INSERT [dbo].[UnidentifiedSequences] OFF;
                    """);
                migrationBuilder.Sql("""
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (0, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000D035', N'Fraz', NULL, NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000D001', N'QDOS', N'TL threshold: 78%' + NCHAR(13) + NCHAR(10) + NCHAR(13) + NCHAR(10) + N'Inbox: Desk' + NCHAR(13) + NCHAR(10) + N'Image based or address: Always Image-based Assessment', NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000D002', N'PCH / Connexus', N'TL threshold: 66% (always)' + NCHAR(13) + NCHAR(10) + NCHAR(13) + NCHAR(10) + N'The report is named "[reg] Initial" or "[reg] Supplementary".', NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000D003', N'AX', N'TL threshold: 74%' + NCHAR(13) + NCHAR(10) + NCHAR(13) + NCHAR(10) + N'Inbox: Info' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Usually always repairable - occasion TL have to use AX Salvage Matrix' + NCHAR(13) + NCHAR(10) + N'Images location: Always from the garage' + NCHAR(13) + NCHAR(10) + N'Image based or address: Image-based Assessment - Images will always come from the garage - will usually have them prior to doing report as AX want the report doing on a certain day' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note not separate - Copy in engineersreports@ax.com and p.mandy@oakwoodscotland.co.uk (except if request is Easdons and mentions Guardian or mentions James Claims)' + NCHAR(13) + NCHAR(10) + N'ALSO send pages 1-4 back to garage - authority will come from AX', NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000D004', N'Fairway Legal', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Sixways - Most jobs come through Sixways' + NCHAR(13) + NCHAR(10) + N'Veetec Motor Group - Images usually come with instruction' + NCHAR(13) + NCHAR(10) + N'Images location: Sixways/Fairways WhatsApp chat or images from Solicitors (If so image based)' + NCHAR(13) + NCHAR(10) + N'Image based or address: Storage - Somstar Recovery and Storage B5 6JX if not in their yard ask them to confirm address' + NCHAR(13) + NCHAR(10) + N'Solicitors Images - Image based' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note does not need to be separate', NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000D005', N'QC Law', N'Contract repair at 73%.' + NCHAR(13) + NCHAR(10) + NCHAR(13) + NCHAR(10) + N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: QCL/Hackney solutions' + NCHAR(13) + NCHAR(10) + N'Email from vd@complexreports.com' + NCHAR(13) + NCHAR(10) + N'Images location: Hackney Solutions' + NCHAR(13) + NCHAR(10) + N'Image based or address: Storage' + NCHAR(13) + NCHAR(10) + N'HS Recovery & Storage LTD William St M12 5FX most of the time but maybe in another place that looks dodgier Green Street Storage Yard, Burnley, BB111RU' + NCHAR(13) + NCHAR(10) + N'Not in Storage' + NCHAR(13) + NCHAR(10) + N'Claim 3000 Cariocca Business Park, Hellidon Close, Manchester. M12 4AH (Check if it looks like outside office)' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note does not need to be separate but copy in credithire@hackneysolutions.co.uk (for Hackney jobs only direct jobs just go back to email instruction came from)' + NCHAR(13) + NCHAR(10) + NCHAR(13) + NCHAR(10) + N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Direct' + NCHAR(13) + NCHAR(10) + N'Email from vd@complexreports.com' + NCHAR(13) + NCHAR(10) + N'Images location: Direct - usually attached to email if need to chase we have a group on whats app - QC Law VD' + NCHAR(13) + NCHAR(10) + N'Image based or address: Image based or Clients address (usually just check images)' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note does not need to be separate', NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000D006', N'Oakwood Solicitors', N'TL threshold: 65%' + NCHAR(13) + NCHAR(10) + NCHAR(13) + NCHAR(10) + N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Paul Mandy Garages' + NCHAR(13) + NCHAR(10) + N'Images location: Request from garage if one is listed on instruction if not request from client' + NCHAR(13) + NCHAR(10) + N'Image based or address: Inspection address - garage address if from garage usually or ask client to confirm if they provide' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note does not need to be separate - copy in p.mandy@oakwoodscotland.co.uk', NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000D007', N'Smart Business Link', N'TL threshold: 66%' + NCHAR(13) + NCHAR(10) + NCHAR(13) + NCHAR(10) + N'Inbox: Info' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: PDF in Email' + NCHAR(13) + NCHAR(10) + N'Images location: Normally Audatex?' + NCHAR(13) + NCHAR(10) + N'If the instruction mentions "C.A.R.S" then it''s Paul Mandy''s job, in which case we need to reach out to the garage for images/estimate' + NCHAR(13) + NCHAR(10) + N'Image based or address: Image based', NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000D008', N'Blackstone Legal', N'TL threshold: 74%' + NCHAR(13) + NCHAR(10) + NCHAR(13) + NCHAR(10) + N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Instructions usually come in from Shazad' + NCHAR(13) + NCHAR(10) + N'Images location: Foyez: If through Foyez he will provide address.' + NCHAR(13) + NCHAR(10) + N'Javad/Samore Cars: Also sends direct jobs.' + NCHAR(13) + NCHAR(10) + N'Client: Contact direct' + NCHAR(13) + NCHAR(10) + N'Solicitors: Provide images with instruction - ask for location' + NCHAR(13) + NCHAR(10) + N'Image based or address: Confirm address with client/source' + NCHAR(13) + NCHAR(10) + N'If Javad/Samore then 30 Brewery Street, Aston B''ham B6 4JB' + NCHAR(13) + NCHAR(10) + N'"Sixways" = Somstar Recovery B5 6JX' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separate return to file handler', NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000D009', N'Robert James Solicitors', N'TL threshold: 70%' + NCHAR(13) + NCHAR(10) + NCHAR(13) + NCHAR(10) + N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Direct' + NCHAR(13) + NCHAR(10) + N'Images location: Direct - request images and inspection location from Client' + NCHAR(13) + NCHAR(10) + N'Image based or address: If not home address ask whoever provides the images for the location' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separate - copy in engreport@robertjameslaw.co.uk' + NCHAR(13) + NCHAR(10) + NCHAR(13) + NCHAR(10) + N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Accident Specialist' + NCHAR(13) + NCHAR(10) + N'Images location: Accident Specialist/Jazy - check board - they will confirm if images are from client or if in storage they will send' + NCHAR(13) + NCHAR(10) + N'Image based or address: Storage - RH10 9NT (Priestley Way, Crawley)' + NCHAR(13) + NCHAR(10) + N'Not in storage - ask Jazy if he has sent us the images if not ask whoever provided images (if looks like an office RH10 6AE Brighton Road)' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separate - copy in engreport@robertjameslaw.co.uk and admin@accidentspecialist.co.uk' + NCHAR(13) + NCHAR(10) + NCHAR(13) + NCHAR(10) + N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Claim Specialists' + NCHAR(13) + NCHAR(10) + N'Images location: Claim Specialists - images in whats app claim specialist group - most of the time will have images before the instruction - they will confirm address when images provided' + NCHAR(13) + NCHAR(10) + N'Image based or address: Claim Specialist HQ - Kenilworth Road Luton LU1 1DQ occasionally the storage address will be Storage Yard, 363 Dunstable Road, LU4 8BX' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separate - copy in engreport@robertjameslaw.co.uk and also - info@claimspecialists.co.uk and accounts@carhirespec', NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000D00A', N'Davison Flynn Duke Solicitors', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: PDF in email' + NCHAR(13) + NCHAR(10) + N'If SMC, images/instruction may arrive separately.' + NCHAR(13) + NCHAR(10) + N'If so, log separately and wait - don''t chase for images or instructions' + NCHAR(13) + NCHAR(10) + N'Car2Go - as above don''t need to chase until after 7 days' + NCHAR(13) + NCHAR(10) + N'Images location: SMC (they email estimate and images to engineers) Tarran way Moreton. If quiet, copy estimate into parts tab on EVA and do click through, if not, engineers will do as normal.' + NCHAR(13) + NCHAR(10) + N'Nick @ Car2go (Whatsapp group for images and estimate by email - Location Car2go 220 Price street)' + NCHAR(13) + NCHAR(10) + N'Car Claims - ask Andy for images' + NCHAR(13) + NCHAR(10) + N'Plus any other adhoc requests - contact the client for images' + NCHAR(13) + NCHAR(10) + N'Image based or address: Address in Email' + NCHAR(13) + NCHAR(10) + N'If images from Nabeel - 219 Slade Lane, M19 2EX', NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000D00B', N'Knightsbridge Solicitors', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Direct' + NCHAR(13) + NCHAR(10) + N'Images location: Direct - request images and inspection location from Client (occasionaly solicitors will send)' + NCHAR(13) + NCHAR(10) + N'Image based or address: Ask client for images if struggle to get good set can go image based' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separate - Copy in info@knightsbridgesolicitors.co.uk' + NCHAR(13) + NCHAR(10) + NCHAR(13) + NCHAR(10) + N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Kabir' + NCHAR(13) + NCHAR(10) + N'Images location: Kabir Jobs - whats app sometimes we already have images when instruction lands' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separate - Copy in info@knightsbridgesolicitors.co.uk' + NCHAR(13) + NCHAR(10) + NCHAR(13) + NCHAR(10) + N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Expert Claims' + NCHAR(13) + NCHAR(10) + N'Images location: Expert Claims (most of the time)' + NCHAR(13) + NCHAR(10) + N'Image based or address: Expert Claims Whatsapp - if address is in stoke ask Expert claims for images first' + NCHAR(13) + NCHAR(10) + N'Uttoxeter Road, Stoke on Trent, Staffordshire ST3 5LQ' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separate' + NCHAR(13) + NCHAR(10) + NCHAR(13) + NCHAR(10) + N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Apex Hire' + NCHAR(13) + NCHAR(10) + N'Images location: Apex Hire - whats app sometimes we already have images when instruction lands' + NCHAR(13) + NCHAR(10) + N'Image based or address: 25 Camelot Way Birmingham B10 OND' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separate - Copy in info@knightsbridgesolicitors.co.uk', NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000D00C', N'Montreal Prestige', N'Inbox: Desk' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Images should come with instruction' + NCHAR(13) + NCHAR(10) + N'Instruction Date (Use date email arrived not date on instruction)' + NCHAR(13) + NCHAR(10) + N'Inspection Date (Use hand written date they want report doing on which will be the most recent date - thats what date they want inspection/report completing.' + NCHAR(13) + NCHAR(10) + N'Images location: With instruction - if not, email them straight back.' + NCHAR(13) + NCHAR(10) + N'Image based or address: Inspection address listed in report' + NCHAR(13) + NCHAR(10) + N'Sending Report: Report without images on 1 document' + NCHAR(13) + NCHAR(10) + N'Images as 1 document' + NCHAR(13) + NCHAR(10) + N'Parts list as 3rd document' + NCHAR(13) + NCHAR(10) + N'Copy in mdakri@montrealprestige.co.uk', NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000D00D', N'YM Law / Network HD UK', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: PDF in email' + NCHAR(13) + NCHAR(10) + N'Images location: Whatsapp Mark Wilson' + NCHAR(13) + NCHAR(10) + N'Whatsapp Y M Law' + NCHAR(13) + NCHAR(10) + N'Image based or address: If Mark Wilson then Swinton Recovery and Storage, Hurlbutts Drive Queensferry, Deeside CH5 1SF' + NCHAR(13) + NCHAR(10) + NCHAR(13) + NCHAR(10) + N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: PDF in email' + NCHAR(13) + NCHAR(10) + N'Images location: Whatsapp Mark Wilson or Whatsapp Y M Law' + NCHAR(13) + NCHAR(10) + N'Image based or address: If Mark Wilson then Swinton Recovery and Storage, Hurlbutts Drive Queensferry, Deeside CH5 1SF', NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000D00E', N'Auto Logistic Solutions Ltd', N'TL threshold: 78%' + NCHAR(13) + NCHAR(10) + NCHAR(13) + NCHAR(10) + N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Usually say if they wat rw/unr and rep or total loss' + NCHAR(13) + NCHAR(10) + N'Images location: Image Based - Images with email or from garage' + NCHAR(13) + NCHAR(10) + N'Image based or address: Image-based' + NCHAR(13) + NCHAR(10) + N'Sending Report: Send back to handler and claims@autologistic.co.uk with fee separate' + NCHAR(13) + NCHAR(10) + N'Copy in Paul Mandy', NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000D00F', N'Baker Coleman', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Rapid Claims (Huddersfield)' + NCHAR(13) + NCHAR(10) + N'Sometimes instructions state PHYSICAL if so need to instruct Service Certainty' + NCHAR(13) + NCHAR(10) + N'Complete Injury Claims (Wolverhampton)' + NCHAR(13) + NCHAR(10) + N'Images location: Usually with instruction if not sufficient ask Ehjaz (Rapid Claims) via Whatsapp' + NCHAR(13) + NCHAR(10) + N'Complete Injury Claims - Whats app group or images directly from Waseem (Complete Injury Claims)' + NCHAR(13) + NCHAR(10) + N'Image based or address: Clients address and if unable to get good set from solicitors or Ehjaz - go image based' + NCHAR(13) + NCHAR(10) + N'Complete injury Claims - confirm address when receive images from them' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note needs to be separate if complete injury claims job copy in completeinjuryclaims@hotmail.com', NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000D017', N'Midas', N'Inbox: Engineers', NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000F001', N'Car 2 Go', NULL, NULL, N'claim_source', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000F002', N'SMC', NULL, NULL, N'claim_source', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000F003', N'CarClaims', NULL, NULL, N'claim_source', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000F004', N'Expert Claims', NULL, NULL, N'claim_source', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000F005', N'Rapid Rental Solutions', NULL, NULL, N'claim_source', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000F006', N'Easdons', NULL, NULL, N'claim_source', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000F007', N'Guardian Accident Claims', NULL, NULL, N'claim_source', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000F008', N'Hackney Solutions', NULL, NULL, N'claim_source', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000F009', N'James Claims', NULL, NULL, N'claim_source', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000F00A', N'Swinton', NULL, NULL, N'claim_source', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000F00B', N'iRevolution Repair Network', NULL, NULL, N'claim_source', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'Ace Claims & Bodyworks Ltd', NULL, NULL, '00000000-0000-4000-8000-00000000D031', N'Stallion', N'Inbox: Not Applicable' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: In Whatsapp - Stallion Bodyworks' + NCHAR(13) + NCHAR(10) + N'Images location: In Stallion Whatsapp' + NCHAR(13) + NCHAR(10) + N'Image based or address: Address provided in Whatsapp' + NCHAR(13) + NCHAR(10) + N'Sending Report: Via Whatsapp - fee is paid prior to report writing', NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/O Alison Law Solicitors' + NCHAR(13) + NCHAR(10) + N'437-441 London Road' + NCHAR(13) + NCHAR(10) + N'Sheffield', NULL, NULL, '00000000-0000-4000-8000-00000000D012', N'Alison Law Solicitors', N'Inbox: Andrew/Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Usually get a message in whats app' + NCHAR(13) + NCHAR(10) + N'Images location: Whats App group with Alison Law' + NCHAR(13) + NCHAR(10) + N'Image based or address: Image based if not residential address' + NCHAR(13) + NCHAR(10) + N'Ask whoever sends images to confirm' + NCHAR(13) + NCHAR(10) + N'Sending Report: Return to file handler copy in marya.sheikh@alisonlaw.co.uk & Bradford@alison-law.co.uk', N'S2 4HJ', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/O Alliance & Cooper Solicitors' + NCHAR(13) + NCHAR(10) + N'474 Cheetham Hill Road' + NCHAR(13) + NCHAR(10) + N'Manchester', NULL, NULL, '00000000-0000-4000-8000-00000000D023', N'Alliance & Cooper', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Images location: Contact client' + NCHAR(13) + NCHAR(10) + N'Image based or address: Confirm address with client' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separate return to file handler and lamiaa.fadl@ac-solicitors.co.uk', N'M8 9JW', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/O Baker Hardman Solicitors' + NCHAR(13) + NCHAR(10) + N'1A Fairways Office Park' + NCHAR(13) + NCHAR(10) + N'Pittman Way', NULL, NULL, '00000000-0000-4000-8000-00000000D026', N'Baker Hardman', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Most jobs come through Omar' + NCHAR(13) + NCHAR(10) + N'Images location: Omar WhatsApp chat' + NCHAR(13) + NCHAR(10) + N'Image based or address: Storage - Berkstone Motors RG10 9UR' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separately. Send Omar a copy via Whatsapp', N'PR2 9LF', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/O Claim Specialists', NULL, NULL, '00000000-0000-4000-8000-00000000D013', N'Claim Specialists', NULL, NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/O Countrywide Vehicle Rentals Ltd' + NCHAR(13) + NCHAR(10) + N'11 Dickson Street' + NCHAR(13) + NCHAR(10) + N'Liverpool', NULL, NULL, '00000000-0000-4000-8000-00000000D028', N'Countrywide', N'Inbox: Andrew/Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Details usually in email body' + NCHAR(13) + NCHAR(10) + N'Images location: Usually in email' + NCHAR(13) + NCHAR(10) + N'If the vehicle is at Dickson St Liverpool contact Rich Morrison on Whatsapp for images', N'L3 7EB', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/O HTU Assessors Ltd' + NCHAR(13) + NCHAR(10) + N'The Old Courthouse' + NCHAR(13) + NCHAR(10) + N'18-22 St Peters Churchyard', NULL, NULL, '00000000-0000-4000-8000-00000000D014', N'HTU Assessors Ltd', N'Inbox: Ben' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Body of email' + NCHAR(13) + NCHAR(10) + N'Images location: Always in the email' + NCHAR(13) + NCHAR(10) + N'Image based or address: Address in instruction', N'DE1 1NN', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/O KMR Law Ltd' + NCHAR(13) + NCHAR(10) + N'221 Withington Road' + NCHAR(13) + NCHAR(10) + N'Manchester', NULL, NULL, '00000000-0000-4000-8000-00000000D016', N'KMR Law Ltd', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Street Cars - usually jobs are Streetrax Ltd jobs' + NCHAR(13) + NCHAR(10) + N'Images location: Images usually with instruction' + NCHAR(13) + NCHAR(10) + N'Image based or address: Usually listed in instruction' + NCHAR(13) + NCHAR(10) + N'Street Cars, 16 Chorlton Street, M1 3HW (Carpark - buildings in background - city centre)' + NCHAR(13) + NCHAR(10) + N'Storage Yard - Winders Way Salford M6 6BU or' + NCHAR(13) + NCHAR(10) + N'Unit 7 Sedgley Park Trading Estate, Prestwich, Manchester M25 9WD' + NCHAR(13) + NCHAR(10) + N'(Should say in KMR instruction)' + NCHAR(13) + NCHAR(10) + N'If images are obviously Manchester Airport, then go image based' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separate return to file handler' + NCHAR(13) + NCHAR(10) + NCHAR(13) + NCHAR(10) + N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Usually have everything with instruction - if not have KMR on whats app' + NCHAR(13) + NCHAR(10) + N'Images location: Images with email' + NCHAR(13) + NCHAR(10) + N'Image based or address: Storage - Unit 3 Broughton Road East, Salford Ind Estate, M6 6AQ' + NCHAR(13) + NCHAR(10) + N'16 Chorlton Street, Manchester, M1 3HW' + NCHAR(13) + NCHAR(10) + N'Sending Report: Send back with fee note separate', N'M16 8LU', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/O LPS Solicitors' + NCHAR(13) + NCHAR(10) + N'Alpha House' + NCHAR(13) + NCHAR(10) + N'2 Coop Place', NULL, NULL, '00000000-0000-4000-8000-00000000D02B', N'LPS Solicitors', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Don''t do many jobs for them - jobs come via Omar' + NCHAR(13) + NCHAR(10) + N'Images location: Omar WhatsApp chat' + NCHAR(13) + NCHAR(10) + N'Image based or address: Storage - Berkstone Motors RG10 9UR' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separately. Send Omar a copy via Whatsapp', N'BD5 8JX', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/O Lex Solicitors' + NCHAR(13) + NCHAR(10) + N'6 St Mark''s Street' + NCHAR(13) + NCHAR(10) + N'Bolton', NULL, NULL, '00000000-0000-4000-8000-00000000D02A', N'LEX Solicitors', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: All jobs come from Hackney Solutions' + NCHAR(13) + NCHAR(10) + N'Images location: Hackney Solutions' + NCHAR(13) + NCHAR(10) + N'Image based or address: Storage' + NCHAR(13) + NCHAR(10) + N'HS Recovery & Storage LTD William St M12 5FX most of the time but maybe Green Street Storage Yard, Burnley - just type that in' + NCHAR(13) + NCHAR(10) + N'Not in Storage' + NCHAR(13) + NCHAR(10) + N'Claim 3000 M12 4AH (Check if it looks like outside office)' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note does not need to be separate but copy in credithire@hackneysolutions.co.uk', N'BL3 6NR', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/O Matt Rowland Solicitors' + NCHAR(13) + NCHAR(10) + N'Spa Road' + NCHAR(13) + NCHAR(10) + N'Bolton', NULL, NULL, '00000000-0000-4000-8000-00000000D02C', N'Matt Rowland Solicitors', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Letter of instruction by email' + NCHAR(13) + NCHAR(10) + N'Images location: Castle accident management whatsapp' + NCHAR(13) + NCHAR(10) + N'Image based or address: Image-based' + NCHAR(13) + NCHAR(10) + N'Sending Report: Check with Castle first if they are ok for us to disclose report before sending to solicitors. Also send fee separate', N'BL1 4AG', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/O Motor X Assistance Ltd' + NCHAR(13) + NCHAR(10) + N'300 Biscot Road' + NCHAR(13) + NCHAR(10) + N'Luton', NULL, NULL, '00000000-0000-4000-8000-00000000D018', N'Motor X Assistance Ltd', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Do not message customer for images. Whatsapp R1AM/MOTORX' + NCHAR(13) + NCHAR(10) + N'Images location: R1AM- whats app group' + NCHAR(13) + NCHAR(10) + N'Image based or address: Clients address' + NCHAR(13) + NCHAR(10) + N'300 Biscot Road, Luton, LU3 1AZ (a terraced parade of shops)' + NCHAR(13) + NCHAR(10) + N'47-49 Park Street, Luton, LU1 3JX (Carpark at the back)' + NCHAR(13) + NCHAR(10) + N'(Ask them if can''t work it out)' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note does not need to be separate', N'LU3 1AZ', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/O Relay Motor Group' + NCHAR(13) + NCHAR(10) + N'47 Chippenham Road' + NCHAR(13) + NCHAR(10) + N'London W9 2AH', NULL, NULL, '00000000-0000-4000-8000-00000000D02F', N'Relay Motor Group', N'Inbox: Whatsapp' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: No instructions - Customers name and accident date' + NCHAR(13) + NCHAR(10) + N'Images location: Whatsapp Relay' + NCHAR(13) + NCHAR(10) + N'Image based or address: Image-based', NULL, N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/O Six Ways Marketing Ltd' + NCHAR(13) + NCHAR(10) + N'390 Coventry Road' + NCHAR(13) + NCHAR(10) + N'Birmingham', NULL, NULL, '00000000-0000-4000-8000-00000000D019', N'Six Ways Marketing Ltd', N'Inbox: Engineers', N'B10 0UF', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/O Swan Solicitors' + NCHAR(13) + NCHAR(10) + N'20 Swan Street' + NCHAR(13) + NCHAR(10) + N'Manchester', NULL, NULL, '00000000-0000-4000-8000-00000000D01C', N'Swan Solicitors', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Claim Specialists or Accident Specialist' + NCHAR(13) + NCHAR(10) + N'Images location: Claim Specialists - images in whats app claim specialist group - most of the time will have images before the instruction - they will confirm address when images provided' + NCHAR(13) + NCHAR(10) + N'Image based or address: Confirm with sources. Claims Specialists HQ is LU1 1BW' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separate return to file handler', N'M4 5JW', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/O Ten Legal' + NCHAR(13) + NCHAR(10) + N'3 Manchester Road' + NCHAR(13) + NCHAR(10) + N'Bury', NULL, NULL, '00000000-0000-4000-8000-00000000D01D', N'Ten Legal', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Sometimes images go to Andy on Whats App - if he has no record use number in instruction to request images' + NCHAR(13) + NCHAR(10) + N'Images location: Andy whats app or client direct' + NCHAR(13) + NCHAR(10) + N'Image based or address: Ask whoever provides images to confirm' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separate return to file handler', N'BL9 0DR', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/O Woodlands Solicitors' + NCHAR(13) + NCHAR(10) + N'235 Bury New Road' + NCHAR(13) + NCHAR(10) + N'Whitefield', NULL, NULL, '00000000-0000-4000-8000-00000000D020', N'Woodlands Solicitors', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Complete Injury Claims' + NCHAR(13) + NCHAR(10) + N'Images location: Complete Injury Claims - Whats App group or direct from Waseem - complete injury claims' + NCHAR(13) + NCHAR(10) + N'Direct - request direct from client if solicitors don''t send images' + NCHAR(13) + NCHAR(10) + N'Image based or address: Confirm location with whoever sends images' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separate send back to handler if it is a complete injury claims job copy in completeinjuryclaims@hotmail.com', N'M45 8QP', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/O Zenith Lawyers' + NCHAR(13) + NCHAR(10) + N'2 Strawberry Bank' + NCHAR(13) + NCHAR(10) + N'Blackburn', NULL, NULL, '00000000-0000-4000-8000-00000000D034', N'Zenith Lawyers', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Images location: Foyez on WhatsApp - confirm location, may be Road Solutions Yard 2 Kiln Lane B''ham B25 8HF' + NCHAR(13) + NCHAR(10) + N'Image based or address: Inspection address' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separately', N'BB2 6AA', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/o Accident Specialists' + NCHAR(13) + NCHAR(10) + N'1-3 Brighton Road' + NCHAR(13) + NCHAR(10) + N'Crawley', NULL, NULL, '00000000-0000-4000-8000-00000000D011', N'Accident Specialists', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Direct they will confirm if in Storage or not - check board' + NCHAR(13) + NCHAR(10) + N'Images location: Accident Specialist group or request from client' + NCHAR(13) + NCHAR(10) + N'Image based or address: Storage - RH10 9NT (Priestley Way Crawley)' + NCHAR(13) + NCHAR(10) + N'Office RH10 6AE' + NCHAR(13) + NCHAR(10) + N'Direct - ask whoever provides images to confirm' + NCHAR(13) + NCHAR(10) + N'Sending Report: Return to file handler copy in admin@accidentspecialist.co.uk', N'RH10 6AE', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/o Affinity Seven Law' + NCHAR(13) + NCHAR(10) + N'140 Drake Street' + NCHAR(13) + NCHAR(10) + N'Rochdale', NULL, NULL, '00000000-0000-4000-8000-00000000D022', N'Affinity Seven Law Solicitors', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Word doc in Email' + NCHAR(13) + NCHAR(10) + N'Images location: Customer Whatsapp', N'OL16 1PS', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/o Aman Solicitors' + NCHAR(13) + NCHAR(10) + N'37 Alum Rock Road' + NCHAR(13) + NCHAR(10) + N'Birmingham', NULL, NULL, '00000000-0000-4000-8000-00000000D024', N'Aman Solicitors Advocates', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Instructions in email' + NCHAR(13) + NCHAR(10) + N'Images location: Details should be in the email' + NCHAR(13) + NCHAR(10) + N'Image based or address: Depends on quality/quantity - image based', N'B8 1LR', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/o Avisons Solicitors' + NCHAR(13) + NCHAR(10) + N'Fountain House' + NCHAR(13) + NCHAR(10) + N'4 South Parade', NULL, NULL, '00000000-0000-4000-8000-00000000D025', N'Avisons Solicitors', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: PDF/Word doc in email' + NCHAR(13) + NCHAR(10) + N'Images location: Image Based - images on separate email' + NCHAR(13) + NCHAR(10) + N'Image based or address: Image-based', N'LS1 5QX', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/o Castle Accident Management' + NCHAR(13) + NCHAR(10) + N'363 Commercial Road' + NCHAR(13) + NCHAR(10) + N'London', NULL, NULL, '00000000-0000-4000-8000-00000000D027', N'Castle', N'Inbox: Whatsapp' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Always received in Whatsapp' + NCHAR(13) + NCHAR(10) + N'Images location: Castle Accident Management - Whats App' + NCHAR(13) + NCHAR(10) + N'Image based or address: Image-based' + NCHAR(13) + NCHAR(10) + N'Sending Report: Send back to Castle on Whats app group', N'E12PS', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/o Graham Coffey & Co' + NCHAR(13) + NCHAR(10) + N'1st Floor, Faulkner House' + NCHAR(13) + NCHAR(10) + N'Faulkner Street', NULL, NULL, '00000000-0000-4000-8000-00000000D029', N'Graham Coffey', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Graham Coffey' + NCHAR(13) + NCHAR(10) + N'Images location: Direct - request images and inspection location from Client (occasionaly solicitors will send)' + NCHAR(13) + NCHAR(10) + N'Image based or address: Ask whoever sends images for the address if set from Solicitors are not good enough request from client' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separate' + NCHAR(13) + NCHAR(10) + NCHAR(13) + NCHAR(10) + N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Raja /Fraz' + NCHAR(13) + NCHAR(10) + N'Images location: Fraz - Request images from Fraz group (whats app) - most the time we have images from Fraz or Raja prior to instruction' + NCHAR(13) + NCHAR(10) + N'Image based or address: Swade Storage - Higginshaw Lane OL2 6HW (They should tell us what storage address)' + NCHAR(13) + NCHAR(10) + N'Britannia Street, Oldham, OL1 3QR' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separate and copy in ontrack789@gmail.com', N'M1 4DY', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/o Kerr Brown Partnership' + NCHAR(13) + NCHAR(10) + N'50 Wellington Street' + NCHAR(13) + NCHAR(10) + N'Glasgow', NULL, NULL, '00000000-0000-4000-8000-00000000D015', N'Kerr Brown Partnership', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: In body of email' + NCHAR(13) + NCHAR(10) + N'Images location: Varies' + NCHAR(13) + NCHAR(10) + N'Image based or address: Varies on quality of images', N'G26HJ', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/o MBH Solicitors' + NCHAR(13) + NCHAR(10) + N'26 Bridgeman Terrace' + NCHAR(13) + NCHAR(10) + N'Wigan', NULL, NULL, '00000000-0000-4000-8000-00000000D02D', N'MBH Solicitors', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Most jobs come from Nabeel' + NCHAR(13) + NCHAR(10) + N'Images location: Nabeel- whats app group' + NCHAR(13) + NCHAR(10) + N'Image based or address: Storage - Parkers Autobodies, 4 Chapel Street, Manchester. M19 3QA or' + NCHAR(13) + NCHAR(10) + N'219 Slade Lane Manchester M19 2EX' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee does not need sending separately - copy in Alisonnorris@wigansolicitors.com', N'WN11TD', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/o ROZZII Ltd' + NCHAR(13) + NCHAR(10) + N'386 Park Road' + NCHAR(13) + NCHAR(10) + N'Birmingham', NULL, NULL, '00000000-0000-4000-8000-00000000D030', N'ROZZII/Green Destinations', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Images with instruction' + NCHAR(13) + NCHAR(10) + N'Images location: With instruction' + NCHAR(13) + NCHAR(10) + N'Image based or address: Inspection address listed in report if ok if not image based?' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separately', N'B185ST', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/o Regent Law' + NCHAR(13) + NCHAR(10) + N'Suite 19, Freckleton Business Centre' + NCHAR(13) + NCHAR(10) + N'Freckleton Street', NULL, NULL, '00000000-0000-4000-8000-00000000D02E', N'Regent Law Ltd', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Images location: Usually for Bicycles - will come with phone number for storage company (I have mobile number in my phone so text Saraj - Supreme Drive 07483371748) - he will provide images and bike info' + NCHAR(13) + NCHAR(10) + N'Image based or address: Confirm with whoever provides images' + NCHAR(13) + NCHAR(10) + N'Sending Report: Fee note separate return to file handler', N'BB2 2AL', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/o Savas & Savage Solicitors Ltd' + NCHAR(13) + NCHAR(10) + N'20 Stanney Lane' + NCHAR(13) + NCHAR(10) + N'Ellesmere Port', NULL, NULL, '00000000-0000-4000-8000-00000000D01A', N'Savas & Savage Solicitors Ltd', N'Inbox: Andrew' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Instructions come via Andy' + NCHAR(13) + NCHAR(10) + N'Images location: Instruction will come from Andy usually with images sometimes we contact client' + NCHAR(13) + NCHAR(10) + N'Image based or address: Inspection address - if instruction from Andy he will advise if images from client we will need to ask them.' + NCHAR(13) + NCHAR(10) + N'Sending Report: Email back to solicitors and copy in mw@savasandsavage.co.uk', N'CH65 9AD', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/o Taylor Price Solicitors' + NCHAR(13) + NCHAR(10) + N'Unit 5, 173 Cheetham Hill Road' + NCHAR(13) + NCHAR(10) + N'Manchester', NULL, NULL, '00000000-0000-4000-8000-00000000D01E', N'Taylor Price Solicitors', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: PDF in email' + NCHAR(13) + NCHAR(10) + N'Images location: Contained in email', N'M8 8LG', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/o Turnamms Assessors' + NCHAR(13) + NCHAR(10) + N'34 Claredon Road' + NCHAR(13) + NCHAR(10) + N'Watford', NULL, NULL, '00000000-0000-4000-8000-00000000D032', N'Turnams', N'Inbox: Andrew' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Go direct to Andy usually image based but Andy will tell you' + NCHAR(13) + NCHAR(10) + N'Images location: Image based - images with email' + NCHAR(13) + NCHAR(10) + N'Image based or address: Image Based - most of time' + NCHAR(13) + NCHAR(10) + N'Sending Report: Send back with fee note separate', N'WD17 1JA', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/o Williams & Co' + NCHAR(13) + NCHAR(10) + N'Scott House, 115-117 Bolton Road' + NCHAR(13) + NCHAR(10) + N'Wigan', NULL, NULL, '00000000-0000-4000-8000-00000000D033', N'Williams & Co', N'Inbox: Andy to Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Word doc in Email' + NCHAR(13) + NCHAR(10) + N'Images location: Requested from claimant via Whatsapp' + NCHAR(13) + NCHAR(10) + N'Image based or address: Address', N'WN4 8AD', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'Plot B1' + NCHAR(13) + NCHAR(10) + N'Higginshaw Lane' + NCHAR(13) + NCHAR(10) + N'Oldham', NULL, NULL, '00000000-0000-4000-8000-00000000D01B', N'Swade Solutions Ltd', N'Inbox: Engineers', N'OL13LA', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'Suite 4 Grove Business Centre' + NCHAR(13) + NCHAR(10) + N'Great Horton Road' + NCHAR(13) + NCHAR(10) + N'Bradforf', NULL, NULL, '00000000-0000-4000-8000-00000000D021', N'Abrahams Solicitors', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: Attached to email', N'BD7 1AH', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'Walker Prestons' + NCHAR(13) + NCHAR(10) + N'Mount Pleasant' + NCHAR(13) + NCHAR(10) + N'Trinity Street', NULL, NULL, '00000000-0000-4000-8000-00000000D01F', N'Walker Prestons', N'Inbox: Engineers', N'BB1 5BN', N'principal', NULL, 0);
                    INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'Wentworth Building' + NCHAR(13) + NCHAR(10) + N'1b Fairways Office Park' + NCHAR(13) + NCHAR(10) + N'Pittman Way', NULL, NULL, '00000000-0000-4000-8000-00000000D010', N'AMS Solicitors', N'Inbox: Engineers' + NCHAR(13) + NCHAR(10) + N'Solicitors Instructions: PDF in email' + NCHAR(13) + NCHAR(10) + N'Images location: Raja or Ontrack' + NCHAR(13) + NCHAR(10) + N'Image based or address: If Higginshaw Lane - then OL2 6HW', N'PR2 9LF', N'principal', NULL, 0);
                    """);
                migrationBuilder.Sql("""
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E001');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E002');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E003');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E004');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E005');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E006');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E007');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E008');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E009');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E00A');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E00B');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E00C');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E00D');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E00E');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E00F');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E010');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E011');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E012');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E013');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E014');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E015');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E016');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E017');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E018');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E019');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E01A');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E01B');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E01C');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E01D');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E01E');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E01F');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E020');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E021');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E022');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E023');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E024');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E025');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E026');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E027');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E028');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E029');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E02A');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E02B');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E02C');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E02D');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E02E');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E02F');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E030');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E031');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E032');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E033');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E034');
                    INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-10-28T17:19:19+00:00', '00000000-0000-4000-8000-00000000E035');
                    """);
                migrationBuilder.Sql("""
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'ABRAHAMS', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C021', N'physical_address', 1, '00000000-0000-4000-8000-00000000D021', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E021', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'ACSP', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C011', N'physical_address', 1, '00000000-0000-4000-8000-00000000D011', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["admin@accidentspecialist.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E011', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'ALISON', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C012', N'physical_address', 1, '00000000-0000-4000-8000-00000000D012', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E012', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'ALL', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C023', N'physical_address', 1, '00000000-0000-4000-8000-00000000D023', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E023', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'ALS', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C00E', N'physical_address', 1, '00000000-0000-4000-8000-00000000D00E', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["claims@autologistic.co.uk","p.mandy@oakwoodscotland.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":true,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":true,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E00E', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'AMS', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C010', N'physical_address', 1, '00000000-0000-4000-8000-00000000D010', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E010', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'ASLS', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C022', N'physical_address', 1, '00000000-0000-4000-8000-00000000D022', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E022', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'AS', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C024', N'physical_address', 1, '00000000-0000-4000-8000-00000000D024', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E024', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'AVI', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C025', N'physical_address', 1, '00000000-0000-4000-8000-00000000D025', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E025', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'AX', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C003', N'physical_address', 1, '00000000-0000-4000-8000-00000000D003', NULL, N'{"sendTo":["engineersreports@ax-uk.com","engineersinspections@ax-uk.com"],"sendToOnly":false,"replyAll":true,"cc":["p.mandy@oakwoodscotland.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":true,"reminders":[],"rules":[{"match":"All","if":[{"kind":"BodyshopMentions","values":["Easdons"]},{"kind":"BodyshopMentions","values":["Guardian","James Claims"]}],"then":{"ccAdd":[],"ccRemove":["p.mandy@oakwoodscotland.co.uk"]}}]}', NULL, '00000000-0000-4000-8000-00000000E003', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'BAKER', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C026', N'physical_address', 1, '00000000-0000-4000-8000-00000000D026', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E026', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'BC', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C00F', N'physical_address', 1, '00000000-0000-4000-8000-00000000D00F', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["a.nawaz@bakercoleman.co.uk","h.ahmed@bakercoleman.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E00F', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'BLACK', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C008', N'physical_address', 1, '00000000-0000-4000-8000-00000000D008', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":true,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E008', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'CASTLE', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C027', N'physical_address', 1, '00000000-0000-4000-8000-00000000D027', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E027', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'CS', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C013', N'physical_address', 1, '00000000-0000-4000-8000-00000000D013', NULL, N'{"sendFromMailbox":"engineers@collisionengineers.co.uk","sendTo":["info@claimsspecialists.co.uk","accounts@claimsspecialists.co.uk"],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E013', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'CW', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C028', N'physical_address', 1, '00000000-0000-4000-8000-00000000D028', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E028', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'DFD', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C00A', N'physical_address', 1, '00000000-0000-4000-8000-00000000D00A', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["richard.pownall@dfd-solicitors.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[{"match":"All","if":[{"kind":"ClaimSource","values":["00000000-0000-4000-8000-00000000f001"]}],"then":{"ccAdd":["info@car2go-nw.co.uk"],"ccRemove":[]}},{"match":"All","if":[{"kind":"ClaimSource","values":["00000000-0000-4000-8000-00000000f002"]}],"then":{"ccAdd":["tony@shaunmarnell.co.uk"],"ccRemove":[]}},{"match":"All","if":[{"kind":"ClaimSource","values":["00000000-0000-4000-8000-00000000f003"]}],"then":{"ccAdd":[],"ccRemove":[],"stop":"CarClaims job. This needs checking with Andy before it is sent."}}]}', NULL, '00000000-0000-4000-8000-00000000E00A', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'FRZ', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C035', N'physical_address', 0, '00000000-0000-4000-8000-00000000D035', NULL, N'{"sendTo":["Ontrack789@gmail.com"],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":["WhatsApp Swade to say the report has been sent."],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E035', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'FW', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C004', N'physical_address', 1, '00000000-0000-4000-8000-00000000D004', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["info@fairwaylegal.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E004', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'GG', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C029', N'physical_address', 1, '00000000-0000-4000-8000-00000000D029', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E029', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'HTU', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C014', N'physical_address', 1, '00000000-0000-4000-8000-00000000D014', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E014', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'KBS', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C00B', N'physical_address', 1, '00000000-0000-4000-8000-00000000D00B', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["info@knightsbridgesolicitors.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[{"match":"All","if":[{"kind":"ClaimSource","values":["00000000-0000-4000-8000-00000000f004"]}],"then":{"ccAdd":["gabbas@knightsbridgesolicitors.co.uk"],"ccRemove":[]}},{"match":"All","if":[{"kind":"ClaimSource","values":["00000000-0000-4000-8000-00000000f005"]}],"then":{"ccAdd":["Rapidlongton@gmail.com"],"ccRemove":[]}}]}', NULL, '00000000-0000-4000-8000-00000000E00B', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'KERR', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C015', N'physical_address', 1, '00000000-0000-4000-8000-00000000D015', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E015', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'KMR', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C016', N'physical_address', 1, '00000000-0000-4000-8000-00000000D016', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["info@kmrlaw.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E016', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'LEX', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C02A', N'physical_address', 1, '00000000-0000-4000-8000-00000000D02A', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E02A', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'LPS', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C02B', N'physical_address', 1, '00000000-0000-4000-8000-00000000D02B', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E02B', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'MATT', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C02C', N'physical_address', 1, '00000000-0000-4000-8000-00000000D02C', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E02C', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'MBH', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C02D', N'physical_address', 1, '00000000-0000-4000-8000-00000000D02D', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E02D', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'MIDAS', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C017', N'physical_address', 1, '00000000-0000-4000-8000-00000000D017', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"hold":"WhatsApp the report to Midas to check before it is sent.","reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E017', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'MOTORX', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C018', N'physical_address', 1, '00000000-0000-4000-8000-00000000D018', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E018', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'MP', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C00C', N'physical_address', 1, '00000000-0000-4000-8000-00000000D00C', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["mdakri@montrealprestige.co.uk","info@montrealprestige.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":false,"vehicleImagesDocument":true,"figureBreakdown":true},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E00C', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'OAK', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C006', N'physical_address', 1, '00000000-0000-4000-8000-00000000D006', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["p.mandy@oakwoodscotland.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":true,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E006', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'PCH', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C002', N'physical_address', 1, '00000000-0000-4000-8000-00000000D002', NULL, N'{"sendTo":["SBL@connexus.co.uk"],"sendToOnly":false,"replyAll":false,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":true},"garageFigures":false,"reminders":[],"rules":[{"match":"All","if":[{"kind":"ClaimSource","values":["00000000-0000-4000-8000-00000000f00b"]},{"kind":"Outcome","values":["repairable"]}],"then":{"ccAdd":["repairs@irevolutionrepairnetwork.co.uk"],"ccRemove":[],"remind":"Authorise the garage","rateCard":true}},{"match":"Any","if":[{"kind":"Mentions","values":["audit"]},{"kind":"RefPrefix","values":["a"]},{"kind":"PreviousJob","values":[]}],"then":{"ccAdd":[],"ccRemove":["repairs@irevolutionrepairnetwork.co.uk"]}}],"attachmentName":{"first":"{reg} Initial","resend":"{reg} Supplementary"}}', NULL, '00000000-0000-4000-8000-00000000E002', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'QCL', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C005', N'physical_address', 1, '00000000-0000-4000-8000-00000000D005', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":["credithire@hackneysolutions.co.uk"],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E005', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'QDOS', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C001', N'image_based_assessment', 1, '00000000-0000-4000-8000-00000000D001', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["engineers@qdosassist.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":true,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E001', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'RELAY', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C02F', N'physical_address', 1, '00000000-0000-4000-8000-00000000D02F', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E02F', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'RJS', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C009', N'physical_address', 1, '00000000-0000-4000-8000-00000000D009', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["engreport@robertjameslaw.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[{"match":"Any","if":[{"kind":"Mentions","values":["Luton"]},{"kind":"Mentions","values":["Claim Specialist","Claims Specialist","claimsspecialists.co.uk"]}],"then":{"ccAdd":["accounts@claimsspecialists.co.uk"],"ccRemove":[]}},{"match":"All","if":[{"kind":"Mentions","values":["Accident Specialist"]}],"then":{"ccAdd":["admin@accidentspecialist.co.uk"],"ccRemove":[]}}]}', NULL, '00000000-0000-4000-8000-00000000E009', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'RL', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C02E', N'physical_address', 1, '00000000-0000-4000-8000-00000000D02E', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E02E', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'ROZZII', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C030', N'physical_address', 1, '00000000-0000-4000-8000-00000000D030', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E030', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'SBL', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C007', N'physical_address', 1, '00000000-0000-4000-8000-00000000D007', NULL, N'{"sendTo":["Claims@smartbusinesslink.com"],"sendToOnly":false,"replyAll":true,"cc":["SBL@connexus.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[{"match":"All","if":[{"kind":"Outcome","values":["repairable"]}],"then":{"ccAdd":[],"ccRemove":[],"remind":"Check with the engineer that the garage has been authorised."}}]}', NULL, '00000000-0000-4000-8000-00000000E007', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'SIX', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C019', N'physical_address', 1, '00000000-0000-4000-8000-00000000D019', NULL, N'{"sendTo":["info@sixwaysclaims.co.uk"],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E019', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'SS', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C01A', N'physical_address', 1, '00000000-0000-4000-8000-00000000D01A', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E01A', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'STALLION', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C031', N'physical_address', 1, '00000000-0000-4000-8000-00000000D031', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E031', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'SWADE', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C01B', N'physical_address', 1, '00000000-0000-4000-8000-00000000D01B', NULL, N'{"sendTo":["Ontrack789@gmail.com"],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":["WhatsApp Swade to say the report has been sent."],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E01B', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'SWAN', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C01C', N'physical_address', 1, '00000000-0000-4000-8000-00000000D01C', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E01C', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'TA', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C032', N'physical_address', 1, '00000000-0000-4000-8000-00000000D032', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E032', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'TEN', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C01D', N'physical_address', 1, '00000000-0000-4000-8000-00000000D01D', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E01D', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'TP', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C01E', N'physical_address', 1, '00000000-0000-4000-8000-00000000D01E', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["info@taylorprice.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E01E', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'WALKER', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C01F', N'physical_address', 1, '00000000-0000-4000-8000-00000000D01F', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E01F', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'WIL', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C033', N'physical_address', 1, '00000000-0000-4000-8000-00000000D033', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E033', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'WLS', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C020', N'physical_address', 1, '00000000-0000-4000-8000-00000000D020', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E020', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'YML', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C00D', N'physical_address', 1, '00000000-0000-4000-8000-00000000D00D', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[{"match":"All","if":[{"kind":"ImagesFrom","values":["Swinton"]}],"then":{"ccAdd":[],"ccRemove":[],"remind":"send Swinton a copy of the report on WhatsApp."}}]}', NULL, '00000000-0000-4000-8000-00000000E00D', NULL, 0);
                    INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'ZEN', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '00000000-0000-4000-8000-00000000C034', N'physical_address', 1, '00000000-0000-4000-8000-00000000D034', NULL, N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '00000000-0000-4000-8000-00000000E034', NULL, 0);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("The Baseline is the first migration; there is no earlier schema to return to.");
    }
}

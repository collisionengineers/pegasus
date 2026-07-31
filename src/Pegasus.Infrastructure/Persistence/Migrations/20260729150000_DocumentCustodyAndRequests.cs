using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentCustodyAndRequests : Migration
    {
        private static readonly string[] BoxFileRequestCreateOperationIndexColumns = ["CaseId", "CreateOperationKey"];
        private static readonly string[] CaseDocumentSourceOccurrenceIndexColumns = ["CaseId", "SourceOccurrenceIdentity"];
        private static readonly string[] CaseHistoryOccurredAtIndexColumns = ["CaseId", "OccurredAtUtc"];
        private static readonly string[] CaseSequenceIndexColumns = ["SequenceLineageId", "Year", "Sequence"];
        private static readonly string[] DocumentOccurrenceDocumentIndexColumns = ["CaseId", "DocumentId"];
        private static readonly string[] DocumentOccurrenceOperationIndexColumns = ["CaseId", "OperationKey"];
        private static readonly string[] DocumentVersionIndexColumns = ["DocumentId", "Version"];
        private static readonly string[] ExternalWorkItemDueIndexColumns = ["State", "DueAtUtc"];
        private static readonly string[] IntakeEvaluationRevisionIndexColumns = ["StagedReceiptId", "Revision"];
        private static readonly string[] IntakeStagedReceiptSourceIdentityIndexColumns = ["SourceChannel", "ExternalReceiptToken"];
        private static readonly string[] IntakeWorkItemDueIndexColumns = ["State", "DueAtUtc"];
        private static readonly string[] OpenIddictAuthorizationLookupIndexColumns = ["ApplicationId", "Status", "Subject", "Type"];
        private static readonly string[] OpenIddictTokenLookupIndexColumns = ["ApplicationId", "Status", "Subject", "Type"];
        private static readonly string[] RequestUploadLinkCreateOperationIndexColumns = ["CaseId", "CreateOperationKey"];
        private static readonly string[] RequestUploadReceiptOperationIndexColumns = ["RequestId", "OperationKey"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "IntakeReceipts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "ApplicationInitializations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ManifestSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    MigrationId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationInitializations", x => x.Id);
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
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
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
                    StagedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeStagedReceipts", x => x.Id);
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
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                    table.CheckConstraint("CK_Organizations_Name", "[Name] <> ''");
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
                name: "OrganizationRoles",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationRoles", x => new { x.OrganizationId, x.Role });
                    table.CheckConstraint("CK_OrganizationRoles_Role", "[Role] IN ('work_provider', 'instruction_intermediary')");
                    table.ForeignKey(
                        name: "FK_OrganizationRoles_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    table.CheckConstraint("CK_CaseSequences_LastAllocatedSequence", "[LastAllocatedSequence] >= 0 AND [LastAllocatedSequence] <= 999");
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
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Principals", x => x.Id);
                    table.CheckConstraint("CK_Principals_Code", "[Code] <> ''");
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
                    InitialState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CustodyState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OriginIntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StandaloneAuditAssessment = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    InstructionComplete = table.Column<bool>(type: "bit", nullable: false),
                    ImagesComplete = table.Column<bool>(type: "bit", nullable: false),
                    InstructionConfirmedByStaff = table.Column<bool>(type: "bit", nullable: false),
                    ImagesConfirmedByStaff = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustodyRootRemoteId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustodySourceRemoteId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustodySourceContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CustodySourceETag = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustodyConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cases", x => x.Id);
                    table.CheckConstraint("CK_Cases_Sequence", "[Sequence] >= 1 AND [Sequence] <= 999");
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
                });

            migrationBuilder.CreateTable(
                name: "BoxFileRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeactivatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreateOperationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RevokeOperationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LinkTokenDigest = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoxFileRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoxFileRequests_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceOccurrenceIdentity = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseDocuments", x => x.Id);
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
                name: "ExternalWorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                });

            migrationBuilder.CreateTable(
                name: "RequestUploadLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenDigest = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcceptedFileCount = table.Column<int>(type: "int", nullable: false),
                    AcceptedByteCount = table.Column<long>(type: "bigint", nullable: false),
                    LimitsVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreateOperationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RevokeOperationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestUploadLinks", x => x.Id);
                    table.CheckConstraint("CK_RequestUploadLinks_AcceptedByteCount", "[AcceptedByteCount] >= 0");
                    table.CheckConstraint("CK_RequestUploadLinks_AcceptedFileCount", "[AcceptedFileCount] >= 0");
                    table.ForeignKey(
                        name: "FK_RequestUploadLinks_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
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
                    CustodyStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    IsLogicallyRemoved = table.Column<bool>(type: "bit", nullable: false),
                    RemovalReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RemovalOperationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
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
                name: "CaseIntakeLinks",
                columns: table => new
                {
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustodyWorkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
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
                name: "DocumentOccurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SemanticRole = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceOccurrenceIdentity = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentOccurrences", x => x.Id);
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
                name: "RequestUploadReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurrenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContentHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestUploadReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestUploadReceipts_DocumentOccurrences_OccurrenceId",
                        column: x => x.OccurrenceId,
                        principalTable: "DocumentOccurrences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestUploadReceipts_DocumentVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestUploadReceipts_RequestUploadLinks_RequestId",
                        column: x => x.RequestId,
                        principalTable: "RequestUploadLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BoxFileRequests_CaseId_CreateOperationKey",
                table: "BoxFileRequests",
                columns: BoxFileRequestCreateOperationIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoxFileRequests_LinkTokenDigest",
                table: "BoxFileRequests",
                column: "LinkTokenDigest",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseDocuments_CaseId_SourceOccurrenceIdentity",
                table: "CaseDocuments",
                columns: CaseDocumentSourceOccurrenceIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseHistory_CaseId_OccurredAtUtc",
                table: "CaseHistory",
                columns: CaseHistoryOccurredAtIndexColumns);

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
                name: "IX_Cases_AuditReference",
                table: "Cases",
                column: "AuditReference",
                unique: true,
                filter: "[AuditReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_OriginIntakeReceiptId",
                table: "Cases",
                column: "OriginIntakeReceiptId",
                unique: true);

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
                columns: CaseSequenceIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_CaseId_DocumentId",
                table: "DocumentOccurrences",
                columns: DocumentOccurrenceDocumentIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_CaseId_OperationKey",
                table: "DocumentOccurrences",
                columns: DocumentOccurrenceOperationIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_DocumentId",
                table: "DocumentOccurrences",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_VersionId",
                table: "DocumentOccurrences",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_DocumentId_Version",
                table: "DocumentVersions",
                columns: DocumentVersionIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorkItems_CaseId",
                table: "ExternalWorkItems",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorkItems_OperationKey",
                table: "ExternalWorkItems",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorkItems_State_DueAtUtc",
                table: "ExternalWorkItems",
                columns: ExternalWorkItemDueIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeEvaluations_StagedReceiptId_Revision",
                table: "IntakeEvaluations",
                columns: IntakeEvaluationRevisionIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeStagedReceipts_SourceChannel_ExternalReceiptToken",
                table: "IntakeStagedReceipts",
                columns: IntakeStagedReceiptSourceIdentityIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeStagedReceipts_SourceHash",
                table: "IntakeStagedReceipts",
                column: "SourceHash");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeWorkItems_OperationKey",
                table: "IntakeWorkItems",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeWorkItems_StagedReceiptId",
                table: "IntakeWorkItems",
                column: "StagedReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeWorkItems_State_DueAtUtc",
                table: "IntakeWorkItems",
                columns: IntakeWorkItemDueIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictApplications_ClientId",
                table: "OpenIddictApplications",
                column: "ClientId",
                unique: true,
                filter: "[ClientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type",
                table: "OpenIddictAuthorizations",
                columns: OpenIddictAuthorizationLookupIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictScopes_Name",
                table: "OpenIddictScopes",
                column: "Name",
                unique: true,
                filter: "[Name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type",
                table: "OpenIddictTokens",
                columns: OpenIddictTokenLookupIndexColumns);

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
                name: "IX_RequestUploadLinks_CaseId_CreateOperationKey",
                table: "RequestUploadLinks",
                columns: RequestUploadLinkCreateOperationIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestUploadLinks_TokenDigest",
                table: "RequestUploadLinks",
                column: "TokenDigest",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestUploadReceipts_OccurrenceId",
                table: "RequestUploadReceipts",
                column: "OccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestUploadReceipts_RequestId_OperationKey",
                table: "RequestUploadReceipts",
                columns: RequestUploadReceiptOperationIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestUploadReceipts_VersionId",
                table: "RequestUploadReceipts",
                column: "VersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationInitializations");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "BoxFileRequests");

            migrationBuilder.DropTable(
                name: "CaseHistory");

            migrationBuilder.DropTable(
                name: "CaseIntakeLinks");

            migrationBuilder.DropTable(
                name: "CaseSequences");

            migrationBuilder.DropTable(
                name: "IntakeEvaluations");

            migrationBuilder.DropTable(
                name: "IntakeWorkItems");

            migrationBuilder.DropTable(
                name: "OpenIddictScopes");

            migrationBuilder.DropTable(
                name: "OpenIddictTokens");

            migrationBuilder.DropTable(
                name: "OrganizationRoles");

            migrationBuilder.DropTable(
                name: "RequestUploadReceipts");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "ExternalWorkItems");

            migrationBuilder.DropTable(
                name: "IntakeStagedReceipts");

            migrationBuilder.DropTable(
                name: "OpenIddictAuthorizations");

            migrationBuilder.DropTable(
                name: "DocumentOccurrences");

            migrationBuilder.DropTable(
                name: "RequestUploadLinks");

            migrationBuilder.DropTable(
                name: "OpenIddictApplications");

            migrationBuilder.DropTable(
                name: "DocumentVersions");

            migrationBuilder.DropTable(
                name: "CaseDocuments");

            migrationBuilder.DropTable(
                name: "Cases");

            migrationBuilder.DropTable(
                name: "Principals");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropTable(
                name: "PrincipalSequenceLineages");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "IntakeReceipts");
        }
    }
}

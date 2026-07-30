using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffTriageCaseWorkspaceV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isSqlite = ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite";
            var GuidType = isSqlite ? "TEXT" : "uniqueidentifier";
            var IntegerType = isSqlite ? "INTEGER" : "int";
            var LongType = isSqlite ? "INTEGER" : "bigint";
            var BitType = isSqlite ? "INTEGER" : "bit";
            var TimestampType = isSqlite ? "TEXT" : "datetimeoffset";
            string TextType(string sqlServerType) => isSqlite ? "TEXT" : sqlServerType;
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: GuidType, nullable: false),
                    Name = table.Column<string>(type: TextType("nvarchar(256)"), maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: TextType("nvarchar(256)"), maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: TextType("nvarchar(max)"), nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: GuidType, nullable: false),
                    DisplayName = table.Column<string>(type: TextType("nvarchar(200)"), maxLength: 200, nullable: false),
                    DisabledAtUtc = table.Column<DateTimeOffset>(type: TimestampType, nullable: true),
                    ForcePasswordChange = table.Column<bool>(type: BitType, nullable: false),
                    Version = table.Column<long>(type: LongType, nullable: false),
                    UserName = table.Column<string>(type: TextType("nvarchar(256)"), maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: TextType("nvarchar(256)"), maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: TextType("nvarchar(256)"), maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: TextType("nvarchar(256)"), maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: BitType, nullable: false),
                    PasswordHash = table.Column<string>(type: TextType("nvarchar(max)"), nullable: true),
                    SecurityStamp = table.Column<string>(type: TextType("nvarchar(max)"), nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: TextType("nvarchar(max)"), nullable: true),
                    PhoneNumber = table.Column<string>(type: TextType("nvarchar(max)"), nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: BitType, nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: BitType, nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: TimestampType, nullable: true),
                    LockoutEnabled = table.Column<bool>(type: BitType, nullable: false),
                    AccessFailedCount = table.Column<int>(type: IntegerType, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: GuidType, nullable: false),
                    CaseId = table.Column<Guid>(type: GuidType, nullable: true),
                    TriageId = table.Column<Guid>(type: GuidType, nullable: true),
                    ActorKind = table.Column<string>(type: TextType("nvarchar(40)"), maxLength: 40, nullable: false),
                    ActorId = table.Column<Guid>(type: GuidType, nullable: false),
                    Caller = table.Column<string>(type: TextType("nvarchar(100)"), maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: TextType("nvarchar(100)"), maxLength: 100, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: TimestampType, nullable: false),
                    CorrelationId = table.Column<Guid>(type: GuidType, nullable: false),
                    BeforeJson = table.Column<string>(type: TextType("nvarchar(max)"), nullable: true),
                    AfterJson = table.Column<string>(type: TextType("nvarchar(max)"), nullable: true),
                    Outcome = table.Column<string>(type: TextType("nvarchar(40)"), maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: TextType("nvarchar(1000)"), maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaseLeases",
                columns: table => new
                {
                    CaseId = table.Column<Guid>(type: GuidType, nullable: false),
                    HolderId = table.Column<Guid>(type: GuidType, nullable: false),
                    HolderName = table.Column<string>(type: TextType("nvarchar(200)"), maxLength: 200, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AcquiredAtUtc = table.Column<DateTimeOffset>(type: TimestampType, nullable: false),
                    RenewedAtUtc = table.Column<DateTimeOffset>(type: TimestampType, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: TimestampType, nullable: false),
                    Version = table.Column<long>(type: LongType, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseLeases", x => x.CaseId);
                });

            migrationBuilder.CreateTable(
                name: "Cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: GuidType, nullable: false),
                    PrincipalCode = table.Column<string>(type: TextType("nvarchar(20)"), maxLength: 20, nullable: false),
                    BaseReference = table.Column<string>(type: TextType("nvarchar(40)"), maxLength: 40, nullable: false),
                    DisplayReference = table.Column<string>(type: TextType("nvarchar(60)"), maxLength: 60, nullable: false),
                    Type = table.Column<string>(type: TextType("nvarchar(40)"), maxLength: 40, nullable: false),
                    Registration = table.Column<string>(type: TextType("nvarchar(20)"), maxLength: 20, nullable: false),
                    SecondaryAuditReference = table.Column<string>(type: TextType("nvarchar(max)"), nullable: true),
                    Claimant = table.Column<string>(type: TextType("nvarchar(300)"), maxLength: 300, nullable: true),
                    ClaimNumber = table.Column<string>(type: TextType("nvarchar(100)"), maxLength: 100, nullable: true),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: TimestampType, nullable: false),
                    InstructionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Origin = table.Column<string>(type: TextType("nvarchar(max)"), nullable: true),
                    State = table.Column<string>(type: TextType("nvarchar(40)"), maxLength: 40, nullable: false),
                    IsHeld = table.Column<bool>(type: BitType, nullable: false),
                    NextDueAtUtc = table.Column<DateTimeOffset>(type: TimestampType, nullable: true),
                    DuePaused = table.Column<bool>(type: BitType, nullable: false),
                    ChaseCount = table.Column<int>(type: IntegerType, nullable: false),
                    EngineerId = table.Column<Guid>(type: GuidType, nullable: true),
                    EngineerName = table.Column<string>(type: TextType("nvarchar(max)"), nullable: true),
                    TerminalOutcome = table.Column<string>(type: TextType("nvarchar(60)"), maxLength: 60, nullable: true),
                    ReplacementCaseId = table.Column<Guid>(type: GuidType, nullable: true),
                    Version = table.Column<long>(type: LongType, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaseSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: GuidType, nullable: false),
                    PrincipalCode = table.Column<string>(type: TextType("nvarchar(20)"), maxLength: 20, nullable: false),
                    Year = table.Column<int>(type: IntegerType, nullable: false),
                    LastSequence = table.Column<int>(type: IntegerType, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseSequences", x => x.Id);
                    table.CheckConstraint("CK_CaseSequences_LastSequence", "[LastSequence] BETWEEN 0 AND 999");
                });

            migrationBuilder.CreateTable(
                name: "Triages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: GuidType, nullable: false),
                    SourceId = table.Column<Guid>(type: GuidType, nullable: false),
                    Registration = table.Column<string>(type: TextType("nvarchar(20)"), maxLength: 20, nullable: false),
                    AssigneeId = table.Column<Guid>(type: GuidType, nullable: true),
                    AssigneeName = table.Column<string>(type: TextType("nvarchar(max)"), nullable: true),
                    State = table.Column<string>(type: TextType("nvarchar(40)"), maxLength: 40, nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: TimestampType, nullable: false),
                    Version = table.Column<long>(type: LongType, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Triages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: IntegerType, nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: GuidType, nullable: false),
                    ClaimType = table.Column<string>(type: TextType("nvarchar(max)"), nullable: true),
                    ClaimValue = table.Column<string>(type: TextType("nvarchar(max)"), nullable: true)
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
                    Id = table.Column<int>(type: IntegerType, nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: GuidType, nullable: false),
                    ClaimType = table.Column<string>(type: TextType("nvarchar(max)"), nullable: true),
                    ClaimValue = table.Column<string>(type: TextType("nvarchar(max)"), nullable: true)
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
                    LoginProvider = table.Column<string>(type: TextType("nvarchar(450)"), nullable: false),
                    ProviderKey = table.Column<string>(type: TextType("nvarchar(450)"), nullable: false),
                    ProviderDisplayName = table.Column<string>(type: TextType("nvarchar(max)"), nullable: true),
                    UserId = table.Column<Guid>(type: GuidType, nullable: false)
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
                    UserId = table.Column<Guid>(type: GuidType, nullable: false),
                    RoleId = table.Column<Guid>(type: GuidType, nullable: false)
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
                    UserId = table.Column<Guid>(type: GuidType, nullable: false),
                    LoginProvider = table.Column<string>(type: TextType("nvarchar(450)"), nullable: false),
                    Name = table.Column<string>(type: TextType("nvarchar(450)"), nullable: false),
                    Value = table.Column<string>(type: TextType("nvarchar(max)"), nullable: true)
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
                name: "TriageCaseLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: GuidType, nullable: false),
                    TriageId = table.Column<Guid>(type: GuidType, nullable: false),
                    CaseId = table.Column<Guid>(type: GuidType, nullable: false),
                    LinkedAtUtc = table.Column<DateTimeOffset>(type: TimestampType, nullable: false),
                    UnlinkedAtUtc = table.Column<DateTimeOffset>(type: TimestampType, nullable: true),
                    Reason = table.Column<string>(type: TextType("nvarchar(1000)"), maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriageCaseLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriageCaseLinks_Triages_TriageId",
                        column: x => x.TriageId,
                        principalTable: "Triages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TriageFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: GuidType, nullable: false),
                    TriageId = table.Column<Guid>(type: GuidType, nullable: false),
                    Roadworthiness = table.Column<string>(type: TextType("nvarchar(40)"), maxLength: 40, nullable: true),
                    Assessment = table.Column<string>(type: TextType("nvarchar(40)"), maxLength: 40, nullable: true),
                    Reason = table.Column<string>(type: TextType("nvarchar(1000)"), maxLength: 1000, nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: TimestampType, nullable: false),
                    ActorId = table.Column<Guid>(type: GuidType, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriageFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriageFindings_Triages_TriageId",
                        column: x => x.TriageId,
                        principalTable: "Triages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TriageReplyEvidence",
                columns: table => new
                {
                    TriageId = table.Column<Guid>(type: GuidType, nullable: false),
                    ExternalMessageId = table.Column<string>(type: TextType("nvarchar(200)"), maxLength: 200, nullable: false),
                    ConversationId = table.Column<string>(type: TextType("nvarchar(200)"), maxLength: 200, nullable: false),
                    ApprovedMailbox = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: TimestampType, nullable: false),
                    ReplyHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriageReplyEvidence", x => x.TriageId);
                    table.ForeignKey(
                        name: "FK_TriageReplyEvidence_Triages_TriageId",
                        column: x => x.TriageId,
                        principalTable: "Triages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_BusinessActions_CaseId_OccurredAtUtc",
                table: "BusinessActions",
                columns: new[] { "CaseId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessActions_TriageId_OccurredAtUtc",
                table: "BusinessActions",
                columns: new[] { "TriageId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_DisplayReference",
                table: "Cases",
                column: "DisplayReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cases_Registration_PrincipalCode",
                table: "Cases",
                columns: new[] { "Registration", "PrincipalCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_State_IsHeld_NextDueAtUtc",
                table: "Cases",
                columns: new[] { "State", "IsHeld", "NextDueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseSequences_PrincipalCode_Year",
                table: "CaseSequences",
                columns: new[] { "PrincipalCode", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TriageCaseLinks_TriageId_UnlinkedAtUtc",
                table: "TriageCaseLinks",
                columns: new[] { "TriageId", "UnlinkedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TriageFindings_TriageId",
                table: "TriageFindings",
                column: "TriageId");

            migrationBuilder.CreateIndex(
                name: "IX_TriageReplyEvidence_ExternalMessageId_ConversationId",
                table: "TriageReplyEvidence",
                columns: new[] { "ExternalMessageId", "ConversationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "BusinessActions");

            migrationBuilder.DropTable(
                name: "CaseLeases");

            migrationBuilder.DropTable(
                name: "Cases");

            migrationBuilder.DropTable(
                name: "CaseSequences");

            migrationBuilder.DropTable(
                name: "TriageCaseLinks");

            migrationBuilder.DropTable(
                name: "TriageFindings");

            migrationBuilder.DropTable(
                name: "TriageReplyEvidence");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Triages");
        }
    }
}
#pragma warning restore CA1861

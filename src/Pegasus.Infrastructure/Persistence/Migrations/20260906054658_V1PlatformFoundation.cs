using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V1PlatformFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorKind",
                table: "TriageHistory",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false);

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseDataFields_FieldName",
                table: "CaseDataFields");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseAssessmentFields_FieldPath",
                table: "CaseAssessmentFields");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseValuations_Source",
                table: "CaseValuations");

            migrationBuilder.AddColumn<long>(
                name: "ReconciledAssociationVersion",
                table: "UnidentifiedItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrincipalId",
                table: "Triage",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "Triage",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "Sequence",
                table: "Triage",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "RequestUploadLinks",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Recipient",
                table: "RequestUploadLinks",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultInspectionAddress",
                table: "Principals",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultInspectionLocationLabel",
                table: "Principals",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultInspectionPostcode",
                table: "Principals",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultInspectionSourceKind",
                table: "Principals",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultInspectionSourceRecordId",
                table: "Principals",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DefaultInspectionSourceVersion",
                table: "Principals",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoxFileId",
                table: "IntakeAssets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoxVersionId",
                table: "IntakeAssets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustodyStatus",
                table: "IntakeAssets",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrincipalId",
                table: "ImageIntakes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoxFileId",
                table: "DocumentVersions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoxVersionId",
                table: "DocumentVersions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingContentStorageKey",
                table: "DocumentVersions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CropHeight",
                table: "DocumentOccurrences",
                type: "decimal(8,7)",
                precision: 8,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CropLeft",
                table: "DocumentOccurrences",
                type: "decimal(8,7)",
                precision: 8,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CropTop",
                table: "DocumentOccurrences",
                type: "decimal(8,7)",
                precision: 8,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CropWidth",
                table: "DocumentOccurrences",
                type: "decimal(8,7)",
                precision: 8,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreparationRole",
                table: "DocumentOccurrences",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PreparationVersion",
                table: "DocumentOccurrences",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PreparedAtUtc",
                table: "DocumentOccurrences",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreparedBy",
                table: "DocumentOccurrences",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "RotationDegrees",
                table: "DocumentOccurrences",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<int>(
                name: "SupportingOrder",
                table: "DocumentOccurrences",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EditLeaseGeneration",
                table: "CaseWorkflows",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateOnly>(
                name: "GuideMonth",
                table: "CaseValuations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalculationBreakdownJson",
                table: "CaseRepairSpecifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LabourVatApplicable",
                table: "CaseRepairSpecifications",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaterialsDiscountPercent",
                table: "CaseRepairSpecifications",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MaterialsVatApplicable",
                table: "CaseRepairSpecifications",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverallDiscountPercent",
                table: "CaseRepairSpecifications",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PartsDiscountPercent",
                table: "CaseRepairSpecifications",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PartsVatApplicable",
                table: "CaseRepairSpecifications",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RateCardId",
                table: "CaseRepairSpecifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RateCardVersion",
                table: "CaseRepairSpecifications",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepairerVatStatus",
                table: "CaseRepairSpecifications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "SpecialistDiscountPercent",
                table: "CaseRepairSpecifications",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SpecialistVatApplicable",
                table: "CaseRepairSpecifications",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatOverrideReason",
                table: "CaseRepairSpecifications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "WorkUnits",
                table: "CaseEstimateLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,1)",
                oldPrecision: 9,
                oldScale: 1,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PaintWorkUnits",
                table: "CaseEstimateLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,1)",
                oldPrecision: 9,
                oldScale: 1,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AmendedAtUtc",
                table: "CaseEstimateLines",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AmendedBy",
                table: "CaseEstimateLines",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentValuesJson",
                table: "CaseEstimateLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Materials",
                table: "CaseEstimateLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Operation",
                table: "CaseEstimateLines",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalValuesJson",
                table: "CaseEstimateLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceDocumentIdentity",
                table: "CaseEstimateLines",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceDocumentSha256",
                table: "CaseEstimateLines",
                type: "nchar(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceDocumentVersionId",
                table: "CaseEstimateLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceRowIdentity",
                table: "CaseEstimateLines",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Generation",
                table: "ApprovedSentPollStates",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ScopeFingerprint",
                table: "ApprovedSentPollStates",
                type: "nchar(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartBoundaryUtc",
                table: "ApprovedSentPollStates",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<long>(
                name: "Generation",
                table: "ApprovedMailboxSubscriptions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "AllowStaffSend",
                table: "ApprovedMailboxes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "MailboxGeneration",
                table: "ApprovedMailboxes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SendLimitVerifiedAtUtc",
                table: "ApprovedMailboxes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SendLimitVerifiedBy",
                table: "ApprovedMailboxes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VerifiedEncodedMessageSizeLimit",
                table: "ApprovedMailboxes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Generation",
                table: "ApprovedInboxPollStates",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartBoundaryUtc",
                table: "ApprovedInboxPollStates",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "AppliedValuationSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                });

            migrationBuilder.CreateTable(
                name: "CaseReportGenerations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                        name: "FK_CaseReportGenerations_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Contact = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Telephone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentContentCacheEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IntakeAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
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
                name: "IntakeOcrOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IntakeAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.CheckConstraint("CK_IntakeOcrOperations_Source", "([DocumentVersionId] IS NULL AND [IntakeAssetId] IS NOT NULL) OR ([DocumentVersionId] IS NOT NULL AND [IntakeAssetId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_IntakeOcrOperations_DocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeOcrOperations_IntakeAssets_IntakeAssetId",
                        column: x => x.IntakeAssetId,
                        principalTable: "IntakeAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabourRateCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PanelRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
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
                name: "OrganizationDirectoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Contact = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Postcode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NormalizedPostcode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SourceKind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Telephone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationDirectoryEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PublicUploadSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestUploadLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LimitsVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinalizedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicUploadSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicUploadSessions_RequestUploadLinks_RequestUploadLinkId",
                        column: x => x.RequestUploadLinkId,
                        principalTable: "RequestUploadLinks",
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
                name: "StaffMailSendOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MailboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MailboxGeneration = table.Column<long>(type: "bigint", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ContextId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContextVersion = table.Column<long>(type: "bigint", nullable: false),
                    ComposeMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OriginalRetainedMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalImmutableMessageId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalInternetMessageId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalConversationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    ObservedSentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReconciliationContinuation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffMailSendOperations", x => x.Id);
                    table.CheckConstraint("CK_StaffMailSendOperations_AttemptStage", "[AttemptStage] IS NULL OR [AttemptStage] IN ('CreateDraft', 'Attach', 'Send', 'ObserveSent')");
                    table.CheckConstraint("CK_StaffMailSendOperations_State", "[State] IN ('Prepared', 'DraftCreating', 'DraftReady', 'Sending', 'Submitted', 'Sent', 'Failed', 'Unknown', 'Cancelled')");
                });

            migrationBuilder.CreateTable(
                name: "TriageSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LastAllocatedSequence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriageSequences", x => x.Id);
                    table.CheckConstraint("CK_TriageSequences_LastAllocatedSequence", "[LastAllocatedSequence] >= 0");
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
                name: "ValuationPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SuggestedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
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
                name: "CaseReportDeliveryIntents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GenerationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GenerationVersion = table.Column<long>(type: "bigint", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreparedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseReportDeliveryIntents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseReportDeliveryIntents_CaseReportGenerations_GenerationId",
                        column: x => x.GenerationId,
                        principalTable: "CaseReportGenerations",
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

            migrationBuilder.CreateTable(
                name: "PublicUploadOccurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProposedName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CustodyState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicUploadOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicUploadOccurrences_PublicUploadSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "PublicUploadSessions",
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

            migrationBuilder.UpdateData(
                table: "ApprovedMailboxes",
                keyColumn: "Id",
                keyValue: new Guid("49f47eb9-c5b0-464f-b8f0-8c90ba061728"),
                columns: new[] { "AllowStaffSend", "MailboxGeneration", "SendLimitVerifiedAtUtc", "SendLimitVerifiedBy", "VerifiedEncodedMessageSizeLimit" },
                values: new object[] { false, 1L, null, null, null });

            migrationBuilder.Sql(
                "UPDATE [dbo].[ApprovedMailboxes] SET [MailboxGeneration] = 1 WHERE [State] = N'Approved';");

            migrationBuilder.InsertData(
                table: "TriageSequences",
                columns: new[] { "Id", "LastAllocatedSequence" },
                values: new object[] { 1, 0L });

            migrationBuilder.InsertData(
                table: "ValuationPresets",
                columns: new[] { "Id", "Active", "ConcurrencyToken", "Label", "SuggestedAmount", "UpdatedAtUtc", "UpdatedBy", "Version" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-4000-8000-00000000f001"), true, new Guid("00000000-0000-4000-8000-00000000f101"), "Tow bar", 300m, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:v1-foundation", 1L },
                    { new Guid("00000000-0000-4000-8000-00000000f002"), true, new Guid("00000000-0000-4000-8000-00000000f102"), "PCO plated", 1500m, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:v1-foundation", 1L },
                    { new Guid("00000000-0000-4000-8000-00000000f003"), true, new Guid("00000000-0000-4000-8000-00000000f103"), "Decals", 500m, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:v1-foundation", 1L },
                    { new Guid("00000000-0000-4000-8000-00000000f004"), true, new Guid("00000000-0000-4000-8000-00000000f104"), "Camper conversion", 0m, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:v1-foundation", 1L },
                    { new Guid("00000000-0000-4000-8000-00000000f005"), true, new Guid("00000000-0000-4000-8000-00000000f105"), "Driving tuition", 500m, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:v1-foundation", 1L }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Triage_PrincipalId",
                table: "Triage",
                column: "PrincipalId");

            migrationBuilder.CreateIndex(
                name: "IX_Triage_Reference",
                table: "Triage",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Triage_Sequence",
                table: "Triage",
                column: "Sequence",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Triage_Sequence",
                table: "Triage",
                sql: "[Sequence] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakes_PrincipalId",
                table: "ImageIntakes",
                column: "PrincipalId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DocumentOccurrences_Crop",
                table: "DocumentOccurrences",
                sql: "([CropLeft] IS NULL AND [CropTop] IS NULL AND [CropWidth] IS NULL AND [CropHeight] IS NULL) OR ([CropLeft] BETWEEN 0 AND 1 AND [CropTop] BETWEEN 0 AND 1 AND [CropWidth] > 0 AND [CropWidth] <= 1 AND [CropHeight] > 0 AND [CropHeight] <= 1 AND [CropLeft] + [CropWidth] <= 1 AND [CropTop] + [CropHeight] <= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DocumentOccurrences_Rotation",
                table: "DocumentOccurrences",
                sql: "[RotationDegrees] IN (0, 90, 180, 270)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseWorkflows_EditLeaseGeneration",
                table: "CaseWorkflows",
                sql: "[EditLeaseGeneration] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseDataFields_FieldName",
                table: "CaseDataFields",
                sql: "[FieldName] <> '' AND LEN([FieldName]) <= 60");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseAssessmentFields_FieldPath",
                table: "CaseAssessmentFields",
                sql: "[FieldPath] <> '' AND LEN([FieldPath]) <= 60");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseValuations_Source",
                table: "CaseValuations",
                sql: "[Source] IN ('Glasses', 'Cazana', 'EngineersValue', 'AiMarketResearch', 'Brego', 'SuperCap')");

            migrationBuilder.CreateIndex(
                name: "IX_AppliedValuationSnapshots_CaseId_AcceptedAtUtc",
                table: "AppliedValuationSnapshots",
                columns: new[] { "CaseId", "AcceptedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ValuationPresets_Label",
                table: "ValuationPresets",
                column: "Label",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovedSentPollStates_Generation",
                table: "ApprovedSentPollStates",
                sql: "[Generation] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovedMailboxes_SendLimit",
                table: "ApprovedMailboxes",
                sql: "[VerifiedEncodedMessageSizeLimit] IS NULL OR [VerifiedEncodedMessageSizeLimit] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovedMailboxes_MailboxGeneration",
                table: "ApprovedMailboxes",
                sql: "[MailboxGeneration] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovedInboxPollStates_Generation",
                table: "ApprovedInboxPollStates",
                sql: "[Generation] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_AppliedValuationSnapshots_CaseId_SnapshotHash",
                table: "AppliedValuationSnapshots",
                columns: new[] { "CaseId", "SnapshotHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseReportDeliveryIntents_GenerationId_OperationKey",
                table: "CaseReportDeliveryIntents",
                columns: new[] { "GenerationId", "OperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseReportGenerations_CaseId_SnapshotHash",
                table: "CaseReportGenerations",
                columns: new[] { "CaseId", "SnapshotHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimSources_Name",
                table: "ClaimSources",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentContentCacheEntries_DocumentVersionId",
                table: "DocumentContentCacheEntries",
                column: "DocumentVersionId",
                unique: true,
                filter: "[DocumentVersionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentContentCacheEntries_IntakeAssetId",
                table: "DocumentContentCacheEntries",
                column: "IntakeAssetId",
                unique: true,
                filter: "[IntakeAssetId] IS NOT NULL");

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
                name: "IX_IntakeOcrOperations_DocumentVersionId_SourceSha256",
                table: "IntakeOcrOperations",
                columns: new[] { "DocumentVersionId", "SourceSha256" });

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
                name: "IX_OrganizationDirectoryEntries_Role_NormalizedName_Id",
                table: "OrganizationDirectoryEntries",
                columns: new[] { "Role", "NormalizedName", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationDirectoryEntries_Role_NormalizedPostcode_Id",
                table: "OrganizationDirectoryEntries",
                columns: new[] { "Role", "NormalizedPostcode", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicUploadOccurrences_SessionId_OperationKey",
                table: "PublicUploadOccurrences",
                columns: new[] { "SessionId", "OperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicUploadSessions_RequestUploadLinkId",
                table: "PublicUploadSessions",
                column: "RequestUploadLinkId",
                unique: true);

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
                name: "IX_StaffMailSendOperations_ActorSubjectId_MailboxId_OperationKey",
                table: "StaffMailSendOperations",
                columns: new[] { "ActorSubjectId", "MailboxId", "OperationKey" },
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

            if (string.Equals(ActiveProvider, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
            {
                migrationBuilder.Sql("""
                    DECLARE @seededAt datetimeoffset = SYSDATETIMEOFFSET();
                    INSERT dbo.Organizations (Id,Name,Version) SELECT CAST('00000000-0000-4000-8000-00000000d0'+RIGHT('0'+n,2) AS uniqueidentifier),name,0 FROM (VALUES ('01',N'QDOS'),('02',N'Performance Car Hire / Parkhouse'),('03',N'AX'),('04',N'Fairway Legal'),('05',N'QC Law'),('06',N'Oakwood Solicitors'),('07',N'Smart Business Link'),('08',N'Blackstone Legal'),('09',N'Robert James Solicitors'),('0a',N'Davison Flynn Duke Solicitors'),('0b',N'Knightsbridge Solicitors'),('0c',N'Montreal Prestige'),('0d',N'YM Law / Network HD UK'),('0e',N'Auto Logistic Solutions Ltd'),('0f',N'Baker Coleman')) s(n,name);
                    INSERT dbo.OrganizationRoles (OrganizationId,Role) SELECT CAST('00000000-0000-4000-8000-00000000d0'+RIGHT('0'+n,2) AS uniqueidentifier),N'work_provider' FROM (VALUES ('01'),('02'),('03'),('04'),('05'),('06'),('07'),('08'),('09'),('0a'),('0b'),('0c'),('0d'),('0e'),('0f')) s(n);
                    INSERT dbo.PrincipalSequenceLineages (Id,CreatedAtUtc) SELECT CAST('00000000-0000-4000-8000-00000000e0'+RIGHT('0'+n,2) AS uniqueidentifier),@seededAt FROM (VALUES ('01'),('02'),('03'),('04'),('05'),('06'),('07'),('08'),('09'),('0a'),('0b'),('0c'),('0d'),('0e'),('0f')) s(n);
                    INSERT dbo.Principals (Id,OrganizationId,Code,SequenceLineageId,IsActive,InspectionMode,EvaManualSubmission,EvaAutomaticSubmission,Version) SELECT CAST('00000000-0000-4000-8000-00000000c0'+RIGHT('0'+n,2) AS uniqueidentifier),CAST('00000000-0000-4000-8000-00000000d0'+RIGHT('0'+n,2) AS uniqueidentifier),code,CAST('00000000-0000-4000-8000-00000000e0'+RIGHT('0'+n,2) AS uniqueidentifier),1,CASE WHEN code='QDOS' THEN 'image_based_assessment' ELSE 'physical_address' END,0,0,0 FROM (VALUES ('01','QDOS'),('02','PCH'),('03','AX'),('04','FW'),('05','QCL'),('06','OAK'),('07','SBL'),('08','BLACK'),('09','RJS'),('0a','DFD'),('0b','KBS'),('0c','MP'),('0d','YML'),('0e','ALS'),('0f','BC')) s(n,code);
                    """);
                migrationBuilder.Sql("""
                    IF DATABASE_PRINCIPAL_ID('pegasus_web_runtime_role') IS NOT NULL BEGIN GRANT SELECT,INSERT,UPDATE ON OBJECT::[dbo].[UserExternalCredentials] TO [pegasus_web_runtime_role]; GRANT SELECT,INSERT,UPDATE ON OBJECT::[dbo].[StaffMailSendOperations] TO [pegasus_web_runtime_role]; GRANT SELECT,INSERT,UPDATE ON OBJECT::[dbo].[ValuationPresets] TO [pegasus_web_runtime_role]; GRANT SELECT,INSERT,UPDATE ON OBJECT::[dbo].[LabourRateCards] TO [pegasus_web_runtime_role]; GRANT SELECT,INSERT ON OBJECT::[dbo].[AppliedValuationSnapshots] TO [pegasus_web_runtime_role]; GRANT SELECT,INSERT,UPDATE ON OBJECT::[dbo].[GlassRepairEstimateSessions] TO [pegasus_web_runtime_role]; GRANT SELECT,INSERT ON OBJECT::[dbo].[CaseReportGenerations] TO [pegasus_web_runtime_role]; GRANT SELECT,INSERT ON OBJECT::[dbo].[GeneratedCaseArtifacts] TO [pegasus_web_runtime_role]; GRANT SELECT,INSERT,UPDATE ON OBJECT::[dbo].[CaseReportDeliveryIntents] TO [pegasus_web_runtime_role]; GRANT SELECT ON OBJECT::[dbo].[RetainedInstructionAnalyses] TO [pegasus_web_runtime_role]; GRANT SELECT ON OBJECT::[dbo].[IntakeSourceCandidates] TO [pegasus_web_runtime_role]; GRANT SELECT ON OBJECT::[dbo].[IntakeOcrOperations] TO [pegasus_web_runtime_role]; GRANT SELECT,INSERT,UPDATE ON OBJECT::[dbo].[TriageSequences] TO [pegasus_web_runtime_role]; GRANT SELECT,INSERT,UPDATE ON OBJECT::[dbo].[DocumentContentCacheEntries] TO [pegasus_web_runtime_role]; GRANT SELECT,INSERT,UPDATE ON OBJECT::[dbo].[ClaimSources] TO [pegasus_web_runtime_role]; GRANT SELECT,INSERT,UPDATE ON OBJECT::[dbo].[OrganizationDirectoryEntries] TO [pegasus_web_runtime_role]; GRANT SELECT,INSERT,UPDATE ON OBJECT::[dbo].[PublicUploadSessions] TO [pegasus_web_runtime_role]; GRANT SELECT,INSERT,UPDATE ON OBJECT::[dbo].[PublicUploadOccurrences] TO [pegasus_web_runtime_role]; END;
                    IF DATABASE_PRINCIPAL_ID('pegasus_worker_runtime_role') IS NOT NULL BEGIN GRANT SELECT,INSERT,UPDATE ON OBJECT::[dbo].[StaffMailSendOperations] TO [pegasus_worker_runtime_role]; GRANT SELECT,INSERT,UPDATE ON OBJECT::[dbo].[RetainedInstructionAnalyses] TO [pegasus_worker_runtime_role]; GRANT SELECT,INSERT ON OBJECT::[dbo].[IntakeSourceCandidates] TO [pegasus_worker_runtime_role]; GRANT SELECT,INSERT,UPDATE ON OBJECT::[dbo].[IntakeOcrOperations] TO [pegasus_worker_runtime_role]; GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::[dbo].[DocumentContentCacheEntries] TO [pegasus_worker_runtime_role]; GRANT SELECT,INSERT,UPDATE ON OBJECT::[dbo].[TriageSequences] TO [pegasus_worker_runtime_role]; GRANT UPDATE ON OBJECT::[dbo].[IntakeAssets] TO [pegasus_worker_runtime_role]; END;
                    IF DATABASE_PRINCIPAL_ID('pegasus_web_runtime_role') IS NOT NULL DENY DELETE ON OBJECT::[dbo].[DocumentContentCacheEntries] TO [pegasus_web_runtime_role];
                    DECLARE @denyTable sysname; DECLARE @denySql nvarchar(max); DECLARE deny_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT value FROM STRING_SPLIT('UserExternalCredentials,StaffMailSendOperations,ValuationPresets,LabourRateCards,AppliedValuationSnapshots,GlassRepairEstimateSessions,CaseReportGenerations,GeneratedCaseArtifacts,CaseReportDeliveryIntents,RetainedInstructionAnalyses,IntakeSourceCandidates,IntakeOcrOperations,TriageSequences,ClaimSources,OrganizationDirectoryEntries,PublicUploadSessions,PublicUploadOccurrences', ','); OPEN deny_cursor; FETCH NEXT FROM deny_cursor INTO @denyTable; WHILE @@FETCH_STATUS = 0 BEGIN IF DATABASE_PRINCIPAL_ID('pegasus_web_runtime_role') IS NOT NULL BEGIN SET @denySql=N'DENY DELETE ON [dbo].'+QUOTENAME(@denyTable)+N' TO [pegasus_web_runtime_role]'; EXEC(@denySql); END; IF DATABASE_PRINCIPAL_ID('pegasus_worker_runtime_role') IS NOT NULL BEGIN SET @denySql=N'DENY DELETE ON [dbo].'+QUOTENAME(@denyTable)+N' TO [pegasus_worker_runtime_role]'; EXEC(@denySql); END; FETCH NEXT FROM deny_cursor INTO @denyTable; END; CLOSE deny_cursor; DEALLOCATE deny_cursor;
                    """);
            }

            migrationBuilder.AddForeignKey(
                name: "FK_ImageIntakes_Principals_PrincipalId",
                table: "ImageIntakes",
                column: "PrincipalId",
                principalTable: "Principals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Triage_Principals_PrincipalId",
                table: "Triage",
                column: "PrincipalId",
                principalTable: "Principals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActorKind",
                table: "TriageHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_ImageIntakes_Principals_PrincipalId",
                table: "ImageIntakes");

            migrationBuilder.DropForeignKey(
                name: "FK_Triage_Principals_PrincipalId",
                table: "Triage");

            if (string.Equals(ActiveProvider, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
            {
                migrationBuilder.Sql("""
                    DELETE FROM [dbo].[Principals] WHERE [Id] IN (SELECT CAST('00000000-0000-4000-8000-00000000c0'+RIGHT('0'+n,2) AS uniqueidentifier) FROM (VALUES ('01'),('02'),('03'),('04'),('05'),('06'),('07'),('08'),('09'),('0a'),('0b'),('0c'),('0d'),('0e'),('0f')) s(n));
                    DELETE FROM [dbo].[OrganizationRoles] WHERE [OrganizationId] IN (SELECT CAST('00000000-0000-4000-8000-00000000d0'+RIGHT('0'+n,2) AS uniqueidentifier) FROM (VALUES ('01'),('02'),('03'),('04'),('05'),('06'),('07'),('08'),('09'),('0a'),('0b'),('0c'),('0d'),('0e'),('0f')) s(n));
                    DELETE FROM [dbo].[Organizations] WHERE [Id] IN (SELECT CAST('00000000-0000-4000-8000-00000000d0'+RIGHT('0'+n,2) AS uniqueidentifier) FROM (VALUES ('01'),('02'),('03'),('04'),('05'),('06'),('07'),('08'),('09'),('0a'),('0b'),('0c'),('0d'),('0e'),('0f')) s(n));
                    DELETE FROM [dbo].[PrincipalSequenceLineages] WHERE [Id] IN (SELECT CAST('00000000-0000-4000-8000-00000000e0'+RIGHT('0'+n,2) AS uniqueidentifier) FROM (VALUES ('01'),('02'),('03'),('04'),('05'),('06'),('07'),('08'),('09'),('0a'),('0b'),('0c'),('0d'),('0e'),('0f')) s(n));
                    """);
            }

            migrationBuilder.DropTable(
                name: "AppliedValuationSnapshots");

            migrationBuilder.DropTable(
                name: "CaseReportDeliveryIntents");

            migrationBuilder.DropTable(
                name: "ClaimSources");

            migrationBuilder.DropTable(
                name: "DocumentContentCacheEntries");

            migrationBuilder.DropTable(
                name: "GeneratedCaseArtifacts");

            migrationBuilder.DropTable(
                name: "GlassRepairEstimateSessions");

            migrationBuilder.DropTable(
                name: "IntakeOcrOperations");

            migrationBuilder.DropTable(
                name: "IntakeSourceCandidates");

            migrationBuilder.DropTable(
                name: "LabourRateCards");

            migrationBuilder.DropTable(
                name: "OrganizationDirectoryEntries");

            migrationBuilder.DropTable(
                name: "PublicUploadOccurrences");

            migrationBuilder.DropTable(
                name: "StaffMailSendOperations");

            migrationBuilder.DropTable(
                name: "TriageSequences");

            migrationBuilder.DropTable(
                name: "UserExternalCredentials");

            migrationBuilder.DropTable(
                name: "ValuationPresets");

            migrationBuilder.DropTable(
                name: "CaseReportGenerations");

            migrationBuilder.DropTable(
                name: "RetainedInstructionAnalyses");

            migrationBuilder.DropTable(
                name: "PublicUploadSessions");

            migrationBuilder.DropIndex(
                name: "IX_Triage_PrincipalId",
                table: "Triage");

            migrationBuilder.DropIndex(
                name: "IX_Triage_Reference",
                table: "Triage");

            migrationBuilder.DropIndex(
                name: "IX_Triage_Sequence",
                table: "Triage");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Triage_Sequence",
                table: "Triage");

            migrationBuilder.DropIndex(
                name: "IX_ImageIntakes_PrincipalId",
                table: "ImageIntakes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DocumentOccurrences_Crop",
                table: "DocumentOccurrences");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DocumentOccurrences_Rotation",
                table: "DocumentOccurrences");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseWorkflows_EditLeaseGeneration",
                table: "CaseWorkflows");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseDataFields_FieldName",
                table: "CaseDataFields");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseAssessmentFields_FieldPath",
                table: "CaseAssessmentFields");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseValuations_Source",
                table: "CaseValuations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovedSentPollStates_Generation",
                table: "ApprovedSentPollStates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovedMailboxes_SendLimit",
                table: "ApprovedMailboxes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovedMailboxes_MailboxGeneration",
                table: "ApprovedMailboxes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovedInboxPollStates_Generation",
                table: "ApprovedInboxPollStates");

            migrationBuilder.DropColumn(
                name: "ReconciledAssociationVersion",
                table: "UnidentifiedItems");

            migrationBuilder.DropColumn(
                name: "PrincipalId",
                table: "Triage");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "Triage");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "Triage");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "RequestUploadLinks");

            migrationBuilder.DropColumn(
                name: "Recipient",
                table: "RequestUploadLinks");

            migrationBuilder.DropColumn(
                name: "DefaultInspectionAddress",
                table: "Principals");

            migrationBuilder.DropColumn(
                name: "DefaultInspectionLocationLabel",
                table: "Principals");

            migrationBuilder.DropColumn(
                name: "DefaultInspectionPostcode",
                table: "Principals");

            migrationBuilder.DropColumn(
                name: "DefaultInspectionSourceKind",
                table: "Principals");

            migrationBuilder.DropColumn(
                name: "DefaultInspectionSourceRecordId",
                table: "Principals");

            migrationBuilder.DropColumn(
                name: "DefaultInspectionSourceVersion",
                table: "Principals");

            migrationBuilder.DropColumn(
                name: "BoxFileId",
                table: "IntakeAssets");

            migrationBuilder.DropColumn(
                name: "BoxVersionId",
                table: "IntakeAssets");

            migrationBuilder.DropColumn(
                name: "CustodyStatus",
                table: "IntakeAssets");

            migrationBuilder.DropColumn(
                name: "PrincipalId",
                table: "ImageIntakes");

            migrationBuilder.DropColumn(
                name: "BoxFileId",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "BoxVersionId",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "PendingContentStorageKey",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "CropHeight",
                table: "DocumentOccurrences");

            migrationBuilder.DropColumn(
                name: "CropLeft",
                table: "DocumentOccurrences");

            migrationBuilder.DropColumn(
                name: "CropTop",
                table: "DocumentOccurrences");

            migrationBuilder.DropColumn(
                name: "CropWidth",
                table: "DocumentOccurrences");

            migrationBuilder.DropColumn(
                name: "PreparationRole",
                table: "DocumentOccurrences");

            migrationBuilder.DropColumn(
                name: "PreparationVersion",
                table: "DocumentOccurrences");

            migrationBuilder.DropColumn(
                name: "PreparedAtUtc",
                table: "DocumentOccurrences");

            migrationBuilder.DropColumn(
                name: "PreparedBy",
                table: "DocumentOccurrences");

            migrationBuilder.DropColumn(
                name: "RotationDegrees",
                table: "DocumentOccurrences");

            migrationBuilder.DropColumn(
                name: "SupportingOrder",
                table: "DocumentOccurrences");

            migrationBuilder.DropColumn(
                name: "EditLeaseGeneration",
                table: "CaseWorkflows");

            migrationBuilder.DropColumn(
                name: "GuideMonth",
                table: "CaseValuations");

            migrationBuilder.DropColumn(
                name: "CalculationBreakdownJson",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "LabourVatApplicable",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "MaterialsDiscountPercent",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "MaterialsVatApplicable",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "OverallDiscountPercent",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "PartsDiscountPercent",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "PartsVatApplicable",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "RateCardId",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "RateCardVersion",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "RepairerVatStatus",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "SpecialistDiscountPercent",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "SpecialistVatApplicable",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "VatOverrideReason",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "AmendedAtUtc",
                table: "CaseEstimateLines");

            migrationBuilder.DropColumn(
                name: "AmendedBy",
                table: "CaseEstimateLines");

            migrationBuilder.DropColumn(
                name: "CurrentValuesJson",
                table: "CaseEstimateLines");

            migrationBuilder.DropColumn(
                name: "Materials",
                table: "CaseEstimateLines");

            migrationBuilder.DropColumn(
                name: "Operation",
                table: "CaseEstimateLines");

            migrationBuilder.DropColumn(
                name: "OriginalValuesJson",
                table: "CaseEstimateLines");

            migrationBuilder.DropColumn(
                name: "SourceDocumentIdentity",
                table: "CaseEstimateLines");

            migrationBuilder.DropColumn(
                name: "SourceDocumentSha256",
                table: "CaseEstimateLines");

            migrationBuilder.DropColumn(
                name: "SourceDocumentVersionId",
                table: "CaseEstimateLines");

            migrationBuilder.DropColumn(
                name: "SourceRowIdentity",
                table: "CaseEstimateLines");

            migrationBuilder.DropColumn(
                name: "Generation",
                table: "ApprovedSentPollStates");

            migrationBuilder.DropColumn(
                name: "ScopeFingerprint",
                table: "ApprovedSentPollStates");

            migrationBuilder.DropColumn(
                name: "StartBoundaryUtc",
                table: "ApprovedSentPollStates");

            migrationBuilder.DropColumn(
                name: "Generation",
                table: "ApprovedMailboxSubscriptions");

            migrationBuilder.DropColumn(
                name: "AllowStaffSend",
                table: "ApprovedMailboxes");

            migrationBuilder.DropColumn(
                name: "MailboxGeneration",
                table: "ApprovedMailboxes");

            migrationBuilder.DropColumn(
                name: "SendLimitVerifiedAtUtc",
                table: "ApprovedMailboxes");

            migrationBuilder.DropColumn(
                name: "SendLimitVerifiedBy",
                table: "ApprovedMailboxes");

            migrationBuilder.DropColumn(
                name: "VerifiedEncodedMessageSizeLimit",
                table: "ApprovedMailboxes");

            migrationBuilder.DropColumn(
                name: "Generation",
                table: "ApprovedInboxPollStates");

            migrationBuilder.DropColumn(
                name: "StartBoundaryUtc",
                table: "ApprovedInboxPollStates");

            migrationBuilder.AlterColumn<decimal>(
                name: "WorkUnits",
                table: "CaseEstimateLines",
                type: "decimal(9,1)",
                precision: 9,
                scale: 1,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldPrecision: 18,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PaintWorkUnits",
                table: "CaseEstimateLines",
                type: "decimal(9,1)",
                precision: 9,
                scale: 1,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldPrecision: 18,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseDataFields_FieldName",
                table: "CaseDataFields",
                sql: "[FieldName] IN ('work_provider_code', 'claimant_name', 'claimant_contact_number', 'claimant_address', 'claim_number', 'vehicle_registration', 'vehicle_make', 'vehicle_model', 'vehicle_mileage', 'vehicle_mileage_unit', 'accident_circumstances', 'incident_date', 'contact_name', 'contact_email_address', 'contact_phone_number', 'instruction_date', 'vat_status', 'inspection_date', 'inspection_deadline', 'inspection_address', 'inspection_mode', 'storage_location')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseAssessmentFields_FieldPath",
                table: "CaseAssessmentFields",
                sql: "[FieldPath] IN ('assessment.category', 'assessment.impact_location', 'assessment.impact_severity', 'assessment.legal_status', 'assessment.outcome', 'assessment.salvage_value', 'assessment.unroadworthy_reason', 'assessment.values.engineer', 'assessment.values.retail', 'assessment.values.trade', 'costs.recovery_charge', 'costs.repairer_vat_registered', 'costs.storage_charge', 'damage.impacts', 'damage.material_transfer', 'damage.tyres.centre_belt', 'damage.tyres.left_front.belt', 'damage.tyres.left_front.tyre', 'damage.tyres.left_rear.belt', 'damage.tyres.left_rear.tyre', 'damage.tyres.right_front.belt', 'damage.tyres.right_front.tyre', 'damage.tyres.right_rear.belt', 'damage.tyres.right_rear.tyre', 'damage.tyres.spare', 'damage.unrelated', 'damage.unrelated_deduction', 'engineer.name', 'engineer.qualifications', 'engineer.signature', 'fee.agreed_fee', 'fee.description_lines', 'incident.assessed', 'narrative.engineers_comments', 'narrative.history_check', 'narrative.nature_of_incident', 'rates.card', 'rates.class', 'rates.manufacturer_approved', 'rates.regional_uplift', 'settlement.betterment', 'settlement.claimant_vat_registered', 'settlement.diminution', 'settlement.excess', 'settlement.hire_daily_cost', 'settlement.hire_start', 'settlement.repair_delays', 'settlement.report_delay', 'settlement.reserve', 'settlement.salvage.agent', 'settlement.salvage.agent_reference', 'settlement.salvage.at', 'settlement.salvage.moved', 'settlement.salvage.owner_retains', 'settlement.salvage.settled', 'settlement.salvage.value_agreed', 'settlement.storage_per_day', 'statement_of_truth', 'vehicle.airbags_deployed', 'vehicle.body', 'vehicle.colour', 'vehicle.condition', 'vehicle.engine_cc', 'vehicle.fault_codes', 'vehicle.fuel', 'vehicle.mileage_source', 'vehicle.mot_expiry', 'vehicle.tax_expiry', 'vehicle.temporary_repair_cost', 'vehicle.temporary_repair_method', 'vehicle.temporary_repairs_possible', 'vehicle.transmission', 'vehicle.vehicle_type', 'vehicle.vin', 'vehicle.vin_checked', 'vehicle.year')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseValuations_Source",
                table: "CaseValuations",
                sql: "[Source] IN ('Glasses', 'Cazana', 'EngineersValue', 'AiMarketResearch')");
        }
    }
}

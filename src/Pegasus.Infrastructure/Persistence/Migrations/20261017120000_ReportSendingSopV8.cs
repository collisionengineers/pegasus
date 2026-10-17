using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Report sending rules brought to Report Sending SOP v8 (15 October 2026,
/// docs/principal-profiles/sop-guides/report_sending_sop.v5.yaml; operator, 15
/// and 16 October 2026). The rules of the ten Principals v8 changes (CS, DFD,
/// FW, KBS, MP, PCH, RJS, SBL, WALKER and YML) are overwritten with v8's; the
/// other SOP Principals already hold theirs. FRZ, which v8 adds, is created
/// inactive with its v8 rules, and its route activates it. SWADE, which v8
/// drops, keeps its rules for its existing Cases. The claim sources the rules
/// name are matched by name or created with a fixed id, as the earlier SOP
/// seeds do; iRevolution Repair Network is new. OAK's TL threshold becomes 65%
/// and PCH is named "PCH / Connexus". Data only and forward-only: the
/// overwritten rules come back only from a backup.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261017120000_ReportSendingSopV8")]
public partial class ReportSendingSopV8 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- The claim sources the rule conditions point at, matched by name, else created with a fixed id.
            DECLARE @src TABLE (Code nvarchar(20), Slot char(2), Name nvarchar(300), OrgId uniqueidentifier);
            INSERT @src (Code, Slot, Name) VALUES
                (N'CAR2GO', '01', N'Car 2 Go'),
                (N'SMC', '02', N'SMC'),
                (N'CARCLAIMS', '03', N'CarClaims'),
                (N'EXPERT', '04', N'Expert Claims'),
                (N'RAPID', '05', N'Rapid Rental Solutions'),
                (N'IREVOLUTION', '0b', N'iRevolution Repair Network');
            UPDATE s SET OrgId = o.Id FROM @src s
            JOIN dbo.Organizations o ON o.NormalizedName = UPPER(LTRIM(RTRIM(s.Name))) AND o.Role = N'claim_source';
            INSERT dbo.Organizations (Id, Name, Role, Active, Version)
            SELECT CAST('00000000-0000-4000-8000-00000000f0' + s.Slot AS uniqueidentifier), s.Name, N'claim_source', 1, 0 FROM @src s WHERE s.OrgId IS NULL;
            UPDATE @src SET OrgId = CAST('00000000-0000-4000-8000-00000000f0' + Slot AS uniqueidentifier) WHERE OrgId IS NULL;

            -- FRZ, the Principal v8 adds: the rows a new Principal Contact saved inactive writes, both flags 0.
            IF NOT EXISTS (SELECT 1 FROM dbo.Principals WHERE Code = N'FRZ')
            BEGIN
                INSERT dbo.Organizations (Id, Name, Role, Active, Version)
                VALUES ('00000000-0000-4000-8000-00000000d035', N'Fraz', N'principal', 0, 0);
                INSERT dbo.PrincipalSequenceLineages (Id, CreatedAtUtc)
                VALUES ('00000000-0000-4000-8000-00000000e035', SYSDATETIMEOFFSET());
                INSERT dbo.Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, InspectionMode, Version)
                VALUES ('00000000-0000-4000-8000-00000000c035', '00000000-0000-4000-8000-00000000d035', N'FRZ',
                    '00000000-0000-4000-8000-00000000e035', 0, N'physical_address', 0);
            END

            -- Ids the rule conditions use.
            DECLARE @car2go nvarchar(36) = (SELECT LOWER(CONVERT(nvarchar(36), OrgId)) FROM @src WHERE Code = N'CAR2GO');
            DECLARE @smc nvarchar(36) = (SELECT LOWER(CONVERT(nvarchar(36), OrgId)) FROM @src WHERE Code = N'SMC');
            DECLARE @carclaims nvarchar(36) = (SELECT LOWER(CONVERT(nvarchar(36), OrgId)) FROM @src WHERE Code = N'CARCLAIMS');
            DECLARE @expert nvarchar(36) = (SELECT LOWER(CONVERT(nvarchar(36), OrgId)) FROM @src WHERE Code = N'EXPERT');
            DECLARE @rapid nvarchar(36) = (SELECT LOWER(CONVERT(nvarchar(36), OrgId)) FROM @src WHERE Code = N'RAPID');
            DECLARE @irevolution nvarchar(36) = (SELECT LOWER(CONVERT(nvarchar(36), OrgId)) FROM @src WHERE Code = N'IREVOLUTION');

            -- Report sending rules from Report Sending SOP v8 (15 October 2026), docs/principal-profiles/sop-guides/report_sending_sop.v5.yaml.
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendFromMailbox":"engineers@collisionengineers.co.uk","sendTo":["info@claimsspecialists.co.uk","accounts@claimsspecialists.co.uk"],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'CS';
            UPDATE dbo.Principals SET ReportSendingRulesJson = REPLACE(REPLACE(REPLACE(N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["richard.pownall@dfd-solicitors.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[{"match":"All","if":[{"kind":"ClaimSource","values":["@@CAR2GO@@"]}],"then":{"ccAdd":["info@car2go-nw.co.uk"],"ccRemove":[]}},{"match":"All","if":[{"kind":"ClaimSource","values":["@@SMC@@"]}],"then":{"ccAdd":["tony@shaunmarnell.co.uk"],"ccRemove":[]}},{"match":"All","if":[{"kind":"ClaimSource","values":["@@CARCLAIMS@@"]}],"then":{"ccAdd":[],"ccRemove":[],"stop":"CarClaims job. This needs checking with Andy before it is sent."}}]}', N'@@CAR2GO@@', @car2go), N'@@SMC@@', @smc), N'@@CARCLAIMS@@', @carclaims) WHERE Code = N'DFD';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":["Ontrack789@gmail.com"],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":["WhatsApp Swade to say the report has been sent."],"rules":[]}' WHERE Code = N'FRZ';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["info@fairwaylegal.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'FW';
            UPDATE dbo.Principals SET ReportSendingRulesJson = REPLACE(REPLACE(N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["info@knightsbridgesolicitors.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[{"match":"All","if":[{"kind":"ClaimSource","values":["@@EXPERT@@"]}],"then":{"ccAdd":["gabbas@knightsbridgesolicitors.co.uk"],"ccRemove":[]}},{"match":"All","if":[{"kind":"ClaimSource","values":["@@RAPID@@"]}],"then":{"ccAdd":["Rapidlongton@gmail.com"],"ccRemove":[]}}]}', N'@@EXPERT@@', @expert), N'@@RAPID@@', @rapid) WHERE Code = N'KBS';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["mdakri@montrealprestige.co.uk","info@montrealprestige.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":false,"vehicleImagesDocument":true,"figureBreakdown":true},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'MP';
            UPDATE dbo.Principals SET ReportSendingRulesJson = REPLACE(N'{"sendTo":["SBL@connexus.co.uk"],"sendToOnly":false,"replyAll":false,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":true},"garageFigures":false,"reminders":[],"rules":[{"match":"All","if":[{"kind":"ClaimSource","values":["@@IREVOLUTION@@"]},{"kind":"Outcome","values":["repairable"]}],"then":{"ccAdd":["repairs@irevolutionrepairnetwork.co.uk"],"ccRemove":[],"remind":"Authorise the garage","rateCard":true}},{"match":"Any","if":[{"kind":"Mentions","values":["audit"]},{"kind":"RefPrefix","values":["a"]},{"kind":"PreviousJob","values":[]}],"then":{"ccAdd":[],"ccRemove":["repairs@irevolutionrepairnetwork.co.uk"]}}],"attachmentName":{"first":"{reg} Initial","resend":"{reg} Supplementary"}}', N'@@IREVOLUTION@@', @irevolution) WHERE Code = N'PCH';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["engreport@robertjameslaw.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[{"match":"Any","if":[{"kind":"Mentions","values":["Luton"]},{"kind":"Mentions","values":["Claim Specialist","Claims Specialist","claimsspecialists.co.uk"]}],"then":{"ccAdd":["accounts@claimsspecialists.co.uk"],"ccRemove":[]}},{"match":"All","if":[{"kind":"Mentions","values":["Accident Specialist"]}],"then":{"ccAdd":["admin@accidentspecialist.co.uk"],"ccRemove":[]}}]}' WHERE Code = N'RJS';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":["Claims@smartbusinesslink.com"],"sendToOnly":false,"replyAll":true,"cc":["SBL@connexus.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[{"match":"All","if":[{"kind":"Outcome","values":["repairable"]}],"then":{"ccAdd":[],"ccRemove":[],"remind":"Check with the engineer that the garage has been authorised."}}]}' WHERE Code = N'SBL';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'WALKER';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[{"match":"All","if":[{"kind":"ImagesFrom","values":["Swinton"]}],"then":{"ccAdd":[],"ccRemove":[],"remind":"send Swinton a copy of the report on WhatsApp."}}]}' WHERE Code = N'YML';

            -- OAK's TL threshold, where the v5 seed appended it to its Notes on every Case.
            UPDATE o SET NotesOnEveryCase = REPLACE(o.NotesOnEveryCase, N'TL threshold: 74%', N'TL threshold: 65%')
            FROM dbo.Organizations o JOIN dbo.Principals p ON p.OrganizationId = o.Id
            WHERE p.Code = N'OAK' AND CHARINDEX(N'TL threshold: 74%', o.NotesOnEveryCase) > 0;

            -- PCH's name.
            UPDATE o SET Name = N'PCH / Connexus'
            FROM dbo.Organizations o JOIN dbo.Principals p ON p.OrganizationId = o.Id
            WHERE p.Code = N'PCH';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "ReportSendingSopV8 is forward-only: it overwrites the report sending rules of ten Principals. Restore from an approved backup instead.");
}

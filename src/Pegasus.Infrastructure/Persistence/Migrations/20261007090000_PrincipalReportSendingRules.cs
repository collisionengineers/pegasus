using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Principal report sending rules (Report Sending SOP v5, 2 October 2026;
/// operator, 6 October 2026): every Principal's rules, stored as JSON on its
/// row and required. The migration seeds the claim sources the rule
/// conditions point at, the 17 SOP Principals Pegasus did not yet have, every
/// SOP Principal's rules and the TL thresholds appended to Notes on every
/// Case; the seed is guarded, so it adds nothing that already exists. Every
/// other Principal gets the default rules. It then drops the retired report
/// recipient settings, <c>Principals.IncludeOriginalInstructionSender</c> and
/// <c>Principals.ReportRecipientAddressesJson</c>. Destructive and
/// forward-only.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261007090000_PrincipalReportSendingRules")]
public partial class PrincipalReportSendingRules : Migration
{
    private const string DefaultRulesJson =
        """{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}""";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.AddColumn<string>(name: "ReportSendingRulesJson", table: "Principals", type: "nvarchar(max)", nullable: true);

        migrationBuilder.Sql("""
            DECLARE @seededAt datetimeoffset = SYSDATETIMEOFFSET();
            DECLARE @crlf nvarchar(2) = CHAR(13) + CHAR(10);

            -- Claim sources the rule conditions point at (matched by normalised name, then created with a fixed id).
            DECLARE @src TABLE (Code nvarchar(20), Slot char(2), Name nvarchar(300), Role nvarchar(40), OrgId uniqueidentifier);
            INSERT @src (Code, Slot, Name, Role) VALUES
                (N'CAR2GO', '01', N'Car 2 Go', N'claim_source'),
                (N'SMC', '02', N'SMC', N'claim_source'),
                (N'CARCLAIMS', '03', N'CarClaims', N'claim_source'),
                (N'EXPERT', '04', N'Expert Claims', N'claim_source'),
                (N'RAPID', '05', N'Rapid Rental Solutions', N'claim_source');
            UPDATE s SET OrgId = o.Id FROM @src s JOIN dbo.Organizations o ON o.NormalizedName = UPPER(LTRIM(RTRIM(s.Name)));
            INSERT dbo.Organizations (Id, Name, Active, Version)
            SELECT CAST('00000000-0000-4000-8000-00000000f0' + s.Slot AS uniqueidentifier), s.Name, 1, 0 FROM @src s WHERE s.OrgId IS NULL;
            UPDATE @src SET OrgId = CAST('00000000-0000-4000-8000-00000000f0' + Slot AS uniqueidentifier) WHERE OrgId IS NULL;
            INSERT dbo.ContactRoles (OrganizationId, Role) SELECT s.OrgId, s.Role FROM @src s
            WHERE NOT EXISTS (SELECT 1 FROM dbo.ContactRoles r WHERE r.OrganizationId = s.OrgId AND r.Role = s.Role);

            -- The 17 Principals the SOP names that Pegasus does not have yet. Same rows a new Principal Contact saves.
            DECLARE @new TABLE (Code nvarchar(20), Slot char(2), Name nvarchar(300), Address nvarchar(1000), Postcode nvarchar(20), OrgId uniqueidentifier);
            INSERT @new (Code, Slot, Name, Address, Postcode) VALUES
                (N'AMS', '10', N'AMS Solicitors', N'Wentworth Building' + @crlf + N'1b Fairways Office Park' + @crlf + N'Pittman Way', N'PR2 9LF'),
                (N'ACSP', '11', N'Accident Specialists', N'C/o Accident Specialists' + @crlf + N'1-3 Brighton Road' + @crlf + N'Crawley', N'RH10 6AE'),
                (N'ALISON', '12', N'Alison Law Solicitors', N'C/O Alison Law Solicitors' + @crlf + N'437-441 London Road' + @crlf + N'Sheffield', N'S2 4HJ'),
                (N'CS', '13', N'Claim Specialists', N'C/O Claim Specialists', NULL),
                (N'HTU', '14', N'HTU Assessors Ltd', N'C/O HTU Assessors Ltd' + @crlf + N'The Old Courthouse' + @crlf + N'18-22 St Peters Churchyard', N'DE1 1NN'),
                (N'KERR', '15', N'Kerr Brown Partnership', N'C/o Kerr Brown Partnership' + @crlf + N'50 Wellington Street' + @crlf + N'Glasgow', N'G26HJ'),
                (N'KMR', '16', N'KMR Law Ltd', N'C/O KMR Law Ltd' + @crlf + N'221 Withington Road' + @crlf + N'Manchester', N'M16 8LU'),
                (N'MIDAS', '17', N'Midas', NULL, NULL),
                (N'MOTORX', '18', N'Motor X Assistance Ltd', N'C/O Motor X Assistance Ltd' + @crlf + N'300 Biscot Road' + @crlf + N'Luton', N'LU3 1AZ'),
                (N'SIX', '19', N'Six Ways Marketing Ltd', N'C/O Six Ways Marketing Ltd' + @crlf + N'390 Coventry Road' + @crlf + N'Birmingham', N'B10 0UF'),
                (N'SS', '1a', N'Savas & Savage Solicitors Ltd', N'C/o Savas & Savage Solicitors Ltd' + @crlf + N'20 Stanney Lane' + @crlf + N'Ellesmere Port', N'CH65 9AD'),
                (N'SWADE', '1b', N'Swade Solutions Ltd', N'Plot B1' + @crlf + N'Higginshaw Lane' + @crlf + N'Oldham', N'OL13LA'),
                (N'SWAN', '1c', N'Swan Solicitors', N'C/O Swan Solicitors' + @crlf + N'20 Swan Street' + @crlf + N'Manchester', N'M4 5JW'),
                (N'TEN', '1d', N'Ten Legal', N'C/O Ten Legal' + @crlf + N'3 Manchester Road' + @crlf + N'Bury', N'BL9 0DR'),
                (N'TP', '1e', N'Taylor Price Solicitors', N'C/o Taylor Price Solicitors' + @crlf + N'Unit 5, 173 Cheetham Hill Road' + @crlf + N'Manchester', N'M8 8LG'),
                (N'WALKER', '1f', N'Walker Prestons', N'Walker Prestons' + @crlf + N'Mount Pleasant' + @crlf + N'Trinity Street', N'BB1 5BN'),
                (N'WLS', '20', N'Woodlands Solicitors', N'C/O Woodlands Solicitors' + @crlf + N'235 Bury New Road' + @crlf + N'Whitefield', N'M45 8QP');
            DELETE n FROM @new n WHERE EXISTS (SELECT 1 FROM dbo.Principals p WHERE p.Code = n.Code);
            UPDATE n SET OrgId = o.Id FROM @new n JOIN dbo.Organizations o ON o.NormalizedName = UPPER(LTRIM(RTRIM(n.Name)));
            INSERT dbo.Organizations (Id, Name, Address, Postcode, Active, Version)
            SELECT CAST('00000000-0000-4000-8000-00000000d0' + n.Slot AS uniqueidentifier), n.Name, n.Address, n.Postcode, 1, 0 FROM @new n WHERE n.OrgId IS NULL;
            UPDATE @new SET OrgId = CAST('00000000-0000-4000-8000-00000000d0' + Slot AS uniqueidentifier) WHERE OrgId IS NULL;
            INSERT dbo.ContactRoles (OrganizationId, Role) SELECT n.OrgId, N'principal' FROM @new n
            WHERE NOT EXISTS (SELECT 1 FROM dbo.ContactRoles r WHERE r.OrganizationId = n.OrgId AND r.Role = N'principal');
            INSERT dbo.PrincipalSequenceLineages (Id, CreatedAtUtc) SELECT CAST('00000000-0000-4000-8000-00000000e0' + n.Slot AS uniqueidentifier), @seededAt FROM @new n;
            INSERT dbo.Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, InspectionMode, ReportGenerationPolicy, Version)
            SELECT CAST('00000000-0000-4000-8000-00000000c0' + n.Slot AS uniqueidentifier), n.OrgId, n.Code, CAST('00000000-0000-4000-8000-00000000e0' + n.Slot AS uniqueidentifier), 1, N'physical_address', N'Pegasus', 0
            FROM @new n WHERE NOT EXISTS (SELECT 1 FROM dbo.Principals p WHERE p.Code = n.Code);

            -- ACSP and CS also introduce work, so they are Claim sources as well.
            INSERT dbo.ContactRoles (OrganizationId, Role) SELECT p.OrganizationId, N'claim_source' FROM dbo.Principals p
            WHERE p.Code IN (N'ACSP', N'CS') AND NOT EXISTS (SELECT 1 FROM dbo.ContactRoles r WHERE r.OrganizationId = p.OrganizationId AND r.Role = N'claim_source');

            -- Ids the rule conditions use.
            DECLARE @car2go nvarchar(36) = (SELECT LOWER(CONVERT(nvarchar(36), OrgId)) FROM @src WHERE Code = N'CAR2GO');
            DECLARE @smc nvarchar(36) = (SELECT LOWER(CONVERT(nvarchar(36), OrgId)) FROM @src WHERE Code = N'SMC');
            DECLARE @carclaims nvarchar(36) = (SELECT LOWER(CONVERT(nvarchar(36), OrgId)) FROM @src WHERE Code = N'CARCLAIMS');
            DECLARE @expert nvarchar(36) = (SELECT LOWER(CONVERT(nvarchar(36), OrgId)) FROM @src WHERE Code = N'EXPERT');
            DECLARE @rapid nvarchar(36) = (SELECT LOWER(CONVERT(nvarchar(36), OrgId)) FROM @src WHERE Code = N'RAPID');
            DECLARE @cs nvarchar(36) = (SELECT LOWER(CONVERT(nvarchar(36), OrganizationId)) FROM dbo.Principals WHERE Code = N'CS');
            DECLARE @acsp nvarchar(36) = (SELECT LOWER(CONVERT(nvarchar(36), OrganizationId)) FROM dbo.Principals WHERE Code = N'ACSP');

            -- Report sending rules from Report Sending SOP v5 (2 October 2026), docs/principal-profiles/sop-guides/report_sending_sop.v5.yaml.
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["admin@accidentspecialist.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'ACSP';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'ALISON';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["claims@autologistic.co.uk","p.mandy@oakwoodscotland.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":true,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":true,"reminders":[],"rules":[]}' WHERE Code = N'ALS';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'AMS';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":["engineersreports@ax-uk.com","engineersinspections@ax-uk.com"],"sendToOnly":false,"replyAll":true,"cc":["p.mandy@oakwoodscotland.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":true,"reminders":[],"rules":[{"match":"All","if":[{"kind":"BodyshopMentions","values":["Easdons"]},{"kind":"BodyshopMentions","values":["Guardian","James Claims"]}],"then":{"ccAdd":[],"ccRemove":["p.mandy@oakwoodscotland.co.uk"]}}]}' WHERE Code = N'AX';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["a.nawaz@bakercoleman.co.uk","h.ahmed@bakercoleman.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'BC';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":true,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'BLACK';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendFromMailbox":"engineers@collisionengineers.co.uk","sendTo":["info@claimsspecialists.co.uk","accounts@carhirespec.co.uk"],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'CS';
            UPDATE dbo.Principals SET ReportSendingRulesJson = REPLACE(REPLACE(REPLACE(N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[{"match":"All","if":[{"kind":"ClaimSource","values":["@@CAR2GO@@"]}],"then":{"ccAdd":["info@car2go-nw.co.uk"],"ccRemove":[]}},{"match":"All","if":[{"kind":"ClaimSource","values":["@@SMC@@"]}],"then":{"ccAdd":["tony@shawnmarnell.co.uk","richard.pownall@dfd-solicitors.co.uk"],"ccRemove":[]}},{"match":"All","if":[{"kind":"ClaimSource","values":["@@CARCLAIMS@@"]}],"then":{"ccAdd":[],"ccRemove":[],"stop":"CarClaims job. This needs checking with Andy before it is sent."}}]}', N'@@CAR2GO@@', @car2go), N'@@SMC@@', @smc), N'@@CARCLAIMS@@', @carclaims) WHERE Code = N'DFD';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[{"match":"All","if":[{"kind":"SenderNot","values":["info@fairwaylegal.co.uk"]}],"then":{"ccAdd":["info@fairwaylegal.co.uk"],"ccRemove":[]}}]}' WHERE Code = N'FW';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'HTU';
            UPDATE dbo.Principals SET ReportSendingRulesJson = REPLACE(REPLACE(N'{"sendTo":["info@knightsbridgesolicitors.co.uk"],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[{"match":"All","if":[{"kind":"ClaimSource","values":["@@EXPERT@@"]}],"then":{"ccAdd":["gabbas@knightsbridgesolicitors.co.uk"],"ccRemove":[]}},{"match":"All","if":[{"kind":"ClaimSource","values":["@@RAPID@@"]}],"then":{"ccAdd":["Rapidlongton@gmail.com"],"ccRemove":[]}}]}', N'@@EXPERT@@', @expert), N'@@RAPID@@', @rapid) WHERE Code = N'KBS';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'KERR';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["info@kmrlaw.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'KMR';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"hold":"WhatsApp the report to Midas to check before it is sent.","reminders":[],"rules":[]}' WHERE Code = N'MIDAS';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'MOTORX';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":["mdakri@montrealprestige.co.uk","info@montrealprestige.co.uk"],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":false,"vehicleImagesDocument":true,"figureBreakdown":true},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'MP';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["p.mandy@oakwoodscotland.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":true,"reminders":[],"rules":[]}' WHERE Code = N'OAK';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":["SBL@connexus.co.uk"],"sendToOnly":true,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":true,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[],"attachmentName":{"first":"{reg} Initial","resend":"{reg} Supplementary"}}' WHERE Code = N'PCH';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":["credithire@hackneysolutions.co.uk"],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'QCL';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["engineers@qdosassist.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":true,"reminders":[],"rules":[]}' WHERE Code = N'QDOS';
            UPDATE dbo.Principals SET ReportSendingRulesJson = REPLACE(REPLACE(N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[{"match":"Any","if":[{"kind":"Mentions","values":["Luton"]},{"kind":"ClaimSource","values":["@@CS@@"]}],"then":{"ccAdd":["engreport@robertjameslaw.co.uk","accounts@claimsspecialists.co.uk"],"ccRemove":[]}},{"match":"All","if":[{"kind":"ClaimSource","values":["@@ACSP@@"]}],"then":{"ccAdd":["admin@accidentspecialist.co.uk"],"ccRemove":[]}}]}', N'@@CS@@', @cs), N'@@ACSP@@', @acsp) WHERE Code = N'RJS';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":["Claims@smartbusinesslink.com"],"sendToOnly":false,"replyAll":true,"cc":["SBL@connexus.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":["Authorise the garage."],"rules":[]}' WHERE Code = N'SBL';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":["info@sixwaysclaims.co.uk"],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'SIX';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'SS';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":["Ontrack789@gmail.com"],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":["WhatsApp Swade to say the report has been sent."],"rules":[]}' WHERE Code = N'SWADE';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'SWAN';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":false,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'TEN';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":["info@taylorprice.co.uk"],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'TP';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":["Send the report to Waqqas on WhatsApp."],"rules":[]}' WHERE Code = N'WALKER';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE Code = N'WLS';
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[{"match":"All","if":[{"kind":"ImagesFrom","values":["Swinton"]}],"then":{"ccAdd":[],"ccRemove":[],"remind":"send Swinton a copy of the report."}}]}' WHERE Code = N'YML';

            -- TL thresholds go to Notes on every Case (added once; re-running adds nothing).
            DECLARE @notes TABLE (Code nvarchar(20), NoteText nvarchar(2000));
            INSERT @notes (Code, NoteText) VALUES
                (N'ALS', N'TL threshold: 78%'),
                (N'AX', N'TL threshold: 74%'),
                (N'BLACK', N'TL threshold: 74%'),
                (N'OAK', N'TL threshold: 74%'),
                (N'PCH', N'TL threshold: 66% (always)'),
                (N'QCL', N'Contract repair at 73%.'),
                (N'QDOS', N'TL threshold: 78%'),
                (N'RJS', N'TL threshold: 70%'),
                (N'SBL', N'TL threshold: 66%');
            UPDATE o SET NotesOnEveryCase = CASE WHEN o.NotesOnEveryCase IS NULL OR LTRIM(RTRIM(o.NotesOnEveryCase)) = N'' THEN n.NoteText ELSE o.NotesOnEveryCase + @crlf + n.NoteText END
            FROM dbo.Organizations o JOIN dbo.Principals p ON p.OrganizationId = o.Id JOIN @notes n ON n.Code = p.Code
            WHERE o.NotesOnEveryCase IS NULL OR CHARINDEX(n.NoteText, o.NotesOnEveryCase) = 0;
            """);

        // Every other Principal has the default rules: reply to the original
        // sender and keep the instruction's Cc. The text is what
        // EfOrganizationAdministration.ToReportSendingJson writes for
        // PrincipalReportSendingRules.Default.
        migrationBuilder.Sql("""
            UPDATE dbo.Principals SET ReportSendingRulesJson = N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}' WHERE ReportSendingRulesJson IS NULL;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "ReportSendingRulesJson",
            table: "Principals",
            type: "nvarchar(max)",
            nullable: false,
            // A row written without naming its rules has the Default ones, as
            // the retired settings had their own column defaults.
            defaultValue: DefaultRulesJson,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldNullable: true);

        // The retired report recipient settings (operator, 6 October 2026).
        migrationBuilder.DropColumn(name: "IncludeOriginalInstructionSender", table: "Principals");
        migrationBuilder.DropColumn(name: "ReportRecipientAddressesJson", table: "Principals");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "PrincipalReportSendingRules is forward-only: it drops Principals.IncludeOriginalInstructionSender and Principals.ReportRecipientAddressesJson. Restore from an approved backup instead.");
}

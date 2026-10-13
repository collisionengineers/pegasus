using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Contacts seeded from the SOP evidence (operator, 13 October 2026). The Claim
/// sources Report Sending SOP v5 names that Pegasus did not have; the Principals
/// the job sheet's Principals sheet lists that Pegasus did not have, with the
/// default report sending rules; and each Principal's job-sheet rows (Inbox,
/// Solicitors Instructions, Images location, Image based or address, Sending
/// Report) plus the v5 notes and received-via the sheet lacks, appended to its
/// Notes on every Case. The seed is guarded: it adds no contact whose code or
/// name exists and no note text a record already holds, and it refuses to cut a
/// note short. Data only; nothing to reverse.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261013091000_ContactSopSeed")]
public partial class ContactSopSeed : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE @seededAt datetimeoffset = SYSDATETIMEOFFSET();
            DECLARE @crlf nvarchar(2) = CHAR(13) + CHAR(10);

            -- Claim sources Report Sending SOP v5 names (ACSP and CS are Principals only).
            DECLARE @src TABLE (Slot char(2), Name nvarchar(300));
            INSERT @src (Slot, Name) VALUES
                ('06', N'Easdons'),
                ('07', N'Guardian Accident Claims'),
                ('08', N'Hackney Solutions'),
                ('09', N'James Claims'),
                ('0a', N'Swinton');
            INSERT dbo.Organizations (Id, Name, Role, Active, Version)
            SELECT CAST('00000000-0000-4000-8000-00000000f0' + s.Slot AS uniqueidentifier), s.Name, N'claim_source', 1, 0 FROM @src s
            WHERE NOT EXISTS (SELECT 1 FROM dbo.Organizations o WHERE o.NormalizedName = UPPER(LTRIM(RTRIM(s.Name))) AND o.Role = N'claim_source');

            -- Principals the job sheet lists that Pegasus does not have yet. Same rows a new Principal Contact saves.
            DECLARE @new TABLE (Code nvarchar(20), Slot char(2), Name nvarchar(300));
            INSERT @new (Code, Slot, Name) VALUES
                (N'ABRAHAMS', '21', N'Abrahams Solicitors'),
                (N'ASLS', '22', N'Affinity Seven Law Solicitors'),
                (N'ALL', '23', N'Alliance & Cooper'),
                (N'AS', '24', N'Aman Solicitors Advocates'),
                (N'AVI', '25', N'Avisons Solicitors'),
                (N'BAKER', '26', N'Baker Hardman'),
                (N'CASTLE', '27', N'Castle'),
                (N'CW', '28', N'Countrywide'),
                (N'GG', '29', N'Graham Coffey'),
                (N'LEX', '2a', N'LEX Solicitors'),
                (N'LPS', '2b', N'LPS Solicitors'),
                (N'MATT', '2c', N'Matt Rowland Solicitors'),
                (N'MBH', '2d', N'MBH Solicitors'),
                (N'RL', '2e', N'Regent Law Ltd'),
                (N'RELAY', '2f', N'Relay Motor Group'),
                (N'ROZZII', '30', N'ROZZII/Green Destinations'),
                (N'STALLION', '31', N'Stallion'),
                (N'TA', '32', N'Turnams'),
                (N'WIL', '33', N'Williams & Co'),
                (N'ZEN', '34', N'Zenith Lawyers');
            DELETE n FROM @new n
            WHERE EXISTS (SELECT 1 FROM dbo.Principals p WHERE p.Code = n.Code)
               OR EXISTS (SELECT 1 FROM dbo.Organizations o WHERE o.NormalizedName = UPPER(LTRIM(RTRIM(n.Name))) AND o.Role = N'principal');
            INSERT dbo.Organizations (Id, Name, Role, Active, Version)
            SELECT CAST('00000000-0000-4000-8000-00000000d0' + n.Slot AS uniqueidentifier), n.Name, N'principal', 1, 0 FROM @new n;
            INSERT dbo.PrincipalSequenceLineages (Id, CreatedAtUtc)
            SELECT CAST('00000000-0000-4000-8000-00000000e0' + n.Slot AS uniqueidentifier), @seededAt FROM @new n;
            INSERT dbo.Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, InspectionMode, Version)
            SELECT CAST('00000000-0000-4000-8000-00000000c0' + n.Slot AS uniqueidentifier), CAST('00000000-0000-4000-8000-00000000d0' + n.Slot AS uniqueidentifier),
                n.Code, CAST('00000000-0000-4000-8000-00000000e0' + n.Slot AS uniqueidentifier), 1, N'physical_address', 0
            FROM @new n;

            -- Each Principal's SOP text, appended to its Notes on every Case once.
            DECLARE @notes TABLE (Code nvarchar(20), NoteText nvarchar(2000));
            INSERT @notes (Code, NoteText) VALUES
                (N'ABRAHAMS', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Attached to email'),
                (N'ACSP', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Direct they will confirm if in Storage or not - check board' + @crlf + N'Images location: Accident Specialist group or request from client' + @crlf + N'Image based or address: Storage - RH10 9NT (Priestley Way Crawley)' + @crlf + N'Office RH10 6AE' + @crlf + N'Direct - ask whoever provides images to confirm' + @crlf + N'Sending Report: Return to file handler copy in admin@accidentspecialist.co.uk'),
                (N'ASLS', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Word doc in Email' + @crlf + N'Images location: Customer Whatsapp'),
                (N'ALISON', N'Inbox: Andrew/Engineers' + @crlf + N'Solicitors Instructions: Usually get a message in whats app' + @crlf + N'Images location: Whats App group with Alison Law' + @crlf + N'Image based or address: Image based if not residential address' + @crlf + N'Ask whoever sends images to confirm' + @crlf + N'Sending Report: Return to file handler copy in marya.sheikh@alisonlaw.co.uk & Bradford@alison-law.co.uk'),
                (N'ALL', N'Inbox: Engineers' + @crlf + N'Images location: Contact client' + @crlf + N'Image based or address: Confirm address with client' + @crlf + N'Sending Report: Fee note separate return to file handler and lamiaa.fadl@ac-solicitors.co.uk'),
                (N'AS', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Instructions in email' + @crlf + N'Images location: Details should be in the email' + @crlf + N'Image based or address: Depends on quality/quantity - image based'),
                (N'AMS', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: PDF in email' + @crlf + N'Images location: Raja or Ontrack' + @crlf + N'Image based or address: If Higginshaw Lane - then OL2 6HW'),
                (N'ALS', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Usually say if they wat rw/unr and rep or total loss' + @crlf + N'Images location: Image Based - Images with email or from garage' + @crlf + N'Image based or address: Image-based' + @crlf + N'Sending Report: Send back to handler and claims@autologistic.co.uk with fee separate' + @crlf + N'Copy in Paul Mandy'),
                (N'AVI', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: PDF/Word doc in email' + @crlf + N'Images location: Image Based - images on separate email' + @crlf + N'Image based or address: Image-based'),
                (N'AX', N'Inbox: Info' + @crlf + N'Solicitors Instructions: Usually always repairable - occasion TL have to use AX Salvage Matrix' + @crlf + N'Images location: Always from the garage' + @crlf + N'Image based or address: Image-based Assessment - Images will always come from the garage - will usually have them prior to doing report as AX want the report doing on a certain day' + @crlf + N'Sending Report: Fee note not separate - Copy in engineersreports@ax.com and p.mandy@oakwoodscotland.co.uk (except if request is Easdons and mentions Guardian or mentions James Claims)' + @crlf + N'ALSO send pages 1-4 back to garage - authority will come from AX'),
                (N'BC', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Rapid Claims (Huddersfield)' + @crlf + N'Sometimes instructions state PHYSICAL if so need to instruct Service Certainty' + @crlf + N'Complete Injury Claims (Wolverhampton)' + @crlf + N'Images location: Usually with instruction if not sufficient ask Ehjaz (Rapid Claims) via Whatsapp' + @crlf + N'Complete Injury Claims - Whats app group or images directly from Waseem (Complete Injury Claims)' + @crlf + N'Image based or address: Clients address and if unable to get good set from solicitors or Ehjaz - go image based' + @crlf + N'Complete injury Claims - confirm address when receive images from them' + @crlf + N'Sending Report: Fee note needs to be separate if complete injury claims job copy in completeinjuryclaims@hotmail.com'),
                (N'BAKER', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Most jobs come through Omar' + @crlf + N'Images location: Omar WhatsApp chat' + @crlf + N'Image based or address: Storage - Berkstone Motors RG10 9UR' + @crlf + N'Sending Report: Fee note separately. Send Omar a copy via Whatsapp'),
                (N'BLACK', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Instructions usually come in from Shazad' + @crlf + N'Images location: Foyez: If through Foyez he will provide address.' + @crlf + N'Javad/Samore Cars: Also sends direct jobs.' + @crlf + N'Client: Contact direct' + @crlf + N'Solicitors: Provide images with instruction - ask for location' + @crlf + N'Image based or address: Confirm address with client/source' + @crlf + N'If Javad/Samore then 30 Brewery Street, Aston B''ham B6 4JB' + @crlf + N'"Sixways" = Somstar Recovery B5 6JX' + @crlf + N'Sending Report: Fee note separate return to file handler'),
                (N'CASTLE', N'Inbox: Whatsapp' + @crlf + N'Solicitors Instructions: Always received in Whatsapp' + @crlf + N'Images location: Castle Accident Management - Whats App' + @crlf + N'Image based or address: Image-based' + @crlf + N'Sending Report: Send back to Castle on Whats app group'),
                (N'CW', N'Inbox: Andrew/Engineers' + @crlf + N'Solicitors Instructions: Details usually in email body' + @crlf + N'Images location: Usually in email' + @crlf + N'If the vehicle is at Dickson St Liverpool contact Rich Morrison on Whatsapp for images'),
                (N'DFD', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: PDF in email' + @crlf + N'If SMC, images/instruction may arrive separately.' + @crlf + N'If so, log separately and wait - don''t chase for images or instructions' + @crlf + N'Car2Go - as above don''t need to chase until after 7 days' + @crlf + N'Images location: SMC (they email estimate and images to engineers) Tarran way Moreton. If quiet, copy estimate into parts tab on EVA and do click through, if not, engineers will do as normal.' + @crlf + N'Nick @ Car2go (Whatsapp group for images and estimate by email - Location Car2go 220 Price street)' + @crlf + N'Car Claims - ask Andy for images' + @crlf + N'Plus any other adhoc requests - contact the client for images' + @crlf + N'Image based or address: Address in Email' + @crlf + N'If images from Nabeel - 219 Slade Lane, M19 2EX'),
                (N'FW', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Sixways - Most jobs come through Sixways' + @crlf + N'Veetec Motor Group - Images usually come with instruction' + @crlf + N'Images location: Sixways/Fairways WhatsApp chat or images from Solicitors (If so image based)' + @crlf + N'Image based or address: Storage - Somstar Recovery and Storage B5 6JX if not in their yard ask them to confirm address' + @crlf + N'Solicitors Images - Image based' + @crlf + N'Sending Report: Fee note does not need to be separate'),
                (N'GG', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Graham Coffey' + @crlf + N'Images location: Direct - request images and inspection location from Client (occasionaly solicitors will send)' + @crlf + N'Image based or address: Ask whoever sends images for the address if set from Solicitors are not good enough request from client' + @crlf + N'Sending Report: Fee note separate' + @crlf + N'' + @crlf + N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Raja /Fraz' + @crlf + N'Images location: Fraz - Request images from Fraz group (whats app) - most the time we have images from Fraz or Raja prior to instruction' + @crlf + N'Image based or address: Swade Storage - Higginshaw Lane OL2 6HW (They should tell us what storage address)' + @crlf + N'Britannia Street, Oldham, OL1 3QR' + @crlf + N'Sending Report: Fee note separate and copy in ontrack789@gmail.com'),
                (N'HTU', N'Inbox: Ben' + @crlf + N'Solicitors Instructions: Body of email' + @crlf + N'Images location: Always in the email' + @crlf + N'Image based or address: Address in instruction'),
                (N'KERR', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: In body of email' + @crlf + N'Images location: Varies' + @crlf + N'Image based or address: Varies on quality of images'),
                (N'KMR', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Street Cars - usually jobs are Streetrax Ltd jobs' + @crlf + N'Images location: Images usually with instruction' + @crlf + N'Image based or address: Usually listed in instruction' + @crlf + N'Street Cars, 16 Chorlton Street, M1 3HW (Carpark - buildings in background - city centre)' + @crlf + N'Storage Yard - Winders Way Salford M6 6BU or' + @crlf + N'Unit 7 Sedgley Park Trading Estate, Prestwich, Manchester M25 9WD' + @crlf + N'(Should say in KMR instruction)' + @crlf + N'If images are obviously Manchester Airport, then go image based' + @crlf + N'Sending Report: Fee note separate return to file handler' + @crlf + N'' + @crlf + N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Usually have everything with instruction - if not have KMR on whats app' + @crlf + N'Images location: Images with email' + @crlf + N'Image based or address: Storage - Unit 3 Broughton Road East, Salford Ind Estate, M6 6AQ' + @crlf + N'16 Chorlton Street, Manchester, M1 3HW' + @crlf + N'Sending Report: Send back with fee note separate'),
                (N'KBS', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Direct' + @crlf + N'Images location: Direct - request images and inspection location from Client (occasionaly solicitors will send)' + @crlf + N'Image based or address: Ask client for images if struggle to get good set can go image based' + @crlf + N'Sending Report: Fee note separate - Copy in info@knightsbridgesolicitors.co.uk' + @crlf + N'' + @crlf + N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Kabir' + @crlf + N'Images location: Kabir Jobs - whats app sometimes we already have images when instruction lands' + @crlf + N'Sending Report: Fee note separate - Copy in info@knightsbridgesolicitors.co.uk' + @crlf + N'' + @crlf + N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Expert Claims' + @crlf + N'Images location: Expert Claims (most of the time)' + @crlf + N'Image based or address: Expert Claims Whatsapp - if address is in stoke ask Expert claims for images first' + @crlf + N'Uttoxeter Road, Stoke on Trent, Staffordshire ST3 5LQ' + @crlf + N'Sending Report: Fee note separate' + @crlf + N'' + @crlf + N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Apex Hire' + @crlf + N'Images location: Apex Hire - whats app sometimes we already have images when instruction lands' + @crlf + N'Image based or address: 25 Camelot Way Birmingham B10 OND' + @crlf + N'Sending Report: Fee note separate - Copy in info@knightsbridgesolicitors.co.uk'),
                (N'LEX', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: All jobs come from Hackney Solutions' + @crlf + N'Images location: Hackney Solutions' + @crlf + N'Image based or address: Storage' + @crlf + N'HS Recovery & Storage LTD William St M12 5FX most of the time but maybe Green Street Storage Yard, Burnley - just type that in' + @crlf + N'Not in Storage' + @crlf + N'Claim 3000 M12 4AH (Check if it looks like outside office)' + @crlf + N'Sending Report: Fee note does not need to be separate but copy in credithire@hackneysolutions.co.uk'),
                (N'LPS', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Don''t do many jobs for them - jobs come via Omar' + @crlf + N'Images location: Omar WhatsApp chat' + @crlf + N'Image based or address: Storage - Berkstone Motors RG10 9UR' + @crlf + N'Sending Report: Fee note separately. Send Omar a copy via Whatsapp'),
                (N'MATT', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Letter of instruction by email' + @crlf + N'Images location: Castle accident management whatsapp' + @crlf + N'Image based or address: Image-based' + @crlf + N'Sending Report: Check with Castle first if they are ok for us to disclose report before sending to solicitors. Also send fee separate'),
                (N'MBH', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Most jobs come from Nabeel' + @crlf + N'Images location: Nabeel- whats app group' + @crlf + N'Image based or address: Storage - Parkers Autobodies, 4 Chapel Street, Manchester. M19 3QA or' + @crlf + N'219 Slade Lane Manchester M19 2EX' + @crlf + N'Sending Report: Fee does not need sending separately - copy in Alisonnorris@wigansolicitors.com'),
                (N'MP', N'Inbox: Desk' + @crlf + N'Solicitors Instructions: Images should come with instruction' + @crlf + N'Instruction Date (Use date email arrived not date on instruction)' + @crlf + N'Inspection Date (Use hand written date they want report doing on which will be the most recent date - thats what date they want inspection/report completing.' + @crlf + N'Images location: With instruction - if not, email them straight back.' + @crlf + N'Image based or address: Inspection address listed in report' + @crlf + N'Sending Report: Report without images on 1 document' + @crlf + N'Images as 1 document' + @crlf + N'Parts list as 3rd document' + @crlf + N'Copy in mdakri@montrealprestige.co.uk'),
                (N'YML', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: PDF in email' + @crlf + N'Images location: Whatsapp Mark Wilson' + @crlf + N'Whatsapp Y M Law' + @crlf + N'Image based or address: If Mark Wilson then Swinton Recovery and Storage, Hurlbutts Drive Queensferry, Deeside CH5 1SF' + @crlf + N'' + @crlf + N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: PDF in email' + @crlf + N'Images location: Whatsapp Mark Wilson or Whatsapp Y M Law' + @crlf + N'Image based or address: If Mark Wilson then Swinton Recovery and Storage, Hurlbutts Drive Queensferry, Deeside CH5 1SF'),
                (N'OAK', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Paul Mandy Garages' + @crlf + N'Images location: Request from garage if one is listed on instruction if not request from client' + @crlf + N'Image based or address: Inspection address - garage address if from garage usually or ask client to confirm if they provide' + @crlf + N'Sending Report: Fee note does not need to be separate - copy in p.mandy@oakwoodscotland.co.uk'),
                (N'QCL', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: QCL/Hackney solutions' + @crlf + N'Email from vd@complexreports.com' + @crlf + N'Images location: Hackney Solutions' + @crlf + N'Image based or address: Storage' + @crlf + N'HS Recovery & Storage LTD William St M12 5FX most of the time but maybe in another place that looks dodgier Green Street Storage Yard, Burnley, BB111RU' + @crlf + N'Not in Storage' + @crlf + N'Claim 3000 Cariocca Business Park, Hellidon Close, Manchester. M12 4AH (Check if it looks like outside office)' + @crlf + N'Sending Report: Fee note does not need to be separate but copy in credithire@hackneysolutions.co.uk (for Hackney jobs only direct jobs just go back to email instruction came from)' + @crlf + N'' + @crlf + N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Direct' + @crlf + N'Email from vd@complexreports.com' + @crlf + N'Images location: Direct - usually attached to email if need to chase we have a group on whats app - QC Law VD' + @crlf + N'Image based or address: Image based or Clients address (usually just check images)' + @crlf + N'Sending Report: Fee note does not need to be separate'),
                (N'QDOS', N'Inbox: Desk' + @crlf + N'Image based or address: Always Image-based Assessment'),
                (N'MOTORX', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Do not message customer for images. Whatsapp R1AM/MOTORX' + @crlf + N'Images location: R1AM- whats app group' + @crlf + N'Image based or address: Clients address' + @crlf + N'300 Biscot Road, Luton, LU3 1AZ (a terraced parade of shops)' + @crlf + N'47-49 Park Street, Luton, LU1 3JX (Carpark at the back)' + @crlf + N'(Ask them if can''t work it out)' + @crlf + N'Sending Report: Fee note does not need to be separate'),
                (N'RL', N'Inbox: Engineers' + @crlf + N'Images location: Usually for Bicycles - will come with phone number for storage company (I have mobile number in my phone so text Saraj - Supreme Drive 07483371748) - he will provide images and bike info' + @crlf + N'Image based or address: Confirm with whoever provides images' + @crlf + N'Sending Report: Fee note separate return to file handler'),
                (N'RELAY', N'Inbox: Whatsapp' + @crlf + N'Solicitors Instructions: No instructions - Customers name and accident date' + @crlf + N'Images location: Whatsapp Relay' + @crlf + N'Image based or address: Image-based'),
                (N'RJS', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Direct' + @crlf + N'Images location: Direct - request images and inspection location from Client' + @crlf + N'Image based or address: If not home address ask whoever provides the images for the location' + @crlf + N'Sending Report: Fee note separate - copy in engreport@robertjameslaw.co.uk' + @crlf + N'' + @crlf + N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Accident Specialist' + @crlf + N'Images location: Accident Specialist/Jazy - check board - they will confirm if images are from client or if in storage they will send' + @crlf + N'Image based or address: Storage - RH10 9NT (Priestley Way, Crawley)' + @crlf + N'Not in storage - ask Jazy if he has sent us the images if not ask whoever provided images (if looks like an office RH10 6AE Brighton Road)' + @crlf + N'Sending Report: Fee note separate - copy in engreport@robertjameslaw.co.uk and admin@accidentspecialist.co.uk' + @crlf + N'' + @crlf + N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Claim Specialists' + @crlf + N'Images location: Claim Specialists - images in whats app claim specialist group - most of the time will have images before the instruction - they will confirm address when images provided' + @crlf + N'Image based or address: Claim Specialist HQ - Kenilworth Road Luton LU1 1DQ occasionally the storage address will be Storage Yard, 363 Dunstable Road, LU4 8BX' + @crlf + N'Sending Report: Fee note separate - copy in engreport@robertjameslaw.co.uk and also - info@claimspecialists.co.uk and accounts@carhirespec'),
                (N'ROZZII', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Images with instruction' + @crlf + N'Images location: With instruction' + @crlf + N'Image based or address: Inspection address listed in report if ok if not image based?' + @crlf + N'Sending Report: Fee note separately'),
                (N'SS', N'Inbox: Andrew' + @crlf + N'Solicitors Instructions: Instructions come via Andy' + @crlf + N'Images location: Instruction will come from Andy usually with images sometimes we contact client' + @crlf + N'Image based or address: Inspection address - if instruction from Andy he will advise if images from client we will need to ask them.' + @crlf + N'Sending Report: Email back to solicitors and copy in mw@savasandsavage.co.uk'),
                (N'SBL', N'Inbox: Info' + @crlf + N'Solicitors Instructions: PDF in Email' + @crlf + N'Images location: Normally Audatex?' + @crlf + N'If the instruction mentions "C.A.R.S" then it''s Paul Mandy''s job, in which case we need to reach out to the garage for images/estimate' + @crlf + N'Image based or address: Image based'),
                (N'STALLION', N'Inbox: Not Applicable' + @crlf + N'Solicitors Instructions: In Whatsapp - Stallion Bodyworks' + @crlf + N'Images location: In Stallion Whatsapp' + @crlf + N'Image based or address: Address provided in Whatsapp' + @crlf + N'Sending Report: Via Whatsapp - fee is paid prior to report writing'),
                (N'SWAN', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Claim Specialists or Accident Specialist' + @crlf + N'Images location: Claim Specialists - images in whats app claim specialist group - most of the time will have images before the instruction - they will confirm address when images provided' + @crlf + N'Image based or address: Confirm with sources. Claims Specialists HQ is LU1 1BW' + @crlf + N'Sending Report: Fee note separate return to file handler'),
                (N'TP', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: PDF in email' + @crlf + N'Images location: Contained in email'),
                (N'TEN', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Sometimes images go to Andy on Whats App - if he has no record use number in instruction to request images' + @crlf + N'Images location: Andy whats app or client direct' + @crlf + N'Image based or address: Ask whoever provides images to confirm' + @crlf + N'Sending Report: Fee note separate return to file handler'),
                (N'TA', N'Inbox: Andrew' + @crlf + N'Solicitors Instructions: Go direct to Andy usually image based but Andy will tell you' + @crlf + N'Images location: Image based - images with email' + @crlf + N'Image based or address: Image Based - most of time' + @crlf + N'Sending Report: Send back with fee note separate'),
                (N'WIL', N'Inbox: Andy to Engineers' + @crlf + N'Solicitors Instructions: Word doc in Email' + @crlf + N'Images location: Requested from claimant via Whatsapp' + @crlf + N'Image based or address: Address'),
                (N'WLS', N'Inbox: Engineers' + @crlf + N'Solicitors Instructions: Complete Injury Claims' + @crlf + N'Images location: Complete Injury Claims - Whats App group or direct from Waseem - complete injury claims' + @crlf + N'Direct - request direct from client if solicitors don''t send images' + @crlf + N'Image based or address: Confirm location with whoever sends images' + @crlf + N'Sending Report: Fee note separate send back to handler if it is a complete injury claims job copy in completeinjuryclaims@hotmail.com'),
                (N'ZEN', N'Inbox: Engineers' + @crlf + N'Images location: Foyez on WhatsApp - confirm location, may be Road Solutions Yard 2 Kiln Lane B''ham B25 8HF' + @crlf + N'Image based or address: Inspection address' + @crlf + N'Sending Report: Fee note separately'),
                (N'PCH', N'The report is named "[reg] Initial" or "[reg] Supplementary".'),
                (N'MIDAS', N'Inbox: Engineers'),
                (N'SIX', N'Inbox: Engineers'),
                (N'SWADE', N'Inbox: Engineers'),
                (N'WALKER', N'Inbox: Engineers');
            IF EXISTS (
                SELECT 1 FROM @notes n
                JOIN dbo.Principals p ON p.Code = n.Code
                JOIN dbo.Organizations o ON o.Id = p.OrganizationId
                WHERE CHARINDEX(n.NoteText, ISNULL(o.NotesOnEveryCase, N'')) = 0
                  AND LEN(ISNULL(o.NotesOnEveryCase, N'')) + 4 + LEN(n.NoteText) > 2000)
                THROW 50000, N'A Principal''s Notes on every Case cannot take its SOP text. Shorten the notes before this migration.', 1;
            UPDATE o SET NotesOnEveryCase = CASE WHEN o.NotesOnEveryCase IS NULL OR LTRIM(RTRIM(o.NotesOnEveryCase)) = N'' THEN n.NoteText ELSE o.NotesOnEveryCase + @crlf + @crlf + n.NoteText END
            FROM dbo.Organizations o
            JOIN dbo.Principals p ON p.OrganizationId = o.Id
            JOIN @notes n ON n.Code = p.Code
            WHERE o.NotesOnEveryCase IS NULL OR CHARINDEX(n.NoteText, o.NotesOnEveryCase) = 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}

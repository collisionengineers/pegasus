# Normalized Companion: handover.docx

- Raw source: `docs/reference/raw/collisionrelateddocs/collision_releated/handover.docx`
- SHA256: `9873bdd8f79bc76534a4108fac70c708fee7d5f07ab28500831727f22213e673`
- Extraction method: python-docx-paragraphs-tables
- Extraction confidence: partial-review-required
- Blocker: Headers, footers, text boxes, and embedded objects require later OOXML/manual QA

This companion is a derivative working copy. The raw source remains the source of truth.

---

Matty Handover Document for Alex

The Job Sheet

Where The Job Sheet is Stored

The job sheet is stored on the OneDrive/SharePoint account connected to digital@collisionengineers.co.uk

SharePoint is exactly the same as OneDrive, but it is just what Microsoft calls it when you have a business account.

As long as the digital@ Microsoft account remains, its OneDrive/SharePoint storage will also remain, and the sheet will still be accessible.

The Job Sheet must be opened in Excel Desktop (not the web version) as the VBA (code) will not run on the web and this will cause syncing issues.

Opening The Job Sheet

The Job Sheet must be opened in Excel Desktop not through the browser for all users, this is because the sheet uses VBA (code) which is not supported online.

To open The Job Sheet 2026 in Excel Desktop, the easiest approach is to create a shortcut directly to it.

To create a shortcut, right-click the Desktop, click New > Shortcut, then paste the following:

ms-excel:ofe|u|https://collisionengineers-my.sharepoint.com/:x:/g/personal/digital_collisionengineers_co_uk/IQC4rYl0eFZSTrt5QpnwNKPDAZbZcw0qQGD81wmUUEgC8nU?e=tENYqR

Name the shortcut anything and hit finish.

The Folder Setup

The folder creator on the sheet relies on all users having the same Z drive. Currently, the Z drive is just a shared folder on one of the office PCs.

Folder Setup Guide

For a folder to become a network drive and be accessed by other PCs on the same local network, it must first be shared.

To share a drive, in Windows settings, "File and printer sharing" must be enabled for “Private networks”.

"Password-protected sharing" must also be enabled for “All networks”.

The Windows account must also have a password set.

You must then right-click the folder you intend to share, go to the Sharing tab, and click “Advanced Sharing…”. At this point, you must configure the settings like below.

To map a drive on a connecting PC, you right-click “This PC” in file explorer and click “Map network drive…”, this allows you to add a location on the same local network and designate the drive letter (Z).

The formatting for the network drive is very specific, see below for an example.

You will also be required to enter the username and password of the host PC to complete adding the network drive – this will be the original username if the username has changed (original username can be found by checking C:\Users).

For example:

Network location:

\\PGUSER\Report Images

Username:

PGUSER3945

Password:

password

‘Fixing’ The Job Sheet – Conditional Formatting

To check that the conditional formatting is still working, click Conditional Formatting > Manage Rules, and change the selection to ‘This Worksheet’.

All you need to check here is that there are four rules, and the “Applies to:” column is exactly as below (=C:C, =C:C, =I:I, =D:D).

(the $ are auto added when you click apply - they don’t matter)

The “Applies to:” column is the only thing that is likely to break, and it breaks as a result of moving cells by dragging them or pasting data from external sources instead of special pasting without formatting.

‘Fixing’ The Job Sheet – General Help

You cannot add or remove a column.

Below is an image you can reference for how the columns should be:

The “Add New Row” text must be in column I – if it’s in any other column, it won’t work.

Editing The Job Sheet – Technical Details

The three main functions in the VBA are “CreateNewRow”, “FolderCreator”, and “RowMover”.

If adding or removing columns, all the functions must be updated so any references don’t break.

The “Table Locators” are expected to be in column AA (T1_START, T2_START).

The “Icon Templates” are expected to be on row 6 (AB6 etc.)

Backup Copy of The Job Sheet

In case, for some reason, The Job Sheet becomes permanently lost, I have uploaded a backup copy below.

This backup must not be edited online and must be downloaded, making any changes to the file online will corrupt the backup.

To use this file collaboratively, it must be uploaded to SharePoint and all users must be given access. It then must be opened in Excel Desktop by all users for the VBA (code) to work. No other setup will work for this specific sheet.

https://docs.google.com/spreadsheets/d/1RoFTcYuH0tVLbGUm0TCrQNCCjI_oxKcL/edit?usp=drive_link&ouid=101805445052387758622&rtpof=true&sd=true

Backup Copy of Conditional Formatting

Below are backups of the conditional formatting rules in case they become lost. Every rule uses the “Use a formula” method.

https://drive.google.com/file/d/13VARxYvPutfoKDbBSF4qZugWgmjSUPUw/view?usp=drive_link

CE Document Mapper

Where the Code is

All recent development of the mapping tool took place within the Claude chat named “CE Document Mapper” for the digital@collisionengineers.co.uk Claude account.

All versions of the Document Mapper are stored in Matty Files\CE Document Mapper as well as compiling instructions and a backup copy of the latest ‘providers.json’.

Where Settings are Stored

The tool creates a folder in the user’s Documents folder named “CE Document Mapper”. Within that folder, it creates:

An “app_settings.json” file which simply stores the last window state.

A “providers.json” file which stores all mapping presets.

In most cases, I have also stored the portable exe in this folder for tidiness.

How it Works

The tool imports text documents and uses hardcoded logic to extract text.

The tool accepts importing .pdf, doc, .docx and .msg files (.msg is Microsoft’s file format for storing offline copies of emails from Outlook (Classic)). It imports the files and scans for plain text.

It then applies all of the logic from the “providers.json” file, which contains a list of presets.

This includes first identifying which preset to use by searching for any ‘detected phrases’. If the tool is able to identify which preset to use (the work provider), it then applies the user-created mapping preset for that work provider to extract data for each field.

Image Extraction

The tool has an image extractor which can extract images from any .pdf, .doc, or .docx that is imported.

Batch Processing

The tool has a “batch process” mode which comes into effect if multiple files are imported at once.

Audit Mode

Audit mode relies on the “Engineer Report” tick box within the provider settings.

If a document is imported and is detected as belonging to a work provider which has the “Engineer Report” box ticked in the provider settings, the tool goes into “Audit Mode” and overwrites fields in the detected fields panel instead of ‘starting fresh’. Audit mode will not work if no other document was imported before the engineer report (or at the same time as it) because there is nothing there to overwrite.

The mode exists so that, when doing an audit, we can copy most of the information from the instruction but still use dates and mileage from other engineer report.

The current work providers with the “Engineer Report” box ticked are CNX and EVA.

If two documents are imported at once and one is an engineer report, the tool will automatically recognise this and process them in the correct order.

You will know when ‘Audit Mode’ has kicked in because the overwritten fields in the UI will be outlined in red.

OCR

The tool will OCR a PDF if it has a single image on each page and is less than 3 pages long (1 or 2 pages only). The reason for this cutoff is because some work providers send vehicle images as PDF files with a single image on each page, and so we don’t want these to be OCR’d as that would be highly inefficient (if a user wants to extract images).

Mapping Methods

The current mapping methods implemented in the tool are:

Single Label

Looks for the user’s label then takes any text after the label on the same line. If the rest of the line is empty, it takes text from the next non-empty line.

Name: John Smith

Name:

John Smith

Two Labels

Looks for the two user-defined labels then takes any text in between them. This is a strong approach if labels consistently appear.

Vehicle:

Toyota Prius - BF16 FOT

Fixed Position

Takes any text on a specific line number (this method is likely to break so should be used sparingly).

0001 │ New inspection request for Collision Engineers (51030)

0002 │

0003 │ 05 May 2026

0004 │

Fixed Position + Label

A combination of Fixed Position + Single Label (this method is likely to break so should be used sparingly).

Vehicle:

Toyota Prius - BF16 FOT

Single Label +/-

This method looks for a user-defined label, counts forwards or backwards a defined number of lines, then takes any text on the target line. The counter ignores empty lines. This method is useful for tricky fields on OCR’d documents. This is especially the case for OCR’d documents that contain a table as it can be unpredictable how the table renders after OCRing.

0033 │ Clients Vehicle

0034 │ Third Party Vehicle

0035 │

0036 │ Vehicle Reg:

0037 │ KW18ZWE

0038 │ Vehicle Reg:

0039 │ SL66ZPP

Email Date

This is a special mapping method specifically designed for pulling dates from .msg emails. The method looks for the user-defined label (should probably always be “Date:” anyway) to locate the date and then constructs a DD/MM/YYYY date from text. This may need amending in future if an email body containing “Date:” causes conflicts.

Cc: "alan.mctaggart@icloud.com" <alan.mctaggart@icloud.com>;	"jm@vehicleresolutions.co.uk" <jm@vehicleresolutions.co.uk>

Date: 2026-03-27 16:32:12+00:00

Good afternoon,

Manual Input

This method doesn’t perform any mapping action and simply uses the user-defined text, copying it directly into the output for the field.

AI Subscriptions

Copilot – Query Response Drafter

We have a custom Copilot agent designed to respond to queries (“QRD v2”). This agent is on the digital@collisionengineers.co.uk Copilot account which has an M365 Copilot Premium subscription. This is the only account with a Copilot subscription.

We chose to house the query response agent on Copilot specifically because Copilot was the only place that would allow the AI to access all 3 of our shared inboxes at the same time.

The digital@collisionengineers.co.uk email is set up to have delegated access to the desk@collisionengineers.co.uk, engineers@collisionengineers.co.uk, and info@collisionengineers.co.uk inboxes. This means that, when a Copilot agent under the digital@ account is given access to “My Emails”, the agent is able to search all 3 of the shared inboxes as well.

At the time of writing, the SI has to explicitly give permission and tell the model to search these inboxes, otherwise it will struggle to search them (it needs to be written explicitly in the model instructions).

As a result of this delegated access email setup, it is highly recommended to not add digital@collisionengineers.co.uk to Outlook (Classic), as we have encountered syncing issues with using delegated access and Outlook (Classic) at the same time. All users who had a mailbox with delegated access added in Outlook (Classic) were not able to sync incoming and outgoing mail without restarting the application every time. This issue may have been a result of TheFarmFactory not configuring things correctly, or it could be unavoidable when using delegated access in Outlook (Classic), I’m not sure.

base44

base44 is a platform to build web-based apps and websites. On base44 we currently have:

The current (live) Collision Engineers website

The WIP CarClaims website (the current live site is hosted by TheFarmFactory)

The company leave calendar (LeaveFlow) (managed by Ed)

An AI damage assessment platform (A.N.D.I.E) (WIP)

Claude

We have a Claude team plan with 5-6 seats. On Claude we currently have:

Andy’s AI Assessment Bot (originally in Andy’s account but now a shared project)

CE Document Mapper (in the digital@ account)

Figma

Whiteboards

There is one whiteboard for each company on the Figma account. I use these whiteboards for mapping processes and planning strategies.

The CarClaims whiteboard is mostly all marketing.

The Collision Engineers whiteboard contains a workflow map of some processes which may be useful.

CE CMS Design

There is a design document named “CE CMS Design” which was a rough sketch of how a proprietary Collision Engineers Case Management System might appear. This is not very developed/fleshed out, this was simply some initial ideas.

Copy of TFF Design

There is a design document named “Copy of TFF Design” which is a website design I made for a Google Ads landing page. This was sent to TheFarmFactory and shared with them (editing permissions) so they could implement it on the website. We later decided to build the site ourselves on base44, but at the time of writing this is still a WIP.

EVA

Installation

We have a copy of the EVA installer from a while ago, this installer is “EvaSetup.exe” in Matty Files\EVA Installation.

When launching for the first time you will be prompted to configure the network settings using a quick code – the quick code for our organisation is 22571 (hit tab after entering the code to autofill the remaining fields).

After entering the quick code and proceeding, you will also be prompted to install Minotaur Software PDF Printer (if you don’t see the window, it may be hidden behind the main EVA window). You must install Minotaur Software PDF Printer as it’s essential.

Finally, you will also see the Update Available notification on the homepage after doing all of the above – you must update to the latest version before you can begin using EVA.

We have a cloud backup of the installer in case it is ever lost here:

https://drive.google.com/drive/folders/1Y_1g1213pJFM9Rg_2FB-B_zwjHsN54_9?usp=drive_link

User Guide

There is a detailed guide on how to set up jobs manually in EVA in Matty Files\EVA User Guide.


## Table 1

Field | Method | Config

VRM | Single Label | Name:


## Table 2

Field | Method | Config | Config

Vehicle Model | Two Labels | Vehicle: | -


## Table 3

Field | Method | Config

Instruction Date | Fixed Position | 3


## Table 4

Field | Method | Config | Config

Vehicle Model | Fixed Position + Label | Vehicle: | -


## Table 5

Field | Method | Config | Config

Vehicle Model | Single Label +/- | Third Party Vehicle | +2


## Table 6

Field | Method | Config

Instruction Date | Email Date | Date:

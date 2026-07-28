# Work Package 08 - Box Storage and File Handling

## Purpose

Replace scattered local/network file handling with controlled Box storage and file metadata while preserving original evidence.

## Source files

- `EVA User Guide.pdf`
- `handover.docx`
- `Backup of CE Job Sheet 260429.xlsm`
- `collision_project_context_pack.zip` / `06_BOX_STORAGE_AND_METADATA.md`
- `phase_new_system.md`
- `phase_bespoke_system.md`

## Storage principles

1. Preserve original files exactly as received.
2. Store generated files separately from source files.
3. Use metadata rather than filename alone to identify case relevance.
4. Record Box folder and file IDs in the case database.
5. Use checksums to avoid duplicate uploads.
6. Make file operations idempotent.
7. Do not grant broad Box access to engineers if signed links or restricted portal views are possible.

## Recommended folder structure

```text
Collision Engineers Cases/
  [Year]/
    [Provider]/
      [CaseRef]_[VRM]/
        01_Source_Email/
        02_Instructions/
        03_Images/
        04_Estimates/
        05_Engineer_Pack/
        06_Reports/
        07_EVA_Payloads/
        08_Notes_And_Chasers/
        99_Archive/
```

Alternative: if existing Box conventions are already fixed by provider codes from the `Principals` sheet, retain the existing convention and add metadata rather than forcing a disruptive folder change.

## Step-by-step implementation

### Step 1 - Confirm Box root and naming rules

1. Identify the production Box root folder.
2. Confirm whether folders are currently named by internal reference, provider reference, VRM, or provider code.
3. Confirm whether provider-specific Box Code from `Principals` sheet is mandatory.
4. Confirm whether files need to be visible to engineers through Box directly or through the platform.
5. Record final folder convention.

### Step 2 - Build Box adapter

Adapter functions:

- `find_or_create_case_folder(case)`
- `upload_source_file(case, document)`
- `upload_generated_file(case, file_type, content)`
- `set_file_metadata(file_id, metadata)`
- `get_signed_or_restricted_link(file_id)`
- `list_case_files(case_id)`
- `retry_failed_upload(upload_job_id)`

### Step 3 - Define metadata

Minimum metadata:

- Case ID.
- Work Provider.
- VRM.
- Reference.
- Document type.
- Source email ID.
- Original filename.
- Checksum.
- Received date.
- Uploaded date.
- Uploaded by.
- Parser status.
- Review status.

### Step 4 - Upload original evidence

For each source email or manual upload:

1. Save source metadata.
2. Calculate checksum.
3. Check if identical file already exists for the case.
4. Upload original file.
5. Apply metadata.
6. Store Box file ID and URL.
7. Emit audit event.

### Step 5 - Handle generated files

Generated files include:

- EVA-ready JSON payload.
- Engineer pack PDF or HTML summary.
- Extracted/ordered images if image processing is applied.
- Chaser record.
- Report/invoice support files.

Rules:

1. Store generated files separately from source evidence.
2. Record generator version.
3. Record source document IDs used.
4. Preserve older generated versions unless retention policy says otherwise.

### Step 6 - Image handling

EVA guide requires a specific photo order: first an overall vehicle image showing the registration, then a close-up damage image, then all images including those first two. The system should therefore support:

1. Image extraction from PDFs/DOCs/DOCXs where present.
2. Manual image ordering.
3. Suggested first-two image selection.
4. Duplicate first-two in EVA pack/submission logic where required.
5. Image quality flags: no VRM visible, blurry, reflection issue, irrelevant image, non-vehicle image.

### Step 7 - Failure handling

For failed Box operations:

- Mark document as `storage_failed`.
- Keep temporary file only for a defined short period.
- Queue retry.
- Show case on exception dashboard.
- Allow user to retry or upload manually.
- Never mark case as ready for EVA if required evidence is not safely stored.

## Acceptance criteria

1. Every source file has a Box file ID or explicit storage failure flag.
2. Every generated file records its source case and generator version.
3. Duplicate files are detected by checksum.
4. Engineers can access required evidence through approved access mechanism.
5. Case detail page can list all files without manually searching Box.
6. File upload failure is visible and recoverable.

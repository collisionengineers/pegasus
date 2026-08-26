# Proof — UIIMP-002

## Verified target

- PR: https://github.com/collisionengineers/pegasus/pull/556
- PR state: `MERGED` at 2026-08-26T13:29:29Z
- Merged `dev` commit verified: `0140e236c9156cff16086f6a9e61311fe20f2463`
- Verification ran from the clean UIIMP-002 ticket worktree detached at that exact commit. The user main checkout and Kanmer board worktree were not changed.
- Deployment: `n/a`; this is isolated design documentation and local HTML, not a deployable change.

## Automated evidence

### Catalogue contract

Command:

```powershell
./scripts/Test-UiCatalogue.ps1
```

Result:

```text
Test UI catalogue valid: 52 routed sources, 60 prototypes, 0 broken local references.
```

This verifies route coverage, prototype inventory, local references, source uniqueness, and catalogue isolation rules at the merged commit.

### Documentation and repository hygiene

Commands:

```powershell
./scripts/Test-DocumentationLinks.ps1
./scripts/Test-MarkdownPlacement.ps1 -Base 1a8fda3e244c993c24c52f731e8c5027dcc4d4dc -Head 0140e236c9156cff16086f6a9e61311fe20f2463
[System.Management.Automation.Language.Parser]::ParseFile('./scripts/Test-UiCatalogue.ps1', ...)
git diff --check 1a8fda3e244c993c24c52f731e8c5027dcc4d4dc 0140e236c9156cff16086f6a9e61311fe20f2463
```

Results:

- All relative Markdown links resolve: 200 files checked.
- Markdown placement passed.
- `Test-UiCatalogue.ps1` parses without PowerShell errors.
- `git diff --check` passed with no output.
- The merged tree contains 61 HTML files: one catalogue index plus 60 prototypes.
- A focused `rg` check found no `docs/design/test-ui` or `test-ui` reference from project/build inputs or `scripts/Build-ReleaseArtifacts.ps1`.

### Locked restore and Release build

Commands:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
```

Result:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Visual evidence

Headless Chrome opened the exact merged local `file:` pages with file access enabled and produced fresh captures for:

- authenticated `dashboard--default.html` at a 640x450 CSS viewport with device scale factor 2 (200% scale/reflow);
- navless-auth `sign-in--default.html` at 1280x900;
- external `upload--default.html` at 1280x900.

All three captures were inspected in this verification run. The real Pegasus stylesheet and marks loaded; the shells, navigation, headings, forms, content cards, and responsive dashboard layout rendered without missing-asset indicators or overlap. The dashboard remained navigable at 200% scale, the sign-in focus treatment was visible, and the external upload surface retained its expected two-column layout.

The validator separately proves all 60 catalogue links and their local assets resolve. Static HTML does not and must not claim server handlers, authentication, upload, redirect, download, or business-policy execution.

## Acceptance result

- Every current routed Razor source is classified: pass.
- Every visual route has indexed local HTML state evidence: pass.
- All linked prototype files and assets resolve: pass.
- Representative authenticated, navless-auth, external, and 200%-scale renders are visually sound: pass.
- No application or release input consumes Test UI: pass.
- No deployment was required or performed: pass.

UIIMP-002 is verified complete on merged `dev` commit `0140e236`.

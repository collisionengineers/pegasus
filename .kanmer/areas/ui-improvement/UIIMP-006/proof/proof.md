# Proof — UIIMP-006: Rewrite the design authority to the Integrated Operations Workspace

## What was verified, and where

Verified on merged `dev` at `b92cb9a7`, in the primary checkout
`C:/Users/PC/Documents/GitHub/pegasus`, on 2026-08-29. The ticket's three
recorded commits `b5cb2edd`, `932e0e64` and `9be74a48` are each reachable on
`dev` (`git merge-base --is-ancestor <sha> dev` → exit 0 for all three), landing
through merge commit `3614d63a` ("Merge pull request #587 from
collisionengineers/task/uiimp-006-design-authority"), itself reachable on `dev`.
The change is one file: `docs/design/README.md`, `+1089 / −942`, now 1,486
lines. No other path is touched, matching the ticket's `Owns` clause.

## Evidence

### The document exists on merged `dev` and is the rewritten authority

Tier: build/test (repository content on merged `dev`)

```
git show --stat 3614d63a
 docs/design/README.md | 2031 +++++++++++++++++++-----------------
 1 file changed, 1089 insertions(+), 942 deletions(-)

git show dev:docs/design/README.md | wc -l   -> 1486
```

### Every clause the ticket names has an owning heading and real content

Tier: build/test (heading inventory of `dev:docs/design/README.md`)

| Ticket clause | Owning heading (line) | Verified content |
| --- | --- | --- |
| 220px rail, dark utility bar, tab strip, 1580px content | `### Authenticated shell` (108) | "a 220px sticky `.app-rail`"; "The **utility bar** is dark and sticky"; "The **workspace-tab strip** … at most four, least-recently-used evicted"; "`.content`, capped at 1580px and centred" |
| Nav order and labels | `### Authenticated shell` (108) | "Work Centre (`/`), Inbox [count], Upload, Cases (`/cases`) [count], Search (`/search`), Operations [count]" then "Administration — rendered for Administrators only" |
| Count sources, absent-never-zero | `### Authenticated shell` (108) | "**A rail count is a figure a page already queried**"; Cases = `not_ready + review + with_engineer + held + triage + unidentified`; "An absent count renders nothing at all" |
| Token table | `## Tokens` (299), `### Colour` (307) | 24-row token table sourced from `html[data-design="integrated"]`, verified 2026-08-28 |
| Vendored Inter, licence + SHA-256 | `### Typography` (353) | Two-row table: `InterVariable.woff2` / `InterVariable-Italic.woff2`, SIL OFL 1.1, `fonts/inter/LICENSE.txt` checksum included |
| Class vocabulary and chip tones | `## Component map` (737), `### Colour` (307) | Shell/Page/Record/Dialog class lists; six-row `.status--*` tone table |
| Lucide set + the five added glyphs | `### Icons` (483), mapping table (513) | Sixty-glyph sprite; `activity`, `spark`→`sparkles`, `reply`, `flag`, `sort`→`arrow-up-down` each rowed as "(undefined in the prototype)" with a per-glyph SHA-256 |
| Route map and 301 stubs | `## Routes` (710) | 14-route table; "Route moves are 301 stubs delivered by PLAT-029 and deleted in wave 5" naming all three |
| Breakpoints | `### Spacing, layout and breakpoints` (399) | 1360 / 1180 / 1100 / 980 / 900 / 760 — all six, each with its reflow |
| CSP rule and utility classes | `### Utility classes` (806) | "The Content Security Policy forbids inline styles" plus the thirteen named utilities |
| Keyboard / dialog contract | `### Keyboard and dialog contract` (164) | Ctrl K / Ctrl U / Ctrl N / Ctrl S / F5 / Arrow / Escape table; focus trap, `inert`, focus return |
| Amended disabled-versus-absent (D7) | `## Absent versus disabled` (689) | "**Amended 2026-08-28 (D7).**" with the seam table: Experian→ENG-001, Glass's/Audatex→EXT-09, Cazana→ENG-008 / ENG-009 |
| Removed surfaces | `## Removed surfaces` (1051) | Six bullets covering group §1.14 incl. `/VehicleImages` list (D1) and the folded admin areas (D2) |
| Prototype defects as reviewed divergences | `### Prototype defects, not reproduced` (1071) | Eleven-row table transcribing group §1.15 |

The per-page contract (group `context.md` §1.1–§1.15) is transcribed under
`## Workspace contract` (830) with an owning heading for each: Work Centre
(837), Inbox (852), Cases (875), Triage (891), Unidentified (902), Search
(911), Case workspace (923), Assessment (970), Upload (991), Operations
(1000), Administration (1014), External frames (1044). Sampled sections carry
the full transcription, not stubs — Work Centre names the five metrics, both
panes and all five work-item kinds; Operations names all five panels and the
D5 job kinds with the D6 `automation.jobs` scope.

### The two factual fixes the ticket required

Tier: build/test (repository content on merged `dev`)

Logo mapping. The README now states "`_Layout.cshtml` does **not** use it; the
authenticated rail carries the `pegasus-lockup` mark", and the mapping table
attributes the file to `_LayoutExternal.cshtml`. Confirmed on `dev`:

```
git grep -n "logo_no_margin" dev -- src/Pegasus.Web/Pages/Shared/
  dev:.../_LayoutAuth.cshtml:22      <img src="~/images/logo_no_margin.png" … alt="" />
  dev:.../_LayoutExternal.cshtml:20  <img src="~/images/logo_no_margin.png" … alt="Collision Engineers" />

git grep -n "logo_no_margin" dev -- src/Pegasus.Web/Pages/Shared/_Layout.cshtml
  (no match)
```

Four unplaced marks. The README says `activity`, `brand`, `calendar` and
`casefolder` are "**Not in the tree** — no destination, no checksum".
Confirmed:

```
git ls-tree -r --name-only dev | grep -Ei "marks/(activity|brand|calendar|casefolder)"
  (no match)

git ls-tree --name-only dev src/Pegasus.Web/wwwroot/images/marks/
  README.md access.png accounts.png automation.png checkmark.png
  configuration.png mailboxes.png organisations.png pegasus-lockup.png
  principals.png roles.png
```

### The recorded checksums are the bytes on `dev`

Tier: build/test (independent SHA-256 of the committed blobs)

Every checksum I sampled from the README's mapping tables matches the blob on
`dev` exactly:

```
git show "dev:<path>" | sha256sum
  fonts/inter/InterVariable.woff2         693B77D4…29A8E3   matches
  fonts/inter/InterVariable-Italic.woff2  E564F652…7A262A   matches
  images/lucide-sprite.svg                90FEB7AB…7A0992   matches
  images/logo_no_margin.png               E7247BE4…2C63E2   matches
  docs/design/brand/logos/logo_no_margin.png  E7247BE4…2C63E2   matches (byte-identical copy, as stated)
  images/marks/pegasus-lockup.png         938C22B0…140EF0   matches
```

### The document has real production consumers

Tier: registration for the citations; build/test for the code that implements
what the document states. Nothing here is deployed.

The design authority's "callers" are the documents and code that cite and
implement it. On merged `dev`:

- `src/Pegasus.Web/wwwroot/css/site.css:6` — "The design authority is
  `docs/design/README.md`; the tokens, breakpoints and every class below are
  the contract wave-2 page ports build on." The stylesheet then declares the
  README's exact values: `site.css:34` `--rail:220px;--content-max:1580px;
  --gap:12px;--page-pad:18px`, `site.css:31` `--red:#c9222b;--red-dark:#9e1720`,
  `site.css:34` `--focus:#d3232a`. All six breakpoints exist as media queries
  at `site.css:751, 755, 763, 770, 790, 799` (1360/1180/1100/980/900/760). All
  thirteen named utility classes and all six `.status--*` tones are declared.
- `docs/frd/frd-12-operator-experience.md` cites it six times, including
  `:21` → `#authenticated-shell`, `:66` → `#no-explanatory-copy-and-page-economy`
  and `:419` → `#test-ui`. The `#authenticated-shell` anchor — a heading this
  ticket created — was taken up by UIIMP-007 in `b8b01479`, which is the
  clearest evidence the rewritten document is being consumed rather than
  merely shipped.
- `docs/capabilities.md:167` (UI-16) names "Design authority is
  [design § Authenticated shell](design/README.md#authenticated-shell)".
- `docs/index.md:23` routes "the Integrated Operations Workspace shell
  contract" to this document.
- `AGENTS.md:316,321` bind every UI change to
  `docs/design/README.md#no-explanatory-copy-and-page-economy`.

Every anchor cited from elsewhere resolves in the rewritten file. I derived
slugs from the shipped headings and checked each: `#authenticated-shell`,
`#no-explanatory-copy-and-page-economy`, `#test-ui`,
`#operator-experience-requirements`, `#enforced-presentation-rules`,
`#deferred-integration-and-intake-surfaces`, `#the-pegasus-marks`,
`#ui-specification`, `#contracts`, `#routes`, `#utility-classes`,
`#reviewed-divergences`, `#removed-surfaces` — all present.

### The shell rules the document states are the shipped behaviour

Tier: build/test (code on merged `dev`; no deployment evidence)

The count formula and the absent-never-zero rule are not aspiration. The
shipped filter composes exactly the README's sum:

```
src/Pegasus.Web/Presentation/RailCountsPageFilter.cs:70-75
    ["Cases"] = stages.NotReady
        + stages.Review
        + stages.WithEngineer
        + stages.Held
        + triageTask.Result.TotalCount
        + unidentifiedTask.Result.Count
```

and only the `Cases` key is ever populated, so Inbox and Operations render no
figure — the README's "the shell invents none". `_Layout.cshtml:28-30` reads
it back as `int?` and each nav link renders its `nav-count` span only under
`@if (CountFor("…") is { } count)` (`_Layout.cshtml:74, 87, 101`), which is the
absent-never-zero rule in code. The rail link order in `_Layout.cshtml:68-105`
is Inbox, Upload, Cases, Search, Operations, then Administration under
`@if (isAdministrator)` — the README's order and its Administrator-only rule.

### Sections the ticket required to be kept verbatim

Tier: build/test (section-by-section diff, `3614d63a^1` vs `dev`)

Four of the six are byte-identical:

```
Voice, labels and necessary copy        IDENTICAL
No explanatory copy and page economy    IDENTICAL
Accessibility                           IDENTICAL
Change and verification rule            IDENTICAL
```

Two changed, and I record that rather than call the clause satisfied:

- **Evidence discipline** — two bullets were reframed off the retired
  `0.1.0-alpha.1` inventory: "**Planned `0.1.0-alpha.1`** describes…" became
  "**Planned** describes … the Integrated Operations Workspace below", and the
  retained-raster / offline-QDOS-alpha `/Intake` paragraph became a statement
  that only the prototype's effective render layer is the contract. The five
  evidence-tier definitions themselves are untouched. This matches the ticket
  plan, which scoped the section as "Evidence discipline (framing)".
- **Test UI** — unchanged, with four lines appended stating what the
  catalogue's route keys become as each wave lands. This is an addition beyond
  the ticket body's literal "keep verbatim", though it contradicts nothing in
  the retained text. Recorded as a divergence, not a defect.

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| Every section of the new contract (group `context.md` §1) has an owning heading in the README | Proven | §1.1 → `### Authenticated shell` + `### Keyboard and dialog contract`; §1.2–§1.13 → the twelve `## Workspace contract` sub-headings listed above; §1.14 → `## Removed surfaces`; §1.15 → `### Prototype defects, not reproduced`. All fifteen accounted for |
| `scripts/Test-DocumentationLinks.ps1` passes | Proven | `pwsh -NoProfile -Command "./scripts/Test-DocumentationLinks.ps1"` → `All relative Markdown links resolve (129 files checked).` `EXITCODE=0` |

Solution build and test evidence for the `dev` state this proof is taken at is
the canonical gate record for `b92cb9a7`: `dotnet restore --locked-mode` exit
0; `dotnet build --configuration Release --no-restore` → "Build succeeded. 0
Warning(s), 0 Error(s)"; `dotnet test --filter
'Category!=Corpus&Category!=Browser'` → ArchitectureTests 100 passed,
Core.Tests 1133 passed, IntegrationTests 1022 passed / 2 pre-existing skips, 0
failed. That suite was run once by the orchestrating session and is not
re-run here. It is not this ticket's evidence in any strong sense — UIIMP-006
changes one Markdown file and no test asserts its content.

## Outstanding

- **Rendered conformance is unproven and is not this ticket's to prove.** This
  proof establishes that the document says what the ticket claims and that the
  shipped shell code agrees with it. It does not establish that any page
  renders to the contract, and no browser walk at 1580 / 1100 / 760 was run.
  That walk is owned by **UIIMP-010**, which has its tooling on `dev`
  (`tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs`).
- **Deployed tier: none.** Nothing in this ticket is deployed, and `main` has
  not been promoted. Every claim above sits at the registration or build/test
  tier.
- **Three source and test comments cite this README by line number, and those
  numbers are wrong on `dev`**: `src/Pegasus.Web/Pages/Administration/Automation/Activity.cshtml.cs:67`
  and `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs:183` cite
  "`docs/design/README.md:168`", and
  `tests/Pegasus.IntegrationTests/ApprovedMailboxAdministrationWebTests.cs:148,152`
  cite "line 160" and "line 168". The no-raw-identifiers rule they mean now
  sits at line 1414 (it was line 1185 before the rewrite), so these citations
  were already inaccurate before UIIMP-006 and remain so after it. The files
  belong to other lanes; UIIMP-006 correctly did not touch them. Needs a
  follow-up ticket to replace line numbers with anchors.
- **The logo "Current consumers" bullet names one of two live consumers.** It
  names `_LayoutExternal.cshtml`; on `dev`, `_LayoutAuth.cshtml:22` also embeds
  `logo_no_margin.png`. The mapping table's cell does cover it in prose ("the
  `auth-brand` of the sign-in card"), so the mapping is not wrong, only the
  bullet is less specific than the table. Cosmetic; worth folding into the
  wave-5 README pass rather than its own ticket.
- **The `Test UI` section's four appended lines** exceed the ticket body's
  "keep verbatim" instruction (detailed above). Accepted as harmless, recorded
  so it is not silently absorbed.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not
been promoted; the exact-SHA `dev` → `main` promotion happens at wave 5.

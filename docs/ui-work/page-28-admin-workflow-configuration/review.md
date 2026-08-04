# Page 28 — Administration / Workflow configuration: review

Source: `src/Pegasus.Web/Pages/Administration/Configuration.cshtml`.
Screenshot: `workflow-configuration.png`. Governing standards:
`../ui-standards-and-review.md` (§2 vocabulary, §4 presentation rules).

## 1. Aesthetics

- The lede — *"These versioned gates control whether a case can be assigned to an Engineer.
  This page contains no credentials, secrets, or cloud controls."* — ends by listing what
  the page is **not**. Self-negating narration is the clearest possible "tell, don't show"
  failure: a page never needs to disclaim contents it does not have, and "versioned gates"
  is engineering vocabulary in the first sentence.
- The "Current configuration" detail list prints **"Policy: case-workflow"** (an internal
  policy key) and **"Version: 1"** (an internal concurrency integer). Neither means
  anything to an administrator; both are banned raw identifiers (standards §4.4).
- The left column spends four tall rows saying "Required" four times against labels that
  are near-identical 6–8 word phrases ("Complete instructions before Engineer assignment",
  "Staff image review before Engineer assignment"…) — a wall of repetition for what is,
  in content, four booleans.
- The whole current state is then repeated a second time on the right as four checkboxes
  with *different* phrasings of the same four facts ("Instructions are complete" vs
  "Complete instructions before Engineer assignment") — two vocabularies for one concept
  on one screen.

## 2. Practicality

- The operator's question is "what must be true before a case can go to an Engineer, and
  can I change it?" The split detail-list/form layout answers it twice and therefore
  slowly. Checkboxes already display state; a separate read-only mirror adds scanning cost
  and a drift risk between the two phrasings.
- The gate labels do not state their business meaning as a rule. "Complete instructions
  before Engineer assignment" reads as a command to the operator rather than a condition
  on the case; the checkbox variants ("Instructions are complete") are better but
  ungrounded — complete before *what*? Only the fieldset legend ("Required before Engineer
  assignment") carries the sense, and it is visually subordinate.
- The stale-version failure (another administrator saved first) is handled only as a
  validation summary — the source comment admits *"A rejected save is most often a stale
  version"* — but nothing tells the operator the recovery in page copy; the knowledge
  lives in a Razor comment developers can read and operators cannot.
- There is no record of when the settings last changed or by whom, though every change
  requires a reason — the one piece of history an administrator would actually use.

## 3. Performance / Design / Good practice

- `ExpectedVersion` + `OperationKey` hidden fields: correct optimistic-concurrency and
  idempotency plumbing — keep, but stop displaying the version (standards §4.4).
- The success path uses `TempData["AdministrationStatus"]` in a status card — good pattern,
  already consistent with Mailboxes.
- Four checkboxes + one reason field is the right form shape (standards §4.8: one
  confirmation, a reason where policy requires one). No duplicated confirm pairs here —
  this page's form is structurally the healthiest in Administration.
- Accessibility is sound (fieldset/legend, labelled textarea); the redundancy is the issue,
  not the semantics.
- Button label "Save workflow configuration" repeats the page title rather than the effect;
  the effect is changing what is required before Engineer assignment.

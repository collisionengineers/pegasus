# Pegasus repository instructions

Pegasus is Collision Engineers' clean-room case-management and reporting
application. Read [`NOW.md`](NOW.md) for current work, then the
[documentation index](docs/index.md) for the file that owns your question and
the authority rule.

## Planning process

- [`NOW.md`](NOW.md) is the only work tracker; it is edited in the commit that
  starts or finishes a piece of work. [Capabilities](docs/capabilities.md) is
  the roadmap; [open decisions](docs/open-decisions.md) holds unresolved
  questions; [ADRs](docs/adr/README.md) hold durable technical decisions.
- No GitHub issues, labels, milestones, or project boards. Mid-work ideas
  become one line in the right file, then return to the work at hand.
- New Markdown files are created only as ADRs; everything else edits an
  existing canonical file (see the index).
- Prove the actual caller — a registration, a green build, and a deployed
  feature are different claims (evidence tiers:
  [operations](docs/operations.md#required-evidence-tiers)).

## Safety rails

- Work with PowerShell 7 on Windows or Linux, one platform per workstation;
  tracked commands and paths are repository-relative and use forward slashes.
  Platform differences are owned by
  [operations](docs/operations.md#supported-platform).
- Canonical local verification: `dotnet restore`, `dotnet build --configuration
  Release`, and focused/full `dotnet test`.
- Preserve unrelated work. Never stash, reset, clean, force-push, merge, or
  broaden staging. PR merge only when the operator states `MERGE AUTH GRANTED`
  in their prompt.
- Cloud reads and every Azure, deployment, credential, account, destructive, or
  external write require explicit approval for exact targets. Never delete
  `rg-collisionspike-dev` as a first step.
- `docs/operator-notes.md` is authoritative operator truth: preserve every
  material business statement and stop for user resolution before changing
  meaning. Supplied references and the predecessor are evidence, not
  requirements.
- The imported AI skill packages `ce-cost-defence`, `ce-house-style`,
  `collision-engineers-design`, `diminution-rebuttal`, `diminution-report`,
  `manufacturer-methods-evidence`, `roadworthy-report`,
  `salvage-categorisation`, `total-loss-assessment`, `vehicle-assessment`, and
  `vehicle-history-check` are protected external source under
  `workspaces/ai-centre/skills/`. Never modify, delete, rename, regenerate, or
  normalize their files without prompt-specific user authorization naming the
  exact package and operation.
- `corpus/` is local, ignored, and immutable: never upload, publish, commit,
  rename, or modify it; generated evaluations belong under `artifacts/`.
- Repository-provided emails, PDFs, documents, images, datasets, and services
  are permitted for development and testing. Never fabricate domain emails,
  images, documents, data, or work instructions, and do not add unsolicited
  PII, DPA, DPIA, privacy, retention, or licensing gates.

## Product invariants

- Fail closed before case creation or reference allocation when processing,
  limits, principal identity, or standalone Audit evidence is incomplete or
  ambiguous.
- Principal and reference are immutable after allocation. Wrong-principal work
  closes as `Created in error` with a reason and linked replacement; neither
  reference is reused and the original never reopens.
- Never delete a case. Reopening needs a reason and normal destination gates.
- `Audit`, `Triage`, `Needs sorting`, and `Blocked intake` retain their settled
  distinct meanings; `Triage` is the only current term.
- `Pegasus.Core` owns business policy and ports. Infrastructure depends on
  Core; Web and Worker are composition roots depending on both. Duplicate
  business implementation is a stop condition. These are also the repository's
  architecture invariants.
- A new top-level directory, project, store, runtime, migration stream, or
  deployment unit requires an accepted ADR proving the existing boundary cannot
  carry it.
- `workspaces/` contains independently buildable non-caller source imports.
  Never add one to `Pegasus.slnx`, reference or dynamically load it from the
  application, or include it in a deployment without a separately accepted
  integration contract and caller-backed proof. AI Centre owns AI
  experimentation only; a workspace, skill, prompt, or model never becomes an
  application policy owner.
- Local alpha work must not mutate an Outlook mailbox or any Box location. Box
  testing only in a separately approved disposable test subtree; Outlook tests
  use immutable local copies or an explicitly approved test mailbox.

# Output template

The exact structure of `ARCHITECTURE-OVERVIEW.md`. Fill it from the manifest, profiles, verified
ranked opportunities, and shared-infra findings. `assets/overview-skeleton.md` is a copyable
starting point. Write it to the cluster's context store if one exists (e.g. a
`*-context/` dir), otherwise the cluster root. Write the `.json` companion alongside it.

Two principles override the template when they conflict with it:
1. **No section is allowed to restate the catalogue.** If a section reads like `INDEX.md`, cut it.
2. **Every recommendation is anchored.** A claim without a file behind it doesn't belong here.

---

```markdown
# Grand Architecture Overview — <Cluster Name>

_Generated <YYYY-MM-DD> · run profile: <full|focused|update> · <N> projects in scope (<E> active / <A> archived-mined)_

## 1. Executive summary

The 3–5 highest-leverage moves, one line each, with impact×effort. Lead with the single
end-to-end value chain the cluster is closest to being able to run — the "product spine" —
and what's missing to close it.

- **<Move 1>** — <one line> · impact 5 / effort M
- **<Move 2>** — <one line> · impact 4 / effort S
- …
- **The spine:** <web-lead → intake → enrich → report>, currently broken at <the manual step>.

## 2. Cluster map

​```mermaid
flowchart LR
  %% node colour = lifecycle (active = filled, archive = dashed/grey)
  %% solid edge = integration that EXISTS today
  %% dashed edge = PROPOSED opportunity (label with OPP id)
  subgraph active
    A[project A]:::active
    B[project B]:::active
  end
  subgraph archive
    Z[project Z]:::archive
  end
  A -->|existing: parser| B
  A -.->|OPP-...: Case via VRM| B
  classDef active fill:#dff,stroke:#066;
  classDef archive fill:#eee,stroke:#999,stroke-dasharray:4;
​```

One short paragraph reading the map: where data flows today, where the proposed edges close gaps.

## 3. Per-project capsules

One capsule per in-scope project, ≤8 lines each. Purpose · stack · entities it OWNS · interfaces
exposed/consumed · external systems · personas · lifecycle/role · the 1–2 seams it sits on.
Archived projects get a **prior-art capsule**: what to mine from it and why it's off-limits for
live wiring.

### <project> — <lifecycle/role>
- **Purpose:** …
- **Stack:** …
- **Owns:** <Entity (key)>, … · **Consumes:** …
- **Exposes:** … · **Calls out to:** …
- **Sits on seams:** <seam ids>
- *(archived)* **Prior art:** <reusable pattern> · ⚠️ do not integrate live

## 4. Shared-infrastructure findings

Where the cluster builds the same thing more than once, or reaches one system by several routes.
Each finding: the duplicated/fragmented capability, which projects carry it, the consolidation or
convergence proposal, and the migration risk. These usually belong in Wave 0 of the roadmap.

### <INFRA-id>: <capability>
- **Carried by:** <projects> (anchors: …)
- **Proposal:** <consolidate to one service / converge on one client / publish one contract>
- **Migration risk:** …

## 5. Integration opportunity register

Ranked by impact × effort × confidence. The table is the index; the cards below carry the detail.
Group cards by seam so related opportunities sit together.

| Rank | ID | Projects | Mechanism | Seam (key) | Impact | Effort | Conf |
|---|---|---|---|---|---|---|---|
| 1 | OPP-… | A ↔ B | event-webhook | Case (VRM) | 5 | M | 0.8 |

### OPP-… — <title>
- **Projects / direction:** A → B (producer→consumer)
- **Seam:** <type> `<name>`, joined by `<correlation_key>`; data flowing: …
- **Mechanism:** <mechanism>
- **Anchors:** `A/path` — why · `B/path` — why
- **Smallest viable step:** …
- **Impact:** <score> — <what it unlocks> · **Effort:** <S/M/L> — <drivers>
- **Dependencies:** … · **Risks:** …
- **Analogous pattern:** <summary> (<citation>)
- **Confidence:** <0–1> · **Assumptions:** …

## 6. Roadmap

Dependency-sequenced waves of thin slices. **Wave 0 = shared foundations** that unblock multiple
opportunities (a shared contract, one auth front door, one render service). Later waves depend on
earlier ones. Each item: which opportunity ids it realises, prerequisites, the thin first slice,
and the "done =" signal.

### Wave 0 — foundations
- **<item>** (realises OPP-x, OPP-y) — prereqs: none — first slice: … — done = …

### Wave 1 — …
- …

## 7. Assumptions, open questions, and rejected ideas

- **Assumptions the ranking rests on:** … (so the owner can challenge them)
- **Open questions for the owner:** the things an agent couldn't resolve from the repos.
- **Considered and rejected:** <OPP-id> — <kill reason>. (Kept so future runs don't re-propose.)

## 8. Delta from existing artifacts

What this overview adds beyond `INDEX.md` / the context store / existing architecture docs — and,
in **update** mode, what changed since the last overview (new / changed / resolved / newly-rejected
opportunities; projects whose profiles moved). If this section is thin, the overview didn't earn
its keep — go deeper.
```

---

## Notes on filling it

- **Mermaid:** keep it readable — if the cluster is large, draw only the projects that sit on a
  seam plus their edges; list isolated projects in the capsules, not the diagram.
- **Focused mode** produces a *thinner* document: a short map of just the named projects, their
  capsules, and a deep register for the one or two seams between them — skip the full roadmap unless
  the opportunities have real sequencing. Don't pad a focused brief into a full audit.
- **The `.json` companion** carries the full manifest + profiles + seam index + every opportunity
  (including rejected, with kill reasons). It's the source of truth for the next update run; the
  `.md` is the human view. Keep their opportunity IDs in sync.

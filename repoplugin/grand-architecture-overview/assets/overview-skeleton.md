# Grand Architecture Overview — {{CLUSTER_NAME}}

_Generated {{DATE}} · run profile: {{full|focused|update}} · {{N}} projects in scope ({{E}} active / {{A}} archived-mined)_

## 1. Executive summary

<!-- 3–5 highest-leverage moves, one line each, with impact×effort. Lead with the product spine. -->

- **{{Move 1}}** — {{one line}} · impact {{1-5}} / effort {{S|M|L}}
- **The spine:** {{web-lead → intake → enrich → report}}, currently broken at {{the manual step}}.

## 2. Cluster map

```mermaid
flowchart LR
  %% node colour = lifecycle · solid edge = existing integration · dashed edge = proposed (label OPP id)
  classDef active fill:#dff,stroke:#066;
  classDef archive fill:#eee,stroke:#999,stroke-dasharray:4;
```

<!-- One paragraph reading the map. -->

## 3. Per-project capsules

### {{project}} — {{lifecycle/role}}
- **Purpose:** {{…}}
- **Stack:** {{…}}
- **Owns:** {{Entity (key)}} · **Consumes:** {{…}}
- **Exposes:** {{…}} · **Calls out to:** {{…}}
- **Sits on seams:** {{seam ids}}
<!-- archived → add: **Prior art:** {{pattern}} · ⚠️ do not integrate live -->

## 4. Shared-infrastructure findings

### {{INFRA-id}}: {{capability}}
- **Carried by:** {{projects}} (anchors: {{…}})
- **Proposal:** {{consolidate / converge / publish one contract}}
- **Migration risk:** {{…}}

## 5. Integration opportunity register

| Rank | ID | Projects | Mechanism | Seam (key) | Impact | Effort | Conf |
|---|---|---|---|---|---|---|---|
| 1 | {{OPP-…}} | {{A ↔ B}} | {{mechanism}} | {{Case (VRM)}} | {{5}} | {{M}} | {{0.8}} |

### {{OPP-…}} — {{title}}
- **Projects / direction:** {{A → B (producer→consumer)}}
- **Seam:** {{type}} `{{name}}`, joined by `{{correlation_key}}`; data flowing: {{…}}
- **Mechanism:** {{mechanism}}
- **Anchors:** `{{A/path}}` — {{why}} · `{{B/path}}` — {{why}}
- **Smallest viable step:** {{…}}
- **Impact:** {{score}} — {{unlocks}} · **Effort:** {{S/M/L}} — {{drivers}}
- **Dependencies:** {{…}} · **Risks:** {{…}}
- **Analogous pattern:** {{summary}} ({{citation}})
- **Confidence:** {{0–1}} · **Assumptions:** {{…}}

## 6. Roadmap

### Wave 0 — foundations
- **{{item}}** (realises {{OPP-x, OPP-y}}) — prereqs: none — first slice: {{…}} — done = {{…}}

### Wave 1 — {{…}}
- {{…}}

## 7. Assumptions, open questions, and rejected ideas

- **Assumptions the ranking rests on:** {{…}}
- **Open questions for the owner:** {{…}}
- **Considered and rejected:** {{OPP-id}} — {{kill reason}}

## 8. Delta from existing artifacts

<!-- What this adds beyond INDEX.md / context store / arch docs. In update mode: new / changed / resolved / newly-rejected. If thin, go deeper. -->

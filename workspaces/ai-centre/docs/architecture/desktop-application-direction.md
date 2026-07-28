# Long-term desktop application direction

- **Status:** Long-term product direction; not implemented or activated
- **Scope:** AI-assisted Collision Engineers workstation

The long-term direction for AI Centre is a desktop application that gives an engineer one case-centred surface for evidence review, retrieval, assisted drafting, valuation and report preparation. The engineer remains the decision-maker and author of record.

This direction does not create a second product or policy owner. A future desktop composition must reuse:

- `Pegasus.Core` business policy and immutable case identities;
- the root `design/` product UI contract;
- the accepted Pegasus application and service APIs;
- `workspaces/report-renderer` for deterministic document rendering; and
- Collision Brain only through an accepted, caller-backed retrieval port.

## Expected shape

A future desktop application may provide a Windows-installed shell, local integration and an offline/degraded experience around accepted Pegasus capabilities. The implementation technology is deliberately undecided. WebView packaging, a native shell, a progressive web application or another desktop-capable composition remain options until a reviewed decision proves the caller, update model, security boundary and deployment path.

The desktop surface is expected to support:

1. case and instruction review;
2. evidence, image and document inspection;
3. cited knowledge retrieval;
4. engineer-controlled drafting and comparison;
5. report preview and explicit issue approval;
6. permissioned Outlook and vehicle-data integrations; and
7. clear online, offline and degraded-state behavior.

## Activation conditions

Desktop implementation remains deferred until all of the following exist:

- a root product allocation and accepted architecture decision;
- an exercised Pegasus caller and API contract;
- a design mapping under root `design/`;
- an installation, update, identity and local-data threat model;
- an offline/degraded-state contract; and
- end-to-end evidence that no business policy, case store, audit model or renderer is duplicated.

Until those conditions are met, `apps/desktop/` remains documentation-only and Collision Brain remains a non-caller development workspace.

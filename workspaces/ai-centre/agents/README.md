# Business agents

This folder owns the production agents presented in the workstation. These are product components,
not repository-contributor instructions.

Initial bounded agents:

| Agent | Responsibility | Must not do |
|---|---|---|
| Case intake | Match instructions and attachments, extract supplied facts, identify gaps | Invent facts or merge cases silently |
| Assessment copilot | Organise evidence, propose findings, call deterministic checks | Make or sign the engineer's final opinion |
| Correspondence | Summarise threads and draft evidence requests or replies | Send without exact-message approval |
| Report author | Draft source-linked sections from accepted case facts | Directly edit accepted facts or issue a report |
| Quality review | Check identity, arithmetic, consistency, citations, and limitations | Overrule the engineer or hide unresolved warnings |

Each implementation needs a manifest containing purpose, owner, typed input/output contracts,
allowed tools, data classes, approval points, failure/abstention behaviour, audit events, version,
and linked evaluation suite. Keep agent-specific prompts and policies here; reusable operations live
under `skills/`.

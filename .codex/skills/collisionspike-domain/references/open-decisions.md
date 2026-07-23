# Domain decisions still open

Before making any of these irreversible, obtain an operator decision:

- transition permissions and privileged overrides;
- sequence behavior if a case changes principal after allocation;
- standalone-audit identity before repairable/total-loss assessment;
- required intake fields by principal and work type;
- exact Review versus Held transition rules;
- first-MVP scope for enrichment, location, or capture capabilities;
- retention/export policy beyond `never delete` in the application.

Keep code reversible and record the ambiguity in `docs/plans/open-decisions.md`.

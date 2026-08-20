# Proof — TICK-062 (MCP-05)

Type: command-log. Released in **release 14** (`d91fd7d7…`, PR #441), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Verification lane at the cut: `pegasus_mail_list`/`pegasus_mail_get`/`pegasus_mail_correct_classification` registered behind the new `automation.mail` scope (single scope list, idempotent grant reconciliation for the canonical client); tools mirror the staff mail workspace's exact Core use cases; denial test writes an `automation_scope_denied` SecurityEvents row; tool-inventory test pins the set (the count assertion fix landed in-lane).
- Live: composed in production behind `Features__AutomationMcp = true`. The post-deploy live inventory call is available to the operator's automation client; not exercised by the release (no automation credentials used).
- Full transcript: DELIV-013 scratch.

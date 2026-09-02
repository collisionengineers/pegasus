---
kind: review-attestation
pr: "643"
head_sha: "8e8dd8b25567d26a45d29ab7dcc5c19b9848971b"
verdict: needs-changes
reviewer: "codex/deliv-040-operator-remediation"
independent: true
plan_hash: "203f2182d5bac060"
ticket_updated: "2026-09-02T02:52:13.537Z"
board_sha: "d073137852840dc0cbc9cd0cf9989e024d9638fb"
expected_reviewers: []
threads_snapshot:
  - { source: github, id: "PRRT_kwDOThBrk86eWYkR", author: "chatgpt-codex-connector", resolved: false, finding: F-013 }
  - { source: github, id: "PRRT_kwDOThBrk86eWYkT", author: "collisionengineers", resolved: false, finding: F-001 }
  - { source: github, id: "PRRT_kwDOThBrk86eWYkU", author: "chatgpt-codex-connector", resolved: false, finding: F-014 }
  - { source: github, id: "PRRT_kwDOThBrk86eWYkW", author: "collisionengineers", resolved: false, finding: F-002 }
  - { source: github, id: "PRRT_kwDOThBrk86eWYkZ", author: "collisionengineers", resolved: false, finding: F-003 }
  - { source: github, id: "PRRT_kwDOThBrk86eWYkb", author: "chatgpt-codex-connector", resolved: false, finding: F-002 }
  - { source: github, id: "PRRT_kwDOThBrk86eWYkc", author: "collisionengineers", resolved: false, finding: F-004 }
  - { source: github, id: "PRRT_kwDOThBrk86eWYke", author: "collisionengineers", resolved: false, finding: F-002 }
  - { source: github, id: "PRRT_kwDOThBrk86eWsAf", author: "chatgpt-codex-connector", resolved: false, finding: F-012 }
  - { source: github, id: "PRRT_kwDOThBrk86eWsAk", author: "collisionengineers", resolved: false, finding: F-005 }
  - { source: github, id: "PRRT_kwDOThBrk86eWsAm", author: "collisionengineers", resolved: false, finding: F-006 }
  - { source: github, id: "PRRT_kwDOThBrk86eWsAn", author: "collisionengineers", resolved: false, finding: F-007 }
  - { source: github, id: "PRRT_kwDOThBrk86eWsAq", author: "collisionengineers", resolved: false, finding: F-008 }
  - { source: github, id: "PRRT_kwDOThBrk86eWsAs", author: "collisionengineers", resolved: false, finding: F-009 }
  - { source: github, id: "PRRT_kwDOThBrk86ecBTW", author: "collisionengineers", resolved: false, finding: F-005 }
  - { source: github, id: "PRRT_kwDOThBrk86ecQyH", author: "collisionengineers", resolved: false, finding: F-010 }
  - { source: github, id: "PRRT_kwDOThBrk86ecSFs", author: "collisionengineers", resolved: false, finding: F-011 }
  - { source: github, id: "PRRT_kwDOThBrk86ecS66", author: "collisionengineers", resolved: false, finding: F-005 }
  - { source: github, id: "PRRT_kwDOThBrk86ecTIx", author: "collisionengineers", resolved: false, finding: F-005 }
findings:
  - { id: F-001, severity: major, summary: "Clarify the dark-feature convention: disabled inert frontend previews are allowed when no backend exists.", disposition: open }
  - { id: F-002, severity: major, summary: "Defer the complete D18 signature and issuer package and allocate a follow-up ticket before review closes.", disposition: open }
  - { id: F-003, severity: major, summary: "Permit full manual estimate entry and direct edits or overrides of imported and accepted estimates while retaining source evidence.", disposition: open }
  - { id: F-004, severity: minor, summary: "Stop treating the external work-pack HTML as a canonical repository source.", disposition: open }
  - { id: F-005, severity: major, summary: "Withdraw proposed upload caps and defer a new limit pending requirements, Azure, performance and cost research.", disposition: open }
  - { id: F-006, severity: major, summary: "Apply chase-interval changes after an operator warning without policy-version migration.", disposition: open }
  - { id: F-007, severity: major, summary: "Allow retries in one upload-link session; first success starts a fixed fifteen-minute TTL and closure refuses later bytes.", disposition: open }
  - { id: F-008, severity: minor, summary: "Remove migration rules for hypothetical live cases because the application is still in development.", disposition: open }
  - { id: F-009, severity: major, summary: "Separate raw PDF or XML import from structured AI-draft save; only the latter requires an Estimate AI job.", disposition: open }
  - { id: F-010, severity: minor, summary: "Cap the optional AI target estimate at eighty percent.", disposition: open }
  - { id: F-011, severity: minor, summary: "Remove future keyboard-map scope unless explicitly operator-authorized.", disposition: open }
  - { id: F-012, severity: minor, summary: "Correct the planned-capability census to 205.", disposition: open }
  - { id: F-013, severity: minor, summary: "Retain ordinary keyboard accessibility while removing the dedicated keyboard-map deliverable.", disposition: open }
  - { id: F-014, severity: note, summary: "Keep planned surface removals qualified as pending until their owning tickets land.", disposition: fixed }
---

# Review return — operator dispositions on PR #643

The operator's current-head comments materially replace parts of D7, D16, D17,
D18, D20, D23 and D24. Findings F-001 through F-013 are one authorized
remediation batch. The same PR, branch and recorded worktree must be reused.

No merge is authorized. After remediation, every GitHub thread requires a public
disposition and resolution and the new head requires a fresh independent delta
review.

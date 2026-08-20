# Checklist — PR-023

- [ ] Reset the process-global native exit state only after the final intentional non-zero classifier assertion has passed.
- [ ] Run the focused script as both a file and a GitHub-style PowerShell command, and run CI change-classification and diff checks.
- [ ] Commit and push the scoped repair to the existing PLAT-014 PR branch, then update its author evidence.
- [ ] Confirm the re-run local-development GitHub job and the complete PLAT-014 PR check set are green before independent re-review.

## Progress notes

(append with `set_ticket_doc(doc: "checklist", append: true)`)

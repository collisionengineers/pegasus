# Report delivery: panels

The form is one block, top to bottom as `_CaseReport.cshtml` draws it:

1. Already sent line (`data-report-already-sent`).
2. From (`data-report-from`).
3. Stopped notice (`data-report-stop`) and Override reason (`data-report-stop-override`).
4. Missing companion notices (`data-report-missing-companion`).
5. Answer before sending: one question row per undecided condition (`data-report-question`).
6. Reviewed recipients: To, Add To, Cc, Add Cc, the Cc hint (`data-report-cc-hint`).
7. Attach (`data-report-attach`).
8. Filed estimate (`data-report-filed-estimates`) and warning lines (`data-report-warning`).
9. File name (`data-report-file-name`).
10. Message (`data-report-message`).
11. After sending (`data-report-after-sending`).
12. Hold rows with their Done ticks (`data-report-hold-row`).
13. Send report.

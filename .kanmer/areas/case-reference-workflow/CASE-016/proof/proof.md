# Proof

**Shipped:** PR #506, commit `3d7f87d6` · **Deployed:** Release 18, `1f3be493`, revision `pegasus-prod-web-252ow37gij--1f3be493c8c6`, image `sha256:818fe360…`.

`Invoke-ProductionSmoke.ps1` asserts the live host reports **exactly** this source SHA, so
the markup below is the markup running.

## The word is gone

A scan of `src/Pegasus.Web/Pages/**/*.cshtml` at the deployed revision returns **no**
occurrence of "immutable" in operator-facing text — only Razor comments and the
`ImmutableItemIdentity` code identifier, which is Outlook's own term and not something an
operator reads.

Five labels lost the word; three Administration sentences were deleted rather than
reworded, because they were explanatory copy and a field hint, which the design authority
bans independently of the vocabulary. Nothing was written to replace them — the approved
necessary-copy list is closed.

## Evidence tier, stated plainly

This is a **deployed-code** proof, not an observed one. The change is deterministic markup
with no runtime input: the strings are either in the rendered view or they are not, and the
build that contains them is the build that is serving. The authenticated case and
Administration pages have not been viewed, because doing so needs a sign-in I must not
perform.

CI's full suite passed on this revision, including `CaseReportApprovalWebTests`, which
renders the report-approval panel and now asserts the button reads "Approve report" — an
authenticated Web caller exercising one of the changed labels end to end.

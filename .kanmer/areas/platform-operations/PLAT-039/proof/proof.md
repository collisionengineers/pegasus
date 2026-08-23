# Proof — production, 2026-08-23

Tier: **production**. Release 26 (`7d6a948a`), revision
`pegasus-prod-web-252ow37gij--7d6a948a2f34`.

## The Box read works from the Web app again

The operator exported `QDOS26014` — a case created after the wipe, whose three
photographs live in Box — and got an archive. Building that archive requires
`BoxDocumentContentStore.OpenReadVersionAsync` to resolve the case folder and
read every image, which is exactly the call that returned
`Box returned 401; response length 0` before this fix. Operator report:
*"This was correctly sorted to review. The export seems to take too long
though, around 10ish seconds."* — slow, but it completed.

Corroborating: the operator's Evidence-tab feedback ([[DOCS-011]],
[[DOCS-012]]) is about how images are *presented*, not that they are missing.
Images rendering at all means the same Box read path is serving them.

## What this proof does not yet cover, and why

The defect was that a token minted at container start was still being presented
an hour later. The revision above started at 14:35Z; the export ran at roughly
15:00Z, **inside** the first hour. A fresh container passed the old code too,
so this is proof that the renewal did not *break* the Box read — not yet proof
that it renews.

**The outstanding check is one export taken more than an hour after a revision
starts.** Under the old code that failed 100% of the time; under this one it
must succeed. Until that is taken, the honest claim is: deployed, unit-proved
across expiry and concurrency (`BoxAuthorizationHeaderTests`, 7 tests), and
confirmed not to have regressed the working path.

## Second defect in the same ticket, proved by its absence

`a.QDOS26013` has no images, so its export refuses. The operator reports the
refusal as *"Export didn't work due to lacking images (this is correct)"* — a
reasoned message on the case page, not the generic "We could not complete that
request" error page that an unhandled `HttpRequestException` produced before.
The export page now reports a failure instead of throwing one.

(That the case reached Review at all with no images is a separate defect,
[[CASE-021]].)

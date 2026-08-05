# Record releases 4, 5, 6 and 7 in operations.md

`docs/operations.md` § Production environment is the exclusive owner of
current production state. Its deployed-evidence paragraph stopped at release
3 (2026-08-03). Four releases have shipped since, and the queue line asking
for releases 4 and 5 to be recorded had been open since 2026-08-04 — long
enough to accumulate two more. This closes all four.

## What changed

The prose paragraph became a table plus a per-release note, because seven
releases will not read as a sentence. The table carries the five facts that
identify a release — date, source revision, image digest, Web revision name,
and whether it applied a migration — and the notes carry what each release
proved beyond smoke.

The shared route (build clean → validate in three modes → push digest-pinned
image → apply migrations *before* packages → activate revision → redeploy
Worker → smoke) is stated once above the table rather than repeated per
release, and the smoke assertions are named so "smoke passed" means something
specific.

## Evidence

Every digest in the table was read back from the production registry rather
than transcribed from `NOW.md`:

| Tag | Digest |
|---|---|
| `8e34078…` | `sha256:ae2cc7b83844da366227d5dc45b7243a5e23e810a7be5a88495a1a2ab919d9b8` |
| `c6571f7…` | `sha256:29d4fcffd555905532db9559032e005ae4b31b6e7134a13ca17bcd9d131d2e18` |
| `ef987ac4…` | `sha256:89165ad556c20f6efcdc08eaf04e0ff4d2c09c0101859565f605937ef03be2af` |
| `474a0924…` | `sha256:b2ceaf37e7054dd798542a92374723ad2e990875f8a4c8ecca3a532af70e196e` |
| `32feefa…` | `sha256:c8a0ebac40111764fdfc5b03519b12bd26cc50ae7bbb956f1f8e240fadeb1d54` |

All five match what the records claimed.

Release 4's Web revision name is recorded as absent rather than guessed: the
Container App keeps only the current and immediately preceding revisions, so
it is no longer readable, and no source in the repository records it.

## The verification account

`claudeuiverification` is now an enabled Administrator on the production
estate, and nothing outside `NOW.md` said so. It gets its own bullet under
production environment, naming what it is, why it exists, that its password
is in source control, and that it must be removed before go-live. An account
that can be read out of a public-facing repository belongs in the operational
record, not only in the work tracker.

## The lesson worth keeping

Release 6's note records *why* live verification found six defects that local
testing could not, because the reason generalises past those defects: an
empty local database makes a permanently-zero count look correct, and a
Europe/London workstation clock makes `ToLocalTime()` look correct where the
deployed Linux container runs UTC. A count query and a rendered time cannot
be proved locally. That belongs in operations, not in a temp plan that gets
deleted.

## Not in scope

No claim about deployment proving acceptance, and no change to the release
validation rules or the evidence tiers. `.azure/deployment-plan.md` remains
the immutable record of the `0.1.0-alpha.1` execution and is untouched.

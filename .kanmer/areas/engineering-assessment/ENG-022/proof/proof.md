---
kind: proof
pr: "579"
merge_sha: "84132d01ccb0afca7af6c6ce519e6f3491aee160"
verified_on: "783b4b884d3f110e78efe25366b66950d04551fc"
result: PASS
verified_at: "2026-08-28T03:07:00Z"
---

# Proof — ENG-022

Verified on merged `main`, which is `783b4b88` (the release-36 docs commit);
this ticket's own merge commit is `84132d01`, an ancestor of it.

## The committed blob no longer carries a BOM

```
$ git cat-file -p main:infra/main.parameters.json | head -c 3 | xxd
00000000: 7b0a 20                                  {.
```

`7B 0A` — an opening brace and a newline, not `EF BB BF`. Read from the blob
rather than the working tree, because the checkout applies CRLF and would have
made the file's first bytes look different for an unrelated reason.

## The content is otherwise unchanged

Diffed against `68adedaf`, the last commit before TICK-077 wrote the BOM. The
only difference is the six EVA parameter blocks TICK-077 legitimately added:
`evaClientIdSecretUri`, `evaClientSecretSecretUri`, `evaBaseUri`,
`evaRequestFrom`, `evaInspectionType`, `evaInstructionEmail`. Nothing else
moved, and the BOM difference is gone.

## azd gets past the step it died on

The purpose of the fix, and the only test that matters. Against
`rg-pegasus-prod`, `azd provision` completed:

```
(✓) Done: Function App: pegasus-prod-worker-252ow37gij (2.319s)
(✓) Done: Container App: pegasus-prod-web-252ow37gij (17.556s)
SUCCESS: Your application was provisioned in Azure in 1 minute 24 seconds.
```

Before the fix the same command produced:

```
ERROR: deployment failed: initializing provisioning manager: resolving bicep
parameters file: error unmarshalling Bicep template parameters: invalid
character 'ï' looking for beginning of value
```

Release 36 deployed from `84132d01` on the strength of this.

## Result

**PASS.** All three of the ticket's verification conditions hold.

## What this does not prove

No CI gate reads `main.parameters.json` as JSON, so nothing stops the same BOM
returning tomorrow. Closing that would mean adding a gate — a separate change
with its own justification, not a silent addition here.

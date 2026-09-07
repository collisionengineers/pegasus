# ENG-041 research

## Evidence and root cause

Read against origin/dev 1d972f05c0f10c2ecf804f271a4fd3155242f1ef.
The supplied PR675 reviews F2/F3 identify real current caller failures.
GlassRepairEstimateGateway persists Prepared, then Launching before vehicle
creation, but only persists the returned vehicle/estimate together at Active.
Cancellation bypasses its transport catch. Resume refuses Prepared/Launching,
and the Case page additionally hides Unknown/Importing recovery. GlassMvaClient
has no lookup capable of recovering a lost create/start identifier. Starting
with ere_id=0 again is therefore unsafe when that answer is unknown.

Details.OnPostSaveEstimateAsync replaces submitted Case version with a fresh
read and enriches submitted lines from mutable current lines and the current
clock before hashing. A replay can change intent/hash. EfRepairSpecificationStore
checks the durable CaseWorkflowEvent hash first, but locates a prior result via
CreationOperationKey/LastOperationKey, which loses K2 after K3. Existing
ActionHistory.AfterJson already permanently records the affected estimate Id.

## Reused owners and decision

Reuse the Glass gateway/client/session port, encrypted ProviderState and CAS
session store. Persist the callback before provider work, vehicle identity as
soon as known, and a marker before the estimate-start call. Prepared resumes
read-only preparation; known vehicle without a started estimate continues once;
known estimate resumes its existing ID. Interrupted writes without an answer
remain Unknown and hold the external-account slot. Persist cancellation
settlement with an uncancelled token; permanent transitions use existing
ActionHistory, never provider tokens or content.

Root explicitly approved an own-Unknown-session Close session action with a
required reason and engineer confirmation that Glass is closed/no estimate
remains open; CAS prevents stale closure. No provider lookup or retry is invented.

Keep submitted estimate version/line identities in SaveEstimateRequest. Move
provenance/rate carry-forward and amendment stamping behind the replay guard,
using EstimatePolicy as the one policy owner. Use existing permanent operation
history to locate the result Id and return that same aggregate in its current
state; no historical snapshot table, and never reapply an old edit. Root
explicitly confirmed that current-aggregate replay outcome.

## Sources and boundaries

FRD-06 governs source-labelled engineer drafts and preserved provenance.
EPIC-014 context/current operator instruction authorizes these v1 repairs.
No declared external sources for this area. Provider contract is the existing
captured transport, not guessed external APIs. No live Glass calls, new
dependencies, schema, broad grants or report/intake mutations. Root is sole
build/test/UI snapshot verifier.

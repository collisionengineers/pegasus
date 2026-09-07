# Current closeout execution index

Complete earlier scratch/execution history is preserved in immutable board commit
728b6e3bdf6f764d9d51e10df06f26386781ce2f at
.kanmer/areas/platform-operations/PLAT-075/scratch/execution.md
(version ce0f70f334530715, 102652 characters).
Do not reinterpret earlier passes as final acceptance.

Current common source: 1ba06bd01329d87c3aa4ce7c6b604def78c26809.
All three source histories are included. Approved closeout finishes remaining
intake fixtures, correspondence attachments, shell failure isolation, frozen
estimate projection, current documentation/snapshots, verification and reviews.
Then merge dev and open main PR unmerged. Prior543 full run: Core1811PASS,
Architecture109PASS, Integration1968PASS24FAIL0SKIP. Later fixes unverified.
Six scan-only OCR-provider outcomes remain INCONCLUSIVE release gates by
explicit operator approval. No deployment/provider/mailbox/Box writes.

2026-09-07 closeout execution: A packet initially GATE_BLOCKED because145088-character ticket-dispositions and oversized C/execution logs exceeded64KiB/document and512KiB aggregate. Preserved all210 roster rows in five bounded plan parts with exact concatenation readback. Original full logs remain at immutable board728b6e3bdf6f764d9d51e10df06f26386781ce2f; bounded indexes reference them. Packet now ready. Approved closeout recorded on all3owner plan/closeout adfb276e7412137b. Terra454a94400/857bb4f19 Cfixture/mapping/G28assertion source independently PASS; removed synthetic ambiguity test superseded by existing real-policy Core ambiguity tests. Root a8506a4a2 totals and163eef507 shell independently source PASS with dispositioned minor: explicit cancellation/auth catch exclusions verified in source; no disproportionate new filter harness. ffa20fbf0 removes unsupported never-activated-EVA claim and staleQDOS-only comment. Corpus baseline543 exit1:Core61P;Integration29P5F13skip due narrow inherited corpusroot, scoped-services fixture, and accepted sixMP scan evidence. Final root must PEGASUS_CORPUS_ROOT=source checkout corpus (full immutable estate), PEGASUS_REFERENCE_PACK_ROOT=source pack. Frozen refresh merge hit one CaseWorkflowMigration expectation conflict; first build incorrectly continued and FAILED0W3E75.57s due conflictmarkers; no stale-binary tests. Resolved by retaining fullG28chain; combinedf3e85d6937b131e4c90f03a463c0c2c25a507a0e tree exactlyequals sourceffa20fbf0, rebuild running98956. No dev/main or providerwrite.

Closeout refresh: frozen f3e85d6937b131e4c90f03a463c0c2c25a507a0e build failed 6 compile errors, 0 warnings (37.76s). Source b5a04bd7f corrects serialized estimate breakdown access, QDOS review-field contract access, and derived WebApplicationFactory client creation without weakening assertions. Combined 37fcf220c6e6431dea5df73c81745fcbddadce5d restore passed; build then failed MSB3027/MSB3021 (10 warnings, 2 errors, 15.57s) due to retained MSBuild node PID4376 locking Pegasus.Core.dll. Verified process command is MSBuild /nodemode:1 /nodeReuse:true. Graceful dotnet build-server shutdown passed; rebuilding same source with --disable-build-servers. No tests run against stale binaries.

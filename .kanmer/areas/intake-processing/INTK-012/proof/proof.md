# Proof — INTK-012

Type: command-log. Released in **release 14** (`d91fd7d7…`, PR #454), production smoke passed 2026-08-20.

- Verification lane at the cut: `GroupedIntakeMemberToken.ParentTokenCandidates` (strict `:{ordinal>=1}` suffix strip; bare token stays an ordinal-0 candidate) with `HasSiblingMembers` gating both live callers (`ImageIntakeAutomation.TryApplyGroupAsync`, `ReconcileGroupedImageIntake`); tests `ParentTokenCandidates` theory, `AOneMemberGroupKeepsTheSingleImageAssociationRule`, `EveryMemberResolvesToItsGroupByItsOwnSourceIdentity` all present; composes intact with INTK-015 at the head.
- Worker exercising the path in production without error post-deploy (zero exceptions; polls completing — DELIV-013 scratch).

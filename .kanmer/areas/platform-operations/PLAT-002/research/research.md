# Research — PLAT-002: one staff-actor page root

## Question

Can the two existing Razor Page bases and named private copies share one staff-actor/operation-key owner without changing authorization or identity, and does the written scope satisfy the one-root verification?

## Findings

- Cases/CaseMutationPageModel.cs and Administration/AdministrationPageModel.cs independently resolve the same name-identifier and role claims through StaffActorFactory.TryCreate. Administration uses the tidier nullable-flow signature; the case base manually assigns null on failure. Both generate Guid N-format keys.
- The case base also owns substantial case-edit and TempData policy. A new root above it fits the existing boundary: case-only behaviour remains on CaseMutationPageModel.
- Operations/Index, Triage/Details, Cases/Assessment/Index, and Cases/Documents/Download carry explicit staff-role authorization. A metadata-free root does not change their authorization.
- Account/PasswordChange has no page attribute, but Program.cs:495-496 sets an authenticated fallback policy. Its helper also returns a staff Guid; ActionActor.SubjectId carries the same identifier and can be parsed locally after shared resolution.
- Triage/Details likewise needs both staff Guid and actor. Preserve the local subject-to-Guid parse rather than add a second shared signature.
- Cases/Documents/Download is staff-authorized; being a GET/file response is not unsafe when the root adds no endpoint metadata.
- Uploads/Request is explicitly AllowAnonymous and resolves no staff actor; it only duplicates key generation. Inheriting StaffPageModel would falsely model a public page as staff. It can call an accessible shared generator without inheriting staff semantics, or planning must find a neutral existing owner.
- The scope and verification disagree. The read-only command rg -l "StaffActorFactory.TryCreate" src/Pegasus.Web --glob "*.cs" returns 20 files. Direct inline resolutions remain in root Index, Intake, Image Intake, mail, upload/status, unidentified, case-create/index, and Triage index pages. The stated one-root rg check cannot pass when only named pages change.
- Guid.NewGuid().ToString("N") appears in eight page-model files, including extra copies in Cases/Create and Upload. The written scope also cannot establish one operation-key owner without addressing or excluding them.
- SIMPLI-011 created CaseMutationPageModel as a behaviour-preserving extraction, proved the current callers on merged dev, and explicitly deferred consolidation here. Reuse that base; do not reopen the case workspace split.
- This is an internal Web simplification. No PRD/FRD/ADR behaviour, Azure resource, deployment state, or cloud write is implicated.

## Implications

The narrow shape is a metadata-free root owning nullable TryGetActor and operation-key generation. Both existing bases and named authorized pages derive from it; pages needing staff Guid retain only parsing; the anonymous upload page consumes the static generator without inheriting staff semantics.

Planning cannot define honest acceptance until scope is decided: expand to all current Web actor/key copies, or narrow the ticket and its rg check. No Azure work belongs in either option.

## Open questions

- Expand to every current Web actor/key copy, or retain the named cleanup with narrower verification?

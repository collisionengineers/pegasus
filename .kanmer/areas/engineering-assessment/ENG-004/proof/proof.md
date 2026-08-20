# Proof — ENG-004

Type: command-log. Released in **release 14** (`d91fd7d7…`, PR #437), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Verification lane at the cut: label-position anchoring (bare label only at line start / after separators; mid-line requires explicit `:`/`-`), `TruncateAtColumnBoundary`, `IsPlausibleVehicleMakeModel` (MOT/wheel-position vocabulary rejected), `AcceptsValue` wired onto Vehicle make/model; fixture pins the literal production value "AUDI NSF : Footbrake : SATISFACTORY" as never offered.
- Live caller: worker intake pipeline (`IntakeWorkFunction` → `ProcessIntake` → `QdosInstructionExtractionPolicy`), running clean post-deploy. New intakes cannot repeat the QDOS26002 pollution; the stored wrong suggestions there are pre-fix data for the operator to decline/correct.
- Full transcript: DELIV-013 scratch.

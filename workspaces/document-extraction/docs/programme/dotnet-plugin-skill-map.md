# .NET plugin skill inventory and implementation-wave routing

This inventory records the skills exposed by the four requested .NET plugins on 23 July 2026. Skills are invoked by delegated implementation agents when their trigger applies; listing a skill here does not claim that its trigger has occurred or that its checks have passed.

## `dotnet` (1)

- `setup-local-sdk`

Use for Wave 0/bootstrap only when the pinned .NET 10 SDK is absent or an isolated SDK is required. The current workstation already has the required SDK, so installation is not part of the ordinary path.

## `dotnet-diag` (7)

- `analyzing-dotnet-performance`
- `android-tombstone-symbolication`
- `apple-crash-symbolication`
- `clr-activation-debugging`
- `dotnet-trace-collect`
- `dump-collect`
- `microbenchmarking`

Apply static performance analysis to parser and decoder hot paths as they are implemented. Use `microbenchmarking` in Wave 13 to establish repeatable extraction baselines, then collect traces or dumps only for a measured performance, hang, or crash problem. Mobile symbolication and .NET Framework CLR activation do not apply to the current headless .NET 10 library/CLI target.

## `dotnet-msbuild` (19)

- `binlog-failure-analysis`
- `binlog-generation`
- `build-parallelism`
- `build-perf-baseline`
- `build-perf-diagnostics`
- `check-bin-obj-clash`
- `copy-to-output-directory`
- `directory-build-organization`
- `eval-performance`
- `extension-points`
- `including-generated-files`
- `incremental-build`
- `item-management`
- `msbuild-antipatterns`
- `msbuild-modernization`
- `msbuild-server`
- `property-patterns`
- `resolve-project-references`
- `target-authoring`

Use `directory-build-organization`, `property-patterns`, and `msbuild-antipatterns` when establishing or changing repository-wide build policy. Use output, item, extension, generated-file, and target-authoring skills only when such build behaviour is introduced. Establish build-performance and parallelism evidence in Waves 13–14. Generate and analyse a binary log only when ordinary build output does not explain a failure or measured bottleneck. Legacy-project modernisation is not currently applicable because projects are SDK-style.

## `dotnet-test` (20)

- `assertion-quality`
- `code-testing-agent`
- `code-testing-extensions`
- `coverage-analysis`
- `crap-score`
- `detect-static-dependencies`
- `filter-syntax`
- `find-untested-sources`
- `generate-testability-wrappers`
- `grade-tests`
- `migrate-static-to-wrapper`
- `mtp-hot-reload`
- `platform-detection`
- `run-tests`
- `test-analysis-extensions`
- `test-anti-patterns`
- `test-gap-analysis`
- `test-smell-detection`
- `test-tagging`
- `writing-mstest-tests`

Every implementation wave begins with `platform-detection` when the test project is new or unfamiliar, uses `writing-mstest-tests` or `code-testing-agent` for focused tests, and ends with `run-tests`. Apply assertion, gap, tagging, smell, anti-pattern, untested-source, and coverage analysis at feature and acceptance gates. Static-dependency detection is mandatory for the public orchestration/CLI boundary and useful for code that touches time, files, environment, or processes. Generate or migrate wrappers only when a real static boundary is found. Filtering, grading, CRAP scoring, and MTP hot reload are problem-specific aids rather than unconditional gates.

## Wave routing

| Wave | Required skill use | Conditional skill use |
|---|---|---|
| 0 | MSBuild organisation/property/anti-pattern review; test tagging policy | Local SDK setup |
| 1–4 | Platform detection; MSTest authoring; focused test execution; assertion and gap review; parser performance scan | Coverage/CRAP; static wrappers; binlog diagnostics |
| 5 | Test execution and API/CLI tests; static-dependency detection | Output/item/target/generated-file skills where packaging requires them |
| 6–12 | Platform detection; focused tests; assertion/gap/tagging review; parser performance scan | Coverage, fuzz-related test analysis, binlogs, hot reload |
| 13 | Microbenchmarking; performance scan; build-performance baseline | Trace collection and build-performance diagnostics when evidence identifies a bottleneck |
| 14 | Build anti-pattern/output/clash review; test quality, gap, coverage and acceptance analysis | Publish-target authoring; binlog failure analysis |
| 15 | Caller test execution, tagging, gap and coverage review | Failure diagnostics driven by caller evidence |

Delegated agents must name the skills they used in their evidence record, include exact commands and exits, and distinguish a skill review from a passed product gate.

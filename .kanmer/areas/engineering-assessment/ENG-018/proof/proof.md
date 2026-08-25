# Proof — ENG-018

## Merged and released

- PR #542 merged to dev as b1aa68c86063fbcf70658f10271e6b622e792d32 and that exact SHA was atomically fast-forwarded to main.
- Production release 29 was built from a clean exact-SHA tree.
- Manifest image digest: sha256:cb4803190a9db02361eb03228acf3905149ccbbbb14f008cc7b70834ddc6b31e.
- Active ready Web revision: pegasus-prod-web-252ow37gij--b1aa68c86063 at 100% traffic.

## Behavior and regression evidence

- Core export/contract tests: 26 passed.
- End-to-end export journeys without EVA activation configuration: 2 passed.
- Full merged-source solution evidence: Core 978 passed; Architecture 99 passed; Integration 946 passed with 16 corpus-dependent skips.
- GitHub PR checks all passed, including unit, browser, infrastructure and all three SQL shards.
- Independent review passed with no blocking findings.
- Live Container App configuration read-back contains no Eva__AcceptedMapping__ settings.
- Production exact-version/source smoke passed and all nine Worker functions remain enabled.

## Boundary

These checks prove the obsolete activation gate and message are absent from the deployed source/configuration and the configuration-free Export path passes end to end. No authenticated operator Export or EVA import was performed during verification; the operator can now retry the case that exposed the release-28 defect.

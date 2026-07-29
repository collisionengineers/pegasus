---
name: roadworthy-report
description: Inactive source-workspace renderer contract for a prepared HS document; never invoke until the separately accepted renderer change and source-evidence gate are complete.
---

# HS roadworthy report

## Authority boundary

This package may produce evidence, candidates, or draft output only. `Pegasus.Core` and an authorised human own every accepted case fact, cost, category, outcome, legal position, and approval.

## Inactive — renderer source change required

Do not invoke this package. The unchanged deterministic renderer hard-codes `Legal Status=Roadworthy`, so it cannot safely distinguish an approved source fact from a template literal. Activation requires a separately accepted source change that stops unless a cited source artifact contains the named Engineer's approved roadworthy/legal-status fact. A template, fallback, assessment pack, model, or skill output is not that evidence.

## Retained package contract

The package is retained only as source/provenance evidence for a prepared HS DOCX transformation. `references/field-mapping.md` documents the 14-field payload and must label fallback values as template behavior, never case evidence or policy. `scripts/render_roadworthy.py` validates placeholders and fails closed when the prepared template is absent; neither the script nor template may be used while this package is inactive.

Any future activation must preserve the original template, operate on a copy, accept only source-labelled and human-approved values, record renderer/template/payload identity, produce a draft for review, and stop rather than hand-edit XML or invent a substitute template.

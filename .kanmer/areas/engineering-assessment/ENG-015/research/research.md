# The original extractor settles four open questions

Source: `C:\Users\Alex\Documents\Matty_Files\CE Document Mapper\V67 case_intake_app`
— the predecessor `cedocumentmapper` (`app.py`, 4390 lines; `providers.json`,
29 providers). Supplied by the operator 2026-08-24.

**Status of this source.** `docs/operator-notes.md` records it as *predecessor
evidence only* and forbids adopting or reusing the implementation. It is read
here as **evidence of the output contract EVA accepts**, never as code to port.
Every finding below is a fact about the JSON that works, not a design borrowed.

## The QDOS provider config, verbatim

```json
"reference":              { "method": "single_label", "config": "Our Ref:" },
"instruction_date":       { "method": "single_label", "config": "Date:" },
"inspection_date":        { "method": "manual_input", "config": "" },
"inspection_address":     { "method": "manual_input", "config": "Image-based Assessment" },
"accident_circumstances": { "method": "two_labels",   "config": "Damage Area || TP Vehicle" },
"vat_status":             { "method": "single_label", "config": "" },
"mileage":                { "method": "single_label", "config": "" },
"mileage_unit":           { "method": "single_label", "config": "" }
```
plus `"use_current_date_for_inspection_date": true`, `"engineer_report": false`.

## 1. `Reference` — confirmed, no change to the decision

`single_label` on **`"Our Ref:"`**. That is the work provider's own reference,
exactly as decided. `AKH//47743/1` in the operator's sample is the value of
`Our Ref:` in the instruction letter.

## 2. `Inspection Address` — the 6-line block is a hard import requirement

`app.py:2286` `normalise_inspection_address_value`, quoting the author:

> Normalise the inspection address to a **6-line canonical form**. The output is
> always exactly 6 lines separated by 5 newlines — `line1\n…\npostcode` —
> including when the input is empty (in which case all 6 lines are blank,
> yielding `"\n\n\n\n\n"`). **Downstream JSON export feeds this to a management
> system that requires the 6-line shape; returning a bare empty string would
> fail the import.**
>
> Lines 1-5 are the address body, line 6 is the postcode. Body lines overflow by
> joining surplus content into line 5 with spaces.

This is direct confirmation from the original author, with the reason. It was
inferred from three samples; it is now stated. Note the rule is **unconditional**
— even an empty address emits `"\n\n\n\n\n"`, so this is not special-casing
`Image-based Assessment`, it is how every address is shaped.

Also: commas are converted to line breaks before splitting
(`re.sub(r"\s*,\s*", "\n", text)`), and the postcode is canonicalised to
`OUTWARD INWARD` (space before the last three characters).

The literal is `manual_input` config **`"Image-based Assessment"`** — hyphen,
lowercase `b` — then run through the 6-line normaliser, producing
`"Image-based Assessment\n\n\n\n\n"`. Exactly the sample bytes.

## 3. `VAT Status` — **resolved: blank is correct for QDOS**

`app.py:158` `PRESENCE_CHECK_FIELDS`:

```python
"vat_status":   {"positive_value": "Yes", "negative_value": "No"},
"mileage_unit": {"positive_value": "Miles", "negative_value": "Km"},
```

`resolve_presence_check` (`:2109`): the config is a comma-separated list of
probe tokens; if any appears anywhere in the document text (case-insensitive)
the field is `positive_value`, otherwise `negative_value` — **and a blank config
returns `""`**.

- AX probes `"VAT Registered: \nYes"`; SBL probes `"Policyholder VAT Status:\nVAT Registered"`.
- **QDOS's config is empty.** So VAT Status is `""` for every QDOS case, by
  design, not by failure.

**Consequence: Pegasus emitting `""` here is already correct.** The earlier open
question — "confirm VAT is meant to be operator-entered rather than derived" —
is answered: for QDOS it is neither. It is deliberately blank. No extractor is
needed and none should be added.

## 4. `Mileage Unit` — a presence check, not a unit conversion

Same mechanism: `Miles` / `Km`, **Title case**. It is not derived from the
mileage value's unit at all — it is "does this probe string appear in the
document". Pegasus emits lowercase `"miles"` / `"kilometres"`
(`EvaHandoffStore.cs:898-907`), which matches neither sample.

## 5. Audit cases with an original report — the "slight difference"

Three providers carry `"engineer_report": true`: **CNX (Engineers)**,
**EVA (Engineers)**, **LAIRDS (Engineers)**. These are the *original engineer's
report* in an audit case, and their configs are almost entirely empty except:

```json
"mileage":          { "method": "single_label", "config": "Speedo:" },
"mileage_unit":     { "method": "single_label", "config": "Miles" },
"inspection_date":  { "method": "single_label", "config": "Date:" },
"instruction_date": { "method": "two_labels",   "config": "instructions received on || and" }
```

The merge rule is one line, `app.py:3602` `combine_instruction_and_engineer_report`:

```python
merged_values = dict(instruction_session.values)
for key in NON_PROVIDER_FIELDS:          # every field except work_provider
    new_value = (engineer_session.values.get(key) or "").strip()
    if new_value:
        merged_values[key] = new_value
```

**The instruction is the base; the engineer's report overrides any field for
which it produced a non-empty value.** `work_provider` is never overridden.

That is the whole audit difference. It explains `AX_SP58WVO.json` carrying
`"Mileage": "94730"` / `"Mileage Unit": "Miles"` while AX's own config has both
blank — the values came from the engineer's report (`Speedo:`, and the `Miles`
probe hitting).

## 6. Export format — confirms ENG-014, and adds one thing

`app.py:3863`:

```python
ordered = {FIELD_LABELS[key]: values.get(key, "") for key in FIELD_KEYS}
json_string = json.dumps(ordered, ensure_ascii=False, indent=2)
output_json.write_text(json_string, encoding="utf-8")
```

- `indent=2` → confirms ENG-014's indentation change.
- **`ensure_ascii=False`** → non-ASCII is written as literal UTF-8, never
  `\uXXXX`. Verified: both retained samples contain raw non-ASCII bytes (curly
  apostrophe, en-dash) and zero `\u` escapes. Pegasus's default
  `JavaScriptEncoder` escapes them. **Handed to [[ENG-014]]**, which owns the
  serializer options.
- `write_text(..., encoding="utf-8")` on Windows → no BOM, and `\n` translated
  to CRLF. The CRLF in all three samples is a Python-on-Windows artefact, not an
  EVA requirement.

## Divergence to decide, not to silently fix

**Pegasus fills `Mileage` from the DVLA/DVSA MOT lookup** (a `suggested` value,
added deliberately by ENG-013). **The original only ever fills it from an
engineer's report `Speedo:` reading.** For a QDOS instruction with no engineer's
report, the original emits `""` and Pegasus emits `208602`.

This is existing shipped behaviour that an operator asked for, so it is not
something to remove on my own judgement — but it is a real difference from "the
JSON the original produces", and the operator's instruction was to match that.
Raised in the ticket's open questions rather than resolved here.

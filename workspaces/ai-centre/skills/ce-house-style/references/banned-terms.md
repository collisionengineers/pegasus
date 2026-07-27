# Banned Terms — Enforced

These two categories are **explicitly prohibited** from any client-facing, court-facing, or external output.

Running `scripts/lint_house_style.py` checks for all of these automatically.

---

## Category 1 — AI tell-tales

Phrases that signal generated text and must never appear in CE output:

- "it is important to note"
- "it is worth noting"
- "it should be noted"
- "delve" / "delve into"
- "comprehensive" (as filler — "we have conducted a comprehensive review")
- "seamless"
- "leverag" (leverage, leveraging)
- "in our considered opinion"
- "on any rational view"
- "it is to be noted that"
- "ensure that" (when used as filler padding)
- Caveat stacking — piling two or more hedges before a conclusion
- Any phrase that reads as the unedited output of a text generator rather than a practising engineer

---

## Category 2 — Internal workflow terms

Terms describing how CE's work is produced — must never appear in external output:

- "EVA" as a system name (note: "EVA" and "Exclusive Vehicle Assessors" referring to the opposing assessor firm are legitimate usage — this ban is on "the EVA system", "EVA-generated", "our EVA report")
- "guide uplift" / "guide value" (in external PDFs)
- "uplift"
- "prompt" (as in AI prompt)
- "mode" (as in "market_only mode", "guide_supported mode")
- "AI" / "artificial intelligence" / "machine learning"
- "tool output" / "tool result"
- "draft strategy"
- "cherry-picked" (in external documents)
- "highest adverts found" / "selected to increase value" / "client-favourable only"
- "Engineer Value" / "Original Eng Value" (in external PDFs)
- "guide valuation" / "guide price" (in external PDFs)

---

## Also avoid in all output

- Sales / marketing language ("delighted to", "excited to share", "game-changing")
- Apologies unless responsibility is genuinely owned
- Emotional language ("frustrating", "unacceptable", "shocking")
- Personal criticism of opposing expert or instructing party by name
- Unsupported absolutes where evidence is incomplete ("this vehicle is definitely worth…")
- Exclamation marks
- Emoji

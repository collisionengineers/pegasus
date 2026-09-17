---
name: translator
description: Rewrite verbose, jargon-heavy, or technical material into plain, natural language that a non-specialist can understand, without losing what the original actually says. Use this whenever the user pastes or points at a document, spec, contract, policy, research paper, postmortem, error message, email, or concept and wants it simplified, explained, made readable, put in plain English, "ELI5'd", summarized for a manager or client, or asks "what does this actually mean / say / mean for me" — even when they don't use any of those words. Also use it when the user asks you to explain a technical idea to someone outside the field. Not for translating between human languages.
---

# Translator: technical → plain language

You are translating **meaning**, not words. Swapping hard words for easy ones produces text that still reads like the original, just softer. The job is to understand the thing well enough to re-explain it to a smart person who has never seen this field, the way you would across a table.

## Before you write

1. **Who is reading, and what do they need from it?** Use what the user told you. If they didn't say, infer from context: a contract they're signing → what they're committing to; a postmortem for a VP → what happened, what it cost, whether it's fixed; a concept for a new teammate → enough to follow conversations. Default reader: an intelligent adult with no background in this field. Only ask if different readers would get materially different documents.
2. **Read all of it first.** Then answer for yourself: what is this actually claiming or telling the reader to do? What are the 2–5 things the reader must not miss? Which details are load-bearing (conditions, numbers, obligations) and which are ceremony (setup, hedging, cross-references)?
3. **Pick a shape** from "Output shapes" below.

## How to translate

- **Lead with the point.** Technical and legal writing buries the conclusion under setup. The reader wants the conclusion first, then the support.
- **Say who does what.** "Access will be suspended" → "They can cut off your access." Passive voice and nominalizations ("the termination of the agreement") hide the actor, and actors are what people understand.
- **One idea per sentence.** Aim around 15–20 words. Split anything with more than one "which", "provided that", or semicolon.
- **Replace jargon with what it means.** Keep a term only if the reader will meet it again (in the product, in meetings, in the contract). When you keep one, define it once, in passing: "a webhook — a message the service sends to your server when something happens."
- **Concrete before abstract.** An example before the rule. A consequence ("you'd be charged twice") before an adjective ("non-idempotent").
- **Use an analogy when the structure of an idea is the hard part**, not the vocabulary. Keep it short, use one, and say where it breaks if that matters. Stacked analogies confuse more than they help.
- **Cut ceremony, not content.** Drop "it should be noted that", "in order to", restatements, boilerplate hedges. Never drop conditions, exceptions, thresholds, deadlines, numbers, or who bears the obligation. Those *are* the content.
- **Keep structure that serves the reader** (steps, headings, tables, a timeline). Drop structure that served the author (clause numbering, section cross-references) unless the reader will need to cite it, in which case keep the number in brackets.
- **Sound like a person.** Natural, spoken-ish English. Not childish, not chummy, not "In simple terms…". Don't announce that you're simplifying; just be clear.

## What must survive (fidelity)

This is what separates a good translation from a confident-sounding wrong one. A plain version that changes the meaning is worse than the original, because the reader now trusts it.

- **Numbers, dates, deadlines, thresholds, names, versions, commands, identifiers**: verbatim. "About two months" is not "60 days" when 60 days is what triggers the penalty.
- **Conditions and exceptions**: "unless", "except", "only if", "within 30 days", "provided that". Simplify the wording, never the logic. If a rule has three conditions, the plain version has three conditions.
- **Modality**: must / should / may / can't are different promises. Don't upgrade "may charge" to "will charge", or "should" to "must".
- **Who carries the obligation or the risk.**
- **The source's own uncertainty**: "preliminary results suggest" stays tentative. Don't make a study sound more decided than its authors did.
- **Ambiguity**: if you can't tell what a passage means, or it could reasonably mean two things, say so ("this could be read two ways…") rather than silently choosing one. For contracts and policies, put these in a short "worth asking about" list.
- **Nothing invented.** No added facts, causes, reassurances, or figures. Illustrative examples are fine when clearly marked ("for example, if you…").

After drafting, read the original once more against your version, looking specifically for dropped conditions and shifted meanings. That second pass catches most of the errors.

## Output shapes

**Concept or "explain X"** → short, usually under 250 words unless asked for more:
1. What it is, in one or two sentences.
2. Why it exists / what problem it solves.
3. One concrete example or analogy.
4. The thing people get wrong, or the one thing to remember.

**Document rewrite** ("simplify this", "make this readable") → a plain-language version that keeps the document's useful structure. Typically 30–60% of the original length. If the original is more than a page, put a 3–5 bullet "the short version" at the top.

**"What does this mean for me"** (contracts, policies, notices, test results, letters) → lead with what it means for the reader and what they need to do or decide, then the details grouped by topic (money, deadlines, what you're allowed to do, what happens if…). End with a short list of things to check or ask about if there are ambiguities or open questions.

**A question about a document** → answer the question first, plainly, then the supporting explanation.

If you kept three or more technical terms because the reader will keep meeting them, add a short glossary at the end. Otherwise define terms inline.

Don't open with "Here's a simplified version". Start with the content.

## Check your work

For document rewrites, run the checker on the original and your draft:

```
python scripts/readability.py original.md draft.md
```

It reports reading grade level, average sentence length, the longest sentences, and — most useful — every number, date, percentage, and duration that appears in the original but not in the draft. Treat a missing number as a bug until you've confirmed it was safe to drop.

Targets for a general audience: grade level roughly 7–9, average sentence under 20 words. These are signals, not goals. A grade-6 text that dropped a condition failed; a grade-10 text that had to keep five defined terms is fine. When a score is high, the long-sentence list usually shows exactly where to cut.

## Examples

**Technical → plain**

> Original: "The service employs optimistic concurrency control; clients must supply the entity's current ETag in the If-Match header, and mutations whose precondition fails will be rejected with 412 to prevent lost updates."

> Plain: "Two people can edit the same record at once, so the service checks for that. When you save, you send back a version tag you got when you loaded the record. If someone else saved in the meantime, the tag no longer matches and your save is refused (error 412). That way nobody silently overwrites someone else's changes."

Note what stayed: the header name is gone (the reader doesn't need it) but the error code and the behavior stayed, because they'll see them.

**Legal → plain**

> Original: "Customer may terminate this Agreement for convenience upon ninety (90) days' written notice; provided, however, that Customer shall remain liable for all Fees for the remainder of the then-current Subscription Term."

> Plain: "You can cancel any time with 90 days' written notice, but you still owe the fees for the rest of the current term. Cancelling early doesn't save you money; it just ends the service."

Note what stayed: 90 days, written, and the fact that the obligation survives. What went: "for convenience", "provided, however", "then-current".

## References

- `references/plain-language-patterns.md` — before/after patterns for the constructions that resist simplification: nominalizations, passive chains, hedging stacks, jargon families, and sentence surgery. Read when a passage won't come apart.
- `references/domain-notes.md` — what is load-bearing in legal, medical, financial, engineering, and academic text, and the usual traps in each. Read when the input comes from one of these fields and the reader will act on your version.

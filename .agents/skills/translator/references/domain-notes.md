# Domain notes: what is load-bearing, and the usual traps

Read the section for the field the input comes from. Each one lists what must survive the translation, the traps that produce a fluent but wrong version, and the shape readers in that situation usually want.

## Contents

1. Legal and contracts
2. Medical and health
3. Financial
4. Engineering, incidents, and API documentation
5. Academic and scientific
6. Government, policy, and compliance

---

## 1. Legal and contracts

**Load-bearing**
- Every number: notice periods, cure periods, caps, percentages, term lengths, interest rates.
- Direction of each obligation: who must do it, who benefits, who pays.
- Modal verbs. "May" is permission; "shall/must" is obligation; "will" is often a promise. Don't flatten them.
- Defined terms (capitalized words like "Services", "Customer Data", "Fees"). You can lowercase and plain-name them, but the scope of the defined term matters: "Fees" might exclude taxes; "Services" might exclude beta features.
- "Sole and exclusive remedy", "notwithstanding", "provided that", "except as set forth in": these change what other clauses mean. Track what they point at.
- Survival: which obligations continue after the contract ends.

**Traps**
- Turning "may" into "will" ("they will increase prices" when the contract says they *may*).
- Dropping the cap while keeping the credit ("you get 10% back" without "up to 30% per month, and only if you ask within 30 days").
- Summarizing the limitation of liability as "they aren't liable for anything". Usually it's a cap plus exclusions plus carve-outs, and the carve-outs (payment, indemnity, confidentiality) matter.
- Reading a clause in isolation when another clause overrides it.
- Adding advice that sounds legal ("this is standard", "you should be fine"). You can say a term is common or unusual in general, but don't reassure.

**Shape readers want**: what am I committing to, what can they do to me, what can I do to them, when do I have to act (with dates or day counts), and a list of questions to ask before signing. Group by topic, not by clause number.

## 2. Medical and health

**Load-bearing**
- Doses, frequencies, durations, thresholds (e.g., "if fever exceeds 39°C for more than 48 hours").
- Certainty language. "Consistent with", "suggestive of", "cannot rule out", "likely" each mean different things and a reader may be making decisions on them.
- Which findings are normal vs. abnormal vs. incidental.
- What the document says to do next, and by whom (patient, GP, specialist).
- Negatives: "no evidence of" is a real finding, not filler.

**Traps**
- Making a tentative finding sound diagnosed, or an incidental finding sound urgent.
- Dropping the reference range that gives a number meaning ("your value is 7.2" means nothing without "normal is 4–6").
- Reassuring ("this is nothing to worry about") when the source doesn't.
- Replacing a term the patient will hear from every clinician (the diagnosis name) with a paraphrase and never giving the real word.

**Shape readers want**: what did they find, what does it mean, what happens next, what should I ask. Keep the clinical term next to the plain explanation ("a benign cyst — a fluid-filled sac that isn't cancer").

## 3. Financial

**Load-bearing**
- Rates, and what they apply to (annual vs. monthly, on principal vs. on balance).
- Fees and when they trigger.
- Time: compounding periods, lock-ups, notice windows, vesting schedules.
- Conditionality of returns ("target", "projected", "historical" are not "guaranteed").
- Who bears which risk.

**Traps**
- Converting units loosely (basis points, APR vs. APY, gross vs. net).
- Reporting a projection as an outcome.
- Dropping a fee because it's small or conditional.
- Rounding a threshold that triggers something.

**Shape readers want**: what does it cost me, what do I get, when, and what could go wrong. Show worked examples with the reader's numbers if they gave them, otherwise with clearly labelled hypothetical ones.

## 4. Engineering, incidents, and API documentation

**Load-bearing**
- Impact in user terms: how many, how long, what they saw, whether anything was lost.
- The causal chain, in order. Non-engineers can follow "A caused B caused C" if each step is concrete; they can't follow it if steps are skipped or reordered.
- Whether it is fixed, mitigated, or still open, and what's being done.
- Timestamps and durations.
- For API docs: exact names of things the reader will type or see (endpoints, error codes, field names), even when everything around them is paraphrased.

**Traps**
- Explaining the mechanism at the expense of the impact. A VP wants "22% of checkouts failed for 38 minutes, 630 orders were lost, it's fixed, here's what prevents a repeat" before any mention of pools or caches.
- Blame drift: postmortems are usually written blamelessly; keep that. Don't turn "the change was based on incomplete data" into "someone made a mistake".
- Dropping the "why it wasn't caught" section. Managers care about it as much as the root cause.
- For API docs, paraphrasing an identifier so the reader can't find it in the real interface.

**Shape readers want** (incident): what happened, who was affected and how much, why, why it wasn't caught, what's done and what's planned (with dates). For concepts: what it is, why we need it, an example, the thing to remember.

## 5. Academic and scientific

**Load-bearing**
- Sample size, population, and setting. A result in 40 mice is not a result in people.
- Effect size, not just significance. "Statistically significant" can be tiny.
- Study design (observational vs. randomized) because it decides whether "linked to" can become "causes".
- The authors' own limitations and hedges.
- What was actually measured vs. what it's a proxy for.

**Traps**
- "Linked to" → "causes".
- "Suggests" → "shows" or "proves".
- Dropping the comparison group ("improved by 30%" compared to what?).
- Reporting relative risk without absolute risk ("doubles the risk" from 1 in 10,000 to 2 in 10,000).
- Leaving out the limitations paragraph because it's boring.

**Shape readers want**: what question did they ask, what did they do, what did they find, how sure should I be, what does it not tell us. Numbers with their comparison and their context.

## 6. Government, policy, and compliance

**Load-bearing**
- Who the rule applies to, and who is exempt.
- Effective dates and transition periods.
- Thresholds that put someone in or out of scope.
- Penalties and who enforces.
- Whether something is required, recommended, or permitted.

**Traps**
- Generalizing scope ("all businesses" when it says "businesses with 50+ employees").
- Collapsing "guidance" and "requirement".
- Missing the definitions section, where the scope is actually set.

**Shape readers want**: does this apply to me, what do I have to do, by when, what happens if I don't, and where the grey areas are.

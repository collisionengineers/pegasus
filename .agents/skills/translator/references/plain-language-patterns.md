# Plain-language patterns

Before/after patterns for the constructions that most often resist simplification. Each one names the problem, explains why it hurts the reader, and shows the move. Use these as moves, not rules: the goal is a reader who gets it on the first pass.

## Contents

1. Nominalizations (verbs hiding as nouns)
2. Passive voice and missing actors
3. Hedging stacks and throat-clearing
4. Sentence surgery (splitting long sentences safely)
5. Jargon families and their plain equivalents
6. Conditions and exceptions without losing logic
7. Abstractions → concrete
8. Analogies: when and how
9. Things that look like jargon but aren't

---

## 1. Nominalizations

A verb turned into a noun ("decide" → "decision", "fail" → "failure") pulls the action out of the sentence and forces a weak verb ("make", "perform", "conduct") to carry it. Readers process actions faster than abstractions.

| Before | After |
|---|---|
| The team made the decision to perform a rollback of the release. | The team rolled the release back. |
| Termination of the agreement by Customer requires provision of written notice. | To end the agreement, you must give written notice. |
| Failure to comply will result in the suspension of access. | If you don't follow this, they can suspend your access. |
| The system carries out validation of the input prior to processing. | The system checks the input before it processes it. |

Spot them: words ending in -tion, -ment, -ance, -ence, -ity, -ing used as nouns, especially after "the", "make", "perform", "conduct", "effect".

## 2. Passive voice and missing actors

Passive isn't wrong; it's wrong when it hides who does what and the reader needs to know. Contracts and incident reports are full of "will be suspended", "was identified", "is required" with no actor. Put the actor back. If the source genuinely doesn't say who, that's worth flagging ("it doesn't say who decides this").

| Before | After |
|---|---|
| Requests exceeding the limit will be rejected. | The server rejects requests over the limit. |
| It was determined that the pool size was insufficient. | The on-call engineer found the pool was too small. |
| Payment must be received within 30 days. | You must pay within 30 days. |
| Data may be retained in backups. | They may keep your data in backups. |

## 3. Hedging stacks and throat-clearing

Formal writing wraps claims in layers that carry no information: "it should be noted that", "in order to", "for the purposes of", "with respect to", "in the event that", "at this point in time", "it is important to understand that". Strip them. Keep hedges that carry information ("preliminary", "in most cases", "we believe") because they tell the reader how much to trust the claim.

| Before | After |
|---|---|
| It should be noted that, in the event that the threshold is exceeded, alerts may be generated. | If the threshold is exceeded, you may get an alert. |
| In order to facilitate the onboarding process, users are required to complete verification. | To get started, you need to verify your identity. |
| Preliminary analysis suggests that the effect may be attributable to selection bias. | Early analysis suggests the effect may come from selection bias. (Keep "early", "suggests", "may": they're real hedges.) |

## 4. Sentence surgery

Long sentences usually contain several claims joined by "which", "provided that", "and", semicolons, or parentheses. Find the joints and cut. Each piece gets its own subject and verb. Then check that the logical relationship between the pieces (because, unless, then, but) survived as a word, not as a comma.

Before:
> Provider may suspend Customer's access to the Services upon fifteen (15) days' written notice if any undisputed amount remains unpaid more than thirty (30) days after its due date, provided that Provider shall not suspend access during any period in which Customer is disputing an invoice in good faith.

Joints: "if", "provided that". Three claims: they can suspend / the trigger / the exception.

After:
> If you're more than 30 days late paying an undisputed invoice, they can cut off your access after giving you 15 days' written notice. They can't do this while you're disputing an invoice in good faith.

The exception became its own sentence and kept the word "can't". The two numbers survived. "Undisputed" survived because it's a condition.

## 5. Jargon families

Groups of terms that show up together. For each, the plain move, and when to keep the term.

**Engineering / systems**
| Term | Plain | Keep if… |
|---|---|---|
| latency | how long a request takes / delay | the reader sees dashboards with it |
| p50 / p95 / p99 | typical / slowest 5% / slowest 1% of requests | they'll see it on graphs; define once |
| throughput | how much it handles per second | rarely |
| timeout | gave up waiting | fine to keep, it's near-plain |
| connection pool exhaustion | ran out of connections to the other service | never for non-engineers |
| circuit breaker | a safety switch that stops sending requests to something that's failing | keep only if it's the fix they'll hear about |
| idempotent | safe to repeat; doing it twice has the same effect as once | keep with definition if the reader works with the API |
| race condition | two things happening at once in an order nobody planned for | rarely |
| rollback | undo the release / go back to the previous version | fine to keep with a gloss |
| cache / TTL | a saved copy / how long the saved copy is trusted before refreshing | keep "cache"; replace TTL |
| deprecated | still works, but being phased out; don't build on it | keep with gloss |

**Legal / contracts**
| Term | Plain | Keep if… |
|---|---|---|
| shall | must | never |
| indemnify | cover their costs if they get sued because of something you did | keep with gloss; it's a real obligation they'll hear again |
| limitation of liability | the most they'd ever have to pay you (and vice versa) | gloss |
| consequential damages | knock-on losses (lost sales, lost data) as opposed to direct costs | gloss |
| terminate for convenience | cancel for any reason / without needing a cause | never |
| material breach | a serious violation of the contract | gloss |
| cure period | time to fix the problem before the other side can act | gloss |
| sole and exclusive remedy | this is all you get; you can't also sue for more | never, but the meaning must survive |
| commercially reasonable efforts | they'll try, but it's not a guarantee | flag as vague |
| pro rata | in proportion (e.g., refund for the unused months) | never |

**Medical**
| Term | Plain | Keep if… |
|---|---|---|
| benign / malignant | not cancer / cancer | keep both words, they'll hear them |
| acute / chronic | sudden and short-term / long-term | gloss |
| prognosis | what's likely to happen next | gloss |
| contraindicated | shouldn't be used with / in this case | never |
| adverse event | side effect or harm | rarely |
| efficacy | how well it works | never |
| placebo-controlled | compared against a fake treatment | gloss |

**Finance**
| Term | Plain | Keep if… |
|---|---|---|
| accrue | build up over time | rarely |
| principal | the amount originally borrowed / invested | keep, gloss |
| basis points | hundredths of a percent (25 bp = 0.25%) | convert; show both |
| liquidity | how quickly you can turn it into cash | gloss |
| amortization | paying off gradually in scheduled chunks | gloss |
| net of | after subtracting | never |

**Academic / research**
| Term | Plain | Keep if… |
|---|---|---|
| statistically significant | unlikely to be chance (but not necessarily large or important) | gloss, and don't let it become "proven" |
| effect size | how big the difference actually was | gloss |
| cohort | the group of people studied | never |
| confounder | something else that could explain the result | gloss |
| in vitro / in vivo | in a dish / in living animals or people | never |
| n = 40 | 40 participants | never |

## 6. Conditions and exceptions

The most common fidelity failure: the plain version is cleaner because a condition quietly disappeared. Technique: before rewriting a clause, list its conditions as bullets. After rewriting, tick them off.

Original: "Customer shall be entitled to a service credit equal to 10% of monthly Fees, provided that Customer submits a written request within 30 days following the end of the affected month, and provided further that aggregate credits shall not exceed 30% of monthly Fees."

Conditions: (a) 10% credit, (b) written request, (c) within 30 days after the month ends, (d) capped at 30% per month.

Plain: "If they miss the uptime target, you can get 10% of that month's fee back as a credit. You have to ask in writing within 30 days after the month ends, and credits are capped at 30% of a month's fee no matter how many failures there were."

All four survived.

## 7. Abstractions → concrete

Abstract nouns ("efficiency initiative", "resource constraints", "degraded experience", "exposure") make sense to insiders who already know the concrete thing. For outsiders, name the concrete thing.

| Before | After |
|---|---|
| Users experienced a degraded checkout experience. | About 1 in 5 people trying to check out got an error or a spinning page. |
| The change was part of a resource-efficiency initiative. | The change was meant to save server costs. |
| This creates material exposure for the company. | If this goes wrong, the company could owe a lot of money. |

Where the source gives a number, use it. Where it doesn't, don't invent one; describe the kind of thing instead.

## 8. Analogies

Use an analogy when the *shape* of an idea is what's hard: something recursive, something with a feedback loop, something where the order of events matters. Don't use one for a term that's just unfamiliar vocabulary; a definition is shorter and more accurate.

Rules that keep analogies honest:
- One analogy per concept. Mixing two ("it's like a library card, but also like a thermostat") loses the reader.
- Keep it to two or three sentences, then return to the real thing.
- If the analogy breaks in a way that matters ("unlike a real key, this one expires"), say so in one clause.
- Prefer everyday domains: queues at a counter, keys and locks, receipts, recipes, mail, plumbing.

Example, for "thundering herd" after cache expiry:
> Imagine a shop where everyone's coffee goes cold at the exact same moment, so the whole room rushes the counter at once. That's what happened: thousands of saved prices expired at the same second, and every checkout asked the pricing system for fresh numbers simultaneously.

## 9. Things that look like jargon but aren't

Some words are technical and also the clearest available word. Don't replace them with something vaguer: "server", "password", "invoice", "refund", "backup", "encrypt", "browser", "download", "cancel". Readers know these. Replacing "encrypt" with "scramble so nobody can read it" is fine once as a gloss, but using it every time makes the text longer and less precise.

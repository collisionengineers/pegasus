2026-08-21: operator approved the mapping including rules 5 and 7, with the
constraint that **the labels and rules are QDOS-specific** — they belong in
`QdosInstructionExtractionPolicy` (its definitions and steps), never as
provider-neutral engine behaviour. Consequence for the landed work: the
`TP `-prefix guard currently sits in the engine's label regexes and encodes
QDOS letter grammar — it moves to policy-supplied configuration in the
follow-up ticket implementing rules 5 and 7.

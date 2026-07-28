# Pilot and acceptance

## Synthetic pilot

Prepare a small non-sensitive corpus covering:

- exact terminology;
- synonyms and paraphrases;
- documents with overlapping subjects;
- metadata tags and sources;
- deliberately irrelevant material;
- embedded prompt-injection text treated as ordinary evidence.

Create labelled queries with expected document/chunk citations and explicit no-answer cases.

## Acceptance scenarios

- Text/file write returns pending state and a job ID.
- Worker moves valid content to ready and malformed/empty content to failed.
- Lookup ignores non-ready documents and returns correct stable citations.
- HTTP and stdio expose identical tool schemas and safety annotations.
- Duplicate writes return the existing document without creating a second job.
- View-all pagination is stable across pages and exposes no complete document bodies.
- Reader/contributor/admin boundaries are enforced.
- Remove purges source and chunks and leaves only a tombstone.
- Replaying an old job or rebuilding from active sources does not resurrect removed content.
- Export/import restores active knowledge into a fresh environment.

## Promotion evidence

Attach the labelled retrieval report, provider comparison, cost calculation, backup/restore result,
deletion proof, and approved target account/region/SKU/corpus/cap. Without all six, the implementation
remains a prototype.

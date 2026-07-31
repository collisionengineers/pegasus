# ADR 0003: .NET 10 runtime cutover

Status: Accepted for the Collision Brain v1 clean cutover

Replace the TypeScript/Node runtime with one independently buildable .NET 10
package-local executable. The executable selects `api`, `worker`, `stdio`,
admin, benchmark, and healthcheck subcommands; API and worker remain separate
processes but share one published artifact.

The existing workspace boundary can carry this replacement because Collision
Brain is already an independently buildable source import with no Pegasus caller,
application reference, dynamic load, or deployment registration. This change
therefore adds no Pegasus project, top-level runtime boundary, or integration
contract. `Pegasus.Core` retains business policy; Collision Brain remains an AI
Centre retrieval/ingestion experiment and does not generate answers.

Docker and PostgreSQL remain independent of language. Docker continues to provide
optional local orchestration for PostgreSQL/pgvector and shared object storage;
.NET does not require Docker for memory-driver operation. Direct SQL/Npgsql and
the existing pgvector migration remain the persistence contract rather than being
replaced by EF or a generic vector abstraction.

The cutover preserves document identity, content hashes, upload references, SQL
migration lineage, MCP tool names and wire shapes, embedding descriptors,
export/import schema, lifecycle transitions, deletion tombstones, and local
verification boundaries. Existing `migrations/001_initial.sql`, benchmarks,
corpus, and untracked intake material remain unchanged.

Deferred capabilities are provider/corpus/model selection, production caller
integration, deployment, live cloud verification, operator acceptance, answer
generation, OCR, and any dormant vector or ingestion platform. The rewrite keeps
only the seams and data identities required to activate a later capability through
its own accepted change; it does not build dormant implementations.

# ADR 0001: Provider boundaries

Status: Accepted

The core application depends on ports for document persistence, object storage, embeddings, and
authentication. Provider packages may appear only in adapters or deployment code. MCP schemas and
domain services remain provider neutral.

This permits local testing, one researched reference deployment, and later migration without
changing the four public tools.

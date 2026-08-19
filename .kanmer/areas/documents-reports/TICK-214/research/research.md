# Research — MCPB host and distribution boundary

## Question

What long-term MCPB host/distribution boundary survives CollisionRenderer integration?

## Findings

1. The renderer workspace MCPB is a standalone stdio/local-file host with its own manifest, build script, browser-install tool, output directory, and local file descriptors.
2. The operator's binding direction says CollisionRenderer is not separate and explicitly excludes a separate MCP host, package, API, repository, or Azure service.
3. TICK-203 research found no caller-backed need to add renderer tools to Pegasus's existing authenticated Automation MCP. Automatic generation is an internal Core-owned workflow after accepted assessment completion.
4. Preserving an MCPB would keep a second caller and distribution lifecycle, enable arbitrary payload/template/path rendering outside case policy/custody, and contradict the approved monolith boundary.
5. Useful renderer-engine tests can migrate without retaining MCP transport/packaging.

## Implications

- Retire the MCPB manifest, build script, stdio host, output access, browser installation tool, and MCP-specific tests during integration.
- No long-term renderer MCPB distribution exists.
- Pegasus's existing MCP stays unchanged for this capability. Any future report status tool needs a separately governed application use case and returns Pegasus identities, never local artifacts.

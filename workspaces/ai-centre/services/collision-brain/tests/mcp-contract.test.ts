import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { InMemoryTransport } from "@modelcontextprotocol/sdk/inMemory.js";
import { afterEach, describe, expect, it } from "vitest";
import { createMcpServer } from "../src/mcp/server.js";
import { admin, createTestService } from "./helpers.js";

describe("MCP contract", () => {
  const closeables: Array<{ close(): Promise<void> }> = [];

  afterEach(async () => {
    await Promise.all(closeables.splice(0).map((item) => item.close()));
  });

  it("exposes the four required tools with safety annotations", async () => {
    const { rag } = createTestService();
    const server = createMcpServer(rag, admin);
    const client = new Client({ name: "contract-test", version: "1.0.0" });
    const [clientTransport, serverTransport] = InMemoryTransport.createLinkedPair();
    closeables.push(client, server);
    await Promise.all([
      client.connect(clientTransport),
      server.connect(serverTransport),
    ]);

    const tools = await client.listTools();
    expect(tools.tools.map((tool) => tool.name)).toEqual([
      "lookup",
      "write",
      "view_all",
      "remove",
    ]);
    expect(tools.tools.find((tool) => tool.name === "lookup")?.annotations?.readOnlyHint)
      .toBe(true);
    expect(tools.tools.find((tool) => tool.name === "remove")?.annotations?.destructiveHint)
      .toBe(true);

    const write = await client.callTool({
      name: "write",
      arguments: { title: "MCP document", text: "MCP contract test knowledge." },
    });
    expect(write.isError).not.toBe(true);
    await rag.processOneJob();

    const lookup = await client.callTool({
      name: "lookup",
      arguments: { query: "contract knowledge" },
    });
    expect(lookup.isError).not.toBe(true);
    expect(lookup.structuredContent).toMatchObject({ count: 1 });
  });
});

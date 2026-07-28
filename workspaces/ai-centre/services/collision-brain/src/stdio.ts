import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StreamableHTTPClientTransport } from "@modelcontextprotocol/sdk/client/streamableHttp.js";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { loadConfig } from "./config.js";
import { registerRagTools } from "./mcp/register-tools.js";

const config = loadConfig();
const headers = config.ragHttpBearerToken
  ? { authorization: `Bearer ${config.ragHttpBearerToken}` }
  : {};
const remoteTransport = new StreamableHTTPClientTransport(new URL(config.ragHttpUrl), {
  requestInit: { headers },
});
const client = new Client({ name: "collisionengineers-rag-stdio-proxy", version: "0.1.0" });
await client.connect(remoteTransport);

async function callRemote(name: string, arguments_: Record<string, unknown>) {
  const result = await client.callTool({ name, arguments: arguments_ });
  const content = Array.isArray(result.content)
    ? result.content.filter((item): item is { type: "text"; text: string } =>
      typeof item === "object" &&
      item !== null &&
      "type" in item &&
      item.type === "text" &&
      "text" in item &&
      typeof item.text === "string")
    : [];
  if (result.isError) {
    const message = content
      .map((item) => item.text)
      .join("\n");
    throw new Error(message || `${name} failed`);
  }
  if (result.structuredContent &&
      typeof result.structuredContent === "object" &&
      !Array.isArray(result.structuredContent)) {
    return result.structuredContent as Record<string, unknown>;
  }
  const text = content[0];
  return text ? JSON.parse(text.text) as Record<string, unknown> : {};
}

const server = new McpServer({ name: "collisionengineers-rag-stdio", version: "0.1.0" });
registerRagTools(server, {
  lookup: (arguments_) => callRemote("lookup", arguments_),
  write: (arguments_) => callRemote("write", arguments_),
  viewAll: (arguments_) => callRemote("view_all", arguments_),
  remove: (arguments_) => callRemote("remove", arguments_),
});

await server.connect(new StdioServerTransport());

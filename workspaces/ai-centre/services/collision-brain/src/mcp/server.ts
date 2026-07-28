import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { Principal } from "../domain/types.js";
import type { RagService } from "../services/rag-service.js";
import { registerRagTools } from "./register-tools.js";

export function createMcpServer(rag: RagService, principal: Principal): McpServer {
  const server = new McpServer({
    name: "collisionengineers-rag",
    version: "0.1.0",
  });
  registerRagTools(server, {
    lookup: async (arguments_) => rag.lookup(principal, {
      query: arguments_.query,
      ...(arguments_.limit ? { limit: arguments_.limit } : {}),
      ...(arguments_.filters ? {
        filters: {
          ...(arguments_.filters.source ? { source: arguments_.filters.source } : {}),
          ...(arguments_.filters.tags ? { tags: arguments_.filters.tags } : {}),
          ...(arguments_.filters.document_ids
            ? { documentIds: arguments_.filters.document_ids }
            : {}),
        },
      } : {}),
    }),
    write: async (arguments_) => rag.write(principal, {
      title: arguments_.title,
      ...(arguments_.text ? { text: arguments_.text } : {}),
      ...(arguments_.upload_ref ? { uploadRef: arguments_.upload_ref } : {}),
      ...(arguments_.source ? { source: arguments_.source } : {}),
      ...(arguments_.tags ? { tags: arguments_.tags } : {}),
    }),
    viewAll: async (arguments_) => rag.viewAll(principal, {
      ...(arguments_.cursor ? { cursor: arguments_.cursor } : {}),
      ...(arguments_.limit ? { limit: arguments_.limit } : {}),
      ...(arguments_.status ? { status: arguments_.status } : {}),
    }),
    remove: async (arguments_) =>
      rag.remove(principal, arguments_.document_id, arguments_.confirm),
  });
  return server;
}

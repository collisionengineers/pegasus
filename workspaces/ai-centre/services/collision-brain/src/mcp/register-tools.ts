import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { documentStatuses } from "../domain/types.js";
import { errorToolResult, jsonToolResult } from "./tool-result.js";

const lookupArguments = {
  query: z.string().min(1).max(8000).describe("Natural-language or keyword retrieval query"),
  limit: z.number().int().min(1).max(25).optional()
    .describe("Maximum ranked passages to return; defaults to 8"),
  filters: z.object({
    source: z.string().min(1).optional(),
    tags: z.array(z.string().min(1)).max(20).optional(),
    document_ids: z.array(z.string().uuid()).max(100).optional(),
  }).optional(),
};

const writeArguments = {
  title: z.string().min(1).max(500),
  text: z.string().min(1).optional()
    .describe("Pasted or AI-extracted text; mutually exclusive with upload_ref"),
  upload_ref: z.string().min(1).optional()
    .describe("Short-lived reference returned by POST /uploads; mutually exclusive with text"),
  source: z.string().min(1).max(1000).optional(),
  tags: z.array(z.string().min(1).max(100)).max(20).optional(),
};

const viewAllArguments = {
  cursor: z.string().min(1).optional(),
  limit: z.number().int().min(1).max(100).optional(),
  status: z.enum(documentStatuses).optional(),
};

const removeArguments = {
  document_id: z.string().uuid(),
  confirm: z.literal(true).describe("Must be true to permanently purge document content"),
};

export interface RagToolHandlers {
  lookup(arguments_: z.infer<z.ZodObject<typeof lookupArguments>>): Promise<Record<string, unknown>>;
  write(arguments_: z.infer<z.ZodObject<typeof writeArguments>>): Promise<Record<string, unknown>>;
  viewAll(arguments_: z.infer<z.ZodObject<typeof viewAllArguments>>): Promise<Record<string, unknown>>;
  remove(arguments_: z.infer<z.ZodObject<typeof removeArguments>>): Promise<Record<string, unknown>>;
}

async function run(tool: string, operation: () => Promise<Record<string, unknown>>) {
  try {
    return jsonToolResult(await operation());
  } catch (error) {
    return errorToolResult(tool, error);
  }
}

export function registerRagTools(server: McpServer, handlers: RagToolHandlers): void {
  server.registerTool(
    "lookup",
    {
      title: "Look up knowledge",
      description: "Retrieve ranked source passages with stable citations. This read-only tool does not generate an answer and is safe for client-controlled automatic invocation.",
      inputSchema: lookupArguments,
      annotations: {
        readOnlyHint: true,
        destructiveHint: false,
        idempotentHint: true,
        openWorldHint: false,
      },
    },
    async (arguments_) => run("lookup", () => handlers.lookup(arguments_)),
  );

  server.registerTool(
    "write",
    {
      title: "Add knowledge",
      description: "Queue pasted text or a securely staged file for asynchronous extraction, chunking, embedding, and indexing.",
      inputSchema: writeArguments,
      annotations: {
        readOnlyHint: false,
        destructiveHint: false,
        idempotentHint: true,
        openWorldHint: false,
      },
    },
    async (arguments_) => run("write", () => handlers.write(arguments_)),
  );

  server.registerTool(
    "view_all",
    {
      title: "View all documents",
      description: "Return a paginated document registry with processing status and metadata, without returning complete document bodies.",
      inputSchema: viewAllArguments,
      annotations: {
        readOnlyHint: true,
        destructiveHint: false,
        idempotentHint: true,
        openWorldHint: false,
      },
    },
    async (arguments_) => run("view_all", () => handlers.viewAll(arguments_)),
  );

  server.registerTool(
    "remove",
    {
      title: "Remove knowledge",
      description: "Permanently purge a document's source and searchable chunks while retaining a content-free audit tombstone.",
      inputSchema: removeArguments,
      annotations: {
        readOnlyHint: false,
        destructiveHint: true,
        idempotentHint: false,
        openWorldHint: false,
      },
    },
    async (arguments_) => run("remove", () => handlers.remove(arguments_)),
  );
}

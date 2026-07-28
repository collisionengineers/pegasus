import { randomUUID } from "node:crypto";
import cors from "cors";
import express, { type NextFunction, type Request, type Response } from "express";
import multer from "multer";
import { StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/streamableHttp.js";
import { type AuthenticatedRequest, requireAuthentication, requireRole } from "./auth.js";
import type { ApplicationContext } from "./context.js";
import { AppError, ValidationError } from "./domain/errors.js";
import { isSupportedFilename, supportedExtensions } from "./ingestion/extract-text.js";
import { createMcpServer } from "./mcp/server.js";
import { logger } from "./observability/logger.js";

export function createHttpApp(context: ApplicationContext) {
  const app = express();
  const authenticate = requireAuthentication(context.auth);
  const upload = multer({
    storage: multer.memoryStorage(),
    limits: { fileSize: context.config.maxUploadBytes, files: 1 },
  });

  app.disable("x-powered-by");
  app.use(cors());
  app.use(express.json({ limit: "1mb" }));

  app.get("/health", (_request, response) => {
    response.json({
      status: "ok",
      service: "collisionengineers-rag",
      version: "0.1.0",
    });
  });

  app.post(
    "/uploads",
    authenticate,
    upload.single("file"),
    async (request: AuthenticatedRequest, response, next) => {
      try {
        requireRole(request.principal as NonNullable<AuthenticatedRequest["principal"]>, "contributor");
        if (!request.file) throw new ValidationError("Multipart field 'file' is required");
        if (!isSupportedFilename(request.file.originalname)) {
          throw new ValidationError(
            `Unsupported file extension; supported extensions: ${supportedExtensions.join(", ")}`,
          );
        }
        const staged = await context.uploads.stage({
          body: request.file.buffer,
          filename: request.file.originalname,
          contentType: request.file.mimetype || "application/octet-stream",
        });
        response.status(201).json({
          upload_ref: staged.uploadRef,
          content_hash: staged.contentHash,
          expires_at: staged.expiresAt,
        });
      } catch (error) {
        next(error);
      }
    },
  );

  app.post("/mcp", authenticate, async (request: AuthenticatedRequest, response, next) => {
    const server = createMcpServer(
      context.rag,
      request.principal as NonNullable<AuthenticatedRequest["principal"]>,
    );
    const transport = new StreamableHTTPServerTransport({ sessionIdGenerator: undefined });
    response.on("close", () => {
      void transport.close();
      void server.close();
    });
    try {
      await server.connect(transport);
      await transport.handleRequest(request, response, request.body);
    } catch (error) {
      next(error);
    }
  });

  app.use((_request, response) => {
    response.status(404).json({ error: "not_found", message: "Route was not found" });
  });

  app.use((error: unknown, request: Request, response: Response, _next: NextFunction) => {
    const requestId = request.header("x-request-id") ?? randomUUID();
    if (error instanceof AppError) {
      response.status(error.statusCode).json({
        error: error.code,
        message: error.message,
        request_id: requestId,
      });
      return;
    }
    if (error instanceof multer.MulterError) {
      response.status(400).json({
        error: "upload_error",
        message: error.message,
        request_id: requestId,
      });
      return;
    }
    logger.error("HTTP request failed", {
      requestId,
      method: request.method,
      path: request.path,
      errorType: error instanceof Error ? error.name : "unknown",
    });
    response.status(500).json({
      error: "internal_error",
      message: "Internal server error",
      request_id: requestId,
    });
  });

  return app;
}

import { AppError } from "../domain/errors.js";
import { logger } from "../observability/logger.js";

export function jsonToolResult(value: Record<string, unknown>) {
  return {
    content: [{ type: "text" as const, text: JSON.stringify(value, null, 2) }],
    structuredContent: value,
  };
}

export function errorToolResult(tool: string, error: unknown) {
  const appError = error instanceof AppError ? error : null;
  if (!appError) {
    logger.error("MCP tool failed", {
      tool,
      errorType: error instanceof Error ? error.name : "unknown",
    });
  }
  const message = appError?.message ?? "Internal server error";
  const value = {
    error: appError?.code ?? "internal_error",
    message: `${tool} failed: ${message}`,
  };
  return {
    content: [{ type: "text" as const, text: JSON.stringify(value, null, 2) }],
    structuredContent: value,
    isError: true,
  };
}

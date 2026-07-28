export interface LogFields {
  [key: string]: boolean | number | string | null | undefined;
}

function write(level: "info" | "warn" | "error", message: string, fields: LogFields = {}): void {
  const safeFields = Object.fromEntries(
    Object.entries(fields).filter(([, value]) => value !== undefined),
  );
  process.stderr.write(`${JSON.stringify({
    timestamp: new Date().toISOString(),
    level,
    message,
    ...safeFields,
  })}\n`);
}

export const logger = {
  info: (message: string, fields?: LogFields) => write("info", message, fields),
  warn: (message: string, fields?: LogFields) => write("warn", message, fields),
  error: (message: string, fields?: LogFields) => write("error", message, fields),
};

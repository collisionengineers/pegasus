import { loadConfig } from "./config.js";
import { createApplicationContext } from "./context.js";
import { createHttpApp } from "./http-app.js";
import { logger } from "./observability/logger.js";

const config = loadConfig();
const context = await createApplicationContext(config);
const app = createHttpApp(context);

const server = app.listen(config.port, () => {
  logger.info("RAG API listening", {
    port: config.port,
    authMode: config.authMode,
    repositoryDriver: config.repositoryDriver,
  });
});

async function shutdown(signal: string): Promise<void> {
  logger.info("Shutting down RAG API", { signal });
  server.close(async () => {
    await context.close();
    process.exit(0);
  });
}

process.on("SIGINT", () => void shutdown("SIGINT"));
process.on("SIGTERM", () => void shutdown("SIGTERM"));

import { setTimeout as delay } from "node:timers/promises";
import { loadConfig } from "./config.js";
import { createApplicationContext } from "./context.js";
import { logger } from "./observability/logger.js";

const config = loadConfig();
const context = await createApplicationContext(config);
let stopping = false;

process.on("SIGINT", () => {
  stopping = true;
});
process.on("SIGTERM", () => {
  stopping = true;
});

logger.info("Ingestion worker started", { pollMilliseconds: config.workerPollMs });
while (!stopping) {
  const processed = await context.rag.processOneJob();
  if (!processed) {
    const removed = await context.rag.cleanupExpiredUploads();
    if (removed > 0) logger.info("Expired staged uploads removed", { count: removed });
    await delay(config.workerPollMs);
  }
}
await context.close();
logger.info("Ingestion worker stopped");

// The Azure Functions entry: one sweep per timer tick. The schedule is the
// CLASSIFIER_CRON app setting; the Functions host never overlaps two runs.

import { app } from "@azure/functions";
import { runClassifier } from "./run.js";

app.timer("classify", {
  schedule: "%CLASSIFIER_CRON%",
  handler: async (_timer, context) => {
    const summary = await runClassifier((message, properties) =>
      context.log(message, properties ?? {}));
    context.log("run complete", summary);
  },
});

// A local run of one sweep: npm run build && npm start. Settings come from
// .env (see .env.example); keep CLASSIFIER_DRY_RUN=true unless a write to
// Pegasus is approved.

import { runClassifier } from "./run.js";

try {
  const summary = await runClassifier((message, properties) =>
    console.log(JSON.stringify({ message, ...properties })));
  console.log(JSON.stringify({ message: "run complete", ...summary }));
} catch (error) {
  console.error(error);
  process.exitCode = 1;
}

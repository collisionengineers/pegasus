// Which mail the agent may touch. This is enforced here, in code, never left
// to the prompt.
//
// A message is eligible when it is Unclassified or has no Case linked, and
// its current classification was decided by the pipeline (system-worker).
// A decision by staff, by any Automation caller (including this agent's own
// earlier runs) or by a Principal is left alone.

import type { MailDetail, MailSummary } from "./pegasus.js";

export function decider(actor: string): string {
  const separator = actor.indexOf(":");
  return separator < 0 ? "" : actor.slice(0, separator);
}

/** From the list row alone: worth reading in full. */
export function isCandidate(summary: MailSummary): boolean {
  return summary.caseId === null || summary.classification?.outcome === "Unclassified";
}

/** From the full read, at the start of a run. */
export function isEligible(detail: MailDetail): boolean {
  return (
    detail.classification !== null &&
    decider(detail.classification.currentActor) === "system-worker" &&
    (detail.classification.current.outcome === "Unclassified" || detail.summary.caseId === null)
  );
}

/**
 * Before a write later in the same run. The message was eligible when the run
 * listed it; it stays writable unless staff have decided it since. This lets
 * the agent both link and classify one message in a run.
 */
export function isStillWritable(detail: MailDetail): boolean {
  return detail.classification !== null && decider(detail.classification.currentActor) !== "staff";
}

/**
 * What the list row says about a message. A reviewed message whose fingerprint
 * is unchanged is skipped without reading it again.
 */
export function fingerprint(summary: MailSummary): string {
  return [
    summary.classification?.outcome ?? "",
    summary.classification?.category ?? "",
    summary.caseId ?? "",
  ].join("|");
}

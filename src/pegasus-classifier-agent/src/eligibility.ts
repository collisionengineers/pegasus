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
 * listed it; it stays writable unless staff have decided it since. Another
 * caller's change that leaves the pipeline as the decider, such as a staff
 * link, is caught by hasChangedSince, not here.
 */
export function isStillWritable(detail: MailDetail): boolean {
  return detail.classification !== null && decider(detail.classification.currentActor) !== "staff";
}

/** What the run last knew of a message: its classification version and linked Case. */
export interface MessageState {
  classificationVersion: number;
  /** Lower case, so Case ids compare whichever way Pegasus writes them. */
  caseId: string | null;
}

export function stateOf(detail: MailDetail): MessageState | null {
  return detail.classification === null
    ? null
    : {
        classificationVersion: detail.classification.version,
        caseId: detail.summary.caseId?.toLowerCase() ?? null,
      };
}

/**
 * Before any write: the message no longer reads as the run last knew it, as
 * listed or as the run's own latest write left it. Someone else has classified
 * or linked it since, so the run leaves it alone.
 */
export function hasChangedSince(known: MessageState, detail: MailDetail): boolean {
  const current = stateOf(detail);
  return current === null
    || current.classificationVersion !== known.classificationVersion
    || current.caseId !== known.caseId;
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

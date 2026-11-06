// Builders for the tests: a listed e-mail as Pegasus returns it, a batch, and
// a direct call of one tool's handler.

import { randomUUID } from "node:crypto";
import { FixturePegasus, type FixtureCandidate } from "../src/fixture-pegasus.js";
import type { MailDetail } from "../src/pegasus.js";
import { createClassifierTools, newOutcomes, type Candidate, type RunOutcomes } from "../src/tools.js";

export interface MailOptions {
  subject?: string;
  body?: string;
  outcome?: "Unclassified" | "Classified";
  category?: string | null;
  caseType?: string | null;
  caseId?: string | null;
  actor?: string;
  version?: number;
  attachments?: { fileName: string; mediaType: string }[];
  receiptId?: string | null;
}

export function mail(options: MailOptions = {}): MailDetail {
  const id = randomUUID();
  const current = {
    outcome: options.outcome ?? "Unclassified",
    category: options.category ?? null,
    caseType: options.caseType ?? null,
    reason: "The pipeline left it.",
  };
  return {
    summary: {
      id,
      mailboxAddress: "desk@example.test",
      senderAddress: "sender@principal.test",
      effectiveSenderAddress: "sender@principal.test",
      senderDisplayName: "Sender",
      subject: options.subject ?? "Subject",
      receivedAtUtc: "2026-11-06T10:00:00Z",
      attachmentCount: options.attachments?.length ?? 0,
      caseId: options.caseId ?? null,
      caseReference: options.caseId ? "QDOS26001" : null,
      isTriageCase: false,
      intakeReceiptId: options.receiptId === undefined ? randomUUID() : options.receiptId,
      classification: current,
    },
    toAddresses: ["desk@example.test"],
    ccAddresses: [],
    bodyPlainText: options.body ?? "Body",
    attachments: (options.attachments ?? []).map((item) => ({ ...item, contentLength: 1000 })),
    thread: [],
    routeDisposition: "no_match",
    classification: {
      version: options.version ?? 1,
      currentActor: options.actor ?? "system-worker:legacy-intake",
      current,
    },
    intakeVersion: 1,
    canLinkCase: options.caseId == null,
  };
}

export interface Harness {
  pegasus: FixturePegasus;
  batch: Map<string, Candidate>;
  outcomes: RunOutcomes;
  logs: { message: string; properties?: Record<string, unknown> }[];
  /** Calls one tool's handler; returns its text and whether it is an error. */
  call(name: string, args: Record<string, unknown>): Promise<{ text: string; isError: boolean }>;
}

export function harness(
  candidates: FixtureCandidate[],
  options: { dryRun?: boolean; pegasus?: FixturePegasus; folders?: boolean; mailboxes?: boolean } = {},
): Harness {
  const pegasus = options.pegasus ?? new FixturePegasus({
    candidates,
    ...(options.folders ? { folders: [] } : {}),
    ...(options.mailboxes ? { mailboxCorpus: [] } : {}),
  });
  const batch = new Map<string, Candidate>(candidates.map((item) => [item.detail.summary.id, item]));
  const outcomes = newOutcomes();
  const logs: Harness["logs"] = [];
  const tools = createClassifierTools({
    pegasus,
    batch,
    outcomes,
    dryRun: options.dryRun ?? false,
    log: (message, properties) => logs.push({ message, ...(properties ? { properties } : {}) }),
  });
  return {
    pegasus,
    batch,
    outcomes,
    logs,
    async call(name, args) {
      const found = tools.find((item) => item.name === name);
      if (!found) {
        throw new Error(`No tool ${name}.`);
      }
      // The handlers are typed per tool; the tests pass the arguments a model would.
      const result = await (found.handler as unknown as (args: unknown, extra: unknown) => Promise<{ content: { type: string; text?: string }[]; isError?: boolean }>)(args, {});
      return { text: result.content.map((block) => block.text ?? "").join(""), isError: result.isError === true };
    },
  };
}

export function candidate(detail: MailDetail, extra: Partial<FixtureCandidate> = {}): FixtureCandidate {
  return { detail, destinations: [], ...extra };
}

/** A timed-out MCP call, as the client raises it. */
export function timedOut(): Error {
  return new Error("MCP error -32001: Request timed out");
}

// The agent's six tools, served in-process: one list, three writes and two
// read-only lookups. Each write re-reads the message from Pegasus and refuses
// it, without calling Pegasus, when staff have decided it or its
// classification version or linked Case differs from what this run listed (or
// last wrote itself); Pegasus then applies its own checks (versions, leases,
// link rules) as it does for staff. The lookups read the old system's Box
// folders and the approved mailboxes through Pegasus's own read-only tools.

import { randomUUID } from "node:crypto";
import { createSdkMcpServer, tool } from "@anthropic-ai/claude-agent-sdk";
import { z } from "zod";
import { hasChangedSince, isStillWritable, stateOf, type MessageState } from "./eligibility.js";
import type { LinkDestination, MailDetail, PegasusMail } from "./pegasus.js";
import { CorrespondenceCode, MailCategoryCodes, WorkTypes } from "./prompt.js";

export interface Candidate {
  detail: MailDetail;
  destinations: LinkDestination[];
}

export interface Logger {
  (message: string, properties?: Record<string, unknown>): void;
}

export const ServerName = "classifier";
export const ToolNames = [
  "list_eligible_emails",
  "classify_email",
  "link_case",
  "open_query",
  "search_old_system_folders",
  "search_mailboxes",
] as const;

/** Hits per lookup: enough to tell whether the old system holds the case, not a listing. */
const LookupLimit = 10;

const BodyLimit = 6000;
const ReasonPrefix = "Classifier agent: ";

function text(value: unknown, isError = false) {
  return {
    content: [{ type: "text" as const, text: typeof value === "string" ? value : JSON.stringify(value, null, 2) }],
    ...(isError ? { isError: true } : {}),
  };
}

/**
 * Caller idempotency keys: mcp:, the message without hyphens, the version acted
 * on, and for a command Pegasus replays by key, a nonce that makes each attempt
 * its own command. Pegasus keys a link's one-command lease from the key and
 * records every claim under it, so a repeated key is read as a replay of an
 * already released lease and refused, however the first attempt ended.
 */
function operationKey(kind: string, messageId: string, version: number, attempt?: string): string {
  const key = `mcp:${kind}:${messageId.replaceAll("-", "")}:${version}`;
  return attempt ? `${key}:${attempt}` : key;
}

function newAttempt(): string {
  return randomUUID().replaceAll("-", "").slice(0, 12);
}

function view({ detail, destinations }: Candidate) {
  const body = detail.bodyPlainText ?? "";
  return {
    messageId: detail.summary.id,
    receivedAtUtc: detail.summary.receivedAtUtc,
    from: detail.summary.senderDisplayName
      ? `${detail.summary.senderDisplayName} <${detail.summary.senderAddress ?? ""}>`
      : detail.summary.senderAddress,
    to: detail.toAddresses,
    cc: detail.ccAddresses,
    subject: detail.summary.subject,
    body: body.length > BodyLimit ? `${body.slice(0, BodyLimit)}\n[truncated]` : body,
    attachments: detail.attachments.map((item) => item.fileName),
    thread: detail.thread.map((item) => ({ from: item.senderAddress, subject: item.subject, receivedAtUtc: item.receivedAtUtc })),
    currentOutcome: detail.classification?.current.outcome,
    currentCategory: detail.classification?.current.category,
    linkedCase: detail.summary.caseReference,
    canLinkCase: detail.canLinkCase,
    suggestedCases: destinations.map((item) => ({
      caseId: item.caseId,
      reference: item.reference,
      registration: item.registration,
      claimant: item.claimant,
      principal: item.principal,
      state: item.isTriageCase ? `Triage ${item.triageState}` : item.state,
    })),
  };
}

export function createClassifierServer(options: {
  pegasus: PegasusMail;
  batch: Map<string, Candidate>;
  /**
   * Messages whose write failed this run, with why. They are looked at again
   * next run, up to the run's cap on failed attempts.
   */
  failed: Map<string, string>;
  dryRun: boolean;
  log: Logger;
}) {
  const { pegasus, batch, failed, dryRun, log } = options;

  // What this run last knew of each listed message: its classification version
  // and linked Case id as listed, then as this run's own writes left them.
  const known = new Map<string, MessageState>();
  for (const [messageId, { detail }] of batch) {
    const state = stateOf(detail);
    if (state) {
      known.set(messageId, state);
    }
  }

  // A write is allowed only for a message this run listed, not once staff have
  // decided it, and only while its classification version and linked Case are
  // still the ones this run knows. Anything else changed it since: a staff link,
  // another caller's correction. The run leaves it alone and says so.
  async function reread(messageId: string): Promise<{ detail: MailDetail; expected: MessageState } | string> {
    const expected = known.get(messageId);
    if (!expected) {
      return "That message is not in this run's eligible list.";
    }
    const detail = await pegasus.get(messageId);
    if (!isStillWritable(detail)) {
      return "Staff have decided this message since it was listed; leave it.";
    }
    if (hasChangedSince(expected, detail)) {
      log("changed since listed", { messageId, expected, current: stateOf(detail) });
      return "This e-mail was changed since it was listed; leave it.";
    }
    return { detail, expected };
  }

  const listEligible = tool(
    "list_eligible_emails",
    "Lists the e-mails this run may act on: each one is Unclassified or has no Case linked, and was decided only by the automatic pipeline. Each carries the Cases suggested for linking, when it can be linked.",
    {},
    async () => text([...batch.values()].map(view)),
    { annotations: { readOnlyHint: true, openWorldHint: false } });

  const classify = tool(
    "classify_email",
    "Gives one listed e-mail its mail category, with a one-sentence reason naming the evidence. new-instruction also needs workType.",
    {
      messageId: z.guid().describe("messageId from list_eligible_emails"),
      category: z.enum([CorrespondenceCode, ...MailCategoryCodes]),
      workType: z.enum(WorkTypes).optional().describe("Only with new-instruction"),
      reason: z.string().min(1).max(450),
    },
    async (args) => {
      try {
        if (args.category === "new-instruction" && !args.workType) {
          return text("new-instruction needs workType: Inspection, Audit or InspectionAndAudit.", true);
        }
        const read = await reread(args.messageId);
        if (typeof read === "string") {
          return text(read, true);
        }
        const { detail, expected } = read;
        const classification = detail.classification!;
        if (classification.current.outcome === "Classified"
          && classification.current.category === args.category
          && (args.category !== "new-instruction" || classification.current.caseType === args.workType)) {
          return text(`The e-mail is already ${args.category}; nothing changed.`);
        }
        log("classify", { messageId: args.messageId, from: classification.current.category, to: args.category, workType: args.workType, reason: args.reason, dryRun });
        if (dryRun) {
          return text(`Dry run: would classify as ${args.category}.`);
        }
        // The key is only an audit correlation here: Pegasus guards a
        // correction by the expected version, so a repeat is refused on that.
        const corrected = await pegasus.correct({
          messageId: args.messageId,
          expectedClassificationVersion: expected.classificationVersion,
          classificationKey: args.category,
          ...(args.workType ? { workType: args.workType } : {}),
          reason: ReasonPrefix + args.reason,
          operationKey: operationKey("cls", args.messageId, expected.classificationVersion),
        });
        known.set(args.messageId, { ...expected, classificationVersion: corrected.version });
        return text(`Classified as ${args.category}.`);
      } catch (error) {
        failed.set(args.messageId, (error as Error).message);
        log("classify failed", { messageId: args.messageId, error: (error as Error).message });
        return text(`Pegasus refused the classification: ${(error as Error).message}`, true);
      }
    });

  const link = tool(
    "link_case",
    "Links one listed e-mail to one of the Cases suggested for it, with a one-sentence reason naming the matching evidence.",
    {
      messageId: z.guid().describe("messageId from list_eligible_emails"),
      caseId: z.guid().describe("caseId of one of that e-mail's suggestedCases"),
      reason: z.string().min(1).max(450),
    },
    async (args) => {
      const destination = batch.get(args.messageId)?.destinations.find((item) => item.caseId === args.caseId);
      if (!destination) {
        return text("That Case is not one of the e-mail's suggested Cases.", true);
      }
      try {
        const read = await reread(args.messageId);
        if (typeof read === "string") {
          return text(read, true);
        }
        const { detail } = read;
        if (!detail.canLinkCase || detail.intakeVersion === null) {
          return text("The e-mail can no longer be linked to a Case.", true);
        }
        log("link", { messageId: args.messageId, caseReference: destination.reference, reason: args.reason, dryRun });
        if (dryRun) {
          return text(`Dry run: would link to ${destination.reference}.`);
        }
        // Each attempt is its own command, so a refused link can be tried
        // again. A link that landed on an earlier attempt shows in the
        // re-read above (the Case differs from the one known), not by a
        // replayed key.
        let linked: MailDetail;
        try {
          linked = await pegasus.link({
            messageId: args.messageId,
            caseId: args.caseId,
            expectedIntakeVersion: detail.intakeVersion,
            expectedCaseVersion: destination.version,
            reason: ReasonPrefix + args.reason,
            operationKey: operationKey("cls-link", args.messageId, detail.intakeVersion, newAttempt()),
          });
        } catch (error) {
          // A link whose answer was lost may have landed; the message itself
          // says whether the link happened.
          const landed = await pegasus.get(args.messageId);
          if (landed.summary.caseId?.toLowerCase() !== args.caseId.toLowerCase()) {
            throw error;
          }
          linked = landed;
        }
        const state = stateOf(linked);
        if (state) {
          known.set(args.messageId, state);
        }
        return text(`Linked to ${destination.reference}.`);
      } catch (error) {
        failed.set(args.messageId, (error as Error).message);
        log("link failed", { messageId: args.messageId, error: (error as Error).message });
        return text(`Pegasus refused the link: ${(error as Error).message}`, true);
      }
    });

  return createSdkMcpServer({
    name: ServerName,
    version: "1.0.0",
    tools: [listEligible, classify, link, tool(
      "open_query",
      "Opens a query on a listed e-mail that is linked to a Case whose report is sent, when the fresh text is a question about or challenge to that report.",
      { messageId: z.guid().describe("messageId from list_eligible_emails") },
      async (args) => {
        try {
          const read = await reread(args.messageId);
          if (typeof read === "string") {
            return text(read, true);
          }
          log("open query", { messageId: args.messageId, dryRun });
          if (dryRun) {
            return text("Dry run: would open a query.");
          }
          const opened = await pegasus.openQuery({
            messageId: args.messageId,
            operationKey: operationKey("qry", args.messageId, read.expected.classificationVersion),
          });
          return text(`Opened query ${opened.queryId}.`);
        } catch (error) {
          failed.set(args.messageId, (error as Error).message);
          log("open query failed", { messageId: args.messageId, error: (error as Error).message });
          return text(`Pegasus refused the query: ${(error as Error).message}`, true);
        }
      }),
    tool(
      "search_old_system_folders",
      "Read-only. Searches the firm's old-system case folders by name (a case reference, registration or claimant) and returns folder names only. state is unavailable when the folders could not be read.",
      { query: z.string().min(1).max(200).describe("A case reference, registration, claimant or claim number") },
      async (args) => {
        try {
          const result = await pegasus.searchOldSystemFolders(args.query, LookupLimit);
          log("search old system folders", { query: args.query, state: result.state, hits: result.items.length });
          return text({
            state: result.state,
            isTruncated: result.isTruncated,
            folders: result.items.map((item) => ({ name: item.name, modifiedAtUtc: item.modifiedAtUtc })),
          });
        } catch (error) {
          log("search old system folders failed", { query: args.query, error: (error as Error).message });
          return text(`Pegasus refused the lookup: ${(error as Error).message}`, true);
        }
      },
      { annotations: { readOnlyHint: true, openWorldHint: true } }),
    tool(
      "search_mailboxes",
      "Read-only. Searches the firm's approved mailboxes for earlier e-mail about a term and returns subjects, senders and dates only. state is unavailable when no mailbox could be read.",
      { query: z.string().min(1).max(200).describe("A case reference, registration, claimant or claim number") },
      async (args) => {
        try {
          const result = await pegasus.searchMailboxes(args.query, LookupLimit);
          log("search mailboxes", { query: args.query, state: result.state, hits: result.items.length });
          return text({
            state: result.state,
            isTruncated: result.isTruncated,
            mailboxes: result.mailboxes.map((item) => ({ address: item.mailboxAddress, state: item.state })),
            messages: result.items.map((item) => ({
              mailbox: item.mailboxAddress,
              subject: item.subject,
              from: item.senderAddress,
              receivedAtUtc: item.receivedAtUtc,
              preview: item.preview,
            })),
          });
        } catch (error) {
          log("search mailboxes failed", { query: args.query, error: (error as Error).message });
          return text(`Pegasus refused the lookup: ${(error as Error).message}`, true);
        }
      },
      { annotations: { readOnlyHint: true, openWorldHint: true } })],
    alwaysLoad: true,
  });
}

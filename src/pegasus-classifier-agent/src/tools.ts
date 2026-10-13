// The agent's only three tools, served in-process. Each write re-reads the
// message from Pegasus and re-checks it before calling Pegasus; Pegasus then
// applies its own checks (versions, leases, link rules) as it does for staff.

import { createSdkMcpServer, tool } from "@anthropic-ai/claude-agent-sdk";
import { z } from "zod";
import { isStillWritable } from "./eligibility.js";
import type { LinkDestination, MailDetail, PegasusMail } from "./pegasus.js";
import { MailCategoryCodes, WorkTypes } from "./prompt.js";

export interface Candidate {
  detail: MailDetail;
  destinations: LinkDestination[];
}

export interface Logger {
  (message: string, properties?: Record<string, unknown>): void;
}

export const ServerName = "classifier";
export const ToolNames = ["list_eligible_emails", "classify_email", "link_case"] as const;

const BodyLimit = 6000;
const ReasonPrefix = "Classifier agent: ";

function text(value: unknown, isError = false) {
  return {
    content: [{ type: "text" as const, text: typeof value === "string" ? value : JSON.stringify(value, null, 2) }],
    ...(isError ? { isError: true } : {}),
  };
}

/** Caller idempotency keys: mcp:, the message without hyphens, the version acted on. */
function operationKey(kind: string, messageId: string, version: number): string {
  return `mcp:${kind}:${messageId.replaceAll("-", "")}:${version}`;
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
  /** Messages whose write failed this run; they are looked at again next run. */
  failed: Set<string>;
  dryRun: boolean;
  log: Logger;
}) {
  const { pegasus, batch, failed, dryRun, log } = options;

  // A write is allowed only for a message this run listed, and not once staff
  // have decided it since.
  async function reread(messageId: string): Promise<MailDetail | string> {
    if (!batch.has(messageId)) {
      return "That message is not in this run's eligible list.";
    }
    const detail = await pegasus.get(messageId);
    return isStillWritable(detail) ? detail : "Staff have decided this message since it was listed; leave it.";
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
      category: z.enum(MailCategoryCodes),
      workType: z.enum(WorkTypes).optional().describe("Only with new-instruction"),
      reason: z.string().min(1).max(450),
    },
    async (args) => {
      try {
        if (args.category === "new-instruction" && !args.workType) {
          return text("new-instruction needs workType: Inspection, Audit or InspectionAndAudit.", true);
        }
        const detail = await reread(args.messageId);
        if (typeof detail === "string") {
          return text(detail, true);
        }
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
        await pegasus.correct({
          messageId: args.messageId,
          expectedClassificationVersion: classification.version,
          classificationKey: args.category,
          ...(args.workType ? { workType: args.workType } : {}),
          reason: ReasonPrefix + args.reason,
          operationKey: operationKey("cls", args.messageId, classification.version),
        });
        return text(`Classified as ${args.category}.`);
      } catch (error) {
        failed.add(args.messageId);
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
        const detail = await reread(args.messageId);
        if (typeof detail === "string") {
          return text(detail, true);
        }
        if (!detail.canLinkCase || detail.intakeVersion === null) {
          return text("The e-mail can no longer be linked to a Case.", true);
        }
        log("link", { messageId: args.messageId, caseReference: destination.reference, reason: args.reason, dryRun });
        if (dryRun) {
          return text(`Dry run: would link to ${destination.reference}.`);
        }
        try {
          await pegasus.link({
            messageId: args.messageId,
            caseId: args.caseId,
            expectedIntakeVersion: detail.intakeVersion,
            expectedCaseVersion: destination.version,
            reason: ReasonPrefix + args.reason,
            operationKey: operationKey("cls-link", args.messageId, detail.intakeVersion),
          });
        } catch (error) {
          // A retried link whose first attempt landed is refused; the message
          // itself says whether the link happened.
          const after = await pegasus.get(args.messageId);
          if (after.summary.caseId?.toLowerCase() !== args.caseId.toLowerCase()) {
            throw error;
          }
        }
        return text(`Linked to ${destination.reference}.`);
      } catch (error) {
        failed.add(args.messageId);
        log("link failed", { messageId: args.messageId, error: (error as Error).message });
        return text(`Pegasus refused the link: ${(error as Error).message}`, true);
      }
    });

  return createSdkMcpServer({
    name: ServerName,
    version: "1.0.0",
    tools: [listEligible, classify, link],
    alwaysLoad: true,
  });
}

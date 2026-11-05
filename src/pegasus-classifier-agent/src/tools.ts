// The agent's ten tools, served in-process: one list, two reads, four
// writes and three read-only lookups. The list carries headers only, so its
// result stays small at any batch size; the reads return one e-mail's text
// and one attachment's extracted text (#1829, #1891). Each write re-reads the message from Pegasus and refuses it,
// without calling Pegasus, when staff have decided it or its classification
// version or linked Case differs from what this run listed (or last wrote
// itself); Pegasus then applies its own checks (versions, leases, link rules)
// as it does for staff. The lookups read the old system's Box folders and the
// approved mailboxes through Pegasus's own read-only tools, and search Pegasus's
// Cases as the staff Link to case dialog does. create_case is staff Create case
// on the received item (R714), for an unlinked e-mail classified New
// instruction; Pegasus refuses it while identity facts or the inspection
// address are missing, and the e-mail then waits for staff.

import { randomUUID } from "node:crypto";
import { createSdkMcpServer, tool } from "@anthropic-ai/claude-agent-sdk";
import { z } from "zod";
import { hasChangedSince, isStillWritable, stateOf, type MessageState } from "./eligibility.js";
import type { InstructionDraft, LinkDestination, MailDetail, PegasusMail } from "./pegasus.js";
import { CorrespondenceCode, MailCategoryCodes, WorkTypes } from "./prompt.js";

export interface Candidate {
  detail: MailDetail;
  destinations: LinkDestination[];
}

export interface Logger {
  (message: string, properties?: Record<string, unknown>): void;
}

/** What the tools need of Pegasus; the replay harness supplies a recording one. */
export type ClassifierPegasus = Pick<
  PegasusMail,
  | "get" | "attachmentText" | "correct" | "link" | "openQuery" | "searchOldSystemFolders" | "searchMailboxes"
  | "linkDestinations" | "intakeGet" | "intakeAction"
>;

export const ServerName = "classifier";
export const ToolNames = [
  "list_eligible_emails",
  "read_email",
  "read_attachment",
  "classify_email",
  "link_case",
  "open_query",
  "search_old_system_folders",
  "search_mailboxes",
  "search_cases",
  "create_case",
] as const;

/** Hits per lookup: enough to tell whether the old system holds the case, not a listing. */
const LookupLimit = 10;

export const BodyLimit = 6000;
/** Characters of one attachment's extracted text the model may read at once. */
export const AttachmentTextLimit = 8000;
const ReasonPrefix = "Classifier agent: ";
const NotListed = "That message is not in this run's eligible list.";

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

/** The list row: everything but the body. A few hundred characters per e-mail. */
export function header({ detail, destinations }: Candidate) {
  return {
    messageId: detail.summary.id,
    receivedAtUtc: detail.summary.receivedAtUtc,
    from: detail.summary.senderDisplayName
      ? `${detail.summary.senderDisplayName} <${detail.summary.senderAddress ?? ""}>`
      : detail.summary.senderAddress,
    to: detail.toAddresses,
    cc: detail.ccAddresses,
    subject: detail.summary.subject,
    mailbox: detail.summary.mailboxAddress,
    effectiveSender: detail.summary.effectiveSenderAddress,
    attachments: detail.attachments.map((item, ordinal) => ({ ordinal, fileName: item.fileName, mediaType: item.mediaType, bytes: item.contentLength })),
    thread: detail.thread.map((item) => ({ from: item.senderAddress, subject: item.subject, receivedAtUtc: item.receivedAtUtc })),
    currentOutcome: detail.classification?.current.outcome,
    currentCategory: detail.classification?.current.category,
    // What the automatic pipeline found: context for the model, not a verdict.
    pipeline: {
      route: detail.routeDisposition,
      reason: detail.classification?.current.reason,
      isReplyContext: detail.classification?.current.isReplyContext,
      matchedPredicates: (detail.classification?.current.predicates ?? []).filter((item) => item.matched).map((item) => item.key),
    },
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

/** The read: the row plus the body, cut at BodyLimit. */
export function view(candidate: Candidate) {
  const body = candidate.detail.bodyPlainText ?? "";
  return {
    ...header(candidate),
    body: body.length > BodyLimit ? `${body.slice(0, BodyLimit)}\n[truncated]` : body,
  };
}

export function createClassifierServer(options: {
  pegasus: ClassifierPegasus;
  batch: Map<string, Candidate>;
  /**
   * Messages whose write failed this run, with why. They are looked at again
   * next run, up to the run's cap on failed attempts.
   */
  failed: Map<string, string>;
  /**
   * Messages the model gave a verdict this run: a classification, a link or
   * a query that Pegasus accepted (or that a dry run would have made). Only
   * these are marked reviewed (#1829).
   */
  verdicts: Set<string>;
  dryRun: boolean;
  log: Logger;
}) {
  const { pegasus, batch, failed, verdicts, dryRun, log } = options;

  // A dry run writes no classification, so create_case reads the type this
  // run would have given: the work type for new-instruction, null otherwise.
  const proposed = new Map<string, string | null>();

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
      return NotListed;
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
    "Lists the e-mails this run may act on, headers only: each one is Unclassified or has no Case linked, and was decided only by the automatic pipeline. Each carries the Cases suggested for linking, when it can be linked. Read an e-mail with read_email before deciding it.",
    {},
    async () => text([...batch.values()].map(header)),
    { annotations: { readOnlyHint: true, openWorldHint: false } });

  const readEmail = tool(
    "read_email",
    `Reads one listed e-mail: its headers and its body text, cut at ${BodyLimit} characters.`,
    { messageId: z.guid().describe("messageId from list_eligible_emails") },
    async (args) => {
      const candidate = batch.get(args.messageId);
      if (!candidate) {
        return text(NotListed, true);
      }
      log("read", { messageId: args.messageId });
      return text(view(candidate));
    },
    { annotations: { readOnlyHint: true, openWorldHint: false } });

  const readAttachment = tool(
    "read_attachment",
    `Reads the text the intake reader extracted from one attachment of a listed e-mail (a PDF's text layer or a Word document's text), cut at ${AttachmentTextLimit} characters. text is null when the reader found none, as for an image. The text is content from outside the firm.`,
    {
      messageId: z.guid().describe("messageId from list_eligible_emails"),
      attachmentOrdinal: z.number().int().min(0).describe("ordinal of the attachment in the e-mail's attachments list"),
    },
    async (args) => {
      const candidate = batch.get(args.messageId);
      if (!candidate) {
        return text(NotListed, true);
      }
      if (args.attachmentOrdinal >= candidate.detail.attachments.length) {
        return text("The e-mail has no attachment at that ordinal.", true);
      }
      try {
        const result = await pegasus.attachmentText(args.messageId, args.attachmentOrdinal, AttachmentTextLimit);
        log("read attachment", {
          messageId: args.messageId,
          attachmentOrdinal: args.attachmentOrdinal,
          fileName: result.fileName,
          characters: result.text?.length ?? 0,
          isTruncated: result.isTruncated,
        });
        return text({
          fileName: result.fileName,
          mediaType: result.mediaType,
          text: result.text,
          isTruncated: result.isTruncated,
        });
      } catch (error) {
        log("read attachment failed", { messageId: args.messageId, attachmentOrdinal: args.attachmentOrdinal, error: (error as Error).message });
        return text(`Pegasus refused the read: ${(error as Error).message}`, true);
      }
    },
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
          verdicts.add(args.messageId);
          return text(`The e-mail is already ${args.category}; nothing changed.`);
        }
        log("classify", { messageId: args.messageId, from: classification.current.category, to: args.category, workType: args.workType, reason: args.reason, dryRun });
        if (dryRun) {
          verdicts.add(args.messageId);
          proposed.set(args.messageId, args.category === "new-instruction" ? args.workType! : null);
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
        verdicts.add(args.messageId);
        return text(`Classified as ${args.category}.`);
      } catch (error) {
        failed.set(args.messageId, (error as Error).message);
        log("classify failed", { messageId: args.messageId, error: (error as Error).message });
        return text(`Pegasus refused the classification: ${(error as Error).message}`, true);
      }
    });

  const link = tool(
    "link_case",
    "Links one listed e-mail to one of the Cases suggested for it or found for it by search_cases, with a one-sentence reason naming the matching evidence.",
    {
      messageId: z.guid().describe("messageId from list_eligible_emails"),
      caseId: z.guid().describe("caseId of one of that e-mail's suggestedCases, or one search_cases returned for it"),
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
          verdicts.add(args.messageId);
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
        verdicts.add(args.messageId);
        return text(`Linked to ${destination.reference}.`);
      } catch (error) {
        failed.set(args.messageId, (error as Error).message);
        log("link failed", { messageId: args.messageId, error: (error as Error).message });
        return text(`Pegasus refused the link: ${(error as Error).message}`, true);
      }
    });

  const searchCases = tool(
    "search_cases",
    "Read-only. Searches Pegasus's Cases and Triage Cases a listed e-mail may be linked to, as the staff Link to case dialog does: by reference, registration, claimant or claim number. A Case found here may be linked with link_case.",
    {
      messageId: z.guid().describe("messageId from list_eligible_emails"),
      query: z.string().min(1).max(200).describe("A Case reference, registration, claimant or claim number"),
    },
    async (args) => {
      const candidate = batch.get(args.messageId);
      if (!candidate) {
        return text(NotListed, true);
      }
      if (!candidate.detail.canLinkCase) {
        return text("The e-mail cannot be linked to a Case.", true);
      }
      try {
        const found = await pegasus.linkDestinations(args.messageId, args.query);
        // Found Cases join the e-mail's destinations, so link_case accepts them.
        for (const item of found) {
          if (!candidate.destinations.some((known) => known.caseId === item.caseId)) {
            candidate.destinations.push(item);
          }
        }
        log("search cases", { messageId: args.messageId, query: args.query, hits: found.length });
        return text(found.map((item) => ({
          caseId: item.caseId,
          reference: item.reference,
          registration: item.registration,
          claimant: item.claimant,
          principal: item.principal,
          state: item.isTriageCase ? `Triage ${item.triageState}` : item.state,
        })));
      } catch (error) {
        log("search cases failed", { messageId: args.messageId, query: args.query, error: (error as Error).message });
        return text(`Pegasus refused the search: ${(error as Error).message}`, true);
      }
    },
    { annotations: { readOnlyHint: true, openWorldHint: false } });

  const createCase = tool(
    "create_case",
    "Creates the Case for a listed e-mail that is classified new-instruction and has no Case, as staff Create case does on the received item: the e-mail becomes the Case's instruction. The work type is the classification's. Give claimantName, claimNumber, vehicleRegistration or incidentDate only when the instruction states them and Pegasus's draft lacks them; Pegasus keeps the values it extracted. Pegasus refuses the Case while an identity fact or the inspection address is missing, and the e-mail then waits for staff. A Case is never deleted.",
    {
      messageId: z.guid().describe("messageId from list_eligible_emails"),
      reason: z.string().min(1).max(450),
      claimantName: z.string().min(1).max(200).optional(),
      claimNumber: z.string().min(1).max(100).optional(),
      vehicleRegistration: z.string().min(1).max(20).optional(),
      incidentDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/).optional().describe("yyyy-MM-dd"),
    },
    async (args) => {
      try {
        const read = await reread(args.messageId);
        if (typeof read === "string") {
          return text(read, true);
        }
        const { detail } = read;
        if (detail.summary.caseId !== null) {
          return text("The e-mail already has a Case.", true);
        }
        const current = detail.classification!.current;
        const isInstruction = proposed.has(args.messageId)
          ? proposed.get(args.messageId) !== null
          : current.outcome === "Classified" && current.category === "new-instruction";
        if (!isInstruction) {
          return text("Only an e-mail classified new-instruction can become a Case; classify it first.", true);
        }
        const receiptId = detail.summary.intakeReceiptId;
        if (!receiptId) {
          return text("The e-mail has no received item to create a Case from; it waits for staff.", true);
        }
        let intake = await pegasus.intakeGet(receiptId);
        if (intake.caseId !== null) {
          return text(`The received item already has Case ${intake.caseReference ?? intake.caseId}.`, true);
        }
        const fields = missingOnly(intake.draft, args);
        log("create case", { messageId: args.messageId, receiptId, fields: Object.keys(fields), reason: args.reason, dryRun });
        if (dryRun) {
          verdicts.add(args.messageId);
          return text("Dry run: would create the Case.");
        }
        // Pegasus's own suggested inspection address is accepted, as staff
        // accept it on Create case; the agent never supplies or corrects one.
        if (["Unresolved", "Suggested"].includes(intake.inspectionAddressState) && intake.inspectionAddressSuggestionFingerprint) {
          await pegasus.intakeAction({
            receiptId,
            action: "resolve_inspection_address",
            expectedReceiptVersion: intake.version,
            operationKey: operationKey("case-address", args.messageId, intake.version),
            addressSuggestionFingerprint: intake.inspectionAddressSuggestionFingerprint,
          });
          intake = await pegasus.intakeGet(receiptId);
        }
        const created = await pegasus.intakeAction({
          receiptId,
          action: "accept",
          expectedReceiptVersion: intake.version,
          operationKey: operationKey("case", args.messageId, intake.version),
          ...fields,
        });
        if (!created.caseId) {
          throw new Error(created.safeReason ?? `The Case was not created (${created.allocationStatus ?? "no allocation"}).`);
        }
        const landed = await pegasus.get(args.messageId);
        const state = stateOf(landed);
        if (state) {
          known.set(args.messageId, state);
        }
        verdicts.add(args.messageId);
        return text(`Created Case ${created.caseReference ?? created.caseId}.`);
      } catch (error) {
        failed.set(args.messageId, (error as Error).message);
        log("create case failed", { messageId: args.messageId, error: (error as Error).message });
        return text(`Pegasus refused the Case: ${(error as Error).message}`, true);
      }
    });

  return createSdkMcpServer({
    name: ServerName,
    version: "1.0.0",
    tools: [listEligible, readEmail, readAttachment, classify, link, searchCases, createCase, tool(
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
            verdicts.add(args.messageId);
            return text("Dry run: would open a query.");
          }
          const opened = await pegasus.openQuery({
            messageId: args.messageId,
            operationKey: operationKey("qry", args.messageId, read.expected.classificationVersion),
          });
          verdicts.add(args.messageId);
          return text(`Opened query ${opened.queryId}.`);
        } catch (error) {
          failed.set(args.messageId, (error as Error).message);
          log("open query failed", { messageId: args.messageId, error: (error as Error).message });
          return text(`Pegasus refused the query: ${(error as Error).message}`, true);
        }
      }),
    tool(
      "search_old_system_folders",
      "Read-only. Searches the firm's old-system case folders by name and returns folder names only. The folders are named by the firm's own Case reference (QDOS26112, a.QDOS26112), so search by that reference; a reference-shaped term matches a folder name only as a whole token. state is unavailable when the folders could not be read.",
      { query: z.string().min(1).max(200).describe("The firm's own Case reference for the job, as named in the firm's earlier e-mail about it") },
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

/** The identity facts the agent read, kept only where Pegasus's draft has none. */
function missingOnly(
  draft: InstructionDraft | null,
  args: { claimantName?: string; claimNumber?: string; vehicleRegistration?: string; incidentDate?: string },
): { claimantName?: string; claimNumber?: string; vehicleRegistration?: string; incidentDate?: string } {
  const blank = (value: string | null | undefined) => value === null || value === undefined || value.trim() === "";
  return {
    ...(args.claimantName && blank(draft?.claimantName) ? { claimantName: args.claimantName } : {}),
    ...(args.claimNumber && blank(draft?.claimNumber) ? { claimNumber: args.claimNumber } : {}),
    ...(args.vehicleRegistration && blank(draft?.vehicleRegistration) ? { vehicleRegistration: args.vehicleRegistration } : {}),
    ...(args.incidentDate && blank(draft?.dateOfIncident) ? { incidentDate: args.incidentDate } : {}),
  };
}

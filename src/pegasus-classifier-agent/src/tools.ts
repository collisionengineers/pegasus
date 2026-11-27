// The agent's thirteen tools, served in-process: one list, three reads, five
// writes, one leave and three read-only lookups. The list carries headers
// only, so its result stays small at any batch size; the reads return one
// e-mail's text, one attachment's extracted text, and the plate Pegasus's own
// reader agreed on across the e-mail's photographs (#1829, #1891, #1890). Each
// write re-reads the message from Pegasus and refuses it, without calling
// Pegasus, when staff have decided it or its classification version or linked
// Case differs from what this run listed (or last wrote itself); Pegasus then
// applies its own checks (versions, leases, link rules) as it does for staff.
// A write whose answer is lost, such as a timed-out call, is settled by
// reading the message again: its own effect there is a done write, not a
// refusal (#1963). The lookups read the old system's Box folders and the
// approved mailboxes through Pegasus's own read-only tools, and search
// Pegasus's Cases as the staff Link to case dialog does. create_case is staff
// Create case on the received item (R714), for an unlinked e-mail classified
// New instruction; open_triage is staff Open the Triage on an unlinked Triage
// request whose registration the agent read in its text or Pegasus read in
// its photographs. Pegasus refuses either while what it needs is missing, and
// the e-mail then waits for staff. The Principal is Pegasus's answer, never
// the model's: the one the item established, else the one active Principal
// Contact its sender belongs to (R966, R969). A missing inspection address
// does not stop the Case (R965). A website enquiry becomes a private
// customer's one-off Case, made directly and then linked (R972).

import { randomUUID } from "node:crypto";
import { createSdkMcpServer, tool } from "@anthropic-ai/claude-agent-sdk";
import { z } from "zod";
import { decider, hasChangedSince, isStillWritable, stateOf, type MessageState } from "./eligibility.js";
import type {
  InstructionDraft,
  LinkDestination,
  MailDetail,
  MailPhotoRegistrations,
  PegasusMail,
  RunAction,
  RunItem,
} from "./pegasus.js";
import { CorrespondenceCode, MailCategoryCodes, WorkTypes } from "./prompt.js";

export interface Candidate {
  detail: MailDetail;
  // The matcher's suggested Cases, shown to the model as suggestedCases.
  destinations: LinkDestination[];
  // Cases search_cases found for the e-mail: link_case accepts them too, but
  // they are not matcher suggestions and are never shown as such.
  found?: LinkDestination[];
  // What read_photos returned, which open_triage checks a photo read against.
  photos?: MailPhotoRegistrations;
}

export interface Logger {
  (message: string, properties?: Record<string, unknown>): void;
}

/** What the tools need of Pegasus; the replay harness and the tests supply their own. */
export type ClassifierPegasus = Pick<
  PegasusMail,
  | "get" | "attachmentText" | "correct" | "link" | "openQuery" | "searchOldSystemFolders" | "searchMailboxes"
  | "linkDestinations" | "intakeGet" | "intakeAction" | "photoRegistrations" | "caseCreate"
>;

/**
 * What one run came to per message. The run owns it, marks reviewed from it
 * once the model has finished (run.ts recordOutcomes) and records the run
 * from it (runs.ts buildItems).
 */
export interface RunOutcomes {
  /**
   * Messages the model gave a verdict this run: a classification, a link, a
   * query, a Case, a Triage or a deliberate leave that Pegasus accepted (or
   * that a dry run would have made). Only these are marked reviewed (#1829).
   */
  verdicts: Set<string>;
  /**
   * Messages whose write failed this run, with why, and no verdict since.
   * They are looked at again next run, up to the run's cap on failed attempts.
   */
  failed: Map<string, string>;
  /**
   * Unclassified messages left while the lookups were unavailable (R584 does
   * not apply to mail the agent could not review): looked at again next run
   * with no attempt counted, so an outage never closes mail.
   */
  deferred: Set<string>;
  /** Messages this run wrote to, so reviewed is stored at what they read now. */
  written: Set<string>;
  /** What the run did with each message it reached a verdict on or deferred, for the run's record. */
  acts: Map<string, RunAct>;
}

export function newOutcomes(): RunOutcomes {
  return { verdicts: new Set(), failed: new Map(), deferred: new Set(), written: new Set(), acts: new Map() };
}

/** One message's act in the run's record; a dry run's acts are what it would have done. */
export type RunAct = Omit<RunItem, "messageId">;

/** The acts that wrote to Pegasus, or that a dry run would have written. */
export const WriteActions: ReadonlySet<RunAction> = new Set<RunAction>([
  "classified", "linked", "case_created", "one_off_case", "triage_opened", "query_opened",
]);

/**
 * A message's second act in a run replaces the first, keeping the first's
 * category when it names none. A refused Case or Triage after a write keeps
 * the write, which did happen, and adds only the refusal.
 */
export function mergeAct(earlier: RunAct | undefined, next: RunAct): RunAct {
  if (!earlier) {
    return next;
  }
  if (next.action === "left_for_staff" && WriteActions.has(earlier.action)) {
    return { ...earlier, ...(next.refusal !== undefined ? { refusal: next.refusal } : {}) };
  }
  return { ...next, ...(next.category === undefined && earlier.category !== undefined ? { category: earlier.category } : {}) };
}

export const ServerName = "classifier";
export const ToolNames = [
  "list_eligible_emails",
  "read_email",
  "read_attachment",
  "read_photos",
  "classify_email",
  "link_case",
  "open_query",
  "create_case",
  "open_triage",
  "leave_email",
  "search_old_system_folders",
  "search_mailboxes",
  "search_cases",
] as const;

/** Hits per lookup: enough to tell whether the old system holds the case, not a listing. */
const LookupLimit = 10;

export const BodyLimit = 6000;
/** Characters of one attachment's extracted text the model may read at once. */
export const AttachmentTextLimit = 8000;
const ReasonPrefix = "Classifier agent: ";
/** Pegasus takes a classification correction's reason of 1 to 500 characters. */
const CorrectionReasonLimit = 500;
const NotListed = "That message is not in this run's eligible list.";

function text(value: unknown, isError = false) {
  return {
    content: [{ type: "text" as const, text: typeof value === "string" ? value : JSON.stringify(value, null, 2) }],
    ...(isError ? { isError: true } : {}),
  };
}

export function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
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

/**
 * The e-mail already carries this type. Correspondence is no category, so it
 * reads as a message with none that the pipeline no longer decides.
 */
export function hasType(detail: MailDetail, category: string, workType?: string): boolean {
  const classification = detail.classification;
  if (!classification) {
    return false;
  }
  if (category === CorrespondenceCode) {
    return classification.current.category == null && decider(classification.currentActor) !== "system-worker";
  }
  return classification.current.outcome === "Classified"
    && classification.current.category === category
    && (category !== "new-instruction" || classification.current.caseType === workType);
}

/** A registration as Pegasus compares one: letters and digits, upper case, no spaces. */
export function normalizeRegistration(value: string): string {
  return value.replace(/\s+/g, "").toUpperCase();
}

const RegistrationShape = /^[A-Z0-9]{2,8}$/;

/**
 * The registration appears in the text as a whole token, spaced or not
 * (AB12CDE or AB12 CDE), never inside a longer token.
 */
export function appearsIn(registration: string, source: string | null | undefined): boolean {
  if (!source || !RegistrationShape.test(registration)) {
    return false;
  }
  const spaced = [...registration].join("\\s?");
  return new RegExp(`(?<![A-Z0-9])${spaced}(?![A-Z0-9])`, "i").test(source);
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

export interface ClassifierToolOptions {
  pegasus: ClassifierPegasus;
  batch: Map<string, Candidate>;
  outcomes: RunOutcomes;
  dryRun: boolean;
  log: Logger;
}

/** The tools, without the server around them, so the tests can call each handler. */
export function createClassifierTools(options: ClassifierToolOptions) {
  const { pegasus, batch, outcomes, dryRun, log } = options;

  // A dry run writes no classification, so create_case and open_triage read
  // the type this run would have given.
  const proposed = new Map<string, { category: string; workType?: string }>();

  // The e-mails whose own mailbox search, and whose own folder search, found
  // its source unavailable this run. An Unclassified e-mail may be left without
  // a type only when both lookups were unavailable for it (R738, R885).
  const mailboxesUnavailableFor = new Set<string>();
  const foldersUnavailableFor = new Set<string>();

  // What this run last knew of each listed message: its classification version
  // and linked Case id as listed, then as this run's own writes left them.
  const known = new Map<string, MessageState>();
  for (const [messageId, { detail }] of batch) {
    const state = stateOf(detail);
    if (state) {
      known.set(messageId, state);
    }
  }

  function verdict(messageId: string): void {
    outcomes.verdicts.add(messageId);
    outcomes.failed.delete(messageId);
    outcomes.deferred.delete(messageId);
  }

  function act(messageId: string, entry: RunAct): void {
    outcomes.acts.set(messageId, mergeAct(outcomes.acts.get(messageId), { ...entry, at: new Date().toISOString() }));
  }

  function failed(messageId: string, reason: string): void {
    if (!outcomes.verdicts.has(messageId)) {
      outcomes.failed.set(messageId, reason);
    }
  }

  function wrote(messageId: string, detail?: MailDetail): void {
    outcomes.written.add(messageId);
    const state = detail ? stateOf(detail) : undefined;
    if (state) {
      known.set(messageId, state);
    }
    verdict(messageId);
  }

  // A write is allowed only for a message this run listed, not once staff have
  // decided it, and only while its classification version and linked Case are
  // still the ones this run knows. Anything else changed it since: a staff link,
  // another caller's correction. The run leaves it alone and says so. A read
  // that fails is not a refusal: the message is tried again next run.
  async function reread(messageId: string): Promise<{ detail: MailDetail; expected: MessageState } | string> {
    const expected = known.get(messageId);
    if (!expected) {
      return NotListed;
    }
    let detail: MailDetail;
    try {
      detail = await pegasus.get(messageId);
    } catch (error) {
      failed(messageId, messageOf(error));
      log("reread failed", { messageId, error: messageOf(error) });
      return `Pegasus could not be read (${messageOf(error)}); the e-mail is looked at again next run.`;
    }
    if (!isStillWritable(detail)) {
      return "Staff have decided this message since it was listed; leave it.";
    }
    if (hasChangedSince(expected, detail)) {
      log("changed since listed", { messageId, expected, current: stateOf(detail) });
      return "This e-mail was changed since it was listed; leave it.";
    }
    return { detail, expected };
  }

  /**
   * After a write that threw. The call's answer may be lost while the write
   * landed (a timed-out call, #1963), so the message is read again: its own
   * effect there is a done write; any other change is not knowable; no change
   * is a refusal.
   */
  async function settle(
    messageId: string,
    expected: MessageState,
    landed: (detail: MailDetail) => boolean,
  ): Promise<{ kind: "landed"; detail: MailDetail } | { kind: "unknown" } | { kind: "refused" }> {
    let detail: MailDetail;
    try {
      detail = await pegasus.get(messageId);
    } catch {
      return { kind: "unknown" };
    }
    if (landed(detail)) {
      return { kind: "landed", detail };
    }
    return hasChangedSince(expected, detail) ? { kind: "unknown" } : { kind: "refused" };
  }

  function unknown(messageId: string, what: string, error: unknown) {
    failed(messageId, `outcome unknown: ${messageOf(error)}`);
    log(`${what} outcome unknown`, { messageId, error: messageOf(error) });
    return text(`Pegasus did not answer (${messageOf(error)}) and the e-mail has changed in another way, so it is not known whether the ${what} was applied. Leave the e-mail; it is looked at again next run.`, true);
  }

  /** The type the e-mail has now, or that this dry run would have given it. */
  function typeOf(messageId: string, detail: MailDetail): { category: string | null; workType?: string } {
    const given = proposed.get(messageId);
    if (given) {
      return given;
    }
    const current = detail.classification?.current;
    return current?.outcome === "Classified"
      ? { category: current.category, ...(current.caseType ? { workType: current.caseType } : {}) }
      : { category: null };
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
    `Reads the text the intake reader extracted from one attachment of a listed e-mail (a PDF's text layer or a Word document's text), cut at ${AttachmentTextLimit} characters. text is null when the reader found none, as for an image; read_photos gives the plate Pegasus read in the e-mail's photographs. The text is content from outside the firm.`,
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
        log("read attachment failed", { messageId: args.messageId, attachmentOrdinal: args.attachmentOrdinal, error: messageOf(error) });
        return text(`Pegasus refused the read: ${messageOf(error)}`, true);
      }
    },
    { annotations: { readOnlyHint: true, openWorldHint: false } });

  const readPhotos = tool(
    "read_photos",
    "Reads the registration Pegasus's own plate reader agreed on across a listed e-mail's photographs, as it recorded them when the e-mail arrived. state read: one registration; none: no plate was read; disagree: the photographs show more than one. You never read a plate from a photograph yourself.",
    { messageId: z.guid().describe("messageId from list_eligible_emails") },
    async (args) => {
      const candidate = batch.get(args.messageId);
      if (!candidate) {
        return text(NotListed, true);
      }
      try {
        const result = await pegasus.photoRegistrations(args.messageId);
        candidate.photos = result;
        log("read photos", { messageId: args.messageId, state: result.state, registration: result.registration });
        return text({ state: result.state, registration: result.registration, readRegistrations: result.readRegistrations });
      } catch (error) {
        log("read photos failed", { messageId: args.messageId, error: messageOf(error) });
        return text(`Pegasus refused the read: ${messageOf(error)}`, true);
      }
    },
    { annotations: { readOnlyHint: true, openWorldHint: false } });

  const classify = tool(
    "classify_email",
    "Gives one listed e-mail its mail category, with a one-sentence reason naming the evidence. new-instruction also needs workType. cancellation is refused until the e-mail is linked to the Case it cancels.",
    {
      messageId: z.guid().describe("messageId from list_eligible_emails"),
      category: z.enum([CorrespondenceCode, ...MailCategoryCodes]),
      workType: z.enum(WorkTypes).optional().describe("Only with new-instruction"),
      reason: z.string().min(1).max(450),
    },
    async (args) => {
      if (args.category === "new-instruction" && !args.workType) {
        return text("new-instruction needs workType: Inspection, Audit or InspectionAndAudit.", true);
      }
      const read = await reread(args.messageId);
      if (typeof read === "string") {
        return text(read, true);
      }
      const { detail, expected } = read;
      // A cancellation belongs with the Case it cancels (R1333).
      if (args.category === "cancellation" && detail.summary.caseId == null) {
        return text("A cancellation is given only with the Case it cancels linked: link that Case first. A cancellation of a job Pegasus holds no Case for, with other mail about the job in the firm's mailboxes, is pre-pegasus-case; otherwise it is correspondence.", true);
      }
      if (hasType(detail, args.category, args.workType)) {
        verdict(args.messageId);
        act(args.messageId, { action: "left", category: args.category, reason: args.reason });
        return text(`The e-mail is already ${args.category}; nothing changed.`);
      }
      log("classify", { messageId: args.messageId, from: detail.classification!.current.category, to: args.category, workType: args.workType, reason: args.reason, dryRun });
      const classified: RunAct = { action: "classified", category: args.category, reason: args.reason };
      if (dryRun) {
        verdict(args.messageId);
        act(args.messageId, classified);
        proposed.set(args.messageId, { category: args.category, ...(args.workType ? { workType: args.workType } : {}) });
        return text(`Dry run: would classify as ${args.category}.`);
      }
      try {
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
        wrote(args.messageId);
        act(args.messageId, classified);
        return text(`Classified as ${args.category}.`);
      } catch (error) {
        const settled = await settle(args.messageId, expected, (current) =>
          (current.classification?.version ?? 0) > expected.classificationVersion
          && hasType(current, args.category, args.workType));
        if (settled.kind === "landed") {
          log("classify landed after an error", { messageId: args.messageId, error: messageOf(error) });
          wrote(args.messageId, settled.detail);
          act(args.messageId, classified);
          return text(`Classified as ${args.category}.`);
        }
        if (settled.kind === "unknown") {
          return unknown(args.messageId, "classification", error);
        }
        failed(args.messageId, messageOf(error));
        log("classify failed", { messageId: args.messageId, error: messageOf(error) });
        return text(`Pegasus refused the classification: ${messageOf(error)}`, true);
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
      const candidate = batch.get(args.messageId);
      const destination = candidate?.destinations.find((item) => item.caseId === args.caseId)
        ?? candidate?.found?.find((item) => item.caseId === args.caseId);
      if (!destination) {
        return text("That Case is not one of the e-mail's suggested Cases.", true);
      }
      const read = await reread(args.messageId);
      if (typeof read === "string") {
        return text(read, true);
      }
      const { detail, expected } = read;
      if (!detail.canLinkCase || detail.intakeVersion == null) {
        return text("The e-mail can no longer be linked to a Case.", true);
      }
      log("link", { messageId: args.messageId, caseReference: destination.reference, reason: args.reason, dryRun });
      const linkedAct: RunAct = { action: "linked", caseId: args.caseId, caseReference: destination.reference, reason: args.reason };
      if (dryRun) {
        verdict(args.messageId);
        act(args.messageId, linkedAct);
        return text(`Dry run: would link to ${destination.reference}.`);
      }
      try {
        // Each attempt is its own command, so a refused link can be tried
        // again. A link that landed on an earlier attempt shows in the
        // re-read above (the Case differs from the one known), not by a
        // replayed key.
        const linked = await pegasus.link({
          messageId: args.messageId,
          caseId: args.caseId,
          expectedIntakeVersion: detail.intakeVersion,
          expectedCaseVersion: destination.version,
          reason: ReasonPrefix + args.reason,
          operationKey: operationKey("cls-link", args.messageId, detail.intakeVersion, newAttempt()),
        });
        wrote(args.messageId, linked);
        act(args.messageId, linkedAct);
        return text(`Linked to ${destination.reference}.`);
      } catch (error) {
        const settled = await settle(args.messageId, expected, (current) =>
          current.summary.caseId?.toLowerCase() === args.caseId.toLowerCase());
        if (settled.kind === "landed") {
          log("link landed after an error", { messageId: args.messageId, error: messageOf(error) });
          wrote(args.messageId, settled.detail);
          act(args.messageId, linkedAct);
          return text(`Linked to ${destination.reference}.`);
        }
        if (settled.kind === "unknown") {
          return unknown(args.messageId, "link", error);
        }
        failed(args.messageId, messageOf(error));
        log("link failed", { messageId: args.messageId, error: messageOf(error) });
        return text(`Pegasus refused the link: ${messageOf(error)}`, true);
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
        // Found Cases are kept apart from the matcher's suggestions, so
        // link_case accepts them without the model reading them as suggested.
        candidate.found ??= [];
        for (const item of found) {
          if (!candidate.found.some((known) => known.caseId === item.caseId)) {
            candidate.found.push(item);
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
        log("search cases failed", { messageId: args.messageId, query: args.query, error: messageOf(error) });
        return text(`Pegasus refused the search: ${messageOf(error)}`, true);
      }
    },
    { annotations: { readOnlyHint: true, openWorldHint: false } });

  /**
   * No Case, no type (operator, 27 November 2026, R1332): every type but
   * pre-pegasus-case belongs with a Case, so a new instruction the agent cannot
   * make a Case for ends with none. Its type becomes correspondence, which
   * unlinked is No Case Found, with the agent's reason and Pegasus's refusal for
   * staff. The agent is then its decider, so it is not listed again.
   */
  async function leaveWithoutType(messageId: string, reason: string, refusal: string): Promise<void> {
    const note = `${reason} ${refusal}`.slice(0, CorrectionReasonLimit - ReasonPrefix.length);
    const untyped: RunAct = { action: "classified", category: CorrespondenceCode, reason, refusal };
    log("left without a type", { messageId, refusal, dryRun });
    if (dryRun) {
      verdict(messageId);
      proposed.set(messageId, { category: CorrespondenceCode });
      act(messageId, untyped);
      return;
    }
    const state = known.get(messageId);
    if (!state) {
      return;
    }
    try {
      const corrected = await pegasus.correct({
        messageId,
        expectedClassificationVersion: state.classificationVersion,
        classificationKey: CorrespondenceCode,
        reason: ReasonPrefix + note,
        operationKey: operationKey("cls", messageId, state.classificationVersion),
      });
      known.set(messageId, { ...state, classificationVersion: corrected.version });
      wrote(messageId);
      act(messageId, untyped);
    } catch (error) {
      failed(messageId, messageOf(error));
      log("left without a type failed", { messageId, error: messageOf(error) });
    }
  }

  const createCase = tool(
    "create_case",
    "Creates the Case for a listed e-mail that is classified new-instruction and has no Case, as staff Create case does on the received item: the e-mail becomes the Case's instruction. The work type is the classification's and the Principal is the one Pegasus knows for the e-mail; you never name one. Give claimantName, claimNumber, vehicleRegistration or incidentDate only when the instruction states them and Pegasus's draft lacks them; Pegasus keeps the values it extracted. Pegasus refuses the Case while the vehicle registration is missing or it knows no Principal for the sender; the e-mail then loses its type and waits in No Case Found for staff, with your reason and the refusal. A missing claimant name, claim number or inspection address does not stop the Case: it starts Not ready and staff record them. A website enquiry becomes a one-off Case for a private customer, linked to the e-mail: it needs claimantName and vehicleRegistration exactly as the enquiry writes them. A Case is never deleted.",
    {
      messageId: z.guid().describe("messageId from list_eligible_emails"),
      reason: z.string().min(1).max(450),
      claimantName: z.string().min(1).max(200).optional(),
      claimNumber: z.string().min(1).max(100).optional(),
      vehicleRegistration: z.string().min(1).max(20).optional(),
      incidentDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/).optional().describe("yyyy-MM-dd"),
    },
    async (args) => {
      const read = await reread(args.messageId);
      if (typeof read === "string") {
        return text(read, true);
      }
      const { detail, expected } = read;
      // No Case, no type (R1332): a refused Case leaves the e-mail without one.
      const leftForStaff = (refusal: string) => leaveWithoutType(args.messageId, args.reason, refusal);
      if (detail.summary.caseId != null) {
        return text("The e-mail already has a Case.", true);
      }
      if (typeOf(args.messageId, detail).category !== "new-instruction") {
        return text("Only an e-mail classified new-instruction can become a Case; classify it first.", true);
      }
      const receiptId = detail.summary.intakeReceiptId;
      if (!receiptId) {
        return text("The e-mail has no received item to create a Case from; it waits for staff.", true);
      }
      try {
        let intake = await pegasus.intakeGet(receiptId);
        if (intake.caseId != null) {
          return text(`The received item already has Case ${intake.caseReference ?? intake.caseId}.`, true);
        }
        if (intake.websiteEnquiry) {
          return await createOneOff(args, detail, expected, intake.version, leftForStaff);
        }
        // The Principal is the one the item established; failing that, the
        // one active Principal Contact its sender belongs to, which Pegasus
        // worked out from the directory (R969). With neither, staff name it.
        const senderPrincipal = intake.establishedPrincipalCode == null
          ? intake.senderDirectoryPrincipalCode ?? undefined
          : undefined;
        if (intake.establishedPrincipalCode == null && senderPrincipal === undefined) {
          const refusal = "Pegasus knows no Principal for this e-mail: its sender matched no route and belongs to no one active Principal Contact. The e-mail waits for staff.";
          await leftForStaff(refusal);
          log("create case left for staff", { messageId: args.messageId, missing: ["Principal"] });
          return text(refusal, true);
        }
        const fields = missingOnly(intake.draft, args);
        // Nothing is written while an identity fact stays missing: the
        // address would be settled for a Case Pegasus then refuses. Only the
        // registration is one; a missing claimant name or claim number does
        // not refuse the Case (R1331).
        const stillMissing = (intake.missingIdentityFields ?? [])
          .filter((name) => !(name === "Vehicle registration" && fields.vehicleRegistration !== undefined));
        if (stillMissing.length > 0) {
          const refusal = `Pegasus would refuse the Case: ${intake.missingIdentityFields.join(", ")} missing. The e-mail waits for staff.`;
          await leftForStaff(refusal);
          log("create case left for staff", { messageId: args.messageId, missing: intake.missingIdentityFields });
          return text(refusal, true);
        }
        log("create case", {
          messageId: args.messageId, receiptId, fields: Object.keys(fields), reason: args.reason, dryRun,
          ...(senderPrincipal ? { senderPrincipal } : {}),
        });
        if (dryRun) {
          verdict(args.messageId);
          act(args.messageId, { action: "case_created", category: "new-instruction", reason: args.reason });
          return text("Dry run: would create the Case.");
        }
        // Pegasus's own suggested inspection address is accepted, as staff
        // accept it on Create case; the agent never supplies or corrects one.
        // With none suggested the Case is created without it (R965).
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
        try {
          const created = await pegasus.intakeAction({
            receiptId,
            action: "accept",
            expectedReceiptVersion: intake.version,
            operationKey: operationKey("case", args.messageId, intake.version),
            ...(senderPrincipal ? { principalCode: senderPrincipal } : {}),
            ...fields,
          });
          if (!created.caseId) {
            throw new Error(created.safeReason ?? `The Case was not created (${created.allocationStatus ?? "no allocation"}).`);
          }
          wrote(args.messageId, await pegasus.get(args.messageId).catch(() => undefined));
          act(args.messageId, {
            action: "case_created", category: "new-instruction",
            caseId: created.caseId, caseReference: created.caseReference ?? undefined, reason: args.reason,
          });
          return text(`Created Case ${created.caseReference ?? created.caseId}.`);
        } catch (error) {
          const settled = await settle(args.messageId, expected, (current) => current.summary.caseId != null);
          if (settled.kind === "landed") {
            log("create case landed after an error", { messageId: args.messageId, error: messageOf(error) });
            wrote(args.messageId, settled.detail);
            act(args.messageId, {
              action: "case_created", category: "new-instruction",
              caseId: settled.detail.summary.caseId ?? undefined, caseReference: settled.detail.summary.caseReference ?? undefined, reason: args.reason,
            });
            return text(`Created Case ${settled.detail.summary.caseReference ?? settled.detail.summary.caseId}.`);
          }
          throw error;
        }
      } catch (error) {
        // create_case follows an accepted new-instruction verdict; a refused
        // Case leaves the e-mail for staff Create case, which is that verdict's
        // outcome, not a failed attempt to retry every run.
        await leftForStaff(messageOf(error));
        log("create case refused", { messageId: args.messageId, error: messageOf(error) });
        return text(`Pegasus refused the Case: ${messageOf(error)}. The e-mail waits for staff.`, true);
      }
    });

  /**
   * A website enquiry's Case (R972): a private customer's one-off, which a
   * received item is never accepted as (R581), so it is created directly, as
   * staff New case with the Private customer tick does, and the e-mail is
   * then linked to it. The name and the registration must be written in the
   * enquiry. Called inside create_case's own refusal handling, with its way
   * of recording that the e-mail waits for staff.
   */
  async function createOneOff(
    args: { messageId: string; reason: string; claimantName?: string; vehicleRegistration?: string; incidentDate?: string },
    detail: MailDetail,
    expected: MessageState,
    intakeVersion: number,
    leftForStaff: (refusal: string) => Promise<void>,
  ) {
    const workType = typeOf(args.messageId, detail).workType;
    const claimantName = args.claimantName?.trim();
    const registration = args.vehicleRegistration ? normalizeRegistration(args.vehicleRegistration) : "";
    if (!workType || !claimantName || !registration) {
      const refusal = "A website enquiry becomes a one-off Case only with the customer's name and the vehicle's registration; the enquiry does not give both, so the e-mail waits for staff.";
      await leftForStaff(refusal);
      log("create case left for staff", { messageId: args.messageId, missing: ["website enquiry name or registration"] });
      return text(refusal, true);
    }
    const written = [detail.summary.subject, detail.bodyPlainText].join("\n");
    if (!RegistrationShape.test(registration) || /^\d+$/.test(registration) || !appearsIn(registration, written)) {
      return text(`${args.vehicleRegistration} is not written in the enquiry; give the registration exactly as the enquiry writes it, or leave the e-mail for staff.`, true);
    }
    if (!squash(written).includes(squash(claimantName))) {
      return text(`${claimantName} is not written in the enquiry; give the name exactly as the enquiry writes it, or leave the e-mail for staff.`, true);
    }
    if (!detail.canLinkCase || detail.intakeVersion == null) {
      const refusal = "The e-mail cannot be linked to a Case, so no one-off Case is made for it; it waits for staff.";
      await leftForStaff(refusal);
      return text(refusal, true);
    }
    log("create one-off case", { messageId: args.messageId, registration, reason: args.reason, dryRun });
    if (dryRun) {
      verdict(args.messageId);
      act(args.messageId, { action: "one_off_case", reason: args.reason });
      return text("Dry run: would create the one-off Case and link the e-mail to it.");
    }
    const created = await pegasus.caseCreate({
      caseType: workType,
      privateCustomer: true,
      claimantName,
      vehicleRegistration: registration,
      ...(args.incidentDate ? { incidentDate: args.incidentDate } : {}),
      operationKey: operationKey("one-off", args.messageId, intakeVersion),
    });
    const isLinked = (current: MailDetail) => current.summary.caseId?.toLowerCase() === created.caseId.toLowerCase();
    const oneOff: RunAct = { action: "one_off_case", caseId: created.caseId, caseReference: created.reference, reason: args.reason };
    try {
      const destination = (await pegasus.linkDestinations(args.messageId, created.reference))
        .find((item) => item.caseId.toLowerCase() === created.caseId.toLowerCase());
      if (!destination) {
        throw new Error("the new Case is not offered as a link destination");
      }
      const linked = await pegasus.link({
        messageId: args.messageId,
        caseId: destination.caseId,
        expectedIntakeVersion: detail.intakeVersion,
        expectedCaseVersion: destination.version,
        reason: ReasonPrefix + args.reason,
        operationKey: operationKey("one-off-link", args.messageId, detail.intakeVersion, newAttempt()),
      });
      wrote(args.messageId, linked);
      act(args.messageId, oneOff);
      return text(`Created one-off Case ${created.reference} and linked the e-mail to it.`);
    } catch (error) {
      const settled = await settle(args.messageId, expected, isLinked);
      if (settled.kind === "landed") {
        wrote(args.messageId, settled.detail);
        act(args.messageId, oneOff);
        return text(`Created one-off Case ${created.reference} and linked the e-mail to it.`);
      }
      // The Case exists; only its link is owed, and that is staff's to make.
      verdict(args.messageId);
      act(args.messageId, { ...oneOff, refusal: messageOf(error) });
      log("one-off case not linked", { messageId: args.messageId, caseReference: created.reference, error: messageOf(error) });
      return text(`Created one-off Case ${created.reference}, but the e-mail was not linked to it (${messageOf(error)}). Staff link it.`, true);
    }
  }

  const openTriage = tool(
    "open_triage",
    "Opens the Triage for a listed e-mail classified triage-request that has no Case, as staff Open the Triage does on the received item, with the vehicle registration you read. source says where: subject, body (the fresh text, never the quoted history), attachment (with attachmentOrdinal, its extracted text) or photos (the registration read_photos returned as read). The registration must appear there as written; a plate is never read from a photograph by you. Pegasus refuses the Triage when the item cannot open one, and the e-mail then waits for staff.",
    {
      messageId: z.guid().describe("messageId from list_eligible_emails"),
      vehicleRegistration: z.string().min(2).max(20),
      source: z.enum(["subject", "body", "attachment", "photos"]),
      attachmentOrdinal: z.number().int().min(0).optional().describe("Only with source attachment"),
      reason: z.string().min(1).max(450),
    },
    async (args) => {
      const candidate = batch.get(args.messageId);
      if (!candidate) {
        return text(NotListed, true);
      }
      const registration = normalizeRegistration(args.vehicleRegistration);
      if (!RegistrationShape.test(registration) || /^\d+$/.test(registration)) {
        return text("That is not a vehicle registration.", true);
      }
      // The registration must be where the model says it read it.
      let found: boolean;
      if (args.source === "photos") {
        found = candidate.photos?.state === "read" && candidate.photos.registration != null
          && normalizeRegistration(candidate.photos.registration) === registration;
        if (!found) {
          return text("Pegasus's plate reader did not agree on that registration; call read_photos first, and open the Triage from photos only with the registration it returned as read.", true);
        }
      } else if (args.source === "attachment") {
        if (args.attachmentOrdinal === undefined || args.attachmentOrdinal >= candidate.detail.attachments.length) {
          return text("source attachment needs the attachmentOrdinal of one of the e-mail's attachments.", true);
        }
        let attachment: string | null;
        try {
          attachment = (await pegasus.attachmentText(args.messageId, args.attachmentOrdinal, AttachmentTextLimit)).text;
        } catch (error) {
          return text(`Pegasus refused the read: ${messageOf(error)}`, true);
        }
        found = appearsIn(registration, attachment);
      } else {
        // The body is the fresh text only: a registration in the quoted
        // history is another message's (R1076).
        found = appearsIn(registration, args.source === "subject" ? candidate.detail.summary.subject : candidate.detail.freshBodyPlainText);
      }
      if (!found) {
        return text(`${args.vehicleRegistration} does not appear in the e-mail's ${args.source}; open the Triage only with a registration written there.`, true);
      }
      const read = await reread(args.messageId);
      if (typeof read === "string") {
        return text(read, true);
      }
      const { detail, expected } = read;
      if (detail.summary.caseId != null) {
        return text("The e-mail already has a Case or Triage.", true);
      }
      if (typeOf(args.messageId, detail).category !== "triage-request") {
        return text("Only an e-mail classified triage-request opens a Triage; classify it first.", true);
      }
      const receiptId = detail.summary.intakeReceiptId;
      if (!receiptId) {
        return text("The e-mail has no received item to open a Triage from; it waits for staff.", true);
      }
      log("open triage", { messageId: args.messageId, receiptId, registration, source: args.source, reason: args.reason, dryRun });
      const triage = (opened: { caseId?: string | null; caseReference?: string | null } = {}): RunAct => ({
        action: "triage_opened", category: "triage-request",
        caseId: opened.caseId ?? undefined, caseReference: opened.caseReference ?? opened.caseId ?? undefined, reason: args.reason,
      });
      if (dryRun) {
        verdict(args.messageId);
        act(args.messageId, triage());
        return text(`Dry run: would open the Triage for ${registration}.`);
      }
      try {
        const intake = await pegasus.intakeGet(receiptId);
        try {
          const opened = await pegasus.intakeAction({
            receiptId,
            action: "open_triage",
            expectedReceiptVersion: intake.version,
            operationKey: operationKey("triage", args.messageId, intake.version),
            vehicleRegistration: registration,
          });
          wrote(args.messageId, await pegasus.get(args.messageId).catch(() => undefined));
          act(args.messageId, triage(opened));
          return text(`Opened Triage ${opened.caseReference ?? opened.caseId}.`);
        } catch (error) {
          const settled = await settle(args.messageId, expected, (current) => current.summary.caseId != null);
          if (settled.kind === "landed") {
            log("open triage landed after an error", { messageId: args.messageId, error: messageOf(error) });
            wrote(args.messageId, settled.detail);
            act(args.messageId, triage(settled.detail.summary));
            return text(`Opened Triage ${settled.detail.summary.caseReference ?? settled.detail.summary.caseId}.`);
          }
          throw error;
        }
      } catch (error) {
        // As for create_case: a refused Triage leaves the e-mail for staff
        // Open the Triage (R588), not a failed attempt to retry every run.
        verdict(args.messageId);
        act(args.messageId, { action: "left_for_staff", category: "triage-request", reason: args.reason, refusal: messageOf(error) });
        log("open triage refused", { messageId: args.messageId, error: messageOf(error) });
        return text(`Pegasus refused the Triage: ${messageOf(error)}. The e-mail waits for staff.`, true);
      }
    });

  const leave = tool(
    "leave_email",
    "Leaves a listed e-mail as it is, with a one-sentence reason, when you have read it and nothing is to change: an e-mail that already has a type and nothing to link or open. An Unclassified e-mail is given a type instead, correspondence when nothing else fits; it may be left only when both search_mailboxes and search_old_system_folders were unavailable for it, and it is then looked at again next run. Nothing is written to Pegasus.",
    {
      messageId: z.guid().describe("messageId from list_eligible_emails"),
      reason: z.string().min(1).max(450),
    },
    async (args) => {
      const candidate = batch.get(args.messageId);
      if (!candidate) {
        return text(NotListed, true);
      }
      if (outcomes.verdicts.has(args.messageId)) {
        return text("The e-mail already has this run's verdict; nothing more is needed.");
      }
      if (typeOf(args.messageId, candidate.detail).category !== null) {
        verdict(args.messageId);
        act(args.messageId, { action: "left", reason: args.reason });
        log("leave", { messageId: args.messageId, reason: args.reason });
        return text("Left as it is.");
      }
      if (mailboxesUnavailableFor.has(args.messageId) && foldersUnavailableFor.has(args.messageId)) {
        outcomes.deferred.add(args.messageId);
        outcomes.failed.delete(args.messageId);
        act(args.messageId, { action: "deferred", reason: args.reason });
        log("leave until the lookups are back", { messageId: args.messageId, reason: args.reason });
        return text("Left as it is; it is looked at again next run.");
      }
      return text("Give an Unclassified e-mail a type: correspondence when nothing else fits.", true);
    });

  const openQuery = tool(
    "open_query",
    "Opens a query on a listed e-mail that is linked to a Case whose report is sent, when the fresh text is a question about or challenge to that report.",
    { messageId: z.guid().describe("messageId from list_eligible_emails") },
    async (args) => {
      const read = await reread(args.messageId);
      if (typeof read === "string") {
        return text(read, true);
      }
      log("open query", { messageId: args.messageId, dryRun });
      if (dryRun) {
        verdict(args.messageId);
        act(args.messageId, { action: "query_opened" });
        return text("Dry run: would open a query.");
      }
      try {
        // Pegasus replays a query by its key, so a retry after a lost answer
        // returns the query the first attempt opened.
        const opened = await pegasus.openQuery({
          messageId: args.messageId,
          operationKey: operationKey("qry", args.messageId, read.expected.classificationVersion),
        });
        wrote(args.messageId);
        act(args.messageId, { action: "query_opened" });
        return text(`Opened query ${opened.queryId}.`);
      } catch (error) {
        failed(args.messageId, messageOf(error));
        log("open query failed", { messageId: args.messageId, error: messageOf(error) });
        return text(`Pegasus refused the query, or did not answer: ${messageOf(error)}. Trying open_query again is safe.`, true);
      }
    });

  const searchFolders = tool(
    "search_old_system_folders",
    "Read-only. Searches the firm's old-system case folders by name and returns folder names only. The folders are named by the firm's own Case reference (QDOS26112, a.QDOS26112; for a PRIVATE one-off, its registration), so search by that reference; a reference-shaped term matches a folder name only as a whole token. isTruncated means more folders matched than were returned. state is unavailable when the folders could not be read.",
    {
      messageId: z.guid().describe("messageId from list_eligible_emails of the e-mail this lookup is for"),
      query: z.string().min(1).max(200).describe("The firm's own Case reference for the job, as named in the firm's earlier e-mail about it, or the reference or registration the e-mail itself names when search_mailboxes was unavailable"),
    },
    async (args) => {
      try {
        const result = await pegasus.searchOldSystemFolders(args.query, LookupLimit);
        if (result.state === "unavailable") {
          foldersUnavailableFor.add(args.messageId);
        }
        log("search old system folders", { messageId: args.messageId, query: args.query, state: result.state, hits: result.items.length });
        return text({
          state: result.state,
          isTruncated: result.isTruncated,
          folders: result.items.map((item) => ({ name: item.name, modifiedAtUtc: item.modifiedAtUtc })),
        });
      } catch (error) {
        foldersUnavailableFor.add(args.messageId);
        log("search old system folders failed", { messageId: args.messageId, query: args.query, error: messageOf(error) });
        return text(`Pegasus refused the lookup: ${messageOf(error)}`, true);
      }
    },
    { annotations: { readOnlyHint: true, openWorldHint: true } });

  const searchMailboxes = tool(
    "search_mailboxes",
    "Read-only. Searches the firm's approved mailboxes for other e-mail about a term and returns subjects, senders and dates only. state is unavailable when no mailbox could be read.",
    {
      messageId: z.guid().describe("messageId from list_eligible_emails of the e-mail this lookup is for"),
      query: z.string().min(1).max(200).describe("A case reference, registration, claimant or claim number"),
    },
    async (args) => {
      try {
        const result = await pegasus.searchMailboxes(args.query, LookupLimit);
        if (result.state === "unavailable") {
          mailboxesUnavailableFor.add(args.messageId);
        }
        log("search mailboxes", { messageId: args.messageId, query: args.query, state: result.state, hits: result.items.length });
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
        mailboxesUnavailableFor.add(args.messageId);
        log("search mailboxes failed", { messageId: args.messageId, query: args.query, error: messageOf(error) });
        return text(`Pegasus refused the lookup: ${messageOf(error)}`, true);
      }
    },
    { annotations: { readOnlyHint: true, openWorldHint: true } });

  return [
    listEligible, readEmail, readAttachment, readPhotos, classify, link, openQuery, createCase, openTriage, leave,
    searchFolders, searchMailboxes, searchCases,
  ];
}

export function createClassifierServer(options: ClassifierToolOptions) {
  return createSdkMcpServer({
    name: ServerName,
    version: "1.0.0",
    tools: createClassifierTools(options),
    alwaysLoad: true,
  });
}

/** Lower case with runs of white space as one space, for finding a name as written. */
function squash(value: string): string {
  return value.toLowerCase().replace(/\s+/g, " ").trim();
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

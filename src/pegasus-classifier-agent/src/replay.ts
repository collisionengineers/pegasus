// A replay of one batch from a fixture file instead of Pegasus:
//   npm run build && npm run replay -- <fixture.json> [messageId ...]
// It calls the model exactly as a run does (same prompt, tools and turn
// cap), records every write the tools would have made, and prints one line
// per e-mail. Nothing reaches Pegasus. It needs ANTHROPIC_API_KEY and takes
// CLASSIFIER_MODEL and CLASSIFIER_MAX_BUDGET_USD when set.
//
// The fixture is { candidates, mailboxCorpus?, folders? }:
//   candidates     the batch, in the shape collectBatch builds (MailDetail
//                  plus the suggested Cases for linking)
//   mailboxCorpus  what search_mailboxes may find: message headers with a
//                  preview; a term matches a header whose subject or preview
//                  contains it, case-insensitively. Absent: the search is
//                  unavailable.
//   folders        what search_old_system_folders may find: folder names; a
//                  term matches a folder whose name contains it as a whole
//                  token, as Pegasus matches a reference-shaped term (R592).
//                  Absent: the search is unavailable.
//   cases          what search_cases may find: link destinations; a term
//                  matches a reference, registration, claimant or claim
//                  number containing it, case-insensitively. Absent: none.
//   intake         per intakeReceiptId, what pegasus_intake_get returns for
//                  create_case. Absent: a draft with every identity fact and
//                  a settled inspection address, so create_case succeeds.

import { randomUUID } from "node:crypto";
import { readFileSync } from "node:fs";
import type { ClassifierConfig } from "./config.js";
import type {
  IntakeActionResult,
  IntakeDetail,
  LinkDestination,
  MailAttachmentText,
  MailboxSearch,
  MailClassification,
  MailDetail,
  OldSystemFolderSearch,
} from "./pegasus.js";
import { runAgent } from "./run.js";
import { createClassifierServer, type Candidate, type ClassifierPegasus } from "./tools.js";

/** A mailbox header the search may return; text, when given, is what the term is matched against. */
type CorpusItem = MailboxSearch["items"][number] & { text?: string };

/** A batch member; attachmentTexts holds the reader's text per attachment ordinal (null: none). */
type FixtureCandidate = Candidate & { attachmentTexts?: (string | null)[] };

interface Fixture {
  candidates: FixtureCandidate[];
  mailboxCorpus?: CorpusItem[];
  folders?: { name: string; modifiedAtUtc: string | null }[];
  cases?: (LinkDestination & { claimNumber?: string | null })[];
  intake?: Record<string, Partial<IntakeDetail>>;
}

interface Write {
  kind: "classify" | "link" | "query" | "case";
  messageId: string;
  detail: string;
}

class ReplayPegasus implements ClassifierPegasus {
  readonly writes: Write[] = [];
  private readonly details = new Map<string, MailDetail>();
  private readonly created = new Map<string, string>();

  constructor(private readonly fixture: Fixture) {
    for (const candidate of fixture.candidates) {
      this.details.set(candidate.detail.summary.id, structuredClone(candidate.detail));
    }
  }

  async get(messageId: string): Promise<MailDetail> {
    const detail = this.details.get(messageId);
    if (!detail) {
      throw new Error("No such message.");
    }
    return structuredClone(detail);
  }

  async correct(args: {
    messageId: string;
    expectedClassificationVersion: number;
    classificationKey: string;
    workType?: string;
    reason: string;
    operationKey: string;
  }): Promise<MailClassification> {
    const detail = await this.require(args.messageId);
    const current = detail.classification!;
    if (current.version !== args.expectedClassificationVersion) {
      throw new Error("The classification has changed since it was read.");
    }
    const isCategory = args.classificationKey !== "correspondence";
    const next: MailClassification = {
      version: current.version + 1,
      currentActor: "automation:replay",
      current: {
        outcome: isCategory ? "Classified" : "Unclassified",
        category: isCategory ? args.classificationKey : null,
        caseType: args.workType ?? null,
        reason: args.reason,
      },
    };
    detail.classification = next;
    detail.summary.classification = next.current;
    this.writes.push({
      kind: "classify",
      messageId: args.messageId,
      detail: `${args.classificationKey}${args.workType ? ` (${args.workType})` : ""}: ${args.reason}`,
    });
    return structuredClone(next);
  }

  async link(args: {
    messageId: string;
    caseId: string;
    expectedIntakeVersion: number;
    expectedCaseVersion: number;
    reason: string;
    operationKey: string;
  }): Promise<MailDetail> {
    const detail = await this.require(args.messageId);
    const candidate = this.fixture.candidates.find((item) => item.detail.summary.id === args.messageId);
    const destination = candidate?.destinations.find((item) => item.caseId === args.caseId)
      ?? candidate?.found?.find((item) => item.caseId === args.caseId)
      ?? this.fixture.cases?.find((item) => item.caseId === args.caseId);
    if (!destination) {
      throw new Error("That Case is not a link destination for the message.");
    }
    detail.summary.caseId = destination.caseId;
    detail.summary.caseReference = destination.reference;
    detail.canLinkCase = false;
    this.writes.push({ kind: "link", messageId: args.messageId, detail: `${destination.reference}: ${args.reason}` });
    return structuredClone(detail);
  }

  async attachmentText(messageId: string, attachmentOrdinal: number, maximumCharacters: number): Promise<MailAttachmentText> {
    const candidate = this.fixture.candidates.find((item) => item.detail.summary.id === messageId);
    const attachment = candidate?.detail.attachments[attachmentOrdinal];
    if (!candidate || !attachment) {
      throw new Error("The message has no attachment at that position.");
    }
    const full = candidate.attachmentTexts?.[attachmentOrdinal] ?? null;
    const isTruncated = full !== null && full.length > maximumCharacters;
    return {
      messageId,
      attachmentOrdinal,
      fileName: attachment.fileName,
      mediaType: attachment.mediaType,
      text: isTruncated ? full.slice(0, maximumCharacters) : full,
      isTruncated,
    };
  }

  async openQuery(args: { messageId: string; operationKey: string }): Promise<{ queryId: string }> {
    await this.require(args.messageId);
    const queryId = randomUUID();
    this.writes.push({ kind: "query", messageId: args.messageId, detail: queryId });
    return { queryId };
  }

  async searchOldSystemFolders(query: string, pageSize: number): Promise<OldSystemFolderSearch> {
    const folders = this.fixture.folders;
    if (!folders) {
      return { query, items: [], isTruncated: false, state: "unavailable" };
    }
    const wanted = query.trim().toLowerCase();
    const hits = folders
      .filter((folder) => folder.name.toLowerCase().split(/[\s_\-,()[\]/;]+/).includes(wanted))
      .map((folder) => ({ folderId: randomUUID(), name: folder.name, modifiedAtUtc: folder.modifiedAtUtc }));
    return { query, items: hits.slice(0, pageSize), isTruncated: hits.length > pageSize, state: "available" };
  }

  async searchMailboxes(query: string, pageSize: number): Promise<MailboxSearch> {
    const corpus = this.fixture.mailboxCorpus;
    if (!corpus) {
      return { query, items: [], mailboxes: [], isTruncated: false, state: "unavailable" };
    }
    const wanted = query.trim().toLowerCase();
    const hits = corpus
      .filter((item) =>
        (item.subject ?? "").toLowerCase().includes(wanted)
        || (item.text ?? item.preview ?? "").toLowerCase().includes(wanted))
      .map(({ text: _text, ...item }) => item);
    const mailboxes = [...new Map(corpus.map((item) => [item.mailboxAddress, item.mailboxId])).entries()]
      .map(([mailboxAddress, mailboxId]) => ({ mailboxId, mailboxAddress, state: "available" as const }));
    return { query, items: hits.slice(0, pageSize), mailboxes, isTruncated: hits.length > pageSize, state: "available" };
  }

  async linkDestinations(_messageId: string, query?: string): Promise<LinkDestination[]> {
    const wanted = (query ?? "").trim().toLowerCase();
    if (!wanted) {
      return [];
    }
    return (this.fixture.cases ?? [])
      .filter((item) => [item.reference, item.registration, item.claimant, item.claimNumber]
        .some((value) => (value ?? "").toLowerCase().includes(wanted)))
      .map(({ claimNumber: _claimNumber, ...item }) => item);
  }

  async intakeGet(receiptId: string): Promise<IntakeDetail> {
    const given = this.fixture.intake?.[receiptId] ?? {};
    const created = this.created.get(receiptId);
    return {
      receiptId,
      version: 1,
      decision: "needs_sorting",
      caseId: created ?? null,
      caseReference: created ? `REPLAY-${created.slice(0, 8)}` : null,
      classifiedCaseType: null,
      draft: { claimantName: "Replay", claimNumber: "Replay", vehicleRegistration: "AB12CDE", dateOfIncident: "2026-01-01" },
      missingIdentityFields: [],
      inspectionAddressState: "Accepted",
      inspectionAddressSuggestion: null,
      inspectionAddressSuggestionFingerprint: null,
      ...given,
    };
  }

  async intakeAction(args: {
    receiptId: string;
    action: "accept" | "resolve_inspection_address";
    expectedReceiptVersion: number;
    operationKey: string;
    claimantName?: string;
    claimNumber?: string;
    vehicleRegistration?: string;
    incidentDate?: string;
  }): Promise<IntakeActionResult> {
    const intake = await this.intakeGet(args.receiptId);
    if (args.action === "resolve_inspection_address") {
      return { receiptId: args.receiptId, action: args.action, allocationStatus: null, caseId: null, caseReference: null, safeReason: null };
    }
    if (intake.missingIdentityFields.length > 0 && !args.claimantName && !args.claimNumber && !args.vehicleRegistration) {
      throw new Error(`The draft lacks ${intake.missingIdentityFields.join(", ")}.`);
    }
    const caseId = randomUUID();
    this.created.set(args.receiptId, caseId);
    const detail = [...this.details.values()].find((item) => item.summary.intakeReceiptId === args.receiptId);
    if (detail) {
      detail.summary.caseId = caseId;
      detail.summary.caseReference = `REPLAY-${caseId.slice(0, 8)}`;
      detail.canLinkCase = false;
      this.writes.push({
        kind: "case",
        messageId: detail.summary.id,
        detail: JSON.stringify({ claimantName: args.claimantName, claimNumber: args.claimNumber, vehicleRegistration: args.vehicleRegistration, incidentDate: args.incidentDate }),
      });
    }
    return { receiptId: args.receiptId, action: args.action, allocationStatus: "Succeeded", caseId, caseReference: `REPLAY-${caseId.slice(0, 8)}`, safeReason: null };
  }

  private async require(messageId: string): Promise<MailDetail> {
    const detail = this.details.get(messageId);
    if (!detail) {
      throw new Error("No such message.");
    }
    return detail;
  }
}

function required(name: string): string {
  const value = process.env[name]?.trim();
  if (!value) {
    throw new Error(`${name} is not set.`);
  }
  return value;
}

const [fixturePath, ...only] = process.argv.slice(2);
if (!fixturePath) {
  console.error("Usage: npm run replay -- <fixture.json> [messageId ...]");
  process.exit(2);
}

const fixture = JSON.parse(readFileSync(fixturePath, "utf8")) as Fixture;
const candidates = only.length > 0
  ? fixture.candidates.filter((item) => only.includes(item.detail.summary.id))
  : fixture.candidates;
const batch = new Map(candidates.map((item) => [item.detail.summary.id, item]));

const config: ClassifierConfig = {
  anthropicApiKey: required("ANTHROPIC_API_KEY"),
  mcpUrl: "replay",
  tokenUrl: "replay",
  clientId: "replay",
  clientSecret: "replay",
  model: process.env.CLASSIFIER_MODEL?.trim() || "claude-sonnet-5-5",
  dryRun: false,
  batch: batch.size,
  scanPages: 1,
  maxBudgetUsd: Number(process.env.CLASSIFIER_MAX_BUDGET_USD?.trim() || "2"),
  storageAccount: undefined,
};

const pegasus = new ReplayPegasus({ ...fixture, candidates });
const failed = new Map<string, string>();
const verdicts = new Set<string>();
const reads = new Set<string>();
const attachmentReads: string[] = [];
const lookups: string[] = [];
const log = (message: string, properties?: Record<string, unknown>) => {
  if (message === "read") {
    reads.add(String(properties?.messageId));
  } else if (message === "read attachment") {
    attachmentReads.push(`${String(properties?.messageId).slice(0, 8)} #${String(properties?.attachmentOrdinal)} ${String(properties?.fileName)} (${String(properties?.characters)} chars)`);
  } else if (message.startsWith("search ")) {
    lookups.push(`${message}: ${String(properties?.query)} -> ${String(properties?.hits)} (${String(properties?.state)})`);
  } else if (message === "result") {
    return;
  }
  console.error(JSON.stringify({ message, ...properties }));
};

const server = createClassifierServer({ pegasus, batch, failed, verdicts, dryRun: false, log });
const result = await runAgent(config, server, batch.size, log);

console.log(`\nReplay of ${batch.size} e-mail(s): ${result.outcome}, ${result.turns} turns, USD ${result.costUsd.toFixed(3)}`);
console.log(`read ${reads.size}/${batch.size}; attachments read ${attachmentReads.length}; verdicts ${verdicts.size}; refused writes ${failed.size}; lookups ${lookups.length}`);
for (const line of attachmentReads) {
  console.log(`  attachment ${line}`);
}
for (const line of lookups) {
  console.log(`  ${line}`);
}
console.log("");
for (const candidate of candidates) {
  const id = candidate.detail.summary.id;
  const writes = pegasus.writes.filter((write) => write.messageId === id);
  const verdict = writes.length === 0
    ? (failed.has(id) ? `REFUSED ${failed.get(id)}` : "no verdict")
    : writes.map((write) => `${write.kind} ${write.detail}`).join(" | ");
  console.log(`${id}  ${(candidate.detail.summary.subject ?? "").slice(0, 70)}`);
  console.log(`    read=${reads.has(id) ? "yes" : "NO"}  ${verdict}`);
}
console.log(`\nModel summary:\n${result.summary ?? "(none)"}`);

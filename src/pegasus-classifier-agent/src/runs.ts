// The record of each sweep, so the Administration page can show the
// classifier's runs and what each did per message. Pegasus records every run
// through pegasus_classifier_run_record: opened once Pegasus answers, closed
// with the outcome and one item per message of the batch. Production also
// keeps each run in the `runs` table of its own storage account, which the
// hosted test instance reads to show production's runs; a run with no
// storage account keeps none. A record that fails is logged and the sweep
// goes on: the record describes the run and never decides it.
//
// One row per run in `runs`, merged on open and again on close:
//   partition  the UTC date the run started, yyyy-MM-dd
//   row        the run id
//   open       startedAt and the run's facts
//   close      the same again, so a lost open still makes the row; then
//              finishedAt, the result's figures, counts (items per action, as
//              JSON), acted (items that wrote, or would have in a dry run)
//              and items (as JSON, each with its e-mail's subject and sender,
//              since the test instance cannot read production's mail)

import { randomUUID } from "node:crypto";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { TableClient, type Edm, type TableEntity } from "@azure/data-tables";
import { ManagedIdentityCredential } from "@azure/identity";
import type { ClassifierConfig } from "./config.js";
import type { PegasusMail, RunFacts, RunItem, RunRecord, RunResult } from "./pegasus.js";
import { messageOf, WriteActions, type Candidate, type Logger, type RunOutcomes } from "./tools.js";

/** Why a message a completed run gave no verdict counts as failed. */
export const NoVerdict = "no verdict in a completed run";

const ReasonLimit = 500;
const RefusalLimit = 1000;
const ErrorLimit = 1000;
const SummaryLimit = 4000;
const SubjectLimit = 200;
/**
 * Characters of the row's items JSON. A table string property holds 32K
 * UTF-16 characters and a row 1 MiB, so the items stay under both.
 */
export const ItemsLimit = 30000;
/** What reasons and refusals are cut to when the items do not fit the row. */
const ShortLimit = 200;

const PackageName = "pegasus-classifier-agent";

/** The one place a recorded string is cut to its limit. */
export function cap(value: string, limit: number): string {
  return value.length > limit ? value.slice(0, limit) : value;
}

/** A run's result before its duration, which the recorder measures. */
export type RunEnding = Omit<RunResult, "durationMs">;

/** An ending with nothing done: a tick with nothing eligible, or a run that threw. */
export function emptyRun(
  outcome: string,
  seen: Pick<RunEnding, "awaitingDecision" | "candidatesHidden" | "hiddenRuns"> = { awaitingDecision: 0, candidatesHidden: false, hiddenRuns: 0 },
): RunEnding {
  return { outcome, eligible: 0, ...seen, verdicts: 0, failed: 0, deferred: 0, turns: 0, costUsd: 0, items: [] };
}

/** A run that threw after it opened: its outcome names the error. */
export function failedRun(error: unknown): RunEnding {
  const message = cap(messageOf(error), ErrorLimit);
  return { ...emptyRun(`failed: ${message}`), error: message };
}

/**
 * One item per message of the batch, in batch order: what the run did with
 * it, or that it failed, was set aside (closed as failed by run.ts
 * recordOutcomes) or was not reached.
 */
export function buildItems(
  batch: ReadonlyMap<string, Candidate>,
  outcomes: RunOutcomes,
  completed: boolean,
  setAside: ReadonlySet<string>,
): RunItem[] {
  return [...batch.keys()].map((messageId) => cut(itemOf(messageId, outcomes, completed, setAside), ReasonLimit, RefusalLimit));
}

function itemOf(messageId: string, outcomes: RunOutcomes, completed: boolean, setAside: ReadonlySet<string>): RunItem {
  const act = outcomes.acts.get(messageId);
  if (outcomes.verdicts.has(messageId)) {
    return { action: "left", ...act, messageId };
  }
  if (outcomes.deferred.has(messageId)) {
    return { ...act, messageId, action: "deferred" };
  }
  // As run.ts recordOutcomes reads it: a failed write, or no verdict from a
  // completed run, is a failed attempt; a cut-short run did not reach the rest.
  const failure = outcomes.failed.get(messageId) ?? (completed ? NoVerdict : undefined);
  if (failure === undefined) {
    return { messageId, action: "not_reached" };
  }
  return {
    messageId,
    action: setAside.has(messageId) ? "set_aside" : "failed",
    ...(act?.category !== undefined ? { category: act.category } : {}),
    refusal: failure,
  };
}

function cut(item: RunItem, reasonLimit: number, refusalLimit: number): RunItem {
  return {
    ...item,
    ...(item.reason !== undefined ? { reason: cap(item.reason, reasonLimit) } : {}),
    ...(item.refusal !== undefined ? { refusal: cap(item.refusal, refusalLimit) } : {}),
  };
}

/** The stage's idempotency key: mcp:, the stage, and the run id without hyphens. */
export function runKey(stage: RunRecord["stage"], runId: string): string {
  return `mcp:run-${stage}:${runId.replaceAll("-", "")}`;
}

/** How this run is set up, as both stages send it. */
export function runFacts(config: Pick<ClassifierConfig, "dryRun" | "model" | "batch" | "scanPages" | "maxBudgetUsd">): RunFacts {
  return {
    dryRun: config.dryRun,
    model: config.model,
    batch: config.batch,
    scanPages: config.scanPages,
    maxBudgetUsd: config.maxBudgetUsd,
    agentVersion: agentVersion(),
  };
}

/**
 * This package's version, from the first package.json above this module that
 * is this package's: the module runs from dist/ in Azure and from
 * build-test/src/ in the tests.
 */
export function agentVersion(): string {
  let directory = fileURLToPath(new URL(".", import.meta.url));
  for (;;) {
    const version = versionIn(directory);
    if (version !== undefined) {
      return version;
    }
    const parent = dirname(directory);
    if (parent === directory) {
      return "unknown";
    }
    directory = parent;
  }
}

function versionIn(directory: string): string | undefined {
  try {
    const manifest = JSON.parse(readFileSync(join(directory, "package.json"), "utf8")) as { name?: unknown; version?: unknown };
    return manifest.name === PackageName && typeof manifest.version === "string" ? manifest.version : undefined;
  } catch {
    // No package.json here, or not one that reads: look further up.
    return undefined;
  }
}

/** A run as it opened. */
export interface RunStart {
  runId: string;
  startedAt: Date;
  facts: RunFacts;
}

/** Where production keeps its own record of each run. */
export interface RunStore {
  open(run: RunStart): Promise<void>;
  close(run: RunStart, finishedAt: Date, result: RunResult, batch: ReadonlyMap<string, Candidate>): Promise<void>;
}

/** What TableRunStore calls of a TableClient, so a test can pass its own. */
export interface RunTable {
  upsertEntity(entity: TableEntity<Record<string, unknown>>, mode: "Merge"): Promise<unknown>;
}

export class TableRunStore implements RunStore {
  constructor(private readonly table: RunTable) {}

  static inAccount(storageAccount: string): TableRunStore {
    return new TableRunStore(new TableClient(
      `https://${storageAccount}.table.core.windows.net`,
      "runs",
      new ManagedIdentityCredential({ clientId: process.env.AZURE_CLIENT_ID })));
  }

  async open(run: RunStart): Promise<void> {
    await this.table.upsertEntity(openedRow(run), "Merge");
  }

  async close(run: RunStart, finishedAt: Date, result: RunResult, batch: ReadonlyMap<string, Candidate>): Promise<void> {
    await this.table.upsertEntity({
      ...openedRow(run),
      finishedAt: finishedAt.toISOString(),
      outcome: result.outcome,
      eligible: result.eligible,
      awaitingDecision: result.awaitingDecision,
      candidatesHidden: result.candidatesHidden,
      hiddenRuns: result.hiddenRuns,
      verdicts: result.verdicts,
      failed: result.failed,
      deferred: result.deferred,
      turns: result.turns,
      costUsd: double(result.costUsd),
      durationMs: result.durationMs,
      ...(result.error !== undefined ? { error: result.error } : {}),
      ...(result.summary !== undefined ? { summary: result.summary } : {}),
      acted: result.items.filter((item) => WriteActions.has(item.action)).length,
      counts: JSON.stringify(countsOf(result.items)),
      ...itemsColumn(result.items, batch),
    }, "Merge");
  }
}

function openedRow(run: RunStart): TableEntity<Record<string, unknown>> {
  return {
    partitionKey: run.startedAt.toISOString().slice(0, 10),
    rowKey: run.runId,
    startedAt: run.startedAt.toISOString(),
    dryRun: run.facts.dryRun,
    model: run.facts.model,
    batch: run.facts.batch,
    scanPages: run.facts.scanPages,
    maxBudgetUsd: double(run.facts.maxBudgetUsd),
    agentVersion: run.facts.agentVersion,
  };
}

/** An amount in dollars, typed: a whole number would otherwise be stored as Int32. */
function double(value: number): Edm<"Double"> {
  return { value, type: "Double" };
}

function countsOf(items: RunItem[]): Record<string, number> {
  const counts: Record<string, number> = {};
  for (const item of items) {
    counts[item.action] = (counts[item.action] ?? 0) + 1;
  }
  return counts;
}

/** An item as the row keeps it, with what the test instance cannot read of production's mail. */
type RowItem = RunItem & { subject?: string; sender?: string };

function withMail(item: RunItem, batch: ReadonlyMap<string, Candidate>): RowItem {
  const summary = batch.get(item.messageId)?.detail.summary;
  return {
    ...item,
    ...(summary?.subject ? { subject: cap(summary.subject, SubjectLimit) } : {}),
    ...(summary?.senderAddress ? { sender: summary.senderAddress } : {}),
  };
}

/**
 * The items as JSON within ItemsLimit: whole when they fit, else with shorter
 * reasons and refusals, and only then without the last items, which
 * itemsTruncated says.
 */
function itemsColumn(items: RunItem[], batch: ReadonlyMap<string, Candidate>): { items: string; itemsTruncated?: true } {
  const whole = JSON.stringify(items.map((item) => withMail(item, batch)));
  if (whole.length <= ItemsLimit) {
    return { items: whole };
  }
  const rows = items.map((item) => withMail(cut(item, ShortLimit, ShortLimit), batch));
  let kept = rows.length;
  let json = JSON.stringify(rows);
  while (json.length > ItemsLimit) {
    kept--;
    json = JSON.stringify(rows.slice(0, kept));
  }
  return kept < rows.length ? { items: json, itemsTruncated: true } : { items: json };
}

/**
 * Records one run in Pegasus and, given a store, in production's table. Each
 * stage tries every target and logs what fails, so a record never stops the
 * sweep.
 */
export class RunRecorder {
  readonly runId = randomUUID();
  readonly startedAt = new Date();

  constructor(
    private readonly facts: RunFacts,
    private readonly pegasus: Pick<PegasusMail, "recordRun">,
    private readonly store: RunStore | undefined,
    private readonly log: Logger,
  ) {}

  async open(): Promise<void> {
    const { runId, facts, store } = this;
    await this.attempt("open", "pegasus", () =>
      this.pegasus.recordRun({ runId, stage: "open", operationKey: runKey("open", runId), facts }));
    if (store) {
      await this.attempt("open", "storage", () => store.open(this.start()));
    }
  }

  /** Closes the run; batch gives the table each item's subject and sender. */
  async close(ending: RunEnding, batch: ReadonlyMap<string, Candidate> = new Map()): Promise<void> {
    const { runId, facts, store } = this;
    const finishedAt = new Date();
    const result: RunResult = {
      ...ending,
      durationMs: finishedAt.getTime() - this.startedAt.getTime(),
      ...(ending.error !== undefined ? { error: cap(ending.error, ErrorLimit) } : {}),
      ...(ending.summary !== undefined ? { summary: cap(ending.summary, SummaryLimit) } : {}),
    };
    await this.attempt("close", "pegasus", () =>
      this.pegasus.recordRun({ runId, stage: "close", operationKey: runKey("close", runId), facts, result }));
    if (store) {
      await this.attempt("close", "storage", () => store.close(this.start(), finishedAt, result, batch));
    }
  }

  private start(): RunStart {
    return { runId: this.runId, startedAt: this.startedAt, facts: this.facts };
  }

  private async attempt(stage: RunRecord["stage"], target: "pegasus" | "storage", record: () => Promise<unknown>): Promise<void> {
    try {
      await record();
    } catch (error) {
      this.log("run record failed", { runId: this.runId, stage, target, error: messageOf(error) });
    }
  }
}

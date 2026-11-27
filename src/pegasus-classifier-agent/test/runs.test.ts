import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { describe, it } from "node:test";
import type { TableEntity } from "@azure/data-tables";
import { FixturePegasus } from "../src/fixture-pegasus.js";
import type { LinkDestination, RunFacts, RunItem } from "../src/pegasus.js";
import {
  agentVersion,
  buildItems,
  emptyRun,
  ItemsLimit,
  NoVerdict,
  RunRecorder,
  TableRunStore,
  type RunTable,
} from "../src/runs.js";
import type { Candidate } from "../src/tools.js";
import { candidate, harness, mail } from "./support.js";

const facts: RunFacts = { dryRun: false, model: "claude-sonnet-5-5", batch: 20, scanPages: 10, maxBudgetUsd: 2, agentVersion: "1.0.0" };

function destination(reference: string): LinkDestination {
  return {
    caseId: randomUUID(), reference, registration: null, claimant: null, principal: null,
    state: "Open", triageState: null, isTriageCase: false, version: 1,
  };
}

function logged() {
  const logs: { message: string; properties?: Record<string, unknown> }[] = [];
  return { logs, log: (message: string, properties?: Record<string, unknown>) => logs.push({ message, ...(properties ? { properties } : {}) }) };
}

function fakeTable() {
  const writes: { entity: TableEntity<Record<string, unknown>>; mode: string }[] = [];
  const table: RunTable = { upsertEntity: async (entity, mode) => { writes.push({ entity, mode }); } };
  return { writes, table };
}

describe("the run's items", () => {
  async function fiveMessages() {
    const classified = mail();
    const linked = mail({ outcome: "Classified", category: "correspondence" });
    const left = mail({ outcome: "Classified", category: "triage-request" });
    const deferred = mail();
    const unreached = mail();
    const suggested = destination("QDOS26002");
    const run = harness([
      candidate(classified), candidate(linked, { destinations: [suggested] }), candidate(left), candidate(deferred), candidate(unreached),
    ]);
    await run.call("classify_email", { messageId: classified.summary.id, category: "cancellation", reason: "The sender cancels." });
    await run.call("link_case", { messageId: linked.summary.id, caseId: suggested.caseId, reason: "The registration matches." });
    await run.call("leave_email", { messageId: left.summary.id, reason: "No registration." });
    await run.call("search_mailboxes", { messageId: deferred.summary.id, query: "QDOS26001" });
    await run.call("search_old_system_folders", { messageId: deferred.summary.id, query: "QDOS26001" });
    await run.call("leave_email", { messageId: deferred.summary.id, reason: "Neither lookup could be read." });
    return { run, unreached: unreached.summary.id };
  }

  it("gives one item per message, in batch order, from what the run did", async () => {
    const { run } = await fiveMessages();
    const items = buildItems(run.batch, run.outcomes, false, new Set());
    assert.deepEqual(items.map((item) => item.action), ["classified", "linked", "left", "deferred", "not_reached"]);
    assert.equal(items[0]?.category, "cancellation");
    assert.equal(items[0]?.reason, "The sender cancels.");
    assert.ok(items[0]?.at);
    assert.equal(items[1]?.caseReference, "QDOS26002");
    assert.equal(items[2]?.reason, "No registration.");
    assert.deepEqual(items[4], { messageId: [...run.batch.keys()][4], action: "not_reached" });
  });

  it("reads a message a completed run gave no verdict as failed, or set aside once closed as failed", async () => {
    const { run, unreached } = await fiveMessages();
    const failed = buildItems(run.batch, run.outcomes, true, new Set())[4];
    assert.equal(failed?.action, "failed");
    assert.equal(failed?.refusal, NoVerdict);
    assert.equal(buildItems(run.batch, run.outcomes, true, new Set([unreached]))[4]?.action, "set_aside");
  });

  it("keeps a classification the later Case refusal left for staff, with the refusal", async () => {
    const receiptId = randomUUID();
    const detail = mail({ receiptId });
    const candidates = [candidate(detail)];
    const pegasus = new FixturePegasus({
      candidates, intake: { [receiptId]: { establishedPrincipalCode: null, senderDirectoryPrincipalCode: null, draft: null } },
    });
    const run = harness(candidates, { pegasus });
    await run.call("classify_email", {
      messageId: detail.summary.id, category: "new-instruction", workType: "Inspection", reason: "The letter instructs an inspection.",
    });
    const refused = await run.call("create_case", { messageId: detail.summary.id, reason: "The instruction is complete." });
    assert.equal(refused.isError, true);
    const [item] = buildItems(run.batch, run.outcomes, true, new Set());
    assert.equal(item?.action, "classified");
    assert.equal(item?.category, "new-instruction");
    assert.equal(item?.reason, "The letter instructs an inspection.");
    assert.match(item?.refusal ?? "", /knows no Principal/);
  });
});

describe("the run's record in Pegasus", () => {
  it("opens and closes the run under keys made from its id, with the facts on both and the result on close", async () => {
    const pegasus = new FixturePegasus({ candidates: [] });
    const recorder = new RunRecorder(facts, pegasus, undefined, () => {});
    await recorder.open();
    await recorder.close(emptyRun("nothing eligible"));
    const key = recorder.runId.replaceAll("-", "");
    const [opened, closed] = pegasus.runRecords;
    assert.equal(pegasus.runRecords.length, 2);
    assert.deepEqual(opened, { runId: recorder.runId, stage: "open", operationKey: `mcp:run-open:${key}`, facts });
    assert.equal(closed?.stage, "close");
    assert.equal(closed?.operationKey, `mcp:run-close:${key}`);
    assert.deepEqual(closed?.facts, facts);
    assert.equal(closed?.result?.outcome, "nothing eligible");
    assert.deepEqual(closed?.result?.items, []);
    assert.ok((closed?.result?.durationMs ?? -1) >= 0);
  });

  it("logs a record Pegasus refuses and goes on", async () => {
    const { logs, log } = logged();
    const refusing = { recordRun: async () => { throw new Error("Unknown tool."); } };
    const recorder = new RunRecorder(facts, refusing, undefined, log);
    await recorder.open();
    await recorder.close(emptyRun("nothing eligible"));
    assert.deepEqual(
      logs.map((entry) => [entry.message, entry.properties?.stage, entry.properties?.target, entry.properties?.error]),
      [["run record failed", "open", "pegasus", "Unknown tool."], ["run record failed", "close", "pegasus", "Unknown tool."]]);
  });

  it("reads this package's version", () => {
    assert.match(agentVersion(), /^\d+\.\d+\.\d+/);
  });
});

describe("production's runs table", () => {
  const run = { runId: randomUUID(), startedAt: new Date("2026-11-27T23:30:00-02:00"), facts };

  it("opens the run's row under the UTC date it started", async () => {
    const { writes, table } = fakeTable();
    await new TableRunStore(table).open(run);
    assert.equal(writes.length, 1);
    assert.equal(writes[0]?.mode, "Merge");
    assert.deepEqual(writes[0]?.entity, {
      partitionKey: "2026-11-28",
      rowKey: run.runId,
      startedAt: "2026-11-28T01:30:00.000Z",
      dryRun: false,
      model: "claude-sonnet-5-5",
      batch: 20,
      scanPages: 10,
      maxBudgetUsd: { value: 2, type: "Double" },
      agentVersion: "1.0.0",
    });
  });

  it("closes it with the outcome, the counts and the items with each e-mail's subject and sender", async () => {
    const detail = mail({ subject: "Cancel the inspection" });
    const harnessed = harness([candidate(detail)]);
    await harnessed.call("classify_email", { messageId: detail.summary.id, category: "cancellation", reason: "The sender cancels." });
    const items = buildItems(harnessed.batch, harnessed.outcomes, true, new Set());
    const { writes, table } = fakeTable();
    await new TableRunStore(table).close(run, new Date("2026-11-28T01:31:00Z"), {
      ...emptyRun("success"), eligible: 1, verdicts: 1, turns: 4, costUsd: 0.12, durationMs: 60000, summary: "One cancellation.", items,
    }, harnessed.batch);
    const entity = writes[0]?.entity;
    assert.equal(writes[0]?.mode, "Merge");
    assert.equal(entity?.rowKey, run.runId);
    assert.equal(entity?.finishedAt, "2026-11-28T01:31:00.000Z");
    assert.equal(entity?.outcome, "success");
    assert.equal(entity?.acted, 1);
    assert.equal(entity?.counts, JSON.stringify({ classified: 1 }));
    assert.equal(entity?.summary, "One cancellation.");
    assert.equal("error" in (entity ?? {}), false);
    assert.equal("itemsTruncated" in (entity ?? {}), false);
    const [row] = JSON.parse(String(entity?.items)) as (RunItem & { subject?: string; sender?: string })[];
    assert.equal(row?.action, "classified");
    assert.equal(row?.subject, "Cancel the inspection");
    assert.equal(row?.sender, "sender@principal.test");
  });

  it("cuts items that would not fit the row, and says so", async () => {
    const batch = new Map<string, Candidate>();
    const items: RunItem[] = [];
    for (let index = 0; index < 100; index++) {
      const detail = mail({ subject: "s".repeat(300) });
      batch.set(detail.summary.id, candidate(detail));
      items.push({ messageId: detail.summary.id, action: "left", reason: "r".repeat(500) });
    }
    const { writes, table } = fakeTable();
    await new TableRunStore(table).close(run, new Date(), { ...emptyRun("success"), durationMs: 1, items }, batch);
    const entity = writes[0]?.entity;
    const json = String(entity?.items);
    assert.ok(json.length <= ItemsLimit, `${json.length} characters`);
    assert.equal(entity?.itemsTruncated, true);
    const rows = JSON.parse(json) as (RunItem & { subject?: string })[];
    assert.ok(rows.length > 0 && rows.length < 100);
    assert.equal(rows[0]?.reason?.length, 200);
    assert.equal(rows[0]?.subject?.length, 200);
    assert.equal(rows[0]?.messageId, items[0]?.messageId);
  });

  it("logs a table that refuses the write and goes on, with Pegasus still recorded", async () => {
    const { logs, log } = logged();
    const pegasus = new FixturePegasus({ candidates: [] });
    const refusing: RunTable = { upsertEntity: async () => { throw new Error("This request is not authorized."); } };
    const recorder = new RunRecorder(facts, pegasus, new TableRunStore(refusing), log);
    await recorder.open();
    await recorder.close(emptyRun("nothing eligible"));
    assert.equal(pegasus.runRecords.length, 2);
    assert.deepEqual(
      logs.map((entry) => [entry.message, entry.properties?.stage, entry.properties?.target]),
      [["run record failed", "open", "storage"], ["run record failed", "close", "storage"]]);
  });
});

import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { fingerprint, isCandidate, isEligible } from "../src/eligibility.js";
import type { MailPage, MailSummary } from "../src/pegasus.js";
import { MemoryReviewedStore } from "../src/reviewed.js";
import {
  checkHidden,
  collectBatch,
  HiddenTicksToFail,
  MaxFailedAttempts,
  recordOutcomes,
  TurnsPerEmail,
  turnBudget,
} from "../src/run.js";
import { candidate, harness, mail } from "./support.js";

const quiet = () => {};

describe("eligibility", () => {
  it("lists a classified message whose caseId Pegasus left out as having no Case (#1890)", () => {
    const summary = { ...mail({ outcome: "Classified", category: "triage-request" }).summary } as Partial<MailSummary>;
    delete summary.caseId;
    assert.ok(isCandidate(summary as MailSummary));
  });

  it("does not list a classified message with a Case", () => {
    assert.ok(!isCandidate(mail({ outcome: "Classified", category: "correspondence", caseId: "c0ffee00-0000-4000-8000-000000000001" }).summary));
  });

  it("leaves mail decided by staff, an Automation caller or a Principal", () => {
    assert.ok(isEligible(mail()));
    for (const actor of ["staff:alex", "automation:pegasus-automation", "principal:qdos"]) {
      assert.ok(!isEligible(mail({ actor })), actor);
    }
  });
});

describe("the turn budget (#1918)", () => {
  it("is stated once: eight calls per e-mail beside the list and the summary", () => {
    assert.equal(TurnsPerEmail, 8);
    assert.equal(turnBudget(1), 13);
    assert.equal(turnBudget(20), 165);
  });
});

describe("recordOutcomes (#1915)", () => {
  it("counts a completed run's missing verdict as one attempt and closes the message after the cap", async () => {
    const detail = mail();
    const store = new MemoryReviewedStore();
    for (let run = 1; run <= MaxFailedAttempts; run++) {
      const { batch, outcomes, pegasus } = harness([candidate(detail)]);
      await recordOutcomes(batch, outcomes, true, store, pegasus, quiet);
      const record = await store.get(detail.summary.id);
      assert.equal(record?.attempts, run);
      assert.equal(record?.outcome, run < MaxFailedAttempts ? "retrying" : "failed");
    }
  });

  it("writes nothing for a message left while the lookups were unavailable", async () => {
    const detail = mail();
    const store = new MemoryReviewedStore();
    const run = harness([candidate(detail)]);
    await run.call("search_mailboxes", { query: "QDOS26001" });
    await run.call("leave_email", { messageId: detail.summary.id, reason: "The mailboxes could not be searched." });
    await recordOutcomes(run.batch, run.outcomes, true, store, run.pegasus, quiet);
    assert.equal(await store.get(detail.summary.id), undefined);
  });

  it("marks reviewed a message whose later write in the same run succeeded", async () => {
    const detail = mail();
    const store = new MemoryReviewedStore();
    const run = harness([candidate(detail)]);
    run.outcomes.failed.set(detail.summary.id, "an earlier refusal");
    await run.call("classify_email", { messageId: detail.summary.id, category: "cancellation", reason: "The sender cancels." });
    assert.equal(run.outcomes.failed.size, 0);
    await recordOutcomes(run.batch, run.outcomes, true, store, run.pegasus, quiet);
    assert.equal((await store.get(detail.summary.id))?.outcome, "reviewed");
  });

  it("stores reviewed at what the list row reads after the run's own write", async () => {
    const detail = mail();
    const store = new MemoryReviewedStore();
    const run = harness([candidate(detail)]);
    await run.call("classify_email", { messageId: detail.summary.id, category: "cancellation", reason: "The sender cancels." });
    await recordOutcomes(run.batch, run.outcomes, true, store, run.pegasus, quiet);
    const after = fingerprint((await run.pegasus.get(detail.summary.id)).summary);
    assert.notEqual(after, fingerprint(detail.summary));
    assert.equal((await store.get(detail.summary.id))?.fingerprint, after);
  });

  it("marks a deliberately left message reviewed after one run", async () => {
    const detail = mail({ outcome: "Classified", category: "triage-request" });
    const store = new MemoryReviewedStore();
    const run = harness([candidate(detail)]);
    await run.call("leave_email", { messageId: detail.summary.id, reason: "No registration." });
    await recordOutcomes(run.batch, run.outcomes, true, store, run.pegasus, quiet);
    assert.deepEqual(await store.get(detail.summary.id), { fingerprint: fingerprint(detail.summary), outcome: "reviewed", attempts: 0 });
  });

  it("writes nothing for messages a cut-short run did not reach", async () => {
    const detail = mail();
    const store = new MemoryReviewedStore();
    const { batch, outcomes, pegasus } = harness([candidate(detail)]);
    await recordOutcomes(batch, outcomes, false, store, pegasus, quiet);
    assert.equal(await store.get(detail.summary.id), undefined);
  });
});

describe("the hidden-list warning (#1892)", () => {
  function listing(items: MailSummary[], awaitingDecisionCount: number) {
    const details = new Map(items.map((summary) => [summary.id, mail()]));
    return {
      list: async (): Promise<MailPage> => ({ items, continuation: null, awaitingDecisionCount }),
      get: async (id: string) => details.get(id)!,
      linkDestinations: async () => [],
    };
  }
  const config = { scanPages: 1, batch: 20 };

  it("warns each run the list shows no candidate while Pegasus counts mail awaiting a decision, and resets once one shows", async () => {
    const store = new MemoryReviewedStore();
    const logs: string[] = [];
    const hidden = listing([mail({ outcome: "Classified", category: "correspondence", caseId: "c0ffee00-0000-4000-8000-000000000001" }).summary], 3);
    let ticks = 0;
    for (let run = 1; run <= HiddenTicksToFail; run++) {
      const result = await checkHidden(await collectBatch(config, hidden, store), store, (message) => logs.push(message));
      assert.ok(result.hidden);
      ticks = result.ticks;
    }
    assert.equal(ticks, HiddenTicksToFail);
    assert.equal(logs.filter((message) => message === "warning: candidates hidden").length, HiddenTicksToFail);

    const shown = listing([mail().summary], 3);
    const result = await checkHidden(await collectBatch(config, shown, store), store, quiet);
    assert.deepEqual(result, { hidden: false, ticks: 0 });
    assert.equal(await store.hiddenTicks(), 0);
  });

  it("is quiet when Pegasus counts nothing awaiting a decision", async () => {
    const store = new MemoryReviewedStore();
    const result = await checkHidden(await collectBatch(config, listing([], 0), store), store, quiet);
    assert.deepEqual(result, { hidden: false, ticks: 0 });
  });

  it("does not warn for candidates the store already closed", async () => {
    const store = new MemoryReviewedStore();
    const closed = mail({ outcome: "Classified", category: "triage-request" });
    await store.set(closed.summary.id, { fingerprint: fingerprint(closed.summary), outcome: "reviewed", attempts: 0 });
    const collected = await collectBatch(config, listing([closed.summary], 1), store);
    assert.equal(collected.batch.size, 0);
    assert.equal(collected.candidatesListed, 1);
    assert.equal((await checkHidden(collected, store, quiet)).hidden, false);
  });
});

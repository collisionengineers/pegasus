import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { FixturePegasus } from "../src/fixture-pegasus.js";
import type { MailClassification } from "../src/pegasus.js";
import { appearsIn, AttachmentTextLimit, BodyLimit } from "../src/tools.js";
import { candidate, harness, mail, timedOut } from "./support.js";

describe("classify_email", () => {
  it("counts a timed-out write whose effect shows on the re-read as done (#1963)", async () => {
    const detail = mail();
    class LostAnswer extends FixturePegasus {
      override async correct(args: Parameters<FixturePegasus["correct"]>[0]): Promise<MailClassification> {
        await super.correct(args);
        throw timedOut();
      }
    }
    const pegasus = new LostAnswer({ candidates: [candidate(detail)] });
    const run = harness([candidate(detail)], { pegasus });
    const id = detail.summary.id;

    const first = await run.call("classify_email", { messageId: id, category: "correspondence", reason: "A chaser." });
    assert.equal(first.isError, false);
    assert.equal(first.text, "Classified as correspondence.");
    assert.ok(run.outcomes.verdicts.has(id));
    assert.ok(run.outcomes.written.has(id));
    assert.equal(run.outcomes.failed.size, 0);

    // The run now knows its own write, so a retry reads as already done, not
    // as a change by someone else.
    const retry = await run.call("classify_email", { messageId: id, category: "correspondence", reason: "A chaser." });
    assert.equal(retry.isError, false);
    assert.match(retry.text, /already correspondence; nothing changed/);
  });

  it("reports a write that changed nothing as refused, and counts it failed", async () => {
    const detail = mail();
    class Refusing extends FixturePegasus {
      override async correct(): Promise<MailClassification> {
        throw new Error("The classification has changed since it was read.");
      }
    }
    const run = harness([candidate(detail)], { pegasus: new Refusing({ candidates: [candidate(detail)] }) });
    const result = await run.call("classify_email", { messageId: detail.summary.id, category: "correspondence", reason: "A chaser." });
    assert.equal(result.isError, true);
    assert.match(result.text, /^Pegasus refused the classification/);
    assert.ok(run.outcomes.failed.has(detail.summary.id));
    assert.equal(run.outcomes.verdicts.size, 0);
  });

  it("says it cannot tell when the e-mail changed some other way after a lost answer", async () => {
    const detail = mail();
    class Overtaken extends FixturePegasus {
      override async correct(args: Parameters<FixturePegasus["correct"]>[0]): Promise<MailClassification> {
        await super.correct({ ...args, classificationKey: "cancellation" });
        throw timedOut();
      }
    }
    const run = harness([candidate(detail)], { pegasus: new Overtaken({ candidates: [candidate(detail)] }) });
    const result = await run.call("classify_email", { messageId: detail.summary.id, category: "correspondence", reason: "A chaser." });
    assert.equal(result.isError, true);
    assert.match(result.text, /not known whether the classification was applied/);
    assert.ok(run.outcomes.failed.has(detail.summary.id));
  });

  it("refuses a message staff decided since it was listed", async () => {
    const listed = mail();
    const now = structuredClone(listed);
    now.classification!.currentActor = "staff:alex";
    const run = harness([candidate(listed)], { pegasus: new FixturePegasus({ candidates: [candidate(now)] }) });
    const result = await run.call("classify_email", { messageId: listed.summary.id, category: "correspondence", reason: "A chaser." });
    assert.equal(result.isError, true);
    assert.match(result.text, /Staff have decided this message/);
  });
});

describe("leave_email (#1915)", () => {
  it("is a verdict for an e-mail that already has a type", async () => {
    const detail = mail({ outcome: "Classified", category: "triage-request" });
    const run = harness([candidate(detail)]);
    const result = await run.call("leave_email", { messageId: detail.summary.id, reason: "No registration to open the Triage with." });
    assert.equal(result.isError, false);
    assert.ok(run.outcomes.verdicts.has(detail.summary.id));
  });

  it("refuses an Unclassified e-mail while the lookups answer (R584)", async () => {
    const detail = mail();
    const run = harness([candidate(detail)], { mailboxes: true, folders: true });
    await run.call("search_mailboxes", { query: "QDOS26001" });
    const result = await run.call("leave_email", { messageId: detail.summary.id, reason: "Nothing fits." });
    assert.equal(result.isError, true);
    assert.equal(run.outcomes.verdicts.size, 0);
    assert.equal(run.outcomes.deferred.size, 0);
  });

  it("defers an Unclassified e-mail when a lookup was unavailable", async () => {
    const detail = mail();
    const run = harness([candidate(detail)]);
    const lookup = await run.call("search_mailboxes", { query: "QDOS26001" });
    assert.match(lookup.text, /"state": "unavailable"/);
    const result = await run.call("leave_email", { messageId: detail.summary.id, reason: "The mailboxes could not be searched." });
    assert.equal(result.isError, false);
    assert.ok(run.outcomes.deferred.has(detail.summary.id));
    assert.equal(run.outcomes.verdicts.size, 0);
  });
});

describe("open_triage (#1890)", () => {
  const triageRequest = (subject: string) => mail({ outcome: "Classified", category: "triage-request", subject });

  it("opens the Triage with a registration written in the subject", async () => {
    const detail = triageRequest("Triage request AB12 CDE");
    const run = harness([candidate(detail)]);
    const result = await run.call("open_triage", {
      messageId: detail.summary.id, vehicleRegistration: "ab12cde", source: "subject", reason: "The subject names AB12 CDE.",
    });
    assert.equal(result.isError, false, result.text);
    assert.match(result.text, /^Opened Triage t\.REPLAY-/);
    assert.deepEqual(run.pegasus.writes.map((write) => [write.kind, write.detail]), [["triage", "AB12CDE"]]);
    assert.ok(run.outcomes.verdicts.has(detail.summary.id));
  });

  it("refuses a registration that is not written where the model says", async () => {
    const detail = triageRequest("Triage request XAB12CDE");
    const run = harness([candidate(detail)]);
    const result = await run.call("open_triage", {
      messageId: detail.summary.id, vehicleRegistration: "AB12CDE", source: "subject", reason: "Read it.",
    });
    assert.equal(result.isError, true);
    assert.equal(run.pegasus.writes.length, 0);
    assert.equal(run.outcomes.verdicts.size, 0);
  });

  it("opens from photographs only with the plate Pegasus's reader agreed on", async () => {
    const detail = triageRequest("Triage request");
    const reads = { state: "read" as const, registration: "AB12CDE", readRegistrations: ["AB12CDE"] };
    const run = harness([candidate(detail, { photoReads: reads })]);
    const args = { messageId: detail.summary.id, vehicleRegistration: "AB12CDE", source: "photos", reason: "Pegasus read AB12CDE." };

    const unread = await run.call("open_triage", args);
    assert.equal(unread.isError, true, "the photographs must be read first");

    await run.call("read_photos", { messageId: detail.summary.id });
    const opened = await run.call("open_triage", args);
    assert.equal(opened.isError, false, opened.text);

    const other = await run.call("open_triage", { ...args, vehicleRegistration: "ZZ99ZZZ" });
    assert.equal(other.isError, true);
  });

  it("refuses an e-mail that is not a triage-request", async () => {
    const detail = mail({ subject: "AB12CDE" });
    const run = harness([candidate(detail)]);
    const result = await run.call("open_triage", {
      messageId: detail.summary.id, vehicleRegistration: "AB12CDE", source: "subject", reason: "Read it.",
    });
    assert.equal(result.isError, true);
    assert.match(result.text, /classify it first/);
  });
});

describe("the read tools' bounds", () => {
  it("cuts the body at BodyLimit", async () => {
    const detail = mail({ body: "x".repeat(BodyLimit + 10) });
    const run = harness([candidate(detail)]);
    const result = await run.call("read_email", { messageId: detail.summary.id });
    const body = (JSON.parse(result.text) as { body: string }).body;
    assert.equal(body, `${"x".repeat(BodyLimit)}\n[truncated]`);
  });

  it("reads an attachment's text up to AttachmentTextLimit and refuses an ordinal past the list", async () => {
    const detail = mail({ attachments: [{ fileName: "letter.pdf", mediaType: "application/pdf" }] });
    const run = harness([candidate(detail, { attachmentTexts: ["y".repeat(AttachmentTextLimit + 5)] })]);
    const read = JSON.parse((await run.call("read_attachment", { messageId: detail.summary.id, attachmentOrdinal: 0 })).text) as { text: string; isTruncated: boolean };
    assert.equal(read.text.length, AttachmentTextLimit);
    assert.equal(read.isTruncated, true);
    const past = await run.call("read_attachment", { messageId: detail.summary.id, attachmentOrdinal: 1 });
    assert.equal(past.isError, true);
  });

  it("refuses a message that is not in the batch", async () => {
    const run = harness([candidate(mail())]);
    const result = await run.call("read_email", { messageId: "00000000-0000-4000-8000-000000000000" });
    assert.equal(result.isError, true);
  });
});

describe("appearsIn", () => {
  it("matches a registration as a whole token, spaced or not", () => {
    assert.ok(appearsIn("AB12CDE", "Please triage AB12 CDE today"));
    assert.ok(appearsIn("AB12CDE", "reg: ab12cde."));
    assert.ok(!appearsIn("AB12CDE", "ref XAB12CDE"));
    assert.ok(!appearsIn("AB12CDE", "AB12CDEF"));
    assert.ok(!appearsIn("AB12CDE", null));
  });
});

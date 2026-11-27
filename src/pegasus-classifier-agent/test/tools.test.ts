import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { describe, it } from "node:test";
import { FixturePegasus } from "../src/fixture-pegasus.js";
import type { IntakeDetail, MailClassification } from "../src/pegasus.js";
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

describe("classify_email cancellation (R1333)", () => {
  it("refuses a cancellation on an e-mail with no Case", async () => {
    const detail = mail();
    const run = harness([candidate(detail)]);
    const result = await run.call("classify_email", { messageId: detail.summary.id, category: "cancellation", reason: "The sender cancels." });
    assert.equal(result.isError, true);
    assert.match(result.text, /link that Case first/);
    assert.deepEqual(run.pegasus.writes, []);
  });

  it("gives a cancellation once the e-mail is linked to the Case it cancels", async () => {
    const detail = mail({ caseId: randomUUID() });
    const run = harness([candidate(detail)]);
    const result = await run.call("classify_email", { messageId: detail.summary.id, category: "cancellation", reason: "The sender cancels." });
    assert.equal(result.isError, false, result.text);
    assert.equal(result.text, "Classified as cancellation.");
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
    await run.call("search_mailboxes", { messageId: detail.summary.id, query: "QDOS26001" });
    const result = await run.call("leave_email", { messageId: detail.summary.id, reason: "Nothing fits." });
    assert.equal(result.isError, true);
    assert.equal(run.outcomes.verdicts.size, 0);
    assert.equal(run.outcomes.deferred.size, 0);
  });

  it("defers an Unclassified e-mail when both its lookups were unavailable (R738, R885)", async () => {
    const detail = mail();
    const run = harness([candidate(detail)]);
    const mailboxes = await run.call("search_mailboxes", { messageId: detail.summary.id, query: "QDOS26001" });
    assert.match(mailboxes.text, /"state": "unavailable"/);
    const folders = await run.call("search_old_system_folders", { messageId: detail.summary.id, query: "QDOS26001" });
    assert.match(folders.text, /"state": "unavailable"/);
    const result = await run.call("leave_email", { messageId: detail.summary.id, reason: "Neither lookup could be read." });
    assert.equal(result.isError, false);
    assert.ok(run.outcomes.deferred.has(detail.summary.id));
    assert.equal(run.outcomes.verdicts.size, 0);
  });

  it("refuses to defer when only one lookup was unavailable (#1982)", async () => {
    const detail = mail();
    const run = harness([candidate(detail)], { folders: true });
    await run.call("search_mailboxes", { messageId: detail.summary.id, query: "QDOS26001" });
    await run.call("search_old_system_folders", { messageId: detail.summary.id, query: "QDOS26001" });
    const result = await run.call("leave_email", { messageId: detail.summary.id, reason: "The mailboxes could not be searched." });
    assert.equal(result.isError, true);
    assert.equal(run.outcomes.deferred.size, 0);
  });

  it("refuses to defer an e-mail whose own lookups were never unavailable (#1982)", async () => {
    const searched = mail();
    const other = mail();
    const run = harness([candidate(searched), candidate(other)]);
    await run.call("search_mailboxes", { messageId: searched.summary.id, query: "QDOS26001" });
    await run.call("search_old_system_folders", { messageId: searched.summary.id, query: "QDOS26001" });
    const result = await run.call("leave_email", { messageId: other.summary.id, reason: "The lookups were down." });
    assert.equal(result.isError, true);
    assert.equal(run.outcomes.deferred.size, 0);
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

  it("refuses a registration written only in the quoted history (R1076)", async () => {
    const detail = mail({
      outcome: "Classified", category: "triage-request", subject: "Triage request",
      body: "Please assess the car.\n\nFrom: Desk\nSent: 1 November 2026\nTo: Sender\nSubject: An older job\n\nThe older vehicle was ZZ99 ZZZ.",
      freshBody: "Please assess the car.\n",
    });
    const run = harness([candidate(detail)]);
    const result = await run.call("open_triage", {
      messageId: detail.summary.id, vehicleRegistration: "ZZ99ZZZ", source: "body", reason: "The body names ZZ99 ZZZ.",
    });
    assert.equal(result.isError, true);
    assert.match(result.text, /does not appear in the e-mail's body/);
    assert.equal(run.pegasus.writes.length, 0);
    assert.equal(run.outcomes.verdicts.size, 0);
  });

  it("opens with a registration written in the fresh body or an attachment's text", async () => {
    const inBody = mail({
      outcome: "Classified", category: "triage-request", subject: "Triage request",
      body: "Please assess AB12 CDE.\n\nFrom: Desk\nSent: 1 November 2026\nTo: Sender\nSubject: An older job\n\nThanks.",
      freshBody: "Please assess AB12 CDE.\n",
    });
    const inAttachment = mail({
      outcome: "Classified", category: "triage-request", subject: "Triage request",
      attachments: [{ fileName: "request.pdf", mediaType: "application/pdf" }],
    });
    const run = harness([candidate(inBody), candidate(inAttachment, { attachmentTexts: ["Vehicle: CD34 EFG"] })]);

    const fromBody = await run.call("open_triage", {
      messageId: inBody.summary.id, vehicleRegistration: "AB12CDE", source: "body", reason: "The body names AB12 CDE.",
    });
    assert.equal(fromBody.isError, false, fromBody.text);
    const fromAttachment = await run.call("open_triage", {
      messageId: inAttachment.summary.id, vehicleRegistration: "CD34EFG", source: "attachment", attachmentOrdinal: 0,
      reason: "The attached request names CD34 EFG.",
    });
    assert.equal(fromAttachment.isError, false, fromAttachment.text);
    assert.deepEqual(run.pegasus.writes.map((write) => [write.kind, write.detail]), [["triage", "AB12CDE"], ["triage", "CD34EFG"]]);
  });

  it("refuses a body read when Pegasus gives no fresh body", async () => {
    const detail = mail({ outcome: "Classified", category: "triage-request", subject: "Triage request", body: "Please assess AB12 CDE.", freshBody: null });
    const run = harness([candidate(detail)]);
    const result = await run.call("open_triage", {
      messageId: detail.summary.id, vehicleRegistration: "AB12CDE", source: "body", reason: "The body names AB12 CDE.",
    });
    assert.equal(result.isError, true);
    assert.equal(run.pegasus.writes.length, 0);
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

describe("create_case", () => {
  const facts = { claimantName: "Pat Example", claimNumber: "REF-1", vehicleRegistration: "AB12CDE" };

  /** A new-instruction e-mail with no Case, and what pegasus_intake_get says of its received item. */
  function instruction(intake: Partial<IntakeDetail>, options: { subject?: string; body?: string; dryRun?: boolean } = {}) {
    const receiptId = randomUUID();
    const detail = mail({
      outcome: "Classified", category: "new-instruction", caseType: "Inspection", receiptId,
      ...(options.subject ? { subject: options.subject } : {}),
      ...(options.body ? { body: options.body } : {}),
    });
    const candidates = [candidate(detail)];
    const pegasus = new FixturePegasus({ candidates, intake: { [receiptId]: intake } });
    return { detail, run: harness(candidates, { pegasus, ...(options.dryRun ? { dryRun: true } : {}) }) };
  }

  it("creates the Case when Pegasus suggested no inspection address (R965)", async () => {
    const { detail, run } = instruction({ inspectionAddressState: "Unresolved", inspectionAddressSuggestionFingerprint: null });
    const result = await run.call("create_case", { messageId: detail.summary.id, reason: "The letter instructs an inspection." });
    assert.equal(result.isError, false, result.text);
    assert.match(result.text, /^Created Case REPLAY-/);
    assert.deepEqual(run.pegasus.writes.map((write) => write.kind), ["case"]);
    assert.ok(!run.pegasus.writes[0]?.detail.includes("principalCode"), "an established Principal is never named again");
  });

  it("gives accept the sender's one directory Principal when the item established none (R969)", async () => {
    const { detail, run } = instruction({
      establishedPrincipalCode: null, senderDirectoryPrincipalCode: "ABRAHAMS", draft: null,
      inspectionAddressState: "Unresolved",
    });
    const result = await run.call("create_case", { messageId: detail.summary.id, reason: "The letter instructs an inspection.", ...facts });
    assert.equal(result.isError, false, result.text);
    assert.equal(JSON.parse(run.pegasus.writes[0]?.detail ?? "{}").principalCode, "ABRAHAMS");
  });

  it("takes the type away when Pegasus knows no Principal for it (R1332)", async () => {
    const { detail, run } = instruction({ establishedPrincipalCode: null, senderDirectoryPrincipalCode: null, draft: null });
    const result = await run.call("create_case", { messageId: detail.summary.id, reason: "The letter instructs an inspection.", ...facts });
    assert.equal(result.isError, true);
    assert.match(result.text, /knows no Principal/);
    assert.deepEqual(run.pegasus.writes.map((write) => write.kind), ["classify"]);
    assert.match(run.pegasus.writes[0]?.detail ?? "", /^correspondence: Classifier agent: The letter instructs an inspection\. Pegasus knows no Principal/);
    assert.equal((await run.pegasus.get(detail.summary.id)).classification?.current.category, null);
    assert.ok(run.outcomes.verdicts.has(detail.summary.id));
  });

  it("takes the type away when Pegasus refuses the Case (R1332)", async () => {
    const receiptId = randomUUID();
    const detail = mail({ outcome: "Classified", category: "new-instruction", caseType: "Inspection", receiptId });
    class Refusing extends FixturePegasus {
      override async intakeAction(): Promise<never> {
        throw new Error("Vehicle registration is needed before this item can become a case.");
      }
    }
    const candidates = [candidate(detail)];
    const run = harness(candidates, { pegasus: new Refusing({ candidates, intake: { [receiptId]: {} } }) });
    const result = await run.call("create_case", { messageId: detail.summary.id, reason: "The letter instructs an inspection." });
    assert.equal(result.isError, true);
    assert.match(result.text, /Pegasus refused the Case: Vehicle registration is needed/);
    assert.deepEqual(run.pegasus.writes.map((write) => write.kind), ["classify"]);
    assert.equal((await run.pegasus.get(detail.summary.id)).classification?.current.category, null);
  });

  it("retries a failed allocation once its cause is corrected, rather than accepting again", async () => {
    const receiptId = randomUUID();
    const attemptId = randomUUID();
    const detail = mail({ outcome: "Classified", category: "new-instruction", caseType: "Inspection", receiptId });
    const actions: Record<string, unknown>[] = [];
    class Recording extends FixturePegasus {
      override async intakeAction(args: Parameters<FixturePegasus["intakeAction"]>[0]) {
        actions.push(args);
        return super.intakeAction(args);
      }
    }
    const candidates = [candidate(detail)];
    const pegasus = new Recording({
      candidates,
      intake: { [receiptId]: { allocation: { attemptId, status: "Failed", safeReason: "The selected Principal is not available.", canRetry: true } } },
    });
    const run = harness(candidates, { pegasus });
    const result = await run.call("create_case", { messageId: detail.summary.id, reason: "Park House instructs an inspection." });
    assert.equal(result.isError, false, result.text);
    assert.deepEqual(actions.map((item) => item.action), ["allocate"]);
    assert.equal(actions[0]?.expectedAttemptId, attemptId);
  });

  it("does not hold back a Case for a missing claimant name or claim number (R1331)", async () => {
    const { detail, run } = instruction({
      draft: { claimantName: null, claimNumber: null, vehicleRegistration: "AB12CDE", dateOfIncident: null },
      missingIdentityFields: [],
    });
    const result = await run.call("create_case", { messageId: detail.summary.id, reason: "The letter instructs an inspection." });
    assert.equal(result.isError, false, result.text);
    assert.deepEqual(run.pegasus.writes.map((write) => write.kind), ["case"]);
  });

  describe("a website enquiry (R972)", () => {
    const website = { websiteEnquiry: true, establishedPrincipalCode: null, senderDirectoryPrincipalCode: null, draft: null };
    const enquiry = { subject: "New General Enquiry - Pat Example", body: "Name: Pat Example\nRegistration: MJ15 UCT\nPlease inspect my car." };

    it("makes the private customer's one-off Case and links the e-mail to it", async () => {
      const { detail, run } = instruction(website, enquiry);
      const result = await run.call("create_case", {
        messageId: detail.summary.id, reason: "The enquiry asks for an inspection.", claimantName: "Pat Example", vehicleRegistration: "mj15uct",
      });
      assert.equal(result.isError, false, result.text);
      assert.equal(result.text, "Created one-off Case MJ15UCT and linked the e-mail to it.");
      assert.deepEqual(run.pegasus.writes.map((write) => write.kind), ["one-off", "link"]);
      assert.equal(run.pegasus.writes[0]?.messageId, detail.summary.id);
      assert.equal((await run.pegasus.get(detail.summary.id)).summary.caseReference, "MJ15UCT");
      assert.ok(run.outcomes.written.has(detail.summary.id));
    });

    it("waits for staff with no type when the enquiry gives no registration (R1332)", async () => {
      const { detail, run } = instruction(website, enquiry);
      const result = await run.call("create_case", { messageId: detail.summary.id, reason: "The enquiry asks for an inspection.", claimantName: "Pat Example" });
      assert.equal(result.isError, true);
      assert.match(result.text, /waits for staff/);
      assert.deepEqual(run.pegasus.writes.map((write) => write.kind), ["classify"]);
    });

    it("refuses a name or a registration the enquiry does not write", async () => {
      const { detail, run } = instruction(website, enquiry);
      const wrongPlate = await run.call("create_case", {
        messageId: detail.summary.id, reason: "The enquiry asks for an inspection.", claimantName: "Pat Example", vehicleRegistration: "AB12CDE",
      });
      const wrongName = await run.call("create_case", {
        messageId: detail.summary.id, reason: "The enquiry asks for an inspection.", claimantName: "Sam Other", vehicleRegistration: "MJ15UCT",
      });
      assert.match(wrongPlate.text, /AB12CDE is not written in the enquiry/);
      assert.match(wrongName.text, /Sam Other is not written in the enquiry/);
      assert.deepEqual(run.pegasus.writes, []);
    });

    it("writes nothing in a dry run", async () => {
      const { detail, run } = instruction(website, { ...enquiry, dryRun: true });
      const result = await run.call("create_case", {
        messageId: detail.summary.id, reason: "The enquiry asks for an inspection.", claimantName: "Pat Example", vehicleRegistration: "MJ15 UCT",
      });
      assert.equal(result.text, "Dry run: would create the one-off Case and link the e-mail to it.");
      assert.deepEqual(run.pegasus.writes, []);
    });
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

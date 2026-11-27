import assert from "node:assert/strict";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { describe, it } from "node:test";
import { FileReviewedStore, isClosedAt, reviewedAt } from "../src/reviewed.js";

describe("FileReviewedStore", () => {
  it("keeps a reviewed message and the hidden ticks from one process's run to the next", async () => {
    const folder = await mkdtemp(join(tmpdir(), "reviewed-"));
    try {
      const path = join(folder, "reviewed.json");
      const first = new FileReviewedStore(path);
      assert.equal(await first.get("m1"), undefined);
      assert.equal(await first.hiddenTicks(), 0);
      await first.set("m1", reviewedAt("Classified|new-instruction|"));
      await first.setHiddenTicks(2);

      const next = new FileReviewedStore(path);
      assert.ok(isClosedAt(await next.get("m1"), "Classified|new-instruction|"));
      assert.ok(!isClosedAt(await next.get("m1"), "Unclassified||"));
      assert.equal(await next.hiddenTicks(), 2);
    } finally {
      await rm(folder, { recursive: true, force: true });
    }
  });
});

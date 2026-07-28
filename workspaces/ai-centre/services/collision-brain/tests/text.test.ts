import { describe, expect, it } from "vitest";
import { chunkText, normaliseText } from "../src/domain/text.js";
import { extractText } from "../src/ingestion/extract-text.js";

describe("text preparation", () => {
  it("normalises and chunks content with stable indexes", () => {
    const source = `First paragraph.\r\n\r\n\r\n${"second ".repeat(80)}`;
    const chunks = chunkText(source, { maxCharacters: 120, overlapCharacters: 20 });
    expect(chunks.length).toBeGreaterThan(1);
    expect(chunks.map((chunk) => chunk.index)).toEqual(
      Array.from({ length: chunks.length }, (_, index) => index),
    );
    expect(normaliseText(source)).not.toContain("\r");
  });

  it("extracts visible HTML and removes scripts", async () => {
    const text = await extractText({
      filename: "policy.html",
      contentType: "text/html",
      body: Buffer.from("<body><p>Visible policy</p><script>secret()</script></body>"),
    });
    expect(text).toContain("Visible policy");
    expect(text).not.toContain("secret()");
  });
});

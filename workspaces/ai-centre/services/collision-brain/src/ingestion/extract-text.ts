import path from "node:path";
import { load } from "cheerio";
import mammoth from "mammoth";
import { ValidationError } from "../domain/errors.js";
import { normaliseText } from "../domain/text.js";
import type { StoredObject } from "../ports/object-store.js";

const plainExtensions = new Set([".txt", ".md", ".markdown"]);
const htmlExtensions = new Set([".html", ".htm"]);

export const supportedExtensions = [
  ".txt",
  ".md",
  ".markdown",
  ".html",
  ".htm",
  ".pdf",
  ".docx",
] as const;

export function isSupportedFilename(filename: string): boolean {
  return supportedExtensions.includes(
    path.extname(filename).toLowerCase() as (typeof supportedExtensions)[number],
  );
}

async function extractPdf(buffer: Buffer): Promise<string> {
  const module = await import("pdf-parse");
  if ("PDFParse" in module) {
    const parser = new module.PDFParse({ data: buffer });
    try {
      const result = await parser.getText();
      return result.text;
    } finally {
      await parser.destroy();
    }
  }

  const legacy = (module as unknown as {
    default?: (value: Buffer) => Promise<{ text: string }>;
  }).default;
  if (!legacy) throw new Error("Installed pdf-parse package has no supported parser API");
  return (await legacy(buffer)).text;
}

export async function extractText(object: StoredObject): Promise<string> {
  const extension = path.extname(object.filename).toLowerCase();
  let result: string;

  if (plainExtensions.has(extension) ||
      object.contentType.startsWith("text/plain") ||
      object.contentType === "text/markdown") {
    result = object.body.toString("utf8");
  } else if (htmlExtensions.has(extension) || object.contentType === "text/html") {
    const document = load(object.body.toString("utf8"));
    document("script, style, noscript").remove();
    result = document("body").text() || document.root().text();
  } else if (extension === ".docx" ||
      object.contentType === "application/vnd.openxmlformats-officedocument.wordprocessingml.document") {
    result = (await mammoth.extractRawText({ buffer: object.body })).value;
  } else if (extension === ".pdf" || object.contentType === "application/pdf") {
    result = await extractPdf(object.body);
  } else {
    throw new ValidationError(`Unsupported file type for ${object.filename}`);
  }

  const normalised = normaliseText(result);
  if (!normalised) {
    throw new ValidationError(
      `No text could be extracted from ${object.filename}; scanned documents require deferred OCR support`,
    );
  }
  return normalised;
}

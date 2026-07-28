const wordPattern = /[\p{L}\p{N}]+/gu;

export function normaliseText(value: string): string {
  return value
    .replace(/\u0000/g, "")
    .replace(/\r\n?/g, "\n")
    .replace(/[ \t]+\n/g, "\n")
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}

export function tokenise(value: string): string[] {
  return (normaliseText(value).toLowerCase().match(wordPattern) ?? []).filter(
    (token) => token.length > 1,
  );
}

export function lexicalScore(query: string, text: string): number {
  const queryTokens = new Set(tokenise(query));
  if (queryTokens.size === 0) return 0;
  const textTokens = new Set(tokenise(text));
  let matches = 0;
  for (const token of queryTokens) {
    if (textTokens.has(token)) matches += 1;
  }
  return matches / queryTokens.size;
}

export interface TextChunk {
  index: number;
  text: string;
}

export function chunkText(
  value: string,
  options: { maxCharacters?: number; overlapCharacters?: number } = {},
): TextChunk[] {
  const text = normaliseText(value);
  if (!text) return [];

  const maxCharacters = options.maxCharacters ?? 1600;
  const overlapCharacters = Math.min(options.overlapCharacters ?? 240, maxCharacters / 2);
  const paragraphs = text.split(/\n{2,}/);
  const chunks: TextChunk[] = [];
  let current = "";

  const pushCurrent = () => {
    const candidate = normaliseText(current);
    if (!candidate) return;
    chunks.push({ index: chunks.length, text: candidate });
    current = candidate.slice(Math.max(0, candidate.length - overlapCharacters));
  };

  for (const paragraph of paragraphs) {
    if (paragraph.length > maxCharacters) {
      if (current) pushCurrent();
      for (let start = 0; start < paragraph.length; start += maxCharacters - overlapCharacters) {
        const segment = paragraph.slice(start, start + maxCharacters);
        chunks.push({ index: chunks.length, text: normaliseText(segment) });
        if (start + maxCharacters >= paragraph.length) break;
      }
      current = "";
      continue;
    }

    const joined = current ? `${current}\n\n${paragraph}` : paragraph;
    if (joined.length > maxCharacters && current) pushCurrent();
    current = current ? `${current}\n\n${paragraph}` : paragraph;
  }

  if (current) {
    const candidate = normaliseText(current);
    if (candidate && chunks.at(-1)?.text !== candidate) {
      chunks.push({ index: chunks.length, text: candidate });
    }
  }

  return chunks;
}

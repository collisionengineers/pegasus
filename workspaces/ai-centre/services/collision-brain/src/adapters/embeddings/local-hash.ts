import { tokenise } from "../../domain/text.js";
import { normaliseVector } from "../../domain/vector.js";
import type { EmbeddingProvider } from "../../ports/embedding-provider.js";

function fnv1a(value: string): number {
  let hash = 0x811c9dc5;
  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193);
  }
  return hash >>> 0;
}

export class LocalHashEmbeddingProvider implements EmbeddingProvider {
  readonly descriptor;

  constructor(private readonly dimensions: number) {
    this.descriptor = {
      provider: "local",
      model: "feature-hash",
      dimensions,
      version: "1",
    };
  }

  async embed(texts: string[]): Promise<number[][]> {
    return texts.map((text) => {
      const vector = Array.from({ length: this.dimensions }, () => 0);
      const tokens = tokenise(text);

      tokens.forEach((token, position) => {
        const unigram = fnv1a(token);
        const unigramIndex = unigram % this.dimensions;
        vector[unigramIndex] = (vector[unigramIndex] ?? 0) + (unigram & 1 ? 1 : -1);

        const next = tokens[position + 1];
        if (next) {
          const bigram = fnv1a(`${token}:${next}`);
          const bigramIndex = bigram % this.dimensions;
          vector[bigramIndex] = (vector[bigramIndex] ?? 0) + (bigram & 1 ? 0.5 : -0.5);
        }
      });

      return normaliseVector(vector);
    });
  }
}

import type { EmbeddingDescriptor } from "../domain/types.js";

export interface EmbeddingProvider {
  readonly descriptor: EmbeddingDescriptor;
  embed(texts: string[]): Promise<number[][]>;
}

// The messages the agent has already looked at, so it does not pay to read
// mail it left alone again on every run. Azure keeps them in the `reviewed`
// table in the classifier's own storage account; a local run keeps them in
// memory.

import { TableClient } from "@azure/data-tables";
import { ManagedIdentityCredential } from "@azure/identity";

export interface ReviewedStore {
  get(messageId: string): Promise<string | undefined>;
  set(messageId: string, fingerprint: string): Promise<void>;
}

const Partition = "mail";

export class TableReviewedStore implements ReviewedStore {
  private readonly table: TableClient;

  constructor(storageAccount: string) {
    this.table = new TableClient(
      `https://${storageAccount}.table.core.windows.net`,
      "reviewed",
      new ManagedIdentityCredential({ clientId: process.env.AZURE_CLIENT_ID }));
  }

  async get(messageId: string): Promise<string | undefined> {
    try {
      const entity = await this.table.getEntity<{ fingerprint: string }>(Partition, messageId);
      return entity.fingerprint;
    } catch (error) {
      if ((error as { statusCode?: number }).statusCode === 404) {
        return undefined;
      }
      throw error;
    }
  }

  async set(messageId: string, fingerprint: string): Promise<void> {
    await this.table.upsertEntity({ partitionKey: Partition, rowKey: messageId, fingerprint }, "Replace");
  }
}

export class MemoryReviewedStore implements ReviewedStore {
  private readonly entries = new Map<string, string>();

  async get(messageId: string): Promise<string | undefined> {
    return this.entries.get(messageId);
  }

  async set(messageId: string, fingerprint: string): Promise<void> {
    this.entries.set(messageId, fingerprint);
  }
}

// The messages the agent has already looked at, so it does not pay to read
// mail it left alone again on every run. Azure keeps them in the `reviewed`
// table in the classifier's own storage account; a local run keeps them in
// memory.
//
// One row per message, replaced whole on every write:
//   fingerprint  what the message's list row said when the agent last looked
//   outcome      reviewed: looked at, and dealt with or deliberately left
//                failed:   no verdict in MaxFailedAttempts runs; given up
//                          until the message changes
//                retrying: no verdict yet; looked at again next run
//   attempts     runs in which a write to the message failed, or that left
//                it without a verdict, at that fingerprint
//
// One more row, partition meta, row hidden-ticks: how many runs in a row
// listed no candidate while Pegasus counted mail awaiting a decision (#1892).

import { TableClient } from "@azure/data-tables";
import { ManagedIdentityCredential } from "@azure/identity";

export type ReviewedOutcome = "reviewed" | "failed" | "retrying";

export interface ReviewedRecord {
  fingerprint: string;
  outcome: ReviewedOutcome;
  attempts: number;
}

export interface ReviewedStore {
  get(messageId: string): Promise<ReviewedRecord | undefined>;
  set(messageId: string, record: ReviewedRecord): Promise<void>;
  /** Consecutive runs that found the list hiding mail; 0 when none. */
  hiddenTicks(): Promise<number>;
  setHiddenTicks(ticks: number): Promise<void>;
}

/** Looked at, with nothing left to do, and the message reads the same now. */
export function reviewedAt(fingerprint: string): ReviewedRecord {
  return { fingerprint, outcome: "reviewed", attempts: 0 };
}

/** A message is skipped only if it was closed at the fingerprint it still has. */
export function isClosedAt(record: ReviewedRecord | undefined, fingerprint: string): boolean {
  return record !== undefined && record.fingerprint === fingerprint && record.outcome !== "retrying";
}

/** Runs already spent failing to write to a message that still reads this way. */
export function attemptsAt(record: ReviewedRecord | undefined, fingerprint: string): number {
  return record !== undefined && record.fingerprint === fingerprint ? record.attempts : 0;
}

const Partition = "mail";
const MetaPartition = "meta";
const HiddenTicksRow = "hidden-ticks";

export class TableReviewedStore implements ReviewedStore {
  private readonly table: TableClient;

  constructor(storageAccount: string) {
    this.table = new TableClient(
      `https://${storageAccount}.table.core.windows.net`,
      "reviewed",
      new ManagedIdentityCredential({ clientId: process.env.AZURE_CLIENT_ID }));
  }

  async get(messageId: string): Promise<ReviewedRecord | undefined> {
    try {
      const entity = await this.table.getEntity<ReviewedRecord>(Partition, messageId);
      return { fingerprint: entity.fingerprint, outcome: entity.outcome, attempts: entity.attempts };
    } catch (error) {
      if ((error as { statusCode?: number }).statusCode === 404) {
        return undefined;
      }
      throw error;
    }
  }

  async set(messageId: string, record: ReviewedRecord): Promise<void> {
    await this.table.upsertEntity({ partitionKey: Partition, rowKey: messageId, ...record }, "Replace");
  }

  async hiddenTicks(): Promise<number> {
    try {
      const entity = await this.table.getEntity<{ ticks: number }>(MetaPartition, HiddenTicksRow);
      return entity.ticks;
    } catch (error) {
      if ((error as { statusCode?: number }).statusCode === 404) {
        return 0;
      }
      throw error;
    }
  }

  async setHiddenTicks(ticks: number): Promise<void> {
    await this.table.upsertEntity({ partitionKey: MetaPartition, rowKey: HiddenTicksRow, ticks }, "Replace");
  }
}

export class MemoryReviewedStore implements ReviewedStore {
  private readonly entries = new Map<string, ReviewedRecord>();

  async get(messageId: string): Promise<ReviewedRecord | undefined> {
    return this.entries.get(messageId);
  }

  async set(messageId: string, record: ReviewedRecord): Promise<void> {
    this.entries.set(messageId, { ...record });
  }

  private ticks = 0;

  async hiddenTicks(): Promise<number> {
    return this.ticks;
  }

  async setHiddenTicks(ticks: number): Promise<void> {
    this.ticks = ticks;
  }
}

// Settings read once per run. Azure supplies them as app settings (Key Vault
// references for the two secrets); a local run reads .env.

export interface ClassifierConfig {
  anthropicApiKey: string;
  mcpUrl: string;
  tokenUrl: string;
  clientId: string;
  clientSecret: string;
  model: string;
  dryRun: boolean;
  batch: number;
  scanPages: number;
  maxBudgetUsd: number;
  storageAccount: string | undefined;
}

function required(name: string): string {
  const value = process.env[name]?.trim();
  if (!value) {
    throw new Error(`${name} is not set.`);
  }
  return value;
}

function positive(name: string, fallback: number): number {
  const raw = process.env[name]?.trim();
  if (!raw) {
    return fallback;
  }
  const value = Number(raw);
  if (!Number.isFinite(value) || value <= 0) {
    throw new Error(`${name} must be a positive number.`);
  }
  return value;
}

export function readConfig(): ClassifierConfig {
  return {
    anthropicApiKey: required("ANTHROPIC_API_KEY"),
    mcpUrl: required("PEGASUS_MCP_URL"),
    tokenUrl: required("PEGASUS_TOKEN_URL"),
    clientId: required("PEGASUS_CLIENT_ID"),
    clientSecret: required("PEGASUS_CLIENT_SECRET"),
    model: process.env.CLASSIFIER_MODEL?.trim() || "claude-sonnet-5-5",
    dryRun: process.env.CLASSIFIER_DRY_RUN?.trim().toLowerCase() === "true",
    batch: Math.floor(positive("CLASSIFIER_BATCH", 20)),
    scanPages: Math.floor(positive("CLASSIFIER_SCAN_PAGES", 10)),
    maxBudgetUsd: positive("CLASSIFIER_MAX_BUDGET_USD", 2),
    storageAccount: process.env.CLASSIFIER_STORAGE_ACCOUNT?.trim() || undefined,
  };
}

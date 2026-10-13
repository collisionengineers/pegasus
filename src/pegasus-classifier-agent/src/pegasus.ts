// The Pegasus Automation MCP server, reached as the pegasus-automation client
// with only the automation.mail scope. Tokens last ten minutes, so each request
// carries a token refreshed shortly before it expires.

import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StreamableHTTPClientTransport } from "@modelcontextprotocol/sdk/client/streamableHttp.js";
import type { ClassifierConfig } from "./config.js";

export interface ClassificationResult {
  outcome: string;
  category: string | null;
  reason: string;
  caseType: string | null;
}

export interface MailSummary {
  id: string;
  senderAddress: string | null;
  senderDisplayName: string | null;
  subject: string | null;
  receivedAtUtc: string;
  attachmentCount: number;
  caseId: string | null;
  caseReference: string | null;
  isTriageCase: boolean;
  classification: ClassificationResult | null;
}

export interface MailPage {
  items: MailSummary[];
  continuation: string | null;
}

export interface MailDetail {
  summary: MailSummary;
  toAddresses: string[];
  ccAddresses: string[];
  bodyPlainText: string | null;
  attachments: { fileName: string; mediaType: string; contentLength: number }[];
  thread: { senderAddress: string | null; subject: string | null; receivedAtUtc: string }[];
  classification: {
    version: number;
    current: ClassificationResult;
    currentActor: string;
  } | null;
  intakeVersion: number | null;
  canLinkCase: boolean;
}

export interface LinkDestination {
  caseId: string;
  reference: string;
  registration: string | null;
  claimant: string | null;
  principal: string | null;
  state: string | null;
  triageState: string | null;
  isTriageCase: boolean;
  version: number;
}

const Scope = "automation.mail";

export class PegasusMail {
  private token: { value: string; expiresAt: number } | undefined;
  private client: Client | undefined;

  constructor(private readonly config: ClassifierConfig) {}

  async connect(): Promise<void> {
    const transport = new StreamableHTTPClientTransport(new URL(this.config.mcpUrl), {
      fetch: async (url, init) => {
        const headers = new Headers(init?.headers);
        headers.set("Authorization", `Bearer ${await this.accessToken()}`);
        return fetch(url, { ...init, headers });
      },
    });
    this.client = new Client({ name: "pegasus-classifier-agent", version: "1.0.0" });
    await this.client.connect(transport);
  }

  async close(): Promise<void> {
    await this.client?.close();
  }

  list(continuation: string | null): Promise<MailPage> {
    return this.call("pegasus_mail_list", {
      folder: "inbox",
      pageSize: 100,
      ...(continuation ? { continuation } : {}),
    });
  }

  get(messageId: string): Promise<MailDetail> {
    return this.call("pegasus_mail_get", { messageId });
  }

  async linkDestinations(messageId: string): Promise<LinkDestination[]> {
    const result = await this.call<{ items: LinkDestination[] }>(
      "pegasus_mail_link_destinations",
      { messageId });
    return result.items;
  }

  correct(args: {
    messageId: string;
    expectedClassificationVersion: number;
    classificationKey: string;
    workType?: string;
    reason: string;
    operationKey: string;
  }): Promise<unknown> {
    return this.call("pegasus_mail_correct_classification", args);
  }

  link(args: {
    messageId: string;
    caseId: string;
    expectedIntakeVersion: number;
    expectedCaseVersion: number;
    reason: string;
    operationKey: string;
  }): Promise<MailDetail> {
    return this.call("pegasus_mail_link_case", args);
  }

  private async call<T>(name: string, args: Record<string, unknown>): Promise<T> {
    if (!this.client) {
      throw new Error("Pegasus is not connected.");
    }
    const result = await this.client.callTool({ name, arguments: args });
    if (result.isError) {
      const content = Array.isArray(result.content) ? result.content : [];
      const text = content
        .map((block) => (block.type === "text" ? block.text : ""))
        .join(" ")
        .trim();
      throw new Error(text || `${name} failed.`);
    }
    return result.structuredContent as T;
  }

  private async accessToken(): Promise<string> {
    if (this.token && this.token.expiresAt > Date.now() + 60_000) {
      return this.token.value;
    }
    const response = await fetch(this.config.tokenUrl, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: new URLSearchParams({
        grant_type: "client_credentials",
        client_id: this.config.clientId,
        client_secret: this.config.clientSecret,
        scope: Scope,
      }),
    });
    if (!response.ok) {
      throw new Error(`The Pegasus token request failed with ${response.status}.`);
    }
    const body = (await response.json()) as { access_token: string; expires_in: number };
    this.token = { value: body.access_token, expiresAt: Date.now() + body.expires_in * 1000 };
    return this.token.value;
  }
}

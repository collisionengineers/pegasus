import type { AddressInfo } from "node:net";
import { afterEach, describe, expect, it } from "vitest";
import { loadConfig } from "../src/config.js";
import { createApplicationContext, type ApplicationContext } from "../src/context.js";
import { createHttpApp } from "../src/http-app.js";

describe("HTTP upload API", () => {
  const contexts: ApplicationContext[] = [];
  const servers: Array<{ close(callback?: (error?: Error) => void): void }> = [];

  afterEach(async () => {
    await Promise.all(servers.splice(0).map((server) =>
      new Promise<void>((resolve, reject) => server.close((error) => error ? reject(error) : resolve()))));
    await Promise.all(contexts.splice(0).map((context) => context.close()));
  });

  it("stages an authenticated supported file and returns an upload reference", async () => {
    const config = loadConfig({
      NODE_ENV: "test",
      REPOSITORY_DRIVER: "memory",
      OBJECT_STORE_DRIVER: "memory",
      AUTH_MODE: "none",
      UPLOAD_TOKEN_SECRET: "http-test-secret",
    });
    const context = await createApplicationContext(config);
    contexts.push(context);
    const server = createHttpApp(context).listen(0);
    servers.push(server);
    await new Promise<void>((resolve) => server.once("listening", resolve));
    const port = (server.address() as AddressInfo).port;

    const form = new FormData();
    form.append("file", new Blob(["portable knowledge"], { type: "text/plain" }), "note.txt");
    const response = await fetch(`http://127.0.0.1:${port}/uploads`, {
      method: "POST",
      body: form,
    });
    expect(response.status).toBe(201);
    expect(await response.json()).toMatchObject({
      upload_ref: expect.any(String),
      content_hash: expect.stringMatching(/^[a-f0-9]{64}$/),
      expires_at: expect.any(String),
    });
  });
});

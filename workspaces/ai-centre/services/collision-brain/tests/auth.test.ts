import { describe, expect, it } from "vitest";
import { SharedSecretAuthProvider } from "../src/adapters/auth/shared-secret.js";

describe("SharedSecretAuthProvider", () => {
  it("accepts only the exact bearer secret", async () => {
    const provider = new SharedSecretAuthProvider("correct-secret");
    expect(await provider.authenticate("Bearer correct-secret")).toMatchObject({
      subject: "shared-secret-client",
    });
    expect(await provider.authenticate("Bearer wrong-secret")).toBeNull();
    expect(await provider.authenticate(undefined)).toBeNull();
  });
});

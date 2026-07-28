import { describe, expect, it, vi } from "vitest";
import { OidcAuthProvider } from "../src/adapters/auth/oidc.js";
import { SharedSecretAuthProvider } from "../src/adapters/auth/shared-secret.js";


const { jwtVerifyMock } = vi.hoisted(() => ({
  jwtVerifyMock: vi.fn(),
}));

vi.mock("jose", () => ({
  createRemoteJWKSet: () => vi.fn(),
  jwtVerify: jwtVerifyMock,
}));
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

describe("OidcAuthProvider", () => {
  it("passes the configured issuer to JWT verification without normalization", async () => {
    jwtVerifyMock.mockResolvedValueOnce({
      payload: { sub: "oidc-user", roles: ["reader"] },
    });
    const provider = new OidcAuthProvider(
      "https://issuer.example/",
      "collision-brain",
      "https://issuer.example/.well-known/jwks.json",
    );

    await expect(provider.authenticate("Bearer token")).resolves.toMatchObject({
      subject: "oidc-user",
    });
    expect(jwtVerifyMock).toHaveBeenCalledWith(
      "token",
      expect.any(Function),
      expect.objectContaining({ issuer: "https://issuer.example/" }),
    );
  });
});

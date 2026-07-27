import { createHash, timingSafeEqual } from "node:crypto";
import type { Principal } from "../../domain/types.js";
import type { AuthProvider } from "../../ports/auth-provider.js";

function bearerValue(header: string | undefined): string | null {
  return header?.match(/^Bearer\s+(.+)$/i)?.[1] ?? null;
}

export class SharedSecretAuthProvider implements AuthProvider {
  private readonly expected: Buffer;

  constructor(secret: string) {
    this.expected = createHash("sha256").update(secret, "utf8").digest();
  }

  async authenticate(authorizationHeader: string | undefined): Promise<Principal | null> {
    const suppliedValue = bearerValue(authorizationHeader);
    if (!suppliedValue) return null;
    const supplied = createHash("sha256").update(suppliedValue, "utf8").digest();
    if (!timingSafeEqual(supplied, this.expected)) return null;
    return {
      subject: "shared-secret-client",
      roles: ["reader", "contributor", "admin"],
    };
  }
}

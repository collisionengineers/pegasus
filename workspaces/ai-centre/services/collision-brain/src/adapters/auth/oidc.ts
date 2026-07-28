import { createRemoteJWKSet, jwtVerify } from "jose";
import type { Role } from "../../domain/types.js";
import type { AuthProvider } from "../../ports/auth-provider.js";

const allowedRoles = new Set<Role>(["reader", "contributor", "admin"]);

export class OidcAuthProvider implements AuthProvider {
  private readonly jwks;
  private readonly issuer: string;

  constructor(
    issuer: string,
    private readonly audience: string,
    jwksUrl: string,
  ) {
    this.issuer = issuer;
    this.jwks = createRemoteJWKSet(new URL(jwksUrl));
  }

  async authenticate(authorizationHeader: string | undefined) {
    const token = authorizationHeader?.match(/^Bearer\s+(.+)$/i)?.[1];
    if (!token) return null;

    try {
      const { payload } = await jwtVerify(token, this.jwks, {
        issuer: this.issuer,
        audience: this.audience,
      });
      if (!payload.sub) return null;
      const claimRoles = Array.isArray(payload.roles)
        ? payload.roles.filter((role): role is Role =>
          typeof role === "string" && allowedRoles.has(role as Role))
        : [];
      const roles: Role[] = claimRoles.length > 0 ? claimRoles : ["reader"];
      return { subject: payload.sub, roles };
    } catch {
      return null;
    }
  }
}

import type { NextFunction, Request, Response } from "express";
import { NoneAuthProvider } from "./adapters/auth/none.js";
import { OidcAuthProvider } from "./adapters/auth/oidc.js";
import { SharedSecretAuthProvider } from "./adapters/auth/shared-secret.js";
import type { AppConfig } from "./config.js";
import { ForbiddenError, UnauthorizedError } from "./domain/errors.js";
import type { Principal, Role } from "./domain/types.js";
import type { AuthProvider } from "./ports/auth-provider.js";

export interface AuthenticatedRequest extends Request {
  principal?: Principal;
}

export function createAuthProvider(config: AppConfig): AuthProvider {
  if (config.authMode === "none") return new NoneAuthProvider();
  if (config.authMode === "shared-secret") {
    return new SharedSecretAuthProvider(config.sharedSecret as string);
  }
  return new OidcAuthProvider(
    config.oidcIssuer as string,
    config.oidcAudience as string,
    config.oidcJwksUrl as string,
  );
}

export function requireAuthentication(provider: AuthProvider) {
  return async (request: AuthenticatedRequest, response: Response, next: NextFunction) => {
    try {
      const principal = await provider.authenticate(request.header("authorization"));
      if (!principal) throw new UnauthorizedError();
      request.principal = principal;
      next();
    } catch (error) {
      if (error instanceof UnauthorizedError) {
        response.status(error.statusCode).json({ error: error.code, message: error.message });
        return;
      }
      next(error);
    }
  };
}

export function requireRole(principal: Principal, role: Role): void {
  if (principal.roles.includes("admin")) return;
  if (!principal.roles.includes(role)) throw new ForbiddenError();
}

import type { AuthProvider } from "../../ports/auth-provider.js";
import type { Principal } from "../../domain/types.js";

export class NoneAuthProvider implements AuthProvider {
  async authenticate(_authorizationHeader: string | undefined): Promise<Principal> {
    return {
      subject: "local-development",
      roles: ["reader", "contributor", "admin"],
    };
  }
}

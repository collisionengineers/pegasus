import type { Principal } from "../domain/types.js";

export interface AuthProvider {
  authenticate(authorizationHeader: string | undefined): Promise<Principal | null>;
}

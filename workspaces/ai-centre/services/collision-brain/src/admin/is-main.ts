import { pathToFileURL } from "node:url";

export function isMain(importMetaUrl: string): boolean {
  const entrypoint = process.argv[1];
  return Boolean(entrypoint) && pathToFileURL(entrypoint as string).href === importMetaUrl;
}

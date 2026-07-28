import { ValidationError } from "../domain/errors.js";

export function requiredArgument(name: string): string {
  const index = process.argv.indexOf(name);
  const value = index >= 0 ? process.argv[index + 1] : undefined;
  if (!value) throw new ValidationError(`${name} is required`);
  return value;
}

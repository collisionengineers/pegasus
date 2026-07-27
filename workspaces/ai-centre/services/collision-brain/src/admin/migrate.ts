import { readdir, readFile } from "node:fs/promises";
import path from "node:path";
import { Pool } from "pg";
import { loadConfig } from "../config.js";
import { isMain } from "./is-main.js";

export async function migrate(connectionString: string, directory = "migrations"): Promise<void> {
  const pool = new Pool({ connectionString });
  try {
    await pool.query(`
      CREATE TABLE IF NOT EXISTS schema_migrations (
        name text PRIMARY KEY,
        applied_at timestamptz NOT NULL DEFAULT now()
      )
    `);
    const files = (await readdir(directory))
      .filter((name) => name.endsWith(".sql"))
      .sort();
    for (const name of files) {
      const existing = await pool.query(
        "SELECT 1 FROM schema_migrations WHERE name = $1",
        [name],
      );
      if (existing.rowCount) continue;
      const sql = await readFile(path.join(directory, name), "utf8");
      const client = await pool.connect();
      try {
        await client.query("BEGIN");
        await client.query(sql);
        await client.query("INSERT INTO schema_migrations (name) VALUES ($1)", [name]);
        await client.query("COMMIT");
        process.stderr.write(`Applied migration ${name}\n`);
      } catch (error) {
        await client.query("ROLLBACK");
        throw error;
      } finally {
        client.release();
      }
    }
  } finally {
    await pool.end();
  }
}

if (isMain(import.meta.url)) {
  const config = loadConfig();
  await migrate(config.databaseUrl);
}

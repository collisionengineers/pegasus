# Optional brief overlay — data migration

Copy this prompt set into the brief when the ticket changes persistent data or schema.

- **Up and down:** define the forward migration and a tested rollback or explain why rollback is impossible.
- **Backfill:** state the data population/transform step, its batching or resumability, and its completion signal.
- **Runtime-role permission test:** prove the application identity—not only an administrator—can execute the changed path.
- **Grants travel with the diff:** include schema permissions, bootstrap, and migration changes together.
- **Data-loss analysis:** name destructive risk, backup/restore assumptions, and the decision point before irreversible work.

# 0053: Shipped migrations are never edited; the idempotent script is fixed in the generator

- Status: Accepted
- Date: 2026-11-13

## Context

`dotnet ef migrations script --idempotent` wraps every migration in
`IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = ...) BEGIN ... END`. EF Core runs its own
risky statements there through `EXEC(N'...')`, but it copies `migrationBuilder.Sql(...)` text as written. Migration
`20261102223854_Week3SchemaCompliance` uses raw SQL for two things, and both break the script:

1. `CREATE OR ALTER PROCEDURE` must be the first statement of a batch, so it cannot sit inside `BEGIN ... END`.
2. The staging copy reads `CounterTransactions` columns that the same migration drops. When the script runs again
   on an up-to-date database, SQL Server compiles the whole batch, so the statement fails even though the `IF` would
   skip it.

To fix this, the raw SQL calls inside that migration were wrapped in a helper (`StoredProcedureSql.Exec`). The
behaviour was the same, but a migration that had already shipped (and was already applied to databases) had been
edited. A migration is a record of a change that was applied. Editing it means the code no longer shows what ran,
and a team member who already applied the old version gets a different script from the same migration ID.

## Decision

- **Migrations that have shipped are never edited.** `Week3SchemaCompliance.cs` and `StoredProcedureSql.cs` are
  restored byte for byte to the version that shipped (commit `55c658f`). Any future schema or procedure change goes
  in a new migration (for a procedure: a new migration that runs `CREATE OR ALTER`).
- **Script generation is fixed where the problem is: the SQL generator.**
  `IdempotentScriptMigrationsSqlGenerator` derives from `SqlServerMigrationsSqlGenerator`. Only when the idempotent
  option is set does it wrap each raw SQL batch (split on `GO`) in `EXEC(N'...')`, which compiles the batch on its
  own at run time. `ApexPostDbContext.OnConfiguring` registers it with `ReplaceService`, so the Web app, the API, the
  design-time factory and the tests all use it.
- **No new migration is added.** Nothing in the database changes: the migration ID, its operations and the
  `__EFMigrationsHistory` rows stay the same. A new migration would be empty.

## Consequences

- `Database.Migrate()`, `dotnet ef database update` and non-idempotent scripts generate exactly what they did when
  the migration shipped. A database that already applied it sees no difference.
- The idempotent script `sql/ApexPostDb_schema.sql` is unchanged. The generator emits the same `EXEC(N'...')` text
  that the helper used to produce, and it can still be run twice
  (`Idempotent_schema_script_builds_an_empty_database_and_is_safe_to_run_twice`).
- Every future `migrationBuilder.Sql(...)` call is safe in the idempotent script without anyone remembering a helper.
- Tests in `MigrationScriptTests` (no database needed) prove it:
  - the context uses the generator;
  - the idempotent script runs the Week 3 procedures and the staging copy through `EXEC`;
  - the normal script creates the procedures as plain batches;
  - `GO`-separated batches each get their own `EXEC`;
  - the committed schema script matches what the migrations generate.

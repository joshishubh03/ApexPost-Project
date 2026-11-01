# 0037: Local SQL Server runtime validation

- Status: Accepted
- Date: 2026-11-01

## Context

The SQL Server persistence work is not considered complete until it is exercised against an actual database engine, not just a compile-time model or static migration script. This project must protect against a common failure mode where code compiles and migrations generate clean SQL, but the database objects and procedure APIs do not actually match the runtime schema or execution semantics.

The repository has a practical requirement to keep the application safe for local development without a configured database. At the same time, it must also support a live validation path using the local SQL Server instance available in the training environment.

## Decision

We treat real database validation as a required engineering step for the Week 3 work:

- The app reads `ConnectionStrings:ApexPostDb` or `APEXPOST_DB_CONNECTION` when present.
- Local validation uses a disposable database and a trusted local server connection, for example:

  `Server=localhost;Database=ApexPostDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true`

- EF Core migrations are applied to a real database before stored-procedure validation.
- Stored procedures are then tested against a disposable database created in `master` and dropped afterward to avoid polluting the development database.
- We prefer a disposable database for integration tests because it proves the procedure contracts and runtime SQL are correct without depending on a shared training instance.

## Why this is the right approach

1. Runtime validation catches schema drift that static migration generation can miss.
2. Stored-procedure execution semantics are sensitive to table names, data types, and parameter binding; they must be validated at runtime.
3. A disposable database keeps tests hermetic while still proving the code works on SQL Server and not only in memory or a mocked layer.
4. The validation strategy remains consistent with the assignment requirement: SQL Server remains the only database for this week, and all database access remains parameterized.

## Consequences

- The solution is expected to pass both the unit and integration tests when a local SQL Server instance is available.
- Integration tests create and drop their own database, so the repository remains clean and no shared database is modified by the test suite.
- The project remains safe on machines without SQL Server because the application still supports the JSON fallback path when no connection string is configured.
- Any future change to the schema or stored procedures must be validated against the real database before being treated as complete.

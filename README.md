# ApexPost Logistics — SQL Server / EF Core Week 3

This repository implements the SQL Server persistence work for the ApexPost Logistics Week 3 assignment. The application keeps the same application contracts and business logic while moving the data layer behind EF Core and SQL Server. Couchbase is intentionally excluded from this assignment; SQL Server is the only supported database path.

## Solution structure

- ApexPost.Domain — domain entities, invariants, and value objects
- ApexPost.Application — application services, interfaces, and commands/queries
- ApexPost.Infrastructure — EF Core DbContext, Fluent API mappings, repository implementations, and SQL Server service adapters
- ApexPost.Web — composition root, MVC project, and runtime configuration
- ApexPost.Tests — unit, integration, and SQL validation tests
- sql — SQL scripts, migration output, reporting queries, stored procedures, and seed data
- docs/adr — architecture decision records for the SQL Server design

## Prerequisites

- .NET 10 SDK
- SQL Server LocalDB or a local SQL Server instance
- Local workstation access to the database instance

## Local SQL setup

Set the application connection string in user-secrets before running the app:

```powershell
dotnet user-secrets set "ConnectionStrings:ApexPostDb" "Server=(localdb)\\MSSQLLocalDB;Database=ApexPostDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true" --project ApexPost.Web
```

For a normal local SQL Server instance:

```powershell
dotnet user-secrets set "ConnectionStrings:ApexPostDb" "Server=localhost;Database=ApexPostDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true" --project ApexPost.Web
```

You may also use the environment variable `APEXPOST_DB_CONNECTION` if you prefer a deployment-style configuration:

```powershell
$env:APEXPOST_DB_CONNECTION = "Server=localhost;Database=ApexPostDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

## Database creation and migrations

Apply the current migrations:

```powershell
dotnet ef database update --project ApexPost.Infrastructure --startup-project ApexPost.Web
```

Generate the idempotent schema script:

```powershell
dotnet ef migrations script --idempotent --project ApexPost.Infrastructure --startup-project ApexPost.Web --output sql/ApexPostDb_schema.sql
```

The repo already includes the SQL artifacts under `sql/` for reporting, stored procedures, seed data, and performance workloads.

## SQL artifacts in this repo

- `sql/ApexPostDb_schema.sql` — idempotent database schema script
- `sql/reporting-queries.sql` — branch sales, low-stock, zone/service, and money-order reports
- `sql/stored-procedures.sql` — `CREATE OR ALTER` procedures for reports and stock adjustment
- `sql/seed-data.sql` — sample seed data for branches, products, staff, stock, rates, and transactions
- `sql/performance-workloads.sql` — index and query workload scripts for plan comparison
- `sql/assignment3-compliance.sql` — multi-table transaction, blocking pattern, deadlock guidance, and SARGable rewrite example
- `sql/performance-evidence/` — saved plan files and performance artifacts

## Run the app

```powershell
dotnet restore ApexPost.slnx
dotnet build ApexPost.slnx
dotnet test ApexPost.slnx
dotnet run --project ApexPost.Web
```

The application includes a safe fallback path for local scenarios without a configured database, but the primary Assignment 3 path is the SQL Server / EF Core implementation.

## Assignment 3 focus

This repo focuses on the Week 3 deliverables:

- normalized relational schema design with branch and stock integrity
- EF Core 10 mapping and migrations
- repository and unit-of-work structure behind application interfaces
- DTO-based and projection-based query reads
- stored-procedure wrappers for report and transactional SQL work
- performance evidence using actual execution plans and measured logical reads/CPU
- ADRs documenting the important design decisions

## Documentation and evidence

- `DESIGN.md` — architecture presentation, ER diagram, normalization notes, and ADR references
- `PERFORMANCE.md` — execution-plan evidence and before/after workload discussion
- `docs/ASSIGNMENT-3-RUBRIC-CHECKLIST.md` — explicit requirement-to-evidence tracking against the final rubric
- `docs/adr/` — accepted design decisions for persistence, schema, procedures, indexes, validation, repository boundaries, concurrency, and final assignment evidence
- `sql/assignment3-compliance.sql` — atomic multi-table sale example, blocking pattern guidance, deadlock-avoidance note, and SARGable rewrite example

## Notes

- No secrets are committed to source control.
- Identity document values remain masked and do not persist full document numbers.
- All database access in the SQL part of the assignment remains parameterized and asynchronous where supported.
- The project intentionally uses SQL Server as the only database for this assignment and does not rely on Couchbase.

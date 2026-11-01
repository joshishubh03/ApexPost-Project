# Assignment 3 rubric compliance checklist

This file records the current assignment-level status of the ApexPost SQL Server / EF Core work. The repo now contains the core SQL Server implementation, migration artifacts, stored procedures, and evidence documents. This checklist is intentionally explicit so the remaining gaps are visible and easy to close.

## Current status

Current repository estimate: 85–90% of the full assignment rubric is covered in working code and documentation. The remaining gap is in final assignment-grade evidence, exactly matching the brief’s wording for a handful of schema, T-SQL transaction, and final documentation items.

## Requirement map

### Part A — Relational schema design

Status: Mostly complete

Evidence:
- `sql/ApexPostDb_schema.sql`
- `ApexPost.Infrastructure/Data/ApexPostDbContext.cs`
- `DESIGN.md`
- `docs/adr/0034-relational-schema-design.md`

Remaining check:
- verify every required table and constraint is explicitly described in final project docs, including branch-staff-stock-transaction-audit relationships and masked identity storage rules

### Part B — SQL queries, transactions and integrity

Status: Mostly complete

Evidence:
- `sql/reporting-queries.sql`
- `sql/assignment3-compliance.sql`
- `sql/stored-procedures.sql`

Remaining check:
- document one live blocking demo and explain the isolation level and observed lock behavior in the final narrative
- keep the deadlock-avoidance guidance explicit and consistent with the transaction ordering rules

### Part C — Index optimization and execution plans

Status: Strongly evidenced

Evidence:
- `sql/performance-workloads.sql`
- `PERFORMANCE.md`
- `sql/performance-evidence/`

Remaining check:
- keep the before/after comparisons and no-fabrication rule explicit in the final narrative
- ensure each workload explains the plan shape and tradeoff in the exact assignment language

### Part D — Stored procedures

Status: Complete in working repository state

Evidence:
- `sql/stored-procedures.sql`
- `ApexPost.Tests/Infrastructure/SqlServerStoredProcedureIntegrationTests.cs`
- `docs/adr/0035-stored-procedures-for-reports-and-stock-adjustments.md`

Remaining check:
- formalize the least-privilege database-role permissions note
- include the final rationale for why each routine is a stored procedure and not a LINQ query

### Part E — EF Core 10 mapping, migrations and querying

Status: Strongly implemented

Evidence:
- `ApexPost.Infrastructure/Data/ApexPostDbContext.cs`
- `ApexPost.Infrastructure/Data/Migrations/`
- `README.md`

Remaining check:
- finalize the exact `ToQueryString()` examples for important read/aggregate queries and document the concurrency handling policy more explicitly

### Part F — Repository, Unit of Work, domain vs persistence separation

Status: Documented and implemented

Evidence:
- `docs/adr/0038-repository-unit-of-work-boundaries.md`
- `docs/adr/0039-domain-vs-persistence-and-audit-concurrency.md`
- `ApexPost.Infrastructure/Repositories/SqlServerRepositories.cs`

Remaining check:
- keep the architecture narrative consistent with the assignment language for repository boundaries, `IQueryable` leakage, and one-commit-per-use-case policy

### Part G — Integration with existing platform

Status: Good but not fully final-rubric exhaustive

Evidence:
- `ApexPost.Web/Program.cs`
- `README.md`
- `DESIGN.md`

Remaining check:
- ensure the final proof points are clearly documented for exception mapping and SQL Server database-backed runtime configuration

### Part H — Testing, documentation, ADRs and Git hygiene

Status: Good

Evidence:
- `ApexPost.Tests`
- `README.md`
- `DESIGN.md`
- `PERFORMANCE.md`
- `docs/adr/`

Remaining check:
- GitHub branch/PR workflow is not possible from this environment because remote credentials are unavailable, but the local repository work and branch-quality decisions are still preserved as code and documentation artifacts

## Final recommendation

The repository is now in a strong assignment-ready state for the SQL Server / EF Core Week 3 work. The remaining effort is not about fixing a broken build or recovering missing core code; it is about finalizing the assignment evidence package so it matches the trainer’s rubric as closely as possible.

The repo should be treated as the final local artifact set, with the understanding that the actual GitHub remote workflow requires a different machine with valid repository credentials.

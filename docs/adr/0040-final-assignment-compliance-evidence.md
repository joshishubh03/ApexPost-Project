# 0040: Final assignment-compliance evidence and repository state

- Status: Accepted
- Date: 2026-11-01

## Context

The ApexPost project now contains the SQL Server persistence foundation, migrations, SQL scripts, stored procedures, an execution-plan evidence package, and ADRs that support the Week 3 assignment. The repository remains buildable and test-passing, but the final assignment brief is broad enough that the repo must be treated as a strong local evidence set rather than a literal 100/100 remote submission environment.

## Decision

We treat the repository as the final audited artifact set for the local assignment work and record its main compliance evidence here. The project is designed around the following principles:

1. SQL Server is the database of record for the Week 3 work.
2. The app keeps its business contracts stable while the persistence layer is swapped behind EF Core.
3. The database artifacts and docs are intentionally versioned under `sql/`, `docs/adr/`, `README.md`, `DESIGN.md`, and `PERFORMANCE.md`.
4. Remote GitHub branch and PR creation remains outside the scope of this environment because credentials and access are not available here.

## Why this is the right final posture

- The code and docs are already aligned to the assignment’s main engineering goals.
- The live SQL validation path, migration artifacts, procedure execution path, and performance evidence make the repository materially credible.
- The remaining items are mainly evidence completeness and final assignment-language alignment rather than foundational code repair.

## Consequences

- The repository is a credible local assignment artifact set for the SQL Server / EF Core work.
- The project stays honest about the environment limitation for GitHub remote actions.
- The final evidence package remains in the repository so a trainer or reviewer can inspect the implementation and design choices directly.

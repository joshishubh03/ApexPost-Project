# 0038: Repository and Unit of Work boundaries

- Status: Accepted
- Date: 2026-11-01

## Context

The assignment requires a layered design in which the Domain remains free of persistence concerns, the Application owns the interfaces, and the Infrastructure implements them with EF Core. The repository layer must still support rich transaction behavior without leaking the `DbContext` or `IQueryable` outside the Infrastructure project.

The application also has read-heavy reporting and write-heavy use cases. A single composition pattern is needed so that controller actions, application services, and reports do not accidentally depend on the database shape or tracking behavior of EF Core.

## Decision

We define the following boundaries:

- `Application` owns repository interfaces such as `IBranchRepository`, `IProductRepository`, `IStockRepository`, `ICounterTransactionRepository`, `IStaffRepository`, and `IAuditStore`.
- `Infrastructure` implements those interfaces with EF Core and exposes no database-specific abstractions to the rest of the solution.
- Repositories stage changes only. They do not call `SaveChangesAsync` and they do not return `IQueryable`.
- One `IUnitOfWork` is used per use case and owns the request-scoped `DbContext` plus the transaction boundary.
- Each use case performs one business commit via `SaveChangesAsync` or `ExecuteInTransactionAsync`.
- Query services project directly into DTOs using `AsNoTracking` and `Select` rather than returning domain entities or exposing tracked `DbSet` instances.
- The only direct `DbContext` access that is allowed is in Infrastructure query services or explicit procedural wrappers where the operation is database-centric, such as a stored procedure execution or a set-based update.

This keeps the repository pattern explicit while still permitting the SQL Server-specific optimizations that the assignment requires.

## Why this is the right split

1. The Application stays independent of EF Core types and database semantics.
2. The Web layer remains a composition root and never owns business logic.
3. Read-only reporting can use projection-based DTO queries without tracking.
4. Transaction boundaries stay clear and consistent across application services.
5. The code remains compatible with both the SQL Server path and the safe fallback path when no database is configured.

## Consequences

- The codebase gains composability and testability because repositories are interface-based and command/query concerns are separated.
- Querying and writing are deliberately different: read screens use projection DTOs, writes use commands and the unit of work.
- Infrastructure code must remain disciplined about avoiding what would otherwise become a leaky persistence layer.
- The design remains stronger for future changes because the SQL implementation can evolve without changing the domain or application contracts.

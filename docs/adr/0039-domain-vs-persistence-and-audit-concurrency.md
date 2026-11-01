# 0039: Domain vs persistence models, audit strategy, and stock concurrency

- Status: Accepted
- Date: 2026-11-01

## Context

The database design must protect the business rules while still supporting high-volume branch operations and safe reporting. The application has several distinct concerns:

- rich domain invariants such as stock decrement rules and money-order verification status
- persistence concerns such as rowversion, audit columns, and database constraints
- cross-cutting audit writes that must not interfere with the business transaction
- concurrent stock updates where two clerks might sell the last unit at nearly the same time

The assignment explicitly demands that the domain model remain rich and the persistence model stay separated where appropriate, while still mapping cleanly to SQL Server tables and EF Core.

## Decision

We make the following design choices:

1. Domain and persistence separation
   - Rich domain entities keep business methods and private setters.
   - The EF Core model uses a persistence representation when needed, with explicit mapping via `IEntityTypeConfiguration` and value converters for domain value objects.
   - Where a business concern is purely database-oriented, we keep the persistence mapping in the infrastructure layer rather than expanding the domain model.

2. Concurrency strategy
   - `BranchStock` and `CounterTransactions` carry a rowversion concurrency token.
   - The application uses optimistic concurrency for stock updates and transaction writes.
   - When a clerk tries to sell the last unit while another clerk has already decremented the same quantity, EF Core raises `DbUpdateConcurrencyException` and the application translates it into a user-facing 409 conflict.
   - The operational flow uses a retry-capable execution strategy only where the operation is a safe repeatable business operation.

3. Audit strategy
   - Audit writes are kept in an `IAuditStore` used by the MVC filter or application layer.
   - Audit writes are intentionally not allowed to block or invalidate the business transaction.
   - We do not force audit entries to share the same commit as the business change in the critical path. Instead, the audit event is recorded as a separate, low-risk write path so that a failed audit cannot roll back a valid sale or stock adjustment.
   - This is a deliberate trade-off: the business transaction remains durable and consistent, while audit data remains useful for traceability and investigation.

## Why this is the right compromise

- The domain remains expressive and not polluted by infrastructure concerns.
- The database keeps concurrency controls where they belong.
- The application receives a clear and explicit 409 response when optimistic concurrency fails.
- Audit entries remain operationally useful without making the core transaction fragile.

## Consequences

- Domain rules stay in the domain, and persistence details stay in Infrastructure.
- The application must handle concurrency conflicts explicitly and convert them to safe problem-details responses.
- Audit writes are operationally valuable but not transactional dependencies of the core branch-sell flow.
- The design remains consistent with the assignment requirement to avoid deadlocks through a consistent table access order and short transactions.

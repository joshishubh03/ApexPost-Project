# 0044: Repository and Unit of Work boundaries; where direct DbContext projections are acceptable

- Status: Accepted (supersedes 0038)
- Date: 2026-11-03

## Decision

**Dependency direction**: Domain references nothing (no NuGet packages at all - EF Core packages were moved out
of `Directory.Build.props` into Infrastructure/Web). Application defines `I*Repository`, `IUnitOfWork`,
`IAuditStore`, `IReportingQueries`, `IStockAdjustmentCommand`, `IPriceRevisionService`. Infrastructure implements
them with EF Core. Web (`Program.cs`) only composes.

**Repositories** (`Persistence/Repositories`):
- One per aggregate: Branch, CounterTerminal, Staff, Product, Inventory (BranchStock + StockAdjustments = the
  "stock repository"), ShippingZone, ShippingRate, CounterTransaction (+ lines, payment, shipment), MoneyOrder,
  IdentityVerification, AuditLog (domain-event audit), Notification.
- They **stage changes only** - no repository calls `SaveChanges` (previously 28 calls).
- No member returns `IQueryable`; reads return immutable domain records.
- Focused members (`GetByIdAsync`, `GetForBranchAsync`, `GetRecentForBranchAsync`, `AddAsync`, `UpdateAsync`)
  have default interface implementations over `GetAll/SaveAll`, so the JSON adapter and test fakes keep working
  unchanged while EF Core overrides them with targeted queries.
- Rows already staged in the current unit of work (`DbSet.Local`) are reused, so two calls in one use case
  never add a duplicate.

**Unit of Work**: `EfUnitOfWork.ExecuteInTransactionAsync` = execution strategy + explicit transaction +
**one** `SaveChangesAsync` + commit. `UnitOfWorkMutationCoordinator` wraps every application write in it, so
**one use case = one commit**; nested use cases (a money order inside a counter sale) join the outer one.
`EfUnitOfWork` also translates `DbUpdateConcurrencyException` -> `ConcurrencyConflictException` and unique-key
violations -> `DuplicateResourceException` (both HTTP 409).

**Lifetimes**: `ApexPostDbContext`, every repository, the unit of work, query services and commands are
**scoped** - one DbContext per request. Only the stateless `AuditColumnsInterceptor` is a singleton.

**Where direct DbContext use is acceptable** (and only there):
1. Query services (`EfReportingQueries`): reports/dashboard read with `AsNoTracking` projections straight into
   DTOs or through stored procedures. No domain object is needed to read, so a repository would only add copying.
2. Set-based commands (`EfPriceRevisionService` with `ExecuteUpdateAsync`, `SqlStockAdjustmentCommand` with the
   stored procedure): the operation *is* a database statement, there is no aggregate to load.
Both run inside their own explicit transaction + execution strategy because they mix `ExecuteUpdate`/procedures
with `SaveChanges`.

**CQS**: reads go through query services, writes through application services + repositories + unit of work.
Both use the same ApexPostDb - **no separate read database is required or used**.

## Consequences

- Services that previously saved after every step now commit once; a failure anywhere rolls back everything
  (proved by `Failed_sale_rolls_back_stock_and_transaction_rows_together` and the spy unit-of-work tests).
- The JSON adapter keeps its Week 2 compensation logic behind `IUnitOfWork.SupportsAtomicCommit == false`.

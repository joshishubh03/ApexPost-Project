# 0046: Concurrency strategy for stock and transactions

- Status: Accepted (supersedes 0039, concurrency part)
- Date: 2026-11-03

## Decision

1. **Optimistic concurrency with `rowversion`** on `BranchStock` and `CounterTransactions`.
   `EfInventoryRepository` and `EfCounterTransactionRepository` remember the rowversion they *read* and use it
   as the original value when the row is updated, so the check covers the whole use case (read -> decide ->
   commit), not just the moment of `SaveChanges`.
2. Two clerks selling the last unit: both read stock = 1; the first commit wins; the second `UPDATE ... WHERE
   RowVersion = @original` affects 0 rows -> `DbUpdateConcurrencyException` -> `ConcurrencyConflictException`
   -> **HTTP 409** with "Another user changed this record... refresh and try again" (MVC shows `Conflict.cshtml`
   with status 409; the API returns Problem Details). Proved by `Two_clerks_selling_the_last_unit_cannot_drive_stock_negative`
   and `Concurrency_conflict_on_stock_returns_409`.
3. **Last line of defence**: `CHECK (QuantityOnHand >= 0)`; the stored procedure uses a guarded atomic
   `UPDATE ... WHERE QuantityOnHand + @Delta >= 0`.
4. **Isolation level**: READ COMMITTED for every unit of work and procedure. `ApexPostDb` runs with
   READ_COMMITTED_SNAPSHOT ON in our environment (verified in `sys.databases`, see the blocking demo), so readers
   such as reports and the dashboard use row versions and are never blocked by a clerk's sale. Correctness does
   not depend on RCSI being on or off: lost updates are prevented by rowversion and guarded UPDATEs, not by
   readers holding locks. Unit-of-work transactions were previously SERIALIZABLE. That turned every concurrent
   sale into range locks and deadlocks, and with rowversion the stronger level buys nothing.
5. **Execution strategy**: `EnableRetryOnFailure(3)` retries transient errors (including deadlock victim 1205)
   for the whole unit of work; `ChangeTracker.Clear()` at the start of each attempt prevents replaying staged
   changes. Concurrency conflicts are *not* transient and are never retried automatically - the clerk must see
   the new stock level.
6. **Deadlock avoidance**: short transactions (no user input inside, all reads that decide the sale happen
   before the commit), and one table order for writers: BranchStock -> StockAdjustments -> transaction rows
   (application, `usp_AdjustStock` and `sql/stock-sale-transaction.sql`). See `docs/sql-transactions-and-concurrency.md`.

## Consequences

Under contention one clerk gets a 409 instead of waiting or deadlocking; that is the intended behaviour for a
counter (retry with fresh stock). `Products` also has a rowversion, used by the catalogue cache version check.

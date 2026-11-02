# T-SQL transactions, blocking and deadlocks (Part B)

## 1. A multi-table transaction in T-SQL

`sql/stock-sale-transaction.sql` records one counter sale in a single transaction. It touches five tables:
`BranchStock`, `StockAdjustments`, `CounterTransactions`, `TransactionLines` and `Payments`.

| Technique | Why |
|---|---|
| `SET XACT_ABORT ON` | Any run-time error (FK, CHECK, conversion) dooms the transaction and rolls it back, so a half-written sale can never be committed. |
| `BEGIN TRANSACTION` inside `BEGIN TRY` | Errors raised inside the transaction jump to `CATCH`. |
| Guarded `UPDATE ... WHERE QuantityOnHand >= @Quantity` + `@@ROWCOUNT` | Checks the stock and changes it in one atomic statement. A separate SELECT-then-UPDATE would leave a race window. |
| `THROW 51001, @Message, 1` | Business errors use numbers in the 51000 range, which the caller can recognise. |
| `IF XACT_STATE() <> 0 ROLLBACK` | `-1` means the transaction is doomed and can only roll back; `1` means it is committable, but the batch failed, so roll back as well. |
| `THROW;` in `CATCH` | Re-raises the original error number, message and line. `RAISERROR` would replace them. |

`usp_AdjustStock` (migration `Week3SchemaCompliance`, also in `sql/stored-procedures.sql`) uses the same pattern
and joins a caller's transaction when there is one. It only issues `BEGIN TRAN` / `COMMIT` when `@@TRANCOUNT = 0`
at entry. That is how `SqlStockAdjustmentCommand` runs the procedure and an EF `SaveChanges` atomically.

In C#, the same guarantees come from `EfUnitOfWork.ExecuteInTransactionAsync`:

1. It runs inside the execution strategy.
2. It opens one READ COMMITTED transaction.
3. It calls one `SaveChanges`, then commits.
4. Any exception disposes the transaction, which rolls it back.

`Failed_sale_rolls_back_stock_and_transaction_rows_together` proves this against SQL Server: it checks that neither the
stock nor any transaction row changed.

## 2. Blocking demonstration (observed)

Script: `sql/blocking-demo.sql` (two SSMS windows). Automated run: `scripts/run-blocking-demo.ps1`, which writes
`sql/performance-evidence/blocking-demo-output.txt`. Captured on 2026-11-03 against `ApexPostDb` on LocalDB
(SQL Server 17.0.4025):

```
name        is_read_committed_snapshot_on  snapshot_isolation_state_desc  is_optimized_locking_on
ApexPostDb  1                              OFF                            0

--- Session 1 (writer, spid 84): BEGIN TRAN; UPDATE BranchStock ... (AP-001 / PACK-003); WAITFOR 20 s; ROLLBACK
writer_locked_at 04:27:03.866        writer_released_at 04:27:23.882

--- Session 3 (observer)
session_id blocking_session_id wait_type wait_ms wait_resource
87         84                  LCK_M_S   2020    KEY: 5:72057594048806912 (6f5b67fe6cb0)

request_session_id resource_type request_mode request_status index_name
84                 KEY           X            GRANT          PK_BranchStock
84                 KEY           X            GRANT          IX_BranchStock_BranchId_QuantityOnHand  (old + new key)
87                 KEY           S            WAIT           IX_BranchStock_BranchId_QuantityOnHand

--- Session 2a (default READ COMMITTED, spid 86)      QuantityOnHand 97   waited 0 ms
--- Session 2b (WITH (READCOMMITTEDLOCK), spid 87)    QuantityOnHand 97   waited 19 796 ms
```

What this shows:

- **The writer** holds exclusive (X) key locks on the clustered index row. It also holds them on the old and new
  keys of the nonclustered index, because `QuantityOnHand` is part of the key of
  `IX_BranchStock_BranchId_QuantityOnHand`.
- **Session 2b uses a locking read.** It needs a shared (S) lock on that row. S is incompatible with X, so the
  session waits (`LCK_M_S`). `blocking_session_id = 84` names the culprit. The wait lasts until the writer rolls
  back, about 19.8 s, the remainder of its 20 s delay. The value it then reads is the committed `97`, not the
  uncommitted `96`.
- **Session 2a does not wait.** `ApexPostDb` has **READ_COMMITTED_SNAPSHOT ON** in this environment
  (observed in `sys.databases`). Under RCSI a plain READ COMMITTED reader gets the last committed version of the
  row from the version store instead of waiting. It also returns `97`: no dirty read, no blocking.

What we do about it:

- **Keep transactions short.** No user interaction and no remote calls happen inside a transaction. The decide
  step (load stock, validate the sale) and the single `SaveChanges` run back-to-back. The audit row is written
  after the commit, through its own context.
- **Prefer RCSI for readers.** Reports and the dashboard read while clerks sell. Under RCSI these readers neither
  block nor are blocked by writers. Our correctness does **not** depend on readers blocking writers: stock
  decrements are protected by `rowversion` and the guarded UPDATE (ADR 0046). So RCSI is safe for ApexPost.
  On a server where it is off, a DBA can enable it with
  `ALTER DATABASE ApexPostDb SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;`.
- **Writer-writer conflicts still block.** RCSI does not remove X/X waits: two clerks updating the same stock row
  are serialised by SQL Server. The second one then fails the `rowversion` check, and the user gets HTTP 409.

## 3. Isolation level choice

| Level | Used? | Reason |
|---|---|---|
| READ UNCOMMITTED / `NOLOCK` | Never | Dirty reads could show stock that is rolled back a moment later. Can also skip or double-read rows during page splits. |
| **READ COMMITTED (+ RCSI where enabled)** | **Yes**: unit of work, procedures, reports | No dirty reads. Readers do not block writers under RCSI. Lost updates are prevented by `rowversion` (optimistic) and guarded UPDATEs, not by holding locks. |
| REPEATABLE READ / SERIALIZABLE | No | The earlier version used SERIALIZABLE for every unit of work. Concurrent sales then took range locks and deadlocked (S->X conversion). The extra protection added nothing that `rowversion` does not already give. |
| SNAPSHOT | Not enabled | Would need `ALLOW_SNAPSHOT_ISOLATION`. Update conflicts would surface as error 3960 instead of our 409. RCSI covers the reporting need. |

## 4. Deadlocks: how they happen and how we avoid them

A deadlock needs two transactions that each hold a lock the other one wants. The classic ApexPost case:

```
T1 (sale of PACK-001 then PACK-002)          T2 (sale of PACK-002 then PACK-001)
UPDATE BranchStock ... PACK-001  -> X(001)
                                             UPDATE BranchStock ... PACK-002 -> X(002)
UPDATE BranchStock ... PACK-002  -> waits for T2
                                             UPDATE BranchStock ... PACK-001 -> waits for T1  => deadlock (1205)
```

SQL Server's lock monitor picks a victim and rolls it back with error 1205. The rules we follow:

1. **One access order everywhere.** Writers go BranchStock -> StockAdjustments -> CounterTransactions ->
   TransactionLines -> Payments/Shipments. EF Core sends the UPDATEs/INSERTs of one `SaveChanges` in a stable order.
   `sql/stock-sale-transaction.sql` and `usp_AdjustStock` follow the same order.
2. **Lock rows in key order.** Stock rows are loaded ordered by `(BranchId, ProductId)`, and EF Core sends
   the UPDATEs of one table in primary-key order within a `SaveChanges`. Two baskets that contain the same
   products therefore lock them in the same sequence, so the T1/T2 cycle above cannot form.
3. **Short transactions.** All reads that decide the sale happen first. The transaction covers only the writes
   and lasts milliseconds.
4. **Narrow, indexed predicates.** Updates seek on the primary key `(BranchId, ProductId)`, so they lock one key,
   not a range. No lock escalation is triggered.
5. **No SERIALIZABLE.** This removes range-lock deadlocks and S->X lock-conversion deadlocks.
6. **Retry when SQL Server picks us as the victim.** `EnableRetryOnFailure` treats 1205 as transient and re-runs
   the whole unit of work. Before each attempt, `ChangeTracker.Clear()` drops any staged changes, so nothing is
   replayed twice. Concurrency conflicts (409) are **not** retried, because the clerk must see the new stock.

To investigate a production deadlock, read the `xml_deadlock_report` from the built-in `system_health` Extended
Events session:

```sql
SELECT CAST(event_data AS xml) AS deadlock_graph
FROM sys.fn_xe_file_target_read_file('system_health*.xel', NULL, NULL, NULL)
WHERE object_name = 'xml_deadlock_report';
```

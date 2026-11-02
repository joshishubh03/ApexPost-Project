/*
    Part B - blocking demonstration with TWO sessions (two SSMS query windows, or two sqlcmd windows).
    Observed results and the explanation are in docs/sql-transactions-and-concurrency.md.

    ============================== SESSION 1 (writer) ==============================
    Run this block. It updates one stock row and keeps the transaction OPEN for 20 seconds.
*/
-- SESSION 1
USE ApexPostDb;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
    UPDATE bs SET QuantityOnHand = QuantityOnHand - 1
    FROM dbo.BranchStock AS bs
    INNER JOIN dbo.Branches AS b ON b.Id = bs.BranchId
    INNER JOIN dbo.Products AS p ON p.Id = bs.ProductId
    WHERE b.Code = 'AP-001' AND p.Sku = 'PACK-003';      -- takes an X (exclusive) row lock
    WAITFOR DELAY '00:00:20';                           -- simulate a slow, too-long transaction
ROLLBACK TRANSACTION;                                   -- demo only: leave the data unchanged
GO

/*
    ============================== SESSION 2 (reader) ==============================
    Start within the 20 seconds. Under LOCKING read committed the SELECT needs a shared (S) lock on the same
    row, which is incompatible with Session 1's X lock, so it WAITS (LCK_M_S) until Session 1 finishes.
    If the database has READ_COMMITTED_SNAPSHOT ON (ApexPostDb does - see the end of this file) this plain
    SELECT does not wait: it reads the last committed row version. Use the locking variant below to see the wait.
*/
-- SESSION 2
USE ApexPostDb;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
SELECT bs.QuantityOnHand
FROM dbo.BranchStock AS bs
INNER JOIN dbo.Branches AS b ON b.Id = bs.BranchId
INNER JOIN dbo.Products AS p ON p.Id = bs.ProductId
WHERE b.Code = 'AP-001' AND p.Sku = 'PACK-003';
GO

/*
    ============================== SESSION 3 (observer) =============================
    While Session 2 is waiting, this shows who blocks whom and on which lock.
*/
-- SESSION 3
SELECT r.session_id, r.blocking_session_id, r.wait_type, r.wait_time AS wait_ms, r.wait_resource,
       t.text AS waiting_sql
FROM sys.dm_exec_requests AS r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) AS t
WHERE r.blocking_session_id <> 0;

SELECT l.request_session_id, l.resource_type, l.request_mode, l.request_status,
       OBJECT_NAME(p.object_id) AS table_name
FROM sys.dm_tran_locks AS l
LEFT JOIN sys.partitions AS p ON p.hobt_id = l.resource_associated_entity_id
WHERE l.resource_database_id = DB_ID('ApexPostDb') AND l.resource_type IN ('KEY', 'PAGE', 'OBJECT')
ORDER BY l.request_session_id;
GO

/*
    Observed (sql/performance-evidence/blocking-demo-output.txt, docs/sql-transactions-and-concurrency.md):
    ApexPostDb has READ_COMMITTED_SNAPSHOT ON, so the plain SELECT in Session 2 returns the last COMMITTED value
    immediately (row versioning - no dirty read, no wait). To see classic lock blocking, run Session 2 with a
    locking read instead:
*/
-- SESSION 2 (locking variant) - waits on LCK_M_S until Session 1 finishes
SELECT bs.QuantityOnHand
FROM dbo.BranchStock AS bs WITH (READCOMMITTEDLOCK)
INNER JOIN dbo.Branches AS b ON b.Id = bs.BranchId
INNER JOIN dbo.Products AS p ON p.Id = bs.ProductId
WHERE b.Code = 'AP-001' AND p.Sku = 'PACK-003';
GO
-- Check the setting:  SELECT name, is_read_committed_snapshot_on FROM sys.databases WHERE name = 'ApexPostDb';

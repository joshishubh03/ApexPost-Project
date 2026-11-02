<#
    Runs sql/blocking-demo.sql as three real sessions against ApexPostDb and records what happened in
    sql/performance-evidence/blocking-demo-output.txt (writer holds an X lock for 20 s, reader waits, observer
    captures sys.dm_exec_requests / sys.dm_tran_locks while the reader is blocked).
    The reader and observer poll the lock manager instead of relying on fixed sleeps, so a slow LocalDB start-up
    cannot make the reader run before the writer has taken its lock.
#>
param([string]$Server = "(localdb)\MSSQLLocalDB", [string]$Database = "ApexPostDb")
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root "sql/performance-evidence/blocking-demo-output.txt"

# Wake LocalDB up first (it auto-stops when idle and the first connection can take several seconds).
sqlcmd -S $Server -E -d $Database -W -h -1 -Q "SET NOCOUNT ON; SELECT 1;" | Out-Null

$filter = "INNER JOIN dbo.Branches AS b ON b.Id = bs.BranchId INNER JOIN dbo.Products AS p ON p.Id = bs.ProductId WHERE b.Code = 'AP-001' AND p.Sku = 'PACK-003'"
$waitForWriter = "DECLARE @w int = 0; WHILE @w < 150 AND NOT EXISTS (SELECT 1 FROM sys.dm_tran_locks WHERE resource_type IN ('KEY', 'XACT') AND request_mode = 'X' AND request_session_id <> @@SPID AND request_session_id IN (SELECT session_id FROM sys.dm_exec_sessions WHERE database_id = DB_ID())) BEGIN WAITFOR DELAY '00:00:00.200'; SET @w += 1; END;"
$writer = "SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION; UPDATE bs SET QuantityOnHand = QuantityOnHand - 1 FROM dbo.BranchStock AS bs $filter; SELECT CONVERT(varchar(23), SYSDATETIME(), 121) AS writer_locked_at, @@SPID AS writer_spid; WAITFOR DELAY '00:00:20'; ROLLBACK TRANSACTION; SELECT CONVERT(varchar(23), SYSDATETIME(), 121) AS writer_released_at;"
$reader = "SET NOCOUNT ON; SET TRANSACTION ISOLATION LEVEL READ COMMITTED; $waitForWriter DECLARE @t0 datetime2 = SYSDATETIME(); SELECT CONVERT(varchar(23), @t0, 121) AS reader_started_at; SELECT bs.QuantityOnHand FROM dbo.BranchStock AS bs $filter; SELECT @@SPID AS reader_spid, CONVERT(varchar(23), SYSDATETIME(), 121) AS reader_finished_at, DATEDIFF(millisecond, @t0, SYSDATETIME()) AS reader_waited_ms;"
$lockingReader = "SET NOCOUNT ON; SET TRANSACTION ISOLATION LEVEL READ COMMITTED; $waitForWriter DECLARE @t0 datetime2 = SYSDATETIME(); SELECT CONVERT(varchar(23), @t0, 121) AS reader_started_at; SELECT bs.QuantityOnHand FROM dbo.BranchStock AS bs WITH (READCOMMITTEDLOCK) $filter; SELECT @@SPID AS reader_spid, CONVERT(varchar(23), SYSDATETIME(), 121) AS reader_finished_at, DATEDIFF(millisecond, @t0, SYSDATETIME()) AS reader_waited_ms;"
$observer = "SET NOCOUNT ON; DECLARE @i int = 0; WHILE @i < 100 AND NOT EXISTS (SELECT 1 FROM sys.dm_exec_requests WHERE blocking_session_id <> 0 AND database_id = DB_ID()) BEGIN WAITFOR DELAY '00:00:00.200'; SET @i += 1; END; WAITFOR DELAY '00:00:02'; SELECT r.session_id, r.blocking_session_id, r.wait_type, r.wait_time AS wait_ms, r.wait_resource FROM sys.dm_exec_requests AS r WHERE r.blocking_session_id <> 0; SELECT l.request_session_id, l.resource_type, l.request_mode, l.request_status, OBJECT_NAME(p.object_id) AS table_name, i.name AS index_name FROM sys.dm_tran_locks AS l LEFT JOIN sys.partitions AS p ON p.hobt_id = l.resource_associated_entity_id LEFT JOIN sys.indexes AS i ON i.object_id = p.object_id AND i.index_id = p.index_id WHERE l.resource_database_id = DB_ID() AND l.resource_type IN ('KEY', 'XACT', 'OBJECT') AND l.request_session_id <> @@SPID ORDER BY l.request_session_id, l.request_status;"
$settings = "SET NOCOUNT ON; SELECT name, is_read_committed_snapshot_on, snapshot_isolation_state_desc, is_accelerated_database_recovery_on, CAST(DATABASEPROPERTYEX(DB_NAME(), 'IsOptimizedLockingOn') AS int) AS is_optimized_locking_on, CAST(SERVERPROPERTY('ProductVersion') AS varchar(30)) AS product_version FROM sys.databases WHERE name = DB_NAME();"

$w = Join-Path $env:TEMP "apexpost-writer.txt"; $r = Join-Path $env:TEMP "apexpost-reader.txt"; $l = Join-Path $env:TEMP "apexpost-locking-reader.txt"; $o = Join-Path $env:TEMP "apexpost-observer.txt"
$s = sqlcmd -S $Server -E -d $Database -W -Q $settings
$wp = Start-Process sqlcmd -ArgumentList "-S `"$Server`" -E -d $Database -W -Q `"$writer`"" -RedirectStandardOutput $w -PassThru -WindowStyle Hidden
$rp = Start-Process sqlcmd -ArgumentList "-S `"$Server`" -E -d $Database -W -Q `"$reader`"" -RedirectStandardOutput $r -PassThru -WindowStyle Hidden
$lp = Start-Process sqlcmd -ArgumentList "-S `"$Server`" -E -d $Database -W -Q `"$lockingReader`"" -RedirectStandardOutput $l -PassThru -WindowStyle Hidden
sqlcmd -S $Server -E -d $Database -W -Q $observer | Out-File $o -Encoding utf8
$wp.WaitForExit(); $rp.WaitForExit(); $lp.WaitForExit()
@("Isolation level: READ COMMITTED for both readers (database settings below)", "Run at: $((Get-Date).ToString('o'))", "") + $s +
    @("", "--- Session 1 (writer) ---") + (Get-Content $w) +
    @("", "--- Session 3 (observer, while the reader is blocked) ---") + (Get-Content $o) +
    @("", "--- Session 2a (reader, default READ COMMITTED - uses row versions when RCSI is ON) ---") + (Get-Content $r) +
    @("", "--- Session 2b (reader, WITH (READCOMMITTEDLOCK) - classic locking read) ---") + (Get-Content $l) | Set-Content $out -Encoding utf8
Get-Content $out

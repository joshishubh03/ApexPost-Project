<#
    Reproducible Part C evidence. Creates ApexPostDb_Perf (same migrations as ApexPostDb), loads realistic volume
    (sql/performance-data.sql), then for each workload captures SET STATISTICS IO/TIME and the ACTUAL execution
    plan before and after the candidate index, plus write overhead and index size. Results:
    sql/performance-evidence/results.json and *.sqlplan.  Usage (from the repo root):
        powershell -ExecutionPolicy Bypass -File scripts/measure-performance.ps1 [-Server localhost] [-SkipLoad]
#>
param([string]$Server = "localhost", [string]$Database = "ApexPostDb_Perf", [switch]$SkipLoad)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$evidence = Join-Path $root "sql/performance-evidence"
New-Item -ItemType Directory -Force $evidence | Out-Null
$cs = "Server=$Server;Database=$Database;Integrated Security=True;TrustServerCertificate=True;Encrypt=False"

if (-not $SkipLoad) {
    & sqlcmd -S $Server -E -C -b -Q "IF DB_ID('$Database') IS NOT NULL BEGIN ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database]; END"
    Push-Location $root
    dotnet ef database update --project ApexPost.Infrastructure --startup-project ApexPost.Web --context ApexPost.Infrastructure.Persistence.ApexPostDbContext --connection $cs
    Pop-Location
    if ($LASTEXITCODE -ne 0) { throw "dotnet ef database update failed ($LASTEXITCODE)" }
    # -I = QUOTED_IDENTIFIER ON, required because the schema has filtered indexes.
    & sqlcmd -S $Server -E -C -b -I -d $Database -i (Join-Path $root "sql/performance-data.sql") -W
    if ($LASTEXITCODE -ne 0) { throw "sql/performance-data.sql failed ($LASTEXITCODE)" }
}

function Invoke-Measured([string]$Sql, [string]$PlanFile) {
    $connection = New-Object System.Data.SqlClient.SqlConnection $cs
    $messages = New-Object System.Collections.Generic.List[string]
    $connection.add_InfoMessage({ param($sender, $e) foreach ($m in $e.Errors) { $messages.Add($m.Message) } })
    $connection.FireInfoMessageEventOnUserErrors = $false
    $connection.Open()
    $plans = @()
    for ($run = 1; $run -le 3; $run++) {   # runs 1-2 warm the cache, run 3 is recorded
        $messages.Clear(); $plans = @()
        $command = $connection.CreateCommand()
        $command.CommandTimeout = 600
        $command.CommandText = "SET STATISTICS IO ON; SET STATISTICS TIME ON; SET STATISTICS XML ON;`n" + $Sql + "`nSET STATISTICS XML OFF; SET STATISTICS IO OFF; SET STATISTICS TIME OFF;"
        $reader = $command.ExecuteReader()
        do {
            if ($reader.FieldCount -eq 1 -and $reader.GetName(0) -like "*Showplan*") {
                while ($reader.Read()) { $plans += $reader.GetString(0) }
            } else { while ($reader.Read()) { } }
        } while ($reader.NextResult())
        $reader.Close()
    }
    $connection.Close()
    $logical = 0; $cpu = 0; $elapsed = 0; $tables = @()
    foreach ($m in $messages) {
        foreach ($line in ($m -split "`n")) {
            if ($line -match "Table '([^']+)'.*?logical reads (\d+)") { $logical += [int]$Matches[2]; $tables += "$($Matches[1])=$($Matches[2])" }
            if ($line -match "SQL Server Execution Times:") { $inExec = $true }
            if ($line -match "CPU time = (\d+) ms,\s+elapsed time = (\d+) ms" -and $inExec) { $cpu += [int]$Matches[1]; $elapsed += [int]$Matches[2]; $inExec = $false }
        }
    }
    $plan = $plans | Where-Object { $_ -match "StatementType=""SELECT""|StatementType=""UPDATE""" } | Select-Object -Last 1
    if (-not $plan) { $plan = $plans | Select-Object -Last 1 }
    $shape = @(); $estimates = @()
    if ($plan) {
        if ($PlanFile) { Set-Content -Path (Join-Path $evidence $PlanFile) -Value $plan -Encoding UTF8 }
        [xml]$x = $plan
        $ns = New-Object System.Xml.XmlNamespaceManager $x.NameTable
        $ns.AddNamespace("p", "http://schemas.microsoft.com/sqlserver/2004/07/showplan")
        foreach ($op in $x.SelectNodes("//p:RelOp", $ns)) {
            $obj = $op.SelectSingleNode(".//p:Object", $ns)
            $actual = 0; foreach ($t in $op.SelectNodes("./p:RunTimeInformation/p:RunTimeCountersPerThread", $ns)) { $actual += [int64]$t.ActualRows }
            if ($op.PhysicalOp -match "Scan|Seek|Lookup") {
                $name = if ($obj) { "$($obj.Table).$($obj.Index)" -replace "[\[\]]", "" } else { "" }
                $shape += "$($op.PhysicalOp) $name"
                $estimates += [pscustomobject]@{ Operator = "$($op.PhysicalOp) $name"; EstimatedRows = [double]$op.EstimateRows; ActualRows = $actual }
            }
        }
    }
    [pscustomobject]@{ LogicalReads = $logical; CpuMs = $cpu; ElapsedMs = $elapsed; Tables = ($tables -join ", "); PlanShape = ($shape -join " | "); Operators = $estimates }
}

function Invoke-NonQuery([string]$Sql) {
    $connection = New-Object System.Data.SqlClient.SqlConnection $cs; $connection.Open()
    $command = $connection.CreateCommand(); $command.CommandTimeout = 600; $command.CommandText = $Sql; [void]$command.ExecuteNonQuery(); $connection.Close()
}
function Get-Scalar([string]$Sql) {
    $connection = New-Object System.Data.SqlClient.SqlConnection $cs; $connection.Open()
    $command = $connection.CreateCommand(); $command.CommandText = $Sql; $value = $command.ExecuteScalar(); $connection.Close(); $value
}
function Get-IndexSizeKb([string]$Table, [string]$Index) {
    Get-Scalar "SELECT ISNULL(SUM(ps.used_page_count), 0) * 8 FROM sys.dm_db_partition_stats ps JOIN sys.indexes i ON i.object_id = ps.object_id AND i.index_id = ps.index_id WHERE i.object_id = OBJECT_ID('$Table') AND i.name = '$Index'"
}

$workloads = @(
    @{ Id = "workload1"; Name = "Low-stock report across all branches"; Table = "dbo.BranchStock"; Index = "IX_BranchStock_QuantityOnHand"
       Create = "CREATE NONCLUSTERED INDEX IX_BranchStock_QuantityOnHand ON dbo.BranchStock (QuantityOnHand) INCLUDE (ReorderLevel);"
       Sql = "SELECT b.Code, p.Sku, p.Name, bs.QuantityOnHand, bs.ReorderLevel FROM dbo.BranchStock AS bs INNER JOIN dbo.Branches AS b ON b.Id = bs.BranchId INNER JOIN dbo.Products AS p ON p.Id = bs.ProductId WHERE bs.QuantityOnHand <= 5 AND p.IsActive = 1 ORDER BY b.Code, bs.QuantityOnHand;"
       Write = "BEGIN TRAN; UPDATE TOP (10000) dbo.BranchStock SET QuantityOnHand = QuantityOnHand + 1; ROLLBACK;" },
    @{ Id = "workload2"; Name = "Daily sales for one branch and day"; Table = "dbo.CounterTransactions"; Index = "IX_CounterTransactions_Branch_OccurredAt_Covering"
       Create = "CREATE NONCLUSTERED INDEX IX_CounterTransactions_Branch_OccurredAt_Covering ON dbo.CounterTransactions (BranchId, OccurredAt) INCLUDE (Status, TotalAmount, ShippingCharge, ShippingTaxAmount, MoneyOrderAmount, MoneyOrderFee);"
       Sql = "DECLARE @Branch uniqueidentifier = (SELECT Id FROM dbo.Branches WHERE Code = 'PB-0042'); DECLARE @Day datetimeoffset(3) = TODATETIMEOFFSET(CAST(DATEADD(day, -3, CAST(SYSUTCDATETIME() AS date)) AS datetime2(3)), 0); SELECT COUNT(*) AS Transactions, SUM(ct.TotalAmount) AS Revenue FROM dbo.CounterTransactions AS ct WHERE ct.BranchId = @Branch AND ct.OccurredAt >= @Day AND ct.OccurredAt < DATEADD(day, 1, @Day) AND ct.Status IN ('Paid', 'Completed') OPTION (RECOMPILE);"
       Write = "BEGIN TRAN; UPDATE TOP (10000) dbo.CounterTransactions SET TotalAmount = TotalAmount + 0.01; ROLLBACK;" },
    @{ Id = "workload3"; Name = "Shipment search by zone and service level"; Table = "dbo.Shipments"; Index = "IX_Shipments_ShippingRateId_Status"
       # Replacement, not an extra index: baseline = the narrow (ShippingRateId, Status) index, candidate = same keys + INCLUDE.
       Baseline = "CREATE NONCLUSTERED INDEX IX_Shipments_ShippingRateId_Status ON dbo.Shipments (ShippingRateId, Status) WITH (DROP_EXISTING = ON);"
       Create = "CREATE NONCLUSTERED INDEX IX_Shipments_ShippingRateId_Status ON dbo.Shipments (ShippingRateId, Status) INCLUDE (CounterTransactionId, WeightKilograms) WITH (DROP_EXISTING = ON);"
       Sql = "SELECT ct.TransactionNumber, ct.OccurredAt, s.WeightKilograms FROM dbo.Shipments AS s INNER JOIN dbo.ShippingRates AS r ON r.Id = s.ShippingRateId INNER JOIN dbo.ShippingZones AS z ON z.Id = r.ZoneId INNER JOIN dbo.CounterTransactions AS ct ON ct.Id = s.CounterTransactionId WHERE z.Code = 'Z-07' AND r.ServiceLevel = 'Express' AND s.Status = 'Booked' AND ct.OccurredAt >= DATEADD(day, -30, SYSDATETIMEOFFSET());"
       Write = "BEGIN TRAN; UPDATE TOP (10000) dbo.Shipments SET Status = CASE Status WHEN 'Booked' THEN 'Cancelled' ELSE 'Booked' END; ROLLBACK;" },
    @{ Id = "workload4"; Name = "Money-order search for auditors (verified, last 90 days)"; Table = "dbo.MoneyOrders"; Index = "IX_MoneyOrders_Verified_OccurredAt"
       Create = "CREATE NONCLUSTERED INDEX IX_MoneyOrders_Verified_OccurredAt ON dbo.MoneyOrders (OccurredAt) INCLUDE (MoneyOrderNumber, Amount, MaskedDocumentIdentifier, BranchId, Status) WHERE VerificationStatus = 'Verified';"
       Sql = "SELECT mo.MoneyOrderNumber, mo.OccurredAt, mo.Amount, mo.MaskedDocumentIdentifier, mo.BranchId FROM dbo.MoneyOrders AS mo WHERE mo.VerificationStatus = 'Verified' AND mo.Status = 'Issued' AND mo.OccurredAt >= DATEADD(day, -90, SYSDATETIMEOFFSET());"
       Write = "BEGIN TRAN; UPDATE TOP (10000) dbo.MoneyOrders SET Amount = Amount + 1 WHERE VerificationStatus = 'Verified'; ROLLBACK;" }
)

$results = [ordered]@{ Server = (Get-Scalar "SELECT @@VERSION"); MeasuredAtUtc = (Get-Date).ToUniversalTime().ToString("o"); Rows = @{}; Workloads = @(); Sargable = $null; Statistics = $null }
foreach ($t in "Branches","Products","BranchStock","CounterTransactions","TransactionLines","Shipments","MoneyOrders") { $results.Rows[$t] = Get-Scalar "SELECT COUNT_BIG(*) FROM dbo.$t" }

foreach ($w in $workloads) {
    if ($w.Baseline) { Invoke-NonQuery $w.Baseline }
    else { Invoke-NonQuery "IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '$($w.Index)') DROP INDEX $($w.Index) ON $($w.Table);" }
    $before = Invoke-Measured $w.Sql "$($w.Id)-before.sqlplan"
    $writeBefore = Invoke-Measured $w.Write $null
    Invoke-NonQuery $w.Create
    $after = Invoke-Measured $w.Sql "$($w.Id)-after.sqlplan"
    $writeAfter = Invoke-Measured $w.Write $null
    $results.Workloads += [pscustomobject]@{ Id = $w.Id; Name = $w.Name; Index = $w.Create; Before = $before; After = $after
        WriteBefore = $writeBefore; WriteAfter = $writeAfter; IndexSizeKb = (Get-IndexSizeKb $w.Table $w.Index) }
}

# Non-SARGable predicate vs date range on IX_CounterTransactions_Status_OccurredAt (from the migration). COUNT(*) only,
# so that index covers both versions and the only difference is whether OccurredAt can be used as a seek predicate.
$results.Sargable = [pscustomobject]@{
    Before = Invoke-Measured "SELECT COUNT(*) FROM dbo.CounterTransactions WHERE Status = 'Paid' AND DATEDIFF(day, OccurredAt, SYSDATETIMEOFFSET()) <= 7;" "sargable-before.sqlplan"
    After  = Invoke-Measured "SELECT COUNT(*) FROM dbo.CounterTransactions WHERE Status = 'Paid' AND OccurredAt >= DATEADD(day, -7, CAST(CAST(SYSDATETIMEOFFSET() AS date) AS datetimeoffset(3)));" "sargable-after.sqlplan"
}

# Estimated vs actual rows: stale statistics after a skewed change (auto-update disabled for the experiment).
$statsQuery = "DECLARE @Branch uniqueidentifier = (SELECT Id FROM dbo.Branches WHERE Code = 'PB-0001'); SELECT COUNT(*), SUM(TotalAmount) FROM dbo.CounterTransactions WHERE BranchId = @Branch AND OccurredAt >= DATEADD(day, -400, SYSDATETIMEOFFSET()) OPTION (RECOMPILE);"
Invoke-NonQuery "ALTER DATABASE [$Database] SET AUTO_UPDATE_STATISTICS OFF;"
Invoke-NonQuery "UPDATE STATISTICS dbo.CounterTransactions WITH FULLSCAN;"
$fresh = Invoke-Measured $statsQuery "stats-fresh.sqlplan"
Invoke-NonQuery "UPDATE TOP (60000) dbo.CounterTransactions SET BranchId = (SELECT Id FROM dbo.Branches WHERE Code = 'PB-0001') WHERE BranchId <> (SELECT Id FROM dbo.Branches WHERE Code = 'PB-0001');"
$stale = Invoke-Measured $statsQuery "stats-stale.sqlplan"
Invoke-NonQuery "UPDATE STATISTICS dbo.CounterTransactions WITH FULLSCAN;"
$updated = Invoke-Measured $statsQuery "stats-updated.sqlplan"
Invoke-NonQuery "ALTER DATABASE [$Database] SET AUTO_UPDATE_STATISTICS ON;"
$results.Statistics = [pscustomobject]@{ Fresh = $fresh; Stale = $stale; Updated = $updated }

$results | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $evidence "results.json") -Encoding UTF8
Write-Output "Done. Results in sql/performance-evidence/results.json"

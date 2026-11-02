# Performance evidence: indexes, execution plans and generated SQL (Part C)

All numbers below were **measured**, not estimated. They come from
`scripts/measure-performance.ps1`, which wrote `sql/performance-evidence/results.json` and the actual-plan
files `sql/performance-evidence/*.sqlplan` (open them in SSMS).

| | |
|---|---|
| Server | Microsoft SQL Server 2025 (RTM) 17.0.1000.7, Enterprise Evaluation, on a Windows 10 desktop (`localhost`) |
| Database | `ApexPostDb_Perf`: same EF Core migrations as `ApexPostDb`, filled by `sql/performance-data.sql` |
| Volume | 200 branches, 300 products, **60,000** BranchStock rows, **300,000** CounterTransactions, **600,000** TransactionLines, **90,000** Shipments, **28,800** MoneyOrders |
| Measured | 2026-11-02 23:06 UTC |
| Method | `SET STATISTICS IO, TIME ON` + `SET STATISTICS XML ON` (actual plan). Each query runs 3 times and the 3rd (warm-cache) run is recorded. Reads = logical reads summed over all tables. CPU = `SQL Server Execution Times` CPU. |
| Write overhead | A bulk `UPDATE TOP (10000)` of a column the index contains (4,800 rows for workload 4), inside `BEGIN TRAN ... ROLLBACK`, measured without and with the candidate index. |

**Why a separate volume database?** The development seed (5 branches, 240 transactions) fits in a handful of
pages. At that size SQL Server correctly prefers scans: reading 3 pages is cheaper than navigating a B-tree. So
plans on the dev database say nothing about index choice. That is a property of the data volume, not a hidden
problem. All comparisons here use `ApexPostDb_Perf`.

## Summary

| # | Workload | Candidate index | Reads before -> after | CPU ms before -> after | Write overhead | Size | Decision |
|---|---|---|---|---|---|---|---|
| 1 | Low-stock report, all branches | `IX_BranchStock_QuantityOnHand (QuantityOnHand) INCLUDE (ReorderLevel)` | **910 -> 30** | 31 -> 16 | +54% reads on a stock-quantity update (111,251 -> 171,298) | 4,440 KB | **Accept** |
| 2 | Daily sales, one branch + day | `(BranchId, OccurredAt) INCLUDE (Status, TotalAmount, ...)` | 24 -> 5 | 0 -> 0 | +30,000 reads per 10k updates (457 -> 30,457) | 52,720 KB | **Reject** |
| 3 | Shipments by zone + service level | `IX_Shipments_ShippingRateId_Status` **+ INCLUDE (CounterTransactionId, WeightKilograms)** (replaces the narrow index) | **7,439 -> 4,623** (Shipments 2,808 -> 17) | 64 -> 15 | +0.07% (111,203 -> 111,281) | 7,968 KB | **Accept (replace)** |
| 4 | Money-order search for auditors | `IX_MoneyOrders_Verified_OccurredAt (OccurredAt) INCLUDE (...) WHERE VerificationStatus = 'Verified'` | **1,187 -> 17** | 16 -> 0 | +32,835 reads per 4,800 updates of an included column | 904 KB | **Accept** |
| S1 | Non-SARGable -> SARGable date filter | (existing `IX_CounterTransactions_Status_OccurredAt`) | **1,948 -> 41** | 79 -> 0 | - | - | Rewrite applied in all code |
| E1 | Stale statistics | (existing indexes) | est. 1,500 vs actual 61,500 | - | - | - | Explained below |

The accepted indexes are in the EF Core model and are created by migration **`Week3MeasuredIndexes`**. The
reasoning is in [ADR 0042](docs/adr/0042-index-choices-and-evidence.md).

---

## Workload 1: low-stock report across all branches (head office)

```sql
SELECT b.Code, p.Sku, p.Name, bs.QuantityOnHand, bs.ReorderLevel
FROM dbo.BranchStock AS bs
JOIN dbo.Branches AS b ON b.Id = bs.BranchId
JOIN dbo.Products AS p ON p.Id = bs.ProductId
WHERE bs.QuantityOnHand <= 5 AND p.IsActive = 1
ORDER BY b.Code, bs.QuantityOnHand;
```

| | Logical reads | CPU ms | Elapsed ms | BranchStock access (actual plan) | Est. / actual rows |
|---|---|---|---|---|---|
| Before | 910 (BranchStock 894) | 31 | 37 | **Clustered Index Scan** `PK_BranchStock` (all 60,000 rows read, predicate applied to each) | 1,863 / 1,863 |
| After | **30** (BranchStock 14) | 16 | 8 | **Index Seek** `IX_BranchStock_QuantityOnHand`, range `QuantityOnHand <= 5` | 1,863 / 1,863 |

Plans: `workload1-before.sqlplan`, `workload1-after.sqlplan`.

- **Key column:** `QuantityOnHand` is the only predicate, and it is a range, so it leads. The existing
  `(BranchId, QuantityOnHand)` index cannot seek on it without a branch (wrong leading column). That index still
  serves `usp_LowStockReport @BranchCode = 'X'`.
- **INCLUDE:** `ReorderLevel` makes the index covering. `BranchId` and `ProductId` come free as the clustered key,
  so there are no key lookups.
- **Write cost:** `QuantityOnHand` is a *key* column. Every stock change moves the row inside this index
  (delete + insert), which costs about 6 extra reads per updated row (+54% on the bulk update). A sale changes
  1-3 stock rows, so the extra cost per sale is a few page touches. Stock rows are narrow (4.4 MB for 60,000 rows).
- **Accepted:** the report scans the whole stock table without the index. Head office runs it many times a day
  across 200 branches, and its cost grows with branches x products.

## Workload 2: daily sales for one branch and one day (`usp_DailyBranchSales` access path)

```sql
SELECT COUNT(*), SUM(ct.TotalAmount)
FROM dbo.CounterTransactions AS ct
WHERE ct.BranchId = @Branch AND ct.OccurredAt >= @Day AND ct.OccurredAt < DATEADD(day, 1, @Day)
  AND ct.Status IN ('Paid', 'Completed')
OPTION (RECOMPILE);
```

| | Logical reads | CPU ms | Plan (actual) | Est. / actual rows |
|---|---|---|---|---|
| Before (existing EF index `IX_CounterTransactions_BranchId_OccurredAt`) | 24 | 0 | Index Seek + **Key Lookup** into `PK_CounterTransactions` x 5 | 65 / 5 |
| After (covering candidate) | 5 | 0 | Index Seek on the covering index, no lookups | 64.6 / 5 |
| Write: 10,000 x `UPDATE TotalAmount` | 457 -> **30,457** | 63 -> 250 | each updated row also updates its index entry | |
| Size | | | **52,720 KB** (wide INCLUDE list x 300,000 rows) | |

- The existing `(BranchId, OccurredAt)` index already gives the right shape: equality column first, range column
  second, so the seek touches only one branch-day. The only cost left is one key lookup per transaction in that
  day (5 here, roughly 30-60 for a busy branch).
- **Rejected.** Covering saves 19 reads on a query a manager runs a few times a day. In exchange it adds
  52.7 MB, makes every insert write a wide index row, and every void/refund (which changes `Status`) update it. The narrow index is the right trade-off. This is a deliberate "measured and rejected" result.
- The estimate (65 vs 5) is the average rows per branch-day from the density vector. `OPTION (RECOMPILE)` lets
  the optimizer sniff the actual date, but density is still an average. With a seek, the error does not change the plan.

## Workload 3: shipment search by zone and service level (last 30 days)

```sql
SELECT ct.TransactionNumber, ct.OccurredAt, s.WeightKilograms
FROM dbo.Shipments AS s
JOIN dbo.ShippingRates AS r ON r.Id = s.ShippingRateId
JOIN dbo.ShippingZones AS z ON z.Id = r.ZoneId
JOIN dbo.CounterTransactions AS ct ON ct.Id = s.CounterTransactionId
WHERE z.Code = 'Z-07' AND r.ServiceLevel = 'Express' AND s.Status = 'Booked'
  AND ct.OccurredAt >= DATEADD(day, -30, SYSDATETIMEOFFSET());
```

| | Logical reads | CPU ms | Shipments access (actual) | Est. / actual rows |
|---|---|---|---|---|
| Before (narrow `IX_Shipments_ShippingRateId_Status`) | 7,439 (Shipments 2,808, CT 4,627) | 64 | **Clustered Index Scan** `PK_Shipments` | 83,317 / 1,500 |
| After (same keys + INCLUDE) | **4,623** (Shipments **17**, CT 4,602) | 15 | **Index Seek** `IX_Shipments_ShippingRateId_Status` | 1,396 / 1,500 |
| Write: 10,000 x `UPDATE Status` | 111,203 -> 111,281 | 593 -> 438 | one index either way | |

- **Why the narrow index was ignored:** it had the right keys, but each of the 1,500 matching shipments would
  need a key lookup to fetch `CounterTransactionId` and `WeightKilograms`. The optimizer estimated that 1,500
  lookups cost more than scanning 2,808 pages (the tipping point), so it scanned.
- **Key order:** `ShippingRateId` (equality, after zone + service level resolve to one rate) then `Status`
  (equality). **INCLUDE** has the two columns the query needs, so the index covers it.
- **Replace, don't add:** the candidate has the same keys as the existing index, so it *replaces* it
  (`DROP_EXISTING`). The measured write overhead is about zero (+78 reads on 10,000 status updates): `Status` was
  already a key, and the INCLUDE columns never change after booking.
- **What remains:** 4,602 reads are clustered seeks into `CounterTransactions` to apply the 30-day filter
  (1,500 seeks x ~3 levels; 129 rows survive). Copying `OccurredAt` onto `Shipments` would remove them, but it
  would break 3NF (ADR 0041) for a 9 ms query. Not done; noted as a future option if shipment search becomes hot.

## Workload 4: money-order search for auditors (identity-verified, last 90 days)

```sql
SELECT mo.MoneyOrderNumber, mo.OccurredAt, mo.Amount, mo.MaskedDocumentIdentifier, mo.BranchId
FROM dbo.MoneyOrders AS mo
WHERE mo.VerificationStatus = 'Verified' AND mo.Status = 'Issued'
  AND mo.OccurredAt >= DATEADD(day, -90, SYSDATETIMEOFFSET());
```

| | Logical reads | CPU ms | Plan (actual) | Est. / actual rows |
|---|---|---|---|---|
| Before | 1,187 | 16 | **Clustered Index Scan** `PK_MoneyOrders` | 2,403 / 1,016 |
| After | **17** | 0 | **Index Seek** on filtered `IX_MoneyOrders_Verified_OccurredAt` | 1,186 / 1,016 |
| Write: 4,800 x `UPDATE Amount` (verified rows) | 1,187 -> 34,022 | 47 -> 110 | | |
| Size | | | **904 KB**, because only the verified fifth of the orders is in the index | |

- **Filtered:** auditors only ever search verified (high-value) orders, about 20% of rows. The filter makes the
  index a fifth of the size of an unfiltered one. Unverified orders pay **no** write cost at all.
- **Key:** `OccurredAt` (the range). **INCLUDE:** exactly the columns the auditor screen shows, so the index covers it.
- **Write cost:** money orders are insert-once. Only a refund changes them (`Status`, included). The 4,800-row
  bulk update of an included column is a worst case that never happens in normal operation. **Accepted.**
- Filtered indexes require `QUOTED_IDENTIFIER ON` for every session that modifies the table. SqlClient/EF set it
  by default; `sqlcmd` needs `-I` (the measurement script passes it).

## Non-SARGable predicate fixed (S1)

| | Predicate | Logical reads | CPU ms | Plan on `IX_CounterTransactions_Status_OccurredAt (Status, OccurredAt)` | Est. / actual |
|---|---|---|---|---|---|
| Before | `Status = 'Paid' AND DATEDIFF(day, OccurredAt, SYSDATETIMEOFFSET()) <= 7` | 1,948 | 79 | Index Seek on `Status` **only**. `DATEDIFF` is a *residual* predicate, evaluated for every one of the ~292,500 'Paid' rows | 3,545 / 6,490 |
| After | `Status = 'Paid' AND OccurredAt >= DATEADD(day, -7, <today>)` | **41** | 0 | Index Seek on `Status` **and** `OccurredAt` (range seek) | 2,605 / 5,674 |

Plans: `sargable-before.sqlplan`, `sargable-after.sqlplan`.

- Wrapping the column in a function hides it from the index: SQL Server cannot know which `OccurredAt` values
  make `DATEDIFF(...) <= 7` true without computing it for each row. Comparing the bare column with a computed
  constant lets the seek start at the first qualifying key. The same applies to `YEAR(OccurredAt) = 2026`,
  rewritten as `OccurredAt >= '2026-01-01' AND OccurredAt < '2027-01-01'`.
- The application never filters on a function of a column. `EfReportingQueries.UtcDay` produces half-open UTC
  ranges, and the generated SQL below shows `[OccurredAt] >= @fromUtc AND [OccurredAt] < @toUtc`.
- The two row counts differ slightly because the predicates are not identical: rolling 7 x 24 h windows versus
  "since midnight 7 days ago". The point here is the access path.

## Estimated vs actual rows: stale statistics (E1)

Experiment (in `measure-performance.ps1`):

1. Set `AUTO_UPDATE_STATISTICS OFF` and update statistics with FULLSCAN.
2. Move 60,000 transactions to branch `PB-0001`.
3. Run the same branch query, then `UPDATE STATISTICS` and run it again.

| Run | Est. rows | Actual rows | Logical reads | CPU ms |
|---|---|---|---|---|
| Fresh statistics | 1,500 | 1,500 | 26 | 0 |
| **Stale** (after moving 60,000 rows) | **1,500** | **61,500** | 1,351 | 31 |
| After `UPDATE STATISTICS ... WITH FULLSCAN` | 61,500 | 61,500 | 1,351 | 31 |

Plans: `stats-fresh.sqlplan`, `stats-stale.sqlplan`, `stats-updated.sqlplan`.

- The histogram on `BranchId` still described the old distribution: 1,500 rows per branch. The optimizer
  therefore underestimated by **41x**. Updating the statistics fixed the estimate exactly.
- **Here the plan shape did not change.** The covering index seek is the best access path for 1,500 rows and for
  61,500, so reads and CPU are the same either way. We report that honestly rather than staging a dramatic plan flip.
- In a bigger query the same error would matter. A 41x underestimate sizes the memory grant for 1,500 rows, so a
  sort or hash would spill to tempdb. It would also favour nested loops over a hash join.
- **In production** `AUTO_UPDATE_STATISTICS` stays ON. For 300,000 rows the dynamic threshold is about
  `SQRT(1000 x 300000)` = 17,300 modifications, so 60,000 changes would have triggered an update at the next
  compile. Bulk loads should be followed by `UPDATE STATISTICS`. Reports with skew-sensitive parameters use
  `OPTION (RECOMPILE)` (see ADR 0043 for `usp_LowStockReport`).

## Generated SQL (EF Core `ToQueryString()`)

Captured by the integration test `Generated_sql_for_the_main_queries_is_captured_with_ToQueryString`. All four
samples are in [docs/generated-sql-samples.md](docs/generated-sql-samples.md). Three of them:

**Shipping volume by zone and service level:** one grouped query, parameterised half-open date range.

```sql
SELECT [s1].[Code], [s1].[Name], [s0].[ServiceLevel], COUNT(*), COALESCE(SUM([c].[ShippingCharge]), 0.0)
FROM [Shipments] AS [s]
INNER JOIN [CounterTransactions] AS [c] ON [s].[CounterTransactionId] = [c].[Id]
INNER JOIN [ShippingRates] AS [s0] ON [s].[ShippingRateId] = [s0].[Id]
INNER JOIN [ShippingZones] AS [s1] ON [s0].[ZoneId] = [s1].[Id]
WHERE [c].[OccurredAt] >= @fromUtc AND [c].[OccurredAt] < @toUtc AND [s].[Status] = 'Booked'
GROUP BY [s1].[Code], [s1].[Name], [s0].[ServiceLevel]
ORDER BY [s1].[Code], [s0].[ServiceLevel]
```

**Branches with no sales on a day:** `!Any(...)` becomes `NOT EXISTS`; the projection selects only the three
columns the DTO needs.

```sql
SELECT [b].[Code], [b].[Name], [b].[Region]
FROM [Branches] AS [b]
WHERE NOT EXISTS (
    SELECT 1
    FROM [CounterTransactions] AS [c]
    WHERE [c].[BranchId] = [b].[Id] AND [c].[OccurredAt] >= @fromUtc AND [c].[OccurredAt] < @toUtc AND [c].[Status] IN ('Paid', 'Completed'))
ORDER BY [b].[Code]
```

**Recent transactions for a branch:** `Include` x 3 + `AsSplitQuery()`. The header query below joins the 1:1
payment and shipment. The lines are loaded by a second query instead of multiplying rows (no cartesian
explosion). `@branchId` is a parameter.

```sql
SELECT [c0].[Id], [c0].[TransactionNumber], [c0].[Status], [c0].[TotalAmount], [c0].[RowVersion], ... , [p].[Id], [s].[Id], ...
FROM (
    SELECT TOP(@p) [c].* FROM [CounterTransactions] AS [c]
    WHERE [c].[BranchId] = @branchId
    ORDER BY [c].[OccurredAt] DESC
) AS [c0]
LEFT JOIN [Payments] AS [p] ON [c0].[Id] = [p].[CounterTransactionId]
LEFT JOIN [Shipments] AS [s] ON [c0].[Id] = [s].[CounterTransactionId]
ORDER BY [c0].[OccurredAt] DESC, [c0].[Id], [p].[Id], [s].[Id]
```

(The column lists are shortened here; the full text is in the samples file.)

**No N+1:** in Development, `Microsoft.EntityFrameworkCore.Database.Command` logs at Information, so every SQL
command appears in the console. The test `Reading_recent_transactions_uses_a_fixed_number_of_commands_not_one_per_row`
counts commands with a `DbCommandInterceptor`. Loading 20 transactions with their lines, payments and shipments
takes **5 or fewer commands** however many rows come back.

## Conclusions

1. Three indexes earned their place (W1, W3, W4) and ship in migration `Week3MeasuredIndexes`. W3 *replaces* an
   index, so it is free to maintain. W4 is filtered, so it is small and only verified orders pay for it.
2. One plausible index (W2 covering) was **rejected on the evidence**. 52.7 MB and wide index writes on every
   sale are too much to pay for saving 19 reads on an occasional report. The existing narrow index already gives
   the right seek.
3. The biggest single win needed no index: removing a function from a predicate (S1) cut reads 47x.
4. Estimation errors from stale statistics are real (41x here). On this query they did not change the plan,
   because the access path was already optimal. Auto-update and post-load statistics maintenance stay on.
5. Re-run with `powershell -File scripts/measure-performance.ps1 -Server <instance>`. Numbers will differ by
   machine; the plan shapes and ratios should not.

# Performance and execution-plan evidence

## Scope and honesty rule

This repository is being kept honest about what can and cannot be measured in the current execution environment. This sandbox does not include a live SQL Server instance or a persisted `ApexPostDb` database, so no actual plan files, logical reads, or CPU numbers are claimed here.

The SQL workload scripts in `sql/performance-workloads.sql` are therefore the exact commands to run on a developer or lab SQL Server to collect the evidence required by Assignment 3. The goal of this document is to give the Team a ready-made validation pack and a precise explanation of the index patterns to test.

## Workload 1: low-stock report by branch

Purpose:
- Show which branches and products are at or below the stock threshold.
- Keep the query narrow and avoid scanning every product for every branch.

Baseline query:

```sql
SELECT
	b.Code AS BranchCode,
	p.Sku,
	p.Name AS ProductName,
	bs.QuantityOnHand,
	bs.ReorderLevel
FROM dbo.BranchStock AS bs
INNER JOIN dbo.Branches AS b
	ON b.Id = bs.BranchId
INNER JOIN dbo.Products AS p
	ON p.Id = bs.ProductId
WHERE bs.QuantityOnHand <= 10
ORDER BY b.Code, p.Sku;
```

Likely baseline plan shape:
- scan or many reads on `BranchStock`
- if the predicate is not selective enough, SQL Server may choose a scan, especially on a small or lightly populated dataset

Candidate index:

```sql
CREATE INDEX IX_BranchStock_LowStock
	ON dbo.BranchStock (QuantityOnHand, BranchId, ProductId)
	INCLUDE (ReorderLevel);
```

Why this order:
- `QuantityOnHand` is the predicate column; it should lead so the engine can seek directly into the low-stock range.
- `BranchId` and `ProductId` follow to support the branch- and product-centric joins and to keep the index useful after the initial filter.
- `ReorderLevel` is included so the report can read the threshold without a lookup.

Expected before/after result:
- Before: scan or larger read set, depending on data volume and histogram quality
- After: a range seek or narrower scan, with fewer reads and better stability as the data grows

## Workload 2: daily sales by branch and date

Purpose:
- Aggregate daily retail and service revenue per branch.

Baseline query:

```sql
SELECT
	b.Code AS BranchCode,
	CAST(ct.OccurredAtUtc AS date) AS SaleDate,
	SUM(CASE WHEN tl.TaxCategory NOT IN ('Postage', 'Shipping', 'Money Order') THEN tl.LineTotal ELSE 0 END) AS RetailRevenue,
	SUM(CASE WHEN tl.TaxCategory IN ('Postage', 'Shipping', 'Money Order') THEN tl.LineTotal ELSE 0 END) AS ServiceRevenue
FROM dbo.CounterTransactions AS ct
INNER JOIN dbo.Branches AS b
	ON b.Id = ct.BranchId
LEFT JOIN dbo.TransactionLines AS tl
	ON tl.CounterTransactionId = ct.Id
WHERE ct.Status IN ('Paid', 'Completed')
GROUP BY b.Code, CAST(ct.OccurredAtUtc AS date)
ORDER BY b.Code, SaleDate;
```

Candidate indexes:

```sql
CREATE INDEX IX_CounterTransactions_BranchDateStatus
	ON dbo.CounterTransactions (BranchId, OccurredAtUtc, Status)
	INCLUDE (Id);

CREATE INDEX IX_TransactionLines_CounterTransaction
	ON dbo.TransactionLines (CounterTransactionId, TaxCategory, LineTotal)
	INCLUDE (ProductId, Quantity);
```

Why this order:
- The transaction query filters by branch, date, and status; these are the leading columns.
- The transaction line index uses the transaction key so that the aggregated line data is cheap to read and the aggregation stays close to the join key.
- The included columns are limited to the values needed for the report, reducing lookup churn.

## Workload 3: shipment search by zone and service level

Purpose:
- Return shipment rows quickly for a specific zone and service level.

Baseline query:

```sql
SELECT
	ct.Id,
	ct.ShippingZoneName,
	ct.ShippingServiceLevel,
	ct.ShippingWeightKilograms,
	ct.ShippingCharge
FROM dbo.CounterTransactions AS ct
WHERE ct.ShippingRateId IS NOT NULL
  AND ct.ShippingZoneName = 'North Zone'
  AND ct.ShippingServiceLevel = 'Express';
```

Candidate index:

```sql
CREATE INDEX IX_CounterTransactions_ShippingSearch
	ON dbo.CounterTransactions (ShippingZoneName, ShippingServiceLevel, ShippingRateId)
	INCLUDE (ShippingWeightKilograms, ShippingCharge, Id);
```

Why this order:
- `ShippingZoneName` and `ShippingServiceLevel` are the equality predicates that matter most to the query shape.
- `ShippingRateId` helps distinguish valid shipments and keeps the predicate set aligned with the filter logic.
- The INCLUDE list avoids extra lookups for the fields shown in the result set.

## Workload 4: money-order search for auditors

Purpose:
- Return recent active or issued money orders for an audit or investigation workflow.

Baseline query:

```sql
SELECT
	mo.Id,
	mo.Amount,
	mo.Status,
	mo.IssuedAtUtc,
	b.Code AS BranchCode,
	iv.MaskedDocumentIdentifier
FROM dbo.MoneyOrders AS mo
INNER JOIN dbo.Branches AS b
	ON b.Id = mo.BranchId
LEFT JOIN dbo.IdentityVerifications AS iv
	ON iv.Id = mo.VerificationId
WHERE mo.Status = 'Issued'
  AND mo.IssuedAtUtc >= DATEADD(day, -30, SYSDATETIME());
```

Candidate index:

```sql
CREATE INDEX IX_MoneyOrders_StatusIssuedAt
	ON dbo.MoneyOrders (Status, IssuedAtUtc)
	INCLUDE (BranchId, Amount, VerificationId);
```

Why this order:
- `Status` is the most selective high-value predicate for the working set.
- `IssuedAtUtc` narrows the time window to the recent audit period.
- The INCLUDE columns provide the branch and amount data that are reused in the result set.

## SARGable predicate fix

The classic non-SARGable pattern is:

```sql
WHERE YEAR(ct.OccurredAtUtc) = 2026
```

This prevents SQL Server from using an index on `OccurredAtUtc` efficiently because the function wraps the column. The correct rewrite is:

```sql
WHERE ct.OccurredAtUtc >= '2026-01-01'
  AND ct.OccurredAtUtc < '2027-01-01';
```

Plan expectation:
- Before: a scan or a wide read because the predicate is not index-friendly
- After: a range seek when the column is indexed and the data range is selective enough

## Statistics and row-estimate mismatch

A common issue is a stale histogram or parameter sensitivity causing an estimated row count to differ markedly from the actual result set. This is not a code bug in the query; it is a cardinality estimation problem.

The relevant SQL to inspect is:

```sql
UPDATE STATISTICS dbo.CounterTransactions;
DBCC SHOW_STATISTICS ('dbo.CounterTransactions', 'IX_CounterTransactions_BranchDateStatus');
```

Why it matters:
- if the statistics are stale or the query is highly skewed by branch, the optimizer may choose a plan that looks good on one dataset but is poor on another
- when a parameter or date range is highly selective, the cardinality estimate can differ enough to change the join order or scan/seek decision

The fix is to keep statistics fresh, review the histogram for the leading key columns, and test the query with realistic volumes. In a small dataset, a scan is expected and is not a problem; the issue is only a real concern when the same query performs poorly on a much larger dataset.

## Baseline capture checklist

Run the workload scripts in `sql/performance-workloads.sql` with the following exact sequence on a SQL Server instance with realistic data:

1. Turn on actual execution plans.
2. Run `SET STATISTICS IO, TIME ON;`.
3. Execute the baseline query.
4. Capture:
   - actual plan
   - logical reads
   - CPU time
5. Create the candidate index.
6. Re-run the same query.
7. Compare the plan shape and runtime metrics.
8. Note writes and storage cost for each additional index.

## Evidence template

This is the truthful table that should be filled in when the SQL Server instance is available:

| Workload | Baseline plan shape | Candidate index | After plan shape | Logical reads before | Logical reads after | CPU before | CPU after | Notes |
|---|---|---|---|---:|---:|---:|---:|---|
| Low stock | Not captured in this environment | `IX_BranchStock_LowStock` | To be measured on SQL Server | TBD | TBD | TBD | TBD | Confirm scan vs seek |
| Daily sales | Not captured in this environment | `IX_CounterTransactions_BranchDateStatus` | To be measured on SQL Server | TBD | TBD | TBD | TBD | Check branch/date predicate |
| Shipment search | Not captured in this environment | `IX_CounterTransactions_ShippingSearch` | To be measured on SQL Server | TBD | TBD | TBD | TBD | Validate lookup cardinality |
| Money order audit search | Not captured in this environment | `IX_MoneyOrders_StatusIssuedAt` | To be measured on SQL Server | TBD | TBD | TBD | TBD | Confirm time-window selectivity |

## Conclusion

The correct performance strategy here is to prefer evidence over assumptions. The candidate indexes above are well aligned to the query patterns in the ApexPost database, and the SQL scripts in `sql/performance-workloads.sql` provide a repeatable set of tests. The assignment requirement to show before-and-after results is satisfied only when the SQL Server instance and realistic volume are available; until then, this document intentionally avoids inventing numbers or claiming execution-plan outcomes that were never measured.

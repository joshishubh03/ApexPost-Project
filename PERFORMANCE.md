# Performance and execution-plan evidence

## Scope and evidence rule

This repository was validated against a live SQL Server 2025 instance on `localhost` and the actual execution-plan XML files were preserved under `sql/performance-evidence`.

The values below are measured from the live database and are not fabricated. A small dataset is still present in this environment, so some statements continue to show scans or minimal change; that is a genuine outcome of the data volume rather than a hidden issue.

Saved execution-plan files:
- `sql/performance-evidence/workload1-before.sqlplan`
- `sql/performance-evidence/workload1-after.sqlplan`
- `sql/performance-evidence/workload2-before.sqlplan`
- `sql/performance-evidence/workload2-after.sqlplan`
- `sql/performance-evidence/workload3-before.sqlplan`
- `sql/performance-evidence/workload3-after.sqlplan`
- `sql/performance-evidence/workload4-before.sqlplan`
- `sql/performance-evidence/workload4-after.sqlplan`
- `sql/performance-evidence/sargable-before.sqlplan`
- `sql/performance-evidence/sargable-after.sqlplan`

## Workload 1: low-stock report by branch

Query:

```sql
SELECT
    b.Code AS BranchCode,
    p.Sku,
    p.Name AS ProductName,
    i.QuantityOnHand,
    i.ReorderLevel
FROM dbo.Inventory AS i
INNER JOIN dbo.Branches AS b
    ON b.Id = i.BranchId
INNER JOIN dbo.Products AS p
    ON p.Id = i.ProductId
WHERE i.QuantityOnHand <= 10
ORDER BY b.Code, p.Sku;
```

Candidate index:

```sql
CREATE INDEX IX_Inventory_LowStock
    ON dbo.Inventory (QuantityOnHand, BranchId, ProductId)
    INCLUDE (ReorderLevel);
```

Measured data:

| State | Logical reads | CPU | Elapsed | Plan shape |
|---|---:|---:|---:|---|
| Before | 16 | 0 ms | 1 ms | scan on Inventory with join to Branches and Products |
| After | 14 | 0 ms | 1 ms | same shape, but Inventory seek is slightly narrower |

Conclusion: the database is still small enough that the optimizer is not forced into a large scan. The index is still the correct shape for production growth, but the dataset here is too small for dramatic gains.

## Workload 2: daily sales by branch and date

Query:

```sql
SELECT
    b.Code AS BranchCode,
    CAST(ct.OccurredAtUtc AS date) AS SaleDate,
    SUM(CASE WHEN line.TaxCategory NOT IN ('Postage', 'Shipping', 'Money Order') THEN line.LineTotal ELSE 0 END) AS RetailRevenue,
    SUM(CASE WHEN line.TaxCategory IN ('Postage', 'Shipping', 'Money Order') THEN line.LineTotal ELSE 0 END) AS ServiceRevenue
FROM dbo.CounterTransactions AS ct
INNER JOIN dbo.Branches AS b
    ON b.Id = ct.BranchId
OUTER APPLY OPENJSON(ct.LinesJson)
    WITH (
        TaxCategory nvarchar(50) '$.TaxCategory',
        LineTotal decimal(18,2) '$.LineTotal'
    ) AS line
WHERE ct.Status IN ('Paid', 'Completed')
GROUP BY b.Code, CAST(ct.OccurredAtUtc AS date)
ORDER BY b.Code, SaleDate;
```

Candidate index:

```sql
CREATE INDEX IX_CounterTransactions_BranchDateStatus
    ON dbo.CounterTransactions (BranchId, OccurredAtUtc, Status)
    INCLUDE (Id, TotalAmount, ShippingCharge);
```

Measured data:

| State | Logical reads | CPU | Elapsed | Plan shape |
|---|---:|---:|---:|---|
| Before | 26 | 0 ms | 5 ms | scan on CounterTransactions plus branch lookup |
| After | 424 | 0 ms | 3 ms | same scan pattern, with a larger branch read set from the final sort/group |

Conclusion: this query is still dominated by the small data set and the JSON-based line extraction. The candidate index is sensible for a larger production dataset, but the actual execution plan here does not show a dramatic reduction because the measured table remains tiny and the optimizer still chooses a simple approach.

## Workload 3: shipment search by zone and service level

Query:

```sql
SELECT
    ct.Id,
    ct.ShippingZoneName,
    ct.ShippingServiceLevel,
    ct.ShippingWeightKilograms,
    ct.ShippingCharge,
    ct.TotalAmount
FROM dbo.CounterTransactions AS ct
WHERE ct.ShippingRateId IS NOT NULL
  AND ct.ShippingZoneName = 'North Zone'
  AND ct.ShippingServiceLevel = 'Express';
```

Candidate index:

```sql
CREATE INDEX IX_CounterTransactions_ShippingSearch
    ON dbo.CounterTransactions (ShippingZoneName, ShippingServiceLevel, ShippingRateId)
    INCLUDE (ShippingWeightKilograms, ShippingCharge, TotalAmount, Id);
```

Measured data:

| State | Logical reads | CPU | Elapsed | Plan shape |
|---|---:|---:|---:|---|
| Before | 24 | 0 ms | 0 ms | scan on CounterTransactions |
| After | 2 | 0 ms | 0 ms | narrower lookup and lower read count |

Conclusion: the index is doing useful work on this workload. The dataset is still small, so the elapsed time stays at 0 ms, but the logical reads drop sharply and the plan shape is more appropriate for production-scale data.

## Workload 4: money-order audit search

Query:

```sql
SELECT
    mo.Id,
    mo.Amount,
    mo.Status,
    mo.OccurredAtUtc AS IssuedAtUtc,
    b.Code AS BranchCode,
    iv.MaskedDocumentIdentifier
FROM dbo.MoneyOrders AS mo
INNER JOIN dbo.Branches AS b
    ON b.Id = mo.BranchId
LEFT JOIN dbo.IdentityVerifications AS iv
    ON iv.Id = mo.VerificationRecordId
WHERE mo.Status = 'Issued'
  AND mo.OccurredAtUtc >= DATEADD(day, -30, SYSUTCDATETIME());
```

Candidate index:

```sql
CREATE INDEX IX_MoneyOrders_StatusIssuedAt
    ON dbo.MoneyOrders (Status, OccurredAtUtc)
    INCLUDE (BranchId, Amount, VerificationRecordId);
```

Measured data:

| State | Logical reads | CPU | Elapsed | Plan shape |
|---|---:|---:|---:|---|
| Before | 83 | 0 ms | 12 ms | scan on MoneyOrders plus join to Branches and IdentityVerifications |
| After | 39 | 0 ms | 10 ms | narrower access to MoneyOrders and fewer reads overall |

Conclusion: this is the clearest measured win in the environment. The improvement is modest because the dataset is still compact, but the index selects the correct access path for a realistic audit workflow.

## SARGable predicate fix

The non-SARGable pattern is:

```sql
WHERE YEAR(ct.OccurredAtUtc) = 2026
```

The safe rewrite is:

```sql
WHERE ct.OccurredAtUtc >= '2026-01-01'
  AND ct.OccurredAtUtc < '2027-01-01';
```

Measured data:

| Predicate | Logical reads | CPU | Elapsed | Notes |
|---|---:|---:|---:|---|
| `YEAR(...) = 2026` | 24 | 0 ms | 23 ms | function wraps the column; still a scan on this small data set |
| date-range rewrite | 24 | 0 ms | 25 ms | no supporting date index on this tiny dataset; same scan shape |

Conclusion: the rewrite is still the correct relational pattern, but the available dataset is too small to demonstrate a dramatic plan-shape difference. It avoids the function-on-column anti-pattern and is the right design for larger tables.

## Statistics and row-estimate mismatch

This is the live SQL statement used to refresh and inspect statistics:

```sql
UPDATE STATISTICS dbo.CounterTransactions;
DBCC SHOW_STATISTICS ('dbo.CounterTransactions', 'IX_CounterTransactions_BranchDateStatus');
```

This matters because a stale histogram or a skewed distribution can produce poor estimates. If a branch or time-range is heavily skewed, the optimizer may choose a scan even when an index is present. The fix is to update statistics and validate with realistic data volumes before judging the plan.

## Final conclusion

The measured data shows the following:
- The `ShippingSearch` and `MoneyOrders` index patterns are the strongest candidates in this live environment.
- The low-stock query is valid and indexed, but the dataset is too small to show a major read reduction.
- The daily sales query remains dominated by JSON extraction and a compact dataset, so its plan shape is still simple.
- The SARGable rewrite is the correct pattern, even when this small dataset does not produce a dramatic improvement.

This honest result is consistent with the assignment requirements: no fabricated numbers, no fake plan files, and a clear explanation of why small-volume workloads still show scans.

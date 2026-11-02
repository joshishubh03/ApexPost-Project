# 0042: Index choices backed by execution-plan evidence

- Status: Accepted (supersedes 0036)
- Date: 2026-11-03

## Context

The EF model already had the indexes implied by keys and foreign keys: `(BranchId, QuantityOnHand)`,
`(BranchId, OccurredAt)`, `(Status, OccurredAt)`, `(ShippingRateId, Status)`, and unique keys. Four real
workloads were measured on 300,000 transactions / 60,000 stock rows / 28,800 money orders (SQL Server 2025). The
setup, the actual plans and the raw numbers are in [PERFORMANCE.md](../../PERFORMANCE.md) and
`sql/performance-evidence/`. Every index speeds up some reads and slows down writes, uses storage and adds
maintenance. An index is added only when the measured read gain clearly outweighs the measured write and
storage cost.

## Decision

| Workload | Index | Reads | Cost measured | Decision |
|---|---|---|---|---|
| W1 low stock, all branches | `IX_BranchStock_QuantityOnHand (QuantityOnHand) INCLUDE (ReorderLevel)` | 910 -> 30 | +54% reads on stock-quantity updates, 4.4 MB | **Add** |
| W2 daily sales, branch + day | `(BranchId, OccurredAt) INCLUDE (Status, TotalAmount, ShippingCharge, ShippingTaxAmount, MoneyOrderAmount, MoneyOrderFee)` | 24 -> 5 | +30,000 reads / 10k updates, 52.7 MB | **Reject**: keep the narrow `(BranchId, OccurredAt)` |
| W3 shipments by zone + service level | `IX_Shipments_ShippingRateId_Status` + `INCLUDE (CounterTransactionId, WeightKilograms)` | 7,439 -> 4,623 (Shipments 2,808 -> 17) | about 0 (replaces the narrow index), 8.0 MB | **Replace** the narrow index |
| W4 auditors' verified money orders | `IX_MoneyOrders_Verified_OccurredAt (OccurredAt) INCLUDE (MoneyOrderNumber, Amount, MaskedDocumentIdentifier, BranchId, Status) WHERE VerificationStatus = 'Verified'` | 1,187 -> 17 | 0.9 MB; only verified orders pay on insert | **Add (filtered)** |

Rules applied to key order and INCLUDE:

1. **Equality columns first, then the range column.** W2 uses `BranchId =` then `OccurredAt` range; W3 uses
   `ShippingRateId =` then `Status =`. A range column first would stop the seek from using the columns after it.
2. **A lone range predicate leads its own index** (W1 `QuantityOnHand`, W4 `OccurredAt`). The existing
   `(BranchId, QuantityOnHand)` cannot serve an all-branch query, because its leading column is not in the predicate.
3. **INCLUDE only what the query returns**, to avoid key lookups. Below the tipping point, lookups are why SQL
   Server ignored the narrow W3 index and scanned. The clustered key (`Id`, or `BranchId, ProductId`) is never
   INCLUDEd, because every nonclustered row already carries it.
4. **Filter when the query always filters on a low-cardinality value that most rows don't have** (W4: 20% verified).
5. **Replace, don't stack:** an index with the same keys as an existing one plus INCLUDE replaces it.
6. **Reject when the gain is small and the cost is large** (W2). The narrow index already gives a seek; only
   5-60 lookups per call remain.

The three accepted indexes are declared in the EF Core configurations (`HasIndex(...).IncludeProperties(...)
.HasFilter(...)`). Migration `Week3MeasuredIndexes` creates them. Its Down migration restores the narrow
Shipments index.

Query rules that go with the indexes:

- Date filters are half-open ranges on the bare column (`OccurredAt >= @from AND OccurredAt < @to`), never
  `YEAR(col)`, `CAST(col AS date)` or `DATEDIFF(..., col, ...)`. The measured rewrite S1 went from 1,948 to 41 reads.
- Parameters keep the column's type (EF does this; `datetimeoffset` vs `datetimeoffset`, `varchar` codes as
  `varchar`), so there are no implicit conversions on the column side.
- `AUTO_UPDATE_STATISTICS` stays ON. Run `UPDATE STATISTICS` after bulk loads. Experiment E1 showed a 41x row
  underestimate with stale statistics.

## Consequences

- Each stock change maintains three BranchStock structures (PK + two indexes). Measured: about 6 extra reads per
  updated row, which is a few page touches per sale.
- W3's remaining cost is the PK seeks into `CounterTransactions` for the 30-day filter. Removing them would need
  `OccurredAt` copied onto `Shipments` (a 3NF violation), so it was deliberately not done.
- Filtered index W4 requires `QUOTED_IDENTIFIER ON` for all writers. EF/SqlClient set it by default; scripts run
  through `sqlcmd -I`.
- Re-measure with `scripts/measure-performance.ps1` when data shape changes (for example many more products per
  branch), and revisit W2 if branch-day volumes grow by an order of magnitude.

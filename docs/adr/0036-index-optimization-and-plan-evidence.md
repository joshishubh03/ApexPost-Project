# 0036: Index design and execution-plan evidence

- Status: Accepted
- Date: 2026-11-01

## Context

The Assignment 3 requirements specifically call for index choices backed by evidence rather than guesswork. A relational system of record for branches, stock, transactions, shipping, and money orders needs indexes that support operational reporting while controlling write overhead and storage cost.

## Decision

We treat index design as a measured engineering choice. The baseline workload scripts in `sql/performance-workloads.sql` are written so that each query can be executed with:

- Actual Execution Plan enabled
- `SET STATISTICS IO, TIME ON`
- captured logical reads and CPU time before and after each candidate index

Key candidates include:

- `IX_BranchStock_LowStock` for the low-stock branch report
- `IX_CounterTransactions_BranchDateStatus` for daily sales by branch and date
- `IX_CounterTransactions_ShippingSearch` for shipment lookups by zone and service level
- `IX_MoneyOrders_StatusIssuedAt` for auditor money-order queries

The most important principle is that all predicate columns are ordered to match the query pattern and any INCLUDE columns are restricted to the columns fetched without requiring a key lookup.

## Why these are the right indexes

1. Branch stock queries filter on `QuantityOnHand` and then join on `BranchId` and `ProductId`, so the leading key is the low-stock predicate. The INCLUDE list adds `ReorderLevel` to avoid a second lookup.
2. Daily sales group by branch/date and then optionally join the transaction lines, so the transaction table is filtered by `BranchId`, `OccurredAtUtc`, and `Status`. This matches the reporting pattern and keeps the scan narrow.
3. Shipment queries are a lookup on `ShippingZoneName` and `ShippingServiceLevel`, so both are in the key order. The INCLUDE adds the data needed for the final display without forcing extra seeks.
4. Auditor money-order reporting is mostly a time-bounded status query, so `Status` and `IssuedAtUtc` are the leading columns.

## Consequences

- We avoid speculative or broad index creation. Each index is justified by a workload and is expected to reduce read cost in proportion to the real cardinality of the data.
- Some operations will still scan, especially on small datasets or heavily skewed distribution; that is explained by the data, not hidden by naming or optimistic assumptions.
- Indexes add write overhead and storage cost, so the design remains an operational compromise rather than a purely read-optimized schema.
- The performance evidence must be captured from a real SQL Server instance and the results retained in `PERFORMANCE.md` to satisfy the assignment requirement.

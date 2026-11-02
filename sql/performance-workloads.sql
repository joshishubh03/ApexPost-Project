/*
    Part C workloads, exactly as measured by scripts/measure-performance.ps1 against ApexPostDb_Perf.
    Each block: the query, the candidate index (with key-order / INCLUDE justification) and the rollback.
    Measurements, plans and conclusions: PERFORMANCE.md and sql/performance-evidence/*.sqlplan.
*/

-- W1 Low-stock report across all branches (head office), threshold 5.
--    Candidate: key on QuantityOnHand (the range predicate) so only low rows are read; ReorderLevel INCLUDEd so the
--    index covers the query; BranchId/ProductId come free as the clustered key.
SELECT b.Code, p.Sku, p.Name, bs.QuantityOnHand, bs.ReorderLevel
FROM dbo.BranchStock AS bs
INNER JOIN dbo.Branches AS b ON b.Id = bs.BranchId
INNER JOIN dbo.Products AS p ON p.Id = bs.ProductId
WHERE bs.QuantityOnHand <= 5 AND p.IsActive = 1
ORDER BY b.Code, bs.QuantityOnHand;
-- CREATE NONCLUSTERED INDEX IX_BranchStock_QuantityOnHand ON dbo.BranchStock (QuantityOnHand) INCLUDE (ReorderLevel);

-- W2 Daily sales for one branch and one day (the usp_DailyBranchSales access path).
--    Candidate: BranchId (equality) first, OccurredAt (range) second; INCLUDE the amount columns and Status so
--    the header rows are read from the index without key lookups.
DECLARE @Branch uniqueidentifier = (SELECT Id FROM dbo.Branches WHERE Code = 'PB-0042');
DECLARE @Day datetimeoffset(3) = TODATETIMEOFFSET(CAST(DATEADD(day, -3, CAST(SYSUTCDATETIME() AS date)) AS datetime2(3)), 0);
SELECT COUNT(*) AS Transactions, SUM(ct.TotalAmount) AS Revenue
FROM dbo.CounterTransactions AS ct
WHERE ct.BranchId = @Branch AND ct.OccurredAt >= @Day AND ct.OccurredAt < DATEADD(day, 1, @Day) AND ct.Status IN ('Paid', 'Completed');
-- Measured and REJECTED (ADR 0042: 19 reads saved per call vs 52.7 MB and +30k reads per 10k updates):
-- CREATE NONCLUSTERED INDEX IX_CounterTransactions_Branch_OccurredAt_Covering ON dbo.CounterTransactions (BranchId, OccurredAt)
--     INCLUDE (Status, TotalAmount, ShippingCharge, ShippingTaxAmount, MoneyOrderAmount, MoneyOrderFee);

-- W3 Shipment search by zone and service level (last 30 days).
--    Candidate: ShippingRateId (equality after resolving zone + service level to a rate) then Status (equality);
--    INCLUDE CounterTransactionId and WeightKilograms so the join to CounterTransactions needs no lookup.
SELECT ct.TransactionNumber, ct.OccurredAt, s.WeightKilograms
FROM dbo.Shipments AS s
INNER JOIN dbo.ShippingRates AS r ON r.Id = s.ShippingRateId
INNER JOIN dbo.ShippingZones AS z ON z.Id = r.ZoneId
INNER JOIN dbo.CounterTransactions AS ct ON ct.Id = s.CounterTransactionId
WHERE z.Code = 'Z-07' AND r.ServiceLevel = 'Express' AND s.Status = 'Booked'
  AND ct.OccurredAt >= DATEADD(day, -30, SYSDATETIMEOFFSET());
-- Replaces the narrow EF index of the same name (same keys + INCLUDE), applied by migration Week3MeasuredIndexes:
-- CREATE NONCLUSTERED INDEX IX_Shipments_ShippingRateId_Status ON dbo.Shipments (ShippingRateId, Status)
--     INCLUDE (CounterTransactionId, WeightKilograms) WITH (DROP_EXISTING = ON);

-- W4 Money-order search for auditors: identity-verified (high-value) orders in a period.
--    Candidate: FILTERED index - only ~20% of orders are verified, so the index is a fifth of the size and
--    is keyed on OccurredAt for the period range; INCLUDE the columns the auditor screen shows.
SELECT mo.MoneyOrderNumber, mo.OccurredAt, mo.Amount, mo.MaskedDocumentIdentifier, mo.BranchId
FROM dbo.MoneyOrders AS mo
WHERE mo.VerificationStatus = 'Verified' AND mo.Status = 'Issued'
  AND mo.OccurredAt >= DATEADD(day, -90, SYSDATETIMEOFFSET());
-- CREATE NONCLUSTERED INDEX IX_MoneyOrders_Verified_OccurredAt ON dbo.MoneyOrders (OccurredAt)
--     INCLUDE (MoneyOrderNumber, Amount, MaskedDocumentIdentifier, BranchId, Status) WHERE VerificationStatus = 'Verified';

-- S1 Non-SARGable vs SARGable: a function wrapped around OccurredAt prevents a seek on IX_CounterTransactions_Status_OccurredAt
--    (SQL Server must evaluate DATEDIFF for every 'Paid' row); the rewritten range predicate seeks on (Status, OccurredAt).
SELECT COUNT(*) FROM dbo.CounterTransactions
WHERE Status = 'Paid' AND DATEDIFF(day, OccurredAt, SYSDATETIMEOFFSET()) <= 7;                          -- non-SARGable

SELECT COUNT(*) FROM dbo.CounterTransactions
WHERE Status = 'Paid' AND OccurredAt >= DATEADD(day, -7, CAST(CAST(SYSDATETIMEOFFSET() AS date) AS datetimeoffset(3)));  -- SARGable

-- E1 Estimated vs actual rows (stale statistics): see scripts/measure-performance.ps1 step "stale statistics".

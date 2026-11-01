USE ApexPostDb;
GO

-- Assignment 3 performance workload scripts.
-- Run each block with Actual Execution Plan and SET STATISTICS IO, TIME ON.
-- Record the plan shape, logical reads, and CPU time before and after the candidate index.

SET STATISTICS IO, TIME ON;
GO

-- Workload 1: low-stock report by branch
-- Baseline query
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
GO

-- Candidate index for low stock
CREATE INDEX IX_BranchStock_LowStock
	ON dbo.BranchStock (QuantityOnHand, BranchId, ProductId)
	INCLUDE (ReorderLevel);
GO

-- Workload 2: daily sales by branch and date
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
GO

CREATE INDEX IX_CounterTransactions_BranchDateStatus
	ON dbo.CounterTransactions (BranchId, OccurredAtUtc, Status)
	INCLUDE (Id);
GO

CREATE INDEX IX_TransactionLines_CounterTransaction
	ON dbo.TransactionLines (CounterTransactionId, TaxCategory, LineTotal)
	INCLUDE (ProductId, Quantity);
GO

-- Workload 3: shipment search by zone and service level
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
GO

CREATE INDEX IX_CounterTransactions_ShippingSearch
	ON dbo.CounterTransactions (ShippingZoneName, ShippingServiceLevel, ShippingRateId)
	INCLUDE (ShippingWeightKilograms, ShippingCharge, Id);
GO

-- Workload 4: money-order search for auditors
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
GO

CREATE INDEX IX_MoneyOrders_StatusIssuedAt
	ON dbo.MoneyOrders (Status, IssuedAtUtc)
	INCLUDE (BranchId, Amount, VerificationId);
GO

-- SARGable rewrite example
SELECT *
FROM dbo.CounterTransactions AS ct
WHERE YEAR(ct.OccurredAtUtc) = 2026;
GO

SELECT *
FROM dbo.CounterTransactions AS ct
WHERE ct.OccurredAtUtc >= '2026-01-01'
  AND ct.OccurredAtUtc < '2027-01-01';
GO

-- Statistics mismatch demonstration (run on a realistic dataset, not a toy set)
UPDATE STATISTICS dbo.CounterTransactions;
DBCC SHOW_STATISTICS ('dbo.CounterTransactions', 'IX_CounterTransactions_BranchDateStatus');
GO

SET STATISTICS IO, TIME OFF;
GO

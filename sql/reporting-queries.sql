USE ApexPostDb;
GO

-- Assignment 3 reporting baselines for the Counter Platform.
-- These queries remain intentionally simple and parameter-free so they can be tuned
-- with execution plans and later wrapped in stored procedures or query services.

-- 1) Daily branch sales split by retail and services.
SELECT
	b.Code AS BranchCode,
	CAST(ct.OccurredAtUtc AS date) AS SaleDate,
	SUM(CASE
			WHEN tl.TaxCategory IN ('Postage', 'Shipping', 'Money Order') THEN tl.LineTotal
			ELSE 0
		END) AS ServiceRevenue,
	SUM(CASE
			WHEN tl.TaxCategory NOT IN ('Postage', 'Shipping', 'Money Order') THEN tl.LineTotal
			ELSE 0
		END) AS RetailRevenue
FROM dbo.CounterTransactions AS ct
INNER JOIN dbo.Branches AS b
	ON b.Id = ct.BranchId
LEFT JOIN dbo.TransactionLines AS tl
	ON tl.CounterTransactionId = ct.Id
WHERE ct.Status IN ('Paid', 'Completed')
GROUP BY b.Code, CAST(ct.OccurredAtUtc AS date)
HAVING SUM(tl.LineTotal) > 0
ORDER BY b.Code, SaleDate;
GO

-- 2) Branches with no sales today. This is the LEFT JOIN case required by the assignment.
SELECT
	b.Code,
	b.Name,
	CASE WHEN ct.Id IS NULL THEN 'No sales today' ELSE 'Sales recorded' END AS SalesStatus
FROM dbo.Branches AS b
LEFT JOIN dbo.CounterTransactions AS ct
	ON ct.BranchId = b.Id
   AND CAST(ct.OccurredAtUtc AS date) = CAST(SYSDATETIME() AS date)
WHERE ct.Id IS NULL
ORDER BY b.Code;
GO

-- 3) Top 10 products by revenue per region.
SELECT TOP (10)
	b.Region,
	p.Sku,
	p.Name,
	SUM(tl.LineTotal) AS Revenue,
	SUM(tl.Quantity) AS UnitsSold
FROM dbo.TransactionLines AS tl
INNER JOIN dbo.CounterTransactions AS ct
	ON ct.Id = tl.CounterTransactionId
INNER JOIN dbo.Branches AS b
	ON b.Id = ct.BranchId
INNER JOIN dbo.Products AS p
	ON p.Id = tl.ProductId
GROUP BY b.Region, p.Sku, p.Name
HAVING SUM(tl.LineTotal) > 0
ORDER BY Revenue DESC, p.Sku;
GO

-- 4) Shipping volume by zone and service level.
SELECT
	ct.ShippingZoneName AS ZoneName,
	ct.ShippingServiceLevel AS ServiceLevel,
	COUNT(*) AS ShipmentCount,
	SUM(CAST(ct.ShippingWeightKilograms AS decimal(18,2))) AS TotalWeightKg,
	SUM(ct.ShippingCharge) AS TotalShippingRevenue
FROM dbo.CounterTransactions AS ct
WHERE ct.ShippingRateId IS NOT NULL
GROUP BY ct.ShippingZoneName, ct.ShippingServiceLevel
ORDER BY ZoneName, ServiceLevel;
GO

-- 5) Money orders and identity verifications per branch.
SELECT
	b.Code AS BranchCode,
	COUNT(DISTINCT mo.Id) AS MoneyOrdersIssued,
	COUNT(DISTINCT iv.Id) AS IdentityChecks
FROM dbo.Branches AS b
LEFT JOIN dbo.MoneyOrders AS mo
	ON mo.BranchId = b.Id
LEFT JOIN dbo.IdentityVerifications AS iv
	ON iv.BranchId = b.Id
GROUP BY b.Code
ORDER BY b.Code;
GO

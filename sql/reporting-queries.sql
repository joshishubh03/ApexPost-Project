/*
    ApexPost reporting queries (Part B). Run against ApexPostDb (schema from the EF Core migrations).
    All date filters are SARGable UTC ranges on OccurredAt (datetimeoffset(3)); "today" means the UTC day.
    Service revenue = postage / shipping / money-order products + shipping charge + money-order amount and fee.
*/
SET NOCOUNT ON;

DECLARE @Today date = CAST(SYSUTCDATETIME() AS date);
DECLARE @DayStart datetimeoffset(3) = TODATETIMEOFFSET(CAST(@Today AS datetime2(3)), 0);
DECLARE @DayEnd datetimeoffset(3) = DATEADD(day, 1, @DayStart);
DECLARE @From datetimeoffset(3) = DATEADD(day, -30, @DayStart);

/* 1. Daily branch sales split by retail and services (INNER JOIN, GROUP BY, HAVING). */
SELECT
    b.Code AS BranchCode,
    CAST(ct.OccurredAt AS date) AS SaleDate,                -- grouping expression, not a filter
    COUNT(DISTINCT ct.Id) AS Transactions,
    SUM(CASE WHEN p.Category IN (N'Postage', N'Shipping', N'Money Order') THEN 0 ELSE tl.LineTotal + tl.TaxAmount END) AS RetailRevenue,
    SUM(CASE WHEN p.Category IN (N'Postage', N'Shipping', N'Money Order') THEN tl.LineTotal + tl.TaxAmount ELSE 0 END) AS ServiceLineRevenue
FROM dbo.CounterTransactions AS ct
INNER JOIN dbo.Branches AS b ON b.Id = ct.BranchId
INNER JOIN dbo.TransactionLines AS tl ON tl.CounterTransactionId = ct.Id
INNER JOIN dbo.Products AS p ON p.Id = tl.ProductId
WHERE ct.OccurredAt >= @From AND ct.OccurredAt < @DayEnd
  AND ct.Status IN ('Paid', 'Completed')
GROUP BY b.Code, CAST(ct.OccurredAt AS date)
HAVING SUM(tl.LineTotal) > 0
ORDER BY b.Code, SaleDate;

/* 2. Branches with no sales today (LEFT JOIN anti-join). */
SELECT b.Code AS BranchCode, b.Name, b.Region
FROM dbo.Branches AS b
LEFT JOIN dbo.CounterTransactions AS ct
       ON ct.BranchId = b.Id
      AND ct.OccurredAt >= @DayStart AND ct.OccurredAt < @DayEnd
      AND ct.Status IN ('Paid', 'Completed')
WHERE ct.Id IS NULL
ORDER BY b.Code;

/* 3. Top 10 products by revenue per region (last 30 days; window function over a GROUP BY). */
WITH RegionProductRevenue AS
(
    SELECT
        b.Region,
        p.Sku,
        p.Name AS ProductName,
        SUM(tl.LineTotal) AS Revenue,
        SUM(tl.Quantity) AS UnitsSold
    FROM dbo.TransactionLines AS tl
    INNER JOIN dbo.CounterTransactions AS ct ON ct.Id = tl.CounterTransactionId
    INNER JOIN dbo.Branches AS b ON b.Id = ct.BranchId
    INNER JOIN dbo.Products AS p ON p.Id = tl.ProductId
    WHERE ct.OccurredAt >= @From AND ct.OccurredAt < @DayEnd
      AND ct.Status IN ('Paid', 'Completed')
    GROUP BY b.Region, p.Sku, p.Name
    HAVING SUM(tl.LineTotal) > 0
),
Ranked AS
(
    SELECT *, DENSE_RANK() OVER (PARTITION BY Region ORDER BY Revenue DESC) AS RevenueRank
    FROM RegionProductRevenue
)
SELECT Region, RevenueRank, Sku, ProductName, Revenue, UnitsSold
FROM Ranked
WHERE RevenueRank <= 10
ORDER BY Region, RevenueRank, Sku;

/* 4. Shipping volume by zone and service level (INNER JOIN through Shipments -> ShippingRates -> ShippingZones). */
SELECT
    z.Code AS ZoneCode,
    z.Name AS ZoneName,
    r.ServiceLevel,
    COUNT(*) AS Shipments,
    SUM(s.WeightKilograms) AS TotalWeightKg,
    SUM(ct.ShippingCharge) AS ShippingRevenue
FROM dbo.Shipments AS s
INNER JOIN dbo.CounterTransactions AS ct ON ct.Id = s.CounterTransactionId
INNER JOIN dbo.ShippingRates AS r ON r.Id = s.ShippingRateId
INNER JOIN dbo.ShippingZones AS z ON z.Id = r.ZoneId
WHERE s.Status = 'Booked'
  AND ct.OccurredAt >= @From AND ct.OccurredAt < @DayEnd
GROUP BY z.Code, z.Name, r.ServiceLevel
HAVING COUNT(*) >= 1
ORDER BY z.Code, r.ServiceLevel;

/* 5. Money orders and identity verifications per branch (LEFT JOINs to pre-aggregated sets, so branches with none still appear). */
WITH Orders AS
(
    SELECT BranchId,
           SUM(CASE WHEN Status = 'Issued' THEN 1 ELSE 0 END) AS Issued,
           SUM(CASE WHEN Status = 'Refunded' THEN 1 ELSE 0 END) AS Refunded,
           SUM(Amount) AS MoneyOrderValue
    FROM dbo.MoneyOrders
    WHERE OccurredAt >= @From AND OccurredAt < @DayEnd
    GROUP BY BranchId
),
Checks AS
(
    SELECT BranchId,
           SUM(CASE WHEN Status = 'Verified' THEN 1 ELSE 0 END) AS Verified,
           SUM(CASE WHEN Status = 'Failed' THEN 1 ELSE 0 END) AS Failed
    FROM dbo.IdentityVerifications
    WHERE OccurredAt >= @From AND OccurredAt < @DayEnd
    GROUP BY BranchId
)
SELECT b.Code AS BranchCode, b.Name,
       ISNULL(o.Issued, 0) AS MoneyOrdersIssued,
       ISNULL(o.Refunded, 0) AS MoneyOrdersRefunded,
       ISNULL(o.MoneyOrderValue, 0) AS MoneyOrderValue,
       ISNULL(c.Verified, 0) AS VerificationsPassed,
       ISNULL(c.Failed, 0) AS VerificationsFailed
FROM dbo.Branches AS b
LEFT JOIN Orders AS o ON o.BranchId = b.Id
LEFT JOIN Checks AS c ON c.BranchId = b.Id
ORDER BY b.Code;

/* 5b. Branches with suspicious verification failure rates (GROUP BY ... HAVING on a ratio). */
SELECT b.Code AS BranchCode, COUNT(*) AS Checks,
       SUM(CASE WHEN iv.Status = 'Failed' THEN 1 ELSE 0 END) AS Failed
FROM dbo.IdentityVerifications AS iv
INNER JOIN dbo.Branches AS b ON b.Id = iv.BranchId
GROUP BY b.Code
HAVING SUM(CASE WHEN iv.Status = 'Failed' THEN 1 ELSE 0 END) * 1.0 / COUNT(*) > 0.2;

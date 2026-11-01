USE ApexPostDb;
GO

SET NOCOUNT ON;
GO

-- Seed the supporting audit-search data used by the performance workloads.
IF NOT EXISTS (SELECT 1 FROM dbo.IdentityVerifications)
BEGIN
    ;WITH numbers AS (
        SELECT TOP (1200) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
        FROM sys.all_objects AS a
        CROSS JOIN sys.all_objects AS b
    )
    INSERT INTO dbo.IdentityVerifications (Id, BranchId, StaffId, OccurredAtUtc, DocumentType, MaskedDocumentIdentifier, MoneyOrderAmount, Status, CorrelationId)
    SELECT
        NEWID(),
        b.Id,
        s.Id,
        DATEADD(day, -(numbers.n % 180), SYSUTCDATETIME()),
        CASE numbers.n % 4
            WHEN 0 THEN 'Passport'
            WHEN 1 THEN 'NationalId'
            WHEN 2 THEN 'DrivingLicense'
            ELSE 'VoterId'
        END,
        CONCAT('XXXX-XXXX-XXXX-', RIGHT('0000' + CAST((numbers.n * 17) % 10000 AS varchar(4)), 4)),
        CAST((numbers.n % 2500) + 100 AS decimal(18,2)),
        CASE WHEN numbers.n % 7 = 0 THEN 'Rejected' ELSE 'Verified' END,
        CONCAT('CORR-', numbers.n)
    FROM numbers
    CROSS JOIN (SELECT TOP (1) Id FROM dbo.Branches ORDER BY Code) AS b
    CROSS JOIN (SELECT TOP (1) Id FROM dbo.Staff WHERE Role = 'Auditor' ORDER BY Email) AS s;
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.MoneyOrders)
BEGIN
    ;WITH numbers AS (
        SELECT TOP (1200) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
        FROM sys.all_objects AS a
        CROSS JOIN sys.all_objects AS b
    ),
    verificationRows AS (
        SELECT Id,
               ROW_NUMBER() OVER (ORDER BY OccurredAtUtc, Id) AS rn
        FROM dbo.IdentityVerifications
    )
    INSERT INTO dbo.MoneyOrders (Id, MoneyOrderNumber, BranchId, StaffId, OccurredAtUtc, SenderName, RecipientName, Amount, Fee, TotalAmount, VerificationStatus, VerificationRecordId, DocumentType, MaskedDocumentIdentifier, Status)
    SELECT
        NEWID(),
        CONCAT('MO-', numbers.n),
        b.Id,
        s.Id,
        DATEADD(day, -(numbers.n % 120), SYSUTCDATETIME()),
        CONCAT('Sender ', numbers.n),
        CONCAT('Recipient ', numbers.n),
        CAST((numbers.n % 800) + 100 AS decimal(18,2)),
        CAST((numbers.n % 30) + 10 AS decimal(18,2)),
        CAST((numbers.n % 800) + 110 AS decimal(18,2)),
        CASE WHEN numbers.n % 4 = 0 THEN 'Pending' ELSE 'Verified' END,
        vr.Id,
        CASE numbers.n % 4
            WHEN 0 THEN 'Passport'
            WHEN 1 THEN 'NationalId'
            WHEN 2 THEN 'DrivingLicense'
            ELSE 'VoterId'
        END,
        CONCAT('XXXX-XXXX-XXXX-', RIGHT('0000' + CAST((numbers.n * 11) % 10000 AS varchar(4)), 4)),
        CASE WHEN numbers.n % 6 = 0 THEN 'Refunded' ELSE 'Issued' END
    FROM numbers
    CROSS JOIN (SELECT TOP (1) Id FROM dbo.Branches ORDER BY Code) AS b
    CROSS JOIN (SELECT TOP (1) Id FROM dbo.Staff WHERE Role IN ('BranchManager','CounterClerk','Auditor') ORDER BY Email) AS s
    JOIN verificationRows AS vr
        ON vr.rn = ((numbers.n - 1) % (SELECT COUNT(*) FROM dbo.IdentityVerifications)) + 1;
END;
GO

-- Workload 1: low-stock report by branch.
PRINT 'Workload 1: low-stock report baseline';
DROP INDEX IF EXISTS IX_Inventory_LowStock ON dbo.Inventory;
GO
SET STATISTICS IO, TIME ON;
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
SET STATISTICS IO, TIME OFF;
GO

CREATE INDEX IX_Inventory_LowStock
    ON dbo.Inventory (QuantityOnHand, BranchId, ProductId)
    INCLUDE (ReorderLevel);
GO

PRINT 'Workload 1: low-stock report with candidate index';
SET STATISTICS IO, TIME ON;
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
SET STATISTICS IO, TIME OFF;
GO

DROP INDEX IF EXISTS IX_Inventory_LowStock ON dbo.Inventory;
GO

-- Workload 2: daily sales by branch and date.
PRINT 'Workload 2: daily sales baseline';
DROP INDEX IF EXISTS IX_CounterTransactions_BranchDateStatus ON dbo.CounterTransactions;
GO
SET STATISTICS IO, TIME ON;
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
SET STATISTICS IO, TIME OFF;
GO

CREATE INDEX IX_CounterTransactions_BranchDateStatus
    ON dbo.CounterTransactions (BranchId, OccurredAtUtc, Status)
    INCLUDE (Id, TotalAmount, ShippingCharge);
GO

PRINT 'Workload 2: daily sales with candidate index';
SET STATISTICS IO, TIME ON;
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
SET STATISTICS IO, TIME OFF;
GO

DROP INDEX IF EXISTS IX_CounterTransactions_BranchDateStatus ON dbo.CounterTransactions;
GO

-- Workload 3: shipment search by zone and service level.
PRINT 'Workload 3: shipping search baseline';
DROP INDEX IF EXISTS IX_CounterTransactions_ShippingSearch ON dbo.CounterTransactions;
GO
SET STATISTICS IO, TIME ON;
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
SET STATISTICS IO, TIME OFF;
GO

CREATE INDEX IX_CounterTransactions_ShippingSearch
    ON dbo.CounterTransactions (ShippingZoneName, ShippingServiceLevel, ShippingRateId)
    INCLUDE (ShippingWeightKilograms, ShippingCharge, TotalAmount, Id);
GO

PRINT 'Workload 3: shipping search with candidate index';
SET STATISTICS IO, TIME ON;
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
SET STATISTICS IO, TIME OFF;
GO

DROP INDEX IF EXISTS IX_CounterTransactions_ShippingSearch ON dbo.CounterTransactions;
GO

-- Workload 4: money-order search for auditors.
PRINT 'Workload 4: money-order audit search baseline';
DROP INDEX IF EXISTS IX_MoneyOrders_StatusIssuedAt ON dbo.MoneyOrders;
GO
SET STATISTICS IO, TIME ON;
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
SET STATISTICS IO, TIME OFF;
GO

CREATE INDEX IX_MoneyOrders_StatusIssuedAt
    ON dbo.MoneyOrders (Status, OccurredAtUtc)
    INCLUDE (BranchId, Amount, VerificationRecordId);
GO

PRINT 'Workload 4: money-order audit search with candidate index';
SET STATISTICS IO, TIME ON;
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
SET STATISTICS IO, TIME OFF;
GO

DROP INDEX IF EXISTS IX_MoneyOrders_StatusIssuedAt ON dbo.MoneyOrders;
GO

-- SARGable rewrite example.
PRINT 'SARGable rewrite example before';
SET STATISTICS IO, TIME ON;
SELECT *
FROM dbo.CounterTransactions AS ct
WHERE YEAR(ct.OccurredAtUtc) = 2026;
SET STATISTICS IO, TIME OFF;
GO

PRINT 'SARGable rewrite example after';
SET STATISTICS IO, TIME ON;
SELECT *
FROM dbo.CounterTransactions AS ct
WHERE ct.OccurredAtUtc >= '2026-01-01'
  AND ct.OccurredAtUtc < '2027-01-01';
SET STATISTICS IO, TIME OFF;
GO

PRINT 'Statistics refresh reference';
UPDATE STATISTICS dbo.CounterTransactions;
DBCC SHOW_STATISTICS ('dbo.CounterTransactions', 'IX_CounterTransactions_BranchDateStatus');
GO

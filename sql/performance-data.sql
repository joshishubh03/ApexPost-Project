/*
    Realistic-volume data for the index experiments (Part C). Run ONLY against a separate database
    (ApexPostDb_Perf) created with the same migrations - never against the development database.
    Volumes: 200 branches, 600 staff, 300 products, 60,000 stock rows, 60 rates, ~300,000 counter
    transactions (~600,000 lines, 300,000 payments, ~90,000 shipments), 30,000 money orders, 6,000 ID checks.
    Set-based generation from a numbers CTE; deterministic pseudo-random values via ABS(CHECKSUM(...)).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.CounterTransactions)
    THROW 52000, N'ApexPostDb_Perf already contains data - drop and recreate it first.', 1;

DECLARE @Now datetimeoffset(3) = SYSDATETIMEOFFSET();

;WITH n AS (SELECT TOP (1000000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
            FROM sys.all_objects a CROSS JOIN sys.all_objects b CROSS JOIN sys.all_objects c)
SELECT i INTO #n FROM n;
CREATE UNIQUE CLUSTERED INDEX IX_n ON #n (i);

INSERT dbo.StaffRoles (Code, Name) VALUES ('CounterClerk', N'Counter Clerk'), ('BranchManager', N'Branch Manager'), ('Auditor', N'Auditor'), ('Administrator', N'Administrator');
INSERT dbo.TaxCategories (Code, Name) VALUES (N'Standard', N'Standard'), (N'Shipping', N'Shipping');
INSERT dbo.ProductCategories (Code, Name) VALUES (N'Postage', N'Postage'), (N'Packaging', N'Packaging'), (N'Stationery', N'Stationery'), (N'Office Supplies', N'Office Supplies'), (N'Shipping', N'Shipping'), (N'Money Order', N'Money Order');

INSERT dbo.Branches (Id, Code, Name, Region, IsOpen, OpeningTime, ClosingTime)
SELECT CONVERT(uniqueidentifier, CONCAT('B0000000-0000-0000-0000-', RIGHT(CONCAT('000000000000', i), 12))),
       CONCAT('PB-', RIGHT(CONCAT('0000', i), 4)), CONCAT(N'Perf Branch ', i), CONCAT(N'Region ', 1 + i % 10), 1, '09:00', '18:00'
FROM #n WHERE i <= 200;

INSERT dbo.CounterTerminals (Id, BranchId, Code, Name, IsActive)
SELECT NEWID(), b.Id, 'T-01', N'Counter 1', 1 FROM dbo.Branches AS b;

INSERT dbo.Staff (Id, BranchId, Email, FullName, Role, IsActive, PasswordHash)
SELECT NEWID(), b.Id, CONCAT(LOWER(r.Prefix), '.', LOWER(b.Code), '@perf.apexpost.local'), CONCAT(r.Prefix, N' ', b.Code), r.Role, 1, 'not-a-real-hash'
FROM dbo.Branches AS b
CROSS JOIN (VALUES ('manager', 'BranchManager'), ('clerka', 'CounterClerk'), ('clerkb', 'CounterClerk')) AS r(Prefix, Role);

INSERT dbo.Products (Id, Sku, Name, Description, Category, TaxCategory, Price, IsActive)
SELECT NEWID(), CONCAT('P-', RIGHT(CONCAT('00000', i), 5)), CONCAT(N'Perf product ', i), N'Generated for index experiments',
       CASE i % 6 WHEN 0 THEN N'Postage' WHEN 1 THEN N'Packaging' WHEN 2 THEN N'Stationery' WHEN 3 THEN N'Office Supplies' WHEN 4 THEN N'Shipping' ELSE N'Packaging' END,
       N'Standard', CAST(10 + (i * 7) % 490 AS decimal(18, 2)), CASE WHEN i % 50 = 0 THEN 0 ELSE 1 END
FROM #n WHERE i <= 300;

INSERT dbo.BranchStock (BranchId, ProductId, QuantityOnHand, ReorderLevel)
SELECT b.Id, p.Id, ABS(CHECKSUM(b.Id, p.Id)) % 200, 10
FROM dbo.Branches AS b CROSS JOIN dbo.Products AS p;

INSERT dbo.ShippingZones (Id, Code, Name, Destination, IsActive)
SELECT NEWID(), CONCAT('Z-', RIGHT(CONCAT('00', i), 2)), CONCAT(N'Zone ', i), CONCAT(N'Destination ', i), 1 FROM #n WHERE i <= 20;

INSERT dbo.ShippingRates (Id, ZoneId, ServiceLevel, BasePrice, PricePerKilogram, MaximumWeightKilograms, IsActive)
SELECT NEWID(), z.Id, s.Level, 30 + s.Extra, 10, 30, 1
FROM dbo.ShippingZones AS z
CROSS JOIN (VALUES ('Standard', 0), ('Express', 25), ('Registered', 40)) AS s(Level, Extra);

-- Reference sets numbered for deterministic picking.
SELECT ROW_NUMBER() OVER (ORDER BY Code) AS rn, Id INTO #b FROM dbo.Branches;
SELECT ROW_NUMBER() OVER (ORDER BY s.Email) AS rn, s.Id, s.BranchId INTO #clerk FROM dbo.Staff AS s WHERE s.Role = 'CounterClerk';
SELECT ROW_NUMBER() OVER (ORDER BY Sku) AS rn, Id, Sku, Name, Price INTO #p FROM dbo.Products WHERE IsActive = 1;
SELECT ROW_NUMBER() OVER (ORDER BY z.Code, r.ServiceLevel) AS rn, r.Id INTO #r FROM dbo.ShippingRates AS r INNER JOIN dbo.ShippingZones AS z ON z.Id = r.ZoneId;
DECLARE @Products int = (SELECT COUNT(*) FROM #p), @Rates int = (SELECT COUNT(*) FROM #r);

-- 300,000 transactions over the last 365 days.
SELECT n.i,
       CONVERT(uniqueidentifier, HASHBYTES('MD5', CONCAT('perf-tx-', n.i))) AS Id,
       c.BranchId, c.Id AS StaffId,
       DATEADD(minute, -(ABS(CHECKSUM(n.i * 31)) % (365 * 24 * 60)), @Now) AS OccurredAt,
       1 + n.i % 3 AS LineCount,
       CASE WHEN n.i % 40 = 0 THEN 'Voided' ELSE 'Paid' END AS Status,
       CASE WHEN n.i % 10 < 3 THEN 1 ELSE 0 END AS HasShipment
INTO #tx
FROM #n AS n
INNER JOIN #clerk AS c ON c.rn = 1 + n.i % 400
WHERE n.i <= 300000;

SELECT t.Id AS TxId, l.LineNumber, p.Id AS ProductId, p.Sku, p.Name, p.Price, 1 + (t.i + l.LineNumber) % 4 AS Quantity
INTO #lines
FROM #tx AS t
CROSS APPLY (SELECT 0 AS LineNumber UNION ALL SELECT 1 UNION ALL SELECT 2) AS l
INNER JOIN #p AS p ON p.rn = 1 + (t.i * 13 + l.LineNumber * 7) % @Products
WHERE l.LineNumber < t.LineCount;

SELECT TxId, SUM(Price * Quantity) AS Subtotal INTO #totals FROM #lines GROUP BY TxId;

INSERT dbo.CounterTransactions (Id, TransactionNumber, BranchId, StaffId, TerminalId, MoneyOrderId, Subtotal, TaxTotal, ShippingCharge,
    ShippingTaxRatePercent, ShippingTaxAmount, MoneyOrderAmount, MoneyOrderFee, TotalAmount, Status, OccurredAt, ResolvedAt, ResolvedByStaffId, ResolutionReason)
SELECT t.Id, CONCAT('APT-PERF-', RIGHT(CONCAT('0000000', t.i), 7)), t.BranchId, t.StaffId, term.Id, NULL, tot.Subtotal, 0,
       CASE WHEN t.HasShipment = 1 THEN 55 ELSE 0 END, 0, 0, 0, 0,
       tot.Subtotal + CASE WHEN t.HasShipment = 1 THEN 55 ELSE 0 END, t.Status, t.OccurredAt,
       CASE WHEN t.Status = 'Voided' THEN DATEADD(minute, 30, t.OccurredAt) END,
       CASE WHEN t.Status = 'Voided' THEN t.StaffId END,
       CASE WHEN t.Status = 'Voided' THEN N'Generated void' END
FROM #tx AS t
INNER JOIN #totals AS tot ON tot.TxId = t.Id
INNER JOIN dbo.CounterTerminals AS term ON term.BranchId = t.BranchId;

INSERT dbo.TransactionLines (Id, CounterTransactionId, LineNumber, ProductId, Sku, ProductName, Quantity, UnitPrice, LineTotal, TaxCategory, TaxRatePercent, TaxAmount)
SELECT NEWID(), l.TxId, l.LineNumber, l.ProductId, l.Sku, l.Name, l.Quantity, l.Price, l.Price * l.Quantity, N'Standard', 0, 0
FROM #lines AS l;

INSERT dbo.Payments (Id, CounterTransactionId, Method, Amount, PaidAt)
SELECT NEWID(), ct.Id, CASE ABS(CHECKSUM(ct.Id)) % 3 WHEN 0 THEN 'Cash' WHEN 1 THEN 'Card' ELSE 'DigitalWallet' END, ct.TotalAmount, ct.OccurredAt
FROM dbo.CounterTransactions AS ct;

INSERT dbo.Shipments (Id, CounterTransactionId, ShippingRateId, ShipmentType, WeightKilograms, LengthCentimeters, WidthCentimeters, HeightCentimeters, RecipientName, AddressLine, Status)
SELECT NEWID(), t.Id, r.Id, 'Parcel', CAST(0.5 + (t.i % 50) * 0.5 AS decimal(9, 3)), 30, 20, 10, N'Generated Recipient', N'1 Generated Street',
       CASE WHEN t.Status = 'Voided' THEN 'Cancelled' ELSE 'Booked' END
FROM #tx AS t
INNER JOIN #r AS r ON r.rn = 1 + (t.i / 10) % @Rates   -- every zone/service level gets an equal share
WHERE t.HasShipment = 1;

-- Identity checks and money orders (verified ones are the high-value orders auditors search for).
SELECT n.i, c.BranchId, c.Id AS StaffId, DATEADD(minute, -(ABS(CHECKSUM(n.i * 17)) % (365 * 24 * 60)), @Now) AS OccurredAt,
       CONVERT(uniqueidentifier, HASHBYTES('MD5', CONCAT('perf-iv-', n.i))) AS IvId
INTO #mo FROM #n AS n INNER JOIN #clerk AS c ON c.rn = 1 + (n.i * 7) % 400 WHERE n.i <= 30000;

INSERT dbo.IdentityVerifications (Id, BranchId, StaffId, OccurredAt, DocumentType, MaskedDocumentIdentifier, MoneyOrderAmount, Status, CorrelationId)
SELECT IvId, BranchId, StaffId, OccurredAt, 'Passport', CONCAT('****', RIGHT(CONCAT('0000', i), 4)), 15000, CASE WHEN i % 25 = 0 THEN 'Failed' ELSE 'Verified' END, CONCAT('perf-', i)
FROM #mo WHERE i % 5 = 0;

INSERT dbo.MoneyOrders (Id, CounterTransactionId, BranchId, StaffId, IdentityVerificationId, MoneyOrderNumber, OccurredAt, SenderName, RecipientName, Amount, Fee,
    VerificationStatus, DocumentType, MaskedDocumentIdentifier, Status, RefundedAt, RefundedByStaffId, RefundReason)
SELECT NEWID(), NULL, m.BranchId, m.StaffId,
       CASE WHEN m.i % 5 = 0 THEN m.IvId END,
       CONCAT('APM-PERF-', RIGHT(CONCAT('000000', m.i), 6)), m.OccurredAt, N'Generated Sender', N'Generated Recipient',
       CASE WHEN m.i % 5 = 0 THEN 15000 ELSE 100 + m.i % 9000 END, 50,
       CASE WHEN m.i % 5 = 0 THEN 'Verified' ELSE 'NotRequired' END,
       CASE WHEN m.i % 5 = 0 THEN 'Passport' END,
       CASE WHEN m.i % 5 = 0 THEN CONCAT('****', RIGHT(CONCAT('0000', m.i), 4)) END,
       CASE WHEN m.i % 30 = 0 THEN 'Refunded' ELSE 'Issued' END,
       CASE WHEN m.i % 30 = 0 THEN DATEADD(hour, 1, m.OccurredAt) END,
       CASE WHEN m.i % 30 = 0 THEN m.StaffId END,
       CASE WHEN m.i % 30 = 0 THEN N'Generated refund' END
FROM #mo AS m
WHERE m.i % 5 <> 0 OR m.i % 25 <> 0;   -- failed checks never produce an order

UPDATE STATISTICS dbo.CounterTransactions WITH FULLSCAN;
UPDATE STATISTICS dbo.TransactionLines WITH FULLSCAN;
UPDATE STATISTICS dbo.Shipments WITH FULLSCAN;
UPDATE STATISTICS dbo.MoneyOrders WITH FULLSCAN;
UPDATE STATISTICS dbo.BranchStock WITH FULLSCAN;

SELECT (SELECT COUNT(*) FROM dbo.Branches) AS Branches, (SELECT COUNT(*) FROM dbo.Staff) AS Staff, (SELECT COUNT(*) FROM dbo.Products) AS Products,
       (SELECT COUNT(*) FROM dbo.BranchStock) AS StockRows, (SELECT COUNT(*) FROM dbo.CounterTransactions) AS Transactions,
       (SELECT COUNT(*) FROM dbo.TransactionLines) AS Lines, (SELECT COUNT(*) FROM dbo.Shipments) AS Shipments,
       (SELECT COUNT(*) FROM dbo.MoneyOrders) AS MoneyOrders, (SELECT COUNT(*) FROM dbo.IdentityVerifications) AS IdentityChecks;

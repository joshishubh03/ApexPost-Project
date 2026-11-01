USE ApexPostDb;
GO

SET NOCOUNT ON;
GO

BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.Branches)
BEGIN
	INSERT INTO dbo.Branches (Id, Code, Name, Region, IsOpen, OpeningTime, ClosingTime)
	VALUES
		(N'10000000-0000-0000-0000-000000000001', 'AP-001', 'ApexPost Branch 1', 'North', 1, '08:00:00', '18:00:00'),
		(N'10000000-0000-0000-0000-000000000002', 'AP-002', 'ApexPost Branch 2', 'South', 1, '08:00:00', '18:00:00'),
		(N'10000000-0000-0000-0000-000000000003', 'AP-003', 'ApexPost Branch 3', 'East', 1, '09:00:00', '19:00:00'),
		(N'10000000-0000-0000-0000-000000000004', 'AP-004', 'ApexPost Branch 4', 'West', 1, '09:00:00', '19:00:00'),
		(N'10000000-0000-0000-0000-000000000005', 'AP-005', 'ApexPost Branch 5', 'Central', 1, '08:30:00', '18:30:00');
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Staff)
BEGIN
	DECLARE @staffTemplate TABLE (Id uniqueidentifier, Email nvarchar(254), FullName nvarchar(160), Role nvarchar(max), BranchId uniqueidentifier, IsActive bit, PasswordHash nvarchar(512));

	INSERT INTO @staffTemplate (Id, Email, FullName, Role, BranchId, IsActive, PasswordHash)
	VALUES
		(N'20000000-0000-0000-0000-000000000001', 'admin@apexpost.local', 'ApexPost Administrator', 'Administrator', NULL, 1, 'seeded-admin-hash'),
		(N'20000000-0000-0000-0000-000000000002', 'manager1@apexpost.local', 'Branch Manager 1', 'BranchManager', N'10000000-0000-0000-0000-000000000001', 1, 'seeded-hash'),
		(N'20000000-0000-0000-0000-000000000003', 'clerk11@apexpost.local', 'Counter Clerk 1-1', 'CounterClerk', N'10000000-0000-0000-0000-000000000001', 1, 'seeded-hash'),
		(N'20000000-0000-0000-0000-000000000004', 'clerk12@apexpost.local', 'Counter Clerk 1-2', 'CounterClerk', N'10000000-0000-0000-0000-000000000001', 1, 'seeded-hash'),
		(N'20000000-0000-0000-0000-000000000005', 'manager2@apexpost.local', 'Branch Manager 2', 'BranchManager', N'10000000-0000-0000-0000-000000000002', 1, 'seeded-hash'),
		(N'20000000-0000-0000-0000-000000000006', 'clerk21@apexpost.local', 'Counter Clerk 2-1', 'CounterClerk', N'10000000-0000-0000-0000-000000000002', 1, 'seeded-hash'),
		(N'20000000-0000-0000-0000-000000000007', 'clerk22@apexpost.local', 'Counter Clerk 2-2', 'CounterClerk', N'10000000-0000-0000-0000-000000000002', 1, 'seeded-hash'),
		(N'20000000-0000-0000-0000-000000000008', 'manager3@apexpost.local', 'Branch Manager 3', 'BranchManager', N'10000000-0000-0000-0000-000000000003', 1, 'seeded-hash'),
		(N'20000000-0000-0000-0000-000000000009', 'clerk31@apexpost.local', 'Counter Clerk 3-1', 'CounterClerk', N'10000000-0000-0000-0000-000000000003', 1, 'seeded-hash'),
		(N'20000000-0000-0000-0000-000000000010', 'clerk32@apexpost.local', 'Counter Clerk 3-2', 'CounterClerk', N'10000000-0000-0000-0000-000000000003', 1, 'seeded-hash'),
		(N'20000000-0000-0000-0000-000000000011', 'manager4@apexpost.local', 'Branch Manager 4', 'BranchManager', N'10000000-0000-0000-0000-000000000004', 1, 'seeded-hash'),
		(N'20000000-0000-0000-0000-000000000012', 'clerk41@apexpost.local', 'Counter Clerk 4-1', 'CounterClerk', N'10000000-0000-0000-0000-000000000004', 1, 'seeded-hash'),
		(N'20000000-0000-0000-0000-000000000013', 'clerk42@apexpost.local', 'Counter Clerk 4-2', 'CounterClerk', N'10000000-0000-0000-0000-000000000004', 1, 'seeded-hash'),
		(N'20000000-0000-0000-0000-000000000014', 'manager5@apexpost.local', 'Branch Manager 5', 'BranchManager', N'10000000-0000-0000-0000-000000000005', 1, 'seeded-hash'),
		(N'20000000-0000-0000-0000-000000000015', 'clerk51@apexpost.local', 'Counter Clerk 5-1', 'CounterClerk', N'10000000-0000-0000-0000-000000000005', 1, 'seeded-hash'),
		(N'20000000-0000-0000-0000-000000000016', 'clerk52@apexpost.local', 'Counter Clerk 5-2', 'CounterClerk', N'10000000-0000-0000-0000-000000000005', 1, 'seeded-hash'),
		(N'20000000-0000-0000-0000-000000000017', 'auditor1@apexpost.local', 'ApexPost Auditor', 'Auditor', N'10000000-0000-0000-0000-000000000001', 1, 'seeded-hash'),
		(N'20000000-0000-0000-0000-000000000018', 'clerk-extra1@apexpost.local', 'Counter Clerk Extra 1', 'CounterClerk', N'10000000-0000-0000-0000-000000000001', 1, 'seeded-hash'),
		(N'20000000-0000-0000-0000-000000000019', 'clerk-extra2@apexpost.local', 'Counter Clerk Extra 2', 'CounterClerk', N'10000000-0000-0000-0000-000000000001', 1, 'seeded-hash'),
		(N'20000000-0000-0000-0000-000000000020', 'clerk-extra3@apexpost.local', 'Counter Clerk Extra 3', 'CounterClerk', N'10000000-0000-0000-0000-000000000001', 1, 'seeded-hash');

	INSERT INTO dbo.Staff (Id, Email, FullName, Role, BranchId, IsActive, PasswordHash)
	SELECT Id, Email, FullName, Role, BranchId, IsActive, PasswordHash
	FROM @staffTemplate;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Products)
BEGIN
	INSERT INTO dbo.Products (Id, Sku, Name, Description, Category, TaxCategory, Price, IsActive)
	VALUES
		(N'30000000-0000-0000-0000-000000000001', 'POST-001', 'Domestic Letter', 'Standard domestic letter postage', 'Postage', 'Standard', 25.00, 1),
		(N'30000000-0000-0000-0000-000000000002', 'POST-002', 'Large Letter', 'Large format domestic letter postage', 'Postage', 'Standard', 45.00, 1),
		(N'30000000-0000-0000-0000-000000000003', 'POST-003', 'Registered Letter', 'Tracked registered letter service', 'Postage', 'Standard', 85.00, 1),
		(N'30000000-0000-0000-0000-000000000004', 'POST-004', 'Express Letter', 'Priority domestic letter service', 'Postage', 'Standard', 120.00, 1),
		(N'30000000-0000-0000-0000-000000000005', 'POST-005', 'Postcard', 'Domestic postcard postage', 'Postage', 'Standard', 15.00, 1),
		(N'30000000-0000-0000-0000-000000000006', 'POST-006', 'International Letter', 'Standard international letter postage', 'Postage', 'Standard', 150.00, 1),
		(N'30000000-0000-0000-0000-000000000007', 'POST-007', 'International Express', 'Priority international letter service', 'Postage', 'Standard', 450.00, 1),
		(N'30000000-0000-0000-0000-000000000008', 'POST-008', 'Printed Matter', 'Printed material postal service', 'Postage', 'Standard', 35.00, 1),
		(N'30000000-0000-0000-0000-000000000009', 'PACK-001', 'Small Mailing Box', 'Small corrugated mailing box', 'Packaging', 'Standard', 40.00, 1),
		(N'30000000-0000-0000-0000-000000000010', 'PACK-002', 'Medium Mailing Box', 'Medium corrugated mailing box', 'Packaging', 'Standard', 65.00, 1),
		(N'30000000-0000-0000-0000-000000000011', 'PACK-003', 'Large Mailing Box', 'Large corrugated mailing box', 'Packaging', 'Standard', 95.00, 1),
		(N'30000000-0000-0000-0000-000000000012', 'PACK-004', 'Document Envelope', 'Rigid document mailing envelope', 'Packaging', 'Standard', 20.00, 1),
		(N'30000000-0000-0000-0000-000000000013', 'PACK-005', 'Padded Mailer', 'Protective padded mailer', 'Packaging', 'Standard', 30.00, 1),
		(N'30000000-0000-0000-0000-000000000014', 'PACK-006', 'Bubble Wrap Roll', 'Protective bubble wrap roll', 'Packaging', 'Standard', 110.00, 1),
		(N'30000000-0000-0000-0000-000000000015', 'PACK-007', 'Packing Tape', 'Standard clear packing tape', 'Packaging', 'Standard', 35.00, 1),
		(N'30000000-0000-0000-0000-000000000016', 'PACK-008', 'Shipping Labels', 'Pack of adhesive shipping labels', 'Packaging', 'Standard', 55.00, 1),
		(N'30000000-0000-0000-0000-000000000017', 'RETL-001', 'ApexPost Pen', 'Branded blue ballpoint pen', 'Stationery', 'Standard', 20.00, 1),
		(N'30000000-0000-0000-0000-000000000018', 'RETL-002', 'ApexPost Notebook', 'Branded pocket notebook', 'Stationery', 'Standard', 60.00, 1),
		(N'30000000-0000-0000-0000-000000000019', 'RETL-003', 'Address Book', 'Hardcover address book', 'Stationery', 'Standard', 125.00, 1),
		(N'30000000-0000-0000-0000-000000000020', 'RETL-004', 'Greeting Card', 'Blank all-occasion greeting card', 'Stationery', 'Standard', 40.00, 1),
		(N'30000000-0000-0000-0000-000000000021', 'RETL-005', 'Gift Wrap Sheet', 'Decorative gift wrapping sheet', 'Packaging', 'Standard', 25.00, 1),
		(N'30000000-0000-0000-0000-000000000022', 'RETL-006', 'Postage Stamp Sheet', 'Sheet of domestic postage stamps', 'Postage', 'Standard', 100.00, 1),
		(N'30000000-0000-0000-0000-000000000023', 'RETL-007', 'Desk Calendar', 'ApexPost annual desk calendar', 'Office Supplies', 'Standard', 85.00, 1),
		(N'30000000-0000-0000-0000-000000000024', 'RETL-008', 'Document Folder', 'Reusable document folder', 'Office Supplies', 'Standard', 45.00, 1),
		(N'30000000-0000-0000-0000-000000000025', 'SHIP-001', 'Small Parcel Service', 'Domestic small parcel handling', 'Shipping', 'Standard', 90.00, 1),
		(N'30000000-0000-0000-0000-000000000026', 'SHIP-002', 'Medium Parcel Service', 'Domestic medium parcel handling', 'Shipping', 'Standard', 160.00, 1),
		(N'30000000-0000-0000-0000-000000000027', 'SHIP-003', 'Large Parcel Service', 'Domestic large parcel handling', 'Shipping', 'Standard', 280.00, 1),
		(N'30000000-0000-0000-0000-000000000028', 'SHIP-004', 'Fragile Handling', 'Additional fragile parcel handling', 'Shipping', 'Standard', 75.00, 1),
		(N'30000000-0000-0000-0000-000000000029', 'MORD-001', 'Money Order Fee - Basic', 'Basic money order processing fee', 'Money Order', 'Standard', 30.00, 1),
		(N'30000000-0000-0000-0000-000000000030', 'MORD-002', 'Money Order Fee - Express', 'Express money order processing fee', 'Money Order', 'Standard', 65.00, 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.ShippingZones)
BEGIN
	INSERT INTO dbo.ShippingZones (Id, Code, Name, Destination, IsActive)
	VALUES
		(N'40000000-0000-0000-0000-000000000001', 'DOM-LOCAL', 'Local Metro', 'Same city or metropolitan area', 1),
		(N'40000000-0000-0000-0000-000000000002', 'DOM-STATE', 'Intra-State', 'Destinations within the same state', 1),
		(N'40000000-0000-0000-0000-000000000003', 'DOM-NORTH', 'North Zone', 'Northern India', 1),
		(N'40000000-0000-0000-0000-000000000004', 'DOM-SOUTH', 'South Zone', 'Southern India', 1),
		(N'40000000-0000-0000-0000-000000000005', 'DOM-EAST', 'East Zone', 'Eastern India', 1),
		(N'40000000-0000-0000-0000-000000000006', 'DOM-WEST', 'West Zone', 'Western India', 1),
		(N'40000000-0000-0000-0000-000000000007', 'DOM-CENTRAL', 'Central Zone', 'Central India', 1),
		(N'40000000-0000-0000-0000-000000000008', 'DOM-NE', 'North-East Zone', 'North-Eastern India', 1),
		(N'40000000-0000-0000-0000-000000000009', 'DOM-REMOTE', 'Remote Areas', 'Remote and difficult-to-reach areas', 1),
		(N'40000000-0000-0000-0000-000000000010', 'INT-REG', 'International Regional', 'International destinations', 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.ShippingRates)
BEGIN
	INSERT INTO dbo.ShippingRates (Id, ZoneId, ServiceLevel, BasePrice, PricePerKilogram, MaximumWeightKilograms, IsActive)
	VALUES
		(N'50000000-0000-0000-0000-000000000001', N'40000000-0000-0000-0000-000000000001', 'Standard', 35.00, 12.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000002', N'40000000-0000-0000-0000-000000000001', 'Express', 65.00, 22.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000003', N'40000000-0000-0000-0000-000000000001', 'Registered', 85.00, 30.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000004', N'40000000-0000-0000-0000-000000000002', 'Standard', 40.00, 14.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000005', N'40000000-0000-0000-0000-000000000002', 'Express', 70.00, 24.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000006', N'40000000-0000-0000-0000-000000000002', 'Registered', 92.00, 32.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000007', N'40000000-0000-0000-0000-000000000003', 'Standard', 48.00, 16.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000008', N'40000000-0000-0000-0000-000000000003', 'Express', 76.00, 26.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000009', N'40000000-0000-0000-0000-000000000003', 'Registered', 98.00, 34.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000010', N'40000000-0000-0000-0000-000000000004', 'Standard', 46.00, 15.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000011', N'40000000-0000-0000-0000-000000000004', 'Express', 74.00, 25.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000012', N'40000000-0000-0000-0000-000000000004', 'Registered', 96.00, 33.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000013', N'40000000-0000-0000-0000-000000000005', 'Standard', 50.00, 17.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000014', N'40000000-0000-0000-0000-000000000005', 'Express', 78.00, 27.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000015', N'40000000-0000-0000-0000-000000000005', 'Registered', 104.00, 36.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000016', N'40000000-0000-0000-0000-000000000006', 'Standard', 52.00, 18.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000017', N'40000000-0000-0000-0000-000000000006', 'Express', 82.00, 28.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000018', N'40000000-0000-0000-0000-000000000006', 'Registered', 108.00, 37.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000019', N'40000000-0000-0000-0000-000000000007', 'Standard', 56.00, 19.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000020', N'40000000-0000-0000-0000-000000000007', 'Express', 86.00, 29.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000021', N'40000000-0000-0000-0000-000000000007', 'Registered', 112.00, 38.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000022', N'40000000-0000-0000-0000-000000000008', 'Standard', 58.00, 20.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000023', N'40000000-0000-0000-0000-000000000008', 'Express', 90.00, 30.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000024', N'40000000-0000-0000-0000-000000000008', 'Registered', 118.00, 40.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000025', N'40000000-0000-0000-0000-000000000009', 'Standard', 62.00, 22.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000026', N'40000000-0000-0000-0000-000000000009', 'Express', 94.00, 32.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000027', N'40000000-0000-0000-0000-000000000009', 'Registered', 122.00, 42.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000028', N'40000000-0000-0000-0000-000000000010', 'Standard', 66.00, 24.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000029', N'40000000-0000-0000-0000-000000000010', 'Express', 98.00, 34.00, 30.00, 1),
		(N'50000000-0000-0000-0000-000000000030', N'40000000-0000-0000-0000-000000000010', 'Registered', 128.00, 44.00, 30.00, 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.CounterTerminals)
BEGIN
	INSERT INTO dbo.CounterTerminals (Id, BranchId, Code, Name, IsActive)
	VALUES
		(N'60000000-0000-0000-0000-000000000001', N'10000000-0000-0000-0000-000000000001', 'T-01', 'Counter 1', 1),
		(N'60000000-0000-0000-0000-000000000002', N'10000000-0000-0000-0000-000000000001', 'T-02', 'Counter 2', 1),
		(N'60000000-0000-0000-0000-000000000003', N'10000000-0000-0000-0000-000000000002', 'T-01', 'Counter 1', 1),
		(N'60000000-0000-0000-0000-000000000004', N'10000000-0000-0000-0000-000000000002', 'T-02', 'Counter 2', 1),
		(N'60000000-0000-0000-0000-000000000005', N'10000000-0000-0000-0000-000000000003', 'T-01', 'Counter 1', 1),
		(N'60000000-0000-0000-0000-000000000006', N'10000000-0000-0000-0000-000000000003', 'T-02', 'Counter 2', 1),
		(N'60000000-0000-0000-0000-000000000007', N'10000000-0000-0000-0000-000000000004', 'T-01', 'Counter 1', 1),
		(N'60000000-0000-0000-0000-000000000008', N'10000000-0000-0000-0000-000000000004', 'T-02', 'Counter 2', 1),
		(N'60000000-0000-0000-0000-000000000009', N'10000000-0000-0000-0000-000000000005', 'T-01', 'Counter 1', 1),
		(N'60000000-0000-0000-0000-000000000010', N'10000000-0000-0000-0000-000000000005', 'T-02', 'Counter 2', 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Inventory)
BEGIN
	DECLARE @branchId uniqueidentifier;
	DECLARE @productId uniqueidentifier;
	DECLARE @branchIndex int = 1;

	WHILE @branchIndex <= 5
	BEGIN
		DECLARE @productIndex int = 1;
		SET @branchId = CASE @branchIndex
			WHEN 1 THEN N'10000000-0000-0000-0000-000000000001'
			WHEN 2 THEN N'10000000-0000-0000-0000-000000000002'
			WHEN 3 THEN N'10000000-0000-0000-0000-000000000003'
			WHEN 4 THEN N'10000000-0000-0000-0000-000000000004'
			WHEN 5 THEN N'10000000-0000-0000-0000-000000000005'
		END;

		WHILE @productIndex <= 30
		BEGIN
			SET @productId = CASE @productIndex
				WHEN 1 THEN N'30000000-0000-0000-0000-000000000001'
				WHEN 2 THEN N'30000000-0000-0000-0000-000000000002'
				WHEN 3 THEN N'30000000-0000-0000-0000-000000000003'
				WHEN 4 THEN N'30000000-0000-0000-0000-000000000004'
				WHEN 5 THEN N'30000000-0000-0000-0000-000000000005'
				WHEN 6 THEN N'30000000-0000-0000-0000-000000000006'
				WHEN 7 THEN N'30000000-0000-0000-0000-000000000007'
				WHEN 8 THEN N'30000000-0000-0000-0000-000000000008'
				WHEN 9 THEN N'30000000-0000-0000-0000-000000000009'
				WHEN 10 THEN N'30000000-0000-0000-0000-000000000010'
				WHEN 11 THEN N'30000000-0000-0000-0000-000000000011'
				WHEN 12 THEN N'30000000-0000-0000-0000-000000000012'
				WHEN 13 THEN N'30000000-0000-0000-0000-000000000013'
				WHEN 14 THEN N'30000000-0000-0000-0000-000000000014'
				WHEN 15 THEN N'30000000-0000-0000-0000-000000000015'
				WHEN 16 THEN N'30000000-0000-0000-0000-000000000016'
				WHEN 17 THEN N'30000000-0000-0000-0000-000000000017'
				WHEN 18 THEN N'30000000-0000-0000-0000-000000000018'
				WHEN 19 THEN N'30000000-0000-0000-0000-000000000019'
				WHEN 20 THEN N'30000000-0000-0000-0000-000000000020'
				WHEN 21 THEN N'30000000-0000-0000-0000-000000000021'
				WHEN 22 THEN N'30000000-0000-0000-0000-000000000022'
				WHEN 23 THEN N'30000000-0000-0000-0000-000000000023'
				WHEN 24 THEN N'30000000-0000-0000-0000-000000000024'
				WHEN 25 THEN N'30000000-0000-0000-0000-000000000025'
				WHEN 26 THEN N'30000000-0000-0000-0000-000000000026'
				WHEN 27 THEN N'30000000-0000-0000-0000-000000000027'
				WHEN 28 THEN N'30000000-0000-0000-0000-000000000028'
				WHEN 29 THEN N'30000000-0000-0000-0000-000000000029'
				WHEN 30 THEN N'30000000-0000-0000-0000-000000000030'
			END;

			INSERT INTO dbo.Inventory (BranchId, ProductId, QuantityOnHand, ReorderLevel, MovementsJson)
			VALUES (@branchId, @productId, (10 + ((@branchIndex * 7) + @productIndex) % 50), 8, N'[{"Delta":0}]');

			SET @productIndex = @productIndex + 1;
		END;

		SET @branchIndex = @branchIndex + 1;
	END;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.CounterTransactions)
BEGIN
	DECLARE @transactionIndex int = 1;
	WHILE @transactionIndex <= 200
	BEGIN
		DECLARE @branchIdForTxn uniqueidentifier;
		DECLARE @staffIdForTxn uniqueidentifier;
		DECLARE @terminalIdForTxn uniqueidentifier;
		DECLARE @branchNumber int = ((@transactionIndex - 1) % 5) + 1;
		DECLARE @transactionDate datetimeoffset = DATEADD(day, -(@transactionIndex % 45), SYSUTCDATETIME());
		DECLARE @lineJson nvarchar(max);
		DECLARE @subtotal decimal(18,2) = 12.50 + (@transactionIndex % 40) * 3.75;
		DECLARE @shippingCharge decimal(18,2) = 9.99 + (@transactionIndex % 7) * 1.25;

		SET @branchIdForTxn = CASE @branchNumber
			WHEN 1 THEN N'10000000-0000-0000-0000-000000000001'
			WHEN 2 THEN N'10000000-0000-0000-0000-000000000002'
			WHEN 3 THEN N'10000000-0000-0000-0000-000000000003'
			WHEN 4 THEN N'10000000-0000-0000-0000-000000000004'
			WHEN 5 THEN N'10000000-0000-0000-0000-000000000005'
		END;

		SET @staffIdForTxn = CASE @branchNumber
			WHEN 1 THEN N'20000000-0000-0000-0000-000000000003'
			WHEN 2 THEN N'20000000-0000-0000-0000-000000000006'
			WHEN 3 THEN N'20000000-0000-0000-0000-000000000009'
			WHEN 4 THEN N'20000000-0000-0000-0000-000000000012'
			WHEN 5 THEN N'20000000-0000-0000-0000-000000000015'
		END;

		SET @terminalIdForTxn = CASE @branchNumber
			WHEN 1 THEN N'60000000-0000-0000-0000-000000000001'
			WHEN 2 THEN N'60000000-0000-0000-0000-000000000003'
			WHEN 3 THEN N'60000000-0000-0000-0000-000000000005'
			WHEN 4 THEN N'60000000-0000-0000-0000-000000000007'
			WHEN 5 THEN N'60000000-0000-0000-0000-000000000009'
		END;

		SET @lineJson = CONCAT(
			N'[',
			N'{"TaxCategory":"Standard","LineTotal":', CAST(@subtotal AS nvarchar(20)), N'},',
			N'{"TaxCategory":"Shipping","LineTotal":', CAST(@shippingCharge AS nvarchar(20)), N'}',
			N']');

		INSERT INTO dbo.CounterTransactions (
			Id, TransactionNumber, BranchId, StaffId, TerminalId, MoneyOrderId, MoneyOrderAmount, MoneyOrderFee,
			OccurredAtUtc, LinesJson, ShippingRateId, ShippingZoneName, ShippingServiceLevel, ShipmentType,
			ShippingWeightKilograms, Subtotal, TaxTotal, ShippingCharge, ShippingTaxRatePercent,
			ShippingTaxAmount, TotalAmount, PaymentMethod, Status, ResolvedAtUtc, ResolvedByStaffId,
			ResolutionReason, ShippingRecipientName, ShippingAddress, ParcelLengthCentimeters,
			ParcelWidthCentimeters, ParcelHeightCentimeters)
		VALUES (
			NEWID(), CONCAT('TXN-', YEAR(@transactionDate), '-', RIGHT('000' + CAST(@transactionIndex AS varchar(3)), 3)),
			@branchIdForTxn, @staffIdForTxn, @terminalIdForTxn, NULL, 0.00, 0.00,
			@transactionDate, @lineJson, NULL, 'North Zone', 'Express', 'Parcel', 1.25,
			@subtotal, 0.00, @shippingCharge, 0.00, 0.00,
			@subtotal + @shippingCharge, 'Card', 'Paid', DATEADD(minute, 5, @transactionDate), @staffIdForTxn,
			NULL, 'ApexPost Customer', '22 Market Road', 25.00, 18.00, 12.00
		);

		SET @transactionIndex = @transactionIndex + 1;
	END;
END;

COMMIT TRANSACTION;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

INSERT INTO dbo.StaffRoles (Code, Name)
SELECT source.Code, source.Name
FROM (VALUES
    ('CounterClerk', N'Counter Clerk'),
    ('BranchManager', N'Branch Manager'),
    ('Auditor', N'Auditor'),
    ('Administrator', N'Administrator')
) AS source(Code, Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.StaffRoles AS target WHERE target.Code = source.Code);

INSERT INTO dbo.ProductCategories (Code, Name)
SELECT source.Code, source.Name
FROM (VALUES
    (N'Packaging', N'Packaging'),
    (N'Stationery', N'Stationery'),
    (N'Office Supplies', N'Office Supplies'),
    (N'Postage', N'Postage'),
    (N'Shipping', N'Shipping'),
    (N'Money Order', N'Money Order')
) AS source(Code, Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductCategories AS target WHERE target.Code = source.Code);

INSERT INTO dbo.TaxCategories (Code, Name)
SELECT source.Code, source.Name
FROM (VALUES
    (N'Standard', N'Standard rate'),
    (N'Shipping', N'Shipping services')
) AS source(Code, Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.TaxCategories AS target WHERE target.Code = source.Code);

COMMIT TRANSACTION;

/*
    Part D - least-privilege access for the application login.

    The web application needs exactly the table permissions listed below, granted table by table (no schema-wide
    grant, so a table added by a future migration is NOT readable or writable until someone grants it on purpose).
    Rules:
      * Nothing is ever deleted by the application: there is no DELETE grant at all. Voids, refunds and
        deactivation are UPDATEs that keep history.
      * Append-only history (stock movements, audit, payments, lines, identity checks) gets SELECT + INSERT only.
      * Rows that legitimately change state get UPDATE as well (stock balance, transaction status, refunds,
        shipment status, notification delivery, master data edits by administrators).
      * Lookup tables get SELECT + INSERT (a new product/tax category or role is added, never changed).
      * EXECUTE on the three procedures. Procedures and tables share the owner dbo, so ownership chaining lets
        usp_AdjustStock write BranchStock/StockAdjustments with EXECUTE alone.
      * No DDL, no ALTER, no access to __EFMigrationsHistory: migrations are applied by a separate deployment
        identity (db_ddladmin) during release, not by the running application.
    Re-runnable: CREATE ROLE is guarded; GRANT/DENY are idempotent.
*/
USE ApexPostDb;
GO
IF DATABASE_PRINCIPAL_ID('apexpost_app') IS NULL
    CREATE ROLE apexpost_app AUTHORIZATION dbo;
GO

-- Master data (administrators create and edit; nothing is deleted - rows are deactivated with IsActive / IsOpen).
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.Branches          TO apexpost_app;
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.CounterTerminals  TO apexpost_app;
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.Staff             TO apexpost_app;
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.Products          TO apexpost_app;
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.ShippingZones     TO apexpost_app;
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.ShippingRates     TO apexpost_app;

-- Lookup tables: read, and add a new code; never change or remove one.
GRANT SELECT, INSERT ON OBJECT::dbo.StaffRoles        TO apexpost_app;
GRANT SELECT, INSERT ON OBJECT::dbo.ProductCategories TO apexpost_app;
GRANT SELECT, INSERT ON OBJECT::dbo.TaxCategories     TO apexpost_app;

-- Operational rows whose state changes.
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.BranchStock              TO apexpost_app;  -- balance + reorder level
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.CounterTransactions      TO apexpost_app;  -- Paid -> Voided / Refunded
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.Shipments                TO apexpost_app;  -- Booked -> Cancelled
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.MoneyOrders              TO apexpost_app;  -- Issued -> Refunded
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.OperationalNotifications TO apexpost_app;  -- delivery attempts

-- Append-only history: read and insert, never update.
GRANT SELECT, INSERT ON OBJECT::dbo.TransactionLines      TO apexpost_app;
GRANT SELECT, INSERT ON OBJECT::dbo.Payments              TO apexpost_app;
GRANT SELECT, INSERT ON OBJECT::dbo.StockAdjustments      TO apexpost_app;
GRANT SELECT, INSERT ON OBJECT::dbo.IdentityVerifications TO apexpost_app;
GRANT SELECT, INSERT ON OBJECT::dbo.AuditEntries          TO apexpost_app;

-- Stored procedures.
GRANT EXECUTE ON OBJECT::dbo.usp_DailyBranchSales TO apexpost_app;
GRANT EXECUTE ON OBJECT::dbo.usp_LowStockReport   TO apexpost_app;
GRANT EXECUTE ON OBJECT::dbo.usp_AdjustStock      TO apexpost_app;

-- Explicit DENYs on the most sensitive history, so a later broad grant (or role membership) cannot reopen them.
DENY UPDATE, DELETE ON OBJECT::dbo.AuditEntries     TO apexpost_app;
DENY UPDATE, DELETE ON OBJECT::dbo.StockAdjustments TO apexpost_app;
DENY DELETE ON OBJECT::dbo.CounterTransactions      TO apexpost_app;
DENY DELETE ON OBJECT::dbo.MoneyOrders              TO apexpost_app;
DENY SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.__EFMigrationsHistory TO apexpost_app;
GO

/*
    Verify: list the effective permissions of the role, one row per table and permission.
*/
SELECT OBJECT_NAME(p.major_id) AS ObjectName, p.permission_name, p.state_desc
FROM sys.database_permissions AS p
WHERE p.grantee_principal_id = DATABASE_PRINCIPAL_ID('apexpost_app')
ORDER BY ObjectName, p.permission_name;
GO
/*
    Map the application's login (example names - never commit passwords):
        CREATE USER [apexpost_web] FOR LOGIN [apexpost_web];
        ALTER ROLE apexpost_app ADD MEMBER [apexpost_web];
*/

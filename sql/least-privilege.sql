/*
    Part D - least-privilege access for the application login.
    The app needs DML on its tables and EXECUTE on the three procedures. It never needs DDL, DELETE on
    financial history, or ALTER: migrations are applied by a separate deployment identity (db_owner / db_ddladmin).
*/
USE ApexPostDb;
GO
IF DATABASE_PRINCIPAL_ID('apexpost_app') IS NULL
    CREATE ROLE apexpost_app AUTHORIZATION dbo;
GO
-- Read + write business tables (schema-level grant keeps new tables covered; DELETE is granted narrowly below).
GRANT SELECT, INSERT, UPDATE ON SCHEMA::dbo TO apexpost_app;
GRANT EXECUTE ON OBJECT::dbo.usp_DailyBranchSales TO apexpost_app;
GRANT EXECUTE ON OBJECT::dbo.usp_LowStockReport TO apexpost_app;
GRANT EXECUTE ON OBJECT::dbo.usp_AdjustStock TO apexpost_app;
-- History is append-only for the app: no DELETE on money, stock history or audit.
DENY DELETE ON OBJECT::dbo.CounterTransactions TO apexpost_app;
DENY DELETE ON OBJECT::dbo.TransactionLines TO apexpost_app;
DENY DELETE ON OBJECT::dbo.Payments TO apexpost_app;
DENY DELETE ON OBJECT::dbo.StockAdjustments TO apexpost_app;
DENY DELETE ON OBJECT::dbo.AuditEntries TO apexpost_app;
DENY DELETE ON OBJECT::dbo.MoneyOrders TO apexpost_app;
DENY DELETE ON OBJECT::dbo.IdentityVerifications TO apexpost_app;
DENY UPDATE ON OBJECT::dbo.AuditEntries TO apexpost_app;
-- The app must not change the migration history.
DENY INSERT, UPDATE, DELETE ON OBJECT::dbo.__EFMigrationsHistory TO apexpost_app;
GO
/*
    Map the application's Windows / SQL login (example names - never commit passwords):
        CREATE USER [apexpost_web] FOR LOGIN [apexpost_web];
        ALTER ROLE apexpost_app ADD MEMBER [apexpost_web];
    Procedures run with ownership chaining (owner dbo), so EXECUTE alone is enough for the tables they touch.
*/

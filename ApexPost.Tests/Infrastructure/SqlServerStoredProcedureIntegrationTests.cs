using System.Data;
using ApexPost.Application.Models;
using ApexPost.Domain.Enums;
using ApexPost.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace ApexPost.Tests.Infrastructure;

public sealed class SqlServerStoredProcedureIntegrationTests
{
    [Fact]
    public async Task Stored_procedures_work_against_a_real_local_sql_server_database()
    {
        var dbName = $"ApexPostIntegration_{Guid.NewGuid():N}";
        var masterConnectionString = "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=False";

        await CreateDatabaseAsync(masterConnectionString, dbName);

        var connectionStringBuilder = new SqlConnectionStringBuilder(masterConnectionString)
        {
            InitialCatalog = dbName,
            MultipleActiveResultSets = true
        };

        var options = new DbContextOptionsBuilder<ApexPostDbContext>()
            .UseSqlServer(connectionStringBuilder.ConnectionString)
            .Options;

        await using (var context = new ApexPostDbContext(options))
        {
            await context.Database.MigrateAsync();
            await CreateStoredProceduresAsync(context);

            var branchId = Guid.NewGuid();
            var staffId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var transactionId = Guid.NewGuid();

            context.Branches.Add(new BranchEntity
            {
                Id = branchId,
                Code = "EAST",
                Name = "East Branch",
                Region = "North",
                IsOpen = true,
                OpeningTime = new TimeOnly(8, 0),
                ClosingTime = new TimeOnly(18, 0)
            });

            context.Staff.Add(new StaffMemberEntity
            {
                Id = staffId,
                Email = "ops@apexpost.test",
                FullName = "Ops Lead",
                Role = StaffRole.BranchManager,
                BranchId = branchId,
                IsActive = true,
                PasswordHash = "hash"
            });

            context.Products.Add(new ProductEntity
            {
                Id = productId,
                Sku = "SKU-1001",
                Name = "Parcel Tape",
                Description = "Heavy-duty parcel tape",
                Category = "Stationery",
                TaxCategory = "Standard",
                Price = 12.50m,
                IsActive = true
            });

            context.Inventory.Add(new InventoryItemEntity
            {
                BranchId = branchId,
                ProductId = productId,
                QuantityOnHand = 7,
                ReorderLevel = 5,
                MovementsJson = "[{\"Delta\":-1}]"
            });

            context.CounterTransactions.Add(new CounterTransactionEntity
            {
                Id = transactionId,
                TransactionNumber = "TXN-1001",
                BranchId = branchId,
                StaffId = staffId,
                OccurredAtUtc = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero),
                LinesJson = "[{\"TaxCategory\":\"Standard\",\"LineTotal\":12.50},{\"TaxCategory\":\"Shipping\",\"LineTotal\":9.99}]",
                ShippingZoneName = "North",
                ShippingServiceLevel = ShippingServiceLevel.Express,
                ShipmentType = ShipmentType.Parcel,
                ShippingWeightKilograms = 1.5m,
                Subtotal = 12.50m,
                TaxTotal = 0m,
                ShippingCharge = 9.99m,
                ShippingTaxRatePercent = 0m,
                ShippingTaxAmount = 0m,
                TotalAmount = 22.49m,
                PaymentMethod = PaymentMethod.Cash,
                Status = TransactionStatus.Paid
            });

            await context.SaveChangesAsync();

            var dailySales = await context.Database
                .SqlQuery<DailyBranchSalesRow>(FormattableStringFactory.Create(
                    "EXEC dbo.usp_DailyBranchSales @BranchCode = {0}, @Date = {1}",
                    "EAST",
                    new DateOnly(2026, 11, 1)))
                .ToListAsync();

            var salesRow = Assert.Single(dailySales);
            Assert.Equal("EAST", salesRow.BranchCode);
            Assert.Equal(new DateOnly(2026, 11, 1), salesRow.SaleDate);
            Assert.Equal(9.99m, salesRow.ServiceRevenue);
            Assert.Equal(12.50m, salesRow.RetailRevenue);

            var lowStockRows = await context.Database
                .SqlQuery<LowStockReportRow>(FormattableStringFactory.Create(
                    "EXEC dbo.usp_LowStockReport @BranchCode = {0}, @Threshold = {1}",
                    "EAST",
                    10))
                .ToListAsync();

            var lowStock = Assert.Single(lowStockRows);
            Assert.Equal("SKU-1001", lowStock.Sku);
            Assert.Equal(7, lowStock.QuantityOnHand);
            Assert.Equal(5, lowStock.ReorderLevel);

            await context.Database.ExecuteSqlAsync(FormattableStringFactory.Create(
                "EXEC dbo.usp_AdjustStock @BranchCode = {0}, @Sku = {1}, @Delta = {2}, @Reason = {3}, @ChangedBy = {4}",
                "EAST",
                "SKU-1001",
                -2,
                "Cycle count adjustment",
                "ops@apexpost.test"));

            context.ChangeTracker.Clear();
            var inventoryRow = await context.Inventory.SingleAsync(i => i.BranchId == branchId && i.ProductId == productId);
            Assert.Equal(5, inventoryRow.QuantityOnHand);
        }

        await DropDatabaseAsync(masterConnectionString, dbName);
    }

    private static async Task CreateDatabaseAsync(string masterConnectionString, string databaseName)
    {
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = $"IF DB_ID('{databaseName}') IS NULL CREATE DATABASE [{databaseName}];";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CreateStoredProceduresAsync(ApexPostDbContext context)
    {
        var scripts = new[]
        {
            @"CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
    @BranchCode varchar(20),
    @Date date
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @BranchCode IS NULL OR LTRIM(RTRIM(@BranchCode)) = ''
        THROW 50001, 'Branch code is required.', 1;

    IF @Date IS NULL
        THROW 50002, 'Sales date is required.', 1;

    SELECT
        b.Code AS BranchCode,
        CAST(ct.OccurredAtUtc AS date) AS SaleDate,
        SUM(CASE
                WHEN line.TaxCategory IN ('Postage', 'Shipping', 'Money Order') THEN line.LineTotal
                ELSE 0
            END) AS ServiceRevenue,
        SUM(CASE
                WHEN line.TaxCategory NOT IN ('Postage', 'Shipping', 'Money Order') THEN line.LineTotal
                ELSE 0
            END) AS RetailRevenue
    FROM dbo.Branches AS b
    INNER JOIN dbo.CounterTransactions AS ct
        ON ct.BranchId = b.Id
    OUTER APPLY OPENJSON(ct.LinesJson)
        WITH (
            TaxCategory nvarchar(50) '$.TaxCategory',
            LineTotal decimal(18,2) '$.LineTotal'
        ) AS line
    WHERE b.Code = @BranchCode
      AND CAST(ct.OccurredAtUtc AS date) = @Date
      AND ct.Status IN ('Paid', 'Completed')
    GROUP BY b.Code, CAST(ct.OccurredAtUtc AS date)
    ORDER BY SaleDate;
END;",
            @"CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport
    @BranchCode varchar(20) = NULL,
    @Threshold int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Threshold IS NULL OR @Threshold < 0
        THROW 50003, 'Threshold must be a non-negative integer.', 1;

    SELECT
        b.Code AS BranchCode,
        p.Sku,
        p.Name AS ProductName,
        i.QuantityOnHand,
        i.ReorderLevel,
        (i.QuantityOnHand - i.ReorderLevel) AS BelowReorderLevel
    FROM dbo.Inventory AS i
    INNER JOIN dbo.Branches AS b
        ON b.Id = i.BranchId
    INNER JOIN dbo.Products AS p
        ON p.Id = i.ProductId
    WHERE i.QuantityOnHand <= @Threshold
      AND (@BranchCode IS NULL OR b.Code = @BranchCode)
    ORDER BY b.Code, p.Sku
    OPTION (RECOMPILE);
END;",
            @"CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
    @BranchCode varchar(20),
    @Sku varchar(60),
    @Delta int,
    @Reason nvarchar(200),
    @ChangedBy varchar(120)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @BranchCode IS NULL OR LTRIM(RTRIM(@BranchCode)) = ''
        THROW 50004, 'Branch code is required.', 1;

    IF @Sku IS NULL OR LTRIM(RTRIM(@Sku)) = ''
        THROW 50005, 'Product SKU is required.', 1;

    IF @Delta = 0
        THROW 50006, 'Stock delta cannot be zero.', 1;

    IF @Reason IS NULL OR LTRIM(RTRIM(@Reason)) = ''
        THROW 50007, 'Adjustment reason is required.', 1;

    IF @ChangedBy IS NULL OR LTRIM(RTRIM(@ChangedBy)) = ''
        THROW 50008, 'ChangedBy is required.', 1;

    DECLARE @BranchId uniqueidentifier;
    DECLARE @ProductId uniqueidentifier;
    DECLARE @ActorId uniqueidentifier;
    DECLARE @NewQuantity int;
    DECLARE @BranchNotFoundMessage nvarchar(200);
    DECLARE @ProductNotFoundMessage nvarchar(200);
    DECLARE @MissingActorMessage nvarchar(200);

    SELECT @BranchId = Id
    FROM dbo.Branches
    WHERE Code = @BranchCode;

    IF @BranchId IS NULL
    BEGIN
        SET @BranchNotFoundMessage = CONCAT('Branch code not found: ', @BranchCode);
        THROW 50009, @BranchNotFoundMessage, 1;
    END;

    SELECT @ProductId = Id
    FROM dbo.Products
    WHERE Sku = UPPER(LTRIM(RTRIM(@Sku)));

    IF @ProductId IS NULL
    BEGIN
        SET @ProductNotFoundMessage = CONCAT('Product SKU not found: ', @Sku);
        THROW 50010, @ProductNotFoundMessage, 1;
    END;

    SELECT @ActorId = Id
    FROM dbo.Staff
    WHERE Email = @ChangedBy OR FullName = @ChangedBy;

    IF @ActorId IS NULL
    BEGIN
        SET @MissingActorMessage = CONCAT('ChangedBy must match a staff email or name: ', @ChangedBy);
        THROW 50011, @MissingActorMessage, 1;
    END;

    BEGIN TRY
        BEGIN TRAN;

        SELECT @NewQuantity = QuantityOnHand
        FROM dbo.Inventory
        WHERE BranchId = @BranchId AND ProductId = @ProductId;

        IF @NewQuantity IS NULL
        BEGIN
            DECLARE @MissingInventoryMessage nvarchar(200) = CONCAT('No stock row exists for branch ', @BranchCode, ' and product ', @Sku);
            THROW 50012, @MissingInventoryMessage, 1;
        END;

        IF (@NewQuantity + @Delta) < 0
        BEGIN
            DECLARE @NegativeStockMessage nvarchar(200) = CONCAT('Adjustment would reduce stock below zero for ', @Sku, ' at ', @BranchCode);
            THROW 50013, @NegativeStockMessage, 1;
        END;

        UPDATE dbo.Inventory
        SET QuantityOnHand = QuantityOnHand + @Delta
        WHERE BranchId = @BranchId
          AND ProductId = @ProductId;

        INSERT INTO dbo.OperationalNotifications
            (Id, Type, Message, BranchId, ActorId, OccurredAtUtc, CorrelationId, ReferenceId)
        VALUES
            (NEWID(), 'StockAdjustment', CONCAT('Adjusted stock for ', @Sku, ' by ', @Delta, ': ', @Reason), @BranchId, @ActorId, SYSUTCDATETIME(), NEWID(), @Sku);

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRAN;

        THROW;
    END CATCH;
END;"
        };

        foreach (var script in scripts)
        {
            await context.Database.ExecuteSqlRawAsync(script);
        }
    }

    private static async Task DropDatabaseAsync(string masterConnectionString, string databaseName)
    {
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];";
        await command.ExecuteNonQueryAsync();
    }
}

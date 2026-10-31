CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
    @BranchId uniqueidentifier,
    @BusinessDate date
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StartUtc datetimeoffset(3) = TODATETIMEOFFSET(CONVERT(datetime2(3), @BusinessDate), '+00:00');
    DECLARE @EndUtc datetimeoffset(3) = DATEADD(day, 1, @StartUtc);

    SELECT
        @BranchId AS BranchId,
        @BusinessDate AS BusinessDate,
        CAST(COALESCE(SUM(t.Subtotal + t.TaxTotal - t.ShippingTaxAmount), 0) AS decimal(18, 2)) AS RetailSales,
        CAST(COALESCE(SUM(t.ShippingCharge + t.ShippingTaxAmount + t.MoneyOrderAmount + t.MoneyOrderFee), 0) AS decimal(18, 2)) AS ServiceSales,
        CAST(COALESCE(SUM(t.TotalAmount), 0) AS decimal(18, 2)) AS TotalSales,
        COUNT_BIG(*) AS TransactionCount
    FROM dbo.CounterTransactions AS t
    WHERE t.BranchId = @BranchId
      AND t.OccurredAt >= @StartUtc
      AND t.OccurredAt < @EndUtc
      AND t.Status IN ('Paid', 'Completed');
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport
    @BranchId uniqueidentifier,
    @Threshold int = 5
AS
BEGIN
    SET NOCOUNT ON;
    IF @Threshold < 0
        THROW 51010, 'The low-stock threshold must be zero or greater.', 1;

    SELECT
        stock.BranchId,
        stock.ProductId,
        product.Sku,
        product.Name AS ProductName,
        stock.QuantityOnHand,
        stock.ReorderLevel,
        @Threshold AS ReportThreshold
    FROM dbo.BranchStock AS stock
    INNER JOIN dbo.Products AS product ON product.Id = stock.ProductId
    WHERE stock.BranchId = @BranchId
      AND stock.QuantityOnHand <= @Threshold
      AND product.IsActive = 1
    ORDER BY stock.QuantityOnHand, product.Sku;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
    @BranchId uniqueidentifier,
    @ProductId uniqueidentifier,
    @QuantityChange int,
    @Reason nvarchar(250),
    @StaffId uniqueidentifier,
    @CorrelationId varchar(64)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @QuantityChange = 0
        THROW 51011, 'A stock adjustment must change the quantity.', 1;
    IF NULLIF(LTRIM(RTRIM(@Reason)), N'') IS NULL
        THROW 51012, 'A stock adjustment reason is required.', 1;
    IF NOT EXISTS (
        SELECT 1
        FROM dbo.Staff
        WHERE Id = @StaffId
          AND IsActive = 1
          AND Role IN ('BranchManager', 'Administrator')
          AND (BranchId = @BranchId OR Role = 'Administrator')
    )
        THROW 51013, 'An active manager or administrator authorized for this branch is required.', 1;

    BEGIN TRANSACTION;

    UPDATE dbo.BranchStock WITH (UPDLOCK, HOLDLOCK)
       SET QuantityOnHand = QuantityOnHand + @QuantityChange,
           UpdatedAtUtc = SYSUTCDATETIME(),
           UpdatedByStaffId = @StaffId,
           AuditCorrelationId = @CorrelationId
     WHERE BranchId = @BranchId
       AND ProductId = @ProductId
       AND QuantityOnHand + @QuantityChange >= 0;

    IF @@ROWCOUNT = 0
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51014, 'The stock row is missing or the adjustment would make stock negative.', 1;
    END;

    INSERT INTO dbo.StockAdjustments
        (Id, BranchId, ProductId, StaffId, QuantityChange, Reason, OccurredAt,
         CreatedByStaffId, AuditCorrelationId)
    VALUES
        (NEWID(), @BranchId, @ProductId, @StaffId, @QuantityChange, @Reason,
         TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'), @StaffId, @CorrelationId);

    COMMIT TRANSACTION;
END;
GO

USE ApexPostDb;
GO

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
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
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport
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
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
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
END;
GO

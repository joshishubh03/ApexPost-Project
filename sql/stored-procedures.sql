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
				WHEN tl.TaxCategory IN ('Postage', 'Shipping', 'Money Order') THEN tl.LineTotal
				ELSE 0
			END) AS ServiceRevenue,
		SUM(CASE
				WHEN tl.TaxCategory NOT IN ('Postage', 'Shipping', 'Money Order') THEN tl.LineTotal
				ELSE 0
			END) AS RetailRevenue
	FROM dbo.Branches AS b
	INNER JOIN dbo.CounterTransactions AS ct
		ON ct.BranchId = b.Id
	LEFT JOIN dbo.TransactionLines AS tl
		ON tl.CounterTransactionId = ct.Id
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
		bs.QuantityOnHand,
		bs.ReorderLevel,
		(bs.QuantityOnHand - bs.ReorderLevel) AS BelowReorderLevel
	FROM dbo.BranchStock AS bs
	INNER JOIN dbo.Branches AS b
		ON b.Id = bs.BranchId
	INNER JOIN dbo.Products AS p
		ON p.Id = bs.ProductId
	WHERE bs.QuantityOnHand <= @Threshold
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
	DECLARE @NewQuantity int;

	SELECT @BranchId = Id
	FROM dbo.Branches
	WHERE Code = @BranchCode;

	IF @BranchId IS NULL
		THROW 50009, CONCAT('Branch code not found: ', @BranchCode), 1;

	SELECT @ProductId = Id
	FROM dbo.Products
	WHERE Sku = UPPER(LTRIM(RTRIM(@Sku)));

	IF @ProductId IS NULL
		THROW 50010, CONCAT('Product SKU not found: ', @Sku), 1;

	BEGIN TRY
		BEGIN TRAN;

		SELECT @NewQuantity = QuantityOnHand
		FROM dbo.BranchStock
		WHERE BranchId = @BranchId AND ProductId = @ProductId;

		IF @NewQuantity IS NULL
			THROW 50011, CONCAT('No stock row exists for branch ', @BranchCode, ' and product ', @Sku), 1;

		IF (@NewQuantity + @Delta) < 0
			THROW 50012, CONCAT('Adjustment would reduce stock below zero for ', @Sku, ' at ', @BranchCode), 1;

		UPDATE dbo.BranchStock
		SET QuantityOnHand = QuantityOnHand + @Delta,
			ModifiedAtUtc = SYSUTCDATETIME(),
			ModifiedBy = @ChangedBy
		WHERE BranchId = @BranchId
		  AND ProductId = @ProductId;

		INSERT INTO dbo.StockAdjustments
			(Id, BranchId, ProductId, QuantityDelta, Reason, ChangedAtUtc, ChangedBy)
		VALUES
			(NEWID(), @BranchId, @ProductId, @Delta, @Reason, SYSUTCDATETIME(), @ChangedBy);

		COMMIT TRAN;
	END TRY
	BEGIN CATCH
		IF XACT_STATE() <> 0
			ROLLBACK TRAN;

		THROW;
	END CATCH;
END;
GO

USE ApexPostDb;
GO

SET XACT_ABORT ON;
BEGIN TRY
	BEGIN TRANSACTION;

	DECLARE @BranchCode varchar(12) = 'BR1';
	DECLARE @Sku varchar(24) = 'SKU-1001';
	DECLARE @StaffId uniqueidentifier = (SELECT TOP (1) Id FROM dbo.Staff WHERE IsActive = 1);
	DECLARE @BranchId uniqueidentifier = (SELECT TOP (1) Id FROM dbo.Branches WHERE Code = @BranchCode);
	DECLARE @ProductId uniqueidentifier = (SELECT TOP (1) Id FROM dbo.Products WHERE Sku = @Sku);
	DECLARE @InventoryId uniqueidentifier = (SELECT TOP (1) Id FROM dbo.BranchStock WHERE BranchId = @BranchId AND ProductId = @ProductId);
	DECLARE @TransactionId uniqueidentifier = NEWID();
	DECLARE @LineTotal decimal(18,2) = 120.00;

	IF @InventoryId IS NULL
	BEGIN
		THROW 50001, 'No stock record exists for this branch and product.', 1;
	END;

	INSERT INTO dbo.CounterTransactions (
		Id, TransactionNumber, BranchId, StaffId, OccurredAtUtc, Subtotal, TaxTotal,
		ShippingCharge, ShippingTaxRatePercent, ShippingTaxAmount, TotalAmount,
		PaymentMethod, Status, CreatedAtUtc, CreatedBy, ModifiedAtUtc, RowVersion
	)
	VALUES (
		@TransactionId, 'APT-TEST-' + CONVERT(varchar(12), GETDATE(), 112), @BranchId, @StaffId,
		SYSUTCDATETIME(), @LineTotal, 0.00, 0.00, 0.00, 0.00, @LineTotal,
		'Cash', 'Paid', SYSUTCDATETIME(), 'sql-script', NULL, 0x
	);

	INSERT INTO dbo.TransactionLines (
		Id, CounterTransactionId, ProductId, Sku, ProductName, Quantity,
		UnitPrice, LineTotal, TaxCategory, TaxRatePercent, TaxAmount
	)
	SELECT
		NEWID(), @TransactionId, p.Id, p.Sku, p.Name, 1,
		p.Price, p.Price, p.TaxCategory, 0.00, 0.00
	FROM dbo.Products AS p
	WHERE p.Id = @ProductId;

	UPDATE dbo.BranchStock
	SET QuantityOnHand = QuantityOnHand - 1,
		ModifiedAtUtc = SYSUTCDATETIME()
	WHERE Id = @InventoryId;

	COMMIT TRANSACTION;
END TRY
BEGIN CATCH
	IF XACT_STATE() <> 0
		ROLLBACK TRANSACTION;

	THROW;
END CATCH;
GO

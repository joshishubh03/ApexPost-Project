USE ApexPostDb;
GO

SET NOCOUNT ON;
GO

/*
	Assignment 3 compliance script
	------------------------------
	This script records the additional SQL patterns the assignment explicitly expects:
	- an atomic multi-table sale transaction with XACT_ABORT, TRY...CATCH, XACT_STATE and THROW
	- a blocking/demo pattern for two sessions
	- a deadlock-avoidance note and consistent access-order guidance
	- a SARGable rewrite example for a non-SARGable predicate
*/

PRINT 'Assignment 3: multi-table sale transaction example';
GO

SET XACT_ABORT ON;
GO

BEGIN TRY
	BEGIN TRAN;

	DECLARE @BranchId uniqueidentifier;
	DECLARE @StaffId uniqueidentifier;
	DECLARE @ProductId uniqueidentifier;
	DECLARE @StockQty int;
	DECLARE @TransactionId uniqueidentifier = NEWID();
	DECLARE @TransactionNumber varchar(40) = CONCAT('TX-', CONVERT(varchar(20), SYSDATETIME(), 112), '-', ABS(CHECKSUM(NEWID())));

	SELECT TOP (1)
		@BranchId = Id,
		@StaffId = Id
	FROM dbo.Branches AS b
	CROSS JOIN dbo.Staff AS s
	WHERE b.Code = 'BR-001'
	  AND s.Email = 'admin@apexpost.local';

	SELECT TOP (1)
		@ProductId = Id,
		@StockQty = QuantityOnHand
	FROM dbo.Inventory
	WHERE BranchId = @BranchId
	  AND ProductId IN (SELECT Id FROM dbo.Products WHERE Sku = 'SKU-1001');

	IF @BranchId IS NULL OR @StaffId IS NULL OR @ProductId IS NULL
		THROW 50021, 'Required sale context is missing.', 1;

	IF @StockQty < 1
		THROW 50022, 'Insufficient stock to complete the sale.', 1;

	INSERT INTO dbo.CounterTransactions (
		Id,
		BranchId,
		StaffId,
		TransactionNumber,
		OccurredAtUtc,
		Status,
		Subtotal,
		TaxTotal,
		ShippingCharge,
		ShippingTaxAmount,
		TotalAmount,
		PaymentMethod,
		ShipmentType,
		ShippingServiceLevel,
		ShippingZoneName,
		ShippingWeightKilograms,
		LinesJson,
		CorrelationId
	)
	VALUES (
		@TransactionId,
		@BranchId,
		@StaffId,
		@TransactionNumber,
		SYSUTCDATETIME(),
		'Paid',
		CAST(120.00 AS decimal(18,2)),
		CAST(0.00 AS decimal(18,2)),
		CAST(0.00 AS decimal(18,2)),
		CAST(0.00 AS decimal(18,2)),
		CAST(120.00 AS decimal(18,2)),
		'Cash',
		'Parcel',
		'Standard',
		'North Zone',
		CAST(1.25 AS decimal(18,2)),
		N'[{"ProductId":"' + CAST(@ProductId AS varchar(36)) + '","Sku":"SKU-1001","TaxCategory":"Standard","LineTotal":120.00}]',
		NEWID()
	);

	INSERT INTO dbo.OperationalNotifications (
		Id,
		Type,
		Message,
		BranchId,
		ActorId,
		OccurredAtUtc,
		CorrelationId,
		ReferenceId
	)
	VALUES (
		NEWID(),
		'SaleCompleted',
		'Customer sale recorded successfully.',
		@BranchId,
		@StaffId,
		SYSUTCDATETIME(),
		NEWID(),
		CONVERT(varchar(40), @TransactionId)
	);

	UPDATE dbo.Inventory
	SET QuantityOnHand = QuantityOnHand - 1,
		MovementsJson = CONCAT('{"change":-1,"reason":"sale","at":"', CONVERT(varchar(50), SYSUTCDATETIME(), 127), '"}')
	WHERE BranchId = @BranchId
	  AND ProductId = @ProductId;

	IF @@ROWCOUNT = 0
		THROW 50023, 'The stock row was not updated.', 1;

	COMMIT TRAN;
	PRINT 'Transaction committed successfully.';
END TRY
BEGIN CATCH
	IF XACT_STATE() <> 0
		ROLLBACK TRAN;

	DECLARE @ErrorMessage nvarchar(4000) = ERROR_MESSAGE();
	DECLARE @ErrorNumber int = ERROR_NUMBER();
	PRINT CONCAT('Sale failed: ', @ErrorNumber, ' - ', @ErrorMessage);
	THROW;
END CATCH;
GO

PRINT 'Assignment 3: blocking demo pattern';
GO
/*
	Session A:
		BEGIN TRAN;
		UPDATE dbo.Inventory
		SET QuantityOnHand = QuantityOnHand - 1
		WHERE BranchId = ... AND ProductId = ...;

	Session B:
		SELECT QuantityOnHand
		FROM dbo.Inventory
		WHERE BranchId = ... AND ProductId = ...;

	Expected observation: the second session waits on the lock until Session A commits or rolls back.
	This is a classic read/write blocking pattern under the default SQL Server isolation behavior.
	The example is intentionally brief and appropriate for SSMS sessions.
*/
GO

PRINT 'Assignment 3: SARGable rewrite example';
GO
/*
	Non-SARGable:
		WHERE YEAR(ct.OccurredAtUtc) = 2026

	Correct rewrite:
		WHERE ct.OccurredAtUtc >= '2026-01-01'
		  AND ct.OccurredAtUtc < '2027-01-01'

	Why:
	- the function is not applied to a constant value
	- SQL Server can use an index range seek instead of evaluating the function for every row
*/
GO

PRINT 'Assignment 3: deadlock avoidance guidance';
GO
/*
	Deadlock prevention design:
	1. Access tables in a consistent order: Branches -> Inventory -> CounterTransactions -> AuditEntries
	2. Keep transactions short and limited to the rows needed
	3. Never update multiple branches or products in reverse order within the same transaction
	4. Use optimistic concurrency on stock updates and convert DbUpdateConcurrencyException into HTTP 409
	5. Avoid long-running business logic inside the transaction
*/
GO

PRINT 'Assignment 3 compliance script complete.';
GO

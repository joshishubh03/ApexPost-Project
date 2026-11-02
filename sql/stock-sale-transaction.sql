/*
    Part B - one counter sale recorded atomically in T-SQL:
    CounterTransactions + TransactionLines + Payments + BranchStock decrement + StockAdjustments history.
    Pattern: SET XACT_ABORT ON, BEGIN TRAN inside TRY, XACT_STATE() in CATCH, THROW to re-raise.
    Table access order matches the application and usp_AdjustStock (BranchStock first, then history and
    the transaction rows), and the transaction is kept short (no user interaction inside it) - see
    docs/sql-transactions-and-concurrency.md for the deadlock-avoidance rules.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;   -- any run-time error dooms and rolls back the whole transaction

DECLARE @BranchCode varchar(12) = 'AP-001';
DECLARE @Sku varchar(32) = 'PACK-002';
DECLARE @Quantity int = 2;
DECLARE @ClerkEmail varchar(254) = 'clerk11@apexpost.local';

DECLARE @BranchId uniqueidentifier = (SELECT Id FROM dbo.Branches WHERE Code = @BranchCode);
DECLARE @StaffId uniqueidentifier = (SELECT Id FROM dbo.Staff WHERE Email = @ClerkEmail);
DECLARE @TerminalId uniqueidentifier = (SELECT TOP (1) Id FROM dbo.CounterTerminals WHERE BranchId = @BranchId AND IsActive = 1 ORDER BY Code);
DECLARE @ProductId uniqueidentifier, @ProductName nvarchar(120), @UnitPrice decimal(18,2), @TaxCategory nvarchar(60);
SELECT @ProductId = Id, @ProductName = Name, @UnitPrice = Price, @TaxCategory = TaxCategory
FROM dbo.Products WHERE Sku = @Sku AND IsActive = 1;

DECLARE @TransactionId uniqueidentifier = NEWID();
DECLARE @Now datetimeoffset(3) = SYSDATETIMEOFFSET();
DECLARE @LineTotal decimal(18,2) = @UnitPrice * @Quantity;
DECLARE @Message nvarchar(300);

BEGIN TRY
    IF @BranchId IS NULL OR @StaffId IS NULL OR @ProductId IS NULL OR @TerminalId IS NULL
        THROW 51000, N'Unknown branch, clerk, terminal or product.', 1;

    BEGIN TRANSACTION;

    -- 1. Stock first (same order as the app). The guarded UPDATE cannot drive stock below zero.
    UPDATE dbo.BranchStock
    SET QuantityOnHand = QuantityOnHand - @Quantity,
        ModifiedAtUtc = SYSUTCDATETIME(),
        ModifiedBy = @StaffId
    WHERE BranchId = @BranchId AND ProductId = @ProductId AND QuantityOnHand >= @Quantity;

    IF @@ROWCOUNT = 0
    BEGIN
        SET @Message = CONCAT(N'Not enough stock of ', @Sku, N' at ', @BranchCode, N'.');
        THROW 51001, @Message, 1;
    END;

    INSERT dbo.StockAdjustments (Id, BranchId, ProductId, StaffId, QuantityChange, Reason, OccurredAt, CreatedBy)
    VALUES (NEWID(), @BranchId, @ProductId, @StaffId, -@Quantity, CONCAT(N'Counter sale ', CONVERT(char(36), @TransactionId)), @Now, @StaffId);

    -- 2. The sale header, its line and its payment.
    INSERT dbo.CounterTransactions
        (Id, TransactionNumber, BranchId, StaffId, TerminalId, Subtotal, TaxTotal, ShippingCharge, ShippingTaxRatePercent,
         ShippingTaxAmount, MoneyOrderAmount, MoneyOrderFee, TotalAmount, Status, OccurredAt, CreatedBy)
    VALUES
        (@TransactionId, CONCAT('APT-SQL-', LEFT(CONVERT(char(36), @TransactionId), 8)), @BranchId, @StaffId, @TerminalId,
         @LineTotal, 0, 0, 0, 0, 0, 0, @LineTotal, 'Paid', @Now, @StaffId);

    INSERT dbo.TransactionLines
        (Id, CounterTransactionId, LineNumber, ProductId, Sku, ProductName, Quantity, UnitPrice, LineTotal, TaxCategory, TaxRatePercent, TaxAmount)
    VALUES
        (NEWID(), @TransactionId, 0, @ProductId, @Sku, @ProductName, @Quantity, @UnitPrice, @LineTotal, @TaxCategory, 0, 0);

    INSERT dbo.Payments (Id, CounterTransactionId, Method, Amount, PaidAt, CreatedBy)
    VALUES (NEWID(), @TransactionId, 'Cash', @LineTotal, @Now, @StaffId);

    COMMIT TRANSACTION;
    SELECT @TransactionId AS CounterTransactionId, N'Committed' AS Outcome;
END TRY
BEGIN CATCH
    -- XACT_STATE(): -1 = doomed (must roll back), 1 = committable but we still roll back, 0 = no transaction.
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;  -- re-raise the original error number, message and line to the caller
END CATCH;

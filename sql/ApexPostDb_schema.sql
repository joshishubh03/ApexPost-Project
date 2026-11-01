IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070807_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditEntries] (
        [Id] uniqueidentifier NOT NULL,
        [OccurredAtUtc] datetimeoffset NOT NULL,
        [Action] nvarchar(200) NOT NULL,
        [EntityType] nvarchar(100) NOT NULL,
        [EntityId] nvarchar(200) NOT NULL,
        [ActorId] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [CorrelationId] nvarchar(100) NOT NULL,
        [Amount] decimal(18,2) NULL,
        [Details] nvarchar(max) NULL,
        [Role] nvarchar(max) NULL,
        [ResultStatusCode] int NULL,
        [DurationMilliseconds] bigint NULL,
        [RouteValuesJson] nvarchar(max) NULL,
        CONSTRAINT [PK_AuditEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070807_InitialCreate'
)
BEGIN
    CREATE TABLE [Branches] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(12) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Region] nvarchar(100) NOT NULL,
        [IsOpen] bit NOT NULL,
        [OpeningTime] time NOT NULL,
        [ClosingTime] time NOT NULL,
        CONSTRAINT [PK_Branches] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070807_InitialCreate'
)
BEGIN
    CREATE TABLE [CounterTerminals] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [Code] nvarchar(12) NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_CounterTerminals] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070807_InitialCreate'
)
BEGIN
    CREATE TABLE [CounterTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [TransactionNumber] nvarchar(40) NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [StaffId] uniqueidentifier NOT NULL,
        [TerminalId] uniqueidentifier NULL,
        [MoneyOrderId] uniqueidentifier NULL,
        [MoneyOrderAmount] decimal(18,2) NOT NULL,
        [MoneyOrderFee] decimal(18,2) NOT NULL,
        [OccurredAtUtc] datetimeoffset NOT NULL,
        [LinesJson] nvarchar(max) NOT NULL,
        [ShippingRateId] uniqueidentifier NULL,
        [ShippingZoneName] nvarchar(max) NULL,
        [ShippingServiceLevel] nvarchar(max) NULL,
        [ShipmentType] nvarchar(max) NULL,
        [ShippingWeightKilograms] decimal(18,2) NULL,
        [Subtotal] decimal(18,2) NOT NULL,
        [TaxTotal] decimal(18,2) NOT NULL,
        [ShippingCharge] decimal(18,2) NOT NULL,
        [ShippingTaxRatePercent] decimal(18,2) NOT NULL,
        [ShippingTaxAmount] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [PaymentMethod] nvarchar(max) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [ResolvedAtUtc] datetimeoffset NULL,
        [ResolvedByStaffId] uniqueidentifier NULL,
        [ResolutionReason] nvarchar(max) NULL,
        [ShippingRecipientName] nvarchar(max) NULL,
        [ShippingAddress] nvarchar(max) NULL,
        [ParcelLengthCentimeters] decimal(18,2) NULL,
        [ParcelWidthCentimeters] decimal(18,2) NULL,
        [ParcelHeightCentimeters] decimal(18,2) NULL,
        CONSTRAINT [PK_CounterTransactions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070807_InitialCreate'
)
BEGIN
    CREATE TABLE [IdentityVerifications] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [StaffId] uniqueidentifier NOT NULL,
        [OccurredAtUtc] datetimeoffset NOT NULL,
        [DocumentType] nvarchar(max) NOT NULL,
        [MaskedDocumentIdentifier] nvarchar(32) NOT NULL,
        [MoneyOrderAmount] decimal(18,2) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [CorrelationId] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_IdentityVerifications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070807_InitialCreate'
)
BEGIN
    CREATE TABLE [Inventory] (
        [BranchId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [QuantityOnHand] int NOT NULL,
        [ReorderLevel] int NOT NULL,
        [MovementsJson] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Inventory] PRIMARY KEY ([BranchId], [ProductId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070807_InitialCreate'
)
BEGIN
    CREATE TABLE [MoneyOrders] (
        [Id] uniqueidentifier NOT NULL,
        [MoneyOrderNumber] nvarchar(40) NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [StaffId] uniqueidentifier NOT NULL,
        [OccurredAtUtc] datetimeoffset NOT NULL,
        [SenderName] nvarchar(max) NOT NULL,
        [RecipientName] nvarchar(max) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Fee] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [VerificationStatus] nvarchar(max) NOT NULL,
        [VerificationRecordId] uniqueidentifier NULL,
        [DocumentType] nvarchar(max) NULL,
        [MaskedDocumentIdentifier] nvarchar(max) NULL,
        [Status] nvarchar(max) NOT NULL,
        [RefundedAtUtc] datetimeoffset NULL,
        [RefundedByStaffId] uniqueidentifier NULL,
        [RefundReason] nvarchar(max) NULL,
        CONSTRAINT [PK_MoneyOrders] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070807_InitialCreate'
)
BEGIN
    CREATE TABLE [OperationalNotifications] (
        [Id] uniqueidentifier NOT NULL,
        [Type] nvarchar(120) NOT NULL,
        [Message] nvarchar(500) NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [ActorId] uniqueidentifier NOT NULL,
        [OccurredAtUtc] datetimeoffset NOT NULL,
        [CorrelationId] nvarchar(100) NOT NULL,
        [ReferenceId] nvarchar(200) NULL,
        CONSTRAINT [PK_OperationalNotifications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070807_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] uniqueidentifier NOT NULL,
        [Sku] nvarchar(60) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Category] nvarchar(100) NOT NULL,
        [TaxCategory] nvarchar(60) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070807_InitialCreate'
)
BEGIN
    CREATE TABLE [ShippingRates] (
        [Id] uniqueidentifier NOT NULL,
        [ZoneId] uniqueidentifier NOT NULL,
        [ServiceLevel] nvarchar(max) NOT NULL,
        [BasePrice] decimal(18,2) NOT NULL,
        [PricePerKilogram] decimal(18,2) NOT NULL,
        [MaximumWeightKilograms] decimal(18,2) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ShippingRates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070807_InitialCreate'
)
BEGIN
    CREATE TABLE [ShippingZones] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(12) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Destination] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ShippingZones] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070807_InitialCreate'
)
BEGIN
    CREATE TABLE [Staff] (
        [Id] uniqueidentifier NOT NULL,
        [Email] nvarchar(254) NOT NULL,
        [FullName] nvarchar(160) NOT NULL,
        [Role] nvarchar(max) NOT NULL,
        [BranchId] uniqueidentifier NULL,
        [IsActive] bit NOT NULL,
        [PasswordHash] nvarchar(512) NOT NULL,
        CONSTRAINT [PK_Staff] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070807_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Branches_Code] ON [Branches] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070807_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CounterTerminals_Code] ON [CounterTerminals] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070807_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_Sku] ON [Products] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070807_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ShippingZones_Code] ON [ShippingZones] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070807_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Staff_Email] ON [Staff] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070807_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101070807_InitialCreate', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070854_AddReportingIndexes'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MoneyOrders]') AND [c].[name] = N'Status');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [MoneyOrders] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [MoneyOrders] ALTER COLUMN [Status] nvarchar(450) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070854_AddReportingIndexes'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CounterTransactions]') AND [c].[name] = N'Status');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [CounterTransactions] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [CounterTransactions] ALTER COLUMN [Status] nvarchar(450) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070854_AddReportingIndexes'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CounterTransactions]') AND [c].[name] = N'ShippingZoneName');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [CounterTransactions] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [CounterTransactions] ALTER COLUMN [ShippingZoneName] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070854_AddReportingIndexes'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CounterTransactions]') AND [c].[name] = N'ShippingServiceLevel');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [CounterTransactions] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [CounterTransactions] ALTER COLUMN [ShippingServiceLevel] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070854_AddReportingIndexes'
)
BEGIN
    CREATE INDEX [IX_MoneyOrders_StatusIssuedAt] ON [MoneyOrders] ([Status], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070854_AddReportingIndexes'
)
BEGIN
    CREATE INDEX [IX_Inventory_LowStock] ON [Inventory] ([QuantityOnHand], [BranchId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070854_AddReportingIndexes'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_BranchDateStatus] ON [CounterTransactions] ([BranchId], [OccurredAtUtc], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070854_AddReportingIndexes'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_ShippingSearch] ON [CounterTransactions] ([ShippingZoneName], [ShippingServiceLevel], [ShippingRateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101070854_AddReportingIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101070854_AddReportingIndexes', N'10.0.0');
END;

COMMIT;
GO


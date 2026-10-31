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
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [Branches] (
        [Id] uniqueidentifier NOT NULL,
        [Code] varchar(12) NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Region] nvarchar(80) NOT NULL,
        [IsOpen] bit NOT NULL,
        [OpeningTime] time(0) NOT NULL,
        [ClosingTime] time(0) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(3) NULL,
        [CreatedByStaffId] uniqueidentifier NULL,
        [UpdatedByStaffId] uniqueidentifier NULL,
        [AuditCorrelationId] varchar(64) NULL,
        CONSTRAINT [PK_Branches] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Branches_OpeningHours] CHECK ([OpeningTime] < [ClosingTime])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [OutboxMessages] (
        [Id] uniqueidentifier NOT NULL,
        [EventType] varchar(160) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [OccurredAt] datetimeoffset(3) NOT NULL,
        [ProcessedAt] datetimeoffset(3) NULL,
        [AttemptCount] int NOT NULL,
        [LastError] nvarchar(2000) NULL,
        CONSTRAINT [PK_OutboxMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OutboxMessages_Attempts] CHECK ([AttemptCount] >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [ProductCategories] (
        [Code] nvarchar(60) NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(3) NULL,
        [CreatedByStaffId] uniqueidentifier NULL,
        [UpdatedByStaffId] uniqueidentifier NULL,
        [AuditCorrelationId] varchar(64) NULL,
        CONSTRAINT [PK_ProductCategories] PRIMARY KEY ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [ShippingZones] (
        [Id] uniqueidentifier NOT NULL,
        [Code] varchar(20) NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [Destination] nvarchar(120) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(3) NULL,
        [CreatedByStaffId] uniqueidentifier NULL,
        [UpdatedByStaffId] uniqueidentifier NULL,
        [AuditCorrelationId] varchar(64) NULL,
        CONSTRAINT [PK_ShippingZones] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [StaffRoles] (
        [Code] varchar(32) NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(3) NULL,
        [CreatedByStaffId] uniqueidentifier NULL,
        [UpdatedByStaffId] uniqueidentifier NULL,
        [AuditCorrelationId] varchar(64) NULL,
        CONSTRAINT [PK_StaffRoles] PRIMARY KEY ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [TaxCategories] (
        [Code] nvarchar(60) NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(3) NULL,
        [CreatedByStaffId] uniqueidentifier NULL,
        [UpdatedByStaffId] uniqueidentifier NULL,
        [AuditCorrelationId] varchar(64) NULL,
        CONSTRAINT [PK_TaxCategories] PRIMARY KEY ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [CounterTerminals] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [Code] varchar(12) NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(3) NULL,
        [CreatedByStaffId] uniqueidentifier NULL,
        [UpdatedByStaffId] uniqueidentifier NULL,
        [AuditCorrelationId] varchar(64) NULL,
        CONSTRAINT [PK_CounterTerminals] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CounterTerminals_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [OperationalNotifications] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NULL,
        [Kind] varchar(60) NOT NULL,
        [Message] nvarchar(500) NOT NULL,
        [OccurredAt] datetimeoffset(3) NOT NULL,
        [IsAcknowledged] bit NOT NULL,
        CONSTRAINT [PK_OperationalNotifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OperationalNotifications_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [ShippingRates] (
        [Id] uniqueidentifier NOT NULL,
        [ZoneId] uniqueidentifier NOT NULL,
        [ServiceLevel] varchar(24) NOT NULL,
        [BasePrice] decimal(18,2) NOT NULL,
        [PricePerKilogram] decimal(18,2) NOT NULL,
        [MaximumWeightKilograms] decimal(9,3) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(3) NULL,
        [CreatedByStaffId] uniqueidentifier NULL,
        [UpdatedByStaffId] uniqueidentifier NULL,
        [AuditCorrelationId] varchar(64) NULL,
        CONSTRAINT [PK_ShippingRates] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ShippingRates_MaximumWeight] CHECK ([MaximumWeightKilograms] > 0),
        CONSTRAINT [CK_ShippingRates_Prices] CHECK ([BasePrice] >= 0 AND [PricePerKilogram] >= 0),
        CONSTRAINT [FK_ShippingRates_ShippingZones_ZoneId] FOREIGN KEY ([ZoneId]) REFERENCES [ShippingZones] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [Staff] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NULL,
        [Email] varchar(254) NOT NULL,
        [FullName] nvarchar(120) NOT NULL,
        [Role] varchar(32) NOT NULL,
        [IsActive] bit NOT NULL,
        [PasswordHash] varchar(512) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(3) NULL,
        [CreatedByStaffId] uniqueidentifier NULL,
        [UpdatedByStaffId] uniqueidentifier NULL,
        [AuditCorrelationId] varchar(64) NULL,
        CONSTRAINT [PK_Staff] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Staff_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Staff_StaffRoles_Role] FOREIGN KEY ([Role]) REFERENCES [StaffRoles] ([Code]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] uniqueidentifier NOT NULL,
        [Sku] varchar(32) NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Category] nvarchar(60) NOT NULL,
        [TaxCategory] nvarchar(60) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(3) NULL,
        [CreatedByStaffId] uniqueidentifier NULL,
        [UpdatedByStaffId] uniqueidentifier NULL,
        [AuditCorrelationId] varchar(64) NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Products_Price] CHECK ([Price] >= 0),
        CONSTRAINT [FK_Products_ProductCategories_Category] FOREIGN KEY ([Category]) REFERENCES [ProductCategories] ([Code]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Products_TaxCategories_TaxCategory] FOREIGN KEY ([TaxCategory]) REFERENCES [TaxCategories] ([Code]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [AuditEntries] (
        [Id] uniqueidentifier NOT NULL,
        [StaffId] uniqueidentifier NULL,
        [BranchId] uniqueidentifier NULL,
        [Role] varchar(32) NOT NULL,
        [Action] nvarchar(100) NOT NULL,
        [EntityType] nvarchar(80) NOT NULL,
        [EntityId] uniqueidentifier NULL,
        [ResultStatusCode] int NOT NULL,
        [OccurredAt] datetimeoffset(3) NOT NULL,
        [CorrelationId] varchar(64) NOT NULL,
        [SafeDetailsJson] nvarchar(max) NULL,
        CONSTRAINT [PK_AuditEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditEntries_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AuditEntries_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [CounterTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [TransactionNumber] varchar(40) NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [StaffId] uniqueidentifier NOT NULL,
        [TerminalId] uniqueidentifier NULL,
        [ShippingRateId] uniqueidentifier NULL,
        [ShippingZoneName] nvarchar(80) NULL,
        [ShippingServiceLevel] varchar(24) NULL,
        [ShipmentType] varchar(24) NULL,
        [ShippingWeightKilograms] decimal(9,3) NULL,
        [Subtotal] decimal(18,2) NOT NULL,
        [TaxTotal] decimal(18,2) NOT NULL,
        [ShippingCharge] decimal(18,2) NOT NULL,
        [ShippingTaxRatePercent] decimal(5,2) NOT NULL,
        [ShippingTaxAmount] decimal(18,2) NOT NULL,
        [MoneyOrderAmount] decimal(18,2) NOT NULL,
        [MoneyOrderFee] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [Status] varchar(24) NOT NULL,
        [OccurredAt] datetimeoffset(3) NOT NULL,
        [ResolvedAt] datetimeoffset(3) NULL,
        [ResolvedByStaffId] uniqueidentifier NULL,
        [ResolutionReason] nvarchar(250) NULL,
        [ShippingRecipientName] nvarchar(100) NULL,
        [ShippingAddress] nvarchar(300) NULL,
        [ParcelLengthCentimeters] decimal(9,2) NULL,
        [ParcelWidthCentimeters] decimal(9,2) NULL,
        [ParcelHeightCentimeters] decimal(9,2) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(3) NULL,
        [CreatedByStaffId] uniqueidentifier NULL,
        [UpdatedByStaffId] uniqueidentifier NULL,
        [AuditCorrelationId] varchar(64) NULL,
        CONSTRAINT [PK_CounterTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CounterTransactions_Reversal] CHECK (([Status] IN ('Voided','Refunded') AND [ResolvedAt] IS NOT NULL AND [ResolvedByStaffId] IS NOT NULL AND [ResolutionReason] IS NOT NULL) OR ([Status] NOT IN ('Voided','Refunded') AND [ResolvedAt] IS NULL AND [ResolvedByStaffId] IS NULL AND [ResolutionReason] IS NULL)),
        CONSTRAINT [CK_CounterTransactions_Totals] CHECK ([Subtotal] >= 0 AND [TaxTotal] >= 0 AND [ShippingCharge] >= 0 AND [TotalAmount] >= 0),
        CONSTRAINT [FK_CounterTransactions_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CounterTransactions_CounterTerminals_TerminalId] FOREIGN KEY ([TerminalId]) REFERENCES [CounterTerminals] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CounterTransactions_ShippingRates_ShippingRateId] FOREIGN KEY ([ShippingRateId]) REFERENCES [ShippingRates] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CounterTransactions_Staff_ResolvedByStaffId] FOREIGN KEY ([ResolvedByStaffId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CounterTransactions_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [IdentityVerifications] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [StaffId] uniqueidentifier NOT NULL,
        [OccurredAt] datetimeoffset(3) NOT NULL,
        [DocumentType] varchar(32) NOT NULL,
        [MaskedDocumentIdentifier] varchar(16) NOT NULL,
        [MoneyOrderAmount] decimal(18,2) NOT NULL,
        [Status] varchar(24) NOT NULL,
        [CorrelationId] varchar(64) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(3) NULL,
        [CreatedByStaffId] uniqueidentifier NULL,
        [UpdatedByStaffId] uniqueidentifier NULL,
        [AuditCorrelationId] varchar(64) NULL,
        CONSTRAINT [PK_IdentityVerifications] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_IdentityVerifications_MaskedIdentifier] CHECK (LEN([MaskedDocumentIdentifier]) <= 16),
        CONSTRAINT [FK_IdentityVerifications_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_IdentityVerifications_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [BranchStock] (
        [BranchId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [QuantityOnHand] int NOT NULL,
        [ReorderLevel] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(3) NULL,
        [CreatedByStaffId] uniqueidentifier NULL,
        [UpdatedByStaffId] uniqueidentifier NULL,
        [AuditCorrelationId] varchar(64) NULL,
        CONSTRAINT [PK_BranchStock] PRIMARY KEY ([BranchId], [ProductId]),
        CONSTRAINT [CK_BranchStock_QuantityOnHand] CHECK ([QuantityOnHand] >= 0),
        CONSTRAINT [CK_BranchStock_ReorderLevel] CHECK ([ReorderLevel] >= 0),
        CONSTRAINT [FK_BranchStock_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BranchStock_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [StockAdjustments] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [StaffId] uniqueidentifier NOT NULL,
        [QuantityChange] int NOT NULL,
        [Reason] nvarchar(250) NOT NULL,
        [OccurredAt] datetimeoffset(3) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(3) NULL,
        [CreatedByStaffId] uniqueidentifier NULL,
        [UpdatedByStaffId] uniqueidentifier NULL,
        [AuditCorrelationId] varchar(64) NULL,
        CONSTRAINT [PK_StockAdjustments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_StockAdjustments_NonZero] CHECK ([QuantityChange] <> 0),
        CONSTRAINT [FK_StockAdjustments_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockAdjustments_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockAdjustments_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] uniqueidentifier NOT NULL,
        [CounterTransactionId] uniqueidentifier NOT NULL,
        [Method] varchar(24) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaidAt] datetimeoffset(3) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(3) NULL,
        [CreatedByStaffId] uniqueidentifier NULL,
        [UpdatedByStaffId] uniqueidentifier NULL,
        [AuditCorrelationId] varchar(64) NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [FK_Payments_CounterTransactions_CounterTransactionId] FOREIGN KEY ([CounterTransactionId]) REFERENCES [CounterTransactions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [TransactionLines] (
        [Id] uniqueidentifier NOT NULL,
        [CounterTransactionId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Sku] varchar(32) NOT NULL,
        [ProductName] nvarchar(120) NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        [TaxCategory] nvarchar(60) NOT NULL,
        [TaxRatePercent] decimal(5,2) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_TransactionLines] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TransactionLines_Amounts] CHECK ([Quantity] > 0 AND [UnitPrice] >= 0 AND [LineTotal] >= 0 AND [TaxAmount] >= 0),
        CONSTRAINT [FK_TransactionLines_CounterTransactions_CounterTransactionId] FOREIGN KEY ([CounterTransactionId]) REFERENCES [CounterTransactions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TransactionLines_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TransactionLines_TaxCategories_TaxCategory] FOREIGN KEY ([TaxCategory]) REFERENCES [TaxCategories] ([Code]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE TABLE [MoneyOrders] (
        [Id] uniqueidentifier NOT NULL,
        [CounterTransactionId] uniqueidentifier NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [StaffId] uniqueidentifier NOT NULL,
        [IdentityVerificationId] uniqueidentifier NULL,
        [MoneyOrderNumber] varchar(40) NOT NULL,
        [OccurredAt] datetimeoffset(3) NOT NULL,
        [SenderName] nvarchar(120) NOT NULL,
        [RecipientName] nvarchar(120) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Fee] decimal(18,2) NOT NULL,
        [VerificationStatus] varchar(24) NOT NULL,
        [DocumentType] varchar(32) NULL,
        [MaskedDocumentIdentifier] varchar(16) NULL,
        [Status] varchar(24) NOT NULL,
        [RefundedAt] datetimeoffset(3) NULL,
        [RefundedByStaffId] uniqueidentifier NULL,
        [RefundReason] nvarchar(250) NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(3) NULL,
        [CreatedByStaffId] uniqueidentifier NULL,
        [UpdatedByStaffId] uniqueidentifier NULL,
        [AuditCorrelationId] varchar(64) NULL,
        CONSTRAINT [PK_MoneyOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_MoneyOrders_Amounts] CHECK ([Amount] > 0 AND [Fee] >= 0),
        CONSTRAINT [CK_MoneyOrders_MaskedIdentifier] CHECK ([MaskedDocumentIdentifier] IS NULL OR LEN([MaskedDocumentIdentifier]) <= 16),
        CONSTRAINT [FK_MoneyOrders_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MoneyOrders_CounterTransactions_CounterTransactionId] FOREIGN KEY ([CounterTransactionId]) REFERENCES [CounterTransactions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MoneyOrders_IdentityVerifications_IdentityVerificationId] FOREIGN KEY ([IdentityVerificationId]) REFERENCES [IdentityVerifications] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MoneyOrders_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_BranchId_OccurredAt] ON [AuditEntries] ([BranchId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_CorrelationId] ON [AuditEntries] ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_StaffId] ON [AuditEntries] ([StaffId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Branches_Code] ON [Branches] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_BranchStock_BranchId_QuantityOnHand] ON [BranchStock] ([BranchId], [QuantityOnHand]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_BranchStock_ProductId] ON [BranchStock] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CounterTerminals_BranchId_Code] ON [CounterTerminals] ([BranchId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_BranchId_OccurredAt] ON [CounterTransactions] ([BranchId], [OccurredAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_ResolvedByStaffId] ON [CounterTransactions] ([ResolvedByStaffId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_ShippingRateId] ON [CounterTransactions] ([ShippingRateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_StaffId] ON [CounterTransactions] ([StaffId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_Status_OccurredAt] ON [CounterTransactions] ([Status], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_TerminalId] ON [CounterTransactions] ([TerminalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CounterTransactions_TransactionNumber] ON [CounterTransactions] ([TransactionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_IdentityVerifications_BranchId_OccurredAt] ON [IdentityVerifications] ([BranchId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_IdentityVerifications_StaffId] ON [IdentityVerifications] ([StaffId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_MoneyOrders_BranchId_OccurredAt] ON [MoneyOrders] ([BranchId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_MoneyOrders_CounterTransactionId] ON [MoneyOrders] ([CounterTransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_MoneyOrders_IdentityVerificationId] ON [MoneyOrders] ([IdentityVerificationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MoneyOrders_MoneyOrderNumber] ON [MoneyOrders] ([MoneyOrderNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_MoneyOrders_StaffId] ON [MoneyOrders] ([StaffId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_OperationalNotifications_BranchId_IsAcknowledged_OccurredAt] ON [OperationalNotifications] ([BranchId], [IsAcknowledged], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_ProcessedAt_OccurredAt] ON [OutboxMessages] ([ProcessedAt], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Payments_CounterTransactionId] ON [Payments] ([CounterTransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_Products_Category] ON [Products] ([Category]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_Sku] ON [Products] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_Products_TaxCategory] ON [Products] ([TaxCategory]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ShippingRates_ZoneId_ServiceLevel] ON [ShippingRates] ([ZoneId], [ServiceLevel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ShippingZones_Code] ON [ShippingZones] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_Staff_BranchId_Role_IsActive] ON [Staff] ([BranchId], [Role], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Staff_Email] ON [Staff] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_Staff_Role] ON [Staff] ([Role]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_StockAdjustments_BranchId_OccurredAt] ON [StockAdjustments] ([BranchId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_StockAdjustments_ProductId] ON [StockAdjustments] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_StockAdjustments_StaffId] ON [StockAdjustments] ([StaffId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_TransactionLines_CounterTransactionId] ON [TransactionLines] ([CounterTransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_TransactionLines_ProductId] ON [TransactionLines] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    CREATE INDEX [IX_TransactionLines_TaxCategory] ON [TransactionLines] ([TaxCategory]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132322_InitialSqlServerSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031132322_InitialSqlServerSchema', N'10.0.14');
END;

COMMIT;
GO

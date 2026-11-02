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

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031133837_AddProductCatalogueRowVersion'
)
BEGIN
    ALTER TABLE [Products] ADD [RowVersion] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031133837_AddProductCatalogueRowVersion'
)
BEGIN
    CREATE INDEX [IX_Products_RowVersion] ON [Products] ([RowVersion]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031133837_AddProductCatalogueRowVersion'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031133837_AddProductCatalogueRowVersion', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134126_AddRepositoryConcurrencyAndOperationalFields'
)
BEGIN
    ALTER TABLE [OperationalNotifications] ADD [CorrelationId] varchar(64) NOT NULL DEFAULT '';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134126_AddRepositoryConcurrencyAndOperationalFields'
)
BEGIN
    ALTER TABLE [OperationalNotifications] ADD [ReferenceId] varchar(80) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134126_AddRepositoryConcurrencyAndOperationalFields'
)
BEGIN
    ALTER TABLE [OperationalNotifications] ADD [StaffId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134126_AddRepositoryConcurrencyAndOperationalFields'
)
BEGIN
    ALTER TABLE [CounterTransactions] ADD [MoneyOrderId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134126_AddRepositoryConcurrencyAndOperationalFields'
)
BEGIN
    ALTER TABLE [AuditEntries] ADD [Amount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134126_AddRepositoryConcurrencyAndOperationalFields'
)
BEGIN
    ALTER TABLE [AuditEntries] ADD [DurationMilliseconds] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134126_AddRepositoryConcurrencyAndOperationalFields'
)
BEGIN
    ALTER TABLE [AuditEntries] ADD [RouteValuesJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134126_AddRepositoryConcurrencyAndOperationalFields'
)
BEGIN
    CREATE INDEX [IX_OperationalNotifications_StaffId] ON [OperationalNotifications] ([StaffId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134126_AddRepositoryConcurrencyAndOperationalFields'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_MoneyOrderId] ON [CounterTransactions] ([MoneyOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134126_AddRepositoryConcurrencyAndOperationalFields'
)
BEGIN
    ALTER TABLE [OperationalNotifications] ADD CONSTRAINT [FK_OperationalNotifications_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134126_AddRepositoryConcurrencyAndOperationalFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031134126_AddRepositoryConcurrencyAndOperationalFields', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134651_PreserveTransactionLineOrderAndLinkIntegrity'
)
BEGIN
    DROP INDEX [IX_TransactionLines_CounterTransactionId] ON [TransactionLines];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134651_PreserveTransactionLineOrderAndLinkIntegrity'
)
BEGIN
    DROP INDEX [IX_Payments_CounterTransactionId] ON [Payments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134651_PreserveTransactionLineOrderAndLinkIntegrity'
)
BEGIN
    DROP INDEX [IX_CounterTransactions_MoneyOrderId] ON [CounterTransactions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134651_PreserveTransactionLineOrderAndLinkIntegrity'
)
BEGIN
    ALTER TABLE [TransactionLines] ADD [LineNumber] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134651_PreserveTransactionLineOrderAndLinkIntegrity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TransactionLines_CounterTransactionId_LineNumber] ON [TransactionLines] ([CounterTransactionId], [LineNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134651_PreserveTransactionLineOrderAndLinkIntegrity'
)
BEGIN
    EXEC(N'ALTER TABLE [TransactionLines] ADD CONSTRAINT [CK_TransactionLines_LineNumber] CHECK ([LineNumber] >= 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134651_PreserveTransactionLineOrderAndLinkIntegrity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_CounterTransactionId] ON [Payments] ([CounterTransactionId]) WHERE [CounterTransactionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134651_PreserveTransactionLineOrderAndLinkIntegrity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CounterTransactions_MoneyOrderId] ON [CounterTransactions] ([MoneyOrderId]) WHERE [MoneyOrderId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031134651_PreserveTransactionLineOrderAndLinkIntegrity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031134651_PreserveTransactionLineOrderAndLinkIntegrity', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102130000_SyncModelChanges'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102130000_SyncModelChanges', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN

    SELECT ct.Id AS CounterTransactionId, ct.ShippingRateId, ct.ShipmentType, ct.ShippingWeightKilograms,
           ct.ParcelLengthCentimeters, ct.ParcelWidthCentimeters, ct.ParcelHeightCentimeters,
           ct.ShippingRecipientName, ct.ShippingAddress, ct.Status, ct.CreatedAtUtc, ct.CreatedByStaffId
    INTO dbo.[__Week3ShipmentsToMove]
    FROM dbo.CounterTransactions AS ct
    WHERE ct.ShippingRateId IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    ALTER TABLE [CounterTransactions] DROP CONSTRAINT [FK_CounterTransactions_ShippingRates_ShippingRateId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    ALTER TABLE [ShippingRates] DROP CONSTRAINT [CK_ShippingRates_MaximumWeight];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    ALTER TABLE [MoneyOrders] DROP CONSTRAINT [CK_MoneyOrders_MaskedIdentifier];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    ALTER TABLE [IdentityVerifications] DROP CONSTRAINT [CK_IdentityVerifications_MaskedIdentifier];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DROP INDEX [IX_CounterTransactions_ShippingRateId] ON [CounterTransactions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TaxCategories]') AND [c].[name] = N'AuditCorrelationId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [TaxCategories] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [TaxCategories] DROP COLUMN [AuditCorrelationId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StockAdjustments]') AND [c].[name] = N'AuditCorrelationId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [StockAdjustments] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [StockAdjustments] DROP COLUMN [AuditCorrelationId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StaffRoles]') AND [c].[name] = N'AuditCorrelationId');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [StaffRoles] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [StaffRoles] DROP COLUMN [AuditCorrelationId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Staff]') AND [c].[name] = N'AuditCorrelationId');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Staff] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [Staff] DROP COLUMN [AuditCorrelationId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ShippingZones]') AND [c].[name] = N'AuditCorrelationId');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [ShippingZones] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [ShippingZones] DROP COLUMN [AuditCorrelationId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ShippingRates]') AND [c].[name] = N'AuditCorrelationId');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [ShippingRates] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [ShippingRates] DROP COLUMN [AuditCorrelationId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'AuditCorrelationId');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [Products] DROP COLUMN [AuditCorrelationId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var7 nvarchar(max);
    SELECT @var7 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductCategories]') AND [c].[name] = N'AuditCorrelationId');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [ProductCategories] DROP CONSTRAINT ' + @var7 + ';');
    ALTER TABLE [ProductCategories] DROP COLUMN [AuditCorrelationId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var8 nvarchar(max);
    SELECT @var8 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payments]') AND [c].[name] = N'AuditCorrelationId');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Payments] DROP CONSTRAINT ' + @var8 + ';');
    ALTER TABLE [Payments] DROP COLUMN [AuditCorrelationId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var9 nvarchar(max);
    SELECT @var9 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MoneyOrders]') AND [c].[name] = N'AuditCorrelationId');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [MoneyOrders] DROP CONSTRAINT ' + @var9 + ';');
    ALTER TABLE [MoneyOrders] DROP COLUMN [AuditCorrelationId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var10 nvarchar(max);
    SELECT @var10 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[IdentityVerifications]') AND [c].[name] = N'AuditCorrelationId');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [IdentityVerifications] DROP CONSTRAINT ' + @var10 + ';');
    ALTER TABLE [IdentityVerifications] DROP COLUMN [AuditCorrelationId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var11 nvarchar(max);
    SELECT @var11 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CounterTransactions]') AND [c].[name] = N'AuditCorrelationId');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [CounterTransactions] DROP CONSTRAINT ' + @var11 + ';');
    ALTER TABLE [CounterTransactions] DROP COLUMN [AuditCorrelationId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var12 nvarchar(max);
    SELECT @var12 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CounterTransactions]') AND [c].[name] = N'ShippingRateId');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [CounterTransactions] DROP CONSTRAINT ' + @var12 + ';');
    ALTER TABLE [CounterTransactions] DROP COLUMN [ShippingRateId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var13 nvarchar(max);
    SELECT @var13 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CounterTransactions]') AND [c].[name] = N'ParcelHeightCentimeters');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [CounterTransactions] DROP CONSTRAINT ' + @var13 + ';');
    ALTER TABLE [CounterTransactions] DROP COLUMN [ParcelHeightCentimeters];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var14 nvarchar(max);
    SELECT @var14 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CounterTransactions]') AND [c].[name] = N'ParcelLengthCentimeters');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [CounterTransactions] DROP CONSTRAINT ' + @var14 + ';');
    ALTER TABLE [CounterTransactions] DROP COLUMN [ParcelLengthCentimeters];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var15 nvarchar(max);
    SELECT @var15 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CounterTransactions]') AND [c].[name] = N'ParcelWidthCentimeters');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [CounterTransactions] DROP CONSTRAINT ' + @var15 + ';');
    ALTER TABLE [CounterTransactions] DROP COLUMN [ParcelWidthCentimeters];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var16 nvarchar(max);
    SELECT @var16 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CounterTransactions]') AND [c].[name] = N'ShipmentType');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [CounterTransactions] DROP CONSTRAINT ' + @var16 + ';');
    ALTER TABLE [CounterTransactions] DROP COLUMN [ShipmentType];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var17 nvarchar(max);
    SELECT @var17 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CounterTransactions]') AND [c].[name] = N'ShippingAddress');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [CounterTransactions] DROP CONSTRAINT ' + @var17 + ';');
    ALTER TABLE [CounterTransactions] DROP COLUMN [ShippingAddress];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var18 nvarchar(max);
    SELECT @var18 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CounterTransactions]') AND [c].[name] = N'ShippingRecipientName');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [CounterTransactions] DROP CONSTRAINT ' + @var18 + ';');
    ALTER TABLE [CounterTransactions] DROP COLUMN [ShippingRecipientName];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var19 nvarchar(max);
    SELECT @var19 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CounterTransactions]') AND [c].[name] = N'ShippingServiceLevel');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [CounterTransactions] DROP CONSTRAINT ' + @var19 + ';');
    ALTER TABLE [CounterTransactions] DROP COLUMN [ShippingServiceLevel];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var20 nvarchar(max);
    SELECT @var20 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CounterTransactions]') AND [c].[name] = N'ShippingWeightKilograms');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [CounterTransactions] DROP CONSTRAINT ' + @var20 + ';');
    ALTER TABLE [CounterTransactions] DROP COLUMN [ShippingWeightKilograms];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var21 nvarchar(max);
    SELECT @var21 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CounterTransactions]') AND [c].[name] = N'ShippingZoneName');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [CounterTransactions] DROP CONSTRAINT ' + @var21 + ';');
    ALTER TABLE [CounterTransactions] DROP COLUMN [ShippingZoneName];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var22 nvarchar(max);
    SELECT @var22 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CounterTerminals]') AND [c].[name] = N'AuditCorrelationId');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [CounterTerminals] DROP CONSTRAINT ' + @var22 + ';');
    ALTER TABLE [CounterTerminals] DROP COLUMN [AuditCorrelationId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var23 nvarchar(max);
    SELECT @var23 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BranchStock]') AND [c].[name] = N'AuditCorrelationId');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [BranchStock] DROP CONSTRAINT ' + @var23 + ';');
    ALTER TABLE [BranchStock] DROP COLUMN [AuditCorrelationId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var24 nvarchar(max);
    SELECT @var24 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Branches]') AND [c].[name] = N'AuditCorrelationId');
    IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [Branches] DROP CONSTRAINT ' + @var24 + ';');
    ALTER TABLE [Branches] DROP COLUMN [AuditCorrelationId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[TaxCategories].[UpdatedByStaffId]', N'ModifiedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[TaxCategories].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[TaxCategories].[CreatedByStaffId]', N'CreatedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[StockAdjustments].[UpdatedByStaffId]', N'ModifiedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[StockAdjustments].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[StockAdjustments].[CreatedByStaffId]', N'CreatedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[StaffRoles].[UpdatedByStaffId]', N'ModifiedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[StaffRoles].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[StaffRoles].[CreatedByStaffId]', N'CreatedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[Staff].[UpdatedByStaffId]', N'ModifiedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[Staff].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[Staff].[CreatedByStaffId]', N'CreatedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[ShippingZones].[UpdatedByStaffId]', N'ModifiedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[ShippingZones].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[ShippingZones].[CreatedByStaffId]', N'CreatedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[ShippingRates].[UpdatedByStaffId]', N'ModifiedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[ShippingRates].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[ShippingRates].[CreatedByStaffId]', N'CreatedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[Products].[UpdatedByStaffId]', N'ModifiedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[Products].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[Products].[CreatedByStaffId]', N'CreatedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[ProductCategories].[UpdatedByStaffId]', N'ModifiedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[ProductCategories].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[ProductCategories].[CreatedByStaffId]', N'CreatedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[Payments].[UpdatedByStaffId]', N'ModifiedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[Payments].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[Payments].[CreatedByStaffId]', N'CreatedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[MoneyOrders].[UpdatedByStaffId]', N'ModifiedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[MoneyOrders].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[MoneyOrders].[CreatedByStaffId]', N'CreatedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[IdentityVerifications].[UpdatedByStaffId]', N'ModifiedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[IdentityVerifications].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[IdentityVerifications].[CreatedByStaffId]', N'CreatedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[CounterTransactions].[UpdatedByStaffId]', N'ModifiedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[CounterTransactions].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[CounterTransactions].[CreatedByStaffId]', N'CreatedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[CounterTerminals].[UpdatedByStaffId]', N'ModifiedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[CounterTerminals].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[CounterTerminals].[CreatedByStaffId]', N'CreatedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[BranchStock].[UpdatedByStaffId]', N'ModifiedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[BranchStock].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[BranchStock].[CreatedByStaffId]', N'CreatedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[Branches].[UpdatedByStaffId]', N'ModifiedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[Branches].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC sp_rename N'[Branches].[CreatedByStaffId]', N'CreatedBy', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var25 nvarchar(max);
    SELECT @var25 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OutboxMessages]') AND [c].[name] = N'AttemptCount');
    IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [OutboxMessages] DROP CONSTRAINT ' + @var25 + ';');
    ALTER TABLE [OutboxMessages] ADD DEFAULT 0 FOR [AttemptCount];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var26 nvarchar(max);
    SELECT @var26 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OperationalNotifications]') AND [c].[name] = N'IsAcknowledged');
    IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [OperationalNotifications] DROP CONSTRAINT ' + @var26 + ';');
    ALTER TABLE [OperationalNotifications] ADD DEFAULT CAST(0 AS bit) FOR [IsAcknowledged];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    DECLARE @var27 nvarchar(max);
    SELECT @var27 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AuditEntries]') AND [c].[name] = N'OccurredAt');
    IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [AuditEntries] DROP CONSTRAINT ' + @var27 + ';');
    ALTER TABLE [AuditEntries] ADD DEFAULT (SYSDATETIMEOFFSET()) FOR [OccurredAt];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    CREATE TABLE [Shipments] (
        [Id] uniqueidentifier NOT NULL,
        [CounterTransactionId] uniqueidentifier NOT NULL,
        [ShippingRateId] uniqueidentifier NOT NULL,
        [ShipmentType] varchar(24) NULL,
        [WeightKilograms] decimal(9,3) NOT NULL,
        [LengthCentimeters] decimal(9,2) NULL,
        [WidthCentimeters] decimal(9,2) NULL,
        [HeightCentimeters] decimal(9,2) NULL,
        [RecipientName] nvarchar(100) NULL,
        [AddressLine] nvarchar(300) NULL,
        [Status] varchar(16) NOT NULL DEFAULT 'Booked',
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] uniqueidentifier NULL,
        [ModifiedAtUtc] datetime2(3) NULL,
        [ModifiedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Shipments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Shipments_Dimensions] CHECK (([LengthCentimeters] IS NULL AND [WidthCentimeters] IS NULL AND [HeightCentimeters] IS NULL) OR ([LengthCentimeters] > 0 AND [WidthCentimeters] > 0 AND [HeightCentimeters] > 0)),
        CONSTRAINT [CK_Shipments_LetterHasNoDimensions] CHECK ([ShipmentType] IS NULL OR [ShipmentType] <> 'Letter' OR [LengthCentimeters] IS NULL),
        CONSTRAINT [CK_Shipments_ShipmentType] CHECK ([ShipmentType] IS NULL OR [ShipmentType] IN ('Parcel', 'Letter')),
        CONSTRAINT [CK_Shipments_Status] CHECK ([Status] IN ('Booked', 'Cancelled')),
        CONSTRAINT [CK_Shipments_Weight] CHECK ([WeightKilograms] > 0 AND [WeightKilograms] <= 30),
        CONSTRAINT [FK_Shipments_CounterTransactions_CounterTransactionId] FOREIGN KEY ([CounterTransactionId]) REFERENCES [CounterTransactions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Shipments_ShippingRates_ShippingRateId] FOREIGN KEY ([ShippingRateId]) REFERENCES [ShippingRates] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC(N'ALTER TABLE [StaffRoles] ADD CONSTRAINT [CK_StaffRoles_Code] CHECK ([Code] IN (''CounterClerk'', ''BranchManager'', ''Auditor'', ''Administrator''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC(N'ALTER TABLE [ShippingRates] ADD CONSTRAINT [CK_ShippingRates_MaximumWeight] CHECK ([MaximumWeightKilograms] > 0 AND [MaximumWeightKilograms] <= 30)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC(N'ALTER TABLE [ShippingRates] ADD CONSTRAINT [CK_ShippingRates_ServiceLevel] CHECK ([ServiceLevel] IN (''Standard'', ''Express'', ''Registered''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC(N'ALTER TABLE [Payments] ADD CONSTRAINT [CK_Payments_Method] CHECK ([Method] IN (''Cash'', ''Card'', ''DigitalWallet''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC(N'ALTER TABLE [MoneyOrders] ADD CONSTRAINT [CK_MoneyOrders_DocumentType] CHECK ([DocumentType] IS NULL OR [DocumentType] IN (''NationalId'', ''Passport'', ''DrivingLicence''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC(N'ALTER TABLE [MoneyOrders] ADD CONSTRAINT [CK_MoneyOrders_MaskedIdentifier] CHECK ([MaskedDocumentIdentifier] IS NULL OR (LEN([MaskedDocumentIdentifier]) <= 16 AND [MaskedDocumentIdentifier] LIKE ''****%'' AND LEN(REPLACE([MaskedDocumentIdentifier], ''*'', '''')) <= 4))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC(N'ALTER TABLE [MoneyOrders] ADD CONSTRAINT [CK_MoneyOrders_Refund] CHECK (([Status] = ''Refunded'' AND [RefundedAt] IS NOT NULL AND [RefundedByStaffId] IS NOT NULL AND [RefundReason] IS NOT NULL) OR ([Status] = ''Issued'' AND [RefundedAt] IS NULL AND [RefundedByStaffId] IS NULL AND [RefundReason] IS NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC(N'ALTER TABLE [MoneyOrders] ADD CONSTRAINT [CK_MoneyOrders_Status] CHECK ([Status] IN (''Issued'', ''Refunded''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC(N'ALTER TABLE [MoneyOrders] ADD CONSTRAINT [CK_MoneyOrders_VerificationStatus] CHECK ([VerificationStatus] IN (''NotRequired'', ''Verified''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC(N'ALTER TABLE [IdentityVerifications] ADD CONSTRAINT [CK_IdentityVerifications_Amount] CHECK ([MoneyOrderAmount] > 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC(N'ALTER TABLE [IdentityVerifications] ADD CONSTRAINT [CK_IdentityVerifications_DocumentType] CHECK ([DocumentType] IN (''NationalId'', ''Passport'', ''DrivingLicence''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC(N'ALTER TABLE [IdentityVerifications] ADD CONSTRAINT [CK_IdentityVerifications_MaskedIdentifier] CHECK (LEN([MaskedDocumentIdentifier]) <= 16 AND [MaskedDocumentIdentifier] LIKE ''****%'' AND LEN(REPLACE([MaskedDocumentIdentifier], ''*'', '''')) <= 4)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC(N'ALTER TABLE [IdentityVerifications] ADD CONSTRAINT [CK_IdentityVerifications_Status] CHECK ([Status] IN (''Verified'', ''Failed''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC(N'ALTER TABLE [CounterTransactions] ADD CONSTRAINT [CK_CounterTransactions_MoneyOrder] CHECK ([MoneyOrderAmount] >= 0 AND [MoneyOrderFee] >= 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    EXEC(N'ALTER TABLE [CounterTransactions] ADD CONSTRAINT [CK_CounterTransactions_Status] CHECK ([Status] IN (''Open'', ''Paid'', ''Completed'', ''Voided'', ''Refunded''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Shipments_CounterTransactionId] ON [Shipments] ([CounterTransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    CREATE INDEX [IX_Shipments_ShippingRateId_Status] ON [Shipments] ([ShippingRateId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN

    INSERT INTO dbo.Shipments
        (Id, CounterTransactionId, ShippingRateId, ShipmentType, WeightKilograms, LengthCentimeters, WidthCentimeters,
         HeightCentimeters, RecipientName, AddressLine, Status, CreatedAtUtc, CreatedBy)
    SELECT NEWID(), m.CounterTransactionId, m.ShippingRateId, m.ShipmentType, m.ShippingWeightKilograms,
           m.ParcelLengthCentimeters, m.ParcelWidthCentimeters, m.ParcelHeightCentimeters,
           m.ShippingRecipientName, m.ShippingAddress,
           CASE WHEN m.Status IN ('Voided', 'Refunded') THEN 'Cancelled' ELSE 'Booked' END,
           m.CreatedAtUtc, m.CreatedByStaffId
    FROM dbo.[__Week3ShipmentsToMove] AS m;
    DROP TABLE dbo.[__Week3ShipmentsToMove];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
        @BranchCode varchar(12),
        @Date date
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        IF @BranchCode IS NULL OR LTRIM(RTRIM(@BranchCode)) = ''
            THROW 50001, N'@BranchCode is required.', 1;
        IF @Date IS NULL
            THROW 50002, N'@Date is required.', 1;
        IF @Date > CAST(SYSUTCDATETIME() AS date)
            THROW 50003, N'@Date cannot be in the future.', 1;

        DECLARE @BranchId uniqueidentifier = (SELECT b.Id FROM dbo.Branches AS b WHERE b.Code = @BranchCode);
        IF @BranchId IS NULL
        BEGIN
            DECLARE @Missing nvarchar(200) = CONCAT(N'Branch code not found: ', @BranchCode);
            THROW 50004, @Missing, 1;
        END;

        -- SARGable UTC day window on the indexed (BranchId, OccurredAt) key - never CAST(OccurredAt AS date).
        DECLARE @From datetimeoffset(3) = TODATETIMEOFFSET(CAST(@Date AS datetime2(3)), 0);
        DECLARE @To datetimeoffset(3) = DATEADD(day, 1, @From);

        WITH Sales AS
        (
            SELECT ct.Id, ct.ShippingCharge, ct.ShippingTaxAmount, ct.MoneyOrderAmount, ct.MoneyOrderFee
            FROM dbo.CounterTransactions AS ct
            WHERE ct.BranchId = @BranchId
              AND ct.OccurredAt >= @From
              AND ct.OccurredAt < @To
              AND ct.Status IN ('Paid', 'Completed')
        ),
        LineTotals AS
        (
            SELECT s.Id,
                   SUM(CASE WHEN p.Category IN (N'Postage', N'Shipping', N'Money Order') THEN 0 ELSE tl.LineTotal + tl.TaxAmount END) AS Retail,
                   SUM(CASE WHEN p.Category IN (N'Postage', N'Shipping', N'Money Order') THEN tl.LineTotal + tl.TaxAmount ELSE 0 END) AS ServiceLines
            FROM Sales AS s
            INNER JOIN dbo.TransactionLines AS tl ON tl.CounterTransactionId = s.Id
            INNER JOIN dbo.Products AS p ON p.Id = tl.ProductId
            GROUP BY s.Id
        )
        SELECT
            CAST(@BranchCode AS varchar(12)) AS BranchCode,
            @Date AS SaleDate,
            COUNT(s.Id) AS TransactionCount,
            CAST(ISNULL(SUM(lt.Retail), 0) AS decimal(18, 2)) AS RetailRevenue,
            CAST(ISNULL(SUM(ISNULL(lt.ServiceLines, 0) + s.ShippingCharge + s.ShippingTaxAmount + s.MoneyOrderAmount + s.MoneyOrderFee), 0) AS decimal(18, 2)) AS ServiceRevenue
        FROM Sales AS s
        LEFT JOIN LineTotals AS lt ON lt.Id = s.Id;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport
        @BranchCode varchar(12) = NULL,
        @Threshold int
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        IF @Threshold IS NULL OR @Threshold < 0 OR @Threshold > 1000
            THROW 50005, N'@Threshold must be between 0 and 1000.', 1;

        IF @BranchCode IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE Code = @BranchCode)
        BEGIN
            DECLARE @Missing nvarchar(200) = CONCAT(N'Branch code not found: ', @BranchCode);
            THROW 50004, @Missing, 1;
        END;

        -- Optional parameter: OPTION (RECOMPILE) compiles a plan for the actual values, so one branch
        -- seeks IX_BranchStock_BranchId_QuantityOnHand and "all branches" scans once. See ADR 0043.
        SELECT
            b.Code AS BranchCode,
            p.Sku,
            p.Name AS ProductName,
            bs.QuantityOnHand,
            bs.ReorderLevel,
            CASE WHEN bs.ReorderLevel > bs.QuantityOnHand THEN bs.ReorderLevel - bs.QuantityOnHand ELSE 0 END AS Shortfall
        FROM dbo.BranchStock AS bs
        INNER JOIN dbo.Branches AS b ON b.Id = bs.BranchId
        INNER JOIN dbo.Products AS p ON p.Id = bs.ProductId
        WHERE bs.QuantityOnHand <= @Threshold
          AND p.IsActive = 1
          AND (@BranchCode IS NULL OR b.Code = @BranchCode)
        ORDER BY b.Code, bs.QuantityOnHand, p.Sku
        OPTION (RECOMPILE);
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
        @BranchCode varchar(12),
        @Sku varchar(32),
        @Delta int,
        @Reason nvarchar(250),
        @ChangedBy varchar(254)
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        IF @BranchCode IS NULL OR LTRIM(RTRIM(@BranchCode)) = ''
            THROW 50001, N'@BranchCode is required.', 1;
        IF @Sku IS NULL OR LTRIM(RTRIM(@Sku)) = ''
            THROW 50006, N'@Sku is required.', 1;
        IF @Delta IS NULL OR @Delta = 0
            THROW 50007, N'@Delta must be a non-zero number of units.', 1;
        IF @Reason IS NULL OR LEN(LTRIM(RTRIM(@Reason))) < 3
            THROW 50008, N'@Reason must contain at least three characters.', 1;
        IF @ChangedBy IS NULL OR LTRIM(RTRIM(@ChangedBy)) = ''
            THROW 50009, N'@ChangedBy is required.', 1;

        DECLARE @BranchId uniqueidentifier = (SELECT Id FROM dbo.Branches WHERE Code = @BranchCode);
        DECLARE @ProductId uniqueidentifier = (SELECT Id FROM dbo.Products WHERE Sku = UPPER(LTRIM(RTRIM(@Sku))) AND IsActive = 1);
        DECLARE @StaffId uniqueidentifier = (SELECT Id FROM dbo.Staff WHERE Email = LOWER(LTRIM(RTRIM(@ChangedBy))) AND IsActive = 1);
        DECLARE @Message nvarchar(300);

        IF @BranchId IS NULL
        BEGIN
            SET @Message = CONCAT(N'Branch code not found: ', @BranchCode);
            THROW 50004, @Message, 1;
        END;
        IF @ProductId IS NULL
        BEGIN
            SET @Message = CONCAT(N'Active product SKU not found: ', @Sku);
            THROW 50010, @Message, 1;
        END;
        IF @StaffId IS NULL
        BEGIN
            SET @Message = CONCAT(N'@ChangedBy must be the e-mail of an active staff member: ', @ChangedBy);
            THROW 50011, @Message, 1;
        END;

        DECLARE @AdjustmentId uniqueidentifier = NEWID();
        DECLARE @Now datetime2(3) = SYSUTCDATETIME();
        DECLARE @NewQuantity TABLE (QuantityOnHand int NOT NULL);
        -- Join an outer transaction (EF Core explicit transaction) instead of committing it.
        DECLARE @OwnsTransaction bit = CASE WHEN @@TRANCOUNT = 0 THEN 1 ELSE 0 END;

        BEGIN TRY
            IF @OwnsTransaction = 1
                BEGIN TRANSACTION;

            -- Same table order as the counter sale (BranchStock, then history): avoids lock-order deadlocks.
            -- The guarded UPDATE is atomic: the row is updated only if it stays >= 0, no read-then-write race.
            UPDATE dbo.BranchStock
            SET QuantityOnHand = QuantityOnHand + @Delta,
                ModifiedAtUtc = @Now,
                ModifiedBy = @StaffId
            OUTPUT inserted.QuantityOnHand INTO @NewQuantity
            WHERE BranchId = @BranchId
              AND ProductId = @ProductId
              AND QuantityOnHand + @Delta >= 0;

            IF NOT EXISTS (SELECT 1 FROM @NewQuantity)
            BEGIN
                IF EXISTS (SELECT 1 FROM dbo.BranchStock WHERE BranchId = @BranchId AND ProductId = @ProductId)
                BEGIN
                    SET @Message = CONCAT(N'Adjustment would make stock negative for ', @Sku, N' at ', @BranchCode, N'.');
                    THROW 50012, @Message, 1;
                END;
                IF @Delta < 0
                BEGIN
                    SET @Message = CONCAT(N'No stock is recorded for ', @Sku, N' at ', @BranchCode, N'.');
                    THROW 50013, @Message, 1;
                END;

                INSERT dbo.BranchStock (BranchId, ProductId, QuantityOnHand, ReorderLevel, CreatedAtUtc, CreatedBy)
                OUTPUT inserted.QuantityOnHand INTO @NewQuantity
                VALUES (@BranchId, @ProductId, @Delta, 0, @Now, @StaffId);
            END;

            INSERT dbo.StockAdjustments (Id, BranchId, ProductId, StaffId, QuantityChange, Reason, OccurredAt, CreatedAtUtc, CreatedBy)
            VALUES (@AdjustmentId, @BranchId, @ProductId, @StaffId, @Delta, LTRIM(RTRIM(@Reason)), TODATETIMEOFFSET(@Now, 0), @Now, @StaffId);

            IF @OwnsTransaction = 1
                COMMIT TRANSACTION;

            SELECT @AdjustmentId AS StockAdjustmentId, QuantityOnHand FROM @NewQuantity;
        END TRY
        BEGIN CATCH
            IF @OwnsTransaction = 1 AND XACT_STATE() <> 0
                ROLLBACK TRANSACTION;
            THROW;
        END CATCH;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102223854_Week3SchemaCompliance'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102223854_Week3SchemaCompliance', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102230416_Week3MeasuredIndexes'
)
BEGIN
    DROP INDEX [IX_Shipments_ShippingRateId_Status] ON [Shipments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102230416_Week3MeasuredIndexes'
)
BEGIN
    CREATE INDEX [IX_Shipments_ShippingRateId_Status] ON [Shipments] ([ShippingRateId], [Status]) INCLUDE ([CounterTransactionId], [WeightKilograms]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102230416_Week3MeasuredIndexes'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_MoneyOrders_Verified_OccurredAt] ON [MoneyOrders] ([OccurredAt]) INCLUDE ([MoneyOrderNumber], [Amount], [MaskedDocumentIdentifier], [BranchId], [Status]) WHERE [VerificationStatus] = ''Verified''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102230416_Week3MeasuredIndexes'
)
BEGIN
    CREATE INDEX [IX_BranchStock_QuantityOnHand] ON [BranchStock] ([QuantityOnHand]) INCLUDE ([ReorderLevel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102230416_Week3MeasuredIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102230416_Week3MeasuredIndexes', N'10.0.14');
END;

COMMIT;
GO


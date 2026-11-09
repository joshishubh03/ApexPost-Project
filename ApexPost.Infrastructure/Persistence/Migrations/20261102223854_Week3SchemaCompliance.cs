using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Week3SchemaCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // REVIEWED BY HAND (see docs/adr/0041): shipping columns move from CounterTransactions to the new
            // Shipments table. Copy them to a staging table BEFORE the columns are dropped. A permanent staging
            // table (not #temp) is used because every raw statement runs through EXEC (see StoredProcedureSql.Exec):
            // that keeps the idempotent script valid when it is re-run after these columns were dropped.
            migrationBuilder.Sql(StoredProcedureSql.Exec(@"
SELECT ct.Id AS CounterTransactionId, ct.ShippingRateId, ct.ShipmentType, ct.ShippingWeightKilograms,
       ct.ParcelLengthCentimeters, ct.ParcelWidthCentimeters, ct.ParcelHeightCentimeters,
       ct.ShippingRecipientName, ct.ShippingAddress, ct.Status, ct.CreatedAtUtc, ct.CreatedByStaffId
INTO dbo.[__Week3ShipmentsToMove]
FROM dbo.CounterTransactions AS ct
WHERE ct.ShippingRateId IS NOT NULL;"));

            migrationBuilder.DropForeignKey(
                name: "FK_CounterTransactions_ShippingRates_ShippingRateId",
                table: "CounterTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ShippingRates_MaximumWeight",
                table: "ShippingRates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MoneyOrders_MaskedIdentifier",
                table: "MoneyOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IdentityVerifications_MaskedIdentifier",
                table: "IdentityVerifications");

            migrationBuilder.DropIndex(
                name: "IX_CounterTransactions_ShippingRateId",
                table: "CounterTransactions");

            migrationBuilder.DropColumn(
                name: "AuditCorrelationId",
                table: "TaxCategories");

            migrationBuilder.DropColumn(
                name: "AuditCorrelationId",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "AuditCorrelationId",
                table: "StaffRoles");

            migrationBuilder.DropColumn(
                name: "AuditCorrelationId",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "AuditCorrelationId",
                table: "ShippingZones");

            migrationBuilder.DropColumn(
                name: "AuditCorrelationId",
                table: "ShippingRates");

            migrationBuilder.DropColumn(
                name: "AuditCorrelationId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AuditCorrelationId",
                table: "ProductCategories");

            migrationBuilder.DropColumn(
                name: "AuditCorrelationId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "AuditCorrelationId",
                table: "MoneyOrders");

            migrationBuilder.DropColumn(
                name: "AuditCorrelationId",
                table: "IdentityVerifications");

            migrationBuilder.DropColumn(
                name: "AuditCorrelationId",
                table: "CounterTransactions");

            // Hand fix: EF paired ShippingRateId with CreatedBy (both Guid?). ShippingRateId moves to Shipments.
            migrationBuilder.DropColumn(
                name: "ShippingRateId",
                table: "CounterTransactions");

            migrationBuilder.DropColumn(
                name: "ParcelHeightCentimeters",
                table: "CounterTransactions");

            migrationBuilder.DropColumn(
                name: "ParcelLengthCentimeters",
                table: "CounterTransactions");

            migrationBuilder.DropColumn(
                name: "ParcelWidthCentimeters",
                table: "CounterTransactions");

            migrationBuilder.DropColumn(
                name: "ShipmentType",
                table: "CounterTransactions");

            migrationBuilder.DropColumn(
                name: "ShippingAddress",
                table: "CounterTransactions");

            migrationBuilder.DropColumn(
                name: "ShippingRecipientName",
                table: "CounterTransactions");

            migrationBuilder.DropColumn(
                name: "ShippingServiceLevel",
                table: "CounterTransactions");

            migrationBuilder.DropColumn(
                name: "ShippingWeightKilograms",
                table: "CounterTransactions");

            migrationBuilder.DropColumn(
                name: "ShippingZoneName",
                table: "CounterTransactions");

            migrationBuilder.DropColumn(
                name: "AuditCorrelationId",
                table: "CounterTerminals");

            migrationBuilder.DropColumn(
                name: "AuditCorrelationId",
                table: "BranchStock");

            migrationBuilder.DropColumn(
                name: "AuditCorrelationId",
                table: "Branches");

            migrationBuilder.RenameColumn(
                name: "UpdatedByStaffId",
                table: "TaxCategories",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "TaxCategories",
                newName: "ModifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedByStaffId",
                table: "TaxCategories",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedByStaffId",
                table: "StockAdjustments",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "StockAdjustments",
                newName: "ModifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedByStaffId",
                table: "StockAdjustments",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedByStaffId",
                table: "StaffRoles",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "StaffRoles",
                newName: "ModifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedByStaffId",
                table: "StaffRoles",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedByStaffId",
                table: "Staff",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "Staff",
                newName: "ModifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedByStaffId",
                table: "Staff",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedByStaffId",
                table: "ShippingZones",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "ShippingZones",
                newName: "ModifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedByStaffId",
                table: "ShippingZones",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedByStaffId",
                table: "ShippingRates",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "ShippingRates",
                newName: "ModifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedByStaffId",
                table: "ShippingRates",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedByStaffId",
                table: "Products",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "Products",
                newName: "ModifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedByStaffId",
                table: "Products",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedByStaffId",
                table: "ProductCategories",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "ProductCategories",
                newName: "ModifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedByStaffId",
                table: "ProductCategories",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedByStaffId",
                table: "Payments",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "Payments",
                newName: "ModifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedByStaffId",
                table: "Payments",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedByStaffId",
                table: "MoneyOrders",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "MoneyOrders",
                newName: "ModifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedByStaffId",
                table: "MoneyOrders",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedByStaffId",
                table: "IdentityVerifications",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "IdentityVerifications",
                newName: "ModifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedByStaffId",
                table: "IdentityVerifications",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedByStaffId",
                table: "CounterTransactions",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "CounterTransactions",
                newName: "ModifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedByStaffId",
                table: "CounterTransactions",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedByStaffId",
                table: "CounterTerminals",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "CounterTerminals",
                newName: "ModifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedByStaffId",
                table: "CounterTerminals",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedByStaffId",
                table: "BranchStock",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "BranchStock",
                newName: "ModifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedByStaffId",
                table: "BranchStock",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedByStaffId",
                table: "Branches",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "Branches",
                newName: "ModifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedByStaffId",
                table: "Branches",
                newName: "CreatedBy");

            migrationBuilder.AlterColumn<int>(
                name: "AttemptCount",
                table: "OutboxMessages",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<bool>(
                name: "IsAcknowledged",
                table: "OperationalNotifications",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "OccurredAt",
                table: "AuditEntries",
                type: "datetimeoffset(3)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(3)");

            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CounterTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShippingRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShipmentType = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: true),
                    WeightKilograms = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: false),
                    LengthCentimeters = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    WidthCentimeters = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    HeightCentimeters = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    RecipientName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AddressLine = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false, defaultValue: "Booked"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                    table.CheckConstraint("CK_Shipments_Dimensions", "([LengthCentimeters] IS NULL AND [WidthCentimeters] IS NULL AND [HeightCentimeters] IS NULL) OR ([LengthCentimeters] > 0 AND [WidthCentimeters] > 0 AND [HeightCentimeters] > 0)");
                    table.CheckConstraint("CK_Shipments_LetterHasNoDimensions", "[ShipmentType] IS NULL OR [ShipmentType] <> 'Letter' OR [LengthCentimeters] IS NULL");
                    table.CheckConstraint("CK_Shipments_ShipmentType", "[ShipmentType] IS NULL OR [ShipmentType] IN ('Parcel', 'Letter')");
                    table.CheckConstraint("CK_Shipments_Status", "[Status] IN ('Booked', 'Cancelled')");
                    table.CheckConstraint("CK_Shipments_Weight", "[WeightKilograms] > 0 AND [WeightKilograms] <= 30");
                    table.ForeignKey(
                        name: "FK_Shipments_CounterTransactions_CounterTransactionId",
                        column: x => x.CounterTransactionId,
                        principalTable: "CounterTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Shipments_ShippingRates_ShippingRateId",
                        column: x => x.ShippingRateId,
                        principalTable: "ShippingRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_StaffRoles_Code",
                table: "StaffRoles",
                sql: "[Code] IN ('CounterClerk', 'BranchManager', 'Auditor', 'Administrator')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShippingRates_MaximumWeight",
                table: "ShippingRates",
                sql: "[MaximumWeightKilograms] > 0 AND [MaximumWeightKilograms] <= 30");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShippingRates_ServiceLevel",
                table: "ShippingRates",
                sql: "[ServiceLevel] IN ('Standard', 'Express', 'Registered')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Method",
                table: "Payments",
                sql: "[Method] IN ('Cash', 'Card', 'DigitalWallet')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MoneyOrders_DocumentType",
                table: "MoneyOrders",
                sql: "[DocumentType] IS NULL OR [DocumentType] IN ('NationalId', 'Passport', 'DrivingLicence')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MoneyOrders_MaskedIdentifier",
                table: "MoneyOrders",
                sql: "[MaskedDocumentIdentifier] IS NULL OR (LEN([MaskedDocumentIdentifier]) <= 16 AND [MaskedDocumentIdentifier] LIKE '****%' AND LEN(REPLACE([MaskedDocumentIdentifier], '*', '')) <= 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MoneyOrders_Refund",
                table: "MoneyOrders",
                sql: "([Status] = 'Refunded' AND [RefundedAt] IS NOT NULL AND [RefundedByStaffId] IS NOT NULL AND [RefundReason] IS NOT NULL) OR ([Status] = 'Issued' AND [RefundedAt] IS NULL AND [RefundedByStaffId] IS NULL AND [RefundReason] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MoneyOrders_Status",
                table: "MoneyOrders",
                sql: "[Status] IN ('Issued', 'Refunded')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MoneyOrders_VerificationStatus",
                table: "MoneyOrders",
                sql: "[VerificationStatus] IN ('NotRequired', 'Verified')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_IdentityVerifications_Amount",
                table: "IdentityVerifications",
                sql: "[MoneyOrderAmount] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_IdentityVerifications_DocumentType",
                table: "IdentityVerifications",
                sql: "[DocumentType] IN ('NationalId', 'Passport', 'DrivingLicence')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_IdentityVerifications_MaskedIdentifier",
                table: "IdentityVerifications",
                sql: "LEN([MaskedDocumentIdentifier]) <= 16 AND [MaskedDocumentIdentifier] LIKE '****%' AND LEN(REPLACE([MaskedDocumentIdentifier], '*', '')) <= 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_IdentityVerifications_Status",
                table: "IdentityVerifications",
                sql: "[Status] IN ('Verified', 'Failed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CounterTransactions_MoneyOrder",
                table: "CounterTransactions",
                sql: "[MoneyOrderAmount] >= 0 AND [MoneyOrderFee] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CounterTransactions_Status",
                table: "CounterTransactions",
                sql: "[Status] IN ('Open', 'Paid', 'Completed', 'Voided', 'Refunded')");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_CounterTransactionId",
                table: "Shipments",
                column: "CounterTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_ShippingRateId_Status",
                table: "Shipments",
                columns: new[] { "ShippingRateId", "Status" });

            migrationBuilder.Sql(StoredProcedureSql.Exec(@"
INSERT INTO dbo.Shipments
    (Id, CounterTransactionId, ShippingRateId, ShipmentType, WeightKilograms, LengthCentimeters, WidthCentimeters,
     HeightCentimeters, RecipientName, AddressLine, Status, CreatedAtUtc, CreatedBy)
SELECT NEWID(), m.CounterTransactionId, m.ShippingRateId, m.ShipmentType, m.ShippingWeightKilograms,
       m.ParcelLengthCentimeters, m.ParcelWidthCentimeters, m.ParcelHeightCentimeters,
       m.ShippingRecipientName, m.ShippingAddress,
       CASE WHEN m.Status IN ('Voided', 'Refunded') THEN 'Cancelled' ELSE 'Booked' END,
       m.CreatedAtUtc, m.CreatedByStaffId
FROM dbo.[__Week3ShipmentsToMove] AS m;
DROP TABLE dbo.[__Week3ShipmentsToMove];"));

            // Reporting / batch stored procedures (also in sql/stored-procedures.sql). CREATE PROCEDURE must be the
            // first statement of a batch, so each one runs through EXEC (also inside the idempotent script's IF blocks).
            migrationBuilder.Sql(StoredProcedureSql.Exec(StoredProcedureSql.DailyBranchSales));
            migrationBuilder.Sql(StoredProcedureSql.Exec(StoredProcedureSql.LowStockReport));
            migrationBuilder.Sql(StoredProcedureSql.Exec(StoredProcedureSql.AdjustStock));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(StoredProcedureSql.Exec(StoredProcedureSql.DropAll));

            migrationBuilder.DropTable(
                name: "Shipments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StaffRoles_Code",
                table: "StaffRoles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ShippingRates_MaximumWeight",
                table: "ShippingRates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ShippingRates_ServiceLevel",
                table: "ShippingRates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Method",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MoneyOrders_DocumentType",
                table: "MoneyOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MoneyOrders_MaskedIdentifier",
                table: "MoneyOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MoneyOrders_Refund",
                table: "MoneyOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MoneyOrders_Status",
                table: "MoneyOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MoneyOrders_VerificationStatus",
                table: "MoneyOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IdentityVerifications_Amount",
                table: "IdentityVerifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IdentityVerifications_DocumentType",
                table: "IdentityVerifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IdentityVerifications_MaskedIdentifier",
                table: "IdentityVerifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IdentityVerifications_Status",
                table: "IdentityVerifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CounterTransactions_MoneyOrder",
                table: "CounterTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CounterTransactions_Status",
                table: "CounterTransactions");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "TaxCategories",
                newName: "UpdatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "TaxCategories",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "TaxCategories",
                newName: "CreatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "StockAdjustments",
                newName: "UpdatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "StockAdjustments",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "StockAdjustments",
                newName: "CreatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "StaffRoles",
                newName: "UpdatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "StaffRoles",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "StaffRoles",
                newName: "CreatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "Staff",
                newName: "UpdatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "Staff",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "Staff",
                newName: "CreatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "ShippingZones",
                newName: "UpdatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "ShippingZones",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "ShippingZones",
                newName: "CreatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "ShippingRates",
                newName: "UpdatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "ShippingRates",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "ShippingRates",
                newName: "CreatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "Products",
                newName: "UpdatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "Products",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "Products",
                newName: "CreatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "ProductCategories",
                newName: "UpdatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "ProductCategories",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "ProductCategories",
                newName: "CreatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "Payments",
                newName: "UpdatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "Payments",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "Payments",
                newName: "CreatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "MoneyOrders",
                newName: "UpdatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "MoneyOrders",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "MoneyOrders",
                newName: "CreatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "IdentityVerifications",
                newName: "UpdatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "IdentityVerifications",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "IdentityVerifications",
                newName: "CreatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "CounterTransactions",
                newName: "UpdatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "CounterTransactions",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "CounterTransactions",
                newName: "CreatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "CounterTerminals",
                newName: "UpdatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "CounterTerminals",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "CounterTerminals",
                newName: "CreatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "BranchStock",
                newName: "UpdatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "BranchStock",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "BranchStock",
                newName: "CreatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "Branches",
                newName: "UpdatedByStaffId");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "Branches",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "Branches",
                newName: "CreatedByStaffId");

            migrationBuilder.AddColumn<string>(
                name: "AuditCorrelationId",
                table: "TaxCategories",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditCorrelationId",
                table: "StockAdjustments",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditCorrelationId",
                table: "StaffRoles",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditCorrelationId",
                table: "Staff",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditCorrelationId",
                table: "ShippingZones",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditCorrelationId",
                table: "ShippingRates",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditCorrelationId",
                table: "Products",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditCorrelationId",
                table: "ProductCategories",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditCorrelationId",
                table: "Payments",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AttemptCount",
                table: "OutboxMessages",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<bool>(
                name: "IsAcknowledged",
                table: "OperationalNotifications",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AuditCorrelationId",
                table: "MoneyOrders",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditCorrelationId",
                table: "IdentityVerifications",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditCorrelationId",
                table: "CounterTransactions",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShippingRateId",
                table: "CounterTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ParcelHeightCentimeters",
                table: "CounterTransactions",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ParcelLengthCentimeters",
                table: "CounterTransactions",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ParcelWidthCentimeters",
                table: "CounterTransactions",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipmentType",
                table: "CounterTransactions",
                type: "varchar(24)",
                unicode: false,
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingAddress",
                table: "CounterTransactions",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingRecipientName",
                table: "CounterTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingServiceLevel",
                table: "CounterTransactions",
                type: "varchar(24)",
                unicode: false,
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingWeightKilograms",
                table: "CounterTransactions",
                type: "decimal(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingZoneName",
                table: "CounterTransactions",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditCorrelationId",
                table: "CounterTerminals",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditCorrelationId",
                table: "BranchStock",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditCorrelationId",
                table: "Branches",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "OccurredAt",
                table: "AuditEntries",
                type: "datetimeoffset(3)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(3)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET()");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShippingRates_MaximumWeight",
                table: "ShippingRates",
                sql: "[MaximumWeightKilograms] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MoneyOrders_MaskedIdentifier",
                table: "MoneyOrders",
                sql: "[MaskedDocumentIdentifier] IS NULL OR LEN([MaskedDocumentIdentifier]) <= 16");

            migrationBuilder.AddCheckConstraint(
                name: "CK_IdentityVerifications_MaskedIdentifier",
                table: "IdentityVerifications",
                sql: "LEN([MaskedDocumentIdentifier]) <= 16");

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_ShippingRateId",
                table: "CounterTransactions",
                column: "ShippingRateId");

            migrationBuilder.AddForeignKey(
                name: "FK_CounterTransactions_ShippingRates_ShippingRateId",
                table: "CounterTransactions",
                column: "ShippingRateId",
                principalTable: "ShippingRates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

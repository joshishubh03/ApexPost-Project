using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Week 4 (additive): CourierShipments and ShipmentScans for the shipments API. No Week 3 table is changed.
    /// ShipmentScans.Id is the courier device's scan GUID, so the primary key itself makes re-uploads idempotent.
    /// </summary>
    [DbContext(typeof(ApexPostDbContext))]
    [Migration("20261109090000_Week4CourierShipmentsApi")]
    public partial class Week4CourierShipmentsApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CourierShipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackingNumber = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    Status = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    WeightKilograms = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RecipientPhone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    RecipientAddress = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    RecipientCity = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourierShipments", x => x.Id);
                    table.CheckConstraint("CK_CourierShipments_Status", "[Status] IN ('Created', 'PickedUp', 'InTransit', 'OutForDelivery', 'Delivered', 'Failed')");
                    table.CheckConstraint("CK_CourierShipments_TrackingNumber", "[TrackingNumber] LIKE 'APX-[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'");
                    table.CheckConstraint("CK_CourierShipments_Weight", "[WeightKilograms] >= 0.1 AND [WeightKilograms] <= 70");
                });

            migrationBuilder.CreateTable(
                name: "ShipmentScans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourierShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScanType = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    CourierId = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ScannedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SignatureName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentScans", x => x.Id);
                    table.CheckConstraint("CK_ShipmentScans_DeliveredSignature", "[ScanType] <> 'Delivered' OR [SignatureName] IS NOT NULL");
                    table.CheckConstraint("CK_ShipmentScans_ScanType", "[ScanType] IN ('Pickup', 'InTransit', 'Delivered', 'Failed')");
                    table.ForeignKey(
                        name: "FK_ShipmentScans_CourierShipments_CourierShipmentId",
                        column: x => x.CourierShipmentId,
                        principalTable: "CourierShipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourierShipments_Status_RecipientCity",
                table: "CourierShipments",
                columns: new[] { "Status", "RecipientCity" });

            migrationBuilder.CreateIndex(
                name: "IX_CourierShipments_TrackingNumber",
                table: "CourierShipments",
                column: "TrackingNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentScans_CourierShipmentId_ScannedAtUtc",
                table: "ShipmentScans",
                columns: new[] { "CourierShipmentId", "ScannedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipmentScans");

            migrationBuilder.DropTable(
                name: "CourierShipments");
        }
    }
}

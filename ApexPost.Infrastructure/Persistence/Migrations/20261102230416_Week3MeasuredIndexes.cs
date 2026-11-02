using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Week3MeasuredIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shipments_ShippingRateId_Status",
                table: "Shipments");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_ShippingRateId_Status",
                table: "Shipments",
                columns: new[] { "ShippingRateId", "Status" })
                .Annotation("SqlServer:Include", new[] { "CounterTransactionId", "WeightKilograms" });

            migrationBuilder.CreateIndex(
                name: "IX_MoneyOrders_Verified_OccurredAt",
                table: "MoneyOrders",
                column: "OccurredAt",
                filter: "[VerificationStatus] = 'Verified'")
                .Annotation("SqlServer:Include", new[] { "MoneyOrderNumber", "Amount", "MaskedDocumentIdentifier", "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchStock_QuantityOnHand",
                table: "BranchStock",
                column: "QuantityOnHand")
                .Annotation("SqlServer:Include", new[] { "ReorderLevel" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shipments_ShippingRateId_Status",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_MoneyOrders_Verified_OccurredAt",
                table: "MoneyOrders");

            migrationBuilder.DropIndex(
                name: "IX_BranchStock_QuantityOnHand",
                table: "BranchStock");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_ShippingRateId_Status",
                table: "Shipments",
                columns: new[] { "ShippingRateId", "Status" });
        }
    }
}

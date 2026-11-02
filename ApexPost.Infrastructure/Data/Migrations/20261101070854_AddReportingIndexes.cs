
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Data.Migrations;

public partial class AddReportingIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_CounterTransactions_BranchDateStatus",
            table: "CounterTransactions",
            columns: new[] { "BranchId", "OccurredAtUtc", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_CounterTransactions_ShippingSearch",
            table: "CounterTransactions",
            columns: new[] { "ShippingZoneName", "ShippingServiceLevel", "ShippingRateId" });

        migrationBuilder.CreateIndex(
            name: "IX_Inventory_LowStock",
            table: "Inventory",
            columns: new[] { "QuantityOnHand", "BranchId", "ProductId" });

        migrationBuilder.CreateIndex(
            name: "IX_MoneyOrders_StatusIssuedAt",
            table: "MoneyOrders",
            columns: new[] { "Status", "OccurredAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CounterTransactions_BranchDateStatus",
            table: "CounterTransactions");

        migrationBuilder.DropIndex(
            name: "IX_CounterTransactions_ShippingSearch",
            table: "CounterTransactions");

        migrationBuilder.DropIndex(
            name: "IX_Inventory_LowStock",
            table: "Inventory");

        migrationBuilder.DropIndex(
            name: "IX_MoneyOrders_StatusIssuedAt",
            table: "MoneyOrders");
    }
}

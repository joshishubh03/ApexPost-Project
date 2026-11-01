using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "MoneyOrders",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "CounterTransactions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ShippingZoneName",
                table: "CounterTransactions",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ShippingServiceLevel",
                table: "CounterTransactions",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MoneyOrders_StatusIssuedAt",
                table: "MoneyOrders",
                columns: new[] { "Status", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_LowStock",
                table: "Inventory",
                columns: new[] { "QuantityOnHand", "BranchId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_BranchDateStatus",
                table: "CounterTransactions",
                columns: new[] { "BranchId", "OccurredAtUtc", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_ShippingSearch",
                table: "CounterTransactions",
                columns: new[] { "ShippingZoneName", "ShippingServiceLevel", "ShippingRateId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MoneyOrders_StatusIssuedAt",
                table: "MoneyOrders");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_LowStock",
                table: "Inventory");

            migrationBuilder.DropIndex(
                name: "IX_CounterTransactions_BranchDateStatus",
                table: "CounterTransactions");

            migrationBuilder.DropIndex(
                name: "IX_CounterTransactions_ShippingSearch",
                table: "CounterTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "MoneyOrders",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "CounterTransactions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "ShippingZoneName",
                table: "CounterTransactions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ShippingServiceLevel",
                table: "CounterTransactions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}

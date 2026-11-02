using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MoneyOrders_BranchId_OccurredAtUtc",
                table: "MoneyOrders",
                columns: new[] { "BranchId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityVerifications_BranchId_OccurredAtUtc",
                table: "IdentityVerifications",
                columns: new[] { "BranchId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_BranchId_Status_OccurredAtUtc",
                table: "CounterTransactions",
                columns: new[] { "BranchId", "Status", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchStock_BranchId_QuantityOnHand",
                table: "BranchStock",
                columns: new[] { "BranchId", "QuantityOnHand" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MoneyOrders_BranchId_OccurredAtUtc",
                table: "MoneyOrders");

            migrationBuilder.DropIndex(
                name: "IX_IdentityVerifications_BranchId_OccurredAtUtc",
                table: "IdentityVerifications");

            migrationBuilder.DropIndex(
                name: "IX_CounterTransactions_BranchId_Status_OccurredAtUtc",
                table: "CounterTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BranchStock_BranchId_QuantityOnHand",
                table: "BranchStock");
        }
    }
}

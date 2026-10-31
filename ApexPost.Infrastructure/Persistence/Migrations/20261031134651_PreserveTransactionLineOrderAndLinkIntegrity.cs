using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreserveTransactionLineOrderAndLinkIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransactionLines_CounterTransactionId",
                table: "TransactionLines");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CounterTransactionId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_CounterTransactions_MoneyOrderId",
                table: "CounterTransactions");

            migrationBuilder.AddColumn<int>(
                name: "LineNumber",
                table: "TransactionLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLines_CounterTransactionId_LineNumber",
                table: "TransactionLines",
                columns: new[] { "CounterTransactionId", "LineNumber" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransactionLines_LineNumber",
                table: "TransactionLines",
                sql: "[LineNumber] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CounterTransactionId",
                table: "Payments",
                column: "CounterTransactionId",
                unique: true,
                filter: "[CounterTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_MoneyOrderId",
                table: "CounterTransactions",
                column: "MoneyOrderId",
                unique: true,
                filter: "[MoneyOrderId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransactionLines_CounterTransactionId_LineNumber",
                table: "TransactionLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TransactionLines_LineNumber",
                table: "TransactionLines");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CounterTransactionId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_CounterTransactions_MoneyOrderId",
                table: "CounterTransactions");

            migrationBuilder.DropColumn(
                name: "LineNumber",
                table: "TransactionLines");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLines_CounterTransactionId",
                table: "TransactionLines",
                column: "CounterTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CounterTransactionId",
                table: "Payments",
                column: "CounterTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_MoneyOrderId",
                table: "CounterTransactions",
                column: "MoneyOrderId");
        }
    }
}

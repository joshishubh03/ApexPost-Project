using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryConcurrencyAndOperationalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "OperationalNotifications",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReferenceId",
                table: "OperationalNotifications",
                type: "varchar(80)",
                unicode: false,
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StaffId",
                table: "OperationalNotifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MoneyOrderId",
                table: "CounterTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "AuditEntries",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DurationMilliseconds",
                table: "AuditEntries",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RouteValuesJson",
                table: "AuditEntries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationalNotifications_StaffId",
                table: "OperationalNotifications",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_MoneyOrderId",
                table: "CounterTransactions",
                column: "MoneyOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_OperationalNotifications_Staff_StaffId",
                table: "OperationalNotifications",
                column: "StaffId",
                principalTable: "Staff",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OperationalNotifications_Staff_StaffId",
                table: "OperationalNotifications");

            migrationBuilder.DropIndex(
                name: "IX_OperationalNotifications_StaffId",
                table: "OperationalNotifications");

            migrationBuilder.DropIndex(
                name: "IX_CounterTransactions_MoneyOrderId",
                table: "CounterTransactions");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "OperationalNotifications");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "OperationalNotifications");

            migrationBuilder.DropColumn(
                name: "StaffId",
                table: "OperationalNotifications");

            migrationBuilder.DropColumn(
                name: "MoneyOrderId",
                table: "CounterTransactions");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "DurationMilliseconds",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "RouteValuesJson",
                table: "AuditEntries");
        }
    }
}

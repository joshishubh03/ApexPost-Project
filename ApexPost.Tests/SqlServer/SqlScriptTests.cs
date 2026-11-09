using System.Data;
using ApexPost.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Tests.SqlServer;

/// <summary>
/// The SQL deliverables (Parts A, B and D) are executed against the migrated test database, so a script that
/// no longer matches the schema fails the build instead of failing in front of the assessor.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class SqlScriptTests(SqlServerTestDatabase database)
{
    [Fact]
    public async Task Reporting_queries_script_runs_against_the_migrated_schema()
    {
        var results = await SqlScripts.RunAsync(database.ConnectionString, SqlScripts.Read("reporting-queries.sql"));

        Assert.True(results.Count >= 6, $"Expected at least 6 result sets, got {results.Count}.");
        Assert.True(results[0].Rows.Count > 0, "Daily branch sales should return the seeded history.");
    }

    [Fact]
    public async Task Top_10_products_per_region_never_returns_more_than_10_rows_per_region()
    {
        var results = await SqlScripts.RunAsync(database.ConnectionString, SqlScripts.Read("reporting-queries.sql"));
        var topTen = results[2];

        Assert.True(topTen.Rows.Count > 0);
        foreach (var region in topTen.AsEnumerable().GroupBy(row => row.Field<string>("Region")))
        {
            var ranks = region.Select(row => Convert.ToInt64(row["RevenueRank"], System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Assert.True(ranks.Length <= 10, $"{region.Key} returned {ranks.Length} rows.");
            Assert.Equal(Enumerable.Range(1, ranks.Length).Select(rank => (long)rank), ranks.Order());
        }
    }

    [Fact]
    public async Task Row_number_with_tie_breaker_caps_ties_at_10_where_dense_rank_would_not()
    {
        // 12 products with IDENTICAL revenue: DENSE_RANK gives all of them rank 1 (12 rows);
        // ROW_NUMBER ... ORDER BY Revenue DESC, UnitsSold DESC, Sku keeps exactly 10, deterministically.
        const string sql = """
            WITH Revenue AS (SELECT CONCAT('SKU-', RIGHT(CONCAT('00', n), 2)) AS Sku, 100.00 AS Revenue, 5 AS UnitsSold
                             FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS v(n))
            SELECT
                (SELECT COUNT(*) FROM (SELECT DENSE_RANK() OVER (ORDER BY Revenue DESC) AS r FROM Revenue) AS d WHERE r <= 10),
                (SELECT COUNT(*) FROM (SELECT ROW_NUMBER() OVER (ORDER BY Revenue DESC, UnitsSold DESC, Sku) AS r FROM Revenue) AS w WHERE r <= 10),
                (SELECT MAX(Sku) FROM (SELECT Sku, ROW_NUMBER() OVER (ORDER BY Revenue DESC, UnitsSold DESC, Sku) AS r FROM Revenue) AS w WHERE r <= 10);
            """;
        var row = (await SqlScripts.RunAsync(database.ConnectionString, sql))[0].Rows[0];

        Assert.Equal(12, row[0]);
        Assert.Equal(10, row[1]);
        Assert.Equal("SKU-10", row[2]);
        Assert.Contains("ROW_NUMBER() OVER (PARTITION BY Region ORDER BY Revenue DESC, UnitsSold DESC, Sku ASC)",
            SqlScripts.Read("reporting-queries.sql"));
    }

    [Fact]
    public async Task Stock_sale_transaction_script_writes_stock_movement_receipt_and_payment_together()
    {
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        var before = await CountsAsync(connection, transaction);

        await SqlScripts.RunAsync(database.ConnectionString, SqlScripts.Read("stock-sale-transaction.sql"), transaction, connection);

        var after = await CountsAsync(connection, transaction);
        Assert.Equal(before.Stock - 2, after.Stock);
        Assert.Equal(before.Transactions + 1, after.Transactions);
        Assert.Equal(before.Movements + 1, after.Movements);
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task Stock_sale_transaction_script_rolls_back_and_rethrows_when_stock_is_insufficient()
    {
        var script = SqlScripts.Read("stock-sale-transaction.sql")
            .Replace("DECLARE @Quantity int = 2;", "DECLARE @Quantity int = 1000000;", StringComparison.Ordinal);
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var before = await CountsAsync(connection, null);

        var error = await Assert.ThrowsAsync<SqlException>(() =>
            SqlScripts.RunAsync(database.ConnectionString, script, connection: connection));

        Assert.Equal(51001, error.Number);
        Assert.Equal(before, await CountsAsync(connection, null));
        await using var trancount = new SqlCommand("SELECT @@TRANCOUNT;", connection);
        Assert.Equal(0, (int)(await trancount.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Least_privilege_script_grants_table_by_table_and_never_grants_delete()
    {
        await SqlScripts.RunAsync(database.ConnectionString, SqlScripts.Read("least-privilege.sql"));

        var permissions = (await SqlScripts.RunAsync(database.ConnectionString, """
            SELECT p.class_desc, ISNULL(OBJECT_NAME(p.major_id), '') AS ObjectName, p.permission_name, p.state_desc
            FROM sys.database_permissions AS p
            WHERE p.grantee_principal_id = DATABASE_PRINCIPAL_ID('apexpost_app');
            """))[0].AsEnumerable()
            .Select(row => (Class: row.Field<string>(0)!, Object: row.Field<string>(1)!, Permission: row.Field<string>(2)!, State: row.Field<string>(3)!))
            .ToArray();

        Assert.DoesNotContain(permissions, p => p.Class == "SCHEMA" || p.Class == "DATABASE" && p.Permission != "CONNECT");
        Assert.DoesNotContain(permissions, p => p.Permission == "DELETE" && p.State == "GRANT");
        Assert.Contains(permissions, p => p.Object == "AuditEntries" && p.Permission == "INSERT" && p.State == "GRANT");
        Assert.Contains(permissions, p => p.Object == "AuditEntries" && p.Permission == "UPDATE" && p.State == "DENY");
        Assert.Contains(permissions, p => p.Object == "BranchStock" && p.Permission == "UPDATE" && p.State == "GRANT");
        Assert.Contains(permissions, p => p.Object == "usp_AdjustStock" && p.Permission == "EXECUTE" && p.State == "GRANT");
        Assert.DoesNotContain(permissions, p => p.Object == "TransactionLines" && p.Permission == "UPDATE" && p.State == "GRANT");
    }

    [Fact]
    public async Task Application_role_can_read_and_run_procedures_but_cannot_delete_history_or_touch_migrations()
    {
        await SqlScripts.RunAsync(database.ConnectionString, SqlScripts.Read("least-privilege.sql"));
        await SqlScripts.RunAsync(database.ConnectionString, """
            IF DATABASE_PRINCIPAL_ID('apexpost_test_app_user') IS NULL
                CREATE USER apexpost_test_app_user WITHOUT LOGIN;
            ALTER ROLE apexpost_app ADD MEMBER apexpost_test_app_user;
            """);

        Assert.Equal(0, await AsAppUserAsync("SELECT TOP (1) Id FROM dbo.Branches;"));
        Assert.Equal(0, await AsAppUserAsync("EXEC dbo.usp_LowStockReport @BranchCode = NULL, @Threshold = 0;"));
        Assert.Equal(229, await AsAppUserAsync("DELETE FROM dbo.AuditEntries WHERE 1 = 0;"));
        Assert.Equal(229, await AsAppUserAsync("DELETE FROM dbo.CounterTransactions WHERE 1 = 0;"));
        Assert.Equal(229, await AsAppUserAsync("UPDATE dbo.StockAdjustments SET Reason = Reason WHERE 1 = 0;"));
        Assert.Equal(229, await AsAppUserAsync("SELECT TOP (1) MigrationId FROM dbo.__EFMigrationsHistory;"));
    }

    [Fact]
    public async Task Compliance_script_lists_the_constraints_the_assignment_requires()
    {
        var results = await SqlScripts.RunAsync(database.ConnectionString, SqlScripts.Read("assignment3-compliance.sql"));
        var text = string.Join('\n', results.SelectMany(table => table.AsEnumerable())
            .Select(row => string.Join('|', row.ItemArray)));

        Assert.Contains("CK_BranchStock_QuantityOnHand", text);
        Assert.Contains("CK_Shipments_Weight", text);
        Assert.Contains("CK_IdentityVerifications_MaskedIdentifier", text);
        Assert.Contains("RowVersion", text);
    }

    [Fact]
    public async Task Idempotent_schema_script_builds_an_empty_database_and_is_safe_to_run_twice()
    {
        var builder = new SqlConnectionStringBuilder(database.ConnectionString)
        {
            InitialCatalog = $"ApexPostDb_Script_{Guid.NewGuid():N}"
        };
        var master = new SqlConnectionStringBuilder(database.ConnectionString) { InitialCatalog = "master" }.ConnectionString;
        await SqlScripts.RunAsync(master, $"CREATE DATABASE [{builder.InitialCatalog}];");
        try
        {
            var script = SqlScripts.Read("ApexPostDb_schema.sql");
            await SqlScripts.RunAsync(builder.ConnectionString, script);
            await SqlScripts.RunAsync(builder.ConnectionString, script);

            int expectedMigrations;
            await using (var scope = database.Services.CreateAsyncScope())
            {
                expectedMigrations = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>().Database.GetMigrations().Count();
            }

            Assert.Equal(expectedMigrations, await SqlScripts.ScalarAsync<int>(builder.ConnectionString, "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory;"));
            Assert.Equal(21, await SqlScripts.ScalarAsync<int>(builder.ConnectionString, "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0;"));
            Assert.Equal(3, await SqlScripts.ScalarAsync<int>(builder.ConnectionString, "SELECT COUNT(*) FROM sys.procedures WHERE name LIKE 'usp[_]%';"));
            Assert.Equal(0, await SqlScripts.ScalarAsync<int>(builder.ConnectionString, "SELECT COUNT(*) FROM sys.tables WHERE name = '__Week3ShipmentsToMove';"));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await SqlScripts.RunAsync(master,
                $"ALTER DATABASE [{builder.InitialCatalog}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{builder.InitialCatalog}];");
        }
    }

    private async Task<int> AsAppUserAsync(string statement)
    {
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand($"""
            EXECUTE AS USER = 'apexpost_test_app_user';
            BEGIN TRY
                {statement}
                REVERT;
                SELECT 0;
            END TRY
            BEGIN CATCH
                REVERT;
                SELECT ERROR_NUMBER();
            END CATCH;
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var lastValue = 0;
        do
        {
            while (await reader.ReadAsync())
            {
                if (reader.FieldCount == 1 && reader.GetFieldType(0) == typeof(int))
                {
                    lastValue = reader.GetInt32(0);
                }
            }
        }
        while (await reader.NextResultAsync());

        return lastValue;
    }

    private static async Task<(int Stock, int Transactions, int Movements)> CountsAsync(SqlConnection connection, SqlTransaction? transaction)
    {
        await using var command = new SqlCommand("""
            SELECT bs.QuantityOnHand,
                   (SELECT COUNT(*) FROM dbo.CounterTransactions),
                   (SELECT COUNT(*) FROM dbo.StockAdjustments)
            FROM dbo.BranchStock AS bs
            INNER JOIN dbo.Branches AS b ON b.Id = bs.BranchId
            INNER JOIN dbo.Products AS p ON p.Id = bs.ProductId
            WHERE b.Code = 'AP-001' AND p.Sku = 'PACK-002';
            """, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
    }
}

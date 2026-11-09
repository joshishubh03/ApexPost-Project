using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace ApexPost.Tests.SqlServer;

/// <summary>
/// Runs the hand-written scripts in the repository's sql/ folder the way SSMS/sqlcmd would: GO separates
/// batches, and USE statements are skipped so the script runs against the disposable test database.
/// </summary>
internal static partial class SqlScripts
{
    public static string Read(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "sql", fileName)))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new FileNotFoundException($"sql/{fileName} was not found above the test output folder.");
        }

        return File.ReadAllText(Path.Combine(directory.FullName, "sql", fileName));
    }

    public static IReadOnlyList<string> Batches(string script) =>
        GoSeparator().Split(UseStatement().Replace(script, string.Empty))
            .Where(batch => !string.IsNullOrWhiteSpace(batch))
            .ToArray();

    /// <summary>Executes every batch and returns every result set, in order.</summary>
    public static async Task<IReadOnlyList<DataTable>> RunAsync(
        string connectionString,
        string script,
        SqlTransaction? transaction = null,
        SqlConnection? connection = null)
    {
        var results = new List<DataTable>();
        var ownsConnection = connection is null;
        connection ??= new SqlConnection(connectionString);
        try
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            foreach (var batch in Batches(script))
            {
                await using var command = new SqlCommand(batch, connection, transaction) { CommandTimeout = 120 };
                await using var reader = await command.ExecuteReaderAsync();
                while (!reader.IsClosed)
                {
                    var table = new DataTable();
                    table.Load(reader);
                    results.Add(table);
                }
            }
        }
        finally
        {
            if (ownsConnection)
            {
                await connection.DisposeAsync();
            }
        }

        return results;
    }

    public static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return (T)Convert.ChangeType((await command.ExecuteScalarAsync())!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"^\s*GO\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex GoSeparator();

    [GeneratedRegex(@"^\s*USE\s+\[?\w+\]?\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex UseStatement();
}

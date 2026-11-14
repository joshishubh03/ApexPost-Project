using ApexPost.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ApexPost.Tests.Infrastructure;

/// <summary>
/// Script generation only (no database connection is opened). Proves that the shipped Week 3 migration is used
/// unchanged and that the idempotent script still wraps its raw SQL in EXEC (ADR 0053).
/// </summary>
public sealed class MigrationScriptTests
{
    private const string BeforeWeek3 = "20261102130000_SyncModelChanges";
    private const string Week3SchemaCompliance = "20261102223854_Week3SchemaCompliance";

    private static ApexPostDbContext CreateContext() => new(new DbContextOptionsBuilder<ApexPostDbContext>()
        .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ApexPostScriptOnly;Trusted_Connection=True;TrustServerCertificate=True")
        .Options);

    [Fact]
    public void The_context_uses_the_idempotent_script_generator()
    {
        using var context = CreateContext();
        Assert.IsType<IdempotentScriptMigrationsSqlGenerator>(context.GetService<IMigrationsSqlGenerator>());
    }

    [Fact]
    public void Idempotent_script_runs_week3_raw_sql_and_procedures_through_exec()
    {
        using var context = CreateContext();
        var script = context.GetService<IMigrator>()
            .GenerateScript(BeforeWeek3, Week3SchemaCompliance, MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("EXEC(N'CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales", script, StringComparison.Ordinal);
        Assert.Contains("EXEC(N'CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport", script, StringComparison.Ordinal);
        Assert.Contains("EXEC(N'CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock", script, StringComparison.Ordinal);

        // No procedure is created as a bare statement inside an IF ... BEGIN block, and the staging-table copy
        // (which reads columns this migration drops) is compiled only when it actually runs.
        Assert.DoesNotContain(Lines(script), line => line.StartsWith("CREATE OR ALTER PROCEDURE", StringComparison.Ordinal));
        Assert.True(IsInsideExec(script, "SELECT ct.Id AS CounterTransactionId"),
            "The staging-table copy must run through EXEC in the idempotent script.");
    }

    [Fact]
    public void Normal_migration_sql_is_unchanged_and_creates_procedures_as_their_own_batches()
    {
        using var context = CreateContext();
        var script = context.GetService<IMigrator>().GenerateScript(BeforeWeek3, Week3SchemaCompliance);

        Assert.Contains(Lines(script), line => line.StartsWith("CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales", StringComparison.Ordinal));
        Assert.DoesNotContain("EXEC(N'CREATE OR ALTER PROCEDURE", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_go_separated_batch_gets_its_own_exec_and_quotes_are_doubled()
    {
        using var context = CreateContext();
        var generator = context.GetService<IMigrationsSqlGenerator>();

        var commands = generator.Generate(
            [new SqlOperation { Sql = "SELECT 1;\nGO\nSELECT 'O''Neil';" }],
            model: null,
            MigrationsSqlGenerationOptions.Idempotent);

        Assert.Equal(2, commands.Count);
        Assert.StartsWith("EXEC(N'SELECT 1;", commands[0].CommandText.TrimStart(), StringComparison.Ordinal);
        Assert.Contains("SELECT ''O''''Neil'';", commands[1].CommandText, StringComparison.Ordinal);
        Assert.Equal("EXEC(N'SELECT ''a'';');", IdempotentScriptMigrationsSqlGenerator.ToExecStatement("SELECT 'a';"));
    }

    [Fact]
    public void Committed_schema_script_matches_what_the_migrations_generate()
    {
        using var context = CreateContext();
        var generated = context.GetService<IMigrator>()
            .GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        var committed = ApexPost.Tests.SqlServer.SqlScripts.Read("ApexPostDb_schema.sql");

        var expected = Lines(generated);
        var actual = Lines(committed);
        var firstDifference = Enumerable.Range(0, Math.Min(expected.Length, actual.Length))
            .FirstOrDefault(index => expected[index] != actual[index], -1);

        Assert.True(firstDifference < 0 && expected.Length == actual.Length,
            "sql/ApexPostDb_schema.sql is out of date. Regenerate it with: dotnet ef migrations script --idempotent " +
            "--project ApexPost.Infrastructure --startup-project ApexPost.Web --output sql/ApexPostDb_schema.sql" +
            (firstDifference >= 0
                ? $"{Environment.NewLine}First difference at line {firstDifference + 1}:{Environment.NewLine}" +
                  $"  generated: {expected[firstDifference]}{Environment.NewLine}  committed: {actual[firstDifference]}"
                : string.Empty));
    }

    private static string[] Lines(string text) => text
        .TrimStart('﻿')
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Split('\n')
        .Select(line => line.Trim())
        .Where(line => line.Length > 0)
        .ToArray();

    // Text is inside an EXEC(N'...') literal when the nearest EXEC(N' before it has not been closed yet.
    private static bool IsInsideExec(string script, string text)
    {
        var position = script.IndexOf(text, StringComparison.Ordinal);
        if (position < 0)
        {
            return false;
        }

        var execStart = script.LastIndexOf("EXEC(N'", position, StringComparison.Ordinal);
        if (execStart < 0)
        {
            return false;
        }

        var closing = script.IndexOf("');", execStart, StringComparison.Ordinal);
        return closing > position;
    }
}

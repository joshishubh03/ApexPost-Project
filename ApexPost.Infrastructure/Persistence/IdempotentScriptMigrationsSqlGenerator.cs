using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Update;

namespace ApexPost.Infrastructure.Persistence;

/// <summary>
/// SQL Server migrations generator that keeps the <b>idempotent</b> script (<c>dotnet ef migrations script
/// --idempotent</c>) valid for raw SQL, without editing migrations that have already shipped (ADR 0053).
/// </summary>
/// <remarks>
/// <para>
/// The idempotent script puts every migration inside <c>IF NOT EXISTS (... __EFMigrationsHistory ...) BEGIN ... END</c>.
/// EF Core already runs its own risky statements there through <c>EXEC(N'...')</c>, but it emits
/// <c>migrationBuilder.Sql(...)</c> text as written. Two things then break:
/// </para>
/// <list type="bullet">
/// <item><c>CREATE OR ALTER PROCEDURE</c> must be the first statement of a batch, so it cannot sit inside <c>BEGIN ... END</c>;</item>
/// <item>when the script is run again on an up-to-date database, a statement that reads a column dropped by a later
/// migration fails to <i>compile</i>, even though the IF skips it at run time.</item>
/// </list>
/// <para>
/// Only when the idempotent option is set, this generator wraps each raw SQL batch in <c>EXEC(N'...')</c>, which
/// compiles it on its own at run time. Normal migrations (<c>Database.Migrate()</c>, <c>dotnet ef database update</c>,
/// non-idempotent scripts) are generated exactly as before, so the shipped migrations and their behaviour are unchanged.
/// </para>
/// </remarks>
public sealed partial class IdempotentScriptMigrationsSqlGenerator(
    MigrationsSqlGeneratorDependencies dependencies,
    ICommandBatchPreparer commandBatchPreparer)
    : SqlServerMigrationsSqlGenerator(dependencies, commandBatchPreparer)
{
    public override IReadOnlyList<MigrationCommand> Generate(
        IReadOnlyList<MigrationOperation> operations,
        IModel? model = null,
        MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        if (!options.HasFlag(MigrationsSqlGenerationOptions.Idempotent))
        {
            return base.Generate(operations, model, options);
        }

        var rewritten = new List<MigrationOperation>(operations.Count);
        foreach (var operation in operations)
        {
            if (operation is SqlOperation sql)
            {
                rewritten.AddRange(WrapEachBatch(sql));
            }
            else
            {
                rewritten.Add(operation);
            }
        }

        return base.Generate(rewritten, model, options);
    }

    /// <summary>
    /// <c>EXEC(N'...')</c> around one T-SQL batch; single quotes are doubled so the text survives as a literal.
    /// </summary>
    public static string ToExecStatement(string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        return "EXEC(N'" + sql.Replace("'", "''", StringComparison.Ordinal) + "');";
    }

    private static IEnumerable<SqlOperation> WrapEachBatch(SqlOperation operation)
    {
        // A GO line separates batches (as in SSMS). EXEC cannot contain GO, so each batch gets its own EXEC.
        foreach (var batch in GoSeparator().Split(operation.Sql))
        {
            if (!string.IsNullOrWhiteSpace(batch))
            {
                yield return new SqlOperation
                {
                    Sql = ToExecStatement(batch),
                    SuppressTransaction = operation.SuppressTransaction
                };
            }
        }
    }

    [GeneratedRegex(@"^\s*GO\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex GoSeparator();
}

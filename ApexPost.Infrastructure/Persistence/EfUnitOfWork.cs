using System.Data;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence;

/// <summary>
/// EF Core unit of work. Repositories only stage changes on the shared scoped DbContext; this class is the
/// single place that calls SaveChangesAsync, so one use case = one commit.
/// </summary>
public sealed class EfUnitOfWork(ApexPostDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(
                "Another user changed this record before your change was saved (for example, the last unit of stock " +
                "was sold at another counter). Nothing was saved - refresh the page and try again.",
                exception);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new DuplicateResourceException(
                "A record with the same unique value (for example the same SKU, branch code or e-mail) already exists.",
                exception);
        }
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // Nested use case (for example a money order issued inside a counter sale): join the outer
        // transaction and let the outer boundary commit once.
        if (context.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        // A user-initiated transaction must run inside the execution strategy so that a transient
        // failure retries the WHOLE unit of work (EnableRetryOnFailure is configured in DependencyInjection).
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // A retry must not replay changes staged by the failed attempt.
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            var result = await operation(cancellationToken);
            await SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}

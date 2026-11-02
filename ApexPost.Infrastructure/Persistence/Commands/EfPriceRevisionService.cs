using ApexPost.Application.Queries;
using ApexPost.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Commands;

/// <summary>
/// Set-based price revision. ExecuteUpdateAsync issues one UPDATE ... WHERE statement instead of loading
/// and tracking every product. Because it is combined with a SaveChangesAsync (the audit entry), both run in
/// one explicit transaction inside the execution strategy, so a transient failure retries both together.
/// </summary>
public sealed class EfPriceRevisionService(ApexPostDbContext context) : IPriceRevisionService
{
    public async Task<int> RevisePricesAsync(
        string category,
        decimal percentChange,
        Guid actorId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        if (percentChange == 0 || percentChange < -50m || percentChange > 100m || decimal.Round(percentChange, 2) != percentChange)
        {
            throw new ArgumentOutOfRangeException(nameof(percentChange), "Enter a non-zero change between -50% and +100% with at most two decimal places.");
        }

        var normalizedCategory = category.Trim();
        var factor = 1m + percentChange / 100m;
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var categoryExists = await context.ProductCategories.AnyAsync(row => row.Code == normalizedCategory, cancellationToken);
            if (!categoryExists)
            {
                throw new KeyNotFoundException($"The product category '{normalizedCategory}' does not exist.");
            }

            var now = DateTime.UtcNow;
            var updated = await context.Products
                .Where(product => product.Category == normalizedCategory && product.IsActive)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(product => product.Price, product => Math.Round(product.Price * factor, 2))
                    .SetProperty(product => EF.Property<DateTime?>(product, AuditShadowProperties.ModifiedAtUtc), now)
                    .SetProperty(product => EF.Property<Guid?>(product, AuditShadowProperties.ModifiedBy), actorId),
                    cancellationToken);

            context.AuditEntries.Add(new AuditEntryRow
            {
                Id = Guid.NewGuid(),
                StaffId = actorId,
                Role = "BranchManager",
                Action = "ProductPriceRevision",
                EntityType = "ProductCategory",
                ResultStatusCode = 200,
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = correlationId,
                SafeDetailsJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    Category = normalizedCategory,
                    PercentChange = percentChange,
                    ProductsUpdated = updated
                })
            });
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return updated;
        });
    }
}

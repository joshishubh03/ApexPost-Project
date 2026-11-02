using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Infrastructure.Persistence.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Commands;

/// <summary>
/// Stock adjustment through dbo.usp_AdjustStock. Mixes a stored procedure, an ExecuteUpdate and a
/// SaveChanges, so all three run in one explicit transaction created inside the execution strategy.
/// The procedure detects the outer transaction (@@TRANCOUNT &gt; 0) and does not commit on its own.
/// </summary>
public sealed class SqlStockAdjustmentCommand(
    ApexPostDbContext context,
    IStoredProcedureService storedProcedures) : IStockAdjustmentCommand
{
    public async Task<StockAdjustmentResult> AdjustAsync(
        Guid branchId,
        Guid productId,
        int quantityChange,
        int reorderLevel,
        string reason,
        Guid staffId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentOutOfRangeException.ThrowIfNegative(reorderLevel);
        if (quantityChange == 0)
            throw new InvalidOperationException("Enter a non-zero quantity change.");

        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var branch = await context.Branches.AsNoTracking()
                .Where(row => row.Id == branchId)
                .Select(row => new { row.Code, row.IsOpen })
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("The branch could not be found.");
            if (!branch.IsOpen)
                throw new InvalidOperationException("Inventory cannot be changed for a closed branch.");

            var product = await context.Products.AsNoTracking()
                .Where(row => row.Id == productId && row.IsActive)
                .Select(row => new { row.Sku, row.Name })
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Select an active catalogue product.");
            var staffEmail = await context.Staff.AsNoTracking()
                .Where(row => row.Id == staffId && row.IsActive)
                .Select(row => row.Email)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new UnauthorizedAccessException("The staff account could not be found.");

            StockAdjustmentResult result;
            try
            {
                // 1. Quantity + history row: stored procedure (parameterized).
                result = await storedProcedures.AdjustStockAsync(
                    branch.Code.Value, product.Sku.Value, quantityChange, reason.Trim(), staffEmail, cancellationToken);
            }
            catch (SqlException exception) when (exception.Number is >= 50000 and < 51000)
            {
                // Business-rule errors raised by THROW in the procedure become domain errors (HTTP 400 / view message).
                throw new InvalidOperationException(exception.Message, exception);
            }

            // 2. Reorder level: set-based update, no tracking.
            await context.BranchStock
                .Where(row => row.BranchId == branchId && row.ProductId == productId && row.ReorderLevel != reorderLevel)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.ReorderLevel, reorderLevel), cancellationToken);

            // 3. Notification (+ audit is written by the MVC audit filter): tracked insert + SaveChanges.
            if (result.QuantityOnHand <= reorderLevel)
            {
                context.OperationalNotifications.Add(new OperationalNotificationRow
                {
                    Id = Guid.NewGuid(),
                    BranchId = branchId,
                    StaffId = staffId,
                    Kind = "StockLow",
                    Message = $"{product.Name} is at {result.QuantityOnHand} units (reorder level {reorderLevel}).",
                    OccurredAt = DateTimeOffset.UtcNow,
                    CorrelationId = correlationId,
                    ReferenceId = productId.ToString("D")
                });
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}

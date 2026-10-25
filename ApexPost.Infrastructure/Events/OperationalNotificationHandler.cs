using ApexPost.Application.Abstractions;
using ApexPost.Application.Events;
using ApexPost.Application.Models;
using Microsoft.Extensions.Logging;

namespace ApexPost.Infrastructure.Events;

public sealed class OperationalNotificationHandler(
    INotificationRepository notificationRepository,
    ILogger<OperationalNotificationHandler> logger) :
    IApplicationEventHandler<StockLowEvent>,
    IApplicationEventHandler<HighValueMoneyOrderEvent>,
    IApplicationEventHandler<CounterTransactionReversedEvent>,
    IApplicationEventHandler<MoneyOrderRefundedEvent>
{
    IReadOnlyCollection<Type> IApplicationEventHandler.EventTypes =>
    [typeof(StockLowEvent), typeof(HighValueMoneyOrderEvent), typeof(CounterTransactionReversedEvent), typeof(MoneyOrderRefundedEvent)];

    Task IApplicationEventHandler.HandleAsync(object applicationEvent, CancellationToken cancellationToken) => applicationEvent switch
    {
        StockLowEvent stockLow => HandleAsync(stockLow, cancellationToken),
        HighValueMoneyOrderEvent highValue => HandleAsync(highValue, cancellationToken),
        CounterTransactionReversedEvent reversed => HandleAsync(reversed, cancellationToken),
        MoneyOrderRefundedEvent refunded => HandleAsync(refunded, cancellationToken),
        _ => throw new ArgumentException($"Unsupported notification event type: {applicationEvent.GetType().Name}.", nameof(applicationEvent))
    };

    public Task HandleAsync(StockLowEvent applicationEvent, CancellationToken cancellationToken = default) => AppendAsync(
        "LowStock",
        $"{applicationEvent.ProductName}: {applicationEvent.QuantityOnHand} on hand (reorder at {applicationEvent.ReorderLevel}).",
        applicationEvent.BranchId, applicationEvent.ActorId, applicationEvent.OccurredAt,
        applicationEvent.CorrelationId, applicationEvent.ProductId.ToString("D"), cancellationToken);

    public Task HandleAsync(HighValueMoneyOrderEvent applicationEvent, CancellationToken cancellationToken = default) => AppendAsync(
        "HighValueMoneyOrder",
        $"Money order {applicationEvent.MoneyOrderNumber} issued for {applicationEvent.TotalAmount:C2}.",
        applicationEvent.BranchId, applicationEvent.ActorId, applicationEvent.OccurredAt,
        applicationEvent.CorrelationId, applicationEvent.MoneyOrderId.ToString("D"), cancellationToken);

    public Task HandleAsync(CounterTransactionReversedEvent applicationEvent, CancellationToken cancellationToken = default) => AppendAsync(
        applicationEvent.Status,
        $"Transaction {applicationEvent.TransactionNumber} {applicationEvent.Status.ToLowerInvariant()} for {applicationEvent.Amount:C2}. Reason: {applicationEvent.Reason}",
        applicationEvent.BranchId, applicationEvent.StaffId, applicationEvent.OccurredAt,
        applicationEvent.CorrelationId, applicationEvent.TransactionId.ToString("D"), cancellationToken);

    public Task HandleAsync(MoneyOrderRefundedEvent applicationEvent, CancellationToken cancellationToken = default) =>
        applicationEvent.PartOfCounterTransaction
            ? Task.CompletedTask
            : AppendAsync("MoneyOrderRefunded",
                $"Money order {applicationEvent.MoneyOrderNumber} refunded for {applicationEvent.TotalAmount:C2}. Reason: {applicationEvent.Reason}",
                applicationEvent.BranchId, applicationEvent.ManagerId, applicationEvent.OccurredAt,
                applicationEvent.CorrelationId, applicationEvent.MoneyOrderId.ToString("D"), cancellationToken);

    private async Task AppendAsync(
        string type,
        string message,
        Guid branchId,
        Guid actorId,
        DateTimeOffset occurredAt,
        string correlationId,
        string referenceId,
        CancellationToken cancellationToken)
    {
        try
        {
            await notificationRepository.AppendAsync(new OperationalNotification(
                Guid.NewGuid(), type, message, branchId, actorId, occurredAt, correlationId, referenceId), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Could not persist simulated {NotificationType} notification for branch {BranchId}.", type, branchId);
        }
    }
}

using ApexPost.Application.Abstractions;
using ApexPost.Application.Events;
using ApexPost.Application.Models;
using Microsoft.Extensions.Logging;

namespace ApexPost.Infrastructure.Events;

public sealed class MoneyOrderRefundAuditHandler(
    IAuditLogRepository auditLogRepository,
    ILogger<MoneyOrderRefundAuditHandler> logger) : IApplicationEventHandler<MoneyOrderRefundedEvent>
{
    public async Task HandleAsync(MoneyOrderRefundedEvent applicationEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            await auditLogRepository.AppendAsync(new AuditRecord(
                Guid.NewGuid(), applicationEvent.OccurredAt, "MoneyOrderRefunded", "MoneyOrder",
                applicationEvent.MoneyOrderId.ToString("D"), applicationEvent.ManagerId,
                applicationEvent.BranchId, applicationEvent.CorrelationId, applicationEvent.TotalAmount,
                applicationEvent.Reason), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception,
                "Could not persist audit record for refunded money order {MoneyOrderNumber} (correlation {CorrelationId}).",
                applicationEvent.MoneyOrderNumber, applicationEvent.CorrelationId);
        }
    }
}

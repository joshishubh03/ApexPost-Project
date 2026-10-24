using ApexPost.Application.Abstractions;
using ApexPost.Application.Events;
using ApexPost.Application.Models;
using Microsoft.Extensions.Logging;

namespace ApexPost.Infrastructure.Events;

public sealed class MoneyOrderAuditHandler(
    IAuditLogRepository auditLogRepository,
    ILogger<MoneyOrderAuditHandler> logger) : IApplicationEventHandler<MoneyOrderIssuedEvent>
{
    public async Task HandleAsync(MoneyOrderIssuedEvent applicationEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            await auditLogRepository.AppendAsync(new AuditRecord(
                Guid.NewGuid(),
                applicationEvent.OccurredAt,
                "MoneyOrderIssued",
                "MoneyOrder",
                applicationEvent.MoneyOrderId.ToString("D"),
                applicationEvent.StaffId,
                applicationEvent.BranchId,
                applicationEvent.CorrelationId,
                applicationEvent.TotalAmount), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception,
                "Could not persist audit record for money order {MoneyOrderNumber} (correlation {CorrelationId}).",
                applicationEvent.MoneyOrderNumber,
                applicationEvent.CorrelationId);
        }
    }
}

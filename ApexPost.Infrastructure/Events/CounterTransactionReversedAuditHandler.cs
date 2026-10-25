using ApexPost.Application.Abstractions;
using ApexPost.Application.Events;
using ApexPost.Application.Models;
using Microsoft.Extensions.Logging;

namespace ApexPost.Infrastructure.Events;

public sealed class CounterTransactionReversedAuditHandler(
    IAuditLogRepository auditLogRepository,
    ILogger<CounterTransactionReversedAuditHandler> logger)
    : IApplicationEventHandler<CounterTransactionReversedEvent>
{
    public async Task HandleAsync(CounterTransactionReversedEvent applicationEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            await auditLogRepository.AppendAsync(new AuditRecord(
                Guid.NewGuid(),
                applicationEvent.OccurredAt,
                $"CounterTransaction{applicationEvent.Status}",
                "CounterTransaction",
                applicationEvent.TransactionId.ToString("D"),
                applicationEvent.StaffId,
                applicationEvent.BranchId,
                applicationEvent.CorrelationId,
                applicationEvent.Amount), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception,
                "Could not persist audit record for reversed transaction {TransactionNumber} (correlation {CorrelationId}).",
                applicationEvent.TransactionNumber,
                applicationEvent.CorrelationId);
        }
    }
}

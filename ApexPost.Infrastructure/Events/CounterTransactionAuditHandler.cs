using ApexPost.Application.Abstractions;
using ApexPost.Application.Events;
using ApexPost.Application.Models;
using Microsoft.Extensions.Logging;

namespace ApexPost.Infrastructure.Events;

public sealed class CounterTransactionAuditHandler(
    IAuditLogRepository auditLogRepository,
    ILogger<CounterTransactionAuditHandler> logger) : IApplicationEventHandler<CounterTransactionCompletedEvent>
{
    public async Task HandleAsync(
        CounterTransactionCompletedEvent applicationEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await auditLogRepository.AppendAsync(new AuditRecord(
                Guid.NewGuid(),
                applicationEvent.OccurredAt,
                "CounterTransactionCompleted",
                "CounterTransaction",
                applicationEvent.TransactionId.ToString("D"),
                applicationEvent.StaffId,
                applicationEvent.BranchId,
                applicationEvent.CorrelationId,
                applicationEvent.TotalAmount), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The sale is already committed; audit storage failure must not make the
            // caller retry a completed sale. Record the gap for operators to investigate.
            logger.LogError(exception,
                "Could not persist audit record for transaction {TransactionNumber} (correlation {CorrelationId}).",
                applicationEvent.TransactionNumber,
                applicationEvent.CorrelationId);
        }
    }
}

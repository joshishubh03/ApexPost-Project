using ApexPost.Application.Abstractions;
using ApexPost.Application.Events;
using ApexPost.Application.Models;
using Microsoft.Extensions.Logging;

namespace ApexPost.Infrastructure.Events;

public sealed class IdentityVerificationAuditHandler(
    IAuditLogRepository auditLogRepository,
    ILogger<IdentityVerificationAuditHandler> logger) : IApplicationEventHandler<IdentityVerificationRecordedEvent>
{
    public async Task HandleAsync(
        IdentityVerificationRecordedEvent applicationEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await auditLogRepository.AppendAsync(new AuditRecord(
                Guid.NewGuid(),
                applicationEvent.OccurredAt,
                $"IdentityVerification{applicationEvent.Status}",
                "IdentityVerification",
                applicationEvent.VerificationId.ToString("D"),
                applicationEvent.StaffId,
                applicationEvent.BranchId,
                applicationEvent.CorrelationId,
                applicationEvent.MoneyOrderAmount,
                $"DocumentType={applicationEvent.DocumentType}; Result={applicationEvent.Status}"), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception,
                "Could not persist audit record for identity verification {VerificationId} (correlation {CorrelationId}).",
                applicationEvent.VerificationId,
                applicationEvent.CorrelationId);
        }
    }
}

using ApexPost.Application.Abstractions;
using ApexPost.Application.Events;
using ApexPost.Application.Models;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Application.Services;

public sealed class MoneyOrderService(
    IBranchRepository branchRepository,
    IStaffRepository staffRepository,
    IMoneyOrderRepository moneyOrderRepository,
    ICounterTransactionRepository transactionRepository,
    IIdentityVerificationRepository verificationRepository,
    IIdentityVerificationService identityVerificationService,
    IApplicationEventPublisher eventPublisher,
    MoneyOrderPolicy policy)
{
    public async Task<IReadOnlyList<MoneyOrder>> GetForBranchAsync(
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        var allOrders = await moneyOrderRepository.GetAllAsync(cancellationToken);
        return allOrders.Where(order => order.BranchId == branchId)
            .OrderByDescending(order => order.OccurredAt)
            .Take(50)
            .ToArray();
    }

    public async Task<MoneyOrder> IssueAsync(
        Guid branchId,
        Guid staffId,
        string senderName,
        string recipientName,
        decimal amount,
        IdentityDocumentType? documentType,
        string? documentIdentifier,
        string correlationId,
        CancellationToken cancellationToken = default,
        Guid? orderId = null)
    {
        ValidatePolicy();
        var branch = await branchRepository.GetByIdAsync(branchId, cancellationToken)
            ?? throw new KeyNotFoundException("The branch could not be found.");
        if (!branch.IsOpen)
        {
            throw new InvalidOperationException("A money order cannot be issued at a closed branch.");
        }

        var staff = (await staffRepository.GetAllAsync(cancellationToken))
            .FirstOrDefault(candidate => candidate.Id == staffId)
            ?? throw new UnauthorizedAccessException("The staff account could not be found.");
        if (!staff.IsActive ||
            staff.Role is not (StaffRole.Administrator or StaffRole.BranchManager or StaffRole.CounterClerk) ||
            (staff.Role != StaffRole.Administrator && staff.BranchId != branchId))
        {
            throw new UnauthorizedAccessException("This staff account cannot issue money orders for the selected branch.");
        }

        if (amount <= 0 || decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Enter a positive amount with no more than two decimal places.");
        }

        var verificationStatus = IdentityVerificationStatus.NotRequired;
        Guid? verificationRecordId = null;
        string? maskedIdentifier = null;
        IdentityDocumentType? recordedDocumentType = null;

        if (amount > policy.IdentityVerificationThreshold)
        {
            if (documentType is null || !Enum.IsDefined(documentType.Value) || string.IsNullOrWhiteSpace(documentIdentifier))
            {
                throw new InvalidOperationException("Identity document type and identifier are required above the verification threshold.");
            }

            maskedIdentifier = MaskDocumentIdentifier(documentIdentifier);
            recordedDocumentType = documentType;
            verificationStatus = await identityVerificationService.VerifyAsync(
                documentType.Value,
                documentIdentifier,
                cancellationToken)
                ? IdentityVerificationStatus.Verified
                : IdentityVerificationStatus.Failed;

            var verification = new IdentityVerificationRecord(
                Guid.NewGuid(),
                branchId,
                staffId,
                DateTimeOffset.UtcNow,
                documentType.Value,
                maskedIdentifier,
                amount,
                verificationStatus,
                correlationId);
            var verifications = (await verificationRepository.GetAllAsync(cancellationToken)).ToList();
            verifications.Add(verification);
            await verificationRepository.SaveAllAsync(verifications, cancellationToken);
            verificationRecordId = verification.Id;

            if (verificationStatus != IdentityVerificationStatus.Verified)
            {
                throw new InvalidOperationException("Identity verification failed. The money order was not issued.");
            }
        }

        var id = orderId ?? Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var number = $"APM-{occurredAt:yyyyMMdd}-{id.ToString("N")[..8].ToUpperInvariant()}";
        var order = new MoneyOrder(
            id,
            number,
            branchId,
            staffId,
            occurredAt,
            senderName,
            recipientName,
            amount,
            policy.FeeAmount,
            verificationStatus,
            verificationRecordId,
            recordedDocumentType,
            maskedIdentifier);

        var orders = (await moneyOrderRepository.GetAllAsync(cancellationToken)).ToList();
        orders.Add(order);
        await moneyOrderRepository.SaveAllAsync(orders, cancellationToken);

        await eventPublisher.PublishAsync(new MoneyOrderIssuedEvent(
            order.Id,
            order.MoneyOrderNumber,
            order.BranchId,
            order.StaffId,
            order.OccurredAt,
            order.TotalAmount,
            correlationId), CancellationToken.None);

        if (amount > policy.IdentityVerificationThreshold)
        {
            await eventPublisher.PublishAsync(new HighValueMoneyOrderEvent(
                order.Id, order.MoneyOrderNumber, order.BranchId, order.StaffId,
                order.OccurredAt, order.TotalAmount, correlationId), CancellationToken.None);
        }

        return order;
    }

    public async Task<MoneyOrder> RefundAsync(
        Guid branchId,
        Guid moneyOrderId,
        Guid managerId,
        string reason,
        string correlationId,
        CancellationToken cancellationToken = default,
        bool fromCounterTransaction = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Trim().Length > 250)
            throw new ArgumentException("The reason cannot exceed 250 characters.", nameof(reason));

        var branch = await branchRepository.GetByIdAsync(branchId, cancellationToken)
            ?? throw new KeyNotFoundException("The branch could not be found.");
        if (!branch.IsOpen)
            throw new InvalidOperationException("A money order cannot be refunded at a closed branch.");

        var manager = (await staffRepository.GetAllAsync(cancellationToken))
            .FirstOrDefault(candidate => candidate.Id == managerId);
        if (manager is null || !manager.IsActive || manager.Role != StaffRole.BranchManager || manager.BranchId != branchId)
            throw new UnauthorizedAccessException("Only an active Branch Manager assigned to this branch can refund a money order.");

        var orders = (await moneyOrderRepository.GetAllAsync(cancellationToken)).ToList();
        var index = orders.FindIndex(order => order.Id == moneyOrderId && order.BranchId == branchId);
        if (index < 0)
            throw new KeyNotFoundException("The money order could not be found for this branch.");

        if (!fromCounterTransaction && (await transactionRepository.GetAllAsync(cancellationToken))
            .Any(transaction => transaction.MoneyOrderId == moneyOrderId))
            throw new InvalidOperationException("Refund this money order through its originating counter receipt.");

        if (orders[index].Status == MoneyOrderStatus.Refunded && fromCounterTransaction)
            return orders[index];

        var now = DateTimeOffset.UtcNow;
        var refunded = orders[index].Refund(managerId, reason, now);
        orders[index] = refunded;
        await moneyOrderRepository.SaveAllAsync(orders, cancellationToken);
        await eventPublisher.PublishAsync(new MoneyOrderRefundedEvent(
            refunded.Id, refunded.MoneyOrderNumber, branchId, managerId, now, refunded.TotalAmount,
            refunded.RefundReason!, correlationId, fromCounterTransaction), CancellationToken.None);
        return refunded;
    }

    private void ValidatePolicy()
    {
        if (policy.IdentityVerificationThreshold < 0 || decimal.Round(policy.IdentityVerificationThreshold, 2) != policy.IdentityVerificationThreshold)
        {
            throw new InvalidOperationException("The configured identity verification threshold is invalid.");
        }

        if (policy.FeeAmount < 0 || decimal.Round(policy.FeeAmount, 2) != policy.FeeAmount)
        {
            throw new InvalidOperationException("The configured money order fee is invalid.");
        }

        if (string.IsNullOrWhiteSpace(policy.DemoVerificationSuccessPrefix))
        {
            throw new InvalidOperationException("The simulated verification prefix must be configured.");
        }
    }

    private static string MaskDocumentIdentifier(string identifier)
    {
        var normalized = identifier.Trim();
        var visibleCharacters = Math.Min(4, normalized.Length);
        return new string('*', Math.Max(4, normalized.Length - visibleCharacters)) + normalized[^visibleCharacters..];
    }
}

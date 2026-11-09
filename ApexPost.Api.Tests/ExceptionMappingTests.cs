using ApexPost.Api.ErrorHandling;
using ApexPost.Application.Courier;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Courier;
using Microsoft.AspNetCore.Http;

namespace ApexPost.Api.Tests;

/// <summary>Unit tests for the global exception handler's status mapping (no host needed).</summary>
public sealed class ExceptionMappingTests
{
    [Fact]
    public void Business_exceptions_map_to_their_status_codes()
    {
        Assert.Equal(StatusCodes.Status404NotFound, ApiExceptionHandler.Classify(new ShipmentNotFoundException("APX-10000001")).Status);
        Assert.Equal(StatusCodes.Status409Conflict, ApiExceptionHandler.Classify(new DuplicateResourceException("dup")).Status);
        Assert.Equal(StatusCodes.Status409Conflict, ApiExceptionHandler.Classify(
            new InvalidShipmentStatusTransitionException("APX-10000001", CourierShipmentStatus.Created, CourierShipmentStatus.Delivered)).Status);
        Assert.Equal(StatusCodes.Status409Conflict, ApiExceptionHandler.Classify(new ShipmentRuleViolationException("no")).Status);
        Assert.Equal(StatusCodes.Status409Conflict, ApiExceptionHandler.Classify(new ConcurrencyConflictException("changed")).Status);
        Assert.Equal(StatusCodes.Status403Forbidden, ApiExceptionHandler.Classify(new ScanOwnershipException("courier-1", "courier-2")).Status);
    }

    [Fact]
    public void Domain_argument_errors_are_400_but_other_failures_are_a_generic_500()
    {
        var domainError = Record.Exception(() => new Recipient("", "+15550100123", "1 Road", "Town"))!;
        var (status, _, _) = ApiExceptionHandler.Classify(domainError);
        Assert.Equal(StatusCodes.Status400BadRequest, status);

        var (unexpectedStatus, _, detail) = ApiExceptionHandler.Classify(new InvalidOperationException("SQL timeout on server db01"));
        Assert.Equal(StatusCodes.Status500InternalServerError, unexpectedStatus);
        Assert.DoesNotContain("db01", detail, StringComparison.Ordinal);
    }
}

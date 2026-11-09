using ApexPost.Application.Exceptions;
using ApexPost.Application.Models;
using ApexPost.Domain.ValueObjects;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Tests;

/// <summary>
/// Exception -> HTTP status mapping used by <see cref="SafeProblemDetailsExceptionHandler"/> (Part G).
/// Business-rule failures become 400 with their message; anything unexpected becomes a generic 500 that
/// never leaks internal details.
/// </summary>
public sealed class ProblemDetailsMappingTests
{
    [Fact]
    public void Concurrency_conflict_maps_to_409()
    {
        var (status, _, detail) = SafeProblemDetailsExceptionHandler.Classify(
            new ConcurrencyConflictException("Stock changed since it was read."));

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal("Stock changed since it was read.", detail);
    }

    [Fact]
    public void Duplicate_unique_value_maps_to_409()
    {
        var (status, title, _) = SafeProblemDetailsExceptionHandler.Classify(
            new DuplicateResourceException("SKU BOX-1 already exists."));

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Contains("unique", title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Missing_resource_maps_to_404_without_echoing_the_internal_message()
    {
        var (status, _, detail) = SafeProblemDetailsExceptionHandler.Classify(
            new KeyNotFoundException("Branch 7d0c... not found in dbo.Branches"));

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.DoesNotContain("dbo.", detail);
    }

    [Fact]
    public void Domain_rule_violation_maps_to_400_with_its_message()
    {
        var exception = Record.Exception(() => new Sku("BOX 12"));

        var (status, _, detail) = SafeProblemDetailsExceptionHandler.Classify(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Contains("SKU", detail);
    }

    [Fact]
    public void Application_rule_violation_maps_to_400()
    {
        var policy = new TaxPolicy(new Dictionary<string, decimal> { ["Standard"] = 5m });
        var exception = Record.Exception(() => policy.GetRatePercent("Luxury"));

        var (status, _, _) = SafeProblemDetailsExceptionHandler.Classify(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
    }

    [Fact]
    public void Infrastructure_failures_map_to_a_generic_500()
    {
        var exception = new DbUpdateException("Cannot insert duplicate key in object 'dbo.Secret'.");

        var (status, _, detail) = SafeProblemDetailsExceptionHandler.Classify(exception);

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.DoesNotContain("dbo.", detail);
    }

    [Fact]
    public void Invalid_operation_raised_outside_domain_and_application_is_not_treated_as_a_business_rule()
    {
        var exception = Record.Exception(ThrowFromTestCode);

        var (status, _, detail) = SafeProblemDetailsExceptionHandler.Classify(exception);

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.DoesNotContain("internal state", detail);
    }

    private static void ThrowFromTestCode() => throw new InvalidOperationException("internal state");
}

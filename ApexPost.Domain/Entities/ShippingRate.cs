using ApexPost.Domain.Enums;

namespace ApexPost.Domain.Entities;

public sealed record ShippingRate
{
    public Guid Id { get; }
    public Guid ZoneId { get; }
    public ShippingServiceLevel ServiceLevel { get; }
    public decimal BasePrice { get; }
    public decimal PricePerKilogram { get; }
    public decimal MaximumWeightKilograms { get; }
    public bool IsActive { get; }

    public ShippingRate(
        Guid id,
        Guid zoneId,
        ShippingServiceLevel serviceLevel,
        decimal basePrice,
        decimal pricePerKilogram,
        decimal maximumWeightKilograms,
        bool isActive)
    {
        if (id == Guid.Empty || zoneId == Guid.Empty)
        {
            throw new ArgumentException("A rate and its shipping zone are required.");
        }

        if (!Enum.IsDefined(serviceLevel))
        {
            throw new ArgumentOutOfRangeException(nameof(serviceLevel));
        }

        ValidateAmount(basePrice, nameof(basePrice));
        ValidateAmount(pricePerKilogram, nameof(pricePerKilogram));
        if (maximumWeightKilograms <= 0 || decimal.Round(maximumWeightKilograms, 2) != maximumWeightKilograms)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumWeightKilograms), "Maximum weight must be positive and have no more than two decimal places.");
        }

        Id = id;
        ZoneId = zoneId;
        ServiceLevel = serviceLevel;
        BasePrice = basePrice;
        PricePerKilogram = pricePerKilogram;
        MaximumWeightKilograms = maximumWeightKilograms;
        IsActive = isActive;
    }

    public decimal CalculatePrice(decimal weightKilograms)
    {
        if (weightKilograms <= 0 || decimal.Round(weightKilograms, 2) != weightKilograms)
        {
            throw new ArgumentOutOfRangeException(nameof(weightKilograms), "Weight must be positive and have no more than two decimal places.");
        }

        if (weightKilograms > MaximumWeightKilograms)
        {
            throw new InvalidOperationException($"This rate supports parcels up to {MaximumWeightKilograms:0.##} kg.");
        }

        return decimal.Round(BasePrice + PricePerKilogram * weightKilograms, 2, MidpointRounding.AwayFromZero);
    }

    private static void ValidateAmount(decimal amount, string parameterName)
    {
        if (amount < 0 || decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Prices must be non-negative and have no more than two decimal places.");
        }
    }
}

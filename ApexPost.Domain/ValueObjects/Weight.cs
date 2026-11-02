namespace ApexPost.Domain.ValueObjects;

public readonly record struct Weight
{
    /// <summary>ApexPost counter limit for a single shipment; mirrored by CHECK constraints on Shipments and ShippingRates.</summary>
    public const decimal MaximumKilograms = 30m;

    public decimal Kilograms { get; }

    public Weight(decimal kilograms)
    {
        if (kilograms <= 0 || decimal.Round(kilograms, 2) != kilograms)
        {
            throw new ArgumentOutOfRangeException(nameof(kilograms), "Weight must be positive and have no more than two decimal places.");
        }

        if (kilograms > MaximumKilograms)
        {
            throw new ArgumentOutOfRangeException(nameof(kilograms), $"A single shipment cannot exceed {MaximumKilograms:0} kg.");
        }

        Kilograms = kilograms;
    }
}

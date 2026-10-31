namespace ApexPost.Domain.ValueObjects;

public readonly record struct Weight
{
    public decimal Kilograms { get; }

    public Weight(decimal kilograms)
    {
        if (kilograms <= 0 || decimal.Round(kilograms, 2) != kilograms)
        {
            throw new ArgumentOutOfRangeException(nameof(kilograms), "Weight must be positive and have no more than two decimal places.");
        }

        Kilograms = kilograms;
    }
}

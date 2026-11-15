namespace ApexPost.UnitTests.TestDoubles;

/// <summary>
/// A clock that always returns the same instant, so time-based rules are deterministic in tests. Production code
/// takes a <see cref="TimeProvider"/>; tests pass this one.
/// </summary>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    /// <summary>The instant every unit test uses as "now": 9 Nov 2026 09:00 UTC (fictional).</summary>
    public static readonly DateTimeOffset DefaultNow = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);

    public FixedClock() : this(DefaultNow)
    {
    }

    public DateTimeOffset Now => now;

    public override DateTimeOffset GetUtcNow() => now;
}

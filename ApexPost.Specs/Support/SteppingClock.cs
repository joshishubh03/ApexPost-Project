namespace ApexPost.Specs.Support;

/// <summary>
/// Deterministic phone clock: starts at a fixed instant and moves forward one minute every time it is read, so
/// scans recorded one after another get increasing, predictable timestamps (the server applies scans oldest first).
/// </summary>
public sealed class SteppingClock(DateTimeOffset start) : TimeProvider
{
    private long ticks = start.UtcTicks;

    public override DateTimeOffset GetUtcNow() =>
        new(Interlocked.Add(ref ticks, TimeSpan.TicksPerMinute) - TimeSpan.TicksPerMinute, TimeSpan.Zero);
}

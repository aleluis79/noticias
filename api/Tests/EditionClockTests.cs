using Microsoft.Extensions.Options;
using NewsAggregator.Services.Archive;
using Xunit;

public class EditionClockTests
{
    [Fact]
    public void Today_UsesArgentinaTime()
    {
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 11, 18, 2, 30, 0, TimeSpan.Zero));
        var clock = CreateClock(timeProvider);

        Assert.Equal(new DateOnly(2026, 11, 17), clock.Today);
        Assert.Equal(TimeSpan.FromHours(-3), clock.Now.Offset);
    }

    [Fact]
    public void Today_ChangesAfterMidnightInArgentina()
    {
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 11, 18, 3, 1, 0, TimeSpan.Zero));
        var clock = CreateClock(timeProvider);

        Assert.Equal(new DateOnly(2026, 11, 18), clock.Today);
    }

    [Fact]
    public void ResolveTimeZone_FallsBackToArgentinaOffsetWhenTheIdIsUnknown()
    {
        var timeZone = EditionClock.ResolveTimeZone("Invalid/TimeZone");

        Assert.Equal(TimeSpan.FromHours(-3), timeZone.BaseUtcOffset);
    }

    private static EditionClock CreateClock(TimeProvider timeProvider) =>
        new(timeProvider, Options.Create(new ArchiveOptions()));
}

using Microsoft.Extensions.Options;

namespace NewsAggregator.Services.Archive
{
    /// <summary>
    /// Resolves the current edition date in Argentina time. The clock is injected
    /// so midnight rollover can be tested without waiting for the real clock.
    /// </summary>
    public sealed class EditionClock
    {
        private readonly TimeProvider _timeProvider;
        private readonly TimeZoneInfo _timeZone;

        public EditionClock(TimeProvider timeProvider, IOptions<ArchiveOptions> options)
        {
            _timeProvider = timeProvider;
            _timeZone = ResolveTimeZone(options.Value.TimeZoneId);
        }

        public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

        public DateTimeOffset Now => TimeZoneInfo.ConvertTime(UtcNow, _timeZone);

        public DateOnly Today => DateOnly.FromDateTime(Now.DateTime);

        public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
        {
            if (!string.IsNullOrWhiteSpace(timeZoneId))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            // Argentina has used UTC-3 without daylight saving since 2009.
            return TimeZoneInfo.CreateCustomTimeZone(
                "ART",
                TimeSpan.FromHours(-3),
                "Argentina Time",
                "Argentina Time");
        }
    }
}

namespace NewsAggregator.Services.Archive
{
    /// <summary>Configuration for the local archive of daily editions.</summary>
    public class ArchiveOptions
    {
        public const string SectionName = "Archive";

        /// <summary>Directory where one JSON file per edition date is stored.</summary>
        public string Directory { get; set; } = ".archive";

        /// <summary>Number of dates kept, including today.</summary>
        public int RetentionDays { get; set; } = 3;

        /// <summary>IANA time zone used to decide the edition date.</summary>
        public string TimeZoneId { get; set; } = "America/Argentina/Buenos_Aires";

        public int PreviousDays => Math.Max(0, RetentionDays - 1);

        /// <summary>Turns the configured directory into an absolute path.</summary>
        public void ResolvePaths(string contentRoot)
        {
            if (!Path.IsPathRooted(Directory))
            {
                Directory = Path.GetFullPath(Path.Combine(contentRoot, Directory));
            }
        }
    }
}

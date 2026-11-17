namespace NewsAggregator.Services.Parsers
{
    public class TNParser : HomePageParser
    {
        public override string SourceName => "TN";

        public override string BaseUrl => "https://tn.com.ar";

        protected override string[] SectionPaths => new[] { "/politica", "/economia", "/sociedad", "/autos" };
    }
}

namespace NewsAggregator.Services.Parsers
{
    public class ClarínParser : HomePageParser
    {
        public override string SourceName => "Clarín";

        public override string BaseUrl => "https://www.clarin.com";

        protected override string[] SectionPaths => new[] { "/politica", "/economia", "/sociedad", "/autos" };
    }
}

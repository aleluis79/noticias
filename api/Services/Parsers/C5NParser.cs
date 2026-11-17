namespace NewsAggregator.Services.Parsers
{
    public class C5NParser : HomePageParser
    {
        public override string SourceName => "C5N";

        public override string BaseUrl => "https://www.c5n.com";

        protected override string[] SectionPaths => new[] { "/politica", "/economia", "/sociedad", "/autos" };
    }
}

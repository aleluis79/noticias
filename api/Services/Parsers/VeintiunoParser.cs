namespace NewsAggregator.Services.Parsers
{
    public class VeintiunoParser : HomePageParser
    {
        public override string SourceName => "0221";

        public override string BaseUrl => "https://www.0221.com.ar";

        protected override string[] SectionPaths => new[] { "/la-plata", "/provincia", "/policiales" };
    }
}

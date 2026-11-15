namespace NewsAggregator.Services.Parsers
{
    public class SeulParser : HomePageParser
    {
        public override string SourceName => "Seúl";

        public override string BaseUrl => "https://seul.ar";
    }
}

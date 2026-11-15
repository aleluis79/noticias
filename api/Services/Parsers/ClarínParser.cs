namespace NewsAggregator.Services.Parsers
{
    public class ClarínParser : HomePageParser
    {
        public override string SourceName => "Clarín";

        public override string BaseUrl => "https://www.clarin.com";
    }
}

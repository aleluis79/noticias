namespace NewsAggregator.Services.Parsers
{
    public class InfobaeParser : HomePageParser
    {
        public override string SourceName => "Infobae";

        public override string BaseUrl => "https://www.infobae.com";
    }
}

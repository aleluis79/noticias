namespace NewsAggregator.Services.Parsers
{
    public class ElDiaParser : HomePageParser
    {
        public override string SourceName => "El Día";

        public override string BaseUrl => "https://www.eldia.com";
    }
}

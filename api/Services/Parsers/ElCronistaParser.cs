namespace NewsAggregator.Services.Parsers
{
    public class ElCronistaParser : HomePageParser
    {
        public override string SourceName => "El Cronista";

        public override string BaseUrl => "https://www.cronista.com";
    }
}

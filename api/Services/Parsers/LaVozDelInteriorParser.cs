namespace NewsAggregator.Services.Parsers
{
    public class LaVozDelInteriorParser : HomePageParser
    {
        public override string SourceName => "La Voz del Interior";

        public override string BaseUrl => "https://www.lavoz.com.ar";
    }
}

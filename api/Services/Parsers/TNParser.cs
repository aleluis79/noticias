namespace NewsAggregator.Services.Parsers
{
    public class TNParser : HomePageParser
    {
        public override string SourceName => "TN";

        public override string BaseUrl => "https://tn.com.ar";
    }
}

namespace NewsAggregator.Services.Parsers
{
    public class ElDiaParser : HomePageParser
    {
        public override string SourceName => "El Día";

        public override string BaseUrl => "https://www.eldia.com";

        protected override string[] SectionPaths => new[]
        {
            "/politica-y-economia", "/la-ciudad", "/el-mundo", "/vehiculos"
        };
    }
}

namespace NewsAggregator.Services.Parsers
{
    public class LaNacionParser : HomePageParser
    {
        public override string SourceName => "La Nación";

        public override string BaseUrl => "https://www.lanacion.com.ar";

        protected override string[] SectionPaths => new[] { "/politica", "/economia", "/el-mundo", "/autos" };
    }
}

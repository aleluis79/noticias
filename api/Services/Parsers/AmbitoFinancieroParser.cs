namespace NewsAggregator.Services.Parsers
{
    public class AmbitoFinancieroParser : HomePageParser
    {
        public override string SourceName => "Ámbito Financiero";

        public override string BaseUrl => "https://www.ambito.com";

        protected override string[] SectionPaths => new[] { "/politica", "/economia", "/mundo", "/autos" };
    }
}

namespace NewsAggregator.Services.Parsers
{
    public class Pagina12Parser : HomePageParser
    {
        public override string SourceName => "Página/12";

        public override string BaseUrl => "https://www.pagina12.com.ar";

        // Radio programs from AM 750 are linked from the home page but are not news articles.
        protected override string[] AdditionalIgnoredPathTerms => new[] { "/am750/" };
    }
}

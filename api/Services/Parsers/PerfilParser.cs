namespace NewsAggregator.Services.Parsers
{
    public class PerfilParser : HomePageParser
    {
        public override string SourceName => "Perfil";

        public override string BaseUrl => "https://www.perfil.com";
    }
}

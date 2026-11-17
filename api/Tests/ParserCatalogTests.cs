using NewsAggregator.Services.Parsers;
using Xunit;

public class ParserCatalogTests
{
    [Fact]
    public void EveryParser_ShouldDeclareTheExpectedSectionUrls()
    {
        var parsers = new HomePageParser[]
        {
            new ClarínParser(),
            new LaNacionParser(),
            new InfobaeParser(),
            new Pagina12Parser(),
            new TNParser(),
            new AmbitoFinancieroParser(),
            new ElCronistaParser(),
            new PerfilParser(),
            new C5NParser(),
            new LaVozDelInteriorParser(),
            new ElDiaParser(),
            new VeintiunoParser(),
            new SeulParser()
        };

        var expected = new Dictionary<string, string[]>
        {
            ["Clarín"] = new[]
            {
                "https://www.clarin.com/politica",
                "https://www.clarin.com/economia",
                "https://www.clarin.com/sociedad",
                "https://www.clarin.com/autos"
            },
            ["La Nación"] = new[]
            {
                "https://www.lanacion.com.ar/politica",
                "https://www.lanacion.com.ar/economia",
                "https://www.lanacion.com.ar/el-mundo",
                "https://www.lanacion.com.ar/autos"
            },
            ["Infobae"] = new[]
            {
                "https://www.infobae.com/politica",
                "https://www.infobae.com/economia",
                "https://www.infobae.com/sociedad",
                "https://www.infobae.com/autos"
            },
            ["Página/12"] = new[]
            {
                "https://www.pagina12.com.ar/el-pais",
                "https://www.pagina12.com.ar/economia",
                "https://www.pagina12.com.ar/sociedad"
            },
            ["TN"] = new[]
            {
                "https://tn.com.ar/politica",
                "https://tn.com.ar/economia",
                "https://tn.com.ar/sociedad",
                "https://tn.com.ar/autos"
            },
            ["Ámbito Financiero"] = new[]
            {
                "https://www.ambito.com/politica",
                "https://www.ambito.com/economia",
                "https://www.ambito.com/mundo",
                "https://www.ambito.com/autos"
            },
            ["El Cronista"] = new[]
            {
                "https://www.cronista.com/economia-politica",
                "https://www.cronista.com/finanzas-mercados",
                "https://www.cronista.com/tecnologia",
                "https://www.cronista.com/autos"
            },
            ["Perfil"] = new[]
            {
                "https://www.perfil.com/politica",
                "https://www.perfil.com/economia",
                "https://www.perfil.com/sociedad",
                "https://www.perfil.com/autos"
            },
            ["C5N"] = new[]
            {
                "https://www.c5n.com/politica",
                "https://www.c5n.com/economia",
                "https://www.c5n.com/sociedad",
                "https://www.c5n.com/autos"
            },
            ["La Voz del Interior"] = new[]
            {
                "https://www.lavoz.com.ar/politica",
                "https://www.lavoz.com.ar/negocios",
                "https://www.lavoz.com.ar/ciudadanos"
            },
            ["El Día"] = new[]
            {
                "https://www.eldia.com/politica-y-economia",
                "https://www.eldia.com/la-ciudad",
                "https://www.eldia.com/el-mundo",
                "https://www.eldia.com/vehiculos"
            },
            ["0221"] = new[]
            {
                "https://www.0221.com.ar/la-plata",
                "https://www.0221.com.ar/provincia",
                "https://www.0221.com.ar/policiales"
            },
            ["Seúl"] = Array.Empty<string>()
        };

        Assert.Equal(expected.Count, parsers.Length);

        foreach (var parser in parsers)
        {
            Assert.True(
                expected.TryGetValue(parser.SourceName, out var expectedUrls),
                $"Falta la expectativa para {parser.SourceName}");

            Assert.Equal(expectedUrls, parser.SectionUrls);
        }
    }
}

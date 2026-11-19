namespace NewsAggregator.Services.Summaries
{
    /// <summary>Configuration for the local article summaries.</summary>
    public class SummaryOptions
    {
        public const string SectionName = "Summaries";

        public bool Enabled { get; set; } = true;

        /// <summary>Base URL of the Ollama server.</summary>
        public string OllamaUrl { get; set; } = "http://localhost:11434";

        /// <summary>Ollama model used to summarize the articles.</summary>
        public string Model { get; set; } = "gemma4:e2b";

        /// <summary>Directory where one JSON file per article is stored.</summary>
        public string Directory { get; set; } = ".summaries";

        /// <summary>Maximum number of article characters sent to the model.</summary>
        public int MaxInputCharacters { get; set; } = 12000;

        /// <summary>Upper bound for a single generation request.</summary>
        public int TimeoutSeconds { get; set; } = 300;

        /// <summary>Maximum number of concurrent generation requests.</summary>
        public int MaxConcurrent { get; set; } = 1;

        /// <summary>Turns the configured directory into an absolute path.</summary>
        public void ResolvePaths(string contentRoot)
        {
            if (!Path.IsPathRooted(Directory))
            {
                Directory = Path.GetFullPath(Path.Combine(contentRoot, Directory));
            }
        }
    }
}

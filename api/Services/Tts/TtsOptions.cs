namespace NewsAggregator.Services.Tts
{
    /// <summary>Configuration for the local Piper text-to-speech engine.</summary>
    public class TtsOptions
    {
        public bool Enabled { get; set; } = true;

        /// <summary>Path to the Piper executable, or a command available on PATH.</summary>
        public string PiperPath { get; set; } = ".tts/piper/piper";

        /// <summary>Path to the Piper voice model (.onnx).</summary>
        public string ModelPath { get; set; } = ".tts/voices/es_AR-daniela-high.onnx";

        /// <summary>FFmpeg executable used to encode MP3, or a command on PATH.</summary>
        public string FfmpegPath { get; set; } = "ffmpeg";

        public bool EnableMp3 { get; set; } = true;

        public string CacheDirectory { get; set; } = ".tts/cache";

        /// <summary>Upper bound for a single segment to keep requests bounded.</summary>
        public int MaxCharacters { get; set; } = 4000;

        /// <summary>Maximum number of concurrent Piper processes.</summary>
        public int MaxConcurrent { get; set; } = 2;

        /// <summary>
        /// Turns relative file paths into paths under the app content root. Bare
        /// command names such as "ffmpeg" are left untouched so PATH lookup works.
        /// </summary>
        public void ResolvePaths(string contentRoot)
        {
            PiperPath = ResolveExecutable(PiperPath, contentRoot);
            FfmpegPath = ResolveExecutable(FfmpegPath, contentRoot);
            ModelPath = ResolveFile(ModelPath, contentRoot);
            CacheDirectory = ResolveFile(CacheDirectory, contentRoot);
        }

        private static string ResolveExecutable(string value, string contentRoot) =>
            value.Contains(Path.DirectorySeparatorChar) || value.Contains('/')
                ? ResolveFile(value, contentRoot)
                : value;

        private static string ResolveFile(string value, string contentRoot) =>
            Path.IsPathRooted(value) ? value : Path.GetFullPath(Path.Combine(contentRoot, value));
    }
}

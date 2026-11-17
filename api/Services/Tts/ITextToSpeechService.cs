namespace NewsAggregator.Services.Tts
{
    public sealed record TtsAudio(byte[] Bytes, string ContentType);

    /// <summary>Local text-to-speech engine for article segments.</summary>
    public interface ITextToSpeechService
    {
        bool IsEnabled { get; }

        /// <summary>
        /// Synthesizes text, reusing a cached file when possible. Returns null when
        /// the engine is disabled, unavailable or fails.
        /// </summary>
        Task<TtsAudio?> SynthesizeAsync(
            string cacheScope,
            string text,
            CancellationToken cancellationToken);
    }
}

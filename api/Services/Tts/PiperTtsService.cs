using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NewsAggregator.Services.Tts
{
    /// <summary>
    /// Synthesizes speech with a local Piper process and, when available, encodes
    /// the result to MP3 with FFmpeg. Audio is cached on disk by article segment.
    /// </summary>
    public sealed class PiperTtsService : ITextToSpeechService
    {
        private const string Mp3ContentType = "audio/mpeg";
        private const string WavContentType = "audio/wav";

        private readonly TtsOptions _options;
        private readonly IProcessRunner _runner;
        private readonly ILogger<PiperTtsService> _logger;
        private readonly SemaphoreSlim _gate;
        private readonly ConcurrentDictionary<string, Task<TtsAudio?>> _inFlight = new();

        public PiperTtsService(
            IOptions<TtsOptions> options,
            IProcessRunner runner,
            ILogger<PiperTtsService> logger)
        {
            _options = options.Value;
            _runner = runner;
            _logger = logger;
            _gate = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrent));
        }

        public bool IsEnabled => _options.Enabled && File.Exists(_options.ModelPath);

        public async Task<TtsAudio?> SynthesizeAsync(
            string cacheScope,
            string text,
            CancellationToken cancellationToken)
        {
            if (!IsEnabled || string.IsNullOrWhiteSpace(text) || !File.Exists(_options.ModelPath))
            {
                return null;
            }

            var clean = text.Trim();
            if (clean.Length > _options.MaxCharacters)
            {
                clean = clean[.._options.MaxCharacters].TrimEnd();
            }

            var key = TtsCacheKey.For(cacheScope, _options.ModelPath);
            var cached = await ReadCacheAsync(key, cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                return cached;
            }

            var task = _inFlight.GetOrAdd(key, _ => GenerateAsync(key, clean));
            _ = task.ContinueWith(
                completed => _inFlight.TryRemove(key, out var _),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<TtsAudio?> ReadCacheAsync(string key, CancellationToken cancellationToken)
        {
            var mp3 = Path.Combine(_options.CacheDirectory, $"{key}.mp3");
            if (File.Exists(mp3))
            {
                return new TtsAudio(
                    await File.ReadAllBytesAsync(mp3, cancellationToken).ConfigureAwait(false),
                    Mp3ContentType);
            }

            var wav = Path.Combine(_options.CacheDirectory, $"{key}.wav");
            if (File.Exists(wav))
            {
                return new TtsAudio(
                    await File.ReadAllBytesAsync(wav, cancellationToken).ConfigureAwait(false),
                    WavContentType);
            }

            return null;
        }

        private async Task<TtsAudio?> GenerateAsync(string key, string text)
        {
            await _gate.WaitAsync().ConfigureAwait(false);

            var wavPath = Path.Combine(_options.CacheDirectory, $"{key}.wav");
            var mp3Path = Path.Combine(_options.CacheDirectory, $"{key}.mp3");

            try
            {
                Directory.CreateDirectory(_options.CacheDirectory);

                var piper = await _runner.RunAsync(
                    _options.PiperPath,
                    new[]
                    {
                        "--model", _options.ModelPath,
                        "--output_file", wavPath
                    },
                    text,
                    CancellationToken.None).ConfigureAwait(false);

                if (piper.ExitCode != 0 || !File.Exists(wavPath))
                {
                    _logger.LogWarning(
                        "Piper no pudo generar audio (exit {ExitCode}): {Error}",
                        piper.ExitCode,
                        piper.StandardError);
                    Delete(wavPath);
                    return null;
                }

                if (_options.EnableMp3 &&
                    await TryEncodeMp3Async(wavPath, mp3Path).ConfigureAwait(false))
                {
                    Delete(wavPath);
                    return new TtsAudio(
                        await File.ReadAllBytesAsync(mp3Path).ConfigureAwait(false),
                        Mp3ContentType);
                }

                return new TtsAudio(
                    await File.ReadAllBytesAsync(wavPath).ConfigureAwait(false),
                    WavContentType);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "No se pudo sintetizar el audio local.");
                Delete(wavPath);
                Delete(mp3Path);
                return null;
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<bool> TryEncodeMp3Async(string wavPath, string mp3Path)
        {
            try
            {
                var ffmpeg = await _runner.RunAsync(
                    _options.FfmpegPath,
                    new[]
                    {
                        "-y",
                        "-hide_banner",
                        "-loglevel", "error",
                        "-i", wavPath,
                        "-codec:a", "libmp3lame",
                        "-b:a", "64k",
                        mp3Path
                    },
                    null,
                    CancellationToken.None).ConfigureAwait(false);

                if (ffmpeg.ExitCode == 0 && File.Exists(mp3Path))
                {
                    return true;
                }

                _logger.LogInformation(
                    "FFmpeg no pudo convertir a MP3 (exit {ExitCode}); se usa WAV.",
                    ffmpeg.ExitCode);
            }
            catch (Exception exception)
            {
                _logger.LogInformation(exception, "FFmpeg no está disponible; se usa WAV.");
            }

            Delete(mp3Path);
            return false;
        }

        private static void Delete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best-effort cleanup; the cached file, if any, remains usable.
            }
        }
    }
}

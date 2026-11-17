using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NewsAggregator.Services;
using NewsAggregator.Services.Tts;
using Xunit;

public class PiperTtsServiceTests
{
    private sealed class FakeProcessRunner : IProcessRunner
    {
        public List<(string FileName, IReadOnlyList<string> Arguments, string? Input)> Calls { get; } = new();

        public int PiperExitCode { get; set; }
        public int FfmpegExitCode { get; set; }
        public bool ThrowOnFfmpeg { get; set; }
        public bool ThrowOnPiper { get; set; }
        public TimeSpan Delay { get; set; } = TimeSpan.Zero;
        public byte[] WavBytes { get; set; } = { 1, 2, 3, 4 };
        public byte[] Mp3Bytes { get; set; } = { 9, 8, 7 };

        public async Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string? standardInput,
            CancellationToken cancellationToken)
        {
            Calls.Add((fileName, arguments, standardInput));

            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            if (fileName == "piper")
            {
                if (ThrowOnPiper)
                {
                    throw new InvalidOperationException("piper no está instalado");
                }

                if (PiperExitCode == 0)
                {
                    var output = arguments[arguments.Count - 1];
                    await File.WriteAllBytesAsync(output, WavBytes, cancellationToken);
                }

                return new ProcessResult(PiperExitCode, string.Empty);
            }

            if (ThrowOnFfmpeg)
            {
                throw new InvalidOperationException("ffmpeg no está instalado");
            }

            if (FfmpegExitCode == 0)
            {
                var output = arguments[arguments.Count - 1];
                await File.WriteAllBytesAsync(output, Mp3Bytes, cancellationToken);
            }

            return new ProcessResult(FfmpegExitCode, string.Empty);
        }
    }

    private static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "noticias-tts-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static (PiperTtsService Service, FakeProcessRunner Runner, TtsOptions Options, string Dir)
        CreateService(Action<TtsOptions>? configure = null, FakeProcessRunner? runner = null)
    {
        var dir = CreateTempDirectory();
        var model = Path.Combine(dir, "es_AR-daniela-high.onnx");
        File.WriteAllText(model, "model");

        var options = new TtsOptions
        {
            Enabled = true,
            PiperPath = "piper",
            FfmpegPath = "ffmpeg",
            ModelPath = model,
            CacheDirectory = dir,
            MaxCharacters = 4000
        };
        configure?.Invoke(options);

        runner ??= new FakeProcessRunner();
        var service = new PiperTtsService(
            Options.Create(options),
            runner,
            NullLogger<PiperTtsService>.Instance);

        return (service, runner, options, dir);
    }

    [Fact]
    public async Task Synthesize_WhenDisabled_ReturnsNullWithoutRunningTheEngine()
    {
        var (service, runner, _, _) = CreateService(options => options.Enabled = false);

        var audio = await service.SynthesizeAsync("url|title", "Hola", CancellationToken.None);

        Assert.Null(audio);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Synthesize_WhenModelIsMissing_ReturnsNullAndReportsDisabled()
    {
        var (service, runner, options, _) = CreateService();
        File.Delete(options.ModelPath);

        var audio = await service.SynthesizeAsync("url|title", "Hola", CancellationToken.None);

        Assert.False(service.IsEnabled);
        Assert.Null(audio);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Synthesize_WhenBlank_ReturnsNullWithoutRunningTheEngine()
    {
        var (service, runner, _, _) = CreateService();

        var audio = await service.SynthesizeAsync("url|title", "   ", CancellationToken.None);

        Assert.Null(audio);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Synthesize_GeneratesMp3AndCachesIt()
    {
        var (service, runner, _, dir) = CreateService();

        var first = await service.SynthesizeAsync("url|title", "Hola", CancellationToken.None);
        var second = await service.SynthesizeAsync("url|title", "Hola", CancellationToken.None);

        Assert.NotNull(first);
        Assert.Equal("audio/mpeg", first!.ContentType);
        Assert.Equal(new byte[] { 9, 8, 7 }, first.Bytes);
        Assert.Equal(new byte[] { 9, 8, 7 }, second!.Bytes);
        Assert.Equal(2, runner.Calls.Count);
        Assert.Single(Directory.GetFiles(dir, "*.mp3"));
    }

    [Fact]
    public async Task Synthesize_WhenFfmpegFails_FallsBackToWav()
    {
        var runner = new FakeProcessRunner { FfmpegExitCode = 1 };
        var (service, _, _, dir) = CreateService(runner: runner);

        var audio = await service.SynthesizeAsync("url|title", "Hola", CancellationToken.None);

        Assert.NotNull(audio);
        Assert.Equal("audio/wav", audio!.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, audio.Bytes);
        Assert.Single(Directory.GetFiles(dir, "*.wav"));
        Assert.Empty(Directory.GetFiles(dir, "*.mp3"));
    }

    [Fact]
    public async Task Synthesize_WhenFfmpegIsNotInstalled_FallsBackToWav()
    {
        var runner = new FakeProcessRunner { ThrowOnFfmpeg = true };
        var (service, _, _, _) = CreateService(runner: runner);

        var audio = await service.SynthesizeAsync("url|title", "Hola", CancellationToken.None);

        Assert.NotNull(audio);
        Assert.Equal("audio/wav", audio!.ContentType);
    }

    [Fact]
    public async Task Synthesize_WhenPiperIsNotInstalled_ReturnsNull()
    {
        var runner = new FakeProcessRunner { ThrowOnPiper = true };
        var (service, _, _, _) = CreateService(runner: runner);

        var audio = await service.SynthesizeAsync("url|title", "Hola", CancellationToken.None);

        Assert.Null(audio);
    }

    [Fact]
    public async Task Synthesize_WhenPiperFails_ReturnsNullAndCachesNothing()
    {
        var runner = new FakeProcessRunner { PiperExitCode = 1 };
        var (service, _, _, dir) = CreateService(runner: runner);

        var audio = await service.SynthesizeAsync("url|title", "Hola", CancellationToken.None);

        Assert.Null(audio);
        Assert.Empty(Directory.GetFiles(dir, "*.mp3"));
        Assert.Empty(Directory.GetFiles(dir, "*.wav"));
    }

    [Fact]
    public async Task Synthesize_DeduplicatesConcurrentRequests()
    {
        var runner = new FakeProcessRunner { Delay = TimeSpan.FromMilliseconds(30) };
        var (service, _, _, _) = CreateService(runner: runner);

        var results = await Task.WhenAll(
            service.SynthesizeAsync("url|title", "Hola", CancellationToken.None),
            service.SynthesizeAsync("url|title", "Hola", CancellationToken.None));

        Assert.NotNull(results[0]);
        Assert.NotNull(results[1]);
        Assert.Equal(2, runner.Calls.Count);
    }

    [Fact]
    public async Task Synthesize_TruncatesVeryLongText()
    {
        var (service, runner, options, _) = CreateService(options => options.MaxCharacters = 10);

        var audio = await service.SynthesizeAsync(
            "url|body|0",
            "Texto mucho más largo que el límite",
            CancellationToken.None);

        Assert.NotNull(audio);
        var piperCall = Assert.Single(runner.Calls, call => call.FileName == "piper");
        Assert.True(piperCall.Input!.Length <= options.MaxCharacters);
    }
}

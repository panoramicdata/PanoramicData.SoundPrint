using BenchmarkDotNet.Attributes;
using PanoramicData.SoundPrint.TestAudio;

namespace PanoramicData.SoundPrint.Benchmarks;

/// <summary>Discards landmarks so that allocation figures show only the fingerprinter.</summary>
internal sealed class NullSink : ILandmarkSink
{
	public long Count { get; private set; }

	public void Add(ReadOnlySpan<Landmark> landmarks) => Count += landmarks.Length;
}

/// <summary>
/// Extraction speed and allocation. 60 seconds of audio per operation, so the real-time factor is 60 s divided by the mean time.
/// The streaming benchmarks reuse one fingerprinter, as a live stream would, and must show no allocation.
/// </summary>
[MemoryDiagnoser]
public class ExtractionBenchmarks
{
	private float[] _audio8k = [];
	private float[] _audio44k = [];
	private Fingerprinter _streaming8k = null!;
	private Fingerprinter _streaming44k = null!;
	private readonly NullSink _sink = new();

	[GlobalSetup]
	public void Setup()
	{
		_audio8k = SyntheticSong.Generate(1, 60, 8000);
		_audio44k = SyntheticSong.Generate(1, 60, 44100);
		_streaming8k = new Fingerprinter(8000);
		_streaming44k = new Fingerprinter(44100);
		_streaming8k.Push(_audio8k, _sink);
		_streaming44k.Push(_audio44k, _sink);
	}

	/// <summary>60 s of 8 kHz audio through a warmed streaming fingerprinter.</summary>
	[Benchmark]
	public void Stream60Seconds8kHz() => _streaming8k.Push(_audio8k, _sink);

	/// <summary>60 s of 44.1 kHz audio (so it is resampled) through a warmed streaming fingerprinter.</summary>
	[Benchmark]
	public void Stream60Seconds44100Hz() => _streaming44k.Push(_audio44k, _sink);

	/// <summary>A one-shot fingerprint of 60 s at 8 kHz, including construction and a landmark list.</summary>
	[Benchmark]
	public int OneShot60Seconds8kHz() => Fingerprinter.Fingerprint(_audio8k, 8000).Count;
}

/// <summary>Match latency for a 10 second query against in-memory databases of increasing size.</summary>
[MemoryDiagnoser]
public class MatchBenchmarks
{
	private Corpus _corpus = null!;
	private IReadOnlyList<Landmark> _query = [];

	[Params(100, 1000, 5000)]
	public int Tracks { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		_corpus = Corpus.Build(Tracks, 50_000, 40, 8000, new FingerprintOptions());
		_query = Fingerprinter.Fingerprint(_corpus.Songs[Tracks / 2].AsSpan(8 * 8000, 10 * 8000), 8000);
	}

	[GlobalCleanup]
	public void Cleanup() => _corpus.Dispose();

	/// <summary>Look up and vote for one 10 s query.</summary>
	[Benchmark]
	public int Match10Seconds() => _corpus.Matcher.MatchAsync(_query).AsTask().GetAwaiter().GetResult().Count;
}

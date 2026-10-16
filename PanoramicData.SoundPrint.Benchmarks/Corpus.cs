using PanoramicData.SoundPrint.TestAudio;

namespace PanoramicData.SoundPrint.Benchmarks;

/// <summary>A set of synthetic songs held in memory, with the store and matcher built over them.</summary>
internal sealed class Corpus : IDisposable
{
	public required float[][] Songs { get; init; }

	public required int Rate { get; init; }

	public required double Seconds { get; init; }

	public required FingerprintOptions Options { get; init; }

	public required InMemoryFingerprintStore Store { get; init; }

	public required Matcher Matcher { get; init; }

	/// <summary>Generates songs with seeds <paramref name="seedBase"/>.. in parallel.</summary>
	public static float[][] GenerateSongs(int count, int seedBase, double seconds, int rate)
	{
		var songs = new float[count][];
		Parallel.For(0, count, i => songs[i] = SyntheticSong.Generate(seedBase + i, seconds, rate));
		return songs;
	}

	/// <summary>Generates songs, fingerprints each in parallel and adds them to an in-memory store as <c>song-N</c>.</summary>
	public static Corpus Build(int count, int seedBase, double seconds, int rate, FingerprintOptions options, MatchOptions? matchOptions = null)
	{
		return FromSongs(GenerateSongs(count, seedBase, seconds, rate), rate, seconds, options, matchOptions);
	}

	/// <summary>Fingerprints existing audio in parallel and adds it to an in-memory store as <c>song-N</c>.</summary>
	public static Corpus FromSongs(float[][] songs, int rate, double seconds, FingerprintOptions options, MatchOptions? matchOptions = null)
	{
		var landmarks = new IReadOnlyList<Landmark>[songs.Length];
		Parallel.For(0, songs.Length, i => landmarks[i] = Fingerprinter.Fingerprint(songs[i], rate, options));
		var store = new InMemoryFingerprintStore(options);
		for (var i = 0; i < songs.Length; i++)
		{
			store.AddTrack(TrackId(i), landmarks[i]);
		}

		return new Corpus
		{
			Songs = songs,
			Rate = rate,
			Seconds = seconds,
			Options = options,
			Store = store,
			Matcher = new Matcher(store, options, matchOptions ?? new MatchOptions())
		};
	}

	public static string TrackId(int index) => $"song-{index}";

	public void Dispose() => Store.Dispose();
}

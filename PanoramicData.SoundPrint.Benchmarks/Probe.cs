using PanoramicData.SoundPrint.TestAudio;

namespace PanoramicData.SoundPrint.Benchmarks;

/// <summary>Diagnostic: lists absent clean queries that nonetheless produce a strong match, to see why.</summary>
internal static class Probe
{
	public static int Run(Args args)
	{
		var songs = args.Int("songs", 60);
		var rate = 22050;
		using var corpus = Corpus.Build(songs, 1000, 60, rate, new FingerprintOptions(), new MatchOptions { MinConfidence = 0, MinVotes = 3 });
		var absent = Corpus.GenerateSongs(songs, 900_000, 60, rate);
		var rng = new Random(5);
		for (var t = 0; t < 400; t++)
		{
			var song = rng.Next(absent.Length);
			var start = rng.Next(0, (60 - 10) * rate);
			var landmarks = Fingerprinter.Fingerprint(absent[song].AsSpan(start, 10 * rate), rate);
			var matches = corpus.Matcher.MatchAsync(landmarks).AsTask().GetAwaiter().GetResult();
			if (matches.Count > 0 && matches[0].Score > args.Double("min-score", 20))
			{
				var m = matches[0];
				Console.WriteLine($"absent {900_000 + song} @ {(double)start / rate:F1}s -> {m.TrackId} score {m.Score:F1} votes {m.Votes} offset {m.OffsetInTrack.TotalSeconds:F2}s span {m.QueryStart.TotalSeconds:F1}-{m.QueryEnd.TotalSeconds:F1}s of {landmarks.Count} landmarks");
			}
		}

		return 0;
	}
}

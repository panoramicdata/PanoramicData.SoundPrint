using System.Diagnostics;
using System.Globalization;
using System.Text;
using PanoramicData.SoundPrint.TestAudio;

namespace PanoramicData.SoundPrint.Benchmarks;

/// <summary>What happened to the top match of one query.</summary>
internal sealed record Trial(bool Correct, bool PositionCorrect, double Score, int Votes, double MatchMilliseconds);

/// <summary>All trials of one scenario at one query length.</summary>
internal sealed record Cell(Scenario Scenario, double QuerySeconds, IReadOnlyList<Trial> Present, IReadOnlyList<Trial> Absent);

internal sealed record AccuracyConfig(int Songs, int Trials, int Rate, double SongSeconds, FingerprintOptions Options, IReadOnlyList<Scenario> Scenarios, IReadOnlyList<double> QueryLengths)
{
	/// <summary>Songs to put in the database; synthetic songs are generated when null.</summary>
	public float[][]? PresentSongs { get; init; }

	/// <summary>Songs that are not in the database, for false-positive measurement; synthetic when null.</summary>
	public float[][]? AbsentSongs { get; init; }

	/// <summary>Heading and description for the report.</summary>
	public string Title { get; init; } = "Accuracy results";

	public string Description { get; init; } = string.Empty;

	public string Command { get; init; } = "accuracy";
}

/// <summary>
/// Measures how reliably queries find their track. Songs are synthetic, a degradation is applied to the whole song (as a broadcast
/// chain would), clips are sliced from the degraded audio and matched against a database of the clean songs. "Absent" queries
/// are clips of songs that are not in the database and measure false positives.
/// </summary>
internal static class AccuracyRunner
{
	private const double PositionToleranceSeconds = 0.4;

	public static int Run(Args args)
	{
		var quick = args.Flag("quick");
		var config = new AccuracyConfig(
			args.Int("songs", quick ? 60 : 300),
			args.Int("trials", quick ? 60 : 300),
			args.Int("rate", 22050),
			args.Double("seconds", 60),
			new FingerprintOptions { SampleRate = args.Int("internal-rate", 8000) },
			[.. Scenario.Standard.Where(s => !s.NeedsFfmpeg || !args.Flag("no-ffmpeg"))],
			quick ? [5.0, 10.0] : [3.0, 5.0, 7.0, 10.0]);

		var started = Stopwatch.StartNew();
		var cells = Execute(config);
		Console.WriteLine($"Ran {cells.Count} cells in {started.Elapsed.TotalMinutes:F1} min");

		var fit = Calibration.Fit(cells.Where(c => c.QuerySeconds >= 5).SelectMany(c => c.Present.Concat(c.Absent)));
		var threshold = Calibration.RecommendedScoreThreshold(cells.Where(c => c.QuerySeconds >= 5), 0.001);
		var straddle = RunStraddle(config, args.Int("straddle-trials", quick ? 60 : 300));

		var report = Report(config, cells, fit, threshold, straddle);
		Console.WriteLine(report);
		if (args.String("out") is { } path)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
			File.WriteAllText(path, report);
			Console.WriteLine($"Wrote {path}");
		}

		return 0;
	}

	/// <summary>Builds the database and runs every scenario at every query length.</summary>
	public static List<Cell> Execute(AccuracyConfig config)
	{
		using var corpus = BuildCorpus(config);
		var absentSongs = config.AbsentSongs ?? Corpus.GenerateSongs(config.Songs, 900_000, config.SongSeconds, config.Rate);
		var cells = new List<Cell>();
		var scenarioIndex = 0;
		foreach (var scenario in config.Scenarios)
		{
			var count = Math.Min(config.Songs, scenario.MaxSongs);
			var present = Degrade(scenario, corpus.Songs, count, config.Rate, 11);
			var absent = Degrade(scenario, absentSongs, Math.Min(count, absentSongs.Length), config.Rate, 12);
			foreach (var length in config.QueryLengths)
			{
				var seed = (scenarioIndex * 100) + (int)length;
				cells.Add(new Cell(
					scenario,
					length,
					RunTrials(corpus, present, length, config, seed, expectTrack: true),
					RunTrials(corpus, absent, length, config, seed + 7, expectTrack: false)));
			}

			scenarioIndex++;
			Console.WriteLine($"  done: {scenario.Group} / {scenario.Name}");
		}

		return cells;
	}

	private static float[][] Degrade(Scenario scenario, float[][] songs, int count, int rate, int seedBase)
	{
		var result = new float[count][];
		Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = scenario.NeedsFfmpeg ? 8 : Environment.ProcessorCount }, i =>
			result[i] = scenario.Apply(songs[i], rate, seedBase + i));
		return result;
	}

	private static Trial[] RunTrials(Corpus corpus, float[][] source, double length, AccuracyConfig config, int seed, bool expectTrack)
	{
		var trials = new Trial[config.Trials];
		var clipSamples = (int)(length * config.Rate);
		Parallel.For(0, config.Trials, t =>
		{
			var rng = new Random(Seeds.Mix(seed, t));
			var song = rng.Next(source.Length);
			var startSample = rng.Next(0, source[song].Length - clipSamples);
			var clip = source[song].AsSpan(startSample, clipSamples);
			var landmarks = Fingerprinter.Fingerprint(clip, config.Rate, config.Options);
			var watch = Stopwatch.StartNew();
			var matches = corpus.Matcher.MatchAsync(landmarks).AsTask().GetAwaiter().GetResult();
			watch.Stop();
			var top = matches.Count > 0 ? matches[0] : null;
			// Identification is judged by track: loop-based music repeats exactly, so the right track can legitimately match at another
			// offset. Position accuracy is reported separately.
			var correct = expectTrack && top is not null && top.TrackId == Corpus.TrackId(song);
			var position = correct && Math.Abs(top!.OffsetInTrack.TotalSeconds - ((double)startSample / config.Rate)) <= PositionToleranceSeconds;
			trials[t] = new Trial(correct, position, top?.Score ?? 0, top?.Votes ?? 0, watch.Elapsed.TotalMilliseconds);
		});
		return trials;
	}

	private static Corpus BuildCorpus(AccuracyConfig config) =>
		config.PresentSongs is { } songs
			? Corpus.FromSongs(songs, config.Rate, config.SongSeconds, config.Options, new MatchOptions { MinConfidence = 0, MinVotes = 3 })
			: Corpus.Build(config.Songs, 1000, config.SongSeconds, config.Rate, config.Options, new MatchOptions { MinConfidence = 0, MinVotes = 3 });

	/// <summary>Clips straddling two songs: are both found, in order, and how close is the boundary?</summary>
	internal static StraddleResult RunStraddle(AccuracyConfig config, int trials)
	{
		using var corpus = BuildCorpus(config);
		var rows = new List<(string Name, int Both, int Total, List<double> BoundaryErrors)>();
		foreach (var (name, crossfade, radio) in new[] { ("hard cut", 0.0, false), ("1 s crossfade", 1.0, false), ("hard cut after radio processing", 0.0, true) })
		{
			var songs = radio ? Degrade(Scenario.Standard[5], corpus.Songs, config.Songs, config.Rate, 21) : corpus.Songs;
			var errors = new List<double>();
			var both = 0;
			var outcomes = new (bool Both, double Error)[trials];
			Parallel.For(0, trials, t =>
			{
				var rng = new Random(Seeds.Mix(77, t, name.Length));
				var a = rng.Next(config.Songs);
				var b = (a + 1 + rng.Next(config.Songs - 1)) % config.Songs;
				const double windowSeconds = 10;
				var boundary = 2.5 + (rng.NextDouble() * 5);
				var startA = rng.NextDouble() * (config.SongSeconds - boundary - 1);
				var startB = 1 + (rng.NextDouble() * (config.SongSeconds - (windowSeconds - boundary) - 2));
				var rate = config.Rate;
				var fade = (int)(crossfade * rate);
				var firstSamples = (int)(boundary * rate) + (fade / 2);
				var secondSamples = (int)(windowSeconds * rate) - (int)(boundary * rate) + (fade / 2);
				var spliced = TestAudio.Degradations.Splice(songs[a].AsSpan((int)(startA * rate)), songs[b].AsSpan((int)((startB * rate) - (fade / 2.0))), firstSamples, fade, secondSamples);
				var matches = corpus.Matcher.MatchAsync(Fingerprinter.Fingerprint(spliced, rate, config.Options)).AsTask().GetAwaiter().GetResult();
				var matchA = matches.FirstOrDefault(m => m.TrackId == Corpus.TrackId(a) && Math.Abs(m.OffsetInTrack.TotalSeconds - startA) <= PositionToleranceSeconds);
				var matchB = matches.FirstOrDefault(m => m.TrackId == Corpus.TrackId(b) && Math.Abs(m.OffsetInTrack.TotalSeconds - (startB - boundary)) <= PositionToleranceSeconds);
				outcomes[t] = matchA is not null && matchB is not null && matchA.QueryStart < matchB.QueryStart
					? (true, Math.Abs(((matchA.QueryEnd.TotalSeconds + matchB.QueryStart.TotalSeconds) / 2) - boundary))
					: (false, 0);
			});
			foreach (var (_, error) in outcomes.Where(o => o.Both))
			{
				both++;
				errors.Add(error);
			}

			rows.Add((name, both, trials, errors));
		}

		return new StraddleResult([.. rows.Select(r => new StraddleRow(r.Name, r.Both, r.Total, r.BoundaryErrors.Count == 0 ? double.NaN : Percentile(r.BoundaryErrors, 0.5), r.BoundaryErrors.Count == 0 ? double.NaN : Percentile(r.BoundaryErrors, 0.9)))]);
	}

	private static double Percentile(List<double> values, double p)
	{
		var sorted = values.Order().ToList();
		return sorted[Math.Min(sorted.Count - 1, (int)(p * sorted.Count))];
	}

	internal static string Report(AccuracyConfig config, List<Cell> cells, CalibrationFit fit, double scoreThreshold, StraddleResult straddle)
	{
		var sb = new StringBuilder();
		var confidenceThreshold = fit.Confidence(scoreThreshold);
		sb.AppendLine($"# {config.Title}");
		sb.AppendLine();
		sb.AppendLine($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC by `dotnet run -c Release --project PanoramicData.SoundPrint.Benchmarks -- {config.Command}`.");
		sb.AppendLine();
		sb.AppendLine(config.Description.Length > 0
			? config.Description
			: $"Database: {config.Songs} synthetic songs of {config.SongSeconds:F0} s ({config.Songs * config.SongSeconds / 3600:F1} hours), generated at {config.Rate} Hz and fingerprinted at {config.Options.SampleRate} Hz. " +
			$"{config.Trials} queries per cell with the answer in the database (present) and {config.Trials} without (absent). Synthetic songs never repeat exactly, which real music does, so the real-music benchmark is the better guide to absolute numbers.");
		sb.AppendLine();
		sb.AppendLine("## Confidence calibration and threshold");
		sb.AppendLine();
		sb.AppendLine($"Logistic fit of P(correct) against match score over all queries of 5 s or longer: midpoint {fit.Midpoint:F2}, slope {fit.Slope:F3} ({fit.Samples} queries).");
		sb.AppendLine($"Recommended threshold: score {scoreThreshold:F1}, which is confidence {confidenceThreshold:F4}. At that threshold the pooled false-match rate (absent queries that matched plus present queries matched to the wrong place, over all queries of 5 s or longer) is at most 0.1%.");
		sb.AppendLine();
		sb.AppendLine("## Results by scenario and query length");
		sb.AppendLine();
		sb.AppendLine("Each cell is `top-1 / recall at threshold / false positives at threshold`. Top-1 ignores the threshold (the best match is right); recall counts a query only when the right match also clears the threshold; false positives are absent queries that produced a match above the threshold.");
		sb.AppendLine();
		var lengths = config.QueryLengths;
		sb.AppendLine("| Group | Scenario | " + string.Join(" | ", lengths.Select(l => $"{l:F0} s")) + " |");
		sb.AppendLine("|---|---|" + string.Join("|", lengths.Select(_ => "---")) + "|");
		foreach (var scenario in config.Scenarios)
		{
			var row = new StringBuilder($"| {scenario.Group} | {scenario.Name} |");
			foreach (var length in lengths)
			{
				var cell = cells.First(c => c.Scenario == scenario && c.QuerySeconds == length);
				var top1 = 100.0 * cell.Present.Count(t => t.Correct) / cell.Present.Count;
				var recall = 100.0 * cell.Present.Count(t => t.Correct && t.Score >= scoreThreshold) / cell.Present.Count;
				var falsePositive = 100.0 * cell.Absent.Count(t => t.Score >= scoreThreshold) / cell.Absent.Count;
				row.Append($" {top1:F1}% / {recall:F1}% / {falsePositive:F1}% |");
			}

			sb.AppendLine(row.ToString());
		}

		var identified = cells.Where(c => c.QuerySeconds >= 5).SelectMany(c => c.Present).Where(t => t.Correct && t.Score >= scoreThreshold).ToList();
		sb.AppendLine();
		sb.AppendLine($"Position accuracy: of the queries of 5 s or longer whose track was identified above the threshold, {100.0 * identified.Count(t => t.PositionCorrect) / Math.Max(1, identified.Count):F1}% also had the matched position within {PositionToleranceSeconds:F1} s of the true one (the rest matched the right track at another offset, as happens with loop-based music that repeats exactly).");
		sb.AppendLine();
		sb.AppendLine("## Match latency by query length (this machine, in-memory store)");
		sb.AppendLine();
		sb.AppendLine("| Query length | Median ms | 95th percentile ms |");
		sb.AppendLine("|---|---|---|");
		foreach (var length in lengths)
		{
			var times = cells.Where(c => c.QuerySeconds == length).SelectMany(c => c.Present.Concat(c.Absent)).Select(t => t.MatchMilliseconds).Order().ToList();
			sb.AppendLine($"| {length:F0} s | {times[times.Count / 2]:F2} | {times[(int)(times.Count * 0.95)]:F2} |");
		}

		sb.AppendLine();
		sb.AppendLine("## Clips straddling two songs (10 s window, boundary 2.5 to 7.5 s in)");
		sb.AppendLine();
		sb.AppendLine("A trial succeeds when both songs are identified at the right positions, in the right order, with the matcher's default options and no threshold. Boundary error compares the midpoint between the first song's matched end and the second song's matched start with the true boundary.");
		sb.AppendLine();
		sb.AppendLine("| Join | Both found | Boundary error median | Boundary error 90th percentile |");
		sb.AppendLine("|---|---|---|---|");
		foreach (var row in straddle.Rows)
		{
			sb.AppendLine($"| {row.Name} | {100.0 * row.Both / row.Total:F1}% | {row.Median:F2} s | {row.P90:F2} s |");
		}

		return sb.ToString();
	}

	internal sealed record StraddleRow(string Name, int Both, int Total, double Median, double P90);

	internal sealed record StraddleResult(IReadOnlyList<StraddleRow> Rows);
}

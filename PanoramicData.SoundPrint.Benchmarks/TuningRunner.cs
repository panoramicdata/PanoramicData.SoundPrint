using System.Diagnostics;
using System.Text;
using PanoramicData.SoundPrint.TestAudio;

namespace PanoramicData.SoundPrint.Benchmarks;

/// <summary>
/// Compares extraction parameter choices on the hard scenarios (noise at 5 dB, radio processing, voice-over, and the full chain)
/// at 5 s query length, so each tuning decision has a measured effect.
/// </summary>
internal static class TuningRunner
{
	public static int Run(Args args)
	{
		var songs = args.Int("songs", 200);
		var trials = args.Int("trials", 200);
		var length = args.Double("length", 5);
		var baseline = new FingerprintOptions();
		var variants = new List<(string Name, FingerprintOptions Options)>
		{
			("baseline (defaults)", baseline),
			("fan-out 3", baseline with { FanOut = 3 }),
			("fan-out 8", baseline with { FanOut = 8 }),
			("peaks per band 4", baseline with { PeaksPerBandPerBlock = 4 }),
			("peaks per band 9", baseline with { PeaksPerBandPerBlock = 9 }),
			("neighbourhood 8 bins", baseline with { PeakNeighbourhoodBins = 8 }),
			("neighbourhood 18 bins", baseline with { PeakNeighbourhoodBins = 18 }),
			("neighbourhood 2 frames", baseline with { PeakNeighbourhoodFrames = 2 }),
			("neighbourhood 5 frames", baseline with { PeakNeighbourhoodFrames = 5 }),
			("hop 128", baseline with { HopSize = 128 }),
			("window 512, hop 128", baseline with { WindowSize = 512, HopSize = 128, BandEdges = [0, 4, 8, 16, 32, 64, 128, 257], PeakNeighbourhoodBins = 6 }),
			("internal rate 11025 Hz", baseline with { SampleRate = 11025 }),
			("target zone 40 frames", baseline with { MaxDeltaFrames = 40 })
		};

		if (args.String("only") is { } only)
		{
			var wanted = only.Split(',', StringSplitOptions.TrimEntries);
			variants = [.. variants.Where(v => wanted.Any(w => v.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))];
		}

		var rate = args.Int("rate", 22050);
		var sb = new StringBuilder();
		sb.AppendLine($"| Variant | Top-1 | Recall at 0.1% false matches | Landmarks per second | Extraction speed (x real time) |");
		sb.AppendLine("|---|---|---|---|---|");
		foreach (var (name, options) in variants)
		{
			var config = new AccuracyConfig(songs, trials, rate, 60, options, Scenario.Hard, [length]);
			var cells = AccuracyRunner.Execute(config);
			var all = cells.SelectMany(c => c.Present.Concat(c.Absent)).ToList();
			var top1 = 100.0 * cells.Sum(c => c.Present.Count(t => t.Correct)) / cells.Sum(c => c.Present.Count);
			var threshold = Calibration.RecommendedScoreThreshold(cells, 0.001);
			var recall = 100.0 * cells.Sum(c => c.Present.Count(t => t.Correct && t.Score >= threshold)) / cells.Sum(c => c.Present.Count);

			var sample = SyntheticSong.Generate(5, 60, rate);
			var watch = Stopwatch.StartNew();
			var landmarks = Fingerprinter.Fingerprint(sample, rate, options);
			var elapsed = watch.Elapsed.TotalSeconds;
			var row = $"| {name} | {top1:F1}% | {recall:F1}% | {landmarks.Count / 60.0:F0} | {60 / elapsed:F0} |";
			Console.WriteLine(row);
			sb.AppendLine(row);
		}

		Console.WriteLine();
		Console.WriteLine(sb.ToString());
		if (args.String("out") is { } path)
		{
			File.WriteAllText(path, sb.ToString());
		}

		return 0;
	}
}

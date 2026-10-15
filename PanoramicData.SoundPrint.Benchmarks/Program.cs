// Bootstrap entry point. "spike" runs the throwaway phase 3 round trip (see docs/SPIKE.md). The BenchmarkDotNet speed harness and the
// accuracy runner replace this in phase 6.
using System.Diagnostics;
using PanoramicData.SoundPrint.Benchmarks.Spike;

if (args.Length == 0 || args[0] != "spike")
{
	Console.WriteLine("PanoramicData.SoundPrint.Benchmarks: run with 'spike' for the phase 3 spike; no other benchmarks defined yet.");
	return 0;
}

const int tracks = 100;
const double songSeconds = 60;
var index = new SpikeIndex();
var sw = Stopwatch.StartNew();
var songs = new float[tracks][];
for (var i = 0; i < tracks; i++)
{
	songs[i] = SpikeSynth.Song(1000 + i, songSeconds);
}

Console.WriteLine($"Synthesised {tracks} songs of {songSeconds}s in {sw.ElapsedMilliseconds} ms");

sw.Restart();
for (var i = 0; i < tracks; i++)
{
	index.Add(i, SpikeFingerprinter.Fingerprint(songs[i]));
}

var fpMs = sw.Elapsed.TotalMilliseconds;
var audioSeconds = tracks * songSeconds;
Console.WriteLine($"Fingerprinted {audioSeconds:F0}s of audio in {fpMs:F0} ms = {audioSeconds * 1000 / fpMs:F0}x real time (single thread, unoptimised)");
Console.WriteLine($"Hashes: {index.HashCount} total, {index.HashCount / (audioSeconds / 3600):F0} per hour of audio, {index.HashCount / audioSeconds:F1} per second");

var rng = new Random(42);
foreach (var clipSeconds in new[] { 3.0, 5.0, 10.0 })
{
	foreach (var snr in new double?[] { null, 10, 0 })
	{
		const int trials = 200;
		int correct = 0, offsetOk = 0;
		var trueVotes = new List<int>();
		var matchMs = new List<double>();
		for (var q = 0; q < trials; q++)
		{
			var track = rng.Next(tracks);
			var startSample = rng.Next(0, (int)((songSeconds - clipSeconds) * SpikeSynth.Rate));
			var clip = songs[track].AsSpan(startSample, (int)(clipSeconds * SpikeSynth.Rate)).ToArray();
			if (snr is { } s) { clip = SpikeSynth.AddNoise(clip, s, q); }
			var lm = SpikeFingerprinter.Fingerprint(clip);
			sw.Restart();
			var (mTrack, mOffset, mVotes, _) = index.Match(lm);
			matchMs.Add(sw.Elapsed.TotalMilliseconds);
			trueVotes.Add(mVotes);
			if (mTrack == track)
			{
				correct++;
				var expected = (double)startSample / SpikeFingerprinter.Hop;
				if (Math.Abs(mOffset - expected) <= 2) { offsetOk++; }
			}
		}

		Console.WriteLine($"clip {clipSeconds,4}s snr {(snr is { } sv ? sv.ToString(System.Globalization.CultureInfo.InvariantCulture) : "clean"),5}: top1 {100.0 * correct / trials,5:F1}%  offset ok {100.0 * offsetOk / trials,5:F1}%  median votes {trueVotes.Order().ElementAt(trials / 2),4}  match ms (mean) {matchMs.Average():F2}");
	}
}

// False positives: clips from songs not in the index. Report the vote distribution against true-match votes to choose a threshold.
foreach (var clipSeconds in new[] { 5.0, 10.0 })
{
	var absent = new List<int>();
	for (var i = 0; i < 100; i++)
	{
		var other = SpikeSynth.Song(5000 + i, 30);
		var clip = other.AsSpan(0, (int)(clipSeconds * SpikeSynth.Rate)).ToArray();
		absent.Add(index.Match(SpikeFingerprinter.Fingerprint(clip)).Votes);
	}

	absent.Sort();
	Console.WriteLine($"absent clips {clipSeconds}s: votes max {absent[^1]}, p99 {absent[98]}, median {absent[50]}");
}

return 0;

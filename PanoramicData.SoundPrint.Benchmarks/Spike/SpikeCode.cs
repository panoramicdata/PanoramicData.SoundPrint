// THROWAWAY SPIKE CODE. Proves the landmark pipeline end to end on synthetic audio and produces the numbers in docs/SPIKE.md.
// It is deliberately offline (whole-array), allocation-heavy and untested. The real implementation (phase 4) is written test-first
// and streaming; none of this file is carried into the library.
namespace PanoramicData.SoundPrint.Benchmarks.Spike;

internal static class SpikeSynth
{
	public const int Rate = 8000;

	/// <summary>A deterministic synthetic "song": melody with harmonics, bass line and a hi-hat noise pattern.</summary>
	public static float[] Song(int seed, double seconds)
	{
		var rng = new Random(seed);
		var n = (int)(seconds * Rate);
		var x = new float[n];
		double[] scale = [0, 2, 4, 5, 7, 9, 11, 12, 14, 16];
		var root = 55.0 * Math.Pow(2, rng.Next(0, 12) / 12.0);
		var beat = 60.0 / (90 + rng.Next(0, 60));

		// Melody: random notes, one to four beats long, each with decaying harmonics.
		for (double t = 0; t < seconds;)
		{
			var len = beat * (1 + rng.Next(0, 4)) / 2;
			var f = root * 4 * Math.Pow(2, scale[rng.Next(scale.Length)] / 12.0);
			AddNote(x, t, len, f, 0.30, rng.Next(2, 6));
			t += len;
		}

		// Bass: one note per two beats.
		for (double t = 0; t < seconds; t += beat * 2)
		{
			AddNote(x, t, beat * 2, root * Math.Pow(2, scale[rng.Next(5)] / 12.0), 0.35, 2);
		}

		// Hi-hat: filtered-ish noise burst on each half beat.
		for (double t = 0; t < seconds; t += beat / 2)
		{
			var start = (int)(t * Rate);
			var count = Math.Min((int)(0.04 * Rate), n - start);
			var prev = 0f;
			for (var i = 0; i < count; i++)
			{
				var w = (float)rng.NextDouble() * 2 - 1;
				var hp = w - prev; // crude high-pass
				prev = w;
				x[start + i] += 0.08f * hp * (1 - i / (float)count);
			}
		}

		return x;
	}

	private static void AddNote(float[] x, double start, double length, double freq, double amp, int harmonics)
	{
		var s0 = (int)(start * Rate);
		var count = Math.Min((int)(length * Rate), x.Length - s0);
		for (var i = 0; i < count; i++)
		{
			var t = i / (double)Rate;
			var env = Math.Exp(-3 * t / length) * Math.Min(1, t * 200);
			double v = 0;
			for (var h = 1; h <= harmonics; h++)
			{
				if (freq * h < Rate / 2 - 100)
				{
					v += Math.Sin(2 * Math.PI * freq * h * t) / h;
				}
			}

			x[s0 + i] += (float)(amp * env * v);
		}
	}

	public static float[] AddNoise(float[] x, double snrDb, int seed)
	{
		var rng = new Random(seed);
		double p = 0;
		foreach (var v in x) { p += v * v; }
		p /= x.Length;
		var sigma = Math.Sqrt(p / Math.Pow(10, snrDb / 10));
		var y = new float[x.Length];
		for (var i = 0; i < x.Length; i++)
		{
			// Box-Muller
			var u1 = 1 - rng.NextDouble();
			var u2 = rng.NextDouble();
			y[i] = x[i] + (float)(sigma * Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2));
		}

		return y;
	}
}

internal sealed class SpikeFingerprinter
{
	public const int Window = 1024;
	public const int Hop = 256;
	public const int Bins = Window / 2 + 1;
	public const double FramesPerSecond = (double)SpikeSynth.Rate / Hop;
	private const int NeighbourF = 12, NeighbourT = 3, Fanout = 5, MaxDt = 63, MaxDf = 127, PeaksPerBandPerSecond = 6;
	private static readonly int[] BandEdges = [0, 8, 16, 32, 64, 128, 256, 513];

	public static (uint Hash, int Frame)[] Fingerprint(float[] x)
	{
		var spec = Spectrogram(x);
		var peaks = PickPeaks(spec);
		return Pair(peaks);
	}

	private static float[][] Spectrogram(float[] x)
	{
		var frames = Math.Max(0, (x.Length - Window) / Hop + 1);
		var win = new double[Window];
		for (var i = 0; i < Window; i++) { win[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / Window); }
		var spec = new float[frames][];
		var re = new double[Window];
		var im = new double[Window];
		for (var f = 0; f < frames; f++)
		{
			for (var i = 0; i < Window; i++) { re[i] = x[f * Hop + i] * win[i]; im[i] = 0; }
			Fft(re, im);
			var row = new float[Bins];
			for (var k = 0; k < Bins; k++) { row[k] = (float)(10 * Math.Log10(re[k] * re[k] + im[k] * im[k] + 1e-9)); }
			spec[f] = row;
		}

		return spec;
	}

	private static void Fft(double[] re, double[] im)
	{
		var n = re.Length;
		for (int i = 1, j = 0; i < n; i++)
		{
			var bit = n >> 1;
			for (; (j & bit) != 0; bit >>= 1) { j ^= bit; }
			j ^= bit;
			if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
		}

		for (var len = 2; len <= n; len <<= 1)
		{
			var ang = -2 * Math.PI / len;
			double wr = Math.Cos(ang), wi = Math.Sin(ang);
			for (var i = 0; i < n; i += len)
			{
				double cr = 1, ci = 0;
				for (var k = 0; k < len / 2; k++)
				{
					var ur = re[i + k]; var ui = im[i + k];
					var vr = re[i + k + len / 2] * cr - im[i + k + len / 2] * ci;
					var vi = re[i + k + len / 2] * ci + im[i + k + len / 2] * cr;
					re[i + k] = ur + vr; im[i + k] = ui + vi;
					re[i + k + len / 2] = ur - vr; im[i + k + len / 2] = ui - vi;
					(cr, ci) = (cr * wr - ci * wi, cr * wi + ci * wr);
				}
			}
		}
	}

	private static List<(int F, int T, float Mag)> PickPeaks(float[][] spec)
	{
		var candidates = new List<(int F, int T, float Mag)>();
		for (var t = 0; t < spec.Length; t++)
		{
			for (var f = 2; f < Bins - 1; f++)
			{
				var v = spec[t][f];
				var isMax = true;
				for (var dt = -NeighbourT; dt <= NeighbourT && isMax; dt++)
				{
					var tt = t + dt;
					if (tt < 0 || tt >= spec.Length) { continue; }
					for (var df = -NeighbourF; df <= NeighbourF; df++)
					{
						var ff = f + df;
						if (ff < 0 || ff >= Bins || (dt == 0 && df == 0)) { continue; }
						if (spec[tt][ff] > v || (spec[tt][ff] == v && (dt < 0 || (dt == 0 && df < 0)))) { isMax = false; break; }
					}
				}

				if (isMax) { candidates.Add((f, t, v)); }
			}
		}

		// Band balancing and density cap: per one-second block, per band, keep the strongest few.
		var blockFrames = (int)FramesPerSecond;
		var kept = new List<(int F, int T, float Mag)>();
		foreach (var group in candidates.GroupBy(c => (c.T / blockFrames, Band(c.F))))
		{
			kept.AddRange(group.OrderByDescending(c => c.Mag).Take(PeaksPerBandPerSecond));
		}

		return [.. kept.OrderBy(c => c.T).ThenBy(c => c.F)];
	}

	private static int Band(int f)
	{
		for (var b = 0; b < BandEdges.Length - 1; b++) { if (f < BandEdges[b + 1]) { return b; } }
		return BandEdges.Length - 2;
	}

	private static (uint, int)[] Pair(List<(int F, int T, float Mag)> peaks)
	{
		var result = new List<(uint, int)>();
		for (var i = 0; i < peaks.Count; i++)
		{
			var taken = 0;
			for (var j = i + 1; j < peaks.Count && taken < Fanout; j++)
			{
				var dt = peaks[j].T - peaks[i].T;
				if (dt > MaxDt) { break; }
				var df = peaks[j].F - peaks[i].F;
				if (dt < 1 || df < -128 || df > MaxDf) { continue; }
				var hash = ((uint)peaks[i].F << 14) | ((uint)(df + 128) << 6) | (uint)dt;
				result.Add((hash, peaks[i].T));
				taken++;
			}
		}

		return [.. result];
	}
}

internal sealed class SpikeIndex
{
	private readonly Dictionary<uint, List<(int Track, int Frame)>> _map = [];
	public int HashCount { get; private set; }

	public void Add(int track, (uint Hash, int Frame)[] landmarks)
	{
		foreach (var (h, t) in landmarks)
		{
			if (!_map.TryGetValue(h, out var list)) { _map[h] = list = []; }
			list.Add((track, t));
			HashCount++;
		}
	}

	/// <summary>Returns best track, offset (db frame minus query frame), vote count, and runner-up vote count.</summary>
	public (int Track, int Offset, int Votes, int RunnerUp) Match((uint Hash, int Frame)[] query)
	{
		var votes = new Dictionary<(int, int), int>();
		foreach (var (h, tq) in query)
		{
			if (!_map.TryGetValue(h, out var list)) { continue; }
			foreach (var (track, td) in list)
			{
				var key = (track, td - tq);
				votes[key] = votes.GetValueOrDefault(key) + 1;
			}
		}

		if (votes.Count == 0) { return (-1, 0, 0, 0); }
		var ordered = votes.OrderByDescending(kv => kv.Value).Take(50).ToList();
		var best = ordered[0];
		var runnerUp = ordered.Where(kv => kv.Key.Item1 != best.Key.Item1).Select(kv => kv.Value).DefaultIfEmpty(0).Max();
		return (best.Key.Item1, best.Key.Item2, best.Value, runnerUp);
	}
}

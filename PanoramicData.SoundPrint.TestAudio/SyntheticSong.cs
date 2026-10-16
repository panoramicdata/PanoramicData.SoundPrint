namespace PanoramicData.SoundPrint.TestAudio;

/// <summary>
/// Deterministic synthetic "songs" for tests and benchmarks: a chord pad, a melody, a bass line and drums, with tempo, key, scale,
/// melody and timbre all derived from the seed so different seeds give distinct, repeatable music. No real recordings are used.
/// </summary>
public static class SyntheticSong
{
	private static readonly double[][] Scales =
	[
		[0, 2, 4, 5, 7, 9, 11],   // major
		[0, 2, 3, 5, 7, 8, 10],   // natural minor
		[0, 2, 4, 7, 9],          // major pentatonic
		[0, 3, 5, 7, 10],         // minor pentatonic
		[0, 2, 3, 5, 7, 9, 10]    // dorian
	];

	/// <summary>Generates a song.</summary>
	/// <param name="seed">Selects tempo, key, scale, melody, drum pattern and timbre.</param>
	/// <param name="seconds">Length of the song.</param>
	/// <param name="sampleRate">Output sample rate in hertz.</param>
	/// <returns>Mono samples peaking at about 0.9.</returns>
	public static float[] Generate(int seed, double seconds, int sampleRate = 8000)
	{
		var rng = new Random(seed);
		var n = (int)(seconds * sampleRate);
		var mix = new double[n];

		var bpm = 78 + rng.Next(0, 70);
		var beat = 60.0 / bpm;
		var scale = Scales[rng.Next(Scales.Length)];
		var rootMidi = 36 + rng.Next(0, 12);
		var brightness = 0.35 + (rng.NextDouble() * 0.5);
		var vibratoHz = 4.5 + (rng.NextDouble() * 2);
		var progression = new int[4];
		for (var i = 0; i < progression.Length; i++)
		{
			progression[i] = rng.Next(scale.Length);
		}

		var melodyDegrees = new int[16];
		var melodyBeats = new double[16];
		for (var i = 0; i < melodyDegrees.Length; i++)
		{
			melodyDegrees[i] = rng.Next(scale.Length * 2);
			melodyBeats[i] = rng.Next(0, 3) switch { 0 => 0.5, 1 => 1.0, _ => 1.5 };
		}

		var drumKick = rng.Next(0, 3);

		// Chord pad: one chord per bar, notes are scale degrees root, third and fifth above the progression degree.
		var bar = 4 * beat;
		for (double t = 0, bars = 0; t < seconds; t += bar, bars++)
		{
			var degree = progression[(int)bars % progression.Length];
			foreach (var step in new[] { 0, 2, 4 })
			{
				var midi = DegreeToMidi(scale, rootMidi + 24, degree + step);
				AddTone(mix, sampleRate, t, bar, Frequency(midi), 0.10, 3, brightness * 0.5, 0, 0.3);
			}
		}

		// Bass: root of the chord on every beat, with a seeded rhythm.
		for (double t = 0, beats = 0; t < seconds; t += beat, beats++)
		{
			var degree = progression[((int)beats / 4) % progression.Length];
			if ((((int)beats + drumKick) % 4) is 0 or 2)
			{
				AddTone(mix, sampleRate, t, beat * 1.6, Frequency(DegreeToMidi(scale, rootMidi, degree)), 0.22, 3, 0.5, 0, 6);
			}
		}

		// Melody: a 16-note phrase that repeats with a seeded variation every second pass.
		var cursor = 0.0;
		var note = 0;
		var pass = 0;
		while (cursor < seconds)
		{
			var degree = melodyDegrees[note % melodyDegrees.Length];
			if (pass % 2 == 1 && note % 5 == 2)
			{
				degree += 1;
			}

			var length = melodyBeats[note % melodyBeats.Length] * beat;
			AddTone(mix, sampleRate, cursor, length * 1.1, Frequency(DegreeToMidi(scale, rootMidi + 36, degree)), 0.20, 6, brightness, vibratoHz, 5);
			cursor += length;
			note++;
			if (note % melodyDegrees.Length == 0)
			{
				pass++;
			}
		}

		// Drums.
		var noise = new Random(seed ^ 0x5bd1e995);
		for (double t = 0, beats = 0; t < seconds; t += beat, beats++)
		{
			var b = (int)beats % 4;
			if (b == 0 || (b == 2 && drumKick != 0))
			{
				AddKick(mix, sampleRate, t);
			}

			if (b is 1 or 3)
			{
				AddNoiseBurst(mix, sampleRate, t, 0.12, 0.20, noise);
			}

			AddNoiseBurst(mix, sampleRate, t, 0.04, 0.07, noise);
			AddNoiseBurst(mix, sampleRate, t + (beat / 2), 0.04, 0.05, noise);
		}

		var peak = 0.0;
		foreach (var v in mix)
		{
			peak = Math.Max(peak, Math.Abs(v));
		}

		var scaleFactor = peak > 0 ? 0.9 / peak : 1;
		var result = new float[n];
		for (var i = 0; i < n; i++)
		{
			result[i] = (float)(mix[i] * scaleFactor);
		}

		return result;
	}

	private static double DegreeToMidi(double[] scale, int baseMidi, int degree)
	{
		var octave = Math.DivRem(degree, scale.Length, out var index);
		return baseMidi + scale[index] + (12 * octave);
	}

	private static double Frequency(double midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

	private static void AddTone(double[] mix, int rate, double start, double length, double frequency, double amplitude, int harmonics, double rolloff, double vibratoHz, double decay)
	{
		var first = (int)(start * rate);
		var count = Math.Min((int)(length * rate), mix.Length - first);
		for (var i = 0; i < count; i++)
		{
			var t = (double)i / rate;
			var attack = Math.Min(1, t * 120);
			var release = Math.Min(1, Math.Max(0, (length - t) * 40));
			var envelope = Math.Exp(-decay * t / length) * attack * release;
			var f = frequency * (1 + (vibratoHz > 0 ? 0.004 * Math.Sin(2 * Math.PI * vibratoHz * t) : 0));
			double v = 0;
			for (var h = 1; h <= harmonics; h++)
			{
				if (f * h < (rate / 2) - 200)
				{
					v += Math.Sin(2 * Math.PI * f * h * t) * Math.Pow(rolloff, h - 1);
				}
			}

			mix[first + i] += amplitude * envelope * v;
		}
	}

	private static void AddKick(double[] mix, int rate, double start)
	{
		var first = (int)(start * rate);
		var count = Math.Min((int)(0.18 * rate), mix.Length - first);
		double phase = 0;
		for (var i = 0; i < count; i++)
		{
			var t = (double)i / rate;
			var f = 50 + (110 * Math.Exp(-t * 28));
			phase += 2 * Math.PI * f / rate;
			mix[first + i] += 0.5 * Math.Exp(-t * 14) * Math.Sin(phase);
		}
	}

	private static void AddNoiseBurst(double[] mix, int rate, double start, double seconds, double amplitude, Random rng)
	{
		var first = (int)(start * rate);
		if (first >= mix.Length)
		{
			return;
		}

		var count = Math.Min((int)(seconds * rate), mix.Length - first);
		var previous = 0.0;
		for (var i = 0; i < count; i++)
		{
			var w = (rng.NextDouble() * 2) - 1;
			var value = w - previous;
			previous = w;
			mix[first + i] += amplitude * value * Math.Exp(-6.0 * i / count);
		}
	}
}

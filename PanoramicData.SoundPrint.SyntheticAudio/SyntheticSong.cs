namespace PanoramicData.SoundPrint.SyntheticAudio;

/// <summary>
/// Deterministic synthetic "songs" for tests and benchmarks: a chord pad, a melody, a bass line and drums, with tempo, key, scale,
/// harmony, melody and timbre all derived from the seed so different seeds give distinct, repeatable music. Nothing repeats exactly
/// (chords, voicings, bass figures, drum patterns and melody all keep changing), so a clip has one true position in its song. No real
/// recordings are used.
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
	/// <param name="seed">Selects tempo, key, scale, harmony, melody, drum patterns and timbre.</param>
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
		var bar = 4 * beat;

		// Each song gets its own tuning, kick drum and starting offset, so two songs that happen to share a tempo and key still do not
		// line up sound for sound.
		var tuning = Math.Pow(2, rng.Next(-45, 46) / 1200.0);
		var kickBase = 42 + (rng.NextDouble() * 24);
		var lead = rng.NextDouble() * bar;

		// Harmony, bass and drums are chosen afresh every bar.
		var bars = (int)Math.Ceiling((seconds - lead) / bar);
		for (var b = 0; b < bars; b++)
		{
			var barRng = new Random(Seeds.Mix(seed, b, 17));
			var t = lead + (b * bar);
			var degree = barRng.Next(scale.Length);
			var inversion = barRng.Next(0, 3);
			var step = 0;
			foreach (var chordStep in new[] { 0, 2, 4 })
			{
				var lifted = step++ < inversion ? 7 : 0;
				AddTone(mix, sampleRate, t, bar, Frequency(tuning, DegreeToMidi(scale, rootMidi + 24, degree + chordStep + lifted)), 0.10, 3, brightness * 0.5, 0, 0.3);
			}

			for (var q = 0; q < 4; q++)
			{
				if (barRng.NextDouble() < 0.65)
				{
					var bassDegree = barRng.NextDouble() < 0.7 ? degree : degree + 4;
					AddTone(mix, sampleRate, t + (q * beat), beat * 1.6, Frequency(tuning, DegreeToMidi(scale, rootMidi, bassDegree)), 0.22, 3, 0.5, 0, 6);
				}

				if (barRng.NextDouble() < (q is 0 or 2 ? 0.8 : 0.25))
				{
					AddKick(mix, sampleRate, t + (q * beat), kickBase);
				}

				if (q is 1 or 3 && barRng.NextDouble() < 0.85)
				{
					AddNoiseBurst(mix, sampleRate, t + (q * beat), 0.12, 0.20, barRng);
				}

				AddNoiseBurst(mix, sampleRate, t + (q * beat), 0.04, 0.07, barRng);
				if (barRng.NextDouble() < 0.7)
				{
					AddNoiseBurst(mix, sampleRate, t + (q * beat) + (beat / 2), 0.04, 0.05, barRng);
				}
			}
		}

		// Melody: a continuous random walk through the scale, never looping.
		var melodyRng = new Random(seed ^ 0x2545F491);
		var cursor = lead;
		var degreeNow = scale.Length;
		while (cursor < seconds)
		{
			degreeNow = Math.Clamp(degreeNow + melodyRng.Next(-3, 4), 0, (scale.Length * 2) + 2);
			var length = melodyRng.Next(0, 3) switch { 0 => 0.5, 1 => 1.0, _ => 1.5 } * beat;
			if (melodyRng.NextDouble() < 0.85)
			{
				AddTone(mix, sampleRate, cursor, length * 1.1, Frequency(tuning, DegreeToMidi(scale, rootMidi + 36, degreeNow)), 0.20, 6, brightness, vibratoHz, 5);
			}

			cursor += length;
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

	private static double Frequency(double tuning, double midi) => 440.0 * tuning * Math.Pow(2, (midi - 69) / 12.0);

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

	private static void AddKick(double[] mix, int rate, double start, double baseHz)
	{
		var first = (int)(start * rate);
		if (first >= mix.Length)
		{
			return;
		}

		var count = Math.Min((int)(0.18 * rate), mix.Length - first);
		double phase = 0;
		for (var i = 0; i < count; i++)
		{
			var t = (double)i / rate;
			var f = baseHz + (110 * Math.Exp(-t * 28));
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

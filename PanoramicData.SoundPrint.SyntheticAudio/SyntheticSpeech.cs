namespace PanoramicData.SoundPrint.SyntheticAudio;

/// <summary>
/// A synthetic stand-in for a presenter's voice: a glottal pulse train with a wandering pitch, shaped by three formant resonators,
/// in syllable-length bursts separated by pauses. It has speech-like spectral energy and modulation without being real speech.
/// </summary>
public static class SyntheticSpeech
{
	/// <summary>Generates speech-like audio.</summary>
	public static float[] Generate(int seed, double seconds, int sampleRate)
	{
		var rng = new Random(seed);
		var n = (int)(seconds * sampleRate);
		var voiced = new double[n];
		var envelope = new double[n];

		var pitch = 110 + (rng.NextDouble() * 70);
		double phase = 0;
		var t = 0.0;
		while (t < seconds)
		{
			var syllable = 0.12 + (rng.NextDouble() * 0.18);
			var pause = rng.NextDouble() < 0.25 ? 0.2 + (rng.NextDouble() * 0.4) : 0.02 + (rng.NextDouble() * 0.06);
			var contour = (rng.NextDouble() - 0.5) * 40;
			var start = (int)(t * sampleRate);
			var count = Math.Min((int)(syllable * sampleRate), n - start);
			for (var i = 0; i < count; i++)
			{
				var u = (double)i / count;
				var f0 = pitch + (contour * u);
				phase += f0 / sampleRate;
				phase -= Math.Floor(phase);
				// Glottal-like pulse: a falling sawtooth is rich in harmonics.
				voiced[start + i] = 1 - (2 * phase);
				envelope[start + i] = Math.Sin(Math.PI * u);
			}

			t += syllable + pause;
		}

		var formants = new[]
		{
			(F: 500 + (rng.NextDouble() * 300), Q: 6.0, Gain: 1.0),
			(F: 1200 + (rng.NextDouble() * 500), Q: 8.0, Gain: 0.6),
			(F: 2500 + (rng.NextDouble() * 400), Q: 10.0, Gain: 0.3)
		};
		var shaped = new double[n];
		foreach (var (f, q, gain) in formants)
		{
			var filter = Biquad.Peaking(Math.Min(f, sampleRate * 0.45), 18, q, sampleRate);
			var input = new float[n];
			for (var i = 0; i < n; i++)
			{
				input[i] = (float)voiced[i];
			}

			var output = filter.Process(input);
			for (var i = 0; i < n; i++)
			{
				shaped[i] += gain * output[i];
			}
		}

		var result = new float[n];
		var peak = 1e-9;
		for (var i = 0; i < n; i++)
		{
			result[i] = (float)(shaped[i] * envelope[i]);
			peak = Math.Max(peak, Math.Abs(result[i]));
		}

		for (var i = 0; i < n; i++)
		{
			result[i] = (float)(result[i] / peak * 0.8);
		}

		return result;
	}
}

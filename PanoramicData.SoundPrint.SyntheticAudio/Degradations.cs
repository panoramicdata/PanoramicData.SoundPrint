namespace PanoramicData.SoundPrint.SyntheticAudio;

/// <summary>Signal degradations typical of broadcast chains, used to measure how robust matching is.</summary>
public static class Degradations
{
	/// <summary>Adds white Gaussian noise at a signal-to-noise ratio in decibels.</summary>
	public static float[] AddWhiteNoise(ReadOnlySpan<float> signal, double snrDb, int seed)
	{
		var rng = new Random(seed);
		var sigma = Math.Sqrt(MeanPower(signal) / Math.Pow(10, snrDb / 10));
		var result = new float[signal.Length];
		for (var i = 0; i < result.Length; i++)
		{
			var u1 = 1 - rng.NextDouble();
			var u2 = rng.NextDouble();
			var gaussian = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
			result[i] = signal[i] + (float)(sigma * gaussian);
		}

		return result;
	}

	/// <summary>Scales a signal by a gain in decibels.</summary>
	public static float[] Gain(ReadOnlySpan<float> signal, double gainDb)
	{
		var factor = (float)Math.Pow(10, gainDb / 20);
		var result = new float[signal.Length];
		for (var i = 0; i < result.Length; i++)
		{
			result[i] = signal[i] * factor;
		}

		return result;
	}

	/// <summary>
	/// Typical radio processing: rumble filter, a low-frequency lift, a presence lift, a top cut, then multiband-style compression
	/// approximated by a single fast compressor and a hard limiter.
	/// </summary>
	public static float[] RadioProcessing(ReadOnlySpan<float> signal, int sampleRate)
	{
		var x = signal.ToArray();
		x = Biquad.HighPass(60, 0.707, sampleRate).Process(x);
		x = Biquad.LowShelf(120, 4, sampleRate).Process(x);
		x = Biquad.Peaking(3200, 3, 1.0, sampleRate).Process(x);
		x = Biquad.LowPass(Math.Min(15000, sampleRate * 0.45), 0.707, sampleRate).Process(x);
		x = Compress(x, sampleRate, thresholdDb: -24, ratio: 4, attackMs: 5, releaseMs: 120, makeupDb: 8);
		for (var i = 0; i < x.Length; i++)
		{
			x[i] = Math.Clamp(x[i], -0.95f, 0.95f);
		}

		return x;
	}

	/// <summary>Applies a peak-detecting dynamic range compressor.</summary>
	public static float[] Compress(ReadOnlySpan<float> signal, int sampleRate, double thresholdDb, double ratio, double attackMs, double releaseMs, double makeupDb)
	{
		var attack = Math.Exp(-1.0 / (attackMs * 0.001 * sampleRate));
		var release = Math.Exp(-1.0 / (releaseMs * 0.001 * sampleRate));
		var makeup = Math.Pow(10, makeupDb / 20);
		var result = new float[signal.Length];
		var envelope = 0.0;
		for (var i = 0; i < signal.Length; i++)
		{
			var level = Math.Abs(signal[i]);
			envelope = level > envelope ? (attack * envelope) + ((1 - attack) * level) : (release * envelope) + ((1 - release) * level);
			var db = 20 * Math.Log10(Math.Max(envelope, 1e-9));
			var gainDb = db > thresholdDb ? (thresholdDb - db) * (1 - (1 / ratio)) : 0;
			result[i] = (float)(signal[i] * Math.Pow(10, gainDb / 20) * makeup);
		}

		return result;
	}

	/// <summary>
	/// Mixes a synthetic speech-like voice over music. <paramref name="voiceToMusicDb"/> is the voice level relative to the music, so
	/// 0 means equal RMS and -10 means the voice is 10 dB quieter than the music.
	/// </summary>
	public static float[] MixVoiceOver(ReadOnlySpan<float> music, int sampleRate, double voiceToMusicDb, int seed)
	{
		var voice = SyntheticSpeech.Generate(seed, (double)music.Length / sampleRate, sampleRate);
		var voicePower = MeanPower(voice);
		var target = MeanPower(music) * Math.Pow(10, voiceToMusicDb / 10);
		var scale = voicePower > 0 ? (float)Math.Sqrt(target / voicePower) : 0f;
		var result = new float[music.Length];
		for (var i = 0; i < result.Length; i++)
		{
			result[i] = music[i] + (voice[i] * scale);
		}

		return result;
	}

	/// <summary>Joins the end of one signal to the start of another with a linear crossfade, as a DJ or automation system would.</summary>
	/// <param name="first">The outgoing signal.</param>
	/// <param name="second">The incoming signal.</param>
	/// <param name="firstSamples">How many samples of the first signal to keep.</param>
	/// <param name="crossfadeSamples">Length of the overlap. Zero gives a hard cut.</param>
	/// <param name="secondSamples">How many samples of the second signal to keep.</param>
	public static float[] Splice(ReadOnlySpan<float> first, ReadOnlySpan<float> second, int firstSamples, int crossfadeSamples, int secondSamples)
	{
		var result = new float[firstSamples + secondSamples - crossfadeSamples];
		first[..firstSamples].CopyTo(result);
		var join = firstSamples - crossfadeSamples;
		for (var i = 0; i < secondSamples; i++)
		{
			var weight = crossfadeSamples > 0 && i < crossfadeSamples ? (float)i / crossfadeSamples : 1f;
			result[join + i] = (result[join + i] * (1 - weight)) + (second[i] * weight);
		}

		return result;
	}

	/// <summary>Mean power (mean square) of a signal.</summary>
	public static double MeanPower(ReadOnlySpan<float> signal)
	{
		double sum = 0;
		foreach (var v in signal)
		{
			sum += (double)v * v;
		}

		return signal.Length == 0 ? 0 : sum / signal.Length;
	}
}

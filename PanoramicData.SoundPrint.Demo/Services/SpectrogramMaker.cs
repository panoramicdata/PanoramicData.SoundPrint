namespace PanoramicData.SoundPrint.Demo.Services;

/// <summary>
/// Computes a short-time Fourier power spectrogram with the same window, hop and timeline as the fingerprinter, so the picture
/// behind the peaks is the actual data the peaks were picked from.
/// </summary>
public static class SpectrogramMaker
{
	/// <summary>Result of <see cref="Compute"/>.</summary>
	/// <param name="Frames">Number of time frames.</param>
	/// <param name="Bins">Number of frequency bins per frame (window size / 2 + 1).</param>
	/// <param name="Image">Frame-major bytes, 0 (quiet) to 255 (loud), scaled over an 80 dB range below the loudest cell.</param>
	/// <param name="MaxDecibels">The loudest cell in decibels, which maps to 255.</param>
	public sealed record Spectrogram(int Frames, int Bins, byte[] Image, double MaxDecibels);

	/// <summary>Computes the spectrogram of mono samples using a Hann window of <paramref name="window"/> samples every <paramref name="hop"/>.</summary>
	/// <param name="samples">Mono samples.</param>
	/// <param name="window">Window length in samples; a power of two.</param>
	/// <param name="hop">Samples between window starts.</param>
	/// <returns>The spectrogram.</returns>
	public static Spectrogram Compute(ReadOnlySpan<float> samples, int window, int hop)
	{
		var frames = Math.Max(0, ((samples.Length - window) / hop) + 1);
		var bins = (window / 2) + 1;
		var power = new double[frames * bins];
		var hann = new double[window];
		for (var i = 0; i < window; i++)
		{
			hann[i] = 0.5 - (0.5 * Math.Cos(2 * Math.PI * i / window));
		}

		var re = new double[window];
		var im = new double[window];
		var maxDb = double.NegativeInfinity;
		for (var f = 0; f < frames; f++)
		{
			for (var i = 0; i < window; i++)
			{
				re[i] = samples[(f * hop) + i] * hann[i];
				im[i] = 0;
			}

			Transform(re, im);
			for (var k = 0; k < bins; k++)
			{
				var db = 10 * Math.Log10((re[k] * re[k]) + (im[k] * im[k]) + 1e-12);
				power[(f * bins) + k] = db;
				maxDb = Math.Max(maxDb, db);
			}
		}

		var image = new byte[power.Length];
		for (var i = 0; i < power.Length; i++)
		{
			image[i] = (byte)Math.Clamp((power[i] - (maxDb - 80)) / 80 * 255, 0, 255);
		}

		return new Spectrogram(frames, bins, image, maxDb);
	}

	/// <summary>In-place radix-2 FFT.</summary>
	private static void Transform(double[] re, double[] im)
	{
		var n = re.Length;
		for (int i = 1, j = 0; i < n; i++)
		{
			var bit = n >> 1;
			for (; (j & bit) != 0; bit >>= 1)
			{
				j ^= bit;
			}

			j ^= bit;
			if (i < j)
			{
				(re[i], re[j]) = (re[j], re[i]);
				(im[i], im[j]) = (im[j], im[i]);
			}
		}

		for (var length = 2; length <= n; length <<= 1)
		{
			var angle = -2 * Math.PI / length;
			double wr = Math.Cos(angle), wi = Math.Sin(angle);
			for (var start = 0; start < n; start += length)
			{
				double cr = 1, ci = 0;
				for (var k = 0; k < length / 2; k++)
				{
					var a = start + k;
					var b = a + (length / 2);
					var tr = (re[b] * cr) - (im[b] * ci);
					var ti = (re[b] * ci) + (im[b] * cr);
					re[b] = re[a] - tr;
					im[b] = im[a] - ti;
					re[a] += tr;
					im[a] += ti;
					(cr, ci) = ((cr * wr) - (ci * wi), (cr * wi) + (ci * wr));
				}
			}
		}
	}
}

namespace PanoramicData.SoundPrint.SyntheticAudio;

/// <summary>A biquad filter with coefficients from the Audio EQ Cookbook (Robert Bristow-Johnson).</summary>
public sealed class Biquad
{
	private readonly double _b0, _b1, _b2, _a1, _a2;

	private Biquad(double b0, double b1, double b2, double a0, double a1, double a2)
	{
		_b0 = b0 / a0;
		_b1 = b1 / a0;
		_b2 = b2 / a0;
		_a1 = a1 / a0;
		_a2 = a2 / a0;
	}

	/// <summary>Second-order low-pass.</summary>
	public static Biquad LowPass(double frequency, double q, int sampleRate)
	{
		var (cos, alpha) = Terms(frequency, q, sampleRate);
		return new Biquad((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
	}

	/// <summary>Second-order high-pass.</summary>
	public static Biquad HighPass(double frequency, double q, int sampleRate)
	{
		var (cos, alpha) = Terms(frequency, q, sampleRate);
		return new Biquad((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
	}

	/// <summary>Peaking EQ band.</summary>
	public static Biquad Peaking(double frequency, double gainDb, double q, int sampleRate)
	{
		var (cos, alpha) = Terms(frequency, q, sampleRate);
		var a = Math.Pow(10, gainDb / 40);
		return new Biquad(1 + (alpha * a), -2 * cos, 1 - (alpha * a), 1 + (alpha / a), -2 * cos, 1 - (alpha / a));
	}

	/// <summary>Low shelf.</summary>
	public static Biquad LowShelf(double frequency, double gainDb, int sampleRate)
	{
		var a = Math.Pow(10, gainDb / 40);
		var w = 2 * Math.PI * frequency / sampleRate;
		var cos = Math.Cos(w);
		var alpha = Math.Sin(w) / 2 * Math.Sqrt(2);
		var beta = 2 * Math.Sqrt(a) * alpha;
		return new Biquad(
			a * ((a + 1) - ((a - 1) * cos) + beta),
			2 * a * ((a - 1) - ((a + 1) * cos)),
			a * ((a + 1) - ((a - 1) * cos) - beta),
			(a + 1) + ((a - 1) * cos) + beta,
			-2 * ((a - 1) + ((a + 1) * cos)),
			(a + 1) + ((a - 1) * cos) - beta);
	}

	/// <summary>Filters a signal, returning a new array.</summary>
	public float[] Process(ReadOnlySpan<float> input)
	{
		var output = new float[input.Length];
		double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
		for (var i = 0; i < input.Length; i++)
		{
			var x0 = input[i];
			var y0 = (_b0 * x0) + (_b1 * x1) + (_b2 * x2) - (_a1 * y1) - (_a2 * y2);
			output[i] = (float)y0;
			x2 = x1;
			x1 = x0;
			y2 = y1;
			y1 = y0;
		}

		return output;
	}

	private static (double Cos, double Alpha) Terms(double frequency, double q, int sampleRate)
	{
		var w = 2 * Math.PI * frequency / sampleRate;
		return (Math.Cos(w), Math.Sin(w) / (2 * q));
	}
}

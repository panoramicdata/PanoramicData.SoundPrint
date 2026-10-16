namespace PanoramicData.SoundPrint;

/// <summary>
/// Sine and cosine built only from IEEE 754 basic operations (add, multiply, round), which are exactly reproducible on every
/// machine. <see cref="Math.Sin(double)"/> defers to the platform C runtime and may differ in the last bit between operating
/// systems and CPUs; that could flip a spectral peak tie and change a hash. Window, FFT twiddle and resampler tables use these
/// functions instead, so extraction is bit-for-bit deterministic.
/// </summary>
internal static class DeterministicMath
{
	private const double TwoOverPi = 0.63661977236758134308;
	// Cody-Waite split of pi/2 so the argument reduction stays accurate for the argument sizes used when building tables.
	private const double PiOver2High = 1.57079632673412561417;
	private const double PiOver2Low = 6.07710050650619224932e-11;

	public static double Sin(double x) => Evaluate(x, 0);

	public static double Cos(double x) => Evaluate(x, 1);

	private static double Evaluate(double x, int quadrantShift)
	{
		var k = Math.Round(x * TwoOverPi);
		var r = x - (k * PiOver2High) - (k * PiOver2Low);
		var quadrant = (((long)k + quadrantShift) % 4 + 4) % 4;
		return quadrant switch
		{
			0 => SinPoly(r),
			1 => CosPoly(r),
			2 => -SinPoly(r),
			_ => -CosPoly(r)
		};
	}

	// Taylor series on |r| <= pi/4, where the truncation error is below 1e-19.
	private static double SinPoly(double r)
	{
		var r2 = r * r;
		// r * (1 - r2/3! + r2^2/5! - ...) evaluated by Horner from the highest term.
		var s = 1.0 / 355687428096000.0;
		s = (-s * r2) + (1.0 / 1307674368000.0);
		s = (-s * r2) + (1.0 / 6227020800.0);
		s = (-s * r2) + (1.0 / 39916800.0);
		s = (-s * r2) + (1.0 / 362880.0);
		s = (-s * r2) + (1.0 / 5040.0);
		s = (-s * r2) + (1.0 / 120.0);
		s = (-s * r2) + (1.0 / 6.0);
		s = (-s * r2) + 1.0;
		return r * s;
	}

	private static double CosPoly(double r)
	{
		var r2 = r * r;
		var c = 1.0 / 20922789888000.0;  // 1/16!
		c = (-c * r2) + (1.0 / 87178291200.0); // 1/14!
		c = (-c * r2) + (1.0 / 479001600.0);   // 1/12!
		c = (-c * r2) + (1.0 / 3628800.0);     // 1/10!
		c = (-c * r2) + (1.0 / 40320.0);       // 1/8!
		c = (-c * r2) + (1.0 / 720.0);         // 1/6!
		c = (-c * r2) + (1.0 / 24.0);          // 1/4!
		c = (-c * r2) + 0.5;                   // 1/2!
		return 1.0 - (c * r2);
	}
}

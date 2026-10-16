namespace PanoramicData.SoundPrint.Test;

public class FftTests
{
	[Fact]
	public void Transform_MatchesNaiveDft()
	{
		const int n = 16;
		var rng = new Random(3);
		var re = new double[n];
		var im = new double[n];
		var inRe = new double[n];
		var inIm = new double[n];
		for (var i = 0; i < n; i++)
		{
			inRe[i] = re[i] = rng.NextDouble() - 0.5;
			inIm[i] = im[i] = rng.NextDouble() - 0.5;
		}

		new Fft(n).Transform(re, im);

		for (var k = 0; k < n; k++)
		{
			double sumRe = 0, sumIm = 0;
			for (var t = 0; t < n; t++)
			{
				var angle = -2 * Math.PI * k * t / n;
				sumRe += (inRe[t] * Math.Cos(angle)) - (inIm[t] * Math.Sin(angle));
				sumIm += (inRe[t] * Math.Sin(angle)) + (inIm[t] * Math.Cos(angle));
			}

			re[k].Should().BeApproximately(sumRe, 1e-12);
			im[k].Should().BeApproximately(sumIm, 1e-12);
		}
	}

	[Fact]
	public void Transform_OfSine_PeaksAtItsBin()
	{
		const int n = 1024;
		const int bin = 80;
		var re = new double[n];
		var im = new double[n];
		for (var i = 0; i < n; i++)
		{
			re[i] = Math.Sin(2 * Math.PI * bin * i / n);
		}

		new Fft(n).Transform(re, im);

		var best = 0;
		for (var k = 1; k < n / 2; k++)
		{
			if ((re[k] * re[k]) + (im[k] * im[k]) > (re[best] * re[best]) + (im[best] * im[best]))
			{
				best = k;
			}
		}

		best.Should().Be(bin);
	}
}

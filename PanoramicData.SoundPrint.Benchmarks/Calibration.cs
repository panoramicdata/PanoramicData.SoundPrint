namespace PanoramicData.SoundPrint.Benchmarks;

/// <summary>A fitted mapping from match score to confidence: <c>1 / (1 + exp(-Slope * (score - Midpoint)))</c>.</summary>
internal sealed record CalibrationFit(double Midpoint, double Slope, int Samples)
{
	public double Confidence(double score) => 1 / (1 + Math.Exp(-Slope * (score - Midpoint)));
}

/// <summary>Fits the confidence curve to measured outcomes and chooses a threshold.</summary>
internal static class Calibration
{
	/// <summary>Logistic regression of "top match was correct" on score, by Newton's method with a small ridge.</summary>
	public static CalibrationFit Fit(IEnumerable<Trial> trials)
	{
		var data = trials.Select(t => (X: t.Score, Y: t.Correct ? 1.0 : 0.0)).ToList();
		double w0 = -3, w1 = 0.3;
		for (var iteration = 0; iteration < 100; iteration++)
		{
			double g0 = 0, g1 = 0, h00 = 1e-6, h01 = 0, h11 = 1e-6;
			foreach (var (x, y) in data)
			{
				var p = 1 / (1 + Math.Exp(-(w0 + (w1 * x))));
				var r = p - y;
				var w = Math.Max(p * (1 - p), 1e-9);
				g0 += r;
				g1 += r * x;
				h00 += w;
				h01 += w * x;
				h11 += w * x * x;
			}

			var det = (h00 * h11) - (h01 * h01);
			if (Math.Abs(det) < 1e-18)
			{
				break;
			}

			var d0 = ((h11 * g0) - (h01 * g1)) / det;
			var d1 = ((-h01 * g0) + (h00 * g1)) / det;
			w0 -= Math.Clamp(d0, -2, 2);
			w1 -= Math.Clamp(d1, -0.5, 0.5);
			w1 = Math.Clamp(w1, 0.02, 3);
			if (Math.Abs(d0) + Math.Abs(d1) < 1e-8)
			{
				break;
			}
		}

		return new CalibrationFit(-w0 / w1, w1, data.Count);
	}

	/// <summary>
	/// The lowest score at which the pooled false-match rate is at most <paramref name="maxRate"/>: absent queries that matched plus
	/// present queries whose top match was wrong, as a fraction of all queries.
	/// </summary>
	public static double RecommendedScoreThreshold(IEnumerable<Cell> cells, double maxRate)
	{
		var all = cells.SelectMany(c => c.Present.Select(t => (t, Present: true)).Concat(c.Absent.Select(t => (t, Present: false)))).ToList();
		var falseScores = all.Where(x => !x.Present || !x.t.Correct).Select(x => x.t.Score).Order().ToList();
		var allowed = (int)Math.Floor(maxRate * all.Count);
		// The (allowed + 1)-th highest false score must be below the threshold, so at most `allowed` false matches clear it.
		var index = falseScores.Count - 1 - allowed;
		return index < 0 ? 0 : falseScores[index] + 0.01;
	}
}

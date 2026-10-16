namespace PanoramicData.SoundPrint;

/// <summary>Tunables for <see cref="IMatcher"/>, with defaults.</summary>
public sealed record MatchOptions
{
	/// <summary>A match needs at least this many agreeing landmarks. Default 5.</summary>
	public int MinVotes { get; init; } = 5;

	/// <summary>Landmarks whose offsets differ by up to this many frames are counted together. Default 1.</summary>
	public int OffsetToleranceFrames { get; init; } = 1;

	/// <summary>
	/// Two peaks of one track closer than this many frames of offset are one match. Peaks further apart (a repeated section, a loop)
	/// are reported separately. Default 16, about half a second.
	/// </summary>
	public int SuppressionFrames { get; init; } = 16;

	/// <summary>The most matches returned. Default 5.</summary>
	public int MaxMatches { get; init; } = 5;

	/// <summary>Matches with lower confidence are dropped, which removes the weak secondary peaks that repetitive music produces. Default 0.5.</summary>
	public double MinConfidence { get; init; } = 0.5;

	/// <summary>The lowest expected random-agreement level per offset window, which stops sparse tracks scoring absurdly high. Default 0.05.</summary>
	public double MinBackground { get; init; } = 0.05;

	/// <summary>The <see cref="Match.Score"/> at which confidence is 0.5. Calibrated against the benchmark harness.</summary>
	public double ConfidenceMidpoint { get; init; } = 12;

	/// <summary>How quickly confidence rises around the midpoint, per unit of score. Calibrated against the benchmark harness.</summary>
	public double ConfidenceSlope { get; init; } = 0.4;

	/// <summary>Checks the options for consistency.</summary>
	/// <exception cref="ArgumentException">A value is out of range.</exception>
	public void Validate()
	{
		Require(MinVotes >= 1, nameof(MinVotes), "must be at least 1");
		Require(OffsetToleranceFrames >= 0, nameof(OffsetToleranceFrames), "must not be negative");
		Require(SuppressionFrames >= 1, nameof(SuppressionFrames), "must be at least 1");
		Require(MaxMatches >= 1, nameof(MaxMatches), "must be at least 1");
		Require(MinConfidence is >= 0 and <= 1, nameof(MinConfidence), "must be between 0 and 1");
		Require(MinBackground > 0 && double.IsFinite(MinBackground), nameof(MinBackground), "must be positive");
		Require(double.IsFinite(ConfidenceMidpoint), nameof(ConfidenceMidpoint), "must be finite");
		Require(ConfidenceSlope > 0 && double.IsFinite(ConfidenceSlope), nameof(ConfidenceSlope), "must be positive");
	}

	private static void Require(bool condition, string name, string message)
	{
		if (!condition)
		{
			throw new ArgumentException($"{name} {message}.", name);
		}
	}
}

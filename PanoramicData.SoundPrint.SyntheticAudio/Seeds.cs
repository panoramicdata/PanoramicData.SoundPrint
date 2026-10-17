namespace PanoramicData.SoundPrint.SyntheticAudio;

/// <summary>
/// Deterministic seed mixing. <see cref="HashCode.Combine{T1, T2}(T1, T2)"/> is randomised per process, which would make "the same seed"
/// give different audio on every run; this does not.
/// </summary>
public static class Seeds
{
	/// <summary>Mixes integers into one seed, identically on every run and machine.</summary>
	public static int Mix(int a, int b, int c = 0) =>
		unchecked((a * 73856093) ^ (b * 19349663) ^ (c * 83492791) ^ 0x5bd1e995);
}

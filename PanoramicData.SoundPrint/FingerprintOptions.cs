namespace PanoramicData.SoundPrint;

/// <summary>
/// Every tunable of landmark extraction, with defaults. Changing a value that affects hashes changes
/// <see cref="ParameterFingerprint"/>, and stores refuse to mix tracks and queries with different fingerprints.
/// </summary>
public sealed record FingerprintOptions
{
	/// <summary>The hash layout version, folded into <see cref="ParameterFingerprint"/>.</summary>
	public const int HashFormatVersion = 1;

	/// <summary>The sample rate in hertz that audio is resampled to before analysis. Default 8000.</summary>
	public int SampleRate { get; init; } = 8000;

	/// <summary>STFT window length in samples at <see cref="SampleRate"/>; a power of two from 64 to 1024. Default 1024.</summary>
	public int WindowSize { get; init; } = 1024;

	/// <summary>Samples between successive STFT windows. Default 256.</summary>
	public int HopSize { get; init; } = 256;

	/// <summary>A peak must be the largest within this many bins either side. Default 12.</summary>
	public int PeakNeighbourhoodBins { get; init; } = 12;

	/// <summary>A peak must be the largest within this many frames either side. Default 3.</summary>
	public int PeakNeighbourhoodFrames { get; init; } = 3;

	/// <summary>The lowest frequency bin considered for peaks. Default 2, which skips the DC region.</summary>
	public int MinPeakBin { get; init; } = 2;

	/// <summary>Peaks with spectral power at or below this are ignored, which keeps digital silence from producing peaks. Default 1e-4.</summary>
	public double MinPeakPower { get; init; } = 1e-4;

	/// <summary>
	/// Ascending bin boundaries of the bands used for balancing, starting at 0 and ending at the bin count (window size / 2 + 1).
	/// Default is roughly octave bands for the default window.
	/// </summary>
	public IReadOnlyList<int> BandEdges { get; init; } = [0, 8, 16, 32, 64, 128, 256, 513];

	/// <summary>The most peaks kept per band in each density block. Default 6.</summary>
	public int PeaksPerBandPerBlock { get; init; } = 6;

	/// <summary>Frames per density block (about one second at the defaults). Default 32.</summary>
	public int DensityBlockFrames { get; init; } = 32;

	/// <summary>The most partners paired with each anchor peak. Default 5.</summary>
	public int FanOut { get; init; } = 5;

	/// <summary>The target zone extends this many frames beyond the anchor; 1 to 63. Default 63.</summary>
	public int MaxDeltaFrames { get; init; } = 63;

	/// <summary>The resampling kernel extends this many output-rate zero crossings each side of its centre. Default 16.</summary>
	public int ResamplerHalfTaps { get; init; } = 16;

	/// <summary>Number of frequency bins in the spectrogram (window size / 2 + 1).</summary>
	public int BinCount => (WindowSize / 2) + 1;

	/// <summary>The duration of one frame: the hop between analysis windows.</summary>
	public TimeSpan FrameDuration => FramesToTime(1);

	/// <summary>Converts a frame count to a duration, exactly, using integer arithmetic.</summary>
	/// <param name="frames">The number of frames.</param>
	/// <returns>The duration those frames span.</returns>
	public TimeSpan FramesToTime(long frames) =>
		TimeSpan.FromTicks((long)((Int128)frames * HopSize * TimeSpan.TicksPerSecond / SampleRate));

	/// <summary>
	/// A 64-bit fingerprint of every value that affects which hashes extraction produces (FNV-1a over the values and the hash
	/// format version). Equal fingerprints mean hashes are comparable.
	/// </summary>
	public long ParameterFingerprint
	{
		get
		{
			var h = 14695981039346656037UL;
			void Mix(long value)
			{
				for (var i = 0; i < 8; i++)
				{
					h ^= (byte)(value >> (8 * i));
					h *= 1099511628211UL;
				}
			}

			Mix(HashFormatVersion);
			Mix(SampleRate);
			Mix(WindowSize);
			Mix(HopSize);
			Mix(PeakNeighbourhoodBins);
			Mix(PeakNeighbourhoodFrames);
			Mix(MinPeakBin);
			Mix(BitConverter.DoubleToInt64Bits(MinPeakPower));
			Mix(BandEdges.Count);
			foreach (var edge in BandEdges)
			{
				Mix(edge);
			}

			Mix(PeaksPerBandPerBlock);
			Mix(DensityBlockFrames);
			Mix(FanOut);
			Mix(MaxDeltaFrames);
			Mix(ResamplerHalfTaps);
			return unchecked((long)h);
		}
	}

	/// <summary>Checks the options for consistency.</summary>
	/// <exception cref="ArgumentException">A value is out of range or inconsistent with another.</exception>
	public void Validate()
	{
		Require(SampleRate is >= 1000 and <= 48000, nameof(SampleRate), "must be between 1000 and 48000");
		Require(WindowSize is >= 64 and <= 1024 && (WindowSize & (WindowSize - 1)) == 0, nameof(WindowSize), "must be a power of two from 64 to 1024");
		Require(HopSize >= 1 && HopSize <= WindowSize, nameof(HopSize), "must be between 1 and the window size");
		Require(PeakNeighbourhoodBins >= 1, nameof(PeakNeighbourhoodBins), "must be at least 1");
		Require(PeakNeighbourhoodFrames >= 1, nameof(PeakNeighbourhoodFrames), "must be at least 1");
		Require(MinPeakBin >= 0 && MinPeakBin < BinCount - 2, nameof(MinPeakBin), "must be inside the spectrum");
		Require(MinPeakPower >= 0 && double.IsFinite(MinPeakPower), nameof(MinPeakPower), "must be finite and not negative");
		Require(PeaksPerBandPerBlock >= 1, nameof(PeaksPerBandPerBlock), "must be at least 1");
		Require(DensityBlockFrames > PeakNeighbourhoodFrames, nameof(DensityBlockFrames), "must exceed the frame neighbourhood");
		Require(FanOut >= 1, nameof(FanOut), "must be at least 1");
		Require(MaxDeltaFrames is >= 1 and <= 63, nameof(MaxDeltaFrames), "must be between 1 and 63 (the hash has 6 bits for it)");
		Require(ResamplerHalfTaps >= 2, nameof(ResamplerHalfTaps), "must be at least 2");
		Require(BandEdges is { Count: >= 2 }, nameof(BandEdges), "needs at least two edges");
		Require(BandEdges[0] == 0 && BandEdges[^1] == BinCount, nameof(BandEdges), $"must start at 0 and end at the bin count ({BinCount})");
		for (var i = 1; i < BandEdges.Count; i++)
		{
			Require(BandEdges[i] > BandEdges[i - 1], nameof(BandEdges), "must be strictly ascending");
		}
	}

	private static void Require(bool condition, string name, string message)
	{
		if (!condition)
		{
			throw new ArgumentException($"{name} {message}.", name);
		}
	}
}

namespace PanoramicData.SoundPrint;

/// <summary>One spectral peak that survived band balancing and the density cap.</summary>
/// <param name="Frame">Frame index of the peak, on the stream clock.</param>
/// <param name="Bin">Frequency bin of the peak; the frequency is <c>Bin * SampleRate / WindowSize</c> hertz.</param>
/// <param name="Power">Spectral power of the peak.</param>
public readonly record struct SpectralPeak(int Frame, int Bin, double Power);

/// <summary>
/// Optional hook that receives the spectral peaks a <see cref="Fingerprinter"/> keeps, for visualisation and diagnostics. Peaks arrive in
/// batches as each density block completes. The hook runs on the pushing thread, so keep it quick.
/// </summary>
public interface IPeakObserver
{
	/// <summary>Called with the peaks kept for one density block, ordered by frame then bin. The span is valid only during the call.</summary>
	/// <param name="peaks">The kept peaks.</param>
	void OnPeaks(ReadOnlySpan<SpectralPeak> peaks);
}

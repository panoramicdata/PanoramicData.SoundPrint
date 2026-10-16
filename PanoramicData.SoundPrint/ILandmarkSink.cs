namespace PanoramicData.SoundPrint;

/// <summary>
/// Receives landmarks as an <see cref="IFingerprinter"/> produces them. Batches are passed as spans so that streaming
/// extraction allocates nothing per call.
/// </summary>
public interface ILandmarkSink
{
	/// <summary>
	/// Called with a batch of landmarks, in anchor order. The span is only valid for the duration of the call; copy what you keep.
	/// </summary>
	/// <param name="landmarks">The batch.</param>
	void Add(ReadOnlySpan<Landmark> landmarks);
}

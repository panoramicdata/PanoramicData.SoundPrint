namespace PanoramicData.SoundPrint;

/// <summary>
/// Streaming landmark extraction for one audio stream. Instances are not thread-safe; use one per stream.
/// Output is deterministic and independent of how the input is split into chunks.
/// </summary>
public interface IFingerprinter
{
	/// <summary>
	/// Feeds mono PCM samples at the sample rate the fingerprinter was created for. Landmarks that have become final are passed
	/// to <paramref name="sink"/>; because extraction needs lookahead, output lags the input by up to about three seconds.
	/// </summary>
	/// <param name="samples">Mono samples, nominally in the range -1 to 1.</param>
	/// <param name="sink">Receives the landmarks.</param>
	void Push(ReadOnlySpan<float> samples, ILandmarkSink sink);

	/// <summary>
	/// Marks the end of the stream and emits the landmarks that lookahead was still holding. Further calls to
	/// <see cref="Push"/> throw until <see cref="Reset"/> is called.
	/// </summary>
	/// <param name="sink">Receives the landmarks.</param>
	void Flush(ILandmarkSink sink);

	/// <summary>Starts a new stream, reusing the internal buffers. Frame numbering restarts at zero.</summary>
	void Reset();

	/// <summary>The number of audio frames analysed so far in the current stream.</summary>
	long FramesProcessed { get; }
}

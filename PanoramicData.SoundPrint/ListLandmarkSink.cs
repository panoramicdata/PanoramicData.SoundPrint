namespace PanoramicData.SoundPrint;

/// <summary>
/// An <see cref="ILandmarkSink"/> that collects landmarks into a list. Convenient for fingerprinting a whole track; for a
/// continuous stream use a sink that consumes batches as they arrive, since this one grows without bound.
/// </summary>
public sealed class ListLandmarkSink : ILandmarkSink
{
	private readonly List<Landmark> _landmarks = [];

	/// <summary>The landmarks collected so far.</summary>
	public IReadOnlyList<Landmark> Landmarks => _landmarks;

	/// <inheritdoc />
	public void Add(ReadOnlySpan<Landmark> landmarks) => _landmarks.AddRange(landmarks);

	/// <summary>Discards everything collected so far.</summary>
	public void Clear() => _landmarks.Clear();
}

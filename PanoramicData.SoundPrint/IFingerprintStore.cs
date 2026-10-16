namespace PanoramicData.SoundPrint;

/// <summary>Receives the hits a store finds for a query: one call per matching posting.</summary>
public interface IHitSink
{
	/// <summary>A stored landmark with the same hash as a query landmark.</summary>
	/// <param name="trackIndex">The store's stable integer for the track; resolve it with <see cref="IFingerprintStore.ResolveTrackIdsAsync"/>.</param>
	/// <param name="trackFrame">The frame of the stored landmark within the track.</param>
	/// <param name="queryLandmark">The query landmark that matched.</param>
	void Add(int trackIndex, int trackFrame, Landmark queryLandmark);
}

/// <summary>
/// A database of track fingerprints. Track ids are opaque strings owned by the caller. Implementations must be thread-safe.
/// </summary>
public interface IFingerprintStore
{
	/// <summary>
	/// The <see cref="FingerprintOptions.ParameterFingerprint"/> of the options the stored landmarks were extracted with.
	/// A matcher refuses to query a store whose fingerprint differs from its own options.
	/// </summary>
	long ParameterFingerprint { get; }

	/// <summary>Adds a track, replacing any track already stored under the same id.</summary>
	/// <param name="trackId">The caller's id for the track.</param>
	/// <param name="landmarks">The track's landmarks, as produced by an <see cref="IFingerprinter"/>.</param>
	/// <param name="cancellationToken">Cancels the operation.</param>
	ValueTask AddTrackAsync(string trackId, IReadOnlyList<Landmark> landmarks, CancellationToken cancellationToken);

	/// <summary>Removes a track and all its landmarks.</summary>
	/// <param name="trackId">The caller's id for the track.</param>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <returns>True if the track was present.</returns>
	ValueTask<bool> RemoveTrackAsync(string trackId, CancellationToken cancellationToken);

	/// <summary>Whether a track is stored.</summary>
	/// <param name="trackId">The caller's id for the track.</param>
	/// <param name="cancellationToken">Cancels the operation.</param>
	ValueTask<bool> ContainsTrackAsync(string trackId, CancellationToken cancellationToken);

	/// <summary>The number of tracks stored.</summary>
	/// <param name="cancellationToken">Cancels the operation.</param>
	ValueTask<int> CountTracksAsync(CancellationToken cancellationToken);

	/// <summary>Finds every stored landmark whose hash equals the hash of a query landmark.</summary>
	/// <param name="query">The query landmarks.</param>
	/// <param name="hits">Receives the hits.</param>
	/// <param name="cancellationToken">Cancels the operation.</param>
	ValueTask LookupAsync(IReadOnlyList<Landmark> query, IHitSink hits, CancellationToken cancellationToken);

	/// <summary>Maps track indexes from hits back to the caller's track ids.</summary>
	/// <param name="trackIndexes">Indexes received through <see cref="IHitSink"/>.</param>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <returns>The ids in the same order; null for a track removed since the hit was found.</returns>
	ValueTask<IReadOnlyList<string?>> ResolveTrackIdsAsync(IReadOnlyList<int> trackIndexes, CancellationToken cancellationToken);
}

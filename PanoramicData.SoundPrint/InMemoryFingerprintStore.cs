namespace PanoramicData.SoundPrint;

/// <summary>
/// A thread-safe, in-memory <see cref="IFingerprintStore"/>: a dictionary from hash to a posting list, guarded by a
/// reader-writer lock. Lookups run concurrently; adds and removes are exclusive.
/// </summary>
public sealed class InMemoryFingerprintStore : IFingerprintStore, IDisposable
{
	private readonly ReaderWriterLockSlim _lock = new();
	private readonly Dictionary<uint, PostingList> _postings = [];
	private readonly Dictionary<string, int> _indexById = new(StringComparer.Ordinal);
	private readonly Dictionary<int, TrackEntry> _tracks = [];
	private int _nextIndex;

	/// <summary>Creates an empty store for landmarks extracted with default options.</summary>
	public InMemoryFingerprintStore()
		: this(new FingerprintOptions())
	{
	}

	/// <summary>Creates an empty store.</summary>
	/// <param name="options">The extraction options landmarks will come from.</param>
	public InMemoryFingerprintStore(FingerprintOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		ParameterFingerprint = options.ParameterFingerprint;
	}

	/// <inheritdoc />
	public long ParameterFingerprint { get; }

	/// <summary>The number of tracks stored.</summary>
	public int TrackCount => Read(() => _tracks.Count);

	/// <summary>The total number of landmarks stored across all tracks.</summary>
	public long LandmarkCount => Read(() => _tracks.Values.Sum(t => (long)t.LandmarkCount));

	/// <summary>Adds a track, replacing any track already stored under the same id.</summary>
	/// <param name="trackId">The caller's id for the track.</param>
	/// <param name="landmarks">The track's landmarks.</param>
	public void AddTrack(string trackId, IReadOnlyList<Landmark> landmarks)
	{
		ArgumentException.ThrowIfNullOrEmpty(trackId);
		ArgumentNullException.ThrowIfNull(landmarks);
		foreach (var landmark in landmarks)
		{
			if (landmark.AnchorFrame < 0)
			{
				throw new ArgumentException("Landmark frames must not be negative.", nameof(landmarks));
			}
		}

		_lock.EnterWriteLock();
		try
		{
			RemoveLocked(trackId);
			var index = _nextIndex++;
			var distinct = new HashSet<uint>();
			foreach (var landmark in landmarks)
			{
				if (!_postings.TryGetValue(landmark.Hash, out var list))
				{
					_postings[landmark.Hash] = list = new PostingList();
				}

				list.Add(new Posting(index, landmark.AnchorFrame));
				distinct.Add(landmark.Hash);
			}

			_indexById[trackId] = index;
			_tracks[index] = new TrackEntry(trackId, [.. distinct], landmarks.Count);
		}
		finally
		{
			_lock.ExitWriteLock();
		}
	}

	/// <summary>Removes a track and all its landmarks.</summary>
	/// <param name="trackId">The caller's id for the track.</param>
	/// <returns>True if the track was present.</returns>
	public bool RemoveTrack(string trackId)
	{
		ArgumentException.ThrowIfNullOrEmpty(trackId);
		_lock.EnterWriteLock();
		try
		{
			return RemoveLocked(trackId);
		}
		finally
		{
			_lock.ExitWriteLock();
		}
	}

	/// <summary>Whether a track is stored.</summary>
	/// <param name="trackId">The caller's id for the track.</param>
	public bool ContainsTrack(string trackId)
	{
		ArgumentException.ThrowIfNullOrEmpty(trackId);
		return Read(() => _indexById.ContainsKey(trackId));
	}

	/// <summary>Finds every stored landmark whose hash equals the hash of a query landmark.</summary>
	/// <param name="query">The query landmarks.</param>
	/// <param name="hits">Receives the hits. It is called while a read lock is held and must not call back into this store.</param>
	public void Lookup(IReadOnlyList<Landmark> query, IHitSink hits)
	{
		ArgumentNullException.ThrowIfNull(query);
		ArgumentNullException.ThrowIfNull(hits);
		_lock.EnterReadLock();
		try
		{
			foreach (var landmark in query)
			{
				if (_postings.TryGetValue(landmark.Hash, out var list))
				{
					for (var i = 0; i < list.Count; i++)
					{
						hits.Add(list.Items[i].Track, list.Items[i].Frame, landmark);
					}
				}
			}
		}
		finally
		{
			_lock.ExitReadLock();
		}
	}

	/// <summary>Maps track indexes back to ids.</summary>
	/// <param name="trackIndexes">Indexes received through <see cref="IHitSink"/>.</param>
	/// <returns>The ids in the same order; null for a track removed since.</returns>
	public IReadOnlyList<string?> ResolveTrackIds(IReadOnlyList<int> trackIndexes)
	{
		ArgumentNullException.ThrowIfNull(trackIndexes);
		return Read<IReadOnlyList<string?>>(() => [.. trackIndexes.Select(i => _tracks.TryGetValue(i, out var track) ? track.Id : null)]);
	}

	/// <inheritdoc />
	public ValueTask AddTrackAsync(string trackId, IReadOnlyList<Landmark> landmarks, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		AddTrack(trackId, landmarks);
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public ValueTask<bool> RemoveTrackAsync(string trackId, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult(RemoveTrack(trackId));
	}

	/// <inheritdoc />
	public ValueTask<bool> ContainsTrackAsync(string trackId, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult(ContainsTrack(trackId));
	}

	/// <inheritdoc />
	public ValueTask<int> CountTracksAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult(TrackCount);
	}

	/// <inheritdoc />
	public ValueTask LookupAsync(IReadOnlyList<Landmark> query, IHitSink hits, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Lookup(query, hits);
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public ValueTask<IReadOnlyList<string?>> ResolveTrackIdsAsync(IReadOnlyList<int> trackIndexes, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult(ResolveTrackIds(trackIndexes));
	}

	/// <inheritdoc />
	public void Dispose() => _lock.Dispose();

	private bool RemoveLocked(string trackId)
	{
		if (!_indexById.Remove(trackId, out var index))
		{
			return false;
		}

		var entry = _tracks[index];
		_tracks.Remove(index);
		foreach (var hash in entry.Hashes)
		{
			var list = _postings[hash];
			list.RemoveTrack(index);
			if (list.Count == 0)
			{
				_postings.Remove(hash);
			}
		}

		return true;
	}

	private T Read<T>(Func<T> action)
	{
		_lock.EnterReadLock();
		try
		{
			return action();
		}
		finally
		{
			_lock.ExitReadLock();
		}
	}

	private readonly record struct Posting(int Track, int Frame);

	private sealed record TrackEntry(string Id, uint[] Hashes, int LandmarkCount);

	private sealed class PostingList
	{
		private Posting[] _items = new Posting[2];

		public Posting[] Items => _items;

		public int Count { get; private set; }

		public void Add(Posting posting)
		{
			if (Count == Items.Length)
			{
				Array.Resize(ref _items, Count * 2);
			}

			_items[Count++] = posting;
		}

		public void RemoveTrack(int track)
		{
			var kept = 0;
			for (var i = 0; i < Count; i++)
			{
				if (_items[i].Track != track)
				{
					_items[kept++] = _items[i];
				}
			}

			Count = kept;
		}
	}
}

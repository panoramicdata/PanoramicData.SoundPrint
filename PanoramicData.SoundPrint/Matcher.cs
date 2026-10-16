namespace PanoramicData.SoundPrint;

/// <summary>
/// Offset-histogram matcher. Every stored landmark with the same hash as a query landmark votes for the pair (track, offset), where
/// offset is track frame minus query frame. A genuine match makes many votes agree on one offset; coincidences scatter.
/// The matcher keeps no state between calls, so one instance can serve many threads.
/// </summary>
public sealed class Matcher : IMatcher
{
	private readonly IFingerprintStore _store;
	private readonly FingerprintOptions _fingerprintOptions;
	private readonly MatchOptions _defaults;

	/// <summary>Creates a matcher over a store whose landmarks were extracted with default options.</summary>
	/// <param name="store">The fingerprint database.</param>
	/// <exception cref="ArgumentException">The store was built with different extraction options.</exception>
	public Matcher(IFingerprintStore store)
		: this(store, new FingerprintOptions(), new MatchOptions())
	{
	}

	/// <summary>Creates a matcher over a store, with default match options.</summary>
	/// <param name="store">The fingerprint database.</param>
	/// <param name="fingerprintOptions">The extraction options the store's landmarks and the queries were produced with.</param>
	/// <exception cref="ArgumentException">The store was built with different extraction options, so hashes are not comparable.</exception>
	public Matcher(IFingerprintStore store, FingerprintOptions fingerprintOptions)
		: this(store, fingerprintOptions, new MatchOptions())
	{
	}

	/// <summary>Creates a matcher over a store.</summary>
	/// <param name="store">The fingerprint database.</param>
	/// <param name="fingerprintOptions">The extraction options the store's landmarks and the queries were produced with.</param>
	/// <param name="defaults">Default match options.</param>
	/// <exception cref="ArgumentException">The store was built with different extraction options, so hashes are not comparable.</exception>
	public Matcher(IFingerprintStore store, FingerprintOptions fingerprintOptions, MatchOptions defaults)
	{
		ArgumentNullException.ThrowIfNull(store);
		ArgumentNullException.ThrowIfNull(fingerprintOptions);
		ArgumentNullException.ThrowIfNull(defaults);
		_fingerprintOptions = fingerprintOptions;
		_fingerprintOptions.Validate();
		_defaults = defaults;
		_defaults.Validate();
		if (store.ParameterFingerprint != _fingerprintOptions.ParameterFingerprint)
		{
			throw new ArgumentException(
				"The store was built with different fingerprint options than the matcher was given; their hashes are not comparable.",
				nameof(store));
		}

		_store = store;
	}

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<Match>> MatchAsync(IReadOnlyList<Landmark> query, MatchOptions? options, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);
		var o = options ?? _defaults;
		o.Validate();
		if (query.Count == 0)
		{
			return [];
		}

		using var votes = new VoteCollector();
		await _store.LookupAsync(query, votes, cancellationToken).ConfigureAwait(false);
		var ranked = Rank(new OffsetVoting(votes, o).Analyse(), o.MaxMatches);
		if (ranked.Count == 0)
		{
			return [];
		}

		var ids = await _store.ResolveTrackIdsAsync([.. ranked.Select(r => r.TrackIndex)], cancellationToken).ConfigureAwait(false);
		return ToMatches(ranked, ids);
	}

	/// <summary>Matches a query with the matcher's default options.</summary>
	/// <param name="query">Landmarks of the query audio.</param>
	/// <returns>The matches, most confident first.</returns>
	public ValueTask<IReadOnlyList<Match>> MatchAsync(IReadOnlyList<Landmark> query) =>
		MatchAsync(query, null, CancellationToken.None);

	private static List<RawMatch> Rank(List<RawMatch> raw, int maxMatches)
	{
		raw.Sort(static (a, b) =>
		{
			var c = b.Confidence.CompareTo(a.Confidence);
			if (c != 0)
			{
				return c;
			}

			c = b.Votes.CompareTo(a.Votes);
			return c != 0 ? c : a.OffsetFrames.CompareTo(b.OffsetFrames);
		});
		if (raw.Count > maxMatches)
		{
			raw.RemoveRange(maxMatches, raw.Count - maxMatches);
		}

		return raw;
	}

	private List<Match> ToMatches(List<RawMatch> ranked, IReadOnlyList<string?> ids)
	{
		var matches = new List<Match>(ranked.Count);
		for (var i = 0; i < ranked.Count; i++)
		{
			if (ids[i] is not { } id)
			{
				continue;
			}

			var r = ranked[i];
			matches.Add(new Match(
				id,
				_fingerprintOptions.FramesToTime(r.OffsetFrames),
				_fingerprintOptions.FramesToTime(r.QueryStartFrame),
				_fingerprintOptions.FramesToTime(r.QueryEndFrame),
				r.Confidence,
				r.Votes,
				r.Score));
		}

		return matches;
	}
}

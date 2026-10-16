using System.Buffers;

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

	/// <summary>Creates a matcher over a store.</summary>
	/// <param name="store">The fingerprint database.</param>
	/// <param name="fingerprintOptions">The extraction options the store's landmarks and the queries were produced with.</param>
	/// <param name="defaults">Default match options; library defaults when null.</param>
	/// <exception cref="ArgumentException">The store was built with different extraction options, so hashes are not comparable.</exception>
	public Matcher(IFingerprintStore store, FingerprintOptions? fingerprintOptions = null, MatchOptions? defaults = null)
	{
		ArgumentNullException.ThrowIfNull(store);
		_fingerprintOptions = fingerprintOptions ?? new FingerprintOptions();
		_fingerprintOptions.Validate();
		_defaults = defaults ?? new MatchOptions();
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
	public async ValueTask<IReadOnlyList<Match>> MatchAsync(IReadOnlyList<Landmark> query, MatchOptions? options = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(query);
		var o = options ?? _defaults;
		if (options is not null)
		{
			o.Validate();
		}

		if (query.Count == 0)
		{
			return [];
		}

		using var votes = new VoteCollector();
		await _store.LookupAsync(query, votes, cancellationToken).ConfigureAwait(false);
		var raw = Analyse(votes, o);
		if (raw.Count == 0)
		{
			return [];
		}

		raw.Sort(static (a, b) =>
		{
			var c = b.Confidence.CompareTo(a.Confidence);
			if (c != 0) { return c; }
			c = b.Votes.CompareTo(a.Votes);
			return c != 0 ? c : a.OffsetFrames.CompareTo(b.OffsetFrames);
		});
		if (raw.Count > o.MaxMatches)
		{
			raw.RemoveRange(o.MaxMatches, raw.Count - o.MaxMatches);
		}

		var ids = await _store.ResolveTrackIdsAsync([.. raw.Select(r => r.TrackIndex)], cancellationToken).ConfigureAwait(false);
		var matches = new List<Match>(raw.Count);
		for (var i = 0; i < raw.Count; i++)
		{
			if (ids[i] is { } id)
			{
				var r = raw[i];
				matches.Add(new Match(
					id,
					_fingerprintOptions.FramesToTime(r.OffsetFrames),
					_fingerprintOptions.FramesToTime(r.QueryStartFrame),
					_fingerprintOptions.FramesToTime(r.QueryEndFrame),
					r.Confidence,
					r.Votes,
					r.Score));
			}
		}

		return matches;
	}

	/// <summary>
	/// Negative base-10 logarithm of P(X &gt;= k) for X Poisson with mean <paramref name="lambda"/>.
	/// </summary>
	internal static double PoissonScore(int k, double lambda)
	{
		if (k <= 0)
		{
			return 0;
		}

		double logFactorial = 0;
		for (var i = 2; i <= k; i++)
		{
			logFactorial += Math.Log(i);
		}

		var logTerm = -lambda + (k * Math.Log(lambda)) - logFactorial;
		double sum = 0;
		var term = 1.0;
		for (var j = 1; j < 100000; j++)
		{
			term *= lambda / (k + j);
			sum += term;
			if (term < 1e-17 * (1 + sum))
			{
				break;
			}
		}

		return -(logTerm + Math.Log(1 + sum)) / Math.Log(10);
	}

	private static List<RawMatch> Analyse(VoteCollector votes, MatchOptions o)
	{
		var results = new List<RawMatch>();
		if (votes.Count < o.MinVotes)
		{
			return results;
		}

		Array.Sort(votes.Keys, votes.Spans, 0, votes.Count);
		var runOffsets = new List<int>();
		var runStarts = new List<int>();
		var windowLow = new List<int>();
		var windowHigh = new List<int>();
		var windowSum = new List<int>();
		var order = new List<int>();
		var accepted = new List<int>();

		var i = 0;
		while (i < votes.Count)
		{
			var track = (int)(votes.Keys[i] >> 32);
			var end = i;
			while (end < votes.Count && (int)(votes.Keys[end] >> 32) == track)
			{
				end++;
			}

			if (end - i >= o.MinVotes)
			{
				AnalyseTrack(votes, track, i, end, o, results, runOffsets, runStarts, windowLow, windowHigh, windowSum, order, accepted);
			}

			i = end;
		}

		return results;
	}

	private static void AnalyseTrack(
		VoteCollector votes,
		int track,
		int start,
		int end,
		MatchOptions o,
		List<RawMatch> results,
		List<int> runOffsets,
		List<int> runStarts,
		List<int> windowLow,
		List<int> windowHigh,
		List<int> windowSum,
		List<int> order,
		List<int> accepted)
	{
		runOffsets.Clear();
		runStarts.Clear();
		for (var i = start; i < end; i++)
		{
			var offset = (int)((uint)votes.Keys[i] ^ 0x80000000u);
			if (runOffsets.Count == 0 || runOffsets[^1] != offset)
			{
				runOffsets.Add(offset);
				runStarts.Add(i);
			}
		}

		runStarts.Add(end);
		var runs = runOffsets.Count;

		windowLow.Clear();
		windowHigh.Clear();
		windowSum.Clear();
		var low = 0;
		var high = 0;
		for (var r = 0; r < runs; r++)
		{
			while ((long)runOffsets[low] < (long)runOffsets[r] - o.OffsetToleranceFrames)
			{
				low++;
			}

			while (high < runs && (long)runOffsets[high] <= (long)runOffsets[r] + o.OffsetToleranceFrames)
			{
				high++;
			}

			windowLow.Add(low);
			windowHigh.Add(high);
			windowSum.Add(runStarts[high] - runStarts[low]);
		}

		order.Clear();
		for (var r = 0; r < runs; r++)
		{
			if (windowSum[r] >= o.MinVotes)
			{
				order.Add(r);
			}
		}

		if (order.Count == 0)
		{
			return;
		}

		order.Sort((a, b) =>
		{
			var c = windowSum[b].CompareTo(windowSum[a]);
			return c != 0 ? c : runOffsets[a].CompareTo(runOffsets[b]);
		});

		accepted.Clear();
		var acceptedVotes = 0;
		foreach (var r in order)
		{
			var clash = false;
			foreach (var other in accepted)
			{
				if (Math.Abs((long)runOffsets[r] - runOffsets[other]) < o.SuppressionFrames)
				{
					clash = true;
					break;
				}
			}

			if (!clash)
			{
				accepted.Add(r);
				acceptedVotes += windowSum[r];
			}
		}

		var hits = end - start;
		var spread = Math.Max((long)runOffsets[^1] - runOffsets[0] + 1, (2L * o.OffsetToleranceFrames) + 1);
		var background = Math.Max(o.MinBackground, (hits - acceptedVotes) * ((2.0 * o.OffsetToleranceFrames) + 1) / spread);
		foreach (var r in accepted)
		{
			var queryStart = int.MaxValue;
			var queryEnd = int.MinValue;
			for (var k = runStarts[windowLow[r]]; k < runStarts[windowHigh[r]]; k++)
			{
				var span = votes.Spans[k];
				queryStart = Math.Min(queryStart, (int)(span >> 32));
				queryEnd = Math.Max(queryEnd, (int)(uint)span);
			}

			var score = PoissonScore(windowSum[r], background);
			var confidence = 1 / (1 + Math.Exp(-o.ConfidenceSlope * (score - o.ConfidenceMidpoint)));
			if (confidence >= o.MinConfidence)
			{
				results.Add(new RawMatch(track, runOffsets[r], queryStart, queryEnd, windowSum[r], score, confidence));
			}
		}
	}

	private sealed record RawMatch(int TrackIndex, int OffsetFrames, int QueryStartFrame, int QueryEndFrame, int Votes, double Score, double Confidence);

	/// <summary>Collects hits into pooled arrays: a sortable key (track, offset) and the query span each hit supports.</summary>
	private sealed class VoteCollector : IHitSink, IDisposable
	{
		public long[] Keys { get; private set; } = ArrayPool<long>.Shared.Rent(4096);

		public long[] Spans { get; private set; } = ArrayPool<long>.Shared.Rent(4096);

		public int Count { get; private set; }

		public void Add(int trackIndex, int trackFrame, Landmark queryLandmark)
		{
			if (Count == Keys.Length)
			{
				Grow();
			}

			var offset = trackFrame - queryLandmark.AnchorFrame;
			Keys[Count] = ((long)trackIndex << 32) | (uint)(offset ^ int.MinValue);
			Spans[Count] = ((long)queryLandmark.AnchorFrame << 32) | (uint)unchecked(queryLandmark.AnchorFrame + queryLandmark.DeltaFrames);
			Count++;
		}

		public void Dispose()
		{
			ArrayPool<long>.Shared.Return(Keys);
			ArrayPool<long>.Shared.Return(Spans);
		}

		private void Grow()
		{
			var keys = ArrayPool<long>.Shared.Rent(Keys.Length * 2);
			var spans = ArrayPool<long>.Shared.Rent(Spans.Length * 2);
			Array.Copy(Keys, keys, Count);
			Array.Copy(Spans, spans, Count);
			ArrayPool<long>.Shared.Return(Keys);
			ArrayPool<long>.Shared.Return(Spans);
			Keys = keys;
			Spans = spans;
		}
	}
}

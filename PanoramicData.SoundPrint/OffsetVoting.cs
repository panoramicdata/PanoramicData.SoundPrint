using System.Buffers;

namespace PanoramicData.SoundPrint;

/// <summary>One peak of the offset histogram of one track, before ids are resolved and times converted.</summary>
internal sealed record RawMatch(int TrackIndex, int OffsetFrames, int QueryStartFrame, int QueryEndFrame, int Votes, double Score, double Confidence);

/// <summary>Collects hits into pooled arrays: a sortable key (track, offset) and the query span each hit supports.</summary>
internal sealed class VoteCollector : IHitSink, IDisposable
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

/// <summary>
/// Finds the peaks of each track's offset histogram. Hits are sorted by (track, offset); runs of equal offsets are windowed by the
/// offset tolerance, the strongest windows are accepted greedily (peaks closer than the suppression distance are one match), and each
/// accepted peak is scored against the track's background level. One instance is used for one analysis and holds its scratch lists.
/// </summary>
internal sealed class OffsetVoting(VoteCollector votes, MatchOptions options)
{
	private readonly List<int> _runOffsets = [];
	private readonly List<int> _runStarts = [];
	private readonly List<int> _windowLow = [];
	private readonly List<int> _windowHigh = [];
	private readonly List<int> _windowSum = [];
	private readonly List<int> _accepted = [];
	private readonly List<RawMatch> _results = [];

	/// <summary>Analyses every track that has at least the minimum number of hits.</summary>
	public List<RawMatch> Analyse()
	{
		if (votes.Count < options.MinVotes)
		{
			return _results;
		}

		Array.Sort(votes.Keys, votes.Spans, 0, votes.Count);
		var start = 0;
		while (start < votes.Count)
		{
			var track = TrackOf(start);
			var end = start;
			while (end < votes.Count && TrackOf(end) == track)
			{
				end++;
			}

			if (end - start >= options.MinVotes)
			{
				AnalyseTrack(track, start, end);
			}

			start = end;
		}

		return _results;
	}

	/// <summary>Negative base-10 logarithm of P(X &gt;= k) for X Poisson with mean <paramref name="lambda"/>.</summary>
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
		for (var j = 1; j < 100000 && term >= 1e-17 * (1 + sum); j++)
		{
			term *= lambda / (k + j);
			sum += term;
		}

		return -(logTerm + Math.Log(1 + sum)) / Math.Log(10);
	}

	private int TrackOf(int index) => (int)(votes.Keys[index] >> 32);

	private void AnalyseTrack(int track, int start, int end)
	{
		BuildRuns(start, end);
		BuildWindows();
		var candidates = Candidates();
		if (candidates.Count == 0)
		{
			return;
		}

		AcceptPeaks(candidates);
		var hits = end - start;
		var acceptedVotes = _accepted.Sum(r => _windowSum[r]);
		var spread = Math.Max((long)_runOffsets[^1] - _runOffsets[0] + 1, (2L * options.OffsetToleranceFrames) + 1);
		var background = Math.Max(options.MinBackground, (hits - acceptedVotes) * ((2.0 * options.OffsetToleranceFrames) + 1) / spread);
		foreach (var run in _accepted)
		{
			AddResult(track, run, background);
		}
	}

	/// <summary>Groups the sorted hits of one track into runs of equal offset.</summary>
	private void BuildRuns(int start, int end)
	{
		_runOffsets.Clear();
		_runStarts.Clear();
		for (var i = start; i < end; i++)
		{
			var offset = (int)((uint)votes.Keys[i] ^ 0x80000000u);
			if (_runOffsets.Count == 0 || _runOffsets[^1] != offset)
			{
				_runOffsets.Add(offset);
				_runStarts.Add(i);
			}
		}

		_runStarts.Add(end);
	}

	/// <summary>For each run, the range of runs within the offset tolerance and the number of hits in them.</summary>
	private void BuildWindows()
	{
		_windowLow.Clear();
		_windowHigh.Clear();
		_windowSum.Clear();
		var runs = _runOffsets.Count;
		var low = 0;
		var high = 0;
		for (var r = 0; r < runs; r++)
		{
			while ((long)_runOffsets[low] < (long)_runOffsets[r] - options.OffsetToleranceFrames)
			{
				low++;
			}

			while (high < runs && (long)_runOffsets[high] <= (long)_runOffsets[r] + options.OffsetToleranceFrames)
			{
				high++;
			}

			_windowLow.Add(low);
			_windowHigh.Add(high);
			_windowSum.Add(_runStarts[high] - _runStarts[low]);
		}
	}

	/// <summary>Runs whose window reaches the minimum vote count, strongest first.</summary>
	private List<int> Candidates()
	{
		var order = Enumerable.Range(0, _runOffsets.Count).Where(r => _windowSum[r] >= options.MinVotes).ToList();
		order.Sort((a, b) =>
		{
			var c = _windowSum[b].CompareTo(_windowSum[a]);
			return c != 0 ? c : _runOffsets[a].CompareTo(_runOffsets[b]);
		});
		return order;
	}

	private void AcceptPeaks(List<int> candidates)
	{
		_accepted.Clear();
		foreach (var run in candidates)
		{
			if (!_accepted.Exists(other => Math.Abs((long)_runOffsets[run] - _runOffsets[other]) < options.SuppressionFrames))
			{
				_accepted.Add(run);
			}
		}
	}

	private void AddResult(int track, int run, double background)
	{
		var queryStart = int.MaxValue;
		var queryEnd = int.MinValue;
		for (var k = _runStarts[_windowLow[run]]; k < _runStarts[_windowHigh[run]]; k++)
		{
			var span = votes.Spans[k];
			queryStart = Math.Min(queryStart, (int)(span >> 32));
			queryEnd = Math.Max(queryEnd, (int)(uint)span);
		}

		var score = PoissonScore(_windowSum[run], background);
		var confidence = 1 / (1 + Math.Exp(-options.ConfidenceSlope * (score - options.ConfidenceMidpoint)));
		if (confidence >= options.MinConfidence)
		{
			_results.Add(new RawMatch(track, _runOffsets[run], queryStart, queryEnd, _windowSum[run], score, confidence));
		}
	}
}

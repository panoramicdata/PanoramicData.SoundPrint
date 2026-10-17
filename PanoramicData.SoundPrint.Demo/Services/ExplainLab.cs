namespace PanoramicData.SoundPrint.Demo.Services;

/// <summary>A pair of peaks joined into one landmark, with its hash fields unpacked for display.</summary>
/// <param name="AnchorFrame">Frame of the anchor peak, relative to the view.</param>
/// <param name="AnchorBin">Frequency bin of the anchor.</param>
/// <param name="PartnerFrame">Frame of the partner peak.</param>
/// <param name="PartnerBin">Frequency bin of the partner.</param>
/// <param name="Hash">The 32-bit hash.</param>
public sealed record PairView(int AnchorFrame, int AnchorBin, int PartnerFrame, int PartnerBin, uint Hash)
{
	/// <summary>Frequency difference in bins (partner minus anchor).</summary>
	public int DeltaBins => PartnerBin - AnchorBin;

	/// <summary>Time difference in frames.</summary>
	public int DeltaFrames => PartnerFrame - AnchorFrame;
}

/// <summary>What the fingerprinter saw in a stretch of audio: the spectrogram, the kept peaks and the landmarks.</summary>
/// <param name="StartSeconds">Where in the track the view starts.</param>
/// <param name="Spectrogram">The power spectrogram.</param>
/// <param name="Peaks">Peaks kept by band balancing, frames relative to the view.</param>
/// <param name="Pairs">Landmarks as pairs of peaks.</param>
public sealed record ExplainView(double StartSeconds, SpectrogramMaker.Spectrogram Spectrogram, IReadOnlyList<SpectralPeak> Peaks, IReadOnlyList<PairView> Pairs);

/// <summary>One trial of finding a clip in its track at a given sub-frame phase.</summary>
/// <param name="ShiftSamples">How far the clip's first sample sits past a frame boundary of the track's own frame grid.</param>
/// <param name="ClipLandmarks">Landmarks extracted from the clip.</param>
/// <param name="HitsInTrack">Clip landmarks whose hash also occurs somewhere in the track.</param>
/// <param name="AtTrueOffset">Of those, hits whose offset is within one frame of the true offset.</param>
/// <param name="PeakVotes">Votes in the strongest offset window (the matcher's window of three frames).</param>
/// <param name="FoundOffsetFrames">Centre of that strongest window, in frames.</param>
/// <param name="TrueOffsetFrames">The exact offset in frames (not generally a whole number).</param>
/// <param name="BackgroundVotes">Votes a window would be expected to get by coincidence alone.</param>
/// <param name="Histogram">Votes by offset in frames, binned.</param>
/// <param name="HistogramBinFrames">Width of each histogram bin in frames.</param>
/// <param name="HistogramFirstFrame">Offset in frames of the first histogram bin.</param>
public sealed record ClipTrial(
	int ShiftSamples,
	int ClipLandmarks,
	int HitsInTrack,
	int AtTrueOffset,
	int PeakVotes,
	int FoundOffsetFrames,
	double TrueOffsetFrames,
	double BackgroundVotes,
	IReadOnlyList<int> Histogram,
	int HistogramBinFrames,
	int HistogramFirstFrame);

/// <summary>The computations behind the "How it works" page, kept out of the UI so they can be tested.</summary>
public sealed class ExplainLab(DemoCatalogue catalogue)
{
	/// <summary>Window length used by the fingerprinter (samples).</summary>
	public const int Window = 1024;

	/// <summary>Hop used by the fingerprinter (samples).</summary>
	public const int Hop = 256;

	private sealed class PeakCollector : IPeakObserver
	{
		public List<SpectralPeak> Peaks { get; } = [];

		public void OnPeaks(ReadOnlySpan<SpectralPeak> peaks) => Peaks.AddRange(peaks);
	}

	/// <summary>Builds the spectrogram, peaks and landmarks for a stretch of a catalogue track.</summary>
	/// <param name="track">A catalogue track that kept its audio.</param>
	/// <param name="startSeconds">Where to start.</param>
	/// <param name="seconds">How much to show.</param>
	/// <returns>The view.</returns>
	public ExplainView BuildView(CatalogueTrack track, double startSeconds, int seconds)
	{
		var samples = track.Samples ?? throw new ArgumentException("The track has no audio.", nameof(track));
		var rate = DemoCatalogue.SampleRate;
		var start = (int)(startSeconds * rate);
		var segment = samples.AsSpan(start, Math.Min(seconds * rate, samples.Length - start)).ToArray();

		var collector = new PeakCollector();
		var fingerprinter = new Fingerprinter(rate, catalogue.Options) { PeakObserver = collector };
		var sink = new ListLandmarkSink();
		fingerprinter.Push(segment, sink);
		fingerprinter.Flush(sink);

		var pairs = sink.Landmarks.Select(l =>
		{
			var bin = (int)((l.Hash >> 14) & 0x1FF);
			var deltaBins = (int)((l.Hash >> 6) & 0xFF) - 128;
			return new PairView(l.AnchorFrame, bin, l.AnchorFrame + l.DeltaFrames, bin + deltaBins, l.Hash);
		}).ToList();

		return new ExplainView(startSeconds, SpectrogramMaker.Compute(segment, Window, Hop), collector.Peaks, pairs);
	}

	/// <summary>
	/// Cuts a clip from a track starting <paramref name="startSeconds"/> plus <paramref name="shiftSamples"/> samples in, fingerprints it on its own
	/// frame grid, looks its hashes up in the catalogue, and measures how well the votes find the true offset.
	/// </summary>
	/// <param name="track">The track the clip is cut from.</param>
	/// <param name="startSeconds">Whole seconds into the track.</param>
	/// <param name="shiftSamples">Extra samples, which moves the clip's frame grid relative to the track's.</param>
	/// <param name="clipSeconds">Length of the clip.</param>
	/// <returns>The trial.</returns>
	public ClipTrial RunClip(CatalogueTrack track, double startSeconds, int shiftSamples, int clipSeconds)
	{
		var samples = track.Samples ?? throw new ArgumentException("The track has no audio.", nameof(track));
		var rate = DemoCatalogue.SampleRate;
		var startSample = (int)(startSeconds * rate) + shiftSamples;
		var clip = samples.AsSpan(startSample, Math.Min(clipSeconds * rate, samples.Length - startSample)).ToArray();
		var query = Fingerprinter.Fingerprint(clip, rate, catalogue.Options);

		var collector = new HitCollector();
		catalogue.Store.Lookup(query, collector);
		var indexes = collector.Hits.Select(h => h.Track).Distinct().ToList();
		var ids = catalogue.Store.ResolveTrackIds(indexes);
		var mine = indexes.Where(i => ids[indexes.IndexOf(i)] == track.Id).ToHashSet();
		var offsets = collector.Hits.Where(h => mine.Contains(h.Track)).Select(h => h.Offset).ToList();

		var trueOffset = startSample / (double)Hop;
		var nearest = (int)Math.Round(trueOffset);
		var atTrue = offsets.Count(o => Math.Abs(o - nearest) <= 1);
		var hitsInTrack = collector.Hits.Where(h => mine.Contains(h.Track)).Select(h => (h.QueryFrame, h.Hash)).Distinct().Count();

		var (peakVotes, foundOffset) = StrongestWindow(offsets);
		var span = offsets.Count == 0 ? 1 : offsets.Max() - offsets.Min() + 1;
		var background = offsets.Count == 0 ? 0 : (offsets.Count - peakVotes) * 3.0 / Math.Max(span, 3);

		var binFrames = Math.Max(2, (int)Math.Ceiling(span / 140.0));
		var first = offsets.Count == 0 ? 0 : (int)Math.Floor(offsets.Min() / (double)binFrames) * binFrames;
		var histogram = new int[offsets.Count == 0 ? 0 : (((offsets.Max() - first) / binFrames) + 1)];
		foreach (var offset in offsets)
		{
			histogram[(offset - first) / binFrames]++;
		}

		return new ClipTrial(shiftSamples, query.Count, hitsInTrack, atTrue, peakVotes, foundOffset, trueOffset, background, histogram, binFrames, first);
	}

	/// <summary>Runs the same clip at eight different sub-frame phases.</summary>
	/// <param name="track">The track.</param>
	/// <param name="startSeconds">Whole seconds in.</param>
	/// <param name="clipSeconds">Clip length.</param>
	/// <returns>One trial per phase, 0, 32, 64, ... 224 samples.</returns>
	public IReadOnlyList<ClipTrial> SweepPhases(CatalogueTrack track, double startSeconds, int clipSeconds) =>
		[.. Enumerable.Range(0, 8).Select(i => RunClip(track, startSeconds, i * (Hop / 8), clipSeconds))];

	private static (int Votes, int Offset) StrongestWindow(List<int> offsets)
	{
		if (offsets.Count == 0)
		{
			return (0, 0);
		}

		var counts = offsets.GroupBy(o => o).ToDictionary(g => g.Key, g => g.Count());
		var best = (Votes: 0, Offset: 0);
		foreach (var centre in counts.Keys)
		{
			var votes = counts.GetValueOrDefault(centre - 1) + counts[centre] + counts.GetValueOrDefault(centre + 1);
			if (votes > best.Votes)
			{
				best = (votes, centre);
			}
		}

		return best;
	}

	private sealed class HitCollector : IHitSink
	{
		public List<(int Track, int Offset, int QueryFrame, uint Hash)> Hits { get; } = [];

		public void Add(int trackIndex, int trackFrame, Landmark queryLandmark) =>
			Hits.Add((trackIndex, trackFrame - queryLandmark.AnchorFrame, queryLandmark.AnchorFrame, queryLandmark.Hash));
	}
}

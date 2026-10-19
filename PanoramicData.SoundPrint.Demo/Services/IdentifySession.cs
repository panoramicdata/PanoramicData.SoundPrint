namespace PanoramicData.SoundPrint.Demo.Services;

/// <summary>One stretch of listening during which the same track was recognised.</summary>
/// <param name="TrackId">The recognised track.</param>
/// <param name="FirstHeardAt">Seconds of audio heard when it was first recognised.</param>
/// <param name="LastHeardAt">Seconds of audio heard when it was last recognised.</param>
/// <param name="TrackStart">Where in the track the first matched span started.</param>
public sealed record HeardSegment(string TrackId, double FirstHeardAt, double LastHeardAt, TimeSpan TrackStart);

/// <summary>
/// One listening session in the browser: raw PCM goes in, a live fingerprint is extracted locally, a rolling ten-second window of
/// landmarks is matched against the catalogue every second or two, and recognised tracks are reported with their position and confidence.
/// </summary>
public sealed class IdentifySession(DemoCatalogue catalogue) : IPeakObserver
{
	private const double MatchIntervalSeconds = 1.5;
	private const int WindowSeconds = 12;

	private readonly List<HeardSegment> _history = [];
	private readonly List<SpectralPeak> _peaks = [];
	private Fingerprinter? _fingerprinter;
	private RollingLandmarkWindow? _window;
	private int _rate;
	private double _lastMatchAt;
	private bool _matching;

	/// <summary>Raised when the state changes.</summary>
	public event Action? Changed;

	/// <summary>The best current match, or null when nothing is recognised.</summary>
	public Match? Best { get; private set; }

	/// <summary>Seconds of audio heard so far.</summary>
	public double HeardSeconds { get; private set; }

	/// <summary>Tracks recognised during this session, in order.</summary>
	public IReadOnlyList<HeardSegment> History => _history;

	/// <summary>Frame number of the newest analysed audio.</summary>
	public int LatestFrame => _window?.LatestFrame ?? 0;

	/// <summary>The extraction options, which fix the frame duration and window length.</summary>
	public FingerprintOptions Options => catalogue.Options;

	/// <summary>Length of the matching window in frames.</summary>
	public int WindowFrames => (int)(TimeSpan.FromSeconds(WindowSeconds).Ticks / catalogue.Options.FrameDuration.Ticks);

	/// <summary>Peaks seen in the current window, for drawing the constellation.</summary>
	public IReadOnlyList<SpectralPeak> RecentPeaks
	{
		get
		{
			var cutoff = LatestFrame - WindowFrames;
			_peaks.RemoveAll(p => p.Frame < cutoff);
			return _peaks;
		}
	}

	/// <summary>The live spectrum feeding the display, or null before any audio.</summary>
	public LiveSpectrum? Spectrum { get; private set; }

	/// <summary>The landmarks in the current window, as pairs of peaks with their times and frequencies, for drawing.</summary>
	public IReadOnlyList<(int AnchorFrame, int AnchorBin, int PartnerFrame, int PartnerBin)> RecentLandmarks =>
		_window is null
			? []
			: [.. _window.Snapshot().Select(l =>
			{
				var bin = (int)((l.Hash >> 14) & 0x1FF);
				var deltaBins = (int)((l.Hash >> 6) & 0xFF) - 128;
				return (l.AnchorFrame, bin, l.AnchorFrame + l.DeltaFrames, bin + deltaBins);
			})];

	/// <inheritdoc />
	public void OnPeaks(ReadOnlySpan<SpectralPeak> peaks) => _peaks.AddRange(peaks);

	/// <summary>Starts again with no audio heard.</summary>
	public void Reset()
	{
		_fingerprinter = null;
		Spectrum = null;
		_window = null;
		_peaks.Clear();
		_history.Clear();
		Best = null;
		HeardSeconds = 0;
		_lastMatchAt = 0;
		Changed?.Invoke();
	}

	/// <summary>Feeds 16-bit little-endian mono PCM at the given sample rate.</summary>
	/// <param name="pcm">The samples.</param>
	/// <param name="sampleRate">Their sample rate in hertz.</param>
	public async Task PushPcmAsync(byte[] pcm, int sampleRate)
	{
		if (_fingerprinter is null || _rate != sampleRate)
		{
			_rate = sampleRate;
			_fingerprinter = new Fingerprinter(sampleRate, catalogue.Options) { PeakObserver = this };
			_window = new RollingLandmarkWindow(TimeSpan.FromSeconds(WindowSeconds), catalogue.Options);
			Spectrum = new LiveSpectrum(catalogue.Options.WindowSize, catalogue.Options.HopSize, catalogue.Options.SampleRate, sampleRate);
		}

		var samples = new float[pcm.Length / 2];
		for (var i = 0; i < samples.Length; i++)
		{
			samples[i] = BitConverter.ToInt16(pcm, i * 2) / 32768f;
		}

		_fingerprinter.Push(samples, _window!);
		Spectrum!.Push(samples);
		HeardSeconds += (double)samples.Length / sampleRate;

		if (!_matching && HeardSeconds - _lastMatchAt >= MatchIntervalSeconds && _window!.Count > 0)
		{
			_matching = true;
			_lastMatchAt = HeardSeconds;
			try
			{
				var matches = await catalogue.Matcher.MatchAsync(_window.Snapshot(), null, CancellationToken.None);
				Update(matches.Count > 0 ? matches[0] : null);
			}
			finally
			{
				_matching = false;
			}
		}

		Changed?.Invoke();
	}

	private void Update(Match? best)
	{
		Best = best;
		if (best is null)
		{
			return;
		}

		if (_history.Count > 0 && _history[^1].TrackId == best.TrackId)
		{
			_history[^1] = _history[^1] with { LastHeardAt = HeardSeconds };
		}
		else
		{
			_history.Add(new HeardSegment(best.TrackId, HeardSeconds, HeardSeconds, best.TrackStart));
		}
	}
}

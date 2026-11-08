namespace PanoramicData.SoundPrint;

/// <summary>
/// Streaming landmark extractor: resample, short-time Fourier transform, local-maximum peak picking with band balancing and a
/// per-block density cap, then pairing of each anchor peak with the peaks in a forward target zone into 32-bit hashes.
/// Every buffer is allocated in the constructor, so memory is constant however long the stream runs.
/// </summary>
public sealed partial class Fingerprinter : IFingerprinter
{
	private const int BatchSize = 256;
	private const int CompactThreshold = 4096;

	private readonly FingerprintOptions _options;
	private readonly StreamingResampler? _resampler;
	private readonly Fft _fft;
	private readonly double[] _window;
	private readonly double[] _re;
	private readonly double[] _im;
	private readonly float[] _samples;
	private readonly double[][] _power;
	private readonly byte[] _bandOfBin;
	private readonly List<Peak> _candidates = [];
	private readonly List<Peak> _peaks = [];
	private readonly Landmark[] _batch = new Landmark[BatchSize];
	private int _peakHead;
	private int _batchCount;
	private long _sampleCount;
	private long _nextCandidateFrame;
	private long _block;
	private bool _flushed;
	private ILandmarkSink? _sink;

	/// <summary>Creates a fingerprinter for a stream at the given sample rate.</summary>
	/// <param name="inputSampleRate">Sample rate of the audio that will be pushed, in hertz. It is resampled to <see cref="FingerprintOptions.SampleRate"/>.</param>
	/// <param name="options">Extraction options.</param>
	/// <exception cref="ArgumentException">The options are invalid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The sample rate is not positive.</exception>
	public Fingerprinter(int inputSampleRate, FingerprintOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options;
		_options.Validate();
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(inputSampleRate, 0);

		if (inputSampleRate != _options.SampleRate)
		{
			_resampler = new StreamingResampler(inputSampleRate, _options.SampleRate, _options.ResamplerHalfTaps);
		}

		var size = _options.WindowSize;
		_fft = new Fft(size);
		_window = new double[size];
		for (var i = 0; i < size; i++)
		{
			_window[i] = 0.5 - (0.5 * DeterministicMath.Cos(2 * Math.PI * i / size));
		}

		_re = new double[size];
		_im = new double[size];
		_samples = new float[size];
		_power = new double[(2 * _options.PeakNeighbourhoodFrames) + 1][];
		for (var i = 0; i < _power.Length; i++)
		{
			_power[i] = new double[_options.BinCount];
		}

		_bandOfBin = new byte[_options.BinCount];
		for (var band = 0; band < _options.BandEdges.Count - 1; band++)
		{
			for (var bin = _options.BandEdges[band]; bin < _options.BandEdges[band + 1]; bin++)
			{
				_bandOfBin[bin] = (byte)band;
			}
		}
	}

	/// <summary>Creates a fingerprinter with default options for a stream at the given sample rate.</summary>
	/// <param name="inputSampleRate">Sample rate of the audio that will be pushed, in hertz.</param>
	public Fingerprinter(int inputSampleRate)
		: this(inputSampleRate, new FingerprintOptions())
	{
	}

	/// <summary>Receives the spectral peaks the fingerprinter keeps, when set. Null by default, in which case nothing extra is computed or allocated.</summary>
	public IPeakObserver? PeakObserver { get; set; }

	/// <inheritdoc />
	public long FramesProcessed { get; private set; }

	/// <summary>
	/// Fingerprints a complete recording in one call.
	/// </summary>
	/// <param name="samples">Mono samples.</param>
	/// <param name="sampleRate">Sample rate of <paramref name="samples"/> in hertz.</param>
	/// <param name="options">Extraction options.</param>
	/// <returns>All landmarks of the recording.</returns>
	public static IReadOnlyList<Landmark> Fingerprint(ReadOnlySpan<float> samples, int sampleRate, FingerprintOptions options)
	{
		var fingerprinter = new Fingerprinter(sampleRate, options);
		var sink = new ListLandmarkSink();
		fingerprinter.Push(samples, sink);
		fingerprinter.Flush(sink);
		return sink.Landmarks;
	}

	/// <summary>Fingerprints a complete recording in one call with default options.</summary>
	/// <param name="samples">Mono samples.</param>
	/// <param name="sampleRate">Sample rate of <paramref name="samples"/> in hertz.</param>
	/// <returns>All landmarks of the recording.</returns>
	public static IReadOnlyList<Landmark> Fingerprint(ReadOnlySpan<float> samples, int sampleRate) =>
		Fingerprint(samples, sampleRate, new FingerprintOptions());

	/// <inheritdoc />
	public void Push(ReadOnlySpan<float> samples, ILandmarkSink sink)
	{
		ArgumentNullException.ThrowIfNull(sink);
		if (_flushed)
		{
			throw new InvalidOperationException("The stream has been flushed; call Reset before pushing more audio.");
		}

		_sink = sink;
		if (_resampler is null)
		{
			foreach (var sample in samples)
			{
				OnSample(sample);
			}
		}
		else
		{
			foreach (var sample in samples)
			{
				_resampler.Push(sample);
				while (_resampler.TryRead(out var resampled))
				{
					OnSample(resampled);
				}
			}
		}

		DeliverBatch();
		_sink = null;
	}

	/// <inheritdoc />
	public void Flush(ILandmarkSink sink)
	{
		ArgumentNullException.ThrowIfNull(sink);
		if (_flushed)
		{
			return;
		}

		_sink = sink;
		if (_resampler is not null)
		{
			_resampler.End();
			while (_resampler.TryRead(out var resampled))
			{
				OnSample(resampled);
			}
		}

		ProcessCandidates(final: true);
		if (_candidates.Count > 0)
		{
			FinalizeBlock();
		}

		EmitAnchors(long.MaxValue, final: true);
		DeliverBatch();
		_sink = null;
		_flushed = true;
	}

	/// <inheritdoc />
	public void Reset()
	{
		_resampler?.Reset();
		_candidates.Clear();
		_peaks.Clear();
		_peakHead = 0;
		_batchCount = 0;
		_sampleCount = 0;
		FramesProcessed = 0;
		_nextCandidateFrame = 0;
		_block = 0;
		_flushed = false;
		_sink = null;
	}

	private void OnSample(float sample)
	{
		var size = _options.WindowSize;
		_samples[(int)(_sampleCount & (size - 1))] = sample;
		_sampleCount++;
		if (_sampleCount >= size && (_sampleCount - size) % _options.HopSize == 0)
		{
			ComputeFrame();
			ProcessCandidates(final: false);
		}
	}

	private void ComputeFrame()
	{
		var size = _options.WindowSize;
		var start = _sampleCount - size;
		for (var i = 0; i < size; i++)
		{
			_re[i] = _samples[(int)((start + i) & (size - 1))] * _window[i];
			_im[i] = 0;
		}

		_fft.Transform(_re, _im);
		var row = _power[(int)(FramesProcessed % _power.Length)];
		for (var k = 0; k < row.Length; k++)
		{
			row[k] = (_re[k] * _re[k]) + (_im[k] * _im[k]);
		}

		FramesProcessed++;
	}
}

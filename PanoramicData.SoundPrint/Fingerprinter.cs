using System.Runtime.InteropServices;

namespace PanoramicData.SoundPrint;

/// <summary>
/// Streaming landmark extractor: resample, short-time Fourier transform, local-maximum peak picking with band balancing and a
/// per-block density cap, then pairing of each anchor peak with the peaks in a forward target zone into 32-bit hashes.
/// Every buffer is allocated in the constructor, so memory is constant however long the stream runs.
/// </summary>
public sealed class Fingerprinter : IFingerprinter
{
	private const int BatchSize = 256;
	private const int CompactThreshold = 4096;

	private static readonly Comparison<Peak> ByBandThenPower = static (a, b) =>
	{
		var c = a.Band.CompareTo(b.Band);
		if (c != 0) { return c; }
		c = b.Power.CompareTo(a.Power);
		if (c != 0) { return c; }
		c = a.Frame.CompareTo(b.Frame);
		return c != 0 ? c : a.Bin.CompareTo(b.Bin);
	};

	private static readonly Comparison<Peak> ByFrameThenBin = static (a, b) =>
	{
		var c = a.Frame.CompareTo(b.Frame);
		return c != 0 ? c : a.Bin.CompareTo(b.Bin);
	};

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
	/// <param name="options">Extraction options; <see cref="FingerprintOptions"/> defaults when null.</param>
	/// <exception cref="ArgumentException">The options are invalid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The sample rate is not positive.</exception>
	public Fingerprinter(int inputSampleRate, FingerprintOptions? options = null)
	{
		_options = options ?? new FingerprintOptions();
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

	/// <inheritdoc />
	public long FramesProcessed { get; private set; }

	/// <summary>
	/// Fingerprints a complete recording in one call.
	/// </summary>
	/// <param name="samples">Mono samples.</param>
	/// <param name="sampleRate">Sample rate of <paramref name="samples"/> in hertz.</param>
	/// <param name="options">Extraction options; defaults when null.</param>
	/// <returns>All landmarks of the recording.</returns>
	public static IReadOnlyList<Landmark> Fingerprint(ReadOnlySpan<float> samples, int sampleRate, FingerprintOptions? options = null)
	{
		var fingerprinter = new Fingerprinter(sampleRate, options);
		var sink = new ListLandmarkSink();
		fingerprinter.Push(samples, sink);
		fingerprinter.Flush(sink);
		return sink.Landmarks;
	}

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

	private void ProcessCandidates(bool final)
	{
		var lookahead = final ? 0 : _options.PeakNeighbourhoodFrames;
		while (_nextCandidateFrame <= FramesProcessed - 1 - lookahead)
		{
			EvaluateFrame(_nextCandidateFrame);
			_nextCandidateFrame++;
		}
	}

	private void EvaluateFrame(long frame)
	{
		var block = frame / _options.DensityBlockFrames;
		while (_block < block)
		{
			FinalizeBlock();
		}

		var row = _power[(int)(frame % _power.Length)];
		var lastBin = _options.BinCount - 2;
		for (var bin = _options.MinPeakBin; bin <= lastBin; bin++)
		{
			var value = row[bin];
			if (value > _options.MinPeakPower && IsLocalMaximum(frame, bin, value))
			{
				_candidates.Add(new Peak(checked((int)frame), bin, _bandOfBin[bin], value));
			}
		}
	}

	private bool IsLocalMaximum(long frame, int bin, double value)
	{
		var frameRange = _options.PeakNeighbourhoodFrames;
		var binRange = _options.PeakNeighbourhoodBins;
		var binCount = _options.BinCount;
		for (var dt = -frameRange; dt <= frameRange; dt++)
		{
			var other = frame + dt;
			if (other < 0 || other >= FramesProcessed)
			{
				continue;
			}

			var row = _power[(int)(other % _power.Length)];
			var from = Math.Max(0, bin - binRange);
			var to = Math.Min(binCount - 1, bin + binRange);
			for (var other2 = from; other2 <= to; other2++)
			{
				if (dt == 0 && other2 == bin)
				{
					continue;
				}

				var neighbour = row[other2];
				// Ties go to the earlier position, so a plateau yields exactly one peak.
				if (neighbour > value || (neighbour == value && NeighbourIsEarlier(dt, other2, bin)))
				{
					return false;
				}
			}
		}

		return true;
	}

	/// <summary>Ties between equal peaks go to the earlier position in (frame, bin) order, so a plateau yields exactly one peak.</summary>
	internal static bool NeighbourIsEarlier(int frameOffset, int neighbourBin, int bin) =>
		frameOffset < 0 || (frameOffset == 0 && neighbourBin < bin);

	private void FinalizeBlock()
	{
		var span = CollectionsMarshal.AsSpan(_candidates);
		span.Sort(ByBandThenPower);
		var kept = 0;
		var inBand = 0;
		var band = -1;
		for (var i = 0; i < span.Length; i++)
		{
			if (span[i].Band != band)
			{
				band = span[i].Band;
				inBand = 0;
			}

			if (inBand < _options.PeaksPerBandPerBlock)
			{
				span[kept++] = span[i];
				inBand++;
			}
		}

		var keptSpan = span[..kept];
		keptSpan.Sort(ByFrameThenBin);
		_peaks.AddRange(keptSpan);
		_candidates.Clear();
		var horizon = ((_block + 1) * _options.DensityBlockFrames) - 1;
		_block++;
		EmitAnchors(horizon, final: false);
	}

	private void EmitAnchors(long horizon, bool final)
	{
		var maxDelta = _options.MaxDeltaFrames;
		while (_peakHead < _peaks.Count)
		{
			var anchor = _peaks[_peakHead];
			if (!final && anchor.Frame + maxDelta > horizon)
			{
				break;
			}

			var taken = 0;
			for (var j = _peakHead + 1; j < _peaks.Count && taken < _options.FanOut; j++)
			{
				var partner = _peaks[j];
				var deltaFrames = partner.Frame - anchor.Frame;
				if (deltaFrames > maxDelta)
				{
					break;
				}

				var deltaBins = partner.Bin - anchor.Bin;
				if (deltaFrames < 1 || deltaBins is < -128 or > 127)
				{
					continue;
				}

				var hash = ((uint)anchor.Bin << 14) | ((uint)(deltaBins + 128) << 6) | (uint)deltaFrames;
				AddLandmark(new Landmark(hash, anchor.Frame));
				taken++;
			}

			_peakHead++;
		}

		if (_peakHead >= CompactThreshold)
		{
			_peaks.RemoveRange(0, _peakHead);
			_peakHead = 0;
		}
	}

	private void AddLandmark(Landmark landmark)
	{
		if (_batchCount == _batch.Length)
		{
			DeliverBatch();
		}

		_batch[_batchCount++] = landmark;
	}

	private void DeliverBatch()
	{
		if (_batchCount > 0)
		{
			_sink!.Add(_batch.AsSpan(0, _batchCount));
			_batchCount = 0;
		}
	}

	private readonly record struct Peak(int Frame, int Bin, int Band, double Power);
}

namespace PanoramicData.SoundPrint;

/// <summary>
/// Streaming windowed-sinc resampler between two integer sample rates. Output sample n sits at input position n * inRate / outRate,
/// computed in integer arithmetic, so the output depends only on the input values and never on how they were chunked.
/// Per-phase kernels are built once with <see cref="DeterministicMath"/>; each is normalised to unit gain at DC.
/// </summary>
internal sealed class StreamingResampler
{
	private readonly long _inRate;
	private readonly long _outRate;
	private readonly int _phaseDivisor;
	private readonly int _halfWidth;
	private readonly int _taps;
	private readonly double[] _kernels;
	private readonly float[] _history;
	private readonly int _mask;
	private long _inputCount;
	private long _position;
	private long _lastIndex = long.MaxValue;

	public StreamingResampler(int inRate, int outRate, int halfTaps)
	{
		_inRate = inRate;
		_outRate = outRate;
		_phaseDivisor = Gcd(inRate, outRate);
		var phases = outRate / _phaseDivisor;
		var cutoff = Math.Min(1.0, (double)outRate / inRate);
		_halfWidth = (int)Math.Ceiling(halfTaps / cutoff);
		_taps = 2 * _halfWidth;
		_kernels = new double[phases * _taps];
		for (var phase = 0; phase < phases; phase++)
		{
			BuildKernel(phase, phases, cutoff);
		}

		var size = 1;
		// End() pushes half a kernel of zeros before the pending outputs are read, so the ring must cover three half-widths.
		while (size < (4 * _halfWidth) + 2)
		{
			size <<= 1;
		}

		_history = new float[size];
		_mask = size - 1;
	}

	/// <summary>Appends one input sample.</summary>
	public void Push(float sample)
	{
		_history[(int)(_inputCount & _mask)] = sample;
		_inputCount++;
	}

	/// <summary>Reads the next output sample if enough input has arrived.</summary>
	public bool TryRead(out float sample)
	{
		var index = _position / _outRate;
		if (index + _halfWidth >= _inputCount || index > _lastIndex)
		{
			sample = 0;
			return false;
		}

		var phase = (int)(_position % _outRate / _phaseDivisor);
		var kernelStart = phase * _taps;
		var first = index - _halfWidth + 1;
		double sum = 0;
		for (var t = 0; t < _taps; t++)
		{
			var i = first + t;
			var slot = (int)(((i % _history.Length) + _history.Length) & _mask);
			sum += _history[slot] * _kernels[kernelStart + t];
		}

		sample = (float)sum;
		_position += _inRate;
		return true;
	}

	/// <summary>
	/// Ends the stream: appends zeros so the final real samples are fully covered by the kernel, and stops output at the last real
	/// input position.
	/// </summary>
	public void End()
	{
		_lastIndex = _inputCount - 1;
		for (var i = 0; i < _halfWidth; i++)
		{
			Push(0);
		}
	}

	public void Reset()
	{
		Array.Clear(_history);
		_inputCount = 0;
		_position = 0;
		_lastIndex = long.MaxValue;
	}

	private void BuildKernel(int phase, int phases, double cutoff)
	{
		var fraction = (double)phase / phases;
		var start = phase * _taps;
		double total = 0;
		for (var t = 0; t < _taps; t++)
		{
			// Tap t reads input index (index - halfWidth + 1 + t); its distance from the output position is d input samples.
			var d = (t - _halfWidth + 1) - fraction;
			var x = d * cutoff;
			var sinc = Math.Abs(x) < 1e-12 ? 1.0 : DeterministicMath.Sin(Math.PI * x) / (Math.PI * x);
			var window = 0.5 + (0.5 * DeterministicMath.Cos(Math.PI * d / _halfWidth));
			var weight = cutoff * sinc * window;
			_kernels[start + t] = weight;
			total += weight;
		}

		for (var t = 0; t < _taps; t++)
		{
			_kernels[start + t] /= total;
		}
	}

	private static int Gcd(long a, long b)
	{
		while (b != 0)
		{
			(a, b) = (b, a % b);
		}

		return (int)a;
	}
}

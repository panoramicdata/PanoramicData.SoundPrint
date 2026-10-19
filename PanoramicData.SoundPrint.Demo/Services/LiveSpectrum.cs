namespace PanoramicData.SoundPrint.Demo.Services;

/// <summary>
/// Turns a stream of samples into spectrogram columns as they arrive, on the same frame grid as the fingerprinter (a column every
/// <c>hop</c> samples), so that peaks and landmarks line up with the picture. Levels are scaled over an 80 dB range below the loudest
/// sound heard so far.
/// </summary>
/// <param name="window">The analysis window in samples; a power of two.</param>
/// <param name="hop">Samples between columns.</param>
/// <param name="sampleRate">The sample rate in hertz that the window and hop are measured at (the fingerprinter's rate).</param>
/// <param name="inputRate">The sample rate in hertz of the audio pushed in, which is averaged down to <paramref name="sampleRate"/>.</param>
public sealed class LiveSpectrum(int window, int hop, int sampleRate, int inputRate)
{
	private const double RangeDecibels = 80;
	private readonly List<float> _buffer = [];
	private readonly List<byte> _pending = [];
	private double _reference;
	private double _phase;
	private double _sum;
	private int _count;

	/// <summary>Gets the number of frequency values in each column.</summary>
	public int Bins => (window / 2) + 1;

	/// <summary>Gets the time each column covers.</summary>
	public TimeSpan ColumnDuration => TimeSpan.FromSeconds((double)hop / sampleRate);

	/// <summary>Gets the highest frequency a column reaches, in hertz.</summary>
	public double MaxFrequency => sampleRate / 2.0;

	/// <summary>Adds samples and queues a column for every hop that is now complete.</summary>
	/// <param name="samples">Mono samples.</param>
	public void Push(ReadOnlySpan<float> samples)
	{
		var step = (double)inputRate / sampleRate;
		foreach (var sample in samples)
		{
			_sum += sample;
			_count++;
			_phase += 1;
			if (_phase >= step)
			{
				_phase -= step;
				_buffer.Add((float)(_sum / _count));
				_sum = 0;
				_count = 0;
			}
		}

		if (_buffer.Count < window)
		{
			return;
		}

		var (power, frames, bins, maxDb) = SpectrogramMaker.Decibels(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_buffer), window, hop);
		_reference = Math.Max(_reference, maxDb);
		for (var i = 0; i < power.Length; i++)
		{
			_pending.Add((byte)Math.Clamp((power[i] - (_reference - RangeDecibels)) / RangeDecibels * 255, 0, 255));
		}

		_buffer.RemoveRange(0, frames * hop);
		_ = bins;
	}

	/// <summary>Takes the columns queued since the last call, one after another.</summary>
	/// <returns>The levels, <see cref="Bins"/> bytes per column; empty when there is nothing new.</returns>
	public byte[] Take()
	{
		var levels = _pending.ToArray();
		_pending.Clear();
		return levels;
	}

	/// <summary>Forgets all audio and queued columns.</summary>
	public void Reset()
	{
		_buffer.Clear();
		_pending.Clear();
		_reference = 0;
		_phase = 0;
		_sum = 0;
		_count = 0;
	}
}

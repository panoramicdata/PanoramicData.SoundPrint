namespace PanoramicData.SoundPrint;

/// <summary>
/// An <see cref="ILandmarkSink"/> that keeps only the most recent stretch of a stream's landmarks, so a live stream can be matched
/// window by window with bounded memory. Landmark frames keep the stream's own clock, so match times are on the stream clock too
/// (add the wall-clock UTC time at which the stream's frame 0 began to get absolute times). Thread-safe: one thread may push audio
/// while another takes snapshots.
/// </summary>
public sealed class RollingLandmarkWindow : ILandmarkSink
{
	private readonly object _gate = new();
	private readonly Queue<Landmark> _landmarks = new();
	private readonly int _lengthFrames;
	private int _latestFrame = -1;

	/// <summary>Creates a window.</summary>
	/// <param name="length">How much of the stream to keep, measured back from its newest landmark.</param>
	/// <param name="options">The extraction options of the fingerprinter feeding the window, which define the frame duration.</param>
	/// <exception cref="ArgumentOutOfRangeException">The length is shorter than one frame.</exception>
	public RollingLandmarkWindow(TimeSpan length, FingerprintOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_lengthFrames = (int)(length.Ticks / options.FrameDuration.Ticks);
		ArgumentOutOfRangeException.ThrowIfLessThan(_lengthFrames, 1, nameof(length));
	}

	/// <summary>The number of landmarks currently held.</summary>
	public int Count
	{
		get
		{
			lock (_gate)
			{
				return _landmarks.Count;
			}
		}
	}

	/// <summary>The frame of the newest landmark seen, or -1 before any.</summary>
	public int LatestFrame
	{
		get
		{
			lock (_gate)
			{
				return _latestFrame;
			}
		}
	}

	/// <inheritdoc />
	public void Add(ReadOnlySpan<Landmark> landmarks)
	{
		lock (_gate)
		{
			foreach (var landmark in landmarks)
			{
				_landmarks.Enqueue(landmark);
				_latestFrame = Math.Max(_latestFrame, landmark.AnchorFrame);
			}

			while (_landmarks.Count > 0 && _landmarks.Peek().AnchorFrame <= _latestFrame - _lengthFrames)
			{
				_landmarks.Dequeue();
			}
		}
	}

	/// <summary>A copy of the landmarks currently held, oldest first, ready to pass to a matcher.</summary>
	/// <returns>The landmarks of the last window length.</returns>
	public IReadOnlyList<Landmark> Snapshot()
	{
		lock (_gate)
		{
			return [.. _landmarks];
		}
	}

	/// <summary>Discards everything held, for use when the stream restarts.</summary>
	public void Clear()
	{
		lock (_gate)
		{
			_landmarks.Clear();
			_latestFrame = -1;
		}
	}
}

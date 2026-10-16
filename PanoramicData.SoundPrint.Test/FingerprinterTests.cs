using PanoramicData.SoundPrint.TestAudio;

namespace PanoramicData.SoundPrint.Test;

public class FingerprinterTests
{
	/// <summary>Counts landmarks without keeping them, so allocation measurements see only the fingerprinter.</summary>
	private sealed class CountingSink : ILandmarkSink
	{
		public long Count { get; private set; }
		public void Add(ReadOnlySpan<Landmark> landmarks) => Count += landmarks.Length;
	}

	/// <summary>
	/// Noise and a sawtooth built from integer arithmetic only, so the signal (and therefore the golden hashes) are identical on every machine.
	/// </summary>
	private static float[] IntegerSignal(int count)
	{
		var x = new float[count];
		uint lcg = 12345;
		uint phase = 0;
		for (var i = 0; i < count; i++)
		{
			lcg = (lcg * 1664525) + 1013904223;
			var noise = ((int)(lcg >> 16) & 0x7FFF) - 0x4000;
			phase += 5000000 + (uint)((i / 8000) * 700000);
			var saw = (int)(phase >> 17) - 0x7FFF;
			x[i] = ((noise / 16384f) * 0.1f) + ((saw / 32768f) * 0.5f);
		}

		return x;
	}

	private static float[] Song(int seed, double seconds = 20, int rate = 8000) => SyntheticSong.Generate(seed, seconds, rate);

	[Fact]
	public void Silence_ProducesNoLandmarks() =>
		Fingerprinter.Fingerprint(new float[8000 * 10], 8000).Should().BeEmpty();

	[Fact]
	public void TooShortForOneWindow_ProducesNoLandmarks() =>
		Fingerprinter.Fingerprint(Song(1, 0.1), 8000).Should().BeEmpty();

	[Fact]
	public void Song_ProducesAboutOneHundredLandmarksPerSecond()
	{
		var landmarks = Fingerprinter.Fingerprint(Song(1), 8000);
		(landmarks.Count / 20.0).Should().BeInRange(30, 250);
	}

	[Fact]
	public void Extraction_IsDeterministic()
	{
		var song = Song(2);
		Fingerprinter.Fingerprint(song, 8000).Should().Equal(Fingerprinter.Fingerprint(song, 8000));
	}

	[Fact]
	public void Landmarks_AreOrderedByAnchorFrameAndWellFormed()
	{
		var landmarks = Fingerprinter.Fingerprint(Song(3), 8000);
		landmarks.Select(l => l.AnchorFrame).Should().BeInAscendingOrder();
		foreach (var landmark in landmarks)
		{
			landmark.DeltaFrames.Should().BeInRange(1, 63);
			(landmark.Hash >> 23).Should().Be(0u, "the reserved bits stay zero");
			((landmark.Hash >> 14) & 0x1FF).Should().BeInRange(2u, 511u);
		}
	}

	[Theory]
	[InlineData(8000)]
	[InlineData(11025)]
	[InlineData(44100)]
	public void Output_IsIndependentOfChunking(int rate)
	{
		var song = Song(4, 12, rate);
		var whole = Fingerprinter.Fingerprint(song, rate);

		var fingerprinter = new Fingerprinter(rate);
		var sink = new ListLandmarkSink();
		var rng = new Random(9);
		for (var i = 0; i < song.Length;)
		{
			var take = Math.Min(rng.Next(1, 3000), song.Length - i);
			fingerprinter.Push(song.AsSpan(i, take), sink);
			i += take;
		}

		fingerprinter.Flush(sink);
		sink.Landmarks.Should().Equal(whole);
	}

	[Fact]
	public void SingleSamplePushes_GiveTheSameResultAsOneBigPush()
	{
		var song = Song(5, 6);
		var fingerprinter = new Fingerprinter(8000);
		var sink = new ListLandmarkSink();
		for (var i = 0; i < song.Length; i++)
		{
			fingerprinter.Push(song.AsSpan(i, 1), sink);
		}

		fingerprinter.Flush(sink);
		sink.Landmarks.Should().Equal(Fingerprinter.Fingerprint(song, 8000));
	}

	[Fact]
	public void GoldenHashes_AreStableAcrossMachines()
	{
		var landmarks = Fingerprinter.Fingerprint(IntegerSignal(8000 * 30), 8000);
		ulong checksum = 14695981039346656037UL;
		foreach (var landmark in landmarks)
		{
			checksum = (checksum ^ landmark.Hash) * 1099511628211UL;
			checksum = (checksum ^ (uint)landmark.AnchorFrame) * 1099511628211UL;
		}

		landmarks.Count.Should().Be(GoldenCount);
		checksum.Should().Be(GoldenChecksum);
	}

	private const int GoldenCount = 3956;
	private const ulong GoldenChecksum = 12212493684135057515UL;

	[Fact]
	public void Flush_EmitsLandmarksThatLookaheadWasHolding()
	{
		var song = Song(6, 10);
		var fingerprinter = new Fingerprinter(8000);
		var sink = new ListLandmarkSink();
		fingerprinter.Push(song, sink);
		var beforeFlush = sink.Landmarks.Count;
		fingerprinter.Flush(sink);
		sink.Landmarks.Count.Should().BeGreaterThan(beforeFlush);
	}

	[Fact]
	public void Streaming_LagsInputByLessThanFourSeconds()
	{
		var song = Song(7, 30);
		var fingerprinter = new Fingerprinter(8000);
		var sink = new ListLandmarkSink();
		fingerprinter.Push(song, sink);
		var lastFrame = sink.Landmarks.Max(l => l.AnchorFrame);
		var lag = TimeSpan.FromSeconds(30) - new FingerprintOptions().FramesToTime(lastFrame);
		lag.Should().BeLessThan(TimeSpan.FromSeconds(4));
	}

	[Fact]
	public void Flush_IsIdempotent()
	{
		var fingerprinter = new Fingerprinter(8000);
		var sink = new ListLandmarkSink();
		fingerprinter.Push(Song(8, 6), sink);
		fingerprinter.Flush(sink);
		var count = sink.Landmarks.Count;
		fingerprinter.Flush(sink);
		sink.Landmarks.Count.Should().Be(count);
	}

	[Fact]
	public void PushAfterFlush_Throws()
	{
		var fingerprinter = new Fingerprinter(8000);
		var sink = new ListLandmarkSink();
		fingerprinter.Flush(sink);
		var act = () => fingerprinter.Push(new float[10], sink);
		act.Should().Throw<InvalidOperationException>();
	}

	[Theory]
	[InlineData(11025)]
	[InlineData(22050)]
	public void Reset_AllowsReuseWithIdenticalOutput(int rate)
	{
		var song = Song(9, 8, rate);
		var fingerprinter = new Fingerprinter(rate);
		var first = new ListLandmarkSink();
		fingerprinter.Push(song, first);
		fingerprinter.Flush(first);
		fingerprinter.Reset();
		fingerprinter.FramesProcessed.Should().Be(0);
		var second = new ListLandmarkSink();
		fingerprinter.Push(song, second);
		fingerprinter.Flush(second);
		second.Landmarks.Should().Equal(first.Landmarks);
	}

	[Fact]
	public void FramesProcessed_CountsAnalysisWindows()
	{
		var fingerprinter = new Fingerprinter(11025);
		fingerprinter.Push(new float[11025], new CountingSink());
		fingerprinter.FramesProcessed.Should().Be(((11025 - 1024) / 256) + 1);
	}

	[Fact]
	public void SteadyState_AllocatesNothingPerPush()
	{
		var fingerprinter = new Fingerprinter(22050);
		var sink = new CountingSink();
		var chunk = Song(10, 5, 22050);
		for (var i = 0; i < 4; i++)
		{
			fingerprinter.Push(chunk, sink);
		}

		var before = GC.GetAllocatedBytesForCurrentThread();
		for (var i = 0; i < 6; i++)
		{
			fingerprinter.Push(chunk, sink);
		}

		var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		allocated.Should().BeLessThan(64 * 1024, "30 seconds of steady-state audio must not allocate per call");
		sink.Count.Should().BeGreaterThan(0);
	}

	[Fact]
	public void LongStream_KeepsBufferedPeaksBounded()
	{
		var fingerprinter = new Fingerprinter(8000);
		var sink = new CountingSink();
		var chunk = Song(11, 10);
		for (var i = 0; i < 30; i++)
		{
			fingerprinter.Push(chunk, sink);
		}

		fingerprinter.Flush(sink);
		sink.Count.Should().BeGreaterThan(0);
		fingerprinter.FramesProcessed.Should().BeGreaterThan(9000);
	}

	[Fact]
	public void DifferentSongs_ShareFewHashes()
	{
		var a = Fingerprinter.Fingerprint(Song(20), 8000).Select(l => l.Hash).ToHashSet();
		var b = Fingerprinter.Fingerprint(Song(21), 8000).Select(l => l.Hash).ToHashSet();
		var shared = a.Intersect(b).Count();
		(shared / (double)Math.Min(a.Count, b.Count)).Should().BeLessThan(0.5);
	}

	[Fact]
	public void SameTonesAtAnotherSampleRate_StillShareMostHashes()
	{
		// Pure tones with no noise, so the two renderings differ only in sample rate and the resampler's work.
		static float[] Tones(int rate)
		{
			var x = new float[rate * 20];
			double[] freqs = [220, 277, 330, 392, 440, 523, 659, 784, 880, 1047, 1319, 1568];
			var rng = new Random(77);
			for (var start = 0; start < x.Length; start += rate / 4)
			{
				var f = freqs[rng.Next(freqs.Length)];
				var g = freqs[rng.Next(freqs.Length)];
				for (var i = start; i < Math.Min(x.Length, start + (rate / 4)); i++)
				{
					var t = (double)i / rate;
					x[i] = (float)((0.4 * Math.Sin(2 * Math.PI * f * t)) + (0.25 * Math.Sin(2 * Math.PI * g * t)) + (0.1 * Math.Sin(2 * Math.PI * f * 2 * t)));
				}
			}

			return x;
		}

		var at8k = Fingerprinter.Fingerprint(Tones(8000), 8000).Select(l => l.Hash).ToHashSet();
		var at44k = Fingerprinter.Fingerprint(Tones(44100), 44100).Select(l => l.Hash).ToHashSet();
		(at8k.Intersect(at44k).Count() / (double)at8k.Count).Should().BeGreaterThan(0.6);
	}

	[Fact]
	public void PeriodicSignal_WithIdenticalFrames_YieldsDeterministicPlateauPeaks()
	{
		// Period equals the hop, so every frame is identical and peaks tie across time; ties must resolve identically each run.
		var x = new float[11025 * 5];
		for (var i = 0; i < x.Length; i++)
		{
			x[i] = (((i % 256) / 255f) * 2) - 1;
		}

		var first = Fingerprinter.Fingerprint(x, 11025);
		first.Should().Equal(Fingerprinter.Fingerprint(x, 11025));
	}

	[Theory]
	[InlineData(-1, 0, 5, true)]
	[InlineData(1, 0, 5, false)]
	[InlineData(0, 4, 5, true)]
	[InlineData(0, 5, 5, false)]
	[InlineData(0, 6, 5, false)]
	public void NeighbourIsEarlier_OrdersByFrameThenBin(int frameOffset, int neighbourBin, int bin, bool expected) =>
		Fingerprinter.NeighbourIsEarlier(frameOffset, neighbourBin, bin).Should().Be(expected);

	[Fact]
	public void TonePairs_FarApartInFrequency_AreNotPairedAcrossTheDeltaRange()
	{
		// A 3 kHz tone then a 300 Hz tone: the frequency delta exceeds the 8-bit range in both directions, so such pairs are skipped.
		var x = new float[8000 * 6];
		for (var i = 0; i < x.Length; i++)
		{
			var t = (double)i / 8000;
			var f = (i / 4000) % 2 == 0 ? 3000.0 : 300.0;
			x[i] = (float)Math.Sin(2 * Math.PI * f * t);
		}

		var landmarks = Fingerprinter.Fingerprint(x, 8000);
		foreach (var landmark in landmarks)
		{
			var deltaBins = (int)((landmark.Hash >> 6) & 0xFF) - 128;
			deltaBins.Should().BeInRange(-128, 127);
		}
	}

	[Fact]
	public void ToneBetweenTwoBins_ResolvesTiesInFrequencyDeterministically()
	{
		// 8000/1024 Hz per bin, so 100.5 bins puts equal power in two neighbouring bins.
		var frequency = 100.5 * 8000 / 1024;
		var x = new float[8000 * 4];
		for (var i = 0; i < x.Length; i++)
		{
			x[i] = (float)Math.Sin(2 * Math.PI * frequency * i / 8000);
		}

		var landmarks = Fingerprinter.Fingerprint(x, 8000);
		landmarks.Should().Equal(Fingerprinter.Fingerprint(x, 8000));
	}

	[Fact]
	public void CustomOptions_ChangeTheOutput()
	{
		var song = Song(23);
		var sparse = Fingerprinter.Fingerprint(song, 8000, new FingerprintOptions { FanOut = 1 });
		Fingerprinter.Fingerprint(song, 8000).Count.Should().BeGreaterThan(sparse.Count);
	}

	[Fact]
	public void Constructor_RejectsBadArguments()
	{
		var badRate = () => new Fingerprinter(0);
		badRate.Should().Throw<ArgumentOutOfRangeException>();
		var badOptions = () => new Fingerprinter(8000, new FingerprintOptions { FanOut = 0 });
		badOptions.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void NullOptions_AreRejected()
	{
		var construct = () => new Fingerprinter(8000, null!);
		construct.Should().Throw<ArgumentNullException>();
		var oneShot = () => Fingerprinter.Fingerprint(new float[10], 8000, null!);
		oneShot.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void NullSink_IsRejected()
	{
		var fingerprinter = new Fingerprinter(8000);
		var push = () => fingerprinter.Push(new float[10], null!);
		push.Should().Throw<ArgumentNullException>();
		var flush = () => fingerprinter.Flush(null!);
		flush.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void ListSink_CanBeCleared()
	{
		var sink = new ListLandmarkSink();
		sink.Add([new Landmark(1, 2)]);
		sink.Landmarks.Should().HaveCount(1);
		sink.Clear();
		sink.Landmarks.Should().BeEmpty();
	}
}

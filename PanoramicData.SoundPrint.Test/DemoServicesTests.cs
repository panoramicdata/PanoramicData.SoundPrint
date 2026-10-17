using PanoramicData.SoundPrint.Demo.Services;
using PanoramicData.SoundPrint.SyntheticAudio;

namespace PanoramicData.SoundPrint.Test;

/// <summary>Tests the non-UI services behind the browser demo.</summary>
public class DemoServicesTests
{
	private static byte[] ToPcm(ReadOnlySpan<float> samples)
	{
		var bytes = new byte[samples.Length * 2];
		for (var i = 0; i < samples.Length; i++)
		{
			BitConverter.TryWriteBytes(bytes.AsSpan(i * 2), (short)Math.Clamp(samples[i] * 32767, -32768, 32767));
		}

		return bytes;
	}

	private static async Task<DemoCatalogue> SeededAsync()
	{
		var catalogue = new DemoCatalogue();
		await catalogue.SeedAsync();
		return catalogue;
	}

	[Fact]
	public async Task Seeding_CreatesTheStartingTracksWithAudioAndReportsProgress()
	{
		var catalogue = new DemoCatalogue();
		var reports = new List<(int Done, int Total)>();
		var changed = 0;
		catalogue.Changed += () => changed++;

		await catalogue.SeedAsync(new SynchronousProgress(reports));
		await catalogue.SeedAsync();

		catalogue.Seeded.Should().BeTrue();
		catalogue.Tracks.Should().HaveCount(8);
		catalogue.Tracks.Should().OnlyContain(t => t.Samples != null && t.Landmarks > 0);
		catalogue.LandmarkCount.Should().BeGreaterThan(1000);
		reports.Last().Should().Be((8, 8));
		changed.Should().Be(1);
	}

	private sealed class SynchronousProgress(List<(int Done, int Total)> sink) : IProgress<(int Done, int Total)>
	{
		public void Report((int Done, int Total) value) => sink.Add(value);
	}

	[Fact]
	public async Task AddedTrack_IsFingerprintedAtItsOwnRateAndCanBeFoundAndRemoved()
	{
		var catalogue = await SeededAsync();
		var song = SyntheticSong.Generate(99, 20, 44100);
		var changed = 0;
		catalogue.Changed += () => changed++;

		var track = await catalogue.AddAsync("Mine", "Me", song, 44100);

		track.Samples.Should().BeNull();
		track.Seconds.Should().BeApproximately(20, 0.01);
		catalogue.Find(track.Id)!.Title.Should().Be("Mine");
		catalogue.Tracks.Should().HaveCount(9);

		catalogue.Remove(track.Id);
		catalogue.Remove(track.Id);
		catalogue.Find(track.Id).Should().BeNull();
		changed.Should().Be(2);
	}

	[Fact]
	public async Task Identify_RecognisesACatalogueTrackStreamedInOneSecondChunks()
	{
		var catalogue = await SeededAsync();
		var track = catalogue.Tracks[3];
		var session = new IdentifySession(catalogue);
		var changed = 0;
		session.Changed += () => changed++;

		var clip = track.Samples!.AsSpan(10 * DemoCatalogue.SampleRate, 15 * DemoCatalogue.SampleRate).ToArray();
		for (var start = 0; start < clip.Length; start += DemoCatalogue.SampleRate)
		{
			await session.PushPcmAsync(ToPcm(clip.AsSpan(start, Math.Min(DemoCatalogue.SampleRate, clip.Length - start))), DemoCatalogue.SampleRate);
		}

		session.Best.Should().NotBeNull();
		session.Best!.TrackId.Should().Be(track.Id);
		session.Best.TrackStart.TotalSeconds.Should().BeInRange(9, 17);
		session.HeardSeconds.Should().BeApproximately(15, 0.01);
		session.History.Should().ContainSingle().Which.TrackId.Should().Be(track.Id);
		session.RecentPeaks.Should().NotBeEmpty();
		session.WindowFrames.Should().BeGreaterThan(100);
		session.Options.Should().BeSameAs(catalogue.Options);
		changed.Should().Be(15);
	}

	[Fact]
	public async Task Identify_ExtendsTheSameSegmentWhileTheSameTrackKeepsMatching_AndAddsAnotherForANewTrack()
	{
		var catalogue = await SeededAsync();
		var session = new IdentifySession(catalogue);
		var rate = DemoCatalogue.SampleRate;

		foreach (var index in new[] { 1, 5 })
		{
			var clip = catalogue.Tracks[index].Samples!.AsSpan(5 * rate, 14 * rate).ToArray();
			await session.PushPcmAsync(ToPcm(clip), rate);
			// A further chunk keeps the same track matching, which extends its segment.
			await session.PushPcmAsync(ToPcm(catalogue.Tracks[index].Samples!.AsSpan(19 * rate, 4 * rate)), rate);
		}

		session.History.Select(h => h.TrackId).Should().Equal(catalogue.Tracks[1].Id, catalogue.Tracks[5].Id);
		session.History[0].LastHeardAt.Should().BeGreaterThanOrEqualTo(session.History[0].FirstHeardAt);
	}

	[Fact]
	public async Task Identify_ReportsNothingForSilenceAndRestartsWhenTheSampleRateChanges()
	{
		var catalogue = await SeededAsync();
		var session = new IdentifySession(catalogue);

		await session.PushPcmAsync(new byte[16000 * 2 * 4], 16000);
		session.Best.Should().BeNull();
		session.History.Should().BeEmpty();

		await session.PushPcmAsync(ToPcm(catalogue.Tracks[0].Samples!.AsSpan(0, 3 * DemoCatalogue.SampleRate)), DemoCatalogue.SampleRate);
		session.HeardSeconds.Should().BeApproximately(7, 0.01);
	}

	[Fact]
	public async Task Reset_ForgetsEverything()
	{
		var catalogue = await SeededAsync();
		var session = new IdentifySession(catalogue);
		var rate = DemoCatalogue.SampleRate;
		await session.PushPcmAsync(ToPcm(catalogue.Tracks[2].Samples!.AsSpan(4 * rate, 12 * rate)), rate);
		session.Best.Should().NotBeNull();

		session.Reset();

		session.Best.Should().BeNull();
		session.HeardSeconds.Should().Be(0);
		session.History.Should().BeEmpty();
		session.RecentPeaks.Should().BeEmpty();
		session.LatestFrame.Should().Be(0);
	}
}

public class ExplainLabTests
{
	private static async Task<(DemoCatalogue Catalogue, CatalogueTrack Track, ExplainLab Lab)> ArrangeAsync()
	{
		var catalogue = new DemoCatalogue();
		await catalogue.SeedAsync();
		return (catalogue, catalogue.Tracks[2], new ExplainLab(catalogue));
	}

	[Fact]
	public async Task BuildView_ReturnsASpectrogramPeaksAndPairsOnOneTimeline()
	{
		var (_, track, lab) = await ArrangeAsync();

		var view = lab.BuildView(track, 4, 12);

		view.Spectrogram.Bins.Should().Be(513);
		view.Spectrogram.Frames.Should().BeGreaterThan(400);
		view.Spectrogram.Image.Length.Should().Be(view.Spectrogram.Frames * 513);
		view.Spectrogram.Image.Should().Contain((byte)255);
		view.Peaks.Should().NotBeEmpty();
		view.Peaks.Should().OnlyContain(p => p.Frame >= 0 && p.Frame < view.Spectrogram.Frames);
		view.Pairs.Should().NotBeEmpty();
		view.Pairs.All(p => p.DeltaFrames is >= 1 and <= 63).Should().BeTrue();
		// Each pair's anchor is one of the peaks.
		var anchors = view.Peaks.Select(p => ((long)p.Frame << 16) | (uint)p.Bin).ToHashSet();
		view.Pairs.All(p => anchors.Contains(((long)p.AnchorFrame << 16) | (uint)p.AnchorBin)).Should().BeTrue();
	}

	[Fact]
	public async Task BuildView_RejectsATrackWithoutAudio()
	{
		var (_, _, lab) = await ArrangeAsync();
		var act = () => lab.BuildView(new CatalogueTrack("x", "x", "x", "x", 1, 1, null), 0, 12);
		act.Should().Throw<ArgumentException>();
		var clip = () => lab.RunClip(new CatalogueTrack("x", "x", "x", "x", 1, 1, null), 0, 0, 6);
		clip.Should().Throw<ArgumentException>();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(37)]
	[InlineData(128)]
	[InlineData(255)]
	public async Task Clip_AtAnySubFramePhase_StillVotesForTheTrueOffsetWithinOneFrame(int shift)
	{
		var (_, track, lab) = await ArrangeAsync();

		var trial = lab.RunClip(track, 9, shift, 6);

		trial.ClipLandmarks.Should().BeGreaterThan(100);
		trial.PeakVotes.Should().BeGreaterThan(20);
		Math.Abs(trial.FoundOffsetFrames - trial.TrueOffsetFrames).Should().BeLessThanOrEqualTo(1.5);
		trial.AtTrueOffset.Should().BeGreaterThanOrEqualTo(trial.PeakVotes - 5);
		trial.PeakVotes.Should().BeGreaterThan((int)(trial.BackgroundVotes * 10));
		trial.Histogram.Sum().Should().BeGreaterThanOrEqualTo(trial.PeakVotes);
	}

	[Fact]
	public async Task Sweep_CoversEightPhasesAndEveryOneFindsTheSpike()
	{
		var (_, track, lab) = await ArrangeAsync();

		var rows = lab.SweepPhases(track, 6, 6);

		rows.Select(r => r.ShiftSamples).Should().Equal(0, 32, 64, 96, 128, 160, 192, 224);
		rows.Should().OnlyContain(r => Math.Abs(r.FoundOffsetFrames - r.TrueOffsetFrames) <= 1.5 && r.PeakVotes > 20);
	}

	[Fact]
	public async Task A_clip_of_silence_reports_no_votes()
	{
		var catalogue = new DemoCatalogue();
		await catalogue.SeedAsync();
		var silent = new CatalogueTrack("silent", "s", "s", "s", 40, 1, new float[DemoCatalogue.SampleRate * 40]);
		var trial = new ExplainLab(catalogue).RunClip(silent, 2, 0, 6);
		trial.PeakVotes.Should().Be(0);
		trial.Histogram.Should().BeEmpty();
		trial.BackgroundVotes.Should().Be(0);
	}
}

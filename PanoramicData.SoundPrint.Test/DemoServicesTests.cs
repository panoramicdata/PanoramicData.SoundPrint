using PanoramicData.SoundPrint.Demo.Services;
using PanoramicData.SoundPrint.TestAudio;

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

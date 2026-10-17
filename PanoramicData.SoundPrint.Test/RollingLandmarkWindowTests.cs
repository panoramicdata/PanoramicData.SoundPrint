using PanoramicData.SoundPrint.SyntheticAudio;

namespace PanoramicData.SoundPrint.Test;

public class RollingLandmarkWindowTests
{
	private static readonly FingerprintOptions Options = new();

	[Fact]
	public void NewWindow_IsEmpty()
	{
		var window = new RollingLandmarkWindow(TimeSpan.FromSeconds(10), Options);
		window.Count.Should().Be(0);
		window.LatestFrame.Should().Be(-1);
		window.Snapshot().Should().BeEmpty();
	}

	[Fact]
	public void Window_KeepsOnlyTheRecentFrames()
	{
		var window = new RollingLandmarkWindow(TimeSpan.FromSeconds(1), Options);
		window.Add([.. Enumerable.Range(0, 200).Select(f => new Landmark((uint)f, f))]);

		var frames = Options.FramesToTime(1).Ticks;
		var keep = (int)(TimeSpan.FromSeconds(1).Ticks / frames);
		window.LatestFrame.Should().Be(199);
		window.Count.Should().Be(keep);
		window.Snapshot().Select(l => l.AnchorFrame).Should().BeInAscendingOrder().And.OnlyContain(f => f > 199 - keep);
	}

	[Fact]
	public void Snapshot_IsACopy()
	{
		var window = new RollingLandmarkWindow(TimeSpan.FromSeconds(10), Options);
		window.Add([new Landmark(1, 1)]);
		var snapshot = window.Snapshot();
		window.Add([new Landmark(2, 2)]);
		snapshot.Should().HaveCount(1);
		window.Count.Should().Be(2);
	}

	[Fact]
	public void Clear_ForgetsEverything()
	{
		var window = new RollingLandmarkWindow(TimeSpan.FromSeconds(10), Options);
		window.Add([new Landmark(1, 100)]);
		window.Clear();
		window.Count.Should().Be(0);
		window.LatestFrame.Should().Be(-1);
	}

	[Fact]
	public void Constructor_RejectsBadArguments()
	{
		var tooShort = () => new RollingLandmarkWindow(TimeSpan.FromMilliseconds(1), Options);
		tooShort.Should().Throw<ArgumentOutOfRangeException>();
		var nullOptions = () => new RollingLandmarkWindow(TimeSpan.FromSeconds(1), null!);
		nullOptions.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public async Task LiveStream_MatchedWindowByWindow_FindsEachSongAsItPlays()
	{
		// Two songs back to back on one "stream"; match a 10 s rolling window every few seconds.
		var rate = 8000;
		var a = SyntheticSong.Generate(61, 40, rate);
		var b = SyntheticSong.Generate(62, 40, rate);
		using var store = new InMemoryFingerprintStore(Options);
		store.AddTrack("a", Fingerprinter.Fingerprint(a, rate, Options));
		store.AddTrack("b", Fingerprinter.Fingerprint(b, rate, Options));
		var matcher = new Matcher(store, Options);

		var stream = a.Concat(b).ToArray();
		var fingerprinter = new Fingerprinter(rate, Options);
		var window = new RollingLandmarkWindow(TimeSpan.FromSeconds(12), Options);
		var seen = new List<string>();
		for (var start = 0; start < stream.Length; start += rate * 2)
		{
			fingerprinter.Push(stream.AsSpan(start, Math.Min(rate * 2, stream.Length - start)), window);
			if (start >= 20 * rate)
			{
				var matches = await matcher.MatchAsync(window.Snapshot(), null, TestContext.Current.CancellationToken);
				if (matches.Count > 0)
				{
					seen.Add(matches[0].TrackId);
				}
			}
		}

		seen.Should().Contain("a").And.Contain("b");
		seen.IndexOf("a").Should().BeLessThan(seen.LastIndexOf("b"));
	}
}

using PanoramicData.SoundPrint.TestAudio;

namespace PanoramicData.SoundPrint.Test;

/// <summary>The README's code examples, run for real, so they cannot drift from the API.</summary>
public class ReadmeExampleTests
{
	[Fact]
	public async Task QuickStart_FingerprintStoreAndMatch()
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		var trackSamples = SyntheticSong.Generate(71, 40, 44100);
		var trackSampleRate = 44100;
		var clipSamples = trackSamples.AsSpan(12 * 44100, 8 * 44100).ToArray();
		var clipSampleRate = 44100;

		// --- README: quick start ---
		var options = new FingerprintOptions();
		var store = new InMemoryFingerprintStore(options);

		IReadOnlyList<Landmark> landmarks = Fingerprinter.Fingerprint(trackSamples, trackSampleRate, options);
		await store.AddTrackAsync("my-song-42", landmarks, cancellationToken);

		var matcher = new Matcher(store, options);
		var query = Fingerprinter.Fingerprint(clipSamples, clipSampleRate, options);
		var matches = await matcher.MatchAsync(query, null, cancellationToken);

		var best = matches[0];
		// --- end ---

		best.TrackId.Should().Be("my-song-42");
		best.TrackStart.TotalSeconds.Should().BeApproximately(12, 1);
		best.Confidence.Should().BeGreaterThan(0.95);
		store.Dispose();
	}

	[Fact]
	public async Task StreamingExample_MatchesTheLiveWindow()
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		var options = new FingerprintOptions();
		var song = SyntheticSong.Generate(72, 60, 22050);
		using var store = new InMemoryFingerprintStore(options);
		await store.AddTrackAsync("track", Fingerprinter.Fingerprint(song, 22050, options), cancellationToken);
		var matcher = new Matcher(store, options);
		var incoming = new[] { song.AsSpan(0, 22050 * 30).ToArray() };
		Match? last = null;

		// --- README: streaming ---
		var fingerprinter = new Fingerprinter(22050, options);
		var window = new RollingLandmarkWindow(TimeSpan.FromSeconds(10), options);
		var chunks = 0;

		foreach (var chunk in incoming.SelectMany(a => a.Chunk(22050)))
		{
			fingerprinter.Push(chunk, window);

			// Every five seconds, match what has been heard in the last ten.
			if (++chunks % 5 == 0)
			{
				var matches = await matcher.MatchAsync(window.Snapshot(), null, cancellationToken);
				last = matches.Count > 0 ? matches[0] : last;
			}
		}
		// --- end ---

		last.Should().NotBeNull();
		last!.TrackId.Should().Be("track");
	}
}

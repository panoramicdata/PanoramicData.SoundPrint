namespace PanoramicData.SoundPrint.Test;

public class InMemoryFingerprintStoreTests
{
	private sealed class Hits : IHitSink
	{
		public List<(int Track, int Frame, uint Hash)> Items { get; } = [];
		public void Add(int trackIndex, int trackFrame, Landmark queryLandmark) => Items.Add((trackIndex, trackFrame, queryLandmark.Hash));
	}

	private static Landmark[] Landmarks(params (uint Hash, int Frame)[] items) => [.. items.Select(i => new Landmark(i.Hash, i.Frame))];

	[Fact]
	public void NewStore_IsEmptyAndCarriesTheParameterFingerprint()
	{
		using var store = new InMemoryFingerprintStore();
		store.TrackCount.Should().Be(0);
		store.LandmarkCount.Should().Be(0);
		store.ParameterFingerprint.Should().Be(new FingerprintOptions().ParameterFingerprint);
	}

	[Fact]
	public void CustomOptions_SetTheParameterFingerprint()
	{
		var options = new FingerprintOptions { FanOut = 3 };
		using var store = new InMemoryFingerprintStore(options);
		store.ParameterFingerprint.Should().Be(options.ParameterFingerprint);
	}

	[Fact]
	public void AddedTrack_IsFoundByLookup()
	{
		using var store = new InMemoryFingerprintStore();
		store.AddTrack("a", Landmarks((10, 100), (11, 200)));
		var hits = new Hits();
		store.Lookup(Landmarks((10, 5), (99, 6)), hits);
		hits.Items.Should().ContainSingle().Which.Should().Match<(int Track, int Frame, uint Hash)>(h => h.Frame == 100 && h.Hash == 10);
		store.ContainsTrack("a").Should().BeTrue();
		store.TrackCount.Should().Be(1);
		store.LandmarkCount.Should().Be(2);
	}

	[Fact]
	public void SameHashInManyTracks_ReturnsAHitForEach()
	{
		using var store = new InMemoryFingerprintStore();
		store.AddTrack("a", Landmarks((7, 1)));
		store.AddTrack("b", Landmarks((7, 2), (7, 3)));
		var hits = new Hits();
		store.Lookup(Landmarks((7, 0)), hits);
		hits.Items.Should().HaveCount(3);
		hits.Items.Select(h => h.Track).Distinct().Should().HaveCount(2);
	}

	[Fact]
	public void RemoveTrack_RemovesItsLandmarksOnly()
	{
		using var store = new InMemoryFingerprintStore();
		store.AddTrack("a", Landmarks((7, 1), (8, 2)));
		store.AddTrack("b", Landmarks((7, 3)));
		store.RemoveTrack("a").Should().BeTrue();
		store.ContainsTrack("a").Should().BeFalse();
		var hits = new Hits();
		store.Lookup(Landmarks((7, 0), (8, 0)), hits);
		hits.Items.Should().ContainSingle().Which.Frame.Should().Be(3);
		store.RemoveTrack("a").Should().BeFalse();
	}

	[Fact]
	public void AddingTheSameIdAgain_ReplacesTheTrack()
	{
		using var store = new InMemoryFingerprintStore();
		store.AddTrack("a", Landmarks((7, 1)));
		store.AddTrack("a", Landmarks((9, 5)));
		store.TrackCount.Should().Be(1);
		var hits = new Hits();
		store.Lookup(Landmarks((7, 0), (9, 0)), hits);
		hits.Items.Should().ContainSingle().Which.Hash.Should().Be(9u);
	}

	[Fact]
	public void ResolveTrackIds_MapsIndexesAndReportsRemovedTracksAsNull()
	{
		using var store = new InMemoryFingerprintStore();
		store.AddTrack("a", Landmarks((7, 1)));
		var hits = new Hits();
		store.Lookup(Landmarks((7, 0)), hits);
		var index = hits.Items[0].Track;
		store.ResolveTrackIds([index]).Should().Equal("a");
		store.RemoveTrack("a");
		store.ResolveTrackIds([index]).Should().Equal([null]);
	}

	[Fact]
	public void Validation_RejectsBadArguments()
	{
		using var store = new InMemoryFingerprintStore();
		var emptyId = () => store.AddTrack("", []);
		emptyId.Should().Throw<ArgumentException>();
		var nullLandmarks = () => store.AddTrack("a", null!);
		nullLandmarks.Should().Throw<ArgumentNullException>();
		var negativeFrame = () => store.AddTrack("a", Landmarks((1, -1)));
		negativeFrame.Should().Throw<ArgumentException>();
		var removeEmpty = () => store.RemoveTrack("");
		removeEmpty.Should().Throw<ArgumentException>();
		var containsEmpty = () => store.ContainsTrack("");
		containsEmpty.Should().Throw<ArgumentException>();
		var nullQuery = () => store.Lookup(null!, new Hits());
		nullQuery.Should().Throw<ArgumentNullException>();
		var nullSink = () => store.Lookup([], null!);
		nullSink.Should().Throw<ArgumentNullException>();
		var nullIndexes = () => store.ResolveTrackIds(null!);
		nullIndexes.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public async Task AsyncSurface_BehavesLikeTheSyncOne()
	{
		using var store = new InMemoryFingerprintStore();
		var ct = TestContext.Current.CancellationToken;
		await store.AddTrackAsync("a", Landmarks((7, 1)), ct);
		(await store.ContainsTrackAsync("a", ct)).Should().BeTrue();
		(await store.CountTracksAsync(ct)).Should().Be(1);
		var hits = new Hits();
		await store.LookupAsync(Landmarks((7, 0)), hits, ct);
		(await store.ResolveTrackIdsAsync([hits.Items[0].Track], ct)).Should().Equal("a");
		(await store.RemoveTrackAsync("a", ct)).Should().BeTrue();
		(await store.CountTracksAsync(ct)).Should().Be(0);
	}

	[Fact]
	public async Task AsyncSurface_HonoursCancellation()
	{
		using var store = new InMemoryFingerprintStore();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();
		var token = cts.Token;
		var add = async () => await store.AddTrackAsync("a", [], token);
		await add.Should().ThrowAsync<OperationCanceledException>();
		var remove = async () => await store.RemoveTrackAsync("a", token);
		await remove.Should().ThrowAsync<OperationCanceledException>();
		var contains = async () => await store.ContainsTrackAsync("a", token);
		await contains.Should().ThrowAsync<OperationCanceledException>();
		var count = async () => await store.CountTracksAsync(token);
		await count.Should().ThrowAsync<OperationCanceledException>();
		var lookup = async () => await store.LookupAsync([], new Hits(), token);
		await lookup.Should().ThrowAsync<OperationCanceledException>();
		var resolve = async () => await store.ResolveTrackIdsAsync([], token);
		await resolve.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task ConcurrentAddsRemovesAndLookups_KeepTheStoreConsistent()
	{
		using var store = new InMemoryFingerprintStore();
		var tasks = Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
		{
			var rng = new Random(worker);
			for (var i = 0; i < 300; i++)
			{
				var id = $"t{worker}-{i % 20}";
				store.AddTrack(id, [.. Enumerable.Range(0, 50).Select(_ => new Landmark((uint)rng.Next(200), rng.Next(1000)))]);
				store.Lookup([new Landmark((uint)rng.Next(200), 0)], new Hits());
				if (i % 3 == 0)
				{
					store.RemoveTrack(id);
				}
			}
		}));
		await Task.WhenAll(tasks);

		var expected = 0L;
		for (var worker = 0; worker < 8; worker++)
		{
			for (var slot = 0; slot < 20; slot++)
			{
				if (store.ContainsTrack($"t{worker}-{slot}"))
				{
					expected += 50;
				}
			}
		}

		store.LandmarkCount.Should().Be(expected);
	}
}

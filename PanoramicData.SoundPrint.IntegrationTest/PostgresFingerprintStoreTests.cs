using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PanoramicData.SoundPrint.Postgres;
using PanoramicData.SoundPrint.TestAudio;

namespace PanoramicData.SoundPrint.IntegrationTest;

public class PostgresFingerprintStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
	private sealed class Hits : IHitSink
	{
		public List<(int Track, int Frame, uint Hash, int QueryFrame)> Items { get; } = [];
		public void Add(int trackIndex, int trackFrame, Landmark queryLandmark) =>
			Items.Add((trackIndex, trackFrame, queryLandmark.Hash, queryLandmark.AnchorFrame));
	}

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private static Landmark[] Landmarks(params (uint Hash, int Frame)[] items) => [.. items.Select(i => new Landmark(i.Hash, i.Frame))];

	private static async Task<long> CountRowsAsync(string connectionString, string sql)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(Ct);
		await using var command = new NpgsqlCommand(sql, connection);
		return Convert.ToInt64(await command.ExecuteScalarAsync(Ct), System.Globalization.CultureInfo.InvariantCulture);
	}

	[Fact]
	public async Task Initialize_CreatesTheSchemaAndIsIdempotent()
	{
		var (store, connectionString) = await fixture.CreateStoreAsync();
		await store.InitializeAsync(Ct);
		(await CountRowsAsync(connectionString, "SELECT count(*) FROM information_schema.tables WHERE table_name IN ('tracks','fingerprints','store_info')")).Should().Be(3);
		(await CountRowsAsync(connectionString, "SELECT count(*) FROM store_info")).Should().Be(1);
	}

	[Fact]
	public async Task Migrations_CanBeRevertedAndReapplied()
	{
		var (store, connectionString) = await fixture.CreateStoreAsync();
		var factory = new TestFactory(SoundPrintDbContext.CreateOptions(connectionString));
		await using (var context = factory.CreateDbContext())
		{
			await context.GetService<IMigrator>().MigrateAsync("0", Ct);
		}

		(await CountRowsAsync(connectionString, "SELECT count(*) FROM information_schema.tables WHERE table_name IN ('tracks','fingerprints','store_info')")).Should().Be(0);
		await store.InitializeAsync(Ct);
		(await store.CountTracksAsync(Ct)).Should().Be(0);
	}

	[Fact]
	public async Task Initialize_CreatesTheCoveringHashIndex()
	{
		var (_, connectionString) = await fixture.CreateStoreAsync();
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(Ct);
		await using var command = new NpgsqlCommand("SELECT indexdef FROM pg_indexes WHERE indexname = 'ix_fingerprints_hash'", connection);
		var definition = (string?)await command.ExecuteScalarAsync(Ct);
		definition.Should().Contain("(hash) INCLUDE (track, frame)");
	}

	[Fact]
	public async Task Initialize_RejectsADatabaseBuiltWithDifferentParameters()
	{
		var (_, connectionString) = await fixture.CreateStoreAsync(new FingerprintOptions());
		var other = PostgresFingerprintStore.Create(connectionString, new FingerprintOptions { FanOut = 3 });
		var act = async () => await other.InitializeAsync(Ct);
		await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not comparable*");
	}

	[Fact]
	public async Task ParameterFingerprint_ComesFromTheOptions()
	{
		var options = new FingerprintOptions { FanOut = 4 };
		var (store, _) = await fixture.CreateStoreAsync(options);
		store.ParameterFingerprint.Should().Be(options.ParameterFingerprint);
	}

	[Fact]
	public async Task AddedTrack_IsFoundByLookupAndResolvesToItsId()
	{
		var (store, _) = await fixture.CreateStoreAsync();
		await store.AddTrackAsync("track-a", Landmarks((10, 100), (11, 200)), Ct);
		var hits = new Hits();
		await store.LookupAsync(Landmarks((10, 5), (99, 6)), hits, Ct);

		var (track, frame, _, queryFrame) = hits.Items.Should().ContainSingle().Subject;
		frame.Should().Be(100);
		queryFrame.Should().Be(5);
		(await store.ResolveTrackIdsAsync([track], Ct)).Should().Equal("track-a");
		(await store.ContainsTrackAsync("track-a", Ct)).Should().BeTrue();
		(await store.CountTracksAsync(Ct)).Should().Be(1);
	}

	[Fact]
	public async Task HashesWithTheHighBitSet_RoundTrip()
	{
		var (store, _) = await fixture.CreateStoreAsync();
		await store.AddTrackAsync("hi", Landmarks((0xFFFFFFFFu, 1), (0x80000000u, 2)), Ct);
		var hits = new Hits();
		await store.LookupAsync(Landmarks((0xFFFFFFFFu, 0), (0x80000000u, 0)), hits, Ct);
		hits.Items.Select(h => h.Hash).Order().Should().Equal(0x80000000u, 0xFFFFFFFFu);
	}

	[Fact]
	public async Task SameQueryHashAtSeveralFrames_ReportsEveryQueryLandmark()
	{
		var (store, _) = await fixture.CreateStoreAsync();
		await store.AddTrackAsync("t", Landmarks((5, 50)), Ct);
		var hits = new Hits();
		await store.LookupAsync(Landmarks((5, 1), (5, 9)), hits, Ct);
		hits.Items.Select(h => h.QueryFrame).Order().Should().Equal(1, 9);
	}

	[Fact]
	public async Task RemoveTrack_RemovesItsLandmarksAndNoOthers()
	{
		var (store, connectionString) = await fixture.CreateStoreAsync();
		await store.AddTrackAsync("a", Landmarks((7, 1), (8, 2)), Ct);
		await store.AddTrackAsync("b", Landmarks((7, 3)), Ct);

		(await store.RemoveTrackAsync("a", Ct)).Should().BeTrue();
		(await store.RemoveTrackAsync("a", Ct)).Should().BeFalse();

		(await store.ContainsTrackAsync("a", Ct)).Should().BeFalse();
		(await CountRowsAsync(connectionString, "SELECT count(*) FROM fingerprints")).Should().Be(1);
		var hits = new Hits();
		await store.LookupAsync(Landmarks((7, 0)), hits, Ct);
		hits.Items.Should().ContainSingle().Which.Frame.Should().Be(3);
	}

	[Fact]
	public async Task AddingTheSameIdAgain_ReplacesTheTrack()
	{
		var (store, connectionString) = await fixture.CreateStoreAsync();
		await store.AddTrackAsync("a", Landmarks((7, 1)), Ct);
		await store.AddTrackAsync("a", Landmarks((9, 5), (10, 6)), Ct);

		(await store.CountTracksAsync(Ct)).Should().Be(1);
		(await CountRowsAsync(connectionString, "SELECT count(*) FROM fingerprints")).Should().Be(2);
		var hits = new Hits();
		await store.LookupAsync(Landmarks((7, 0)), hits, Ct);
		hits.Items.Should().BeEmpty();
	}

	[Fact]
	public async Task ResolveTrackIds_ReportsUnknownIndexesAsNull()
	{
		var (store, _) = await fixture.CreateStoreAsync();
		(await store.ResolveTrackIdsAsync([12345], Ct)).Should().Equal([null]);
	}

	[Fact]
	public async Task EmptyTrackAndEmptyQuery_AreHandled()
	{
		var (store, _) = await fixture.CreateStoreAsync();
		await store.AddTrackAsync("empty", [], Ct);
		(await store.ContainsTrackAsync("empty", Ct)).Should().BeTrue();
		var hits = new Hits();
		await store.LookupAsync([], hits, Ct);
		hits.Items.Should().BeEmpty();
	}

	[Fact]
	public async Task LookupWithMoreDistinctHashesThanOneBatch_FindsThemAll()
	{
		var (store, _) = await fixture.CreateStoreAsync();
		var landmarks = Enumerable.Range(0, 12000).Select(i => new Landmark((uint)(i * 7919), i)).ToArray();
		await store.AddTrackAsync("many", landmarks, Ct);
		var hits = new Hits();
		await store.LookupAsync(landmarks, hits, Ct);
		hits.Items.Should().HaveCount(12000);
	}

	[Fact]
	public async Task Validation_RejectsBadArguments()
	{
		var (store, _) = await fixture.CreateStoreAsync();
		var emptyId = async () => await store.AddTrackAsync("", [], Ct);
		await emptyId.Should().ThrowAsync<ArgumentException>();
		var nullLandmarks = async () => await store.AddTrackAsync("a", null!, Ct);
		await nullLandmarks.Should().ThrowAsync<ArgumentNullException>();
		var negative = async () => await store.AddTrackAsync("a", Landmarks((1, -1)), Ct);
		await negative.Should().ThrowAsync<ArgumentException>();
		var removeEmpty = async () => await store.RemoveTrackAsync("", Ct);
		await removeEmpty.Should().ThrowAsync<ArgumentException>();
		var containsEmpty = async () => await store.ContainsTrackAsync("", Ct);
		await containsEmpty.Should().ThrowAsync<ArgumentException>();
		var nullQuery = async () => await store.LookupAsync(null!, new Hits(), Ct);
		await nullQuery.Should().ThrowAsync<ArgumentNullException>();
		var nullSink = async () => await store.LookupAsync([], null!, Ct);
		await nullSink.Should().ThrowAsync<ArgumentNullException>();
		var nullIndexes = async () => await store.ResolveTrackIdsAsync(null!, Ct);
		await nullIndexes.Should().ThrowAsync<ArgumentNullException>();
		var nullFactory = () => new PostgresFingerprintStore(null!);
		nullFactory.Should().Throw<ArgumentNullException>();
		var emptyConnection = () => PostgresFingerprintStore.Create("");
		emptyConnection.Should().Throw<ArgumentException>();
	}

	[Fact]
	public async Task ConcurrentWritesToTheSameId_LeaveOneConsistentTrack()
	{
		var (store, connectionString) = await fixture.CreateStoreAsync();
		var sizes = new[] { 100, 200, 300, 400, 500, 600 };
		await Task.WhenAll(sizes.Select(n => Task.Run(
			async () => await store.AddTrackAsync("same", [.. Enumerable.Range(0, n).Select(i => new Landmark((uint)i, i))], Ct), Ct)));

		(await store.CountTracksAsync(Ct)).Should().Be(1);
		var rows = await CountRowsAsync(connectionString, "SELECT count(*) FROM fingerprints");
		sizes.Select(n => (long)n).Should().Contain(rows);
	}

	[Fact]
	public async Task ConcurrentWritesToDifferentIdsWithConcurrentLookups_AllSucceed()
	{
		var (store, _) = await fixture.CreateStoreAsync();
		var tasks = Enumerable.Range(0, 8).Select(worker => Task.Run(async () =>
		{
			await store.AddTrackAsync($"t{worker}", [.. Enumerable.Range(0, 500).Select(i => new Landmark((uint)(i + (worker * 1000)), i))], Ct);
			await store.LookupAsync([new Landmark((uint)(worker * 1000), 0)], new Hits(), Ct);
		}, Ct));
		await Task.WhenAll(tasks);
		(await store.CountTracksAsync(Ct)).Should().Be(8);
	}

	[Fact]
	public async Task EndToEndMatching_AgreesWithTheInMemoryStore()
	{
		var options = new FingerprintOptions();
		var (pg, _) = await fixture.CreateStoreAsync(options);
		using var memory = new InMemoryFingerprintStore(options);
		var songs = Enumerable.Range(0, 4).Select(i => SyntheticSong.Generate(500 + i, 30, 8000)).ToArray();
		for (var i = 0; i < songs.Length; i++)
		{
			var landmarks = Fingerprinter.Fingerprint(songs[i], 8000, options);
			await pg.AddTrackAsync($"song-{i}", landmarks, Ct);
			memory.AddTrack($"song-{i}", landmarks);
		}

		var clip = songs[2].AsSpan(8 * 8000, 9 * 8000).ToArray();
		var query = Fingerprinter.Fingerprint(clip, 8000, options);
		var fromPg = await new Matcher(pg, options).MatchAsync(query, null, Ct);
		var fromMemory = await new Matcher(memory, options).MatchAsync(query, null, Ct);

		fromPg.Should().NotBeEmpty();
		fromPg[0].TrackId.Should().Be("song-2");
		fromPg[0].TrackStart.TotalSeconds.Should().BeApproximately(8, 0.5);
		fromPg.Select(m => (m.TrackId, m.OffsetInTrack, m.Votes, m.QueryStart, m.QueryEnd))
			.Should().Equal(fromMemory.Select(m => (m.TrackId, m.OffsetInTrack, m.Votes, m.QueryStart, m.QueryEnd)));
	}

	[Fact]
	public async Task BulkInsert_OfHundredsOfThousandsOfLandmarks_UsesTheCoveringIndex()
	{
		var (store, connectionString) = await fixture.CreateStoreAsync();
		var rng = new Random(5);
		var landmarks = Enumerable.Range(0, 400_000).Select(i => new Landmark((uint)rng.Next(), i)).ToArray();
		await store.AddTrackAsync("bulk", landmarks, Ct);
		(await CountRowsAsync(connectionString, "SELECT count(*) FROM fingerprints")).Should().Be(400_000);

		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(Ct);
		await using (var vacuum = new NpgsqlCommand("VACUUM ANALYZE fingerprints", connection))
		{
			await vacuum.ExecuteNonQueryAsync(Ct);
		}

		var hash = unchecked((int)landmarks[1234].Hash);
		await using var explain = new NpgsqlCommand($"EXPLAIN SELECT hash, track, frame FROM fingerprints WHERE hash = ANY(ARRAY[{hash}])", connection);
		var plan = new List<string>();
		await using (var reader = await explain.ExecuteReaderAsync(Ct))
		{
			while (await reader.ReadAsync(Ct))
			{
				plan.Add(reader.GetString(0));
			}
		}

		string.Join('\n', plan).Should().Contain("Index Only Scan using ix_fingerprints_hash");
	}

	[Fact]
	public async Task StoreCanBeUsedThroughAnEfContextFactory()
	{
		var connectionString = await fixture.CreateDatabaseAsync();
		var factory = new TestFactory(SoundPrintDbContext.CreateOptions(connectionString));
		var store = new PostgresFingerprintStore(factory);
		await store.InitializeAsync(Ct);
		await store.AddTrackAsync("via-factory", Landmarks((1, 1)), Ct);
		await using var context = factory.CreateDbContext();
		(await context.Tracks.CountAsync(Ct)).Should().Be(1);
		(await context.Fingerprints.CountAsync(Ct)).Should().Be(1);
	}

	private sealed class TestFactory(DbContextOptions<SoundPrintDbContext> options) : IDbContextFactory<SoundPrintDbContext>
	{
		public SoundPrintDbContext CreateDbContext() => new(options);
	}
}

using System.Diagnostics;
using System.Text;
using Npgsql;
using PanoramicData.SoundPrint.Postgres;
using PanoramicData.SoundPrint.TestAudio;

namespace PanoramicData.SoundPrint.Benchmarks;

/// <summary>
/// Builds a large synthetic database and measures how long a 10 second query takes to match, how much space it uses and how many
/// landmarks an hour of audio produces. Works against the in-memory store, and against PostgreSQL when given a connection string.
/// </summary>
internal static class LatencyRunner
{
	public static int Run(Args args)
	{
		var tracks = args.Int("tracks", 100_000);
		var seconds = args.Double("seconds", 40);
		var rate = args.Int("rate", 8000);
		var queries = args.Int("queries", 300);
		var queryLength = args.Double("query-seconds", 10);
		var connectionString = args.String("postgres");
		var options = new FingerprintOptions();
		var sb = new StringBuilder();
		void Say(string line)
		{
			Console.WriteLine(line);
			sb.AppendLine(line);
		}

		Say($"# Latency and size: {tracks} tracks of {seconds:F0} s ({tracks * seconds / 3600:F0} hours of audio)");
		Say(string.Empty);

		IFingerprintStore store;
		InMemoryFingerprintStore? memory = null;
		PostgresFingerprintStore? postgres = null;
		if (connectionString is null)
		{
			store = memory = new InMemoryFingerprintStore(options);
		}
		else
		{
			store = postgres = PostgresFingerprintStore.Create(connectionString, options);
			postgres.InitializeAsync().GetAwaiter().GetResult();
		}

		var watch = Stopwatch.StartNew();
		long landmarkTotal = 0;
		var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);
		var parallelism = connectionString is null ? 1 : 8;
		var next = -1;
		var workers = Enumerable.Range(0, Math.Max(Environment.ProcessorCount, 4)).Select(_ => Task.Run(async () =>
		{
			int i;
			while ((i = Interlocked.Increment(ref next)) < tracks)
			{
				var song = SyntheticSong.Generate(i, seconds, rate);
				var landmarks = Fingerprinter.Fingerprint(song, rate, options);
				Interlocked.Add(ref landmarkTotal, landmarks.Count);
				if (memory is not null)
				{
					lock (memory)
					{
						memory.AddTrack(Corpus.TrackId(i), landmarks);
					}
				}
				else
				{
					await store.AddTrackAsync(Corpus.TrackId(i), landmarks, CancellationToken.None).ConfigureAwait(false);
				}

				if (i % 5000 == 0)
				{
					Console.WriteLine($"  built {i} tracks ({watch.Elapsed.TotalMinutes:F1} min)");
				}
			}
		})).ToArray();
		_ = parallelism;
		Task.WaitAll(workers);
		var buildMinutes = watch.Elapsed.TotalMinutes;
		var hours = tracks * seconds / 3600;
		Say($"Built in {buildMinutes:F1} min; {landmarkTotal:N0} landmarks; {landmarkTotal / hours:F0} landmarks per hour of audio ({landmarkTotal / (hours * 3600):F1} per second).");

		if (memory is not null)
		{
			var memoryAfter = GC.GetTotalMemory(forceFullCollection: true);
			var delta = memoryAfter - memoryBefore;
			Say($"In-memory store: {delta / 1e9:F2} GB of managed heap, {delta / hours / 1e6:F1} MB per hour of audio, {delta / (double)landmarkTotal:F1} bytes per landmark.");
		}
		else
		{
			using var connection = new NpgsqlConnection(connectionString);
			connection.Open();
			using var vacuum = new NpgsqlCommand("VACUUM ANALYZE fingerprints", connection) { CommandTimeout = 3600 };
			vacuum.ExecuteNonQuery();
			long Scalar(string sql)
			{
				using var command = new NpgsqlCommand(sql, connection);
				return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
			}

			var table = Scalar("SELECT pg_table_size('fingerprints')");
			var index = Scalar("SELECT pg_indexes_size('fingerprints')");
			Say($"PostgreSQL: table {table / 1e9:F2} GB + indexes {index / 1e9:F2} GB = {(table + index) / 1e9:F2} GB; {(table + index) / hours / 1e6:F1} MB per hour of audio, {(table + index) / (double)landmarkTotal:F1} bytes per landmark.");
		}

		// Queries: clips of tracks in the database, matched with default options.
		var rng = new Random(99);
		var timings = new List<double>();
		var hitCounts = new List<long>();
		var correct = 0;
		var matcher = new Matcher(store, options);
		var clipSamples = (int)(queryLength * rate);
		for (var q = 0; q < queries; q++)
		{
			var track = rng.Next(tracks);
			var song = SyntheticSong.Generate(track, seconds, rate);
			var start = rng.Next(0, song.Length - clipSamples);
			var query = Fingerprinter.Fingerprint(song.AsSpan(start, clipSamples), rate, options);
			var counter = new CountingStore(store);
			var w = Stopwatch.StartNew();
			var matches = new Matcher(counter, options).MatchAsync(query).AsTask().GetAwaiter().GetResult();
			w.Stop();
			timings.Add(w.Elapsed.TotalMilliseconds);
			hitCounts.Add(counter.Hits);
			if (matches.Count > 0 && matches[0].TrackId == Corpus.TrackId(track) && Math.Abs(matches[0].OffsetInTrack.TotalSeconds - ((double)start / rate)) < 0.4)
			{
				correct++;
			}
		}

		_ = matcher;
		timings.Sort();
		hitCounts.Sort();
		Say(string.Empty);
		Say($"Match latency for a {queryLength:F0} s query against {tracks:N0} tracks ({(connectionString is null ? "in-memory" : "PostgreSQL")} store, {queries} queries, after fingerprinting):");
		Say($"- median {timings[timings.Count / 2]:F1} ms, 95th percentile {timings[(int)(timings.Count * 0.95)]:F1} ms, maximum {timings[^1]:F1} ms");
		Say($"- hits returned per query: median {hitCounts[hitCounts.Count / 2]:N0}, maximum {hitCounts[^1]:N0}");
		Say($"- top-1 accuracy on clean clips: {100.0 * correct / queries:F1}%");

		if (args.String("out") is { } path)
		{
			File.WriteAllText(path, sb.ToString());
		}

		return 0;
	}

	/// <summary>Counts hits a lookup returns so the report can relate latency to hit volume.</summary>
	private sealed class CountingStore(IFingerprintStore inner) : IFingerprintStore
	{
		public long Hits { get; private set; }

		public long ParameterFingerprint => inner.ParameterFingerprint;

		public ValueTask AddTrackAsync(string trackId, IReadOnlyList<Landmark> landmarks, CancellationToken cancellationToken = default) => inner.AddTrackAsync(trackId, landmarks, cancellationToken);

		public ValueTask<bool> RemoveTrackAsync(string trackId, CancellationToken cancellationToken = default) => inner.RemoveTrackAsync(trackId, cancellationToken);

		public ValueTask<bool> ContainsTrackAsync(string trackId, CancellationToken cancellationToken = default) => inner.ContainsTrackAsync(trackId, cancellationToken);

		public ValueTask<int> CountTracksAsync(CancellationToken cancellationToken = default) => inner.CountTracksAsync(cancellationToken);

		public ValueTask LookupAsync(IReadOnlyList<Landmark> query, IHitSink hits, CancellationToken cancellationToken = default) =>
			inner.LookupAsync(query, new CountingSink(hits, () => Hits++), cancellationToken);

		public ValueTask<IReadOnlyList<string?>> ResolveTrackIdsAsync(IReadOnlyList<int> trackIndexes, CancellationToken cancellationToken = default) => inner.ResolveTrackIdsAsync(trackIndexes, cancellationToken);
	}

	private sealed class CountingSink(IHitSink inner, Action count) : IHitSink
	{
		public void Add(int trackIndex, int trackFrame, Landmark queryLandmark)
		{
			count();
			inner.Add(trackIndex, trackFrame, queryLandmark);
		}
	}
}

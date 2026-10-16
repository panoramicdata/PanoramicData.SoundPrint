using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace PanoramicData.SoundPrint.Postgres;

/// <summary>
/// An <see cref="IFingerprintStore"/> backed by PostgreSQL through EF Core. Tracks are written with binary <c>COPY</c>; lookups are
/// batched index-only scans over <c>fingerprints (hash) INCLUDE (track, frame)</c>. Safe for concurrent use: every call uses its own
/// context from the factory, and writes to the same track id are serialised with an advisory lock.
/// </summary>
public sealed class PostgresFingerprintStore : IFingerprintStore
{
	private const int LookupBatchSize = 5000;

	private readonly IDbContextFactory<SoundPrintDbContext> _factory;

	/// <summary>Creates a store over a context factory, for landmarks extracted with default options. Call <see cref="InitializeAsync()"/> once at start-up.</summary>
	/// <param name="factory">Creates a context per operation.</param>
	public PostgresFingerprintStore(IDbContextFactory<SoundPrintDbContext> factory)
		: this(factory, new FingerprintOptions())
	{
	}

	/// <summary>Creates a store over a context factory. Call <see cref="InitializeAsync()"/> once at start-up.</summary>
	/// <param name="factory">Creates a context per operation.</param>
	/// <param name="options">The extraction options landmarks come from.</param>
	public PostgresFingerprintStore(IDbContextFactory<SoundPrintDbContext> factory, FingerprintOptions options)
	{
		ArgumentNullException.ThrowIfNull(factory);
		ArgumentNullException.ThrowIfNull(options);
		_factory = factory;
		ParameterFingerprint = options.ParameterFingerprint;
	}

	/// <summary>Creates a store for a connection string, for landmarks extracted with default options.</summary>
	/// <param name="connectionString">An Npgsql connection string.</param>
	/// <returns>A store; call <see cref="InitializeAsync()"/> before first use.</returns>
	public static PostgresFingerprintStore Create(string connectionString) =>
		Create(connectionString, new FingerprintOptions());

	/// <summary>Creates a store for a connection string.</summary>
	/// <param name="connectionString">An Npgsql connection string.</param>
	/// <param name="options">The extraction options landmarks come from.</param>
	/// <returns>A store; call <see cref="InitializeAsync()"/> before first use.</returns>
	public static PostgresFingerprintStore Create(string connectionString, FingerprintOptions options)
	{
		ArgumentException.ThrowIfNullOrEmpty(connectionString);
		return new PostgresFingerprintStore(new SimpleFactory(SoundPrintDbContext.CreateOptions(connectionString)), options);
	}

	/// <inheritdoc />
	public long ParameterFingerprint { get; }

	/// <summary>Applies pending migrations and checks the fingerprint parameters, without a cancellation token.</summary>
	/// <exception cref="InvalidOperationException">The database holds landmarks extracted with different parameters.</exception>
	public Task InitializeAsync() => InitializeAsync(CancellationToken.None);

	/// <summary>
	/// Applies pending migrations and checks that the database was built with the same fingerprint parameters as this store.
	/// A new database records them.
	/// </summary>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <exception cref="InvalidOperationException">The database holds landmarks extracted with different parameters.</exception>
	public async Task InitializeAsync(CancellationToken cancellationToken)
	{
		var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
		await using var contextLease = context.ConfigureAwait(false);
		await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

		var info = await context.StoreInfo.SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
		if (info is null)
		{
			context.StoreInfo.Add(new StoreInfoRecord { Id = 1, ParameterFingerprint = ParameterFingerprint, CreatedUtc = DateTime.UtcNow });
			await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		}
		else if (info.ParameterFingerprint != ParameterFingerprint)
		{
			throw new InvalidOperationException(
				$"The database was built with fingerprint parameters {info.ParameterFingerprint}, but this store uses {ParameterFingerprint}. Their hashes are not comparable.");
		}
	}

	/// <inheritdoc />
	public async ValueTask AddTrackAsync(string trackId, IReadOnlyList<Landmark> landmarks, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(trackId);
		ArgumentNullException.ThrowIfNull(landmarks);
		foreach (var landmark in landmarks)
		{
			if (landmark.AnchorFrame < 0)
			{
				throw new ArgumentException("Landmark frames must not be negative.", nameof(landmarks));
			}
		}

		var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
		await using var contextLease = context.ConfigureAwait(false);
		var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
		await using var transactionLease = transaction.ConfigureAwait(false);

		await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({trackId}, 0))", cancellationToken).ConfigureAwait(false);
		await context.Tracks.Where(t => t.TrackId == trackId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

		var track = new TrackRecord { TrackId = trackId, LandmarkCount = landmarks.Count, CreatedUtc = DateTime.UtcNow };
		context.Tracks.Add(track);
		await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		var connection = (NpgsqlConnection)context.Database.GetDbConnection();
		var importer = await connection.BeginBinaryImportAsync("COPY fingerprints (hash, track, frame) FROM STDIN (FORMAT BINARY)", cancellationToken).ConfigureAwait(false);
		await using (importer.ConfigureAwait(false))
		{
			foreach (var landmark in landmarks)
			{
				await importer.StartRowAsync(cancellationToken).ConfigureAwait(false);
				await importer.WriteAsync(unchecked((int)landmark.Hash), NpgsqlDbType.Integer, cancellationToken).ConfigureAwait(false);
				await importer.WriteAsync(track.Idx, NpgsqlDbType.Integer, cancellationToken).ConfigureAwait(false);
				await importer.WriteAsync(landmark.AnchorFrame, NpgsqlDbType.Integer, cancellationToken).ConfigureAwait(false);
			}

			await importer.CompleteAsync(cancellationToken).ConfigureAwait(false);
		}

		await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask<bool> RemoveTrackAsync(string trackId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(trackId);
		var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
		await using var contextLease = context.ConfigureAwait(false);
		return await context.Tracks.Where(t => t.TrackId == trackId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false) > 0;
	}

	/// <inheritdoc />
	public async ValueTask<bool> ContainsTrackAsync(string trackId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(trackId);
		var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
		await using var contextLease = context.ConfigureAwait(false);
		return await context.Tracks.AnyAsync(t => t.TrackId == trackId, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask<int> CountTracksAsync(CancellationToken cancellationToken)
	{
		var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
		await using var contextLease = context.ConfigureAwait(false);
		return await context.Tracks.CountAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask LookupAsync(IReadOnlyList<Landmark> query, IHitSink hits, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);
		ArgumentNullException.ThrowIfNull(hits);
		if (query.Count == 0)
		{
			return;
		}

		var byHash = new Dictionary<int, List<Landmark>>();
		foreach (var landmark in query)
		{
			var key = unchecked((int)landmark.Hash);
			if (!byHash.TryGetValue(key, out var list))
			{
				byHash[key] = list = [];
			}

			list.Add(landmark);
		}

		var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
		await using var contextLease = context.ConfigureAwait(false);
		await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		var connection = (NpgsqlConnection)context.Database.GetDbConnection();
		var distinct = byHash.Keys.ToArray();
		for (var start = 0; start < distinct.Length; start += LookupBatchSize)
		{
			var batch = distinct[start..Math.Min(distinct.Length, start + LookupBatchSize)];
			var command = new NpgsqlCommand("SELECT hash, track, frame FROM fingerprints WHERE hash = ANY(@hashes)", connection);
			await using var commandLease = command.ConfigureAwait(false);
			command.Parameters.Add(new NpgsqlParameter<int[]>("hashes", batch) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Integer });
			var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			await using var readerLease = reader.ConfigureAwait(false);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				var hash = reader.GetInt32(0);
				var track = reader.GetInt32(1);
				var frame = reader.GetInt32(2);
				foreach (var landmark in byHash[hash])
				{
					hits.Add(track, frame, landmark);
				}
			}
		}
	}

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<string?>> ResolveTrackIdsAsync(IReadOnlyList<int> trackIndexes, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(trackIndexes);
		var wanted = trackIndexes.Distinct().ToArray();
		var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
		await using var contextLease = context.ConfigureAwait(false);
		var found = await context.Tracks.Where(t => wanted.Contains(t.Idx)).ToDictionaryAsync(t => t.Idx, t => t.TrackId, cancellationToken).ConfigureAwait(false);
		return [.. trackIndexes.Select(i => found.GetValueOrDefault(i))];
	}

	private sealed class SimpleFactory(DbContextOptions<SoundPrintDbContext> options) : IDbContextFactory<SoundPrintDbContext>
	{
		public SoundPrintDbContext CreateDbContext() => new(options);
	}
}

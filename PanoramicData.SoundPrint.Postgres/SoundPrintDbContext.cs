using Microsoft.EntityFrameworkCore;

namespace PanoramicData.SoundPrint.Postgres;

/// <summary>
/// The EF Core model of the fingerprint database: <c>tracks</c>, <c>fingerprints</c> and <c>store_info</c>.
/// Lookups use a covering index on <c>fingerprints (hash) INCLUDE (track, frame)</c>, so a hash lookup is an index-only scan.
/// </summary>
/// <param name="options">The context options; use <see cref="CreateOptions"/> for the standard configuration.</param>
public class SoundPrintDbContext(DbContextOptions<SoundPrintDbContext> options) : DbContext(options)
{
	/// <summary>Tracks.</summary>
	public DbSet<TrackRecord> Tracks => Set<TrackRecord>();

	/// <summary>Landmarks.</summary>
	public DbSet<FingerprintRecord> Fingerprints => Set<FingerprintRecord>();

	/// <summary>The single store-information row.</summary>
	public DbSet<StoreInfoRecord> StoreInfo => Set<StoreInfoRecord>();

	/// <summary>Builds context options for a PostgreSQL connection string.</summary>
	/// <param name="connectionString">An Npgsql connection string.</param>
	/// <returns>Options with the Npgsql provider and the migrations in this assembly.</returns>
	public static DbContextOptions<SoundPrintDbContext> CreateOptions(string connectionString) =>
		new DbContextOptionsBuilder<SoundPrintDbContext>()
			.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(SoundPrintDbContext).Assembly.GetName().Name))
			.Options;

	/// <inheritdoc />
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);

		modelBuilder.Entity<TrackRecord>(entity =>
		{
			entity.ToTable("tracks");
			entity.HasKey(t => t.Idx);
			entity.Property(t => t.Idx).HasColumnName("idx");
			entity.Property(t => t.TrackId).HasColumnName("track_id").IsRequired();
			entity.Property(t => t.LandmarkCount).HasColumnName("landmark_count");
			entity.Property(t => t.CreatedUtc).HasColumnName("created_utc");
			entity.HasIndex(t => t.TrackId).IsUnique().HasDatabaseName("ux_tracks_track_id");
		});

		modelBuilder.Entity<FingerprintRecord>(entity =>
		{
			entity.ToTable("fingerprints");
			entity.HasNoKey();
			entity.Property(f => f.Hash).HasColumnName("hash");
			entity.Property(f => f.Track).HasColumnName("track");
			entity.Property(f => f.Frame).HasColumnName("frame");
			entity.HasIndex(f => f.Hash).IncludeProperties(f => new { f.Track, f.Frame }).HasDatabaseName("ix_fingerprints_hash");
			entity.HasIndex(f => f.Track).HasDatabaseName("ix_fingerprints_track");
			entity.HasOne<TrackRecord>().WithMany().HasForeignKey(f => f.Track).OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<StoreInfoRecord>(entity =>
		{
			entity.ToTable("store_info");
			entity.HasKey(s => s.Id);
			entity.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
			entity.Property(s => s.ParameterFingerprint).HasColumnName("parameter_fingerprint");
			entity.Property(s => s.CreatedUtc).HasColumnName("created_utc");
		});
	}
}

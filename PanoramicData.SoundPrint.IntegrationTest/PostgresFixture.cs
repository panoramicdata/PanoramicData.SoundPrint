using Npgsql;
using PanoramicData.SoundPrint.Postgres;
using Testcontainers.PostgreSql;

namespace PanoramicData.SoundPrint.IntegrationTest;

/// <summary>
/// Starts one PostgreSQL container for the whole run and hands each test its own empty database. If Docker is not available the
/// fixture fails, and so does every test that uses it, with a message that says what to do: these tests never skip.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
	private PostgreSqlContainer? _container;

	public async ValueTask InitializeAsync()
	{
		try
		{
			_container = new PostgreSqlBuilder("postgres:17-alpine").Build();
			await _container.StartAsync();
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException(
				"The PostgreSQL integration tests need Docker. Start Docker (Docker Desktop on Windows or macOS, the docker service on Linux) so that a postgres:17-alpine container can run, then run the tests again. " +
				"They do not skip when Docker is missing. Cause: " + ex.Message,
				ex);
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_container is not null)
		{
			await _container.DisposeAsync();
		}
	}

	/// <summary>Creates an empty database and returns its connection string.</summary>
	public async Task<string> CreateDatabaseAsync()
	{
		var name = "sp_" + Guid.NewGuid().ToString("N");
		await using (var admin = new NpgsqlConnection(_container!.GetConnectionString()))
		{
			await admin.OpenAsync(TestContext.Current.CancellationToken);
			await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", admin);
			await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
		}

		return new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name }.ConnectionString;
	}

	/// <summary>Creates an empty database and an initialised store over it.</summary>
	public async Task<(PostgresFingerprintStore Store, string ConnectionString)> CreateStoreAsync(FingerprintOptions? options = null)
	{
		var connectionString = await CreateDatabaseAsync();
		var store = PostgresFingerprintStore.Create(connectionString, options);
		await store.InitializeAsync(TestContext.Current.CancellationToken);
		return (store, connectionString);
	}
}

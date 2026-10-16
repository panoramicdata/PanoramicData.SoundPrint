using Microsoft.EntityFrameworkCore.Design;

namespace PanoramicData.SoundPrint.Postgres;

/// <summary>Lets <c>dotnet ef</c> create the context for migrations without a running database.</summary>
internal sealed class DesignTimeFactory : IDesignTimeDbContextFactory<SoundPrintDbContext>
{
	public SoundPrintDbContext CreateDbContext(string[] args) =>
		new(SoundPrintDbContext.CreateOptions("Host=localhost;Database=soundprint"));
}

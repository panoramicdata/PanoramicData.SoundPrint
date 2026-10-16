using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Design;

namespace PanoramicData.SoundPrint.Postgres;

// Only the dotnet ef tool calls this, at design time, so no test process ever reaches it.
/// <summary>Lets <c>dotnet ef</c> create the context for migrations without a running database.</summary>
[ExcludeFromCodeCoverage]
internal sealed class DesignTimeFactory : IDesignTimeDbContextFactory<SoundPrintDbContext>
{
	public SoundPrintDbContext CreateDbContext(string[] args) =>
		new(SoundPrintDbContext.CreateOptions("Host=localhost;Database=soundprint"));
}

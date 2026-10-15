namespace PanoramicData.SoundPrint.Test;

public class AssemblyMarkerTests
{
	[Fact]
	public void CoreAssembly_ExposesPackageName() =>
		AssemblyMarker.Name.Should().Be("PanoramicData.SoundPrint");

	[Fact]
	public void PostgresAssembly_ExposesPackageName() =>
		Postgres.AssemblyMarker.Name.Should().Be("PanoramicData.SoundPrint.Postgres");
}

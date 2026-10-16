namespace PanoramicData.SoundPrint.Test;

public class DeterministicMathTests
{
	[Fact]
	public void Sin_AgreesWithMathSin_OverWideRange()
	{
		for (var x = -200.0; x <= 200.0; x += 0.0137)
		{
			DeterministicMath.Sin(x).Should().BeApproximately(Math.Sin(x), 1e-13, $"x = {x}");
		}
	}

	[Fact]
	public void Cos_AgreesWithMathCos_OverWideRange()
	{
		for (var x = -200.0; x <= 200.0; x += 0.0137)
		{
			DeterministicMath.Cos(x).Should().BeApproximately(Math.Cos(x), 1e-13, $"x = {x}");
		}
	}

	[Fact]
	public void KnownValues_AreExact()
	{
		DeterministicMath.Sin(0).Should().Be(0);
		DeterministicMath.Cos(0).Should().Be(1);
	}

	[Fact]
	public void Results_AreBitwiseRepeatable()
	{
		var first = DeterministicMath.Sin(1.2345);
		var second = DeterministicMath.Sin(1.2345);
		BitConverter.DoubleToInt64Bits(first).Should().Be(BitConverter.DoubleToInt64Bits(second));
	}
}

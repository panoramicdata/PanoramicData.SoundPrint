namespace PanoramicData.SoundPrint.Test;

public class FingerprintOptionsTests
{
	[Fact]
	public void Defaults_AreValid() => new FingerprintOptions().Validate();

	[Fact]
	public void FrameDuration_IsExactly32Milliseconds_At8kHz() =>
		new FingerprintOptions { SampleRate = 8000 }.FrameDuration.Should().Be(TimeSpan.FromMilliseconds(32));

	[Fact]
	public void FramesToTime_ScalesLinearly() =>
		new FingerprintOptions { SampleRate = 8000 }.FramesToTime(1000).Should().Be(TimeSpan.FromSeconds(32));

	[Fact]
	public void BinCount_IsHalfTheWindowPlusOne() =>
		new FingerprintOptions().BinCount.Should().Be(513);

	public static TheoryData<string, FingerprintOptions> InvalidOptions => new()
	{
		{ "SampleRate", new FingerprintOptions { SampleRate = 500 } },
		{ "WindowSize", new FingerprintOptions { WindowSize = 1000 } },
		{ "WindowSize", new FingerprintOptions { WindowSize = 2048 } },
		{ "HopSize", new FingerprintOptions { HopSize = 0 } },
		{ "HopSize", new FingerprintOptions { HopSize = 2048 } },
		{ "PeakNeighbourhoodBins", new FingerprintOptions { PeakNeighbourhoodBins = 0 } },
		{ "PeakNeighbourhoodFrames", new FingerprintOptions { PeakNeighbourhoodFrames = 0 } },
		{ "MinPeakBin", new FingerprintOptions { MinPeakBin = -1 } },
		{ "MinPeakBin", new FingerprintOptions { MinPeakBin = 600 } },
		{ "MinPeakPower", new FingerprintOptions { MinPeakPower = -1 } },
		{ "MinPeakPower", new FingerprintOptions { MinPeakPower = double.NaN } },
		{ "PeaksPerBandPerBlock", new FingerprintOptions { PeaksPerBandPerBlock = 0 } },
		{ "DensityBlockFrames", new FingerprintOptions { DensityBlockFrames = 3 } },
		{ "FanOut", new FingerprintOptions { FanOut = 0 } },
		{ "MaxDeltaFrames", new FingerprintOptions { MaxDeltaFrames = 64 } },
		{ "MaxDeltaFrames", new FingerprintOptions { MaxDeltaFrames = 0 } },
		{ "ResamplerHalfTaps", new FingerprintOptions { ResamplerHalfTaps = 1 } },
		{ "BandEdges", new FingerprintOptions { BandEdges = null! } },
		{ "BandEdges", new FingerprintOptions { BandEdges = [0] } },
		{ "BandEdges", new FingerprintOptions { BandEdges = [1, 513] } },
		{ "BandEdges", new FingerprintOptions { BandEdges = [0, 100] } },
		{ "BandEdges", new FingerprintOptions { BandEdges = [0, 100, 100, 513] } }
	};

	[Theory]
	[MemberData(nameof(InvalidOptions))]
	public void Validate_RejectsInvalidValue(string parameter, FingerprintOptions options)
	{
		var act = options.Validate;
		act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be(parameter);
	}

	[Fact]
	public void ParameterFingerprint_IsStableForEqualOptions() =>
		new FingerprintOptions().ParameterFingerprint.Should().Be(new FingerprintOptions().ParameterFingerprint);

	public static TheoryData<FingerprintOptions> HashAffectingChanges => new()
	{
		new FingerprintOptions { SampleRate = 8000 },
		new FingerprintOptions { WindowSize = 512, HopSize = 128, BandEdges = [0, 8, 16, 32, 64, 128, 257] },
		new FingerprintOptions { HopSize = 128 },
		new FingerprintOptions { PeakNeighbourhoodBins = 10 },
		new FingerprintOptions { PeakNeighbourhoodFrames = 2 },
		new FingerprintOptions { MinPeakBin = 3 },
		new FingerprintOptions { MinPeakPower = 1e-3 },
		new FingerprintOptions { BandEdges = [0, 16, 64, 513] },
		new FingerprintOptions { PeaksPerBandPerBlock = 5 },
		new FingerprintOptions { DensityBlockFrames = 31 },
		new FingerprintOptions { FanOut = 4 },
		new FingerprintOptions { MaxDeltaFrames = 50 },
		new FingerprintOptions { ResamplerHalfTaps = 12 }
	};

	[Theory]
	[MemberData(nameof(HashAffectingChanges))]
	public void ParameterFingerprint_ChangesWithEveryHashAffectingValue(FingerprintOptions changed) =>
		changed.ParameterFingerprint.Should().NotBe(new FingerprintOptions().ParameterFingerprint);
}

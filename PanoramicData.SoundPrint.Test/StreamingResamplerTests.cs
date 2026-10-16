namespace PanoramicData.SoundPrint.Test;

public class StreamingResamplerTests
{
	private static float[] Run(StreamingResampler resampler, float[] input)
	{
		var output = new List<float>();
		foreach (var sample in input)
		{
			resampler.Push(sample);
			while (resampler.TryRead(out var y))
			{
				output.Add(y);
			}
		}

		resampler.End();
		while (resampler.TryRead(out var y))
		{
			output.Add(y);
		}

		return [.. output];
	}

	private static float[] Sine(double frequency, int rate, int count) =>
		[.. Enumerable.Range(0, count).Select(i => (float)Math.Sin(2 * Math.PI * frequency * i / rate))];

	private static double Rms(float[] x, int from, int to) =>
		Math.Sqrt(x.Skip(from).Take(to - from).Sum(v => (double)v * v) / (to - from));

	[Fact]
	public void Downsample_ProducesTheExpectedNumberOfSamples()
	{
		var output = Run(new StreamingResampler(44100, 8000, 16), Sine(440, 44100, 44100));
		output.Length.Should().BeInRange(7999, 8001);
	}

	[Fact]
	public void Upsample_ProducesTheExpectedNumberOfSamples()
	{
		var output = Run(new StreamingResampler(8000, 11025, 16), Sine(440, 8000, 8000));
		output.Length.Should().BeInRange(11024, 11026);
	}

	[Fact]
	public void PassbandTone_KeepsItsAmplitude()
	{
		var output = Run(new StreamingResampler(44100, 8000, 16), Sine(1000, 44100, 44100));
		Rms(output, 500, 7500).Should().BeApproximately(Math.Sqrt(0.5), 0.01);
	}

	[Fact]
	public void PassbandTone_KeepsItsFrequency()
	{
		var output = Run(new StreamingResampler(44100, 8000, 16), Sine(1000, 44100, 44100));
		var expected = Sine(1000, 8000, 8000);
		for (var i = 1000; i < 7000; i += 97)
		{
			output[i].Should().BeApproximately(expected[i], 0.02f);
		}
	}

	[Fact]
	public void ToneAboveTheNewNyquist_IsAttenuated()
	{
		var output = Run(new StreamingResampler(44100, 8000, 16), Sine(9000, 44100, 44100));
		Rms(output, 500, 7500).Should().BeLessThan(0.01);
	}

	[Fact]
	public void ConstantInput_GivesConstantOutput()
	{
		var output = Run(new StreamingResampler(48000, 8000, 16), [.. Enumerable.Repeat(0.5f, 48000)]);
		for (var i = 200; i < 7800; i += 111)
		{
			output[i].Should().BeApproximately(0.5f, 1e-4f);
		}
	}

	[Fact]
	public void Output_IsIndependentOfChunking()
	{
		var input = Sine(700, 22050, 30000);
		var whole = Run(new StreamingResampler(22050, 8000, 16), input);

		var resampler = new StreamingResampler(22050, 8000, 16);
		var chunked = new List<float>();
		var rng = new Random(1);
		for (var i = 0; i < input.Length;)
		{
			var take = Math.Min(rng.Next(1, 500), input.Length - i);
			for (var j = 0; j < take; j++)
			{
				resampler.Push(input[i + j]);
				while (resampler.TryRead(out var y))
				{
					chunked.Add(y);
				}
			}

			i += take;
		}

		resampler.End();
		while (resampler.TryRead(out var y))
		{
			chunked.Add(y);
		}

		chunked.Should().Equal(whole);
	}

	[Fact]
	public void Reset_RestartsTheStream()
	{
		var input = Sine(500, 16000, 8000);
		var resampler = new StreamingResampler(16000, 8000, 16);
		var first = Run(resampler, input);
		resampler.Reset();
		var second = Run(resampler, input);
		second.Should().Equal(first);
	}
}

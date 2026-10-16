namespace PanoramicData.SoundPrint.TestAudio;

/// <summary>Minimal 16-bit mono PCM WAV writer, used to hand audio to ffmpeg.</summary>
public static class WavFile
{
	/// <summary>Encodes samples as a 16-bit mono WAV file in memory.</summary>
	public static byte[] ToBytes(ReadOnlySpan<float> samples, int sampleRate)
	{
		using var stream = new MemoryStream();
		Write(stream, samples, sampleRate);
		return stream.ToArray();
	}

	/// <summary>Writes samples as a 16-bit mono WAV file.</summary>
	public static void Write(string path, ReadOnlySpan<float> samples, int sampleRate)
	{
		using var stream = File.Create(path);
		Write(stream, samples, sampleRate);
	}

	private static void Write(Stream stream, ReadOnlySpan<float> samples, int sampleRate)
	{
		using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
		var dataBytes = samples.Length * 2;
		writer.Write("RIFF"u8);
		writer.Write(36 + dataBytes);
		writer.Write("WAVE"u8);
		writer.Write("fmt "u8);
		writer.Write(16);
		writer.Write((short)1);
		writer.Write((short)1);
		writer.Write(sampleRate);
		writer.Write(sampleRate * 2);
		writer.Write((short)2);
		writer.Write((short)16);
		writer.Write("data"u8);
		writer.Write(dataBytes);
		foreach (var sample in samples)
		{
			writer.Write((short)Math.Round(Math.Clamp(sample, -1f, 1f) * 32767));
		}
	}
}

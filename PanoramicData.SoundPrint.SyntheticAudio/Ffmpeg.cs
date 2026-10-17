using System.Diagnostics;

namespace PanoramicData.SoundPrint.TestAudio;

/// <summary>
/// Lossy re-encoding through a locally installed ffmpeg. The tests that need it fail, with a message saying what is missing,
/// when ffmpeg cannot be found; they never skip.
/// </summary>
public static class Ffmpeg
{
	/// <summary>Environment variable that may point at an ffmpeg executable.</summary>
	public const string EnvironmentVariable = "SOUNDPRINT_FFMPEG";

	/// <summary>Finds ffmpeg or throws an <see cref="InvalidOperationException"/> that says how to supply it.</summary>
	public static string Locate()
	{
		var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
		if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(fromEnvironment))
		{
			return fromEnvironment;
		}

		var exeName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
		foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
		{
			var candidate = Path.Combine(directory.Trim(), exeName);
			if (File.Exists(candidate))
			{
				return candidate;
			}
		}

		throw new InvalidOperationException(
			$"ffmpeg was not found. Install ffmpeg and put it on PATH, or set the {EnvironmentVariable} environment variable to the full path of the ffmpeg executable. " +
			"The re-encode tests need it and do not skip.");
	}

	/// <summary>
	/// Encodes with the given codec options, decodes back to PCM at the same rate, and returns the result.
	/// </summary>
	/// <param name="samples">Input audio.</param>
	/// <param name="sampleRate">Sample rate of the audio.</param>
	/// <param name="extension">Container extension such as mp3 or m4a.</param>
	/// <param name="codecArguments">ffmpeg output arguments, for example "-c:a libmp3lame -b:a 64k".</param>
	/// <returns>The decoded audio, trimmed or padded to the input length.</returns>
	public static float[] RoundTrip(ReadOnlySpan<float> samples, int sampleRate, string extension, string codecArguments)
	{
		var ffmpeg = Locate();
		var directory = Path.Combine(Path.GetTempPath(), "soundprint-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			var source = Path.Combine(directory, "in.wav");
			var encoded = Path.Combine(directory, "enc." + extension);
			var decoded = Path.Combine(directory, "out.raw");
			WavFile.Write(source, samples, sampleRate);
			Run(ffmpeg, $"-y -hide_banner -loglevel error -i \"{source}\" {codecArguments} \"{encoded}\"");
			Run(ffmpeg, $"-y -hide_banner -loglevel error -i \"{encoded}\" -f f32le -ac 1 -ar {sampleRate} \"{decoded}\"");
			var bytes = File.ReadAllBytes(decoded);
			var result = new float[samples.Length];
			var available = Math.Min(result.Length, bytes.Length / 4);
			for (var i = 0; i < available; i++)
			{
				result[i] = BitConverter.ToSingle(bytes, i * 4);
			}

			return result;
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	private static void Run(string executable, string arguments)
	{
		var info = new ProcessStartInfo(executable, arguments)
		{
			RedirectStandardError = true,
			RedirectStandardOutput = true,
			UseShellExecute = false
		};
		using var process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start {executable}.");
		var error = process.StandardError.ReadToEnd();
		process.StandardOutput.ReadToEnd();
		process.WaitForExit();
		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException($"ffmpeg failed with exit code {process.ExitCode}: {error}");
		}
	}
}

using PanoramicData.SoundPrint.SyntheticAudio;

namespace PanoramicData.SoundPrint.Benchmarks;

/// <summary>
/// One way of degrading a song before it is queried. <see cref="Apply"/> degrades a whole song (as a broadcast chain would), and
/// queries are then sliced from the degraded audio.
/// </summary>
internal sealed record Scenario(string Group, string Name, Func<float[], int, int, float[]> Apply, bool NeedsFfmpeg = false, int MaxSongs = int.MaxValue)
{
	public static IReadOnlyList<Scenario> Standard { get; } =
	[
		new("clean", "clean", (s, _, _) => s),
		new("noise", "white noise 20 dB SNR", (s, _, seed) => Degradations.AddWhiteNoise(s, 20, seed)),
		new("noise", "white noise 10 dB SNR", (s, _, seed) => Degradations.AddWhiteNoise(s, 10, seed)),
		new("noise", "white noise 5 dB SNR", (s, _, seed) => Degradations.AddWhiteNoise(s, 5, seed)),
		new("noise", "white noise 0 dB SNR", (s, _, seed) => Degradations.AddWhiteNoise(s, 0, seed)),
		new("radio", "EQ + compression + limiter", (s, rate, _) => Degradations.RadioProcessing(s, rate)),
		new("re-encode", "MP3 128 kbit/s", (s, rate, _) => Ffmpeg.RoundTrip(s, rate, "mp3", "-c:a libmp3lame -b:a 128k"), NeedsFfmpeg: true, MaxSongs: 120),
		new("re-encode", "MP3 64 kbit/s", (s, rate, _) => Ffmpeg.RoundTrip(s, rate, "mp3", "-c:a libmp3lame -b:a 64k"), NeedsFfmpeg: true, MaxSongs: 120),
		new("re-encode", "AAC 64 kbit/s", (s, rate, _) => Ffmpeg.RoundTrip(s, rate, "m4a", "-c:a aac -b:a 64k"), NeedsFfmpeg: true, MaxSongs: 120),
		new("re-encode", "Opus 24 kbit/s", (s, rate, _) => Ffmpeg.RoundTrip(s, rate, "ogg", "-c:a libopus -b:a 24k"), NeedsFfmpeg: true, MaxSongs: 120),
		new("voice-over", "voice 10 dB below music", (s, rate, seed) => Degradations.MixVoiceOver(s, rate, -10, seed)),
		new("voice-over", "voice 5 dB below music", (s, rate, seed) => Degradations.MixVoiceOver(s, rate, -5, seed)),
		new("voice-over", "voice level with music", (s, rate, seed) => Degradations.MixVoiceOver(s, rate, 0, seed)),
		new("chain", "radio + MP3 96k + voice -8 dB + noise 25 dB", (s, rate, seed) =>
			Degradations.AddWhiteNoise(
				Ffmpeg.RoundTrip(Degradations.MixVoiceOver(Degradations.RadioProcessing(s, rate), rate, -8, seed), rate, "mp3", "-c:a libmp3lame -b:a 96k"),
				25,
				seed + 1),
			NeedsFfmpeg: true,
			MaxSongs: 120)
	];

	/// <summary>The harder cases used to compare parameter choices quickly.</summary>
	public static IReadOnlyList<Scenario> Hard { get; } =
	[
		Standard[3],
		Standard[5],
		Standard[11],
		Standard[13]
	];
}

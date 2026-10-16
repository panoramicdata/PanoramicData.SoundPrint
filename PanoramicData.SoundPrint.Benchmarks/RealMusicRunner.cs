using System.Diagnostics;
using System.Text;
using PanoramicData.SoundPrint.TestAudio;

namespace PanoramicData.SoundPrint.Benchmarks;

/// <summary>
/// Local-only benchmark on real music. The default source is a Creative Commons subset of the Free Music Archive dataset that
/// you download into the gitignored <c>benchmarks/audio</c> folder (see <c>benchmarks/SOURCES.md</c>); the other is a folder of your
/// own files named by the <c>SOUNDPRINT_LOCAL_AUDIO</c> environment variable. Audio never leaves the machine and nothing derived
/// from it other than aggregate numbers is printed or written. With no audio present the command says so and exits with code 2:
/// it does not pass silently.
/// </summary>
internal static class RealMusicRunner
{
	private const int Rate = 22050;
	private const double TrackSeconds = 29;

	public static int Run(Args args)
	{
		var source = args.String("source") ?? "fma";
		var repoRoot = FindRepositoryRoot();
		var tracks = source == "local" ? LocalTracks() : FmaTracks(repoRoot, args.Int("tracks", 600));
		if (tracks is null)
		{
			return 2;
		}

		Ffmpeg.Locate();
		Console.WriteLine($"Decoding {tracks.Count} tracks with ffmpeg...");
		var decoded = new float[tracks.Count][];
		var watch = Stopwatch.StartNew();
		Parallel.For(0, tracks.Count, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i => decoded[i] = Decode(tracks[i].Path));
		Console.WriteLine($"Decoded in {watch.Elapsed.TotalSeconds:F0} s");

		var usable = decoded.Select((samples, i) => (samples, track: tracks[i])).Where(x => x.samples.Length >= TrackSeconds * Rate).ToList();
		var clips = usable.Select(x => x.samples.AsSpan(0, (int)(TrackSeconds * Rate)).ToArray()).ToArray();
		var distinct = DistinctTracks(clips);
		Console.WriteLine($"Dropped {clips.Length - distinct.Count} tracks that duplicate another track in the set (the same recording published twice).");
		usable = [.. distinct.Select(i => usable[i])];
		clips = [.. distinct.Select(i => clips[i])];
		var split = (int)(clips.Length * 0.8);
		var present = clips[..split];
		var absent = clips[split..];
		Console.WriteLine($"{present.Length} tracks in the database, {absent.Length} held out as absent.");

		if (args.Flag("write-sources") && source == "fma")
		{
			WriteSources(repoRoot, [.. usable.Select(x => x.track)], split);
		}

		var quick = args.Flag("quick");
		var trials = args.Int("trials", quick ? 60 : 300);
		var config = new AccuracyConfig(
			present.Length,
			trials,
			Rate,
			TrackSeconds,
			new FingerprintOptions(),
			Scenario.Standard,
			quick ? [5.0, 10.0] : [3.0, 5.0, 7.0, 10.0])
		{
			PresentSongs = present,
			AbsentSongs = absent,
			Title = source == "fma" ? "Real-music results (Free Music Archive, Creative Commons)" : "Real-music results (local audio)",
			Description = $"Database: {present.Length} real tracks of {TrackSeconds:F0} s ({present.Length * TrackSeconds / 3600:F2} hours) decoded to {Rate} Hz mono and fingerprinted at 8000 Hz; {absent.Length} further tracks held out as absent queries. " +
				$"{trials} queries per cell. Re-encode and chain scenarios use {Math.Min(120, present.Length)} tracks. " +
				(source == "fma" ? "Source and licences: benchmarks/SOURCES.md. Audio stays local and is not committed." : "Only aggregate numbers are reported."),
			Command = $"real --source {source}"
		};

		var cells = AccuracyRunner.Execute(config);
		var fit = Calibration.Fit(cells.Where(c => c.QuerySeconds >= 5).SelectMany(c => c.Present.Concat(c.Absent)));
		var threshold = Calibration.RecommendedScoreThreshold(cells.Where(c => c.QuerySeconds >= 5), 0.001);
		var straddle = AccuracyRunner.RunStraddle(config, args.Int("straddle-trials", quick ? 60 : 200));
		var report = AccuracyRunner.Report(config, cells, fit, threshold, straddle);
		Console.WriteLine(report);
		if (args.String("out") is { } path)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
			File.WriteAllText(path, report);
		}

		return 0;
	}

	/// <summary>
	/// Indexes of tracks to keep after dropping any that match an earlier track strongly: public music collections contain the same
	/// recording more than once, and a duplicate would count as a false match for what is really a correct one.
	/// </summary>
	private static List<int> DistinctTracks(float[][] clips)
	{
		var options = new FingerprintOptions();
		var landmarks = new IReadOnlyList<Landmark>[clips.Length];
		Parallel.For(0, clips.Length, i => landmarks[i] = Fingerprinter.Fingerprint(clips[i], Rate, options));
		using var store = new InMemoryFingerprintStore(options);
		for (var i = 0; i < clips.Length; i++)
		{
			store.AddTrack(Corpus.TrackId(i), landmarks[i]);
		}

		var matcher = new Matcher(store, options, new MatchOptions { MinConfidence = 0, MinVotes = 10 });
		var duplicate = new bool[clips.Length];
		Parallel.For(0, clips.Length, i =>
		{
			var matches = matcher.MatchAsync(landmarks[i]).AsTask().GetAwaiter().GetResult();
			foreach (var m in matches)
			{
				if (m.TrackId != Corpus.TrackId(i) && m.Votes >= 30 && int.Parse(m.TrackId.AsSpan(5), System.Globalization.CultureInfo.InvariantCulture) < i)
				{
					duplicate[i] = true;
				}
			}
		});
		return [.. Enumerable.Range(0, clips.Length).Where(i => !duplicate[i])];
	}

	private sealed record TrackInfo(string Path, string Id, string Artist, string Title, string License);

	private sealed record FmaRow(string Artist, string Title, string License);

	private static List<TrackInfo>? LocalTracks()
	{
		var folder = Environment.GetEnvironmentVariable("SOUNDPRINT_LOCAL_AUDIO");
		if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
		{
			Console.WriteLine("SKIPPED: the real-music benchmark with --source local needs the SOUNDPRINT_LOCAL_AUDIO environment variable to name a folder of audio files; it is not set or the folder does not exist.");
			return null;
		}

		string[] extensions = [".mp3", ".flac", ".wav", ".m4a", ".ogg", ".opus"];
		var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
			.Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
			.Order(StringComparer.Ordinal)
			.Take(600)
			.ToList();
		if (files.Count < 20)
		{
			Console.WriteLine($"SKIPPED: {folder} holds only {files.Count} audio files; at least 20 are needed.");
			return null;
		}

		return [.. files.Select(f => new TrackInfo(f, string.Empty, string.Empty, string.Empty, string.Empty))];
	}

	private static List<TrackInfo>? FmaTracks(string repoRoot, int count)
	{
		var audio = Path.Combine(repoRoot, "benchmarks", "audio");
		var clips = Path.Combine(audio, "fma-small", "fma_small");
		var metadata = Path.Combine(audio, "fma-metadata", "fma_metadata", "tracks.csv");
		if (!Directory.Exists(clips) || !File.Exists(metadata))
		{
			Console.WriteLine($"SKIPPED: the Free Music Archive audio is not at {clips} (with metadata at {metadata}). Download fma_small.zip and fma_metadata.zip as described in benchmarks/SOURCES.md and extract them under benchmarks/audio/fma-small and benchmarks/audio/fma-metadata. Nothing was measured.");
			return null;
		}

		var rows = ReadFmaMetadata(metadata);
		var files = Directory.EnumerateFiles(clips, "*.mp3", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
		var step = Math.Max(1, files.Count / count);
		var selected = new List<TrackInfo>();
		for (var i = 0; i < files.Count && selected.Count < count; i += step)
		{
			var id = Path.GetFileNameWithoutExtension(files[i]).TrimStart('0');
			rows.TryGetValue(id, out var meta);
			selected.Add(new TrackInfo(files[i], id, meta?.Artist ?? "unknown", meta?.Title ?? "unknown", meta?.License ?? "unknown"));
		}

		return selected;
	}

	/// <summary>Reads artist name, title and licence from the FMA tracks.csv, whose header spans three rows.</summary>
	private static Dictionary<string, FmaRow> ReadFmaMetadata(string path)
	{
		var rows = ParseCsv(File.ReadAllText(path));
		var category = rows[0];
		var field = rows[1];
		int Column(string cat, string name)
		{
			for (var i = 0; i < category.Count; i++)
			{
				if (category[i] == cat && field[i] == name)
				{
					return i;
				}
			}

			return -1;
		}

		var artist = Column("artist", "name");
		var title = Column("track", "title");
		var license = Column("track", "license");
		var result = new Dictionary<string, FmaRow>();
		foreach (var row in rows.Skip(3))
		{
			if (row.Count > Math.Max(artist, Math.Max(title, license)) && row[0].Length > 0)
			{
				result[row[0]] = new FmaRow(row[artist], row[title], row[license]);
			}
		}

		return result;
	}

	private static List<List<string>> ParseCsv(string text)
	{
		var rows = new List<List<string>>();
		var row = new List<string>();
		var field = new StringBuilder();
		var quoted = false;
		for (var i = 0; i < text.Length; i++)
		{
			var c = text[i];
			if (quoted)
			{
				if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
				{
					field.Append('"');
					i++;
				}
				else if (c == '"')
				{
					quoted = false;
				}
				else
				{
					field.Append(c);
				}
			}
			else if (c == '"')
			{
				quoted = true;
			}
			else if (c == ',')
			{
				row.Add(field.ToString());
				field.Clear();
			}
			else if (c == '\n')
			{
				row.Add(field.ToString().TrimEnd('\r'));
				field.Clear();
				rows.Add(row);
				row = [];
			}
			else
			{
				field.Append(c);
			}
		}

		if (field.Length > 0 || row.Count > 0)
		{
			row.Add(field.ToString());
			rows.Add(row);
		}

		return rows;
	}

	private static float[] Decode(string path)
	{
		var info = new ProcessStartInfo(Ffmpeg.Locate(), $"-hide_banner -loglevel error -i \"{path}\" -t {TrackSeconds + 1} -f f32le -ac 1 -ar {Rate} pipe:1")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false
		};
		using var process = Process.Start(info)!;
		using var memory = new MemoryStream();
		process.StandardOutput.BaseStream.CopyTo(memory);
		process.StandardError.ReadToEnd();
		process.WaitForExit();
		var bytes = memory.ToArray();
		var samples = new float[bytes.Length / 4];
		Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 4);
		return samples;
	}

	private static void WriteSources(string repoRoot, List<TrackInfo> tracks, int databaseCount)
	{
		var sb = new StringBuilder();
		sb.AppendLine("# Benchmark audio sources");
		sb.AppendLine();
		sb.AppendLine("The repository contains no recordings and no fingerprint data. The local-only real-music benchmark (`dotnet run -c Release --project PanoramicData.SoundPrint.Benchmarks -- real`) reads Creative Commons licensed music that you download yourself into the gitignored `benchmarks/audio` folder. Only the metadata of the tracks used and the resulting aggregate numbers are committed.");
		sb.AppendLine();
		sb.AppendLine("## Dataset");
		sb.AppendLine();
		sb.AppendLine("FMA: A Dataset For Music Analysis (Defferrard, Benzi, Vandergheynst, Bresson; ISMIR 2017), https://github.com/mdeff/fma. `fma_small` holds 30 second clips of 8,000 tracks that artists published on the Free Music Archive under Creative Commons licences; `fma_metadata` holds the track titles, artists and licences. The audio is used only to compute and match fingerprints locally for benchmarking. It is not redistributed, so share-alike and no-derivatives terms are not engaged, and tracks under non-commercial licences are used for this internal measurement only. Licences differ per track and are listed below.");
		sb.AppendLine();
		sb.AppendLine("How to fetch:");
		sb.AppendLine();
		sb.AppendLine("```shell");
		sb.AppendLine("mkdir -p benchmarks/audio/fma-small benchmarks/audio/fma-metadata");
		sb.AppendLine("curl -L -o benchmarks/audio/fma_small.zip https://os.unil.cloud.switch.ch/fma/fma_small.zip");
		sb.AppendLine("curl -L -o benchmarks/audio/fma_metadata.zip https://os.unil.cloud.switch.ch/fma/fma_metadata.zip");
		sb.AppendLine("unzip benchmarks/audio/fma_small.zip -d benchmarks/audio/fma-small");
		sb.AppendLine("unzip benchmarks/audio/fma_metadata.zip -d benchmarks/audio/fma-metadata");
		sb.AppendLine("```");
		sb.AppendLine();
		sb.AppendLine("ffmpeg must be on PATH or named by the `SOUNDPRINT_FFMPEG` environment variable. Without the audio the `real` command prints a SKIPPED message and exits with code 2.");
		sb.AppendLine();
		sb.AppendLine($"## Tracks used ({tracks.Count}: the first {databaseCount} form the database, the rest are held out as absent queries)");
		sb.AppendLine();
		sb.AppendLine("| FMA track id | Artist | Title | Licence |");
		sb.AppendLine("|---|---|---|---|");
		foreach (var t in tracks)
		{
			sb.AppendLine($"| {t.Id} | {Cell(t.Artist)} | {Cell(t.Title)} | {Cell(t.License)} |");
		}

		var path = Path.Combine(repoRoot, "benchmarks", "SOURCES.md");
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, sb.ToString());
		Console.WriteLine($"Wrote {path}");
	}

	private static string Cell(string s) => s.Replace("|", "/", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PanoramicData.SoundPrint.slnx")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName ?? Directory.GetCurrentDirectory();
	}
}

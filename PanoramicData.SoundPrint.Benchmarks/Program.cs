// Entry point for the benchmark and measurement tools. Run with no arguments to list the commands.
using BenchmarkDotNet.Running;
using PanoramicData.SoundPrint.Benchmarks;

var command = args.Length > 0 ? args[0] : string.Empty;
var options = new Args(args.Skip(1));
switch (command)
{
	case "accuracy":
		return AccuracyRunner.Run(options);
	case "tune":
		return TuningRunner.Run(options);
	case "latency":
		return LatencyRunner.Run(options);
	case "probe":
		return Probe.Run(options);
	case "real":
		return RealMusicRunner.Run(options);
	case "speed":
		BenchmarkSwitcher.FromAssembly(typeof(ExtractionBenchmarks).Assembly).Run([.. args.Skip(1)]);
		return 0;
	default:
		Console.WriteLine("""
			PanoramicData.SoundPrint.Benchmarks

			  accuracy [--quick] [--songs N] [--trials N] [--out file.md]   accuracy by degradation and query length (needs ffmpeg for re-encode scenarios)
			  tune [--only text] [--out file.md]                            compare extraction parameters on the hard scenarios
			  latency [--tracks N] [--postgres connection] [--out file.md]  match latency and size against a large synthetic database
			  real [--out file.md]                                          local-only benchmark on Creative Commons music (see benchmarks/SOURCES.md)
			  speed [BenchmarkDotNet arguments]                             extraction speed and allocations, match latency by database size
			""");
		return command.Length == 0 ? 0 : 1;
}

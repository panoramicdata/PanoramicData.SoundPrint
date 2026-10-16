# Usage guide

Everything here uses real, tested API. The README examples are run by the unit tests (`ReadmeExampleTests`) so they cannot drift.

## Install

```shell
dotnet add package PanoramicData.SoundPrint            # core library, no database dependency
dotnet add package PanoramicData.SoundPrint.Postgres   # optional PostgreSQL store (EF Core)
```

Targets .NET 10. The library takes decoded **mono** PCM as `float` samples (nominally -1 to 1) plus the sample rate. It does not decode MP3, AAC or any other format; use whatever you already have (ffmpeg, NAudio, the browser's Web Audio API). Down-mix stereo before passing it in.

## Concepts

| Term | Meaning |
|---|---|
| **Landmark** | A hash of two spectral peaks plus the frame of the first. The unit of fingerprinting and lookup. |
| **Frame** | One analysis step, 23 ms at the defaults (`FingerprintOptions.FrameDuration`). Landmark times are frame numbers on the stream's own clock. |
| **Track id** | Your opaque string for a recording. The library never interprets it. |
| **Store** | A database from hash to (track, frame). In-memory or PostgreSQL. |
| **Match** | A track, an offset into it, the span of the query it explains, and a confidence. |

## Fingerprint a recording and match a clip

```csharp
using PanoramicData.SoundPrint;

var options = new FingerprintOptions();                       // defaults are tuned; see TUNING.md
var store = new InMemoryFingerprintStore(options);

// Index a track you have already decoded to mono float samples.
IReadOnlyList<Landmark> landmarks = Fingerprinter.Fingerprint(trackSamples, trackSampleRate, options);
await store.AddTrackAsync("my-song-42", landmarks, cancellationToken);

// Identify a short clip.
var matcher = new Matcher(store, options);
var query = Fingerprinter.Fingerprint(clipSamples, clipSampleRate, options);
var matches = await matcher.MatchAsync(query, null, cancellationToken);

var best = matches[0];
Console.WriteLine($"{best.TrackId} from {best.TrackStart} to {best.TrackEnd}, confidence {best.Confidence:P0}");
```

`Matcher.MatchAsync(query)` with no other arguments is also available. Matches come back most confident first; an empty list means nothing cleared the minimum.

## Streaming: a live radio feed

Use one `Fingerprinter` per stream and a `RollingLandmarkWindow` to keep the most recent audio:

```csharp
var fingerprinter = new Fingerprinter(streamSampleRate, options);
var window = new RollingLandmarkWindow(TimeSpan.FromSeconds(10), options);

foreach (var chunk in incomingAudioChunks)                  // any chunk size; one reading thread
{
	fingerprinter.Push(chunk, window);

	if (ShouldMatchNow())                                   // for example every five seconds
	{
		var matches = await matcher.MatchAsync(window.Snapshot(), null, cancellationToken);
		// matches[i].QueryStart / QueryEnd are on the stream's clock; add the UTC time at which
		// the stream's frame 0 began to get wall-clock times.
	}
}

fingerprinter.Flush(window);                                // end of stream: release what lookahead was holding
```

Notes that matter on a 24/7 stream:

- **Memory is constant.** All buffers are allocated up front; steady-state pushes allocate nothing, and `RollingLandmarkWindow` is bounded by its length.
- **Output lags input by two to three seconds** (peak lookahead, a density block, and the pairing zone). A match for "now" therefore trails by about that much.
- **The frame counter lasts about a year and a half** at the defaults and throws on overflow. For longer, start a fresh `Fingerprinter` and `RollingLandmarkWindow` at a convenient moment and remember the new stream's start time.
- **Windows of 10 to 12 seconds, matched every few seconds,** are a good starting point. A longer window gives more votes per match; a shorter one finds changes of song sooner.
- **Song boundaries:** a window that straddles two songs returns two matches, each with its own `QueryStart`/`QueryEnd`. The midpoint between the first song's end and the second's start was within about 0.2 s of the true boundary in the benchmarks (see BENCHMARKS.md); refine with other evidence (for example the announcer's words) if you need better.

## Retracting a mistake

```csharp
await store.RemoveTrackAsync("my-song-42", cancellationToken);   // all of its landmarks go
await store.AddTrackAsync("my-song-42", newLandmarks, cancellationToken);  // adding an existing id replaces it
```

## PostgreSQL

```csharp
using PanoramicData.SoundPrint.Postgres;

var store = PostgresFingerprintStore.Create("Host=db;Database=soundprint;Username=...;Password=...", options);
await store.InitializeAsync(cancellationToken);   // applies EF Core migrations, records/checks the fingerprint parameters
```

or, with dependency injection, register `IDbContextFactory<SoundPrintDbContext>` yourself (use `SoundPrintDbContext.CreateOptions(connectionString)`) and construct `new PostgresFingerprintStore(factory, options)`.

What to know:

- `InitializeAsync` creates `tracks`, `fingerprints` and `store_info`, and refuses a database built with different fingerprint parameters (`InvalidOperationException`).
- A track is added in one transaction: a per-track advisory lock, delete of any existing copy, insert, binary `COPY`. Concurrent writes to the same id are serialised; writes to different ids run in parallel.
- Lookups batch the distinct query hashes into index-only scans and are safe to run concurrently.
- Size is about 30 to 35 bytes per landmark including indexes; see BENCHMARKS.md for measured figures and for latency against a large database.
- Run `VACUUM ANALYZE fingerprints` after a large bulk load so the planner and the visibility map are current (index-only scans depend on it).

## Choosing a confidence threshold

`Match.Confidence` is calibrated on benchmark outcomes: roughly the probability the match is right when queries are a mix of audio that is and is not in the database. The default `MatchOptions.MinConfidence` of 0.5 drops weak matches (a score of about 20.7); on real music about 0.1% of queries produced a false match at or above a score of 19.6. Guidance:

| If you want | Do |
|---|---|
| Very few false identifications (published labels, public results) | `MinConfidence = 0.9` or higher |
| Every plausible candidate for a human or an LLM to review | `MinConfidence = 0.2`, and read `Votes` and `Score` |
| To name songs as they play on a station whose whole playlist you hold | the default; most queries are present, so false matches are rarer than in the benchmark |

Per-call overrides: `matcher.MatchAsync(query, new MatchOptions { MinConfidence = 0.9 }, ct)`. Raise `MinVotes` if you want to ignore very small coincidences outright.

## Options reference

`FingerprintOptions` (every value is also folded into `ParameterFingerprint`):

| Option | Default | Effect |
|---|---|---|
| `SampleRate` | 11025 | Internal rate. 8000 is cheaper; 11025 measured much better on hard queries. |
| `WindowSize` / `HopSize` | 1024 / 256 | FFT window and step. |
| `PeakNeighbourhoodBins` / `Frames` | 8 / 3 | A peak must be the maximum within this many bins and frames. |
| `MinPeakBin`, `MinPeakPower` | 2, 1e-4 | Skip DC; ignore digital silence. |
| `BandEdges` | 7 octave-like bands | Where band balancing splits the spectrum. Must start at 0 and end at the bin count. |
| `PeaksPerBandPerBlock`, `DensityBlockFrames` | 4, 32 | Density cap: strongest peaks kept per band per block. |
| `FanOut`, `MaxDeltaFrames` | 6, 63 | Partners per anchor and how far ahead they may be (at most 63). |
| `ResamplerHalfTaps` | 16 | Resampler kernel length. |

`MatchOptions`: `MinVotes` (5), `OffsetToleranceFrames` (1), `SuppressionFrames` (16), `MaxMatches` (5), `MinConfidence` (0.5), `MinBackground` (0.05), `ConfidenceMidpoint` (20.7), `ConfidenceSlope` (0.6).

Changing any `FingerprintOptions` value that affects hashes means the database must be rebuilt: stores and matchers compare `ParameterFingerprint` and refuse to mix. Keep the source audio (or the means to regenerate it) if you may retune.

## Units and time

- Landmark times are integer **frames**; `FingerprintOptions.FramesToTime` converts exactly using integer arithmetic. `Match` times are `TimeSpan`.
- `OffsetInTrack` is track time minus query time; `TrackStart = QueryStart + OffsetInTrack`.
- The library never reads the clock. Wall-clock times (always UTC in your own code) are the caller's: record when a stream's frame 0 began and add.

## Observing peaks

Set `Fingerprinter.PeakObserver` to an `IPeakObserver` to receive the spectral peaks that survive balancing, in batches per density block. It is for visualisation and diagnostics (the browser demo draws its constellation from it) and costs nothing when unset.

## Errors you may see

| Exception | Cause |
|---|---|
| `ArgumentException` from `FingerprintOptions.Validate` | An option is out of range or inconsistent (for example `BandEdges` that do not end at the bin count). |
| `ArgumentException` from `new Matcher(store, ...)` | The store was built with different fingerprint options. |
| `InvalidOperationException` on `Push` | The stream was flushed; call `Reset()` first. |
| `InvalidOperationException` from `InitializeAsync` | The PostgreSQL database holds landmarks from different parameters. |
| `OverflowException` from `Push` | The stream outlived the frame counter; start a new fingerprinter. |

# PanoramicData.SoundPrint

[![NuGet](https://img.shields.io/nuget/v/PanoramicData.SoundPrint.svg)](https://www.nuget.org/packages/PanoramicData.SoundPrint)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![CI](https://github.com/panoramicdata/PanoramicData.SoundPrint/actions/workflows/ci.yml/badge.svg)](https://github.com/panoramicdata/PanoramicData.SoundPrint/actions/workflows/ci.yml)
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/8672c1a1685745cc9b9b3986b2b49109)](https://app.codacy.com/gh/panoramicdata/PanoramicData.SoundPrint/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)
[![Codacy Coverage](https://app.codacy.com/project/badge/Coverage/8672c1a1685745cc9b9b3986b2b49109)](https://app.codacy.com/gh/panoramicdata/PanoramicData.SoundPrint/dashboard)

Landmark-based audio fingerprinting for .NET: build, store and query fingerprint databases from broadcast audio.

Give it a few seconds of sound and it tells you **which known track it is, where in the track it is, how sure it is, and where one song ends and the next begins**. It is built for continuous streams (a radio station recorded around the clock) but works equally for single clips.

- **Robust.** On real music, 5 second clips are identified 98 to 100% of the time through MP3 64 kbit/s, AAC, Opus, radio-style EQ and compression, white noise down to 5 dB SNR, and a voice 5 dB below the music, with about 0.1% false matches.
- **Fast and flat.** 200 to 425 times real time on one core; a live stream uses under 1% of a core. A streaming fingerprinter allocates nothing, so memory is constant over weeks.
- **Honest about partial matches.** A window covering the end of one song and the start of the next returns both, each with the span of the query it explains.
- **Yours.** Written from the published landmark-hashing literature and first principles, MIT licensed, no native dependencies and no decoding: you pass mono PCM.
- **Deterministic.** The same audio produces bit-identical fingerprints however it is chunked; a golden-hash test passes on both Windows and the Linux CI runner.

> **Try it in your browser:** a Blazor WebAssembly demo runs the whole library locally (microphone, files, and a visual explanation of how it works). See [docs/DEMO.md](docs/DEMO.md).

## Contents

- [Installation](#installation) · [Quick start](#quick-start) · [Streaming](#streaming-a-live-feed) · [PostgreSQL](#postgresql-store) · [Results](#results) · [How it works](#how-it-works) · [Limits](#limits-and-honest-caveats) · [Documentation](#documentation) · [Development](#development)

## Installation

```shell
dotnet add package PanoramicData.SoundPrint            # core library, no database dependency
dotnet add package PanoramicData.SoundPrint.Postgres   # optional PostgreSQL store (EF Core)
```

Targets .NET 10. Not yet published to NuGet: until the first release, reference the projects from this repository.

| Package | Purpose |
|---|---|
| `PanoramicData.SoundPrint` | Fingerprint extraction, matching, store abstractions, in-memory store, rolling window for live streams. |
| `PanoramicData.SoundPrint.Postgres` | PostgreSQL store on EF Core: migrations, binary `COPY` bulk insert, covering-index lookups. |

## Quick start

```csharp
using PanoramicData.SoundPrint;

var options = new FingerprintOptions();                       // tuned defaults
var store = new InMemoryFingerprintStore(options);

// Index a track you have decoded to mono float samples (any sample rate).
IReadOnlyList<Landmark> landmarks = Fingerprinter.Fingerprint(trackSamples, trackSampleRate, options);
await store.AddTrackAsync("my-song-42", landmarks, cancellationToken);

// Identify a short clip.
var matcher = new Matcher(store, options);
var query = Fingerprinter.Fingerprint(clipSamples, clipSampleRate, options);
var matches = await matcher.MatchAsync(query, null, cancellationToken);

var best = matches[0];
// best.TrackId       "my-song-42"
// best.TrackStart    where in the track the clip starts (best.TrackEnd: where it ends)
// best.QueryStart    the span of the clip this match explains (best.QueryEnd)
// best.Confidence    0 to 1, calibrated; best.Votes and best.Score give the evidence
```

Track ids are opaque strings you own; the library knows nothing about songs, artists or radio. Removing a mistaken label is `await store.RemoveTrackAsync(id, ct)`, and adding an existing id replaces it.

## Streaming a live feed

```csharp
var fingerprinter = new Fingerprinter(22050, options);
var window = new RollingLandmarkWindow(TimeSpan.FromSeconds(10), options);
var chunks = 0;

foreach (var chunk in incoming.SelectMany(a => a.Chunk(22050)))
{
	fingerprinter.Push(chunk, window);

	// Every five seconds, match what has been heard in the last ten.
	if (++chunks % 5 == 0)
	{
		var matches = await matcher.MatchAsync(window.Snapshot(), null, cancellationToken);
		last = matches.Count > 0 ? matches[0] : last;
	}
}
```

Push any chunk size, from one reading thread per stream. Output lags input by about two to three seconds (peak lookahead, one density block, and the pairing zone). All times are on the stream's clock; add the UTC time at which the stream started to get wall-clock times. See [docs/USAGE.md](docs/USAGE.md) for more, and [docs/INTEGRATION.md](docs/INTEGRATION.md) for a recording-archive workflow with labelling, boundaries and corrections.

## PostgreSQL store

```csharp
using PanoramicData.SoundPrint.Postgres;

var store = PostgresFingerprintStore.Create("Host=db;Database=soundprint;Username=...;Password=...", options);
await store.InitializeAsync(cancellationToken);   // applies migrations; refuses a database built with different parameters
```

Tracks are written in one transaction with binary `COPY`; lookups are index-only scans on `fingerprints (hash) INCLUDE (track, frame)`. The matcher works identically over either store.

## Results

All figures below are measured by commands in this repository; methodology, caveats and the full tables are in [docs/BENCHMARKS.md](docs/BENCHMARKS.md).

### Accuracy on real music

480 Creative Commons tracks in the database (Free Music Archive; sources and licences in [benchmarks/SOURCES.md](benchmarks/SOURCES.md), audio not committed), 120 more held out as "absent" queries, 300 queries per cell. Recall means the right track is the best match and clears the recommended threshold.

| Degradation | 3 s clip | 5 s clip | 7 s clip | 10 s clip |
|---|---|---|---|---|
| clean | 100.0% | 100.0% | 100.0% | 100.0% |
| white noise, 5 dB SNR | 99.0% | 98.7% | 99.7% | 100.0% |
| white noise, 0 dB SNR | 94.0% | 98.0% | 98.7% | 100.0% |
| radio EQ + compression + limiter | 100.0% | 100.0% | 100.0% | 100.0% |
| MP3 64 kbit/s | 100.0% | 100.0% | 100.0% | 100.0% |
| AAC 64 kbit/s | 100.0% | 100.0% | 100.0% | 100.0% |
| Opus 24 kbit/s | 100.0% | 100.0% | 100.0% | 100.0% |
| voice 10 dB below the music | 97.0% | 98.7% | 99.7% | 100.0% |
| voice 5 dB below the music | 91.7% | 98.7% | 98.7% | 99.3% |
| voice as loud as the music | 74.0% | 89.7% | 94.7% | 97.7% |
| radio + MP3 96k + voice + noise combined | 95.7% | 98.7% | 98.7% | 99.3% |

- **Minimum clip: about 5 seconds** for reliable identification under broadcast-style degradation; 3 seconds works for clean or codec-only audio.
- **Recommended threshold:** a match score of 19.6, at which the pooled false-match rate was at most 0.1%. The default `MatchOptions.MinConfidence` of 0.5 corresponds to a score of 20.7, slightly stricter. Raise it (0.9 and up) when a wrong label is costly; lower it to gather candidates for review.
- **Position:** 98.4% of identified queries also had the matched position within 0.4 s of the true one.
- **Song boundaries:** a 10 s window straddling two songs found both in 98% of trials; the boundary was within a median of 0.06 s (hard cut) or 0.16 s (one-second crossfade).

### Speed, memory, size and latency

| | Result |
|---|---|
| Extraction speed, one core | about 425x real time from 8 kHz audio, about 200x from 44.1 kHz audio |
| Allocation while streaming | none (verified by a test and by BenchmarkDotNet) |
| Landmarks per second of audio | about 165 for real music (about 120 for synthetic) |
| In-memory store | about 16 bytes per landmark: roughly 9.4 MB per hour of real music |
| PostgreSQL store | about 92 bytes per landmark including indexes: roughly 55 MB per hour of real music |
| 10 s query, in-memory, 100,000 tracks | median **214 ms** (95th percentile 358 ms), 7.7 GB of RAM (synthetic tracks, measured) |
| 10 s query, PostgreSQL, 20,000 tracks | median **312 ms** (95th percentile 613 ms) (synthetic tracks, measured) |
| 10 s query, PostgreSQL, 100,000 tracks | not measured; expect on the order of 1.5 s with the current schema (latency grows with database size) |
| 10 s query, in-memory, 480 real tracks | median 2.4 ms |

For capacity planning: 100,000 tracks of 3.5 minutes is about 5,800 hours of audio, roughly 55 GB in memory or 320 GB in PostgreSQL at real-music density. Storage in PostgreSQL is the first thing to optimise if that matters.

## How it works

Spectral peaks are paired into landmarks of (frequency 1, frequency difference, time difference) plus the time of the first peak, stored as `hash -> (track, frame)`. A query's landmarks are looked up and each hit votes for the offset between query and track; a real match concentrates its votes on one offset, coincidences scatter. The vote is scored against the level of coincidences the same track received elsewhere, and the score is mapped to a calibrated confidence. Full description: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). The algorithm follows A. Wang, "An Industrial-Strength Audio Search Algorithm" (2003), written from the paper and first principles with no code taken from other fingerprinting libraries.

Parameters were chosen by measurement; each decision and its effect is logged in [docs/TUNING.md](docs/TUNING.md). The biggest wins were an 11.025 kHz internal sample rate (+20 points of recall on hard queries over 8 kHz) and fewer, stronger peaks per band.

## Limits and honest caveats

- **Not an audio decoder.** Pass decoded mono PCM.
- **Speed and pitch changes break matching.** Playing a track noticeably faster or slower, or pitch-shifting it, changes the timing and frequencies the hash encodes. Drift of about a percent is tolerated.
- **Covers, remixes and live versions are different recordings** and will not match the original.
- **Very short, very sparse or speech-only audio** produces too few landmarks to vote reliably. A voice as loud as the music is the hardest case measured.
- **The benchmarks are simulations on a small real sample.** 600 Creative Commons tracks, simulated radio processing and a synthetic voice; results on a real station's feed will differ. Real-music results on a larger and more varied corpus have not been measured, and PostgreSQL latency at 100,000 tracks has not been measured.
- **Confidence is calibrated to the benchmark's mix** of present and absent queries; if almost all your queries are for tracks that are in the database, false matches will be rarer than the benchmark suggests, and if almost none are, rarer still matches deserve a higher threshold.
- **Patents.** The core landmark-hashing patents (US 6,990,453 and its family) appear to have expired (roughly 2021), but this is from public listings, not legal advice, and other parties' patents were not searched. Take advice before building a commercial service on it. Details in [docs/DESIGN.md](docs/DESIGN.md).

## Documentation

| Document | For |
|---|---|
| [docs/USAGE.md](docs/USAGE.md) | The API in depth: streaming, stores, thresholds, options, errors |
| [docs/INTEGRATION.md](docs/INTEGRATION.md) | Wiring it into a continuous recording system |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | The algorithm and the code that implements it |
| [docs/BENCHMARKS.md](docs/BENCHMARKS.md) | Methodology, full results, caveats |
| [docs/TUNING.md](docs/TUNING.md) | Every parameter decision and its measured effect |
| [docs/DESIGN.md](docs/DESIGN.md) | Original design note (with an as-built appendix) and the patent check |
| [docs/DEMO.md](docs/DEMO.md) | The browser demo |
| [benchmarks/SOURCES.md](benchmarks/SOURCES.md) | Licensed test audio used by the local benchmark |

## Development

```shell
dotnet build                                   # warnings are errors
dotnet test --project PanoramicData.SoundPrint.Test                    # unit tests (xUnit v3, skips count as failures)
dotnet test --project PanoramicData.SoundPrint.IntegrationTest         # PostgreSQL in a container; needs Docker, fails (not skips) without it
dotnet run --project PanoramicData.SoundPrint.Demo                     # the browser demo
dotnet run -c Release --project PanoramicData.SoundPrint.Benchmarks -- # lists the benchmark commands
```

Quality: the core library has 100% line and branch coverage from the unit tests (Microsoft code coverage; settings in `coverage.config`), CI runs unit and PostgreSQL integration tests with coverage, and all test audio in the repository is synthetic: no recordings, broadcast audio or real fingerprint data are committed. Test projects, the synthetic audio generator, the demo's JavaScript and the benchmark harness are excluded from Codacy analysis (`.codacy.yml`) because they use seeded `System.Random` and throwaway SQL that pattern-based security rules flag.

| Project | Contents |
|---|---|
| `PanoramicData.SoundPrint` | The library |
| `PanoramicData.SoundPrint.Postgres` | PostgreSQL store and migrations |
| `PanoramicData.SoundPrint.Test` | Unit tests |
| `PanoramicData.SoundPrint.IntegrationTest` | PostgreSQL integration tests (Testcontainers) |
| `PanoramicData.SoundPrint.SyntheticAudio` | Synthetic music and speech, degradations, ffmpeg wrapper |
| `PanoramicData.SoundPrint.Benchmarks` | Accuracy, tuning, latency, speed and real-music runners |
| `PanoramicData.SoundPrint.Demo` | Blazor WebAssembly demo |

Contributions: see [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md). Never commit recordings, copyrighted music, credentials or real fingerprint data.

## Links

- NuGet: https://www.nuget.org/packages/PanoramicData.SoundPrint (once published)
- Source: https://github.com/panoramicdata/PanoramicData.SoundPrint
- Issues: https://github.com/panoramicdata/PanoramicData.SoundPrint/issues

## License

MIT, see [LICENSE](LICENSE).

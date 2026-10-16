# Benchmarks

Every number in the README comes from a command in this repository and a results file under [`benchmarks/results`](../benchmarks/results). This page explains what is measured, how, and what the numbers do and do not show. Machine: Intel Core i9-11900K, 96 GB, Windows 11, .NET 10. Commands run from the repository root.

## What is measured

| Question | Command | Results file |
|---|---|---|
| How accurate on real music, by degradation and clip length? | `dotnet run -c Release --project PanoramicData.SoundPrint.Benchmarks -- real` | [real-music.md](../benchmarks/results/real-music.md) |
| The same on synthetic music (reproducible without any download)? | `... -- accuracy` | [synthetic-accuracy.md](../benchmarks/results/synthetic-accuracy.md) |
| How fast is extraction, and does it allocate? | `... -- speed` | [speed.md](../benchmarks/results/speed.md) |
| How long does a query take against 100,000 tracks, and how big is the database? | `... -- latency --tracks 100000` | [latency-inmemory.md](../benchmarks/results/latency-inmemory.md) |
| The same on PostgreSQL | `... -- latency --tracks 20000 --postgres "<connection string>"` | [latency-postgres.md](../benchmarks/results/latency-postgres.md) |
| Which parameters, and why? | `... -- tune` | [TUNING.md](TUNING.md) |

Re-encode scenarios need ffmpeg (`PATH` or `SOUNDPRINT_FFMPEG`); without it the runs stop with a message saying so. The real-music benchmark needs the Creative Commons audio downloaded as described in [benchmarks/SOURCES.md](../benchmarks/SOURCES.md); without it the command prints `SKIPPED` and exits with code 2.

## Accuracy method

- A **database** of tracks is fingerprinted from clean audio. **Queries** are clips cut from degraded copies of the tracks (the whole track is degraded, as a broadcast chain would, then clips are sliced), plus **absent** queries cut from tracks that are *not* in the database. 300 queries per cell, 120 absent tracks held out.
- **Real music:** 600 tracks sampled evenly from the 8,000-track `fma_small` set of Creative Commons music (Free Music Archive), 29 s each: 480 in the database, 120 held out. Listed with their licences in [SOURCES.md](../benchmarks/SOURCES.md). Audio is decoded to 22.05 kHz mono with ffmpeg and never leaves the machine.
- **Degradations:** white noise at 20, 10, 5 and 0 dB SNR; EQ, compression and a limiter (a typical radio chain); MP3 at 128 and 64 kbit/s, AAC at 64 kbit/s and Opus at 24 kbit/s (ffmpeg); a synthetic presenter's voice mixed in at -10, -5 and 0 dB relative to the music; and a combined chain (radio processing, MP3 96 kbit/s, voice at -8 dB, noise at 25 dB). Query start times are random.
- **Straddling clips:** a 10 s window made of the end of one track and the start of another, with the join 2.5 to 7.5 s in, as a hard cut or a one-second crossfade, optionally after radio processing.
- **Metrics:**
  - *Top-1*: the best match is the right track (regardless of threshold).
  - *Recall at threshold*: the right track is the best match and its score clears the recommended threshold.
  - *False positives at threshold*: absent queries that produced a match above the threshold.
  - *Pooled false-match rate*: absent queries that matched plus present queries matched to the wrong track, over all queries of 5 s or longer; the recommended threshold is the lowest score at which this is at most 0.1%.
  - *Position accuracy*: of identified queries, how many also had the matched position within 0.4 s. Identification is judged by track because loop-based music repeats exactly and can legitimately match at several offsets.
- **Duplicates** (the same recording published twice) would count a correct match as a false one, so tracks that strongly match an earlier track are dropped before the split. None were found in this sample.

## Headline results (real music, default options)

At the recommended score threshold (19.6; the default `MinConfidence` of 0.5 is slightly stricter), recall by query length and degradation. The full table, with top-1 and false positives per cell, is in [real-music.md](../benchmarks/results/real-music.md).

| Degradation | 3 s | 5 s | 7 s | 10 s |
|---|---|---|---|---|
| clean | 100.0% | 100.0% | 100.0% | 100.0% |
| white noise 10 dB SNR | 98.3% | 100.0% | 100.0% | 100.0% |
| white noise 5 dB SNR | 99.0% | 98.7% | 99.7% | 100.0% |
| white noise 0 dB SNR | 94.0% | 98.0% | 98.7% | 100.0% |
| radio EQ + compression + limiter | 100.0% | 100.0% | 100.0% | 100.0% |
| MP3 64 kbit/s | 100.0% | 100.0% | 100.0% | 100.0% |
| AAC 64 kbit/s | 100.0% | 100.0% | 100.0% | 100.0% |
| Opus 24 kbit/s | 100.0% | 100.0% | 100.0% | 100.0% |
| voice 10 dB below the music | 97.0% | 98.7% | 99.7% | 100.0% |
| voice 5 dB below the music | 91.7% | 98.7% | 98.7% | 99.3% |
| voice level with the music | 74.0% | 89.7% | 94.7% | 97.7% |
| combined chain | 95.7% | 98.7% | 98.7% | 99.3% |

- **Reliable from 5 seconds**: 5 s clips are identified 98 to 100% of the time through every degradation tested except a voice as loud as the music (89.7%). 3 s is workable for clean and codec-degraded audio but drops under heavy noise or loud talk-over. 10 s clips reach 97.7% or better in every scenario.
- **False matches**: at the threshold, the pooled false-match rate is at most 0.1%. Individual cells show 0.3 to 0.7% in places because one false query in 300 is 0.33%.
- **Position**: 98.4% of identified queries also had the matched position within 0.4 s.
- **Song boundaries**: both songs found in 98.0% of straddling windows; median boundary error 0.06 s for a hard cut, 0.16 s for a one-second crossfade (90th percentile 0.17 s and 0.41 s).

## Speed and memory

From [speed.md](../benchmarks/results/speed.md): 60 s of audio fingerprints in about 141 ms from 8 kHz input (425 times real time) and about 297 ms from 44.1 kHz input (200 times real time), on one core. A warmed streaming fingerprinter **allocates nothing**, so memory stays flat however long a stream runs. A live stream uses well under 1% of one core.

## Database size

| | Landmarks per second of audio | Per hour of audio |
|---|---|---|
| Real music (FMA subset) | 165 | 594,000 landmarks |
| Synthetic music (100,000 tracks) | 121 | 437,000 landmarks |

| Store | Bytes per landmark | Per hour of real music | Measured at |
|---|---|---|---|
| In-memory | 15.9 | about 9.4 MB | 485 million landmarks (100,000 tracks) |
| PostgreSQL, covering index plus track index | 92 | about 55 MB | 97 million landmarks (20,000 tracks) |

For scale: a catalogue of 100,000 three-and-a-half-minute tracks is about 5,800 hours: roughly 55 GB in memory or 320 GB in PostgreSQL at real-music density. PostgreSQL's per-row and per-index overhead is the dominant cost; there is room to reduce it (a packed layout, or dropping the track index in favour of partitioning) and this is the first thing to work on if storage matters.

## Match latency

| Store | Database | Query | Median | 95th percentile | Hits returned (median) |
|---|---|---|---|---|---|
| In-memory | 480 real tracks | 10 s | 2.4 ms | 20 ms | not recorded |
| In-memory | 1,000 synthetic tracks | 10 s | 1.7 ms | | |
| In-memory | 5,000 synthetic tracks | 10 s | 5.6 ms | | |
| In-memory | **100,000 synthetic tracks** (7.7 GB) | 10 s | **214 ms** | 358 ms | 1.57 million |
| PostgreSQL | 20,000 synthetic tracks | 10 s | **312 ms** | 613 ms | 312,000 |

Latency is dominated by the number of hits returned, which grows linearly with database size. **The PostgreSQL figure at 100,000 tracks was not measured**; by the same linear growth it would be of the order of 1.5 seconds per 10 s query on this configuration (a throwaway local container with durability settings relaxed for bulk loading). The in-memory 100,000-track figure is measured. If sub-second PostgreSQL queries at that scale matter, the options are to cap how many postings a very common hash contributes, keep a hot in-memory tier, or partition by hash; none is implemented yet.

## Caveats, stated plainly

- **Synthetic music is not real music.** It is deliberately varied (each song has its own tuning, tempo, harmony and drums) but its pitches come from a handful of scales, so its hashes are more concentrated than real music's. Concentrated hashes mean more coincidental hits per query, which may make the synthetic latency figures pessimistic, while real music is denser in landmarks (165 against 121 per second), which pushes the other way; real-music latency at this scale was not measured. Real-music accuracy is the better guide; synthetic music is for reproducibility and for scale tests that cannot use 100,000 real tracks.
- **The real-music sample is small** (600 tracks of 29 s, one genre mix, one dataset) and the absent set is only 120 tracks. A larger and more varied corpus, especially of long tracks with intros and outros, would be a stronger test and would likely produce a few more false matches.
- **The degradations are simulated.** Radio processing is a single compressor and EQ chain, not a real station's multi-band processor; the "voice" is synthesised speech-like audio, not a human announcer; noise is white. Real broadcast audio, including FM multipath and DAB artefacts, was not available to test.
- **Absent queries are fewer than present ones in the confidence calibration** by construction, and confidence is calibrated to this 50/50 mix. See [TUNING.md](TUNING.md).
- **One machine, one run per cell, fixed seeds.** Speed figures use BenchmarkDotNet's short job.
- **The PostgreSQL run used a throwaway container** with `fsync` and `full_page_writes` off for bulk loading; query latency is not affected by that, but build time is.

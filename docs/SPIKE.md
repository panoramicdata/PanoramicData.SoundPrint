# Phase 3 spike results

Throwaway code: `PanoramicData.SoundPrint.Benchmarks/Spike/SpikeCode.cs` and the `spike` command of
`PanoramicData.SoundPrint.Benchmarks/Program.cs`. It is offline, allocation-heavy, untested and is not carried into the library.
It exists to prove the pipeline and to put real numbers behind [DESIGN.md](DESIGN.md).

Run it with `dotnet run -c Release --project PanoramicData.SoundPrint.Benchmarks -- spike`.

## Setup

- 100 synthetic songs, 60 s each, 8 kHz mono, generated deterministically (melody with harmonics, bass line, hi-hat noise).
- STFT: 1024-sample Hann window, 256-sample hop (32 ms), 513 bins.
- Peaks: local maxima over +/-12 bins by +/-3 frames, then the strongest 6 per band per second across 7 bands.
- Pairs: fan-out 5, target zone 1 to 63 frames ahead, frequency delta -128 to +127 bins.
- Hash: 23 bits used of 32 (f1 9 bits, delta f 8 bits, delta t 6 bits).
- Match: offset-histogram voting with one-frame bins.
- Queries: random clips from database songs, plus clips from 100 songs that are not in the database.

## Results (one run, Windows, single thread, unoptimised)

| Measure | Result |
|---|---|
| Fingerprinting speed | 6000 s of audio in 8.4 s, about 713 times real time |
| Hashes | 98.8 per second of audio, 355,651 per hour |
| Match latency (100 tracks, in-memory dictionary) | 0.1 to 0.4 ms per query |

Top-1 accuracy on 200 random clips per cell, and median winning vote count:

| Clip | Clean | SNR 10 dB (white noise) | SNR 0 dB (white noise) |
|---|---|---|---|
| 3 s | 100.0% (95 votes) | 79.5% (12) | 75.5% (9) |
| 5 s | 100.0% (174) | 94.5% (21) | 86.5% (14) |
| 10 s | 100.0% (343) | 99.5% (46) | 99.5% (31) |

Start offset was recovered to within 2 frames (64 ms) in every correct match, so the reported position is accurate to a frame or two.

Clips from songs not in the database (the false-positive population): best-track votes had median 8 to 10, 99th percentile 16 and
maximum 17 for both 5 s and 10 s clips.

## What it shows

1. The landmark pipeline works end to end: clean clips of 3 s and longer match 100% with the right offset.
2. Speed has large headroom. Even unoptimised, the 50 times real time requirement is exceeded about 14 times over.
3. Clean accuracy is not the problem; noise is. At 5 s and 0 dB the winning vote count (median 14) sits inside the range of
   false-positive votes (up to 17), so a single fixed vote threshold cannot yet separate the two. Two things will be worked on in
   phase 6: noise-robust peak picking (the current picker takes the strongest peaks per band, which noise displaces), and a
   confidence score that compares the winning bin against the background rather than using a raw count.
4. The false-positive vote levels are inflated by the synthetic songs sharing a note vocabulary, so they say little about real
   music. They will be re-measured on the Creative Commons set.
5. Not yet measured, and deliberately out of scope for the spike: re-encode, EQ and compression, voice-over, straddling clips,
   and the 8 kHz versus 11.025 kHz choice.

## Caveats

One run with fixed seeds, on one machine. Speed numbers are indicative only; the real benchmarks use BenchmarkDotNet in phase 6.

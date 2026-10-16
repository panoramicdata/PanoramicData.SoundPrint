# Real-music results (Free Music Archive, Creative Commons)

Generated 2026-10-16 13:54 UTC by `dotnet run -c Release --project PanoramicData.SoundPrint.Benchmarks -- real --source fma`.

Database: 480 real tracks of 29 s (3.87 hours) decoded to 22050 Hz mono and fingerprinted at 8000 Hz; 120 further tracks held out as absent queries. 300 queries per cell. Re-encode and chain scenarios use 120 tracks. Source and licences: benchmarks/SOURCES.md. Audio stays local and is not committed.

## Confidence calibration and threshold

Logistic fit of P(correct) against match score over all queries of 5 s or longer: midpoint 20.74, slope 0.595 (25200 queries).
Recommended threshold: score 19.6, which is confidence 0.3373. At that threshold the pooled false-match rate (absent queries that matched plus present queries matched to the wrong place, over all queries of 5 s or longer) is at most 0.1%.

## Results by scenario and query length

Each cell is `top-1 / recall at threshold / false positives at threshold`. Top-1 ignores the threshold (the best match is right); recall counts a query only when the right match also clears the threshold; false positives are absent queries that produced a match above the threshold.

| Group | Scenario | 3 s | 5 s | 7 s | 10 s |
|---|---|---|---|---|---|
| clean | clean | 100.0% / 100.0% / 0.3% | 100.0% / 100.0% / 0.0% | 100.0% / 100.0% / 0.7% | 100.0% / 100.0% / 0.0% |
| noise | white noise 20 dB SNR | 100.0% / 100.0% / 0.0% | 100.0% / 100.0% / 0.0% | 100.0% / 100.0% / 0.3% | 100.0% / 100.0% / 0.7% |
| noise | white noise 10 dB SNR | 100.0% / 98.3% / 0.7% | 100.0% / 100.0% / 0.3% | 100.0% / 100.0% / 0.0% | 100.0% / 100.0% / 0.0% |
| noise | white noise 5 dB SNR | 100.0% / 99.0% / 0.0% | 99.3% / 98.7% / 0.3% | 100.0% / 99.7% / 0.0% | 100.0% / 100.0% / 0.3% |
| noise | white noise 0 dB SNR | 97.3% / 94.0% / 0.3% | 99.0% / 98.0% / 0.0% | 100.0% / 98.7% / 0.3% | 100.0% / 100.0% / 0.7% |
| radio | EQ + compression + limiter | 100.0% / 100.0% / 0.0% | 100.0% / 100.0% / 0.0% | 100.0% / 100.0% / 0.3% | 100.0% / 100.0% / 0.3% |
| re-encode | MP3 128 kbit/s | 100.0% / 100.0% / 0.3% | 100.0% / 100.0% / 0.3% | 100.0% / 100.0% / 0.3% | 100.0% / 100.0% / 0.0% |
| re-encode | MP3 64 kbit/s | 100.0% / 100.0% / 0.0% | 100.0% / 100.0% / 0.3% | 100.0% / 100.0% / 0.0% | 100.0% / 100.0% / 0.0% |
| re-encode | AAC 64 kbit/s | 100.0% / 100.0% / 0.3% | 100.0% / 100.0% / 0.3% | 100.0% / 100.0% / 0.0% | 100.0% / 100.0% / 0.3% |
| re-encode | Opus 24 kbit/s | 100.0% / 100.0% / 0.0% | 100.0% / 100.0% / 0.3% | 100.0% / 100.0% / 0.0% | 100.0% / 100.0% / 0.0% |
| voice-over | voice 10 dB below music | 98.3% / 97.0% / 0.0% | 100.0% / 98.7% / 0.0% | 100.0% / 99.7% / 0.0% | 100.0% / 100.0% / 0.0% |
| voice-over | voice 5 dB below music | 97.7% / 91.7% / 0.0% | 99.3% / 98.7% / 0.0% | 100.0% / 98.7% / 0.0% | 100.0% / 99.3% / 0.0% |
| voice-over | voice level with music | 88.0% / 74.0% / 0.0% | 94.3% / 89.7% / 0.3% | 97.3% / 94.7% / 0.3% | 99.3% / 97.7% / 0.0% |
| chain | radio + MP3 96k + voice -8 dB + noise 25 dB | 98.3% / 95.7% / 0.0% | 98.7% / 98.7% / 0.0% | 99.0% / 98.7% / 0.0% | 99.3% / 99.3% / 0.3% |

Position accuracy: of the queries of 5 s or longer whose track was identified above the threshold, 98.4% also had the matched position within 0.4 s of the true one (the rest matched the right track at another offset, as happens with loop-based music that repeats exactly).

## Match latency by query length (this machine, in-memory store)

| Query length | Median ms | 95th percentile ms |
|---|---|---|
| 3 s | 0.59 | 1.35 |
| 5 s | 1.01 | 2.72 |
| 7 s | 1.44 | 4.04 |
| 10 s | 2.08 | 9.93 |

## Clips straddling two songs (10 s window, boundary 2.5 to 7.5 s in)

A trial succeeds when both songs are identified at the right positions, in the right order, with the matcher's default options and no threshold. Boundary error compares the midpoint between the first song's matched end and the second song's matched start with the true boundary.

| Join | Both found | Boundary error median | Boundary error 90th percentile |
|---|---|---|---|
| hard cut | 98.0% | 0.06 s | 0.17 s |
| 1 s crossfade | 98.5% | 0.16 s | 0.41 s |
| hard cut after radio processing | 98.5% | 0.06 s | 0.19 s |

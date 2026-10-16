# Accuracy results

Generated 2026-10-16 14:04 UTC by `dotnet run -c Release --project PanoramicData.SoundPrint.Benchmarks -- accuracy`.

Database: 300 synthetic songs of 60 s (5.0 hours), generated at 22050 Hz and fingerprinted at 8000 Hz. 300 queries per cell with the answer in the database (present) and 300 without (absent). Synthetic songs never repeat exactly, which real music does, so the real-music benchmark is the better guide to absolute numbers.

## Confidence calibration and threshold

Logistic fit of P(correct) against match score over all queries of 5 s or longer: midpoint 35.66, slope 0.176 (25200 queries).
Recommended threshold: score 50.1, which is confidence 0.9274. At that threshold the pooled false-match rate (absent queries that matched plus present queries matched to the wrong place, over all queries of 5 s or longer) is at most 0.1%.

## Results by scenario and query length

Each cell is `top-1 / recall at threshold / false positives at threshold`. Top-1 ignores the threshold (the best match is right); recall counts a query only when the right match also clears the threshold; false positives are absent queries that produced a match above the threshold.

| Group | Scenario | 3 s | 5 s | 7 s | 10 s |
|---|---|---|---|---|---|
| clean | clean | 100.0% / 99.0% / 0.0% | 100.0% / 100.0% / 1.3% | 100.0% / 100.0% / 0.3% | 100.0% / 100.0% / 2.3% |
| noise | white noise 20 dB SNR | 99.3% / 75.3% / 0.0% | 100.0% / 96.0% / 0.3% | 100.0% / 98.3% / 0.0% | 100.0% / 100.0% / 0.3% |
| noise | white noise 10 dB SNR | 97.0% / 46.3% / 0.0% | 99.0% / 80.3% / 0.0% | 100.0% / 93.7% / 0.0% | 100.0% / 98.7% / 0.0% |
| noise | white noise 5 dB SNR | 95.7% / 35.7% / 0.0% | 98.3% / 72.7% / 0.0% | 99.7% / 88.3% / 0.0% | 100.0% / 97.0% / 0.0% |
| noise | white noise 0 dB SNR | 89.0% / 18.3% / 0.0% | 97.3% / 55.0% / 0.3% | 99.7% / 80.0% / 0.0% | 100.0% / 90.7% / 0.0% |
| radio | EQ + compression + limiter | 96.3% / 37.7% / 0.0% | 99.0% / 79.7% / 0.0% | 100.0% / 95.0% / 0.0% | 100.0% / 99.7% / 0.0% |
| re-encode | MP3 128 kbit/s | 100.0% / 92.0% / 0.0% | 100.0% / 100.0% / 0.3% | 100.0% / 100.0% / 0.3% | 100.0% / 100.0% / 0.3% |
| re-encode | MP3 64 kbit/s | 99.3% / 86.7% / 0.0% | 100.0% / 98.7% / 0.3% | 100.0% / 100.0% / 0.0% | 100.0% / 100.0% / 0.3% |
| re-encode | AAC 64 kbit/s | 100.0% / 93.0% / 0.3% | 100.0% / 100.0% / 0.3% | 100.0% / 100.0% / 0.7% | 100.0% / 100.0% / 0.7% |
| re-encode | Opus 24 kbit/s | 100.0% / 62.7% / 0.0% | 100.0% / 93.0% / 0.0% | 100.0% / 98.7% / 0.0% | 100.0% / 100.0% / 0.0% |
| voice-over | voice 10 dB below music | 97.7% / 61.3% / 0.0% | 99.7% / 93.7% / 0.0% | 100.0% / 97.0% / 0.0% | 100.0% / 100.0% / 0.0% |
| voice-over | voice 5 dB below music | 94.7% / 39.3% / 0.0% | 100.0% / 81.0% / 0.0% | 100.0% / 91.7% / 0.0% | 100.0% / 100.0% / 0.0% |
| voice-over | voice level with music | 86.7% / 13.0% / 0.0% | 97.3% / 50.3% / 0.0% | 98.0% / 63.3% / 0.0% | 99.7% / 87.0% / 0.0% |
| chain | radio + MP3 96k + voice -8 dB + noise 25 dB | 86.7% / 11.0% / 0.0% | 96.7% / 45.7% / 0.0% | 99.3% / 70.7% / 0.0% | 99.7% / 88.7% / 0.0% |

Position accuracy: of the queries of 5 s or longer whose track was identified above the threshold, 100.0% also had the matched position within 0.4 s of the true one (the rest matched the right track at another offset, as happens with loop-based music that repeats exactly).

## Match latency by query length (this machine, in-memory store)

| Query length | Median ms | 95th percentile ms |
|---|---|---|
| 3 s | 0.19 | 0.52 |
| 5 s | 0.31 | 0.63 |
| 7 s | 0.43 | 1.02 |
| 10 s | 0.63 | 1.60 |

## Clips straddling two songs (10 s window, boundary 2.5 to 7.5 s in)

A trial succeeds when both songs are identified at the right positions, in the right order, with the matcher's default options and no threshold. Boundary error compares the midpoint between the first song's matched end and the second song's matched start with the true boundary.

| Join | Both found | Boundary error median | Boundary error 90th percentile |
|---|---|---|---|
| hard cut | 99.7% | 0.10 s | 0.23 s |
| 1 s crossfade | 100.0% | 0.14 s | 0.32 s |
| hard cut after radio processing | 96.0% | 0.11 s | 0.39 s |

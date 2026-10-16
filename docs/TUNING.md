# Tuning log

Every parameter change to the defaults is recorded here with its measured effect. The method, the harness and the data it ran on are described first so the numbers can be reproduced.

## Method

- **Tool:** `dotnet run -c Release --project PanoramicData.SoundPrint.Benchmarks -- tune` (and `tune --combos` for combinations). Source: `PanoramicData.SoundPrint.Benchmarks/TuningRunner.cs`.
- **Data:** 200 synthetic songs of 60 s in the database, 200 queries per scenario with the answer present and 200 absent. Songs are generated at 22.05 kHz, so the library's resampler is in the path. Synthetic songs never repeat exactly, and each has its own tuning, kick drum and starting offset, so two songs do not line up sound for sound. (An earlier generator without those properties let unrelated songs score falsely high, which distorted a first round of tuning; that round was discarded.)
- **Scenarios (the hard ones, 5 s queries):** white noise at 5 dB SNR, EQ plus compression plus limiter ("radio"), a synthetic presenter's voice 5 dB below the music, and the full chain (radio, MP3 96 kbit/s, voice at -8 dB, noise at 25 dB SNR).
- **Metrics:**
  - *Top-1*: the best match is the right track.
  - *Recall at 0.1% false matches*: the share of present queries that are right *and* score above the lowest score at which at most 0.1% of all queries are false (absent queries that matched, or present queries matched to the wrong track). This is the figure that matters, because it needs a threshold that keeps false matches rare.
  - *Landmarks per second*: storage cost.
  - *Extraction speed*: multiples of real time, one core, one 60 s song, measured while the machine was busy with the harness itself, so treat it as a lower bound.
- **Noise in the numbers:** recall at 0.1% depends on the one or two worst false matches in 1,600 queries, so differences of a few points between rows are within noise. Larger differences were repeated before being acted on.

## Round 1: one change at a time

Baseline is the original defaults: 8 kHz, window 1024, hop 256, neighbourhood 12 bins by 3 frames, 6 peaks per band per second, fan-out 5, target zone 63 frames.

| Variant | Top-1 | Recall at 0.1% false | Landmarks/s | Extraction (x real time) |
|---|---|---|---|---|
| baseline (defaults) | 97.5% | 65.9% | 79 | 268 |
| fan-out 3 | 93.6% | 48.9% | 48 | 239 |
| fan-out 8 | 98.5% | 71.2% | 123 | 278 |
| peaks per band 4 | 97.5% | 79.2% | 60 | 268 |
| peaks per band 9 | 97.6% | 66.1% | 101 | 312 |
| neighbourhood 8 bins | 98.1% | 78.1% | 88 | 247 |
| neighbourhood 18 bins | 97.0% | 76.5% | 67 | 208 |
| neighbourhood 2 frames | 96.6% | 71.4% | 85 | 295 |
| neighbourhood 5 frames | 95.1% | 62.6% | 72 | 327 |
| hop 128 | 98.5% | 76.5% | 135 | 158 |
| window 512, hop 128 | 94.4% | 63.6% | 141 | 307 |
| internal rate 11025 Hz | 99.2% | 86.4% | 106 | 227 |
| target zone 40 frames | 97.5% | 64.9% | 76 | 265 |

What this said:

- The **internal sample rate** was the biggest single lever: 11.025 kHz gained 20 points of recall at the same query length. A likely reason (not isolated by a separate experiment) is that content between 4 and 5.5 kHz survives broadcast and codec processing well enough to add discriminating peaks, which 8 kHz discards.
- **Fan-out** trades storage for recall: more partners per anchor means more landmarks survive noise.
- **Fewer peaks per band and a tighter neighbourhood** helped, because noise creates many weak local maxima and keeping only the strongest few per band and second makes the fingerprint more selective.
- A larger frame neighbourhood (5 frames) and a shorter target zone (40 frames) did not help; a smaller window (512) lost frequency resolution and hurt.
- A denser hop (128) helped at a cost of speed and storage, and was not pursued because the combinations below reached the same recall more cheaply.

## Round 2: combinations at 11.025 kHz

| Variant | Top-1 | Recall at 0.1% false | Landmarks/s | Extraction (x real time) |
|---|---|---|---|---|
| 11025 Hz | 99.2% | 86.4% | 106 | 295 |
| 11025 Hz, fan-out 8 | 99.6% | 94.4% | 166 | 366 |
| 11025 Hz, 8-bin neighbourhood | 99.5% | 79.8% | 121 | 328 |
| 11025 Hz, peaks per band 4 | 99.5% | 85.9% | 79 | 237 |
| 11025 Hz, fan-out 8, 8 bins | 100.0% | 91.8% | 190 | 307 |
| **11025 Hz, fan-out 6, 8 bins, peaks 4** | **99.9%** | **93.8%** | **107** | 298 |
| 11025 Hz, fan-out 8, 8 bins, peaks 4 | 99.9% | 96.6% | 139 | 257 |
| 8000 Hz, fan-out 8, 8 bins, peaks 4 | 99.5% | 84.8% | 104 | 335 |

## Decision

Defaults changed to **11,025 Hz, fan-out 6, a neighbourhood of 8 bins by 3 frames, and 4 peaks per band per block** (window 1024, hop 256 and target zone 63 frames unchanged).

| | Baseline | New defaults | Effect |
|---|---|---|---|
| Top-1 (hard cases, 5 s) | 97.5% | 99.9% | +2.4 points |
| Recall at 0.1% false matches | 65.9% | 93.8% | +27.9 points |
| Landmarks per second (synthetic) | 79 | 107 | +35% storage |
| Extraction speed | about 270x | about 300x | unchanged within noise |

The variant with fan-out 8 reached 96.6% recall but at 139 landmarks per second, 30% more storage than fan-out 6 for 2.8 points. Storage dominates the cost of a large catalogue, so fan-out 6 was chosen; `FanOut = 8` is one option away for anyone who prefers recall. The 8 kHz rate remains available through `FingerprintOptions.SampleRate`.

## Confidence calibration

`MatchOptions.ConfidenceMidpoint` and `ConfidenceSlope` are a logistic fit of "the best match was the right track" against the match score, over every query of 5 s or longer in the real-music benchmark (25,200 queries):

- midpoint **20.7**, slope **0.60** (the earlier synthetic-only fit was 35.7 and 0.18: synthetic music produces higher false scores because its notes come from a small set of scales, so it is the stricter test, and real music is the better guide for the default).
- At a score of **19.6**, the pooled false-match rate on real music (absent queries that matched plus present queries matched to the wrong track) is at most 0.1%. The default `MinConfidence` of 0.5 corresponds to a score of 20.7, slightly stricter.

Confidence is calibrated against the benchmark's mix of present and absent queries. If your queries are mostly for audio that is not in the database, raise the threshold; if almost every query is for something in the database, you can lower it.

## Things tried and not adopted

- Hop 128 (denser frames): better recall (76.5%), about 40% slower extraction and 70% more landmarks than the baseline; the combinations above reached higher recall for less.
- Window 512: lost frequency resolution; recall fell to 63.6%.
- Shorter target zone (40 frames): no gain, and it narrows the range of timing the hash can express.
- Judging a match correct only when it is at the right offset. Loop-based music repeats exactly, so the right track can legitimately match at another offset; the benchmark therefore judges identification by track and reports position accuracy separately (98.4% of identified queries also had the position within 0.4 s on real music).

## Re-running

```shell
dotnet run -c Release --project PanoramicData.SoundPrint.Benchmarks -- tune --out temp/tuning.md
dotnet run -c Release --project PanoramicData.SoundPrint.Benchmarks -- tune --combos --out temp/tuning-combos.md
```

Re-encode scenarios need ffmpeg on `PATH` or named by `SOUNDPRINT_FFMPEG`; without it the run stops with a message saying so.

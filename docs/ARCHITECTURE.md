# How PanoramicData.SoundPrint works

This document describes the algorithm and the code that implements it. For measured behaviour see [BENCHMARKS.md](BENCHMARKS.md); for the reasoning behind parameter choices see [TUNING.md](TUNING.md) and [DESIGN.md](DESIGN.md).

## The idea in one paragraph

A recording is reduced to its loudest spectral peaks. Each peak is paired with a few peaks that follow it shortly afterwards, and each pair is hashed from three small numbers: the first peak's frequency, the frequency difference, and the time difference. The hash plus the time of the first peak is a *landmark*. Landmarks survive noise, compression and talk-over because loud peaks mostly survive them, and because a handful of surviving pairs is enough. To identify a clip, its landmarks are looked up in a database; every matching landmark votes for the time offset between the clip and the stored track. A true match piles its votes onto one offset; coincidences scatter. The algorithm follows A. Wang, "An Industrial-Strength Audio Search Algorithm" (2003), written from the paper and first principles.

## Pipeline

```
PCM samples (any rate)
  -> StreamingResampler          fixed internal rate (default 11,025 Hz)
  -> windowed FFT                1024-sample Hann window, 256-sample hop
  -> peak picking                local maxima in a 8-bin x 3-frame neighbourhood
  -> band balancing, density cap  at most 4 peaks per band per ~1 s block, 7 bands
  -> pairing                     each anchor with up to 6 following peaks within 63 frames
  -> 32-bit hash + anchor frame  = Landmark
  -> IFingerprintStore           hash -> (track, frame)
  -> Matcher                     offset histogram vote, Poisson score, confidence
```

### 1. Resampling

`StreamingResampler` converts the caller's sample rate to `FingerprintOptions.SampleRate` with a windowed-sinc filter (kernel half-width of 16 zero crossings of the output rate, scaled when downsampling so the cut-off sits at the output Nyquist). Output sample *n* is taken at input position *n x inRate / outRate*, computed in integer arithmetic, and there is one pre-computed kernel per fractional phase, each normalised to unit gain. The result depends only on the input values and never on how they were split into `Push` calls. If the input rate already equals the internal rate the resampler is skipped.

### 2. Spectrogram

Each frame is a Hann-windowed 1024-sample block (93 ms at 11.025 kHz) taken every 256 samples (23 ms), transformed with an in-place radix-2 FFT. Only the power spectrum (513 bins, 10.8 Hz each) is kept; no logarithm is needed because peak picking compares magnitudes.

### 3. Peak picking

A bin is a peak if its power exceeds `MinPeakPower` and it is the largest in its neighbourhood (8 bins either side, 3 frames either side). Ties go to the earlier position in (frame, bin) order, so a flat region yields one peak, and the rule is the same on every machine. Peaks are then balanced: the spectrum is split into seven roughly octave-wide bands (`BandEdges`), and in each block of 32 frames (about 0.74 s) only the strongest `PeaksPerBandPerBlock` peaks per band survive. This does two jobs: quiet bands are represented (a bass note cannot crowd out the hi-hat), and the number of landmarks per second is bounded, which bounds storage.

Peak picking needs three frames of lookahead, and a density block must complete before its peaks are final, so the stream lags by a block plus the lookahead plus the pairing zone below, about two to three seconds in all. `Flush` releases what lookahead was still holding.

### 4. Pairing and the hash

An anchor peak is paired with the next peaks, in time order, that fall within `MaxDeltaFrames` (63 frames, about 1.5 s) and within the frequency-difference range of -128 to +127 bins, up to `FanOut` partners. Each pair becomes one 32-bit hash:

```
bits 31..23  reserved, zero
bits 22..14  anchor frequency bin (9 bits)
bits 13..6   partner bin minus anchor bin, plus 128 (8 bits)
bits  5..0   frames between the peaks, 1 to 63 (6 bits)
```

The landmark is `(hash, anchor frame)`. A hash says nothing about *when* in the track it occurs; the time comes from the stored frame, which is what makes the offset vote possible. 23 bits are used; selectivity comes mostly from the vote, not from the hash. Every option that changes which hashes are produced feeds `FingerprintOptions.ParameterFingerprint`, a 64-bit FNV hash recorded with a store, so mixing databases and queries from different settings is refused rather than silently wrong.

### 5. Stores

`IFingerprintStore` maps a hash to the places it occurs. `InMemoryFingerprintStore` keeps a dictionary of posting lists behind a reader-writer lock (lookups run concurrently, writes are exclusive). `PostgresFingerprintStore` keeps one row per landmark in `fingerprints (hash int4, track int4, frame int4)` with a covering index `(hash) INCLUDE (track, frame)`, so a lookup is an index-only scan; tracks are written with binary `COPY`, and removing a track cascades. Both return hits as `(track index, track frame, query landmark)` to an `IHitSink`, and resolve the few winning track indexes to caller-owned string ids afterwards.

### 6. Matching

For every hit, `offset = track frame - query frame` is computed and the pair `(track, offset)` receives a vote. Offsets within `OffsetToleranceFrames` of each other are summed, so one frame of jitter does not split a match. Per track, the strongest offset windows are accepted greedily; windows closer than `SuppressionFrames` are the same match, and further apart (a repeated section) are reported separately. This is what lets a clip that straddles two songs report both: each song produces its own peak, with its own span of the query.

Each accepted peak reports:

- **`OffsetInTrack`**: track time minus query time. The audio at query time *q* is at track time *q + OffsetInTrack*. It is negative when the query began before the track did.
- **`QueryStart` / `QueryEnd`**: the first and last query times supported by the votes (the end includes the span of the last landmark pair). `TrackStart` and `TrackEnd` are the same span on the track's clock.
- **`Votes`**: landmarks that agreed.
- **`Score`**: how surprising those votes are. It is the negative base-10 logarithm of the Poisson tail probability of seeing that many votes in one offset window, given the level of coincidental votes the same track received elsewhere (never below `MinBackground`). A score of 20 means roughly one chance in 10^20 under that model; real music is more clustered than the model assumes, which is why the score is calibrated rather than taken at face value.
- **`Confidence`**: the score passed through a logistic curve fitted to benchmark outcomes, so it behaves like the probability that the match is correct (for a benchmark-like mix of present and absent queries).

## Streaming

`Fingerprinter` is a streaming object: push chunks of any size, receive landmarks through an `ILandmarkSink` as they become final. All buffers are allocated in the constructor, so a stream of any length uses constant memory and steady-state pushes allocate nothing (a test enforces it). `RollingLandmarkWindow` is a sink that keeps the last N seconds of landmarks for matching a live stream window by window. The frame counter is an `int`, which lasts about a year and a half at 43 frames per second, and throws on overflow rather than wrapping; start a new fingerprinter for a longer stream.

## Determinism

Extraction is bit-for-bit reproducible: the same samples produce the same landmarks on any machine and any chunking. Sine and cosine for the window, FFT twiddles and resampler kernels come from `DeterministicMath`, which uses only IEEE 754 basic operations, instead of `Math.Sin`, whose last bit can differ between platforms and could flip a peak tie. A golden-hash test pins this with an integer-generated signal.

## Threading

`Fingerprinter` and `RollingLandmarkWindow` (writer side) belong to one stream and one pushing thread. `Matcher`, `InMemoryFingerprintStore` and `PostgresFingerprintStore` are thread-safe and intended to be shared.

## Code map

| Concern | Files (in `PanoramicData.SoundPrint`) |
|---|---|
| Options and versioning | `FingerprintOptions`, `MatchOptions` |
| Extraction | `Fingerprinter`, `StreamingResampler`, `Fft`, `DeterministicMath` |
| Types | `Landmark`, `Match`, `SpectralPeak` |
| Sinks and hooks | `ILandmarkSink`, `ListLandmarkSink`, `RollingLandmarkWindow`, `IPeakObserver` |
| Storage | `IFingerprintStore`, `IHitSink`, `InMemoryFingerprintStore`; PostgreSQL in `PanoramicData.SoundPrint.Postgres` |
| Matching | `IMatcher`, `Matcher`, `OffsetVoting` |

## Known limits

- **Speed changes.** The hash includes the time between peaks and the peak frequencies, so playing a track noticeably faster or slower, or pitch-shifting it, breaks matching. Small drifts (a percent or so) are absorbed by the offset tolerance.
- **Very short or very sparse audio.** Under about three seconds, or with little tonal content, there are too few landmarks to vote reliably (see the accuracy tables).
- **Speech-heavy or percussion-only material** produces fewer stable peaks than tonal music.
- **Covers, remixes and live versions** are different recordings and are not matched to the original.
- **Exact repetition.** Loop-based music can match the right track at several offsets; all are reported.

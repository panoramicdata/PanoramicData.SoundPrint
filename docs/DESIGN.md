# PanoramicData.SoundPrint design note

Status: draft for Checkpoint A. Numbers marked (spike) come from [SPIKE.md](SPIKE.md); numbers marked (estimate) are arithmetic
that phase 6 will replace with measurements.

## 1. Goals and constraints

Identify which known track is playing in a live stream, where in the track it is, and where in the query the match starts and
ends, including windows that straddle two tracks. Own the algorithm: it is written from Wang, "An Industrial-Strength Audio
Search Algorithm" (2003) and first principles, with no code taken from licensed projects.

Targets: at least 50 times real time on one core (spike: about 700 times unoptimised); bounded memory over weeks; thread-safe
stores and matcher; deterministic extraction (same input, same hashes, any machine).

## 2. Input and resampling

Input is mono PCM `float` plus the caller's sample rate. The library resamples to a fixed internal rate with a streaming
polyphase FIR resampler (fixed coefficients computed at construction, so deterministic). Chosen internal rate: **8000 Hz**.
Reasons: radio and codec processing removes or distorts content near the top of the band, music identity lives mostly below 4 kHz,
and the lower rate roughly halves the STFT cost compared with 11.025 kHz. The alternative, 11.025 kHz, is kept as an option
(`FingerprintOptions.SampleRate`) and compared in phase 6 before the default is confirmed.

Determinism: all floating-point work uses `double` or `float` in a fixed operation order with no `Math.FusedMultiplyAdd`, no
parallel reductions and no hardware-dependent intrinsics in the extraction path. Hash equality across machines is a test
(golden hashes for a synthetic signal).

## 3. Extraction parameters (defaults)

| Parameter | Default | Notes |
|---|---|---|
| Internal sample rate | 8000 Hz | see section 2 |
| Window | 1024 samples, Hann (128 ms) | 7.8 Hz bin spacing |
| Hop | 256 samples (32 ms) | 31.25 frames per second |
| Spectrogram | log magnitude, dB | floor at 1e-9 power |
| Peak neighbourhood | +/-12 bins by +/-3 frames | local maximum, ties broken by position |
| Bands for balancing | 7, edges at bins 0, 8, 16, 32, 64, 128, 256, 513 | roughly octaves |
| Density cap | 6 peaks per band per second (42 per second at most) | per fixed one-second block, so streaming |
| Fan-out | 5 targets per anchor | the nearest following peaks in the zone |
| Target zone | 1 to 63 frames ahead (32 ms to 2 s), delta f -128 to +127 bins | |

Everything above lives in a `FingerprintOptions` record with these defaults. Changing any value changes the hashes, so the options
that affect hashing are summarised into a **parameter fingerprint** (a short hash of those values) stored with every track; a
store refuses to mix tracks or queries with different parameter fingerprints.

Peak picking needs a lookahead of 3 frames and the one-second density blocks need the block to complete, so streaming output lags
input by about 1.1 s. This is stated in the streaming API docs.

## 4. Hash layout and width

A landmark is `(hash, anchorFrame)`. The 32-bit hash packs:

```
bits 31..23  reserved (zero in v1; room for a format version or a coarse feature)
bits 22..14  f1: anchor frequency bin, 9 bits (0..511)
bits 13..6   df: f2 minus f1, offset by 128, 8 bits
bits  5..0   dt: frame distance, 6 bits (1..63)
```

23 bits are used, giving 8.4 million possible values. Width trade-off: a wider hash is more selective but more sensitive to bin
jitter; this layout is the paper's approach scaled to our bin counts. Selectivity comes mainly from offset voting, not the hash.
For a 100,000-track database (estimate, section 8) each hash value has on the order of hundreds of postings, which is acceptable
because random collisions land in different (track, offset) bins; the expected random collision count in any single bin is about
2e-4 for a 10 s query. If phase 6 measurements show too much lookup fan-out, the reserved bits become a second quantised feature
(for example the anchor's band) and the format version increments.

Anchor time is stored as an integer frame index (32 ms). Public APIs expose `TimeSpan`; the frame size is
`FingerprintOptions.FrameDuration`.

## 5. Streaming API shape

```csharp
public readonly record struct Landmark(uint Hash, int AnchorFrame);

public interface IFingerprinter
{
    // Feed samples at the caller's rate; landmarks that became final are appended to the sink.
    void Push(ReadOnlySpan<float> samples, ILandmarkSink sink);
    // End of stream: emit whatever the lookahead was still holding.
    void Flush(ILandmarkSink sink);
    // Start a new stream; reuses internal buffers.
    void Reset();
}
```

- Instances are single-stream and not thread-safe; create one per stream. Buffers are fixed-size ring buffers sized from the
  options, so memory is constant however long the stream runs.
- `ILandmarkSink` receives `ReadOnlySpan<Landmark>` batches, so no per-call collection is allocated. A convenience
  `List<Landmark>` sink is provided.
- `AnchorFrame` is counted from the start of the stream (or from `Reset`), as a `long`-safe counter internally and an `int` in
  the landmark; at 31.25 frames per second an `int` lasts over two years, and the API documents the limit and offers
  `ResetFrameOrigin` for continuous operation.
- Wall-clock handling is the consumer's: it records the UTC time at which a stream's frame 0 began; the library never reads the
  clock. Offsets returned are `TimeSpan` from the start of the track or query.

## 6. Matching, partial matches and boundaries

```csharp
public interface IFingerprintStore
{
    void AddTrack(string trackId, IReadOnlyList<Landmark> landmarks);
    bool RemoveTrack(string trackId);
    // Hits for the given hashes: (trackIdx, trackFrame, hash). Implementations decide how.
    void Lookup(ReadOnlySpan<Landmark> query, IHitSink hits);
}

public interface IMatcher
{
    IReadOnlyList<Match> Match(IReadOnlyList<Landmark> query, MatchOptions? options = null);
}

public sealed record Match(
    string TrackId,
    TimeSpan OffsetInTrack,   // where query time zero falls in the track (track time minus query time)
    TimeSpan QueryStart,      // first query time supported by votes for this match
    TimeSpan QueryEnd,        // last query time supported by votes for this match
    double Confidence);       // 0..1, calibrated
```

Algorithm:

1. Look up every query hash. Each hit votes for `(trackIdx, offsetFrame = trackFrame - queryFrame)`.
2. Per track, build the offset histogram. A bin and its immediate neighbours are summed, which tolerates one frame of jitter
   (the spike used single bins and recovered offsets to within 2 frames).
3. **Multiple matches, not one**: for each track, take every local peak in the histogram above a minimum vote count, not just
   the maximum. A window covering the end of song A and the start of song B yields one peak for each, at different tracks and
   different offsets. A track repeating in the window (a loop or chorus) yields two peaks at different offsets.
4. **Span**: for each accepted peak, the supporting hits are those in its bin neighbourhood; `QueryStart` and `QueryEnd` are the
   first and last query frame among them (the end is extended by the dt of the last pair, since a hash vouches for audio up to
   its target peak). The boundary between songs A and B is where A's `QueryEnd` meets B's `QueryStart`. Its precision is bounded by
   landmark density, roughly one to two seconds in sparse passages, and the consumer is expected to refine it (for example against
   transcript timing). The accuracy of boundaries is measured in phase 6 on straddling clips and published.
5. **Score and confidence**: `votes` is the peak bin sum. The background level is estimated from the same track's histogram
   excluding the peak (median and spread) and, for absent tracks, from the global vote distribution. The raw score is a
   normalised z-like value; confidence is that score mapped to 0..1 through a monotone calibration curve fitted in phase 6 so that
   confidence approximates the empirical probability that the match is correct. The recommended threshold is chosen from the
   measured false-positive rate and published.

Streaming use (what WebAudioArchive needs): the consumer keeps a rolling window (for example 10 s, advancing every 2 s) of landmarks
from the live fingerprinter and calls `Match` per window; because results carry spans, overlapping windows can be merged by the
consumer. The matcher is stateless and thread-safe.

Removal: `RemoveTrack` deletes all of a track's landmarks; re-adding under the same id replaces them. Ids are opaque strings.

## 7. Storage schema and index strategy

In-memory store (core): `Dictionary<uint, PostingList>` where a posting is `(trackIdx:int, frame:int)`, guarded by a
reader-writer lock; per-track landmark lists are kept so removal is cheap. Lookups take the read lock; the matcher copies out
postings, so long matches do not block writers.

PostgreSQL store (`PanoramicData.SoundPrint.Postgres`, EF Core):

```sql
tracks(
  idx          integer generated always as identity primary key,
  track_id     text not null unique,
  param_fp     bigint not null,       -- parameter fingerprint, section 3
  landmark_count integer not null,
  created_utc  timestamptz not null)

fingerprints(
  hash   integer not null,            -- the 32-bit hash stored as signed int4
  track  integer not null references tracks(idx) on delete cascade,
  frame  integer not null)

create index ix_fp_hash on fingerprints (hash) include (track, frame);   -- covering: lookups are index-only scans
create index ix_fp_track on fingerprints (track);                        -- fast RemoveTrack
```

Decisions:

- **Integer hash, covering index.** One lookup per query hash is an index-only scan on `ix_fp_hash` and returns `(track, frame)`
  without touching the heap. A hash index was rejected because it cannot be covering and offers no range or ordering benefit
  that the batch lookup needs.
- **Batch lookup** sends the query's distinct hashes as an array (`hash = any(@hashes)`), one round trip per query window, and the
  offset voting is done in the application, where it is cheap (tens of thousands of hits). Voting in SQL was rejected as harder to
  test and not faster for these sizes.
- **Bulk insert** uses binary `COPY` (Npgsql) for fingerprints, with EF Core handling `tracks` and migrations. Indexes are kept in
  place for the common case of adding one track at a time; a documented bulk-load mode drops and recreates them.
- **Partitioning** by hash range is deferred; revisit when measurements at 100,000 tracks show index size or vacuum cost is a
  problem.
- **Parameter fingerprint** is stored per track and checked, so a parameter change cannot silently corrupt matching.
- `EF Core` migrations are shipped in the package; the store exposes `MigrateAsync`.

## 8. Expected size (estimate, spike measured the hash rate)

The spike produced 98.8 landmarks per second (355,651 per hour of audio) with the defaults on synthetic audio.

- Payload per landmark: 12 bytes (hash, track, frame).
- Postgres per landmark: roughly 40 bytes heap tuple (header included) plus about 28 bytes in the covering index and a few bytes in
  `ix_fp_track`, so about 70 bytes: **about 25 MB per hour of audio**.
- In memory (dictionary of postings): roughly 12 to 20 bytes per landmark: about 4 to 7 MB per hour of audio.
- 100,000 tracks of 3.5 minutes is about 5,800 hours: about 145 GB in Postgres, about 25 to 40 GB in memory.

These are large. The levers, to be tuned in phase 6 against accuracy: fan-out (halving it halves storage), the density cap, and
dropping rarely useful hashes at index time. Real music may produce a different hash rate than the synthetic songs. The measured
figures replace this section. **Decision for the owner**: the target database size. A WebAudioArchive-sized catalogue (thousands
of tracks) is trivial either way; 100,000 tracks is where the parameters need tightening.

Match latency for a 10 s query against 100,000 tracks will be measured in phase 6 and stated in the README; the spike measured
0.1 to 0.4 ms against 100 tracks in memory, which does not extrapolate.

## 9. Robustness plan

Degradations the harness will measure (phase 6): lossy re-encode (ffmpeg, test fails with a clear message if missing), additive
noise at several SNRs, EQ and dynamic compression, voice-over at several levels, shifted query starts, and clips straddling two
tracks. The spike showed that white noise at 0 dB with 5 s clips is the weak spot of the current peak picker; planned remedies are
per-band adaptive thresholds relative to a running noise floor, a slightly larger fan-out, and scoring against background.

## 10. Patent check summary

This is a research summary, not legal advice. A patent attorney should confirm before any commercial offering.

What was found (web search of Google Patents listings, no attorney, no USPTO records pulled):

- **US 6,990,453 B2**, "System and methods for recognizing sound and music signals in high noise and distortion", inventors Avery
  Li-Chun Wang and Julius O. Smith III, originally Landmark Digital Services, later Shazam. Filed 20 April 2001, claiming a July 2000
  provisional, granted 24 January 2006. This is the landmark (spectral peak pair, time-frequency hashing) patent that the 2003 paper
  describes. Google Patents lists it as expired. Under the usual 20-year term from the 2001 filing it would have lapsed around
  April 2021, subject to any patent term adjustment.
- Continuations in the same family (US 7,865,368; 8,190,435; 8,386,258; 8,700,407; 9,401,154; 9,899,030) share the same earliest
  filing date, so on the same arithmetic they should have lapsed at the same time. Their individual legal status was not
  verified.
- **US 7,627,477 B2**, "Robust and invariant audio pattern matching", a later Shazam patent covering matching that is invariant to
  time and frequency stretching (speed changes). Google Patents lists it as expired. It is not needed by this design, which makes
  no stretch-invariant claim, but it was checked because it sits next to the same technology.
- Nothing was checked for patents held by other parties (Philips/Haitsma-Kalker, Google, Gracenote, Audible Magic and others hold
  patents on other fingerprinting schemes). This design differs from those (it is spectral peak pairing, not sub-band energy bits or
  chroma sequences), but that is an engineering observation, not a clearance.

Sources:
[US6990453B2 on Google Patents](https://patents.google.com/patent/US6990453B2),
[US7627477 on Google Patents](https://patents.google.com/patent/US7627477),
[Wang 2003 paper listing](https://paperswelove.org/papers/an-industrial-strength-audio-search-algorithm-c0701d84/).

Flags for the owner: (1) the expiry conclusion rests on Google's status label plus term arithmetic, not an official record;
(2) a freedom-to-operate review is advisable before selling a fingerprint database as a service; (3) trademark and copyright
issues (identifying copyrighted music, and the database holding derived data) are separate from patents and not examined here.

## 11. Open decisions

1. Target database scale, which drives how hard to tune storage (section 8).
2. 8 kHz versus 11.025 kHz internal rate: default 8 kHz, to be confirmed by benchmark.
3. Codacy: the repository needs the `CODACY_PROJECT_TOKEN` secret before coverage can reach Codacy (see the Phase 1 report).

## 12. As built: deviations from this note

The design above was approved at Checkpoint A. Implementation changed these details; the code and README are authoritative.

- **Streaming lag** is up to about three seconds, not 1.1 s: a peak needs three frames of lookahead, a one-second density block must complete,
  and an anchor waits until its whole two-second target zone is known.
- **Frame origin.** There is no `ResetFrameOrigin`. A stream's frames are numbered from zero as an `int`, which lasts about two years at
  31.25 frames per second; the extractor throws on overflow and `Reset()` restarts numbering. A consumer running longer starts a new
  fingerprinter (and a new `RollingLandmarkWindow`) at a convenient quiet moment.
- **Store interface is asynchronous** (`ValueTask`, with a `CancellationToken` on every call) so that a database store is not blocked on
  I/O. Hits are delivered to an `IHitSink` as (track index, track frame, query landmark); track indexes are resolved to ids in one
  extra call for the few tracks that matched. `InMemoryFingerprintStore` also offers synchronous methods.
- **Parameter fingerprint is per store, not per track.** A store records the fingerprint of the options it was built with
  (`store_info` table in PostgreSQL), and a matcher refuses a store whose fingerprint differs from its own options.
- **Match** carries `Votes` and `Score` as well as `Confidence`, and has `TrackStart` / `TrackEnd` helpers.
  `OffsetInTrack` is track time minus query time and is negative when the query began before the track did.
- **Scoring.** The score is the negative base-10 logarithm of a Poisson tail probability: the votes in the winning offset window
  against the level expected from the other hits on the same track. Confidence is a logistic function of that score whose two
  parameters are fitted by the benchmark harness (see the README for the fitted values and the recommended threshold).
- **Default minimum confidence is 0.5**, which drops the weak secondary peaks that repetitive music produces; matches the caller
  wants to see below that can be requested with `MatchOptions.MinConfidence`.
- **Rolling window.** `RollingLandmarkWindow` is a ready-made sink that keeps the most recent N seconds of a live stream for matching.
- **Deterministic maths.** Window, FFT and resampler tables are built with `DeterministicMath` (sine and cosine from IEEE basic
  operations) instead of `Math.Sin`, so extraction is bit-identical across platforms; a golden-hash test pins this.

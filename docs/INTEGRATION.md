# Integrating with a continuous recording system

This guide is for an application that records a radio stream around the clock, labels songs some other way (for example an LLM reading transcripts of the announcer), and wants audio fingerprinting to identify songs and find their boundaries, including before the announcer names them. The library knows nothing about songs, artists, transcripts or radio; your application owns that mapping through the opaque track id.

## Division of labour

| Your application | This library |
|---|---|
| Records and archives the stream, decodes a scratch copy to mono PCM | Takes decoded mono `float` samples and a sample rate |
| Decides what a song is and what to call it (track id, metadata) | Fingerprints, stores, matches, and reports ids it was given |
| Labels spans of the recording (for example from transcripts) | Turns a labelled span into a fingerprint, and a later clip into a match |
| Keeps wall-clock time | Reports times relative to a stream's start |
| Mistake handling policy | Supports removing and replacing a track's fingerprints |

## Learning a song from a labelled span

1. Your labeller says that recording time *t0* to *t1* is "song X".
2. Decode that span (or the whole song if you have it) from the archive to a scratch buffer.
3. Fingerprint it and store it under your own id:

```csharp
var landmarks = Fingerprinter.Fingerprint(spanSamples, sampleRate, options);
await store.AddTrackAsync(songId, landmarks, ct);
```

Use one id per distinct recording (not per airing). If the same song airs again, you do not need to add it again; the next airing will match.

**Quality tips:** fingerprint the cleanest airing you have; a span that includes the announcer talking over the intro still works because loud peaks survive talk-over (voice at the same level as the music is the hardest case measured), but a clean intro and outro give the best coverage. A partial span (a song joined or left mid-way) fingerprints only what it contains, so later airings match the covered part.

## Identifying and finding boundaries on the live stream

```csharp
var fingerprinter = new Fingerprinter(streamRate, options);
var window = new RollingLandmarkWindow(TimeSpan.FromSeconds(12), options);
// one reading loop: Push audio, then every few seconds:
var matches = await matcher.MatchAsync(window.Snapshot(), null, ct);
```

Each `Match` carries:

- `TrackId`: your id.
- `QueryStart` and `QueryEnd`: where, on the stream clock, this song was heard. Add the UTC time at which the fingerprinter's frame 0 began (you record that when you start it) to get wall-clock times.
- `TrackStart` and `TrackEnd`: where in the song that span is, so you can tell an intro from a chorus, and "song restarted" from "song continued".
- `Confidence`, `Votes`, `Score`.

**Boundaries.** A window that straddles two songs returns both. The boundary between songs is near the first song's `QueryEnd` and the second's `QueryStart`; their midpoint was within about 0.1 to 0.2 s of the true boundary in the benchmarks with a hard cut, and a few tenths of a second with a one-second crossfade. Overlapping windows (for example every 3 s, 12 s long) let you track a boundary as it moves through the window and settle on a stable estimate. If you also have transcript timing, use fingerprints to place the boundary and the transcript to name the song.

**Unknown songs.** No match is the answer when the song is not in the database. A stream of "no match" for a stretch of several windows between two matched songs is a good candidate to send to your labeller for a new track.

## Handling mistakes

If a label turns out to be wrong:

```csharp
await store.RemoveTrackAsync(wrongId, ct);                 // removes every landmark of that id
await store.AddTrackAsync(rightId, landmarks, ct);         // or re-add under a corrected id
```

Because ids are yours, a correction can also be pure metadata on your side (the same fingerprints under a renamed song). Matches already made are not changed; re-run matching over the affected stretch if you need to relabel history.

## Capacity planning

- Real music produced about 100 to 125 landmarks per second of audio at the defaults; a 24-hour station adds roughly 9 to 11 million landmarks a day if every second were added, but you only add distinct songs. A typical station's rotation is a few thousand songs, which is a database of tens of millions of landmarks: comfortable in memory (about 16 bytes per landmark) or in PostgreSQL.
- See [BENCHMARKS.md](BENCHMARKS.md) for measured match latency against very large databases and for storage per hour of audio.

## Versioning

Record `FingerprintOptions.ParameterFingerprint` alongside any stored fingerprints. A store refuses landmarks from different parameters, so a library upgrade that changes the defaults will tell you rather than silently matching worse; if you may retune, keep the ability to regenerate fingerprints from the archived audio.

## Concurrency

One `Fingerprinter` and `RollingLandmarkWindow` per stream, pushed from one thread. One shared `Matcher` and store for the whole application; they are thread-safe.

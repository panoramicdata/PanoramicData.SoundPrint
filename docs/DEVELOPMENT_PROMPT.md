# Development prompt for PanoramicData.SoundPrint

Give this whole file to a Claude Code session opened in this repository. It is written to be self-contained.

---

You are building `PanoramicData.SoundPrint`, a managed .NET audio fingerprinting library, in this repository (panoramicdata/PanoramicData.SoundPrint). Tracking issue: #1. Read it first with `gh issue view 1`.

## Why this exists

The first consumer is WebAudioArchive (github.com/davidnmbond/WebAudioArchive, issue #2): it records a web radio stream around the clock, and needs to identify which song is playing, with song boundaries, even before the announcer names it. Labels come from an LLM reading transcripts; this library turns labelled audio spans into a fingerprint database and matches new audio against it. The long-term aim is a proprietary fingerprint database offered as a commercial service, which is why we are not using SoundFingerprinting (restrictive licence). Own the algorithm and the code.

## Hard rules

- Public repository, MIT licence. Never commit recordings, broadcast audio, copyrighted music, credentials, tokens or real fingerprint data. Test audio is synthetic. Any downloaded music (see Benchmarks) lives in an untracked, gitignored folder; only metadata and result numbers are committed.
- Write the algorithm from the published literature (Wang, "An Industrial-Strength Audio Search Algorithm", 2003; Dejavu-style descriptions) and first principles. Do not copy or port code from SoundFingerprinting, Chromaprint or other licensed projects. Reading them as a reference is fine.
- Do not edit anything outside this repository. Do not push to other repositories.
- Tests first (xUnit v3, AwesomeAssertions, Microsoft.Testing.Platform). Never skip or weaken a test to make it pass; a test that cannot run must fail with a message saying what is missing. Run commands and read the output before claiming anything works.
- .NET 10, nullable enabled, warnings as errors, central package management, NuGet versions pinned exactly.
- No em dashes anywhere (code, docs, commits, chat). Use commas, colons, semicolons or new sentences.
- The API is explicit about units. Where wall-clock times appear they are UTC.
- Commit after each task with a clear message. Commit to `main` directly; no pull requests.
- When you need the owner's answer, ask with the structured question tool, not prose.
- Use a scratch folder outside the repo (or a gitignored `/temp/`) for experiments.

## Repository standards

Bring the repository up to the standards enforced by panoramicdata/PanoramicData.NugetManagement. Read `NEW_NUGETS.md` in that repository (clone it to a scratch folder, read only, do not edit it) and copy the pattern from its paragon, panoramicdata/Splunk.Api: `.editorconfig`, `global.json` (with the Microsoft.Testing.Platform runner), `version.json` (NBGV), `Directory.Build.props`, `Directory.Packages.props`, CI and CodeQL workflows, Dependabot, `Publish.ps1`, README badges, `SECURITY.md`, `CONTRIBUTING.md`, snupkg symbols, coverage tooling, `PackageReadmeFile`, `PackageIcon`. The `new-nuget-package` skill there is for vendor API clients and does not apply; bootstrap by hand. Finish only when the NugetManagement governance assessment passes every rule for this repository.

## Solution layout

- `PanoramicData.SoundPrint`: core library, no database dependency. Fingerprint extraction, matching, store abstractions, in-memory store.
- `PanoramicData.SoundPrint.Postgres`: PostgreSQL store via EF Core, with migrations.
- `PanoramicData.SoundPrint.Test`: unit tests. A separate integration test project for PostgreSQL using a container, which fails with a clear message if Docker is unavailable.
- `PanoramicData.SoundPrint.Benchmarks`: benchmark harness (BenchmarkDotNet for speed, a custom runner for accuracy).

## Algorithm: landmark hashing

1. Input is mono PCM `float` samples plus sample rate, supplied by the caller. The library does not decode audio. Provide a resampler to a fixed internal rate (start with 8 kHz or 11.025 kHz; justify the choice with benchmarks).
2. STFT with a configurable window and hop. Build a log-magnitude spectrogram.
3. Pick spectral peaks that are local maxima in a time-frequency neighbourhood, with band balancing so quiet bands are represented, and a density cap per second.
4. Form landmark pairs: each anchor peak is paired with peaks in a forward target zone. Hash (f1, f2, delta-t), quantised, into a compact integer. Keep the anchor time t1.
5. Store: hash to (trackId, t1). Index strategy for PostgreSQL decided in the design note.
6. Match: look up the query's hashes, group hits by (trackId, offset = t_db minus t_query), and vote with a histogram. Score is the peak bin height, normalised against the query's hash count and against the background. Return track id, offset, matched span (start and end within the query), and a calibrated confidence.
7. Support partial and moving matches: a query window that covers the end of one song and the start of the next must report both, each with its span. WebAudioArchive uses this to find song boundaries.

Everything tunable goes in an options record with defaults. Keep the extraction deterministic: same input, same hashes, on any machine.

## Performance bar

The consumer fingerprints a live 24/7 stream. Required:

- Fingerprinting at least 50 times faster than real time on one core, and typically a small fraction of one core for a live stream.
- Bounded memory over weeks of continuous operation: streaming API (feed chunks, receive hashes as they are produced), no unbounded buffers, no per-call allocation storms. Benchmark allocations.
- Match latency for a 10 second query against a database of 100,000 tracks stated in the README, with the database size per hour of audio.
- Thread safe stores and matcher.

## Accuracy bar

Measure and publish in the README: top-1 accuracy, false positive rate and match latency by query length (target: reliable at 5 to 10 seconds), and the confidence threshold you recommend. Degradations to test: re-encode at lower bitrate (use ffmpeg locally if present, and make those tests fail with a clear message if it is missing), additive noise at several SNRs, EQ and dynamic compression typical of radio processing, a voice-over mixed in at several levels, shifted query start times, and clips that straddle two tracks.

## Benchmarks and test audio

- Unit and CI tests use only synthetic audio produced by an in-repo generator (note sequences, chirps, noise, simple synthesised "songs" with distinct melodies and rhythms).
- For realism, a local-only benchmark may use a small set of Creative Commons licensed music (for example from the Free Music Archive or Jamendo). Download into a gitignored folder, record only track metadata and licence in a committed `benchmarks/SOURCES.md`, and commit result numbers, never audio. The benchmark must skip with a clear message, not silently pass, when the folder is absent.
- Optionally read a local path from an environment variable `SOUNDPRINT_LOCAL_AUDIO` for the owner's own files. Never log or commit anything derived from it beyond aggregate numbers.

## Phases and checkpoints

Work unattended within a phase. Stop and wait for the owner at each checkpoint, summarising what was done, the numbers, and any decision you need.

1. **Bootstrap.** Repository standards, empty solution that builds green in CI.
2. **Design note.** `docs/DESIGN.md`: parameters, hash layout and width, target zone, quantisation, storage schema, index strategy, expected size per hour, streaming API shape, and how partial matches and boundaries are reported. Include a patent check summary for the landmark approach (note what you found and sources; flag it, do not present it as legal advice).
3. **Spike.** Minimal fingerprint-and-match round trip on synthetic audio, with measured accuracy and speed. Throwaway code is labelled as such.

**CHECKPOINT A: stop here for approval of the design note and spike results.**

4. **Core library.** Test-first implementation, streaming extraction, matcher, in-memory store, 100% line and branch coverage where practical and justified exclusions otherwise.
5. **Postgres store.** EF Core, migrations, bulk insert, efficient hash lookup, integration tests against a container.
6. **Degradation harness and benchmarks.** Accuracy and performance results into the README. Tune parameters against the results; record each tuning decision and its effect.
7. **Docs and quality.** README with quick start, streaming example and results; XML docs on all public members; Codacy grade A; governance assessment passes all rules.

**CHECKPOINT B: stop before the first NuGet publish for approval.**

8. **Release.** Publish `PanoramicData.SoundPrint` and `PanoramicData.SoundPrint.Postgres` through the repository's publish process, verify they appear on NuGet, then update issue #1 with results and close it. Comment on davidnmbond/WebAudioArchive#2 with the released version and the API summary the consumer needs.

## Public API sketch (refine in the design note)

```csharp
IFingerprinter            // streaming: Push(ReadOnlySpan<float> samples) -> hashes; Flush()
IFingerprintStore         // AddTrack(trackId, hashes), RemoveTrack(trackId), Query(hashes) -> hits
IMatcher                  // Match(hashes, options) -> IReadOnlyList<Match>
record Match(string TrackId, TimeSpan OffsetInTrack, TimeSpan QueryStart, TimeSpan QueryEnd, double Confidence)
```

Track ids are opaque strings owned by the caller. The library knows nothing about songs, artists, transcripts or radio; the consumer maps ids to those. Removing a track's fingerprints must be supported, because the consumer retracts mistaken labels.

## Reporting

At each checkpoint and at the end, report faithfully: what passed, what failed, what was skipped and why, with command output. Do not claim success without running the verification commands.

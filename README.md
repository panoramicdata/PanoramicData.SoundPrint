# PanoramicData.SoundPrint

[![NuGet](https://img.shields.io/nuget/v/PanoramicData.SoundPrint.svg)](https://www.nuget.org/packages/PanoramicData.SoundPrint)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![CI](https://github.com/panoramicdata/PanoramicData.SoundPrint/actions/workflows/ci.yml/badge.svg)](https://github.com/panoramicdata/PanoramicData.SoundPrint/actions/workflows/ci.yml)
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/8672c1a1685745cc9b9b3986b2b49109)](https://app.codacy.com/gh/panoramicdata/PanoramicData.SoundPrint/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)
[![Codacy Coverage](https://app.codacy.com/project/badge/Coverage/8672c1a1685745cc9b9b3986b2b49109)](https://app.codacy.com/gh/panoramicdata/PanoramicData.SoundPrint/dashboard)

Landmark-based audio fingerprinting for .NET: build, store and query fingerprint databases from broadcast audio.

> **Status: under construction.** The repository is bootstrapped (build, CI, packaging and governance), but the
> fingerprinting library itself is not written yet. Nothing here is published to NuGet. Progress is tracked in
> [issue #1](https://github.com/panoramicdata/PanoramicData.SoundPrint/issues/1).

## Installation

Not yet published. Once released:

```shell
dotnet add package PanoramicData.SoundPrint
dotnet add package PanoramicData.SoundPrint.Postgres
```

## Packages

| Package | Purpose |
|---|---|
| `PanoramicData.SoundPrint` | Core library: fingerprint extraction, matching, store abstractions and an in-memory store. No database dependency. |
| `PanoramicData.SoundPrint.Postgres` | PostgreSQL fingerprint store built on EF Core. |

## Planned approach

Spectral peaks are paired into landmark hashes of (f1, f2, delta-t) and stored against a track id and anchor time.
A query is matched by looking up its hashes and voting on the time offset between query and database, which gives
the track, the position within it, and the span of the query that matched. The algorithm is written from the
published literature (Wang, "An Industrial-Strength Audio Search Algorithm", 2003) and first principles.

The library takes decoded mono PCM supplied by the caller; it does not decode audio.

## Quick start

To follow once the API exists.

## Test audio

Unit and CI tests use only synthetic audio generated in the repository. No recordings, broadcast audio, copyrighted
music or real fingerprint data are committed.

## Quality

Builds treat warnings as errors, unit tests run on xUnit v3 with skips treated as failures, and coverage is collected
with Microsoft.Testing.Extensions.CodeCoverage. Coverage and accuracy figures will be published here as they are measured.

## Links

- NuGet: https://www.nuget.org/packages/PanoramicData.SoundPrint
- Source: https://github.com/panoramicdata/PanoramicData.SoundPrint
- Issues: https://github.com/panoramicdata/PanoramicData.SoundPrint/issues

## License

MIT, see [LICENSE](LICENSE).

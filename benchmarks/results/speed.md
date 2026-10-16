# Extraction speed and match latency (BenchmarkDotNet)

Generated 2026-10-16 with `dotnet run -c Release --project PanoramicData.SoundPrint.Benchmarks -- speed --filter "*" --job short`. Intel Core i9-11900K, .NET 10, Windows 11, one thread per benchmark. The short job (few iterations) is used, so error bars are wide; the conclusions do not depend on the last digit.

## Extraction: 60 seconds of audio per operation

| Method | Mean | Real-time factor | Allocated |
|---|---|---|---|
| Stream 60 s of 8 kHz audio through a warmed streaming fingerprinter (resampled to 11.025 kHz) | 141.3 ms | about 425x | none |
| Stream 60 s of 44.1 kHz audio through a warmed streaming fingerprinter | 297.3 ms | about 200x | none |
| One-shot `Fingerprinter.Fingerprint` of 60 s at 8 kHz, including construction and the result list | 141.6 ms | about 425x | 411 KB (the landmark list) |

The requirement was at least 50 times real time on one core. A live stream therefore needs well under 1% of one core, and steady-state streaming allocates nothing, so memory stays flat over weeks.

## Matching: one 10 second query, in-memory store, synthetic 40 s tracks

| Tracks in the database | Mean | Allocated per query |
|---|---|---|
| 100 | 75.7 us | 17 KB |
| 1,000 | 1.69 ms | 133 KB |
| 5,000 | 5.59 ms | 518 KB |

Latency grows with database size because every query landmark returns every stored landmark with the same hash; see `latency-inmemory.md` and `latency-postgres.md` for 100,000 and 20,000 track databases.

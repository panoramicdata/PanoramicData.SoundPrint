# Latency and size: 100000 tracks of 40 s (1111 hours of audio)

Built in 46.6 min; 485,561,015 landmarks; 437005 landmarks per hour of audio (121.4 per second).
In-memory store: 7.70 GB of managed heap, 6.9 MB per hour of audio, 15.9 bytes per landmark.

Match latency for a 10 s query against 100,000 tracks (in-memory store, 300 queries, after fingerprinting):
- median 213.6 ms, 95th percentile 357.5 ms, maximum 558.3 ms
- hits returned per query: median 1,573,512, maximum 2,942,374
- top-1 accuracy on clean clips: 100.0%

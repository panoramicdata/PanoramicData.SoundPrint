# Latency and size: 20000 tracks of 40 s (222 hours of audio)

Built in 23.9 min; 97,110,744 landmarks; 436998 landmarks per hour of audio (121.4 per second).
PostgreSQL: table 4.30 GB + indexes 4.65 GB = 8.95 GB; 40.3 MB per hour of audio, 92.1 bytes per landmark.

Match latency for a 10 s query against 20,000 tracks (PostgreSQL store, 200 queries, after fingerprinting):
- median 312.2 ms, 95th percentile 613.1 ms, maximum 1620.5 ms
- hits returned per query: median 312,519, maximum 559,664
- top-1 accuracy on clean clips: 100.0%

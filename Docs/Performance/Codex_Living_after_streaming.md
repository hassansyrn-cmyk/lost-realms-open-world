# Living Verdant after_streaming — 10/30/2026 2:04:23 AM
Desktop Editor, High target 60; not Android.

| Scene | seconds | frames | mean | p50 | p95 | p99 | max | >50ms | used start/end MiB | peak | reserved | Mono | main p95 | alloc p95 KiB | gc0 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---|---:|---:|---:|
| Hunt route | 35.0 | 2025 | 17.29 | 15.42 | 31.91 | 34.08 | 189.10 | 3 | 3001.9 → 3023.7 | 3023.7 | 5547.2 | 101.2 → 104.7 | 33.70 | 47.1 | 4 |

Travelled 166.0 m.

| Work (nested scopes; do not sum) | Calls | Total ms | Max ms | Items |
|---|---:|---:|---:|---:|
| RealmTick | 2373 | 154.43 | 8.66 | 0 |
| RealmFill | 270 | 185.53 | 6.64 | 1272 |
| PopulationTick | 117 | 107.20 | 12.90 | 0 |
| PopulationSpawn | 94 | 92.95 | 4.71 | 94 |

Slowest measured work (includes startup):
- PopulationTick 12.90 ms, 0 items, realtime 13.61.
- PopulationTick 9.89 ms, 0 items, realtime 26.50.
- RealmTick 8.66 ms, 0 items, realtime 20.43.
- PopulationTick 6.99 ms, 0 items, realtime 19.63.
- RealmFill 6.64 ms, 12 items, realtime 13.51.
- RealmFill 5.86 ms, 1 items, realtime 20.43.
- PopulationSpawn 4.71 ms, 1 items, realtime 19.63.
- PopulationTick 4.66 ms, 0 items, realtime 26.80.
- RealmFill 4.61 ms, 6 items, realtime 13.49.
- RealmTick 4.33 ms, 0 items, realtime 20.23.
- RealmTick 4.18 ms, 0 items, realtime 20.35.
- RealmTick 3.99 ms, 0 items, realtime 20.06.

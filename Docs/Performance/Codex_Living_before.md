# Living Verdant before — 10/30/2026 1:58:37 AM
Desktop Editor, High target 60; not Android.

| Scene | seconds | frames | mean | p50 | p95 | p99 | max | >50ms | used start/end MiB | peak | reserved | Mono | main p95 | alloc p95 KiB | gc0 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---|---:|---:|---:|
| Hunt route | 35.0 | 1982 | 17.66 | 15.63 | 32.03 | 34.33 | 608.77 | 5 | 2992.5 → 3009.6 | 3011.2 | 5547.2 | 103.1 → 104.7 | 34.22 | 46.5 | 4 |

Travelled 164.2 m.

| Work (nested scopes; do not sum) | Calls | Total ms | Max ms | Items |
|---|---:|---:|---:|---:|
| RealmTick | 2263 | 158.12 | 25.55 | 0 |
| RealmFill | 239 | 187.19 | 6.56 | 1272 |
| PopulationTick | 57 | 107.65 | 40.57 | 0 |
| PopulationSpawn | 56 | 94.77 | 17.16 | 95 |

Slowest measured work (includes startup):
- PopulationTick 40.57 ms, 0 items, realtime 20.44.
- PopulationTick 28.30 ms, 0 items, realtime 25.80.
- RealmTick 25.55 ms, 0 items, realtime 20.31.
- RealmTick 21.18 ms, 0 items, realtime 12.47.
- RealmTick 19.88 ms, 0 items, realtime 20.23.
- RealmTick 17.33 ms, 0 items, realtime 20.19.
- PopulationSpawn 17.16 ms, 4 items, realtime 20.44.
- RealmTick 15.20 ms, 0 items, realtime 20.35.
- RealmTick 11.96 ms, 0 items, realtime 20.14.
- PopulationTick 11.96 ms, 0 items, realtime 12.56.
- RealmTick 11.92 ms, 0 items, realtime 16.34.
- RealmTick 11.19 ms, 0 items, realtime 20.00.

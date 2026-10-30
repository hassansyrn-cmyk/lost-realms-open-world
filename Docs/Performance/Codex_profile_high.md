# Desktop Editor performance — high — 10/29/2026 11:20:13 PM

Unity 6000.6.0f1; Intel(R) Core(TM) i7-9850H CPU @ 2.60GHz; NVIDIA GeForce GTX 1650 with Max-Q Design; 1333×619. Play mode; frame cap retained. Includes Editor overhead. No Android build, device heat or GPU temperature measurement. Main-thread counter may include waits; NaN means unavailable. Allocations include Editor activity.

| Scenario | Seconds | Frames | Mean ms | P50 | P95 | P99 | Worst | >50 ms | Unity used MiB start→end | Peak used | Peak reserved | Mono MiB start→end | Main thread P95 ms | GC alloc P95 KiB/frame | Gen0 collections |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---|---:|---:|---:|
| Oakvale settled | 30.0 | 1744 | 17.21 | 15.25 | 31.44 | 34.10 | 51.17 | 1 | 2501.7 → 2501.7 | 2503.3 | 5539.2 | 100.9 → 102.1 | 32.11 | 46.0 | 3 |
| Hunt-route traversal | 30.0 | 1622 | 18.50 | 16.97 | 28.16 | 36.14 | 292.13 | 4 | 2503.6 → 2521.2 | 2521.9 | 5539.2 | 105.2 → 107.2 | 28.82 | 41.8 | 2 |
| Horse herd nearby | 30.0 | 1201 | 24.98 | 23.85 | 36.61 | 50.36 | 109.65 | 13 | 2521.9 → 2522.6 | 2523.0 | 5539.2 | 103.4 → 104.6 | 37.29 | 33.1 | 1 |


- Oakvale settled: enemies=15, wildlife=54, NPCs=11, travelled=0.0 m, end=(44.00, 5.96, 42.00).
- Hunt-route traversal: enemies=15, wildlife=54, NPCs=11, travelled=145.7 m, end=(79.73, 5.35, 38.80).
- Horse herd nearby: enemies=15, wildlife=54, NPCs=11, travelled=0.0 m, end=(46.04, 5.16, 73.30).

Sampling clarification: the original Horse herd nearby row selected the village Horse group of ONE animal at (46,63), with other wildlife streamed nearby. It is a village-horse scene, not a four-horse herd test. See the separate high_herd report for the explicit Wild Horse follow-up.

Peak columns in this initial report are once-per-second samples; the final end reading can be marginally higher than that sampled peak. Follow-up probe includes the end reading in its peak summary. Unity used/Mono values describe Editor allocations, not phone residency.

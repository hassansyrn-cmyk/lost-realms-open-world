# Desktop Editor performance — balanced — 10/29/2026 11:17:52 PM

Unity 6000.6.0f1; Intel(R) Core(TM) i7-9850H CPU @ 2.60GHz; NVIDIA GeForce GTX 1650 with Max-Q Design; 1333×619. Play mode; frame cap retained. Includes Editor overhead. No Android build, device heat or GPU temperature measurement. Main-thread counter may include waits; NaN means unavailable. Allocations include Editor activity.

| Scenario | Seconds | Frames | Mean ms | P50 | P95 | P99 | Worst | >50 ms | Unity used MiB start→end | Peak used | Peak reserved | Mono MiB start→end | Main thread P95 ms | GC alloc P95 KiB/frame | Gen0 collections |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---|---:|---:|---:|
| Oakvale settled | 30.0 | 900 | 33.34 | 33.34 | 35.03 | 37.49 | 60.41 | 1 | 2480.9 → 2490.2 | 2490.0 | 5539.2 | 103.3 → 112.4 | 34.77 | 150.0 | 8 |
| Hunt-route traversal | 30.0 | 895 | 33.52 | 33.35 | 36.10 | 41.24 | 219.05 | 1 | 2490.4 → 2505.2 | 2505.1 | 5539.2 | 112.7 → 108.4 | 36.09 | 110.7 | 7 |
| Horse herd nearby | 30.0 | 901 | 33.33 | 33.32 | 35.30 | 37.11 | 46.59 | 0 | 2506.8 → 2507.6 | 2507.7 | 5539.2 | 104.0 → 110.6 | 35.19 | 131.3 | 7 |


- Oakvale settled: enemies=15, wildlife=54, NPCs=11, travelled=0.0 m, end=(44.00, 5.96, 42.00).
- Hunt-route traversal: enemies=15, wildlife=51, NPCs=11, travelled=146.4 m, end=(80.72, 5.35, 39.06).
- Horse herd nearby: enemies=15, wildlife=54, NPCs=11, travelled=0.0 m, end=(46.04, 5.16, 73.30).

Sampling clarification: the original Horse herd nearby row selected the village Horse group of ONE animal at (46,63), with other wildlife streamed nearby. It is a village-horse scene, not a four-horse herd test. See the separate balanced_herd report for the explicit Wild Horse follow-up.

Peak columns in this initial report are once-per-second samples; the final end reading can be marginally higher than that sampled peak. Follow-up probe includes the end reading in its peak summary. Unity used/Mono values describe Editor allocations, not phone residency.

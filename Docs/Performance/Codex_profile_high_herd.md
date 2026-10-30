# Desktop Editor performance — high_herd — 10/29/2026 11:29:02 PM

Unity 6000.6.0f1; Intel(R) Core(TM) i7-9850H CPU @ 2.60GHz; NVIDIA GeForce GTX 1650 with Max-Q Design; 1333×619. Play mode; frame cap retained. Includes Editor overhead. No Android build, device heat or GPU temperature measurement. Main-thread counter may include waits; NaN means unavailable. Allocations include Editor activity.

| Scenario | Seconds | Frames | Mean ms | P50 | P95 | P99 | Worst | >50 ms | Unity used MiB start→end | Peak used | Peak reserved | Mono MiB start→end | Main thread P95 ms | GC alloc P95 KiB/frame | Gen0 collections |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---|---:|---:|---:|
| Wild horse herd nearby | 30.0 | 1573 | 19.08 | 18.92 | 20.77 | 24.40 | 31.89 | 0 | 2530.0 → 2531.7 | 2531.8 | 5539.2 | 98.5 → 107.2 | 21.64 | 33.3 | 1 |


- Wild horse herd nearby: enemies=15, wildlife=50, NPCs=10, travelled=0.0 m, end=(-90.13, 6.51, 30.86).

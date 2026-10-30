# Desktop Editor performance — balanced_herd — 10/29/2026 11:27:54 PM

Unity 6000.6.0f1; Intel(R) Core(TM) i7-9850H CPU @ 2.60GHz; NVIDIA GeForce GTX 1650 with Max-Q Design; 1333×619. Play mode; frame cap retained. Includes Editor overhead. No Android build, device heat or GPU temperature measurement. Main-thread counter may include waits; NaN means unavailable. Allocations include Editor activity.

| Scenario | Seconds | Frames | Mean ms | P50 | P95 | P99 | Worst | >50 ms | Unity used MiB start→end | Peak used | Peak reserved | Mono MiB start→end | Main thread P95 ms | GC alloc P95 KiB/frame | Gen0 collections |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---|---:|---:|---:|
| Wild horse herd nearby | 30.0 | 900 | 33.34 | 33.36 | 36.02 | 38.68 | 46.05 | 0 | 2514.2 → 2516.3 | 2516.9 | 5539.2 | 102.7 → 104.7 | 35.88 | 105.1 | 7 |


- Wild horse herd nearby: enemies=15, wildlife=50, NPCs=10, travelled=0.0 m, end=(-90.13, 6.51, 30.86).

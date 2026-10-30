# Codex performance handoff — measured 29 October 2026

Desktop Unity 6000.6.0f1 Editor, Intel i7-9850H, GTX 1650 Max-Q, 1333 × 619 game view. Each row is a 30-second window after five seconds of settling. The frame cap stayed enabled. These are observations of the current desktop build in the Editor, including Editor overhead, not Android results or a controlled before/after improvement claim.

| Scene | Tier / target | Mean ms | Approx. FPS | P95 ms | Worst frame ms |
|---|---|---:|---:|---:|---:|
| Oakvale | Balanced / 30 | 33.34 | 30.0 | 35.03 | 60.41 |
| Hunting route, touch movement and attacks | Balanced / 30 | 33.52 | 29.8 | 36.10 | 219.05 |
| Four wild horses nearby | Balanced / 30 | 33.34 | 30.0 | 36.02 | 46.05 |
| Oakvale | High / 60 | 17.21 | 58.1 | 31.44 | 51.17 |
| Hunting route, touch movement and attacks | High / 60 | 18.50 | 54.1 | 28.16 | 292.13 |
| Four wild horses nearby | High / 60 | 19.08 | 52.4 | 20.77 | 31.89 |

The route tour actually travelled 146.4 m on Balanced / 145.7 m on High through the real movement controller; it did not teleport between frame samples. The four-horse follow-up visited the Wild Horse group at (-90,41), standing about ten metres out, with 50 wildlife and 15 live enemies streamed in. No frame exceeded 50 ms in either wild-herd window.

The first Horse-labelled sample selected a single village horse at (46,63), with 54 wildlife nearby. That scene averaged 33.33 ms Balanced / 24.98 ms High, with High P95 36.61 ms and worst 109.65 ms. It is a separate village view, not the four-horse result. The raw reports retain an explicit correction.

Unity used allocation samples were approximately **2481–2532 MiB (2.42–2.47 GiB)**. Reserved allocator memory was about **5539 MiB (5.41 GiB)**; reserved is not the same as active working-set use. Mono used roughly 99–112 MiB. Recorded allocation P95 was 33–150 KiB/frame and 1–8 Gen0 collections per 30-second window. All these readings include Editor activity; the sample does not establish a phone memory budget or prove a leak. Native peaks were sampled once per second; the initial reports document that their final end reading can slightly exceed the one-second sampled peak. Follow-up probe includes its end reading in peak accounting.

## Heat and GPU snapshots

NVIDIA's local driver utility supplied two actual snapshots:

| Timestamp Asia/Dubai | Editor context | GPU temperature | GPU busy | Driver GPU memory | GPU power |
|---|---|---:|---:|---:|---:|
| 29 Oct 23:30:45.205 | Play-mode final-check window | 68 °C | 96% | 3111 MiB | 34.14 W |
| 30 Oct 00:41:36.247 | Immediately after GLM's Play-mode QA tour ended | 64 °C | 0% | 1319 MiB | 4.72 W |

These are whole-desktop GPU readings, including other applications. They are not CPU temperatures, isolated Unity GPU memory, a controlled cooldown test, a sustained thermal/throttling pass, or Android battery/skin temperatures. No Android build or phone heat test was run, per the user's postponement.

## Next optimization targets for GLM

1. Attribute the **219–292 ms hunting-route stalls** with a CPU timeline capture during cold and warm traversals. Streaming, asset preparation and GC are suspects to inspect, not established causes from this summary.
2. Investigate the heavier **High village view** separately from the four-horse scene. High is not consistently at its 60 FPS target on this machine.
3. Trace allocation sources with the Profiler and repeat route loops until memory settles, before increasing world content. The current windows alone cannot distinguish warm-up residency from a leak.
4. Keep an Android device frame-time/memory/thermal pass pending until a matching build is available. Do not infer phone results from the desktop cap or these GPU temperatures.

Raw measurements: `Codex_profile_balanced.md`, `Codex_profile_high.md`, `Codex_profile_balanced_herd.md`, `Codex_profile_high_herd.md`. Main samples preceded the final small marker/camp prop polish and GLM's subsequent Phase 3 ambience; timestamps identify that baseline. Combat/journal checks are in `Codex_combat.txt`. No world expansion, graphics-tier change or vendor modification was made for this measurement task.

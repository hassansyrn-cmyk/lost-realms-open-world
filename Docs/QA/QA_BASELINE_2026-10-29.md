# QA baseline — 29 October 2026

Produced by `Assets/Editor/GlmQaSuite.cs` (GLM, ZCode) through `ClaudeBridge`: at each stop the suite waits 3 s for streaming, samples 3 s of wall-clock frame times, records `UnityEditor.UnityStats`, counts live actors, saves a screenshot to `ClaudeBridge/shots/glmqa_<tier>_<stop>.jpg` and tags console errors/exceptions raised while there. Times are editor play-mode times on the desktop (Ryzen + discrete GPU class machine), so they are a relative baseline for regression and for the Phase 4 Android pass, not phone numbers. Filtered as known vendor noise: Fusion import worker, graphicsApiMask/EnvironmentDependencyValues, NetworkPeer, ADTM audio-thread wait.

Rerun: `Docs/QA/README.md`. The brief's "North Pass" has no POI in code, so Frozen Lake stands in for it.

## Balanced tier — target 30 fps — 2026-10-29 22:41 — Unity 6000.6.0f1

| Stop | Avg ms | P95 ms | ~FPS | Short/long ms | Batches | SetPass | Tris | Enemies | Creatures | NPCs | Errors+ |
|---|---|---|---|---|---|---|---|---|---|---|---|
| hub | 33.32 | 35.36 | 30 | 29.6/35.9 | 0 | 181 | 3432907 | 3 | 54 | 6 | 0 |
| oakvale | 34.84 | 48.07 | 29 | 18.5/72.6 | 0 | 327 | 5556666 | 3 | 54 | 7 | 0 |
| mirror_lake | 33.33 | 34.80 | 30 | 27.6/38.4 | 0 | 72 | 710202 | 6 | 26 | 6 | 0 |
| frostpine | 33.30 | 38.09 | 30 | 18.1/39.2 | 0 | 201 | 4806609 | 10 | 38 | 6 | 0 |
| frozen_lake | 36.29 | 52.39 | 28 | 14.2/199.3 | 0 | 196 | 4937320 | 4 | 26 | 6 | 0 |
| sunspire | 33.33 | 36.19 | 30 | 26.6/37.3 | 0 | 175 | 3814556 | 7 | 8 | 6 | 0 |
| dune_ruins | 33.33 | 35.42 | 30 | 27.3/37.5 | 0 | 58 | 257963 | 8 | 10 | 6 | 0 |
| graveyard | 33.33 | 34.85 | 30 | 31.1/36.8 | 0 | 94 | 1266402 | 13 | 7 | 6 | 0 |
| stormveil | 33.57 | 36.05 | 30 | 21.7/74.6 | 0 | 199 | 5201288 | 16 | 0 | 6 | 0 |
| sky_isle | 33.36 | 35.83 | 30 | 26.6/37.1 | 0 | 105 | 1237166 | 3 | 41 | 6 | 0 |

Exceptions: none.

## Balanced tier — target 30 fps — 2026-10-30 00:41 — Unity 6000.6.0f1

| Stop | Avg ms | P95 ms | ~FPS | Short/long ms | Batches | SetPass | Tris | Enemies | Creatures | NPCs | Errors+ |
|---|---|---|---|---|---|---|---|---|---|---|---|
| hub | 33.32 | 35.09 | 30 | 21.2/46.8 | 0 | 184 | 3456389 | 3 | 54 | 10 | 0 |
| oakvale | 33.34 | 35.45 | 30 | 29.9/36.5 | 0 | 171 | 2789911 | 3 | 54 | 11 | 0 |
| mirror_lake | 33.33 | 34.66 | 30 | 31.9/35.2 | 0 | 78 | 710616 | 6 | 30 | 10 | 0 |
| frostpine | 33.29 | 34.74 | 30 | 29.0/35.5 | 0 | 216 | 5030985 | 10 | 38 | 10 | 0 |
| frozen_lake | 36.42 | 43.28 | 27 | 10.3/288.5 | 0 | 199 | 4939714 | 4 | 26 | 10 | 0 |
| sunspire | 33.34 | 34.31 | 30 | 30.3/36.8 | 0 | 180 | 3927833 | 7 | 8 | 10 | 0 |
| dune_ruins | 33.34 | 34.59 | 30 | 31.8/35.0 | 0 | 60 | 258073 | 8 | 10 | 10 | 0 |
| graveyard | 33.35 | 34.91 | 30 | 27.8/38.9 | 0 | 95 | 1375006 | 13 | 7 | 10 | 0 |
| stormveil | 33.35 | 36.07 | 30 | 26.7/40.1 | 0 | 212 | 5415672 | 16 | 0 | 10 | 0 |
| sky_isle | 33.34 | 34.83 | 30 | 29.9/37.0 | 0 | 105 | 1240998 | 3 | 41 | 10 | 0 |

Exceptions: none.

## High tier — target 60 fps — 2026-10-29 22:42 — Unity 6000.6.0f1

| Stop | Avg ms | P95 ms | ~FPS | Short/long ms | Batches | SetPass | Tris | Enemies | Creatures | NPCs | Errors+ |
|---|---|---|---|---|---|---|---|---|---|---|---|
| hub | 18.98 | 20.73 | 53 | 15.9/26.1 | 0 | 365 | 9815544 | 3 | 54 | 6 | 0 |
| oakvale | 17.77 | 20.99 | 56 | 14.8/41.4 | 0 | 323 | 8368876 | 3 | 54 | 7 | 0 |
| mirror_lake | 16.66 | 22.77 | 60 | 7.4/25.5 | 0 | 152 | 1858286 | 6 | 26 | 6 | 0 |
| frostpine | 18.54 | 20.70 | 54 | 16.3/28.2 | 0 | 0 | 0 | 10 | 38 | 6 | 0 |
| frozen_lake | 21.46 | 31.07 | 47 | 17.3/218.2 | 0 | 331 | 12293487 | 4 | 26 | 6 | 0 |
| sunspire | 16.97 | 32.27 | 59 | 12.0/33.5 | 0 | 296 | 8489302 | 7 | 8 | 6 | 0 |
| dune_ruins | 16.64 | 21.66 | 60 | 7.4/26.2 | 0 | 201 | 1107128 | 8 | 10 | 6 | 0 |
| graveyard | 20.27 | 25.08 | 49 | 16.0/31.3 | 0 | 204 | 3893822 | 13 | 7 | 6 | 0 |
| stormveil | 24.77 | 31.06 | 40 | 20.5/37.1 | 0 | 329 | 11936534 | 16 | 0 | 6 | 0 |
| sky_isle | 18.52 | 21.39 | 54 | 12.8/25.7 | 0 | 221 | 4394897 | 3 | 41 | 6 | 0 |

Exceptions: none.


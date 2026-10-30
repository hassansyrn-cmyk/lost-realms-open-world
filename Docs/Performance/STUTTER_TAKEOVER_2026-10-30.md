# Stutter takeover — 30 October 2026

Codex took over Claude's 03:12 investigation at the user's explicit request. Investigation and the verified fixes below are complete; remaining GPU/source-loading stalls are NOT declared solved. Android builds and device tests remain deferred.

## Confirmed findings

Claude's saved CPU captures (ClaudeBridge/out/spikes_frozen_lake.txt and spikes_stormveil.txt) showed large Addressables Editor loading stalls. His loadcosts.txt identified the generated bear clips among the largest dependencies. Current baseline 0292 reproduced the same ReadObject/serialized-file path: first Frozen Lake/Stormveil arrival maxima 4388.63/1739.63 ms, repeat maxima 65.96/98.89 ms. These are teleport arrival windows in Editor, not normal on-device traversal rates or guaranteed cold loads.

The focused, full-route captures completed the missing CPU/render-thread attribution:

- 0305: 923 captured CPU frames, 147.1 m. Largest native frame 389.4 ms included 350.3 ms waiting for the D3D12 GPU; the render thread spent 167.6 ms uploading textures in that frame.
- A separate 159.6 ms frame spent about 139.7 ms in the Addressables delayed loading/deserialization path.
- Another 187.6 ms frame included an Editor hierarchy presentation wait of 134.9 ms. Game View repaint and bridge polling also appeared in smaller outliers.
- 0306 repeated the route after the bridge fix: the largest native frame still included 303.6 ms of GPU wait and 163.0 ms texture upload; another frame spent 197.2 ms in delayed asset loading. These remain open and must not be presented as fixed by reducing script spawn counts.

These traces explain why the earlier five-marker recorder could not attribute every wall-clock hitch. The raw captures include Editor work and profiler overhead. They do not establish a driver defect or predict a phone's behaviour.

## Implemented fixes

### Loading lifecycle

Assets/Scripts/World/OpenWorldPopulation.cs now checks/reacquires invalid handles before a pending multi-frame spawn reads Result. Failed loads release/reset their handles and back off for 10 seconds; synchronous load exceptions no longer leave requested permanently true. Despawn cancels its pending group; released handles are reset explicitly.

Assets/Scripts/OpenWorldImportedContent.cs now checks model/audio handle validity after yielding, before Status/Result. Stale loading entries are cleared so a live request can retry. An invalid-handle error reproduced in this loader at 13:51:58 before the fix; later final console tails had no new gameplay exception.

0296 deliberately created a pending spawn with requested=true and an invalid handle. Recovery passed without dereferencing the invalid handle; the scenario and three return warps recorded zero tagged errors. The initially tried request-per-frame throttle did not demonstrate a performance benefit and was removed. The existing one-actor-per-frame spawn scheduling is retained.

### Generated asset payload

PreparedAnimalPayloadOptimization.Run removed 302,200 redundant keys from exact constant Transform curves in ONLY our ten generated expansion_bear_c_*.anim copies. Nonconstant curves, duration, frame rate and wrap settings are retained. Every clip was compared with its original over all bindings at 129 sample times; maximum sampled difference was zero. Constant-curve eligibility also requires identical values and zero tangents, so this is not a tolerance-based motion decimation.

Serialized source bytes: 276,350,115 -> 165,470,681, a 40.12% reduction. This is a source-asset byte measurement, not a claim of 40% lower runtime RAM or faster FPS. Evidence: Bear_payload_20261030_134925.md.

A one-line ClaudeAnimalPrep.CleanClip hook preserves the reduction on future preparation. The targeted Run command can optimize existing generated bear copies without rebuilding horses or all animals. Vendor/source clips, Aster's baked files and horse clips were untouched.

### Editor bridge overhead

Full CPU traces showed bridge Poll contributing 20–35 ms to several Editor outliers. ClaudeBridge.cs now caches unchanged command text (mtime changes are still detected), reflected boolean members and the disk-space query for 30 seconds. Repeated identical status text waits for the existing 2-second heartbeat; command/state transitions still update immediately. Status writes atomically replace the old file after mapped-file overwrite stalls left status.txt stale. Protocol, user-Play pause and command behaviour are retained.

Final 0306 bridge marker samples: mean 0.359 ms, P95 0.402 ms, maximum 28.178 ms. The tail is not eliminated. The route still completed, the wait-for property detected completion, Stop and refresh ran, and the status heartbeat continued updating afterward.

## Focused route measurements

Same retained Ultra tier, temporary 30 fps diagnostic cap, focused Editor, 875x420 Game View, CPU Editor profiling enabled and 2000-frame history. The cap, profiler settings and protected adventure state are restored afterward. These are attribution captures, not release performance certification.

| Wall-clock measure | 0305 before bridge caching | 0306 after bridge caching |
|---|---:|---:|
| Route seconds | 32.0 | 32.0 |
| Travel m | 147.1 | 144.2 |
| Mean ms | 34.75 | 34.60 |
| P95 ms | 51.64 | 48.31 |
| P99 ms | 64.95 | 67.87 |
| Maximum ms | 372.77 | 332.54 |
| Frames >50 ms | 55 | 30 |
| Unity used MiB start/end | 4025.1 / 5229.4 | 4035.1 / 5237.9 |
| Reserved peak MiB | 8627.8 | 8627.8 |
| Mono MiB start/end | 111.9 / 114.2 | 112.3 / 121.0 |

The distribution is mixed: mean barely changed and P99 was worse. Cache state, Editor activity, profiling, rendering/upload work and the slightly different travelled distances prevent treating the frame-count changes as a controlled causal FPS win. Direct bridge marker timings and reduced file/lookup work support that narrow fix.

The profiler itself retained about 400–448 MB of raw recording per route and substantial live capture data; the rising used memory above is not proof of a gameplay leak or a phone budget. Job 0307 clears the live profiler history after saving the raw captures.

0305 evidence: RouteTimeline_20261030_143344.md, RouteTimeline_before_bridge_20261030.txt, Temp/StutterRoute_20261030_143345.raw (400,348,371 bytes).

0306 evidence: RouteTimeline_bridge_cache_20261030_144254.md, ClaudeBridge/out/spikes_takeover_route_bridge_cache.txt, Temp/StutterRoute_20261030_144254.raw (448,188,320 bytes).

The 2000-frame limit avoids truncating this route to the default short history, at a profiling-memory cost. See [Unity's profiler history guidance](https://docs.unity.com/en-us/engine/6000.0/manual/analysis/profiler/window-navigating). Binary logging follows the [Profiler API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Profiling.Profiler-enableBinaryLog.html); these actual raw files were verified on disk.

## Other runs and limits

0292 baseline and 0296 after reports are retained as Takeover_before_20261030_133626.md and Takeover_after_20261030_140315.md. 0296 was slower overall, not an FPS success; some arrival profiler windows contained zero/incomplete data. 0299 timed out during a graphics-tier transition, so Takeover_final_20261030_142207.md is incomplete and has no valid final route/recovery result. Do not combine these with the focused route as if conditions were identical.

Runtime compile 14:17:10 and latest Editor compile 14:40:14 reported zero errors. Changed-source git diff --check passed with CRLF recognised. Known graphicsApiMask/EnvironmentDependencyValues/Fusion import-worker noise remains; no Photon changes. No Android/player/content build or heat/sustained-device measurement was run.

The conditional RegionWeather.cs, AmbientLife.cs, OpenWorldPerformanceProbe.cs and Addressables settings claims did not require edits. Realm.cs/StreamingDiagnostics.cs gained opt-in refresh/release timing scopes, not an unmeasured rendering or despawn policy change. Previous horse work remains intact.

## Remaining work

1. The remaining major hitch is texture upload/GPU presentation plus Editor source deserialization. Verify with a built player/content pipeline before applying device-specific changes or claiming smoothness; Editor source loading is not a substitute for that measurement. Android remains deferred by user instruction.
2. Identify the uploaded textures/resources around the captured outliers and inspect read/write copies, compression support, residency and loading behaviour in that built target. Do not alter vendor packs or lower global quality on speculation.
3. Measure sustained frame times, actual device memory and heat when device builds resume. No further world expansion should be justified by the current Editor-only measurements.

Shared claims and exact chronology: ../DEVELOPMENT_LOG.md. Regional content delivery: ../REGIONAL_DISCOVERIES_2026-10-30.md. All Codex takeover claims are released after the completion entry; the residual GPU/device work remains explicitly open.

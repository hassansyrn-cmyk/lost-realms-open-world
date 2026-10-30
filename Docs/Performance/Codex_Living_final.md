# Living Verdant final — 10/30/2026 2:18:36 AM
Desktop Editor, High target 60; not Android.

| Scene | seconds | frames | mean | p50 | p95 | p99 | max | >50ms | used start/end MiB | peak | reserved | Mono | main p95 | alloc p95 KiB | gc0 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---|---:|---:|---:|
| Hunt route | 35.0 | 2007 | 17.44 | 15.92 | 32.03 | 34.01 | 180.48 | 5 | 3069.8 → 3100.2 | 3100.2 | 5547.2 | 98.8 → 105.3 | 33.99 | 46.8 | 3 |

Travelled 165.8 m.

| Work (nested scopes; do not sum) | Calls | Total ms | Max ms | Items |
|---|---:|---:|---:|---:|
| RealmTick | 2291 | 161.26 | 6.32 | 0 |
| RealmFill | 269 | 195.30 | 6.56 | 1272 |
| PopulationTick | 115 | 116.73 | 12.99 | 0 |
| PopulationSpawn | 94 | 103.28 | 6.02 | 94 |

Native markers available: Shader.CreateGPUProgram, GC.Collect, Gfx.WaitForPresentOnGfxThread, Gfx.WaitForGfxCommandsFromMainThread, Mesh.UploadMeshData. First wall sample includes settle and is excluded from frame conclusions.
wall 3340.97 ms; Shader.CreateGPUProgram=0.00 ms; GC.Collect=0.00 ms; Gfx.WaitForPresentOnGfxThread=0.00 ms; Gfx.WaitForGfxCommandsFromMainThread=12.87 ms; Mesh.UploadMeshData=0.00 ms
wall 180.60 ms; Shader.CreateGPUProgram=0.00 ms; GC.Collect=0.00 ms; Gfx.WaitForPresentOnGfxThread=0.00 ms; Gfx.WaitForGfxCommandsFromMainThread=0.00 ms; Mesh.UploadMeshData=0.00 ms
wall 96.49 ms; Shader.CreateGPUProgram=0.00 ms; GC.Collect=0.00 ms; Gfx.WaitForPresentOnGfxThread=0.00 ms; Gfx.WaitForGfxCommandsFromMainThread=0.00 ms; Mesh.UploadMeshData=0.00 ms
wall 95.57 ms; Shader.CreateGPUProgram=0.00 ms; GC.Collect=0.00 ms; Gfx.WaitForPresentOnGfxThread=0.00 ms; Gfx.WaitForGfxCommandsFromMainThread=0.00 ms; Mesh.UploadMeshData=0.00 ms
wall 65.06 ms; Shader.CreateGPUProgram=0.00 ms; GC.Collect=0.00 ms; Gfx.WaitForPresentOnGfxThread=0.00 ms; Gfx.WaitForGfxCommandsFromMainThread=61.64 ms; Mesh.UploadMeshData=0.00 ms
wall 64.99 ms; Shader.CreateGPUProgram=0.00 ms; GC.Collect=0.00 ms; Gfx.WaitForPresentOnGfxThread=0.00 ms; Gfx.WaitForGfxCommandsFromMainThread=55.63 ms; Mesh.UploadMeshData=0.00 ms

Slowest measured work (includes startup):
- PopulationTick 12.99 ms, 0 items, realtime 12.82.
- PopulationTick 8.90 ms, 0 items, realtime 20.04.
- PopulationTick 7.97 ms, 0 items, realtime 25.33.
- RealmFill 6.56 ms, 12 items, realtime 12.71.
- RealmTick 6.32 ms, 0 items, realtime 20.97.
- PopulationTick 6.31 ms, 0 items, realtime 19.75.
- PopulationSpawn 6.02 ms, 1 items, realtime 19.75.
- RealmFill 5.96 ms, 1 items, realtime 20.97.
- RealmFill 5.46 ms, 6 items, realtime 12.69.
- PopulationSpawn 5.37 ms, 1 items, realtime 20.04.
- PopulationTick 5.08 ms, 0 items, realtime 20.07.
- PopulationSpawn 5.08 ms, 1 items, realtime 20.07.

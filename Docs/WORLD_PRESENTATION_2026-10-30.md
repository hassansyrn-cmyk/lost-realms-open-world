# Banner cloth and Aether astrolabe — 30 October 2026

Codex presentation pass requested by the user.

## Banner

Outpost flags now start with their original red fabric and artwork. Capturing a camp or restoring its captured save state changes red textile dye to emerald through a selective shader mask. A warm-gold protection term preserves the embroidered emblem and edging; bronze hardware, blue gems, wood and stone retain their original atlas colours. Removed the whole-model colour multiply/emission. The procedural fallback changes only its named fabric renderer.

The original texture/model/normal map are untouched. The shader lives in Resources/World/Presentation/BannerCloth.shader and is loaded directly, so no separate .mat asset was needed. Each banner owns/releases its runtime material. The mask is authored for this particular red/gold atlas; do not reuse it for unrelated banners without reviewing the palette.

## Grapple point

Replaced the sphere and flat spinning cylinder with an original Aether astrolabe: bevelled broken gold arcs, a bronze compass frame, faceted teal crystal, luminous inlays and small pendants. Solar/lunar rings counter-rotate and the crystal floats gently. The animation moves visual children only; anchor positions and traversal logic remain unchanged. Labels sit above the larger silhouette and no longer display the incorrect Q binding.

Four combined mesh assemblies, 7936 triangles, five owned materials, zero colliders and zero added lights per anchor. Mesh/material resources release on teardown. Animation pauses in menus and beyond 100 m. No mobile frame-rate claim is made.

## Validation

- Unity runtime/editor scripts: zero errors at 02:56:20; final editor helper compile zero errors at 02:59:36. Banner shader compiled without errors before/after use.
- Jobs 0260/0261/0262 completed. Final report Docs/WORLD_PRESENTATION_2026-10-30.txt verifies original texture retention, red/captured material states, real Capture transition, restored captured state, mesh bounds/count, absence of colliders/lights, stationary gameplay pivot during animation, existing grapple action and glider deployment.
- Visually reviewed in-world first pass, then clean isolated asset previews. First comparison's second camp sampled a higher surface, so the test fixture was aligned to one height; no production placement change was needed. Final preview banner_fabric_comparison.png shows both states and gold trim together. grapple_astrolabe.png shows the replacement design. Previews hide environment/HUD for inspection, not a change to game presentation.
- Protected adventure snapshot and camera/canvas settings restored; temporary fixtures removed. No Android build. No vendor, horse, credits, performance, Photon or baked Aster animation edits.

Files: Assets/Scripts/OpenWorldEnemies.cs (camp banner only); OpenWorldCryptAndStorm.cs (anchor visuals/label only); new OpenWorldBannerCloth.cs, OpenWorldGrappleVisual.cs; Resources/World/Presentation/BannerCloth.shader; new Assets/Editor/WorldPresentationValidation.cs and generated metas.

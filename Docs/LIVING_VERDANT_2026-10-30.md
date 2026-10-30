# Living Verdant pass — 30 October 2026

Codex. Desktop Unity Editor checks; Android builds deferred at the user's request.

## Delivered

- Prop streaming yields after a 3 ms scheduling budget between instantiations. Individual objects can exceed that budget. Population groups now assemble one actor per frame, retain existing budgets and cancel unfinished distant groups. Imported enemies/quarry go straight to their own rig rather than constructing and discarding a fallback character first.
- The hunting detour has ground-fitted worn paths, amber ribbon stakes and three localized low-foliage clearings around the fallen marker, hidden cache and supply camp. This uses shader state only; no TerrainData modification or global grass-density reduction. Runtime meshes, textures and new trail materials are released on teardown. Shader clearing state resets after leaving the area.
- Brom, Lyra, Rowan, Vane, Kael, Mira and the caravan NPC have short, obstacle-checked routines: dawn 05–08, work 08–17, evening 17–21, rest 21–05. Rest means remaining near home with an idle pose, not a new sleep animation. Far NPCs idle; conversations take priority. Brom occasionally uses the existing work gesture. Lyra and Rowan are unarmed civilians. Repeat conversation lines vary with time/role; the caravan responds to the cleared camp. Introductions, rewards and regional quest-giver locations remain intact.
- Replayed the complete hunt through real interactions and combat: accept, both tracks, Thornhide, cooking and reward. Scripted attack/parry/dodge/summon decisions defeated Thornhide in 9.6 seconds, seven attack warnings, two successful summons, minimum hero HP 5/5. This is an effective-response scenario with the current character progression, not evidence that an inexperienced player will find the fight easy. Retained existing combat/reward numbers rather than tuning difficulty from a perfect-response script alone. Human difficulty/sound/camera-feel judgment remains outstanding.

## Measured performance

Same 35-second High-tier desktop route (~164–166 m), diagnostics armed for these runs only. Scopes are nested and include startup; do not sum them. Streaming diagnostics default off in players.

| Measure | Before | Final |
|---|---:|---:|
| Prop streaming update max | 25.55 ms | 6.32 ms |
| Population update max | 40.57 ms | 12.99 ms |
| Actor spawn call max | 17.16 ms / up to 4 actors | 6.02 ms / 1 actor |
| Mean frame | 17.66 ms | 17.44 ms |
| P95 frame | 32.03 ms | 32.03 ms |
| P99 frame | 34.33 ms | 34.01 ms |
| Maximum frame | 608.77 ms | 180.48 ms |
| Frames >50 ms | 5 | 5 |
| Unity used memory, start → end | 2992.5 → 3009.6 MiB | 3069.8 → 3100.2 MiB |
| Reserved memory | 5547.2 MiB | 5547.2 MiB |
| Mono memory, start → end | 103.1 → 104.7 MiB | 98.8 → 105.3 MiB |

The scoped spawn costs improved; overall frame distribution barely changed and hitches are NOT resolved. Successive Editor sessions have different cache/loaded-asset states, so neither the maximum-frame reduction nor memory difference proves a causal improvement/regression. Final trace found 65 ms stalls coinciding with 56–62 ms Gfx.WaitForGfxCommandsFromMainThread. The 180/96/95 ms stalls were not explained by the five sampled native markers. Recorder values are frame samples, not a full CPU timeline; do not claim attribution. Next: full CPU/render-thread timeline around the outlier, including refresh/despawn, asset loading, Editor activity and GPU workload. Device memory, heat and sustained performance remain unmeasured.

## Verification actually run

- Unity runtime/editor compilation: zero errors after changes; final runtime compile 02:19:53. Known graphicsApiMask/import-worker noise remains.
- Bridge 0241 baseline, 0243 scheduling replay, 0247 final trace. Raw reports: Docs/Performance/Codex_Living_before.md, Codex_Living_after_streaming.md, Codex_Living_final.md.
- Bridge 0245: full hunt, one imported quarry Animator, cooking consumes one meat, advertised 120 gold / 5 gems / Ranger Axe reward and no duplicate reward. All passed. NPC clock phases, varied dialogue, conversation pause and solid-wall fixture passed. Raw checks: Docs/Performance/Codex_Living_validation.txt.
- Bridge 0245 initially failed the shader teardown assertion after only 0.5 s; retest 0246/0247/0248 allowed 2 s for deferred streaming/destruction and passed: hero (-220,-180), zero presentations, clearing count zero. No speculative production workaround was added. 0246's trace invocation was invalid and produced no performance capture; 0247 supersedes it.
- Latest trail captures living_trail_0/1/2.png visually reviewed: note/camp readable and cache approached through the southern opening; striped draft ground texture replaced with softer procedural noise. Capture artifacts: ClaudeBridge/shots/.
- 0248 retested trail build/teardown after adding ownership cleanup for ribbon materials; passed. Adventure snapshot and weather/hour restored. Editor left out of Play mode.

## Files

Assets/Scripts/World/OpenWorldRealm.cs; World/OpenWorldPopulation.cs; OpenWorldCharacterVisual.cs; OpenWorldEnemies.cs; OpenWorldVerdantHunt.cs; OpenWorldVillagesAndNPCs.cs; OpenWorldHuntingDiscoveries.cs; new OpenWorldNpcRoutine.cs, OpenWorldTrailPresentation.cs, OpenWorldStreamingDiagnostics.cs; Assets/GameContent/Shaders/LostRealmsFoliage.shader; new Assets/Editor/VerdantLivingWorldValidation.cs and metas. Reports and bridge jobs 0240–0248.

Horse files, GLM tier/weather/quests/credits, vendor packs, Photon and baked Aster clips were not edited in this pass. No Android build or multiplayer work.

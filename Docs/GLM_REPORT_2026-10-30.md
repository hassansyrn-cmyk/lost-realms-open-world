# GLM (ZCode) night report — 29/30 October 2026

Five phases per `Docs/TASKS_GLM_2026-10-29.md`, worked 22:13–~05:00 alongside Codex (combat feel, journal, performance measurement) and Antigravity (horses). Full details and per-check evidence are in `Docs/DEVELOPMENT_LOG.md` under "GLM (ZCode)".

## Phase 1 — QA autopilot and baseline (done 22:56)

- `Assets/Editor/GlmQaSuite.cs`: bridge-driven play-mode tour of 10 stops; per stop 3 s settle + 3 s frame-time sampling, profiler render counters, live actor census, screenshot, and exceptions tagged by stop (vendor noise filtered). One bridge job per tier, ~2 min each.
- `Docs/QA/QA_BASELINE_2026-10-29.md` + `Docs/QA/README.md` (rerun instructions). Headline numbers: Balanced locked 30 fps everywhere; High ~53-60 fps desktop; Stormveil is the heaviest view; frozen_lake shows a 218-273 ms arrival streaming hitch (a Phase 4 target, attributed to first-visit prefab streaming, not fixed tonight).

## Phase 2 — side quests + quest board (done 00:25)

Four quests, one file each, `A Hunter's Promise` pattern, each 8-15 minutes, rewards saved once:

| Quest | Region | Giver | Reward |
|---|---|---|---|
| The Lost Supply Sled | Frozen Peaks | Matthias, Frostpine | 110 g, 4 gems, Woodsman's Axe |
| Trial of the Sunspire | Burning Dunes | Sahra | 120 g, 6 gems |
| Restless Lanterns (night only) | Hollow Marches | Sexton Ivo | 140 g, 5 gems |
| The Conductor | Stormveil | Rhega | 120 g, 5 gems, Reaper's Spear |

Hub quest board (2,18) opens a modal listing all five side quests with live status; Codex's journal reads the same state via `GlmQuests.Snapshot`. Validated end to end by `Assets/Editor/GlmQuestValidation.cs` (normal Interact, real TakeHit kills, one-time rewards, save/load round trip) — final run all green; captures `ClaudeBridge/shots/glmq_*.png`. One real quest bug found and fixed during validation (the wraith's defeat was unobserved, making Restless Lanterns uncompletable).

## Phase 3 — region weather, ambient life, rain splashes (done 00:55)

- `World/OpenWorldRegionWeather.cs`: schedules the atmosphere's presets by biome and hour (Verdant showers, Frozen whiteout mist, Dunes dust haze, Marches night mist, Stormveil thunderstorms), holds each ≥150 s, respects manual clock choices for 5 minutes, persists per biome in PlayerPrefs.
- `World/OpenWorldAmbientLife.cs`: pooled butterflies, dragonflies, fireflies, falling leaves, dust plumes, spindrift and lake fish jumps (via WaterFx splashes); tier-scaled, distance-culled.
- `OpenWorldSoundscape`/`OpenWorldWaterFx`: rain footstep splashes on dry land while raining (no Hero edits, per the 22:58 update).
- Frame-cost check (QA tour rerun, `Docs/QA` third table): every stop identical to baseline at the 30 fps cap, 0 new exceptions — well inside the 10% budget. Captures: `ClaudeBridge/shots/glm3_*.png`.
- Not done: per-region music with night variants and a combat layer (needs a CC0 sourcing decision); mist lags fast clock jumps by one 20 s decision step.

## Phase 4 — performance (partial)

- Codex measured (`Docs/Performance/Codex_*`); GLM fixes only. Applied: `World/OpenWorldTierTerrain.cs` — per-tier live terrain detail (Balanced 45 m @ 0.4 density, basemap 160 m; High/Ultra re-assert the bake values) on top of the realm bake's own tiering.
- Validated with a full QA tour on Balanced (job 0140, third table in the QA baseline): SetPass down 3-49% per stop (oakvale 327 to 167) and triangles down 2-56% (oakvale 5.56M to 2.46M) versus Phase 1; average frame time unchanged because Balanced sits at its 30 fps cap — the win is phone headroom. Stormveil.s 39.9 ms avg / 425 ms worst in that run includes the thunderstorm particles and a 16-enemy stream-in. Remaining (documented, not fixable tonight without deeper profiling): the 219-292 ms route/arrival streaming stalls (first-load prefab bursts; suspects are streaming ranges and GC, per Codex's handoff), High-tier SetPass above the 120 guidance (material consolidation at world scale), and per-creature PlayableGraph culling (needs an API on the creature model classes owned by other agents).

## Phase 5 — credits and code health (done, pending final visual check)

- `Assets/Scripts/OpenWorldCredits.cs`: credits modal opened from Pause → SYSTEM tab ("CREDITS" button). Finished 30 Oct 02:55: the attribution body is a working ScrollRect (drag or wheel), the frame adapts to the safe area, and OpenWorldCreditsRuntime.Validate asserts all four imported-model credits, licence tokens and that scrolling moves the content (Docs/glm-credits-validation.txt). All CC-BY/attribution entries live in one table: Horse by Henry S (CC BY 4.0, full required credit string), Mountain Dragon by Alexey Zaika (CC BY 4.0 + required modification notice), Komodo by Nyilonelycompany (Sketchfab Standard), and the Medieval Civilian flagged as attribution-pending (no licence document in its archive). New assets need one new line.
- Remaining compiler warnings are all in other agents' or vendor files — GLM's files compile clean: Claude's `ClaudeReviewChecks`/`ClaudePhotonSwitch`/`ClaudeBridge` (UAC0005 ×3), Codex's `GameplayFollowupValidation`/`VerdantSliceValidation`/`WildlifeExpansionValidation`/`OpenWorldWhistleHud` (obsolete FindObjects overloads), vendor `living birds`, `TerrainToolkit`, `Dynamic Snow System`, `TreasureChestMaker`.

## For the phone (when there is a build)

- Everything above is editor-validated only; no APK or Addressables content build was made tonight (postponed by Codex's pass, per the brief).
- Test on device: the four quests (touch Interact), the quest board, region weather transitions, ambient life density on Balanced, and the credits screen legibility.
- Watch: Stormveil on Balanced is the heaviest view (16 enemies streamed); the frozen_lake arrival hitch should be re-checked on device.

# Regional discoveries — 30 October 2026

Codex completed GLM's pending runtime validation and repaired the issues it exposed. No Android build.

## Delivered

Eight discoveries now work through the existing Interact button/F key, banner and journal. Four notes have raised parchment beside their stone marker; four caches retain their textured chest models. Lore is displayed on collection and retained in the journal. Each site pays its original gold/gem reward once per adventure.

Props search nearby dry, clear terrain instead of using one blind water offset. Uneven ground gets the existing textured stone support; note stakes follow the ground at their own base. Search stays within 32 m of the original anchor and does not modify TerrainData, roads or global grass. Actual successful placement offsets were 0–12 m.

Only lightweight site records are created at startup. Prefab/textures request asynchronously nearby; at most one site's model is built in a frame. Props deactivate beyond 45 m; labels billboard near the player. Runtime gameplay no longer appends files under Docs. Shared helper materials remain owned by their cache; generated parchment owns its mesh/texture/material cleanup.

## Verified placements and rewards

Coordinates are X,Z from the completed 0297 tour. The repeated 0298 tour also passed.

| Region | Discovery | Placement X,Z | Gold | Gems |
|---|---|---|---:|---:|
| Frozen Peaks | Scout's Last Letter | -160, 288 | 20 | 1 |
| Frozen Peaks | Frozen Supply Cache | -84, 300 | 30 | 3 |
| Burning Dunes | Caravan Ledger Page | 292, -8 | 20 | 1 |
| Burning Dunes | Sunken Caravan Strongbox | 370, 84.54 | 35 | 3 |
| Hollow Marches | Mourner's Vow | -194, -206 | 25 | 1 |
| Hollow Marches | Mourner's Offering | -238, -244 | 35 | 2 |
| Stormveil | Stormwarden's Test | 225.46, -248 | 25 | 1 |
| Stormveil | Lightning-Sealed Chest | 254.54, -294 | 40 | 3 |

The placement search uses current colliders, so a later visit can choose a different suitable point nearby. It preserves the canonical glm-disc: saved IDs.

## Checks actually run

0297 and final 0298 passed all eight sites: model ready, dry/grounded bounds, valid materials, parchment on notes, clear approach, normal interaction prompt, real Manager.Interact dispatch, exact reward, second collection refusal, full lore in banner/journal, disk save/load flags and distant deactivation. Temporary solid cover blocked interaction. Totals are 230 gold and 15 gems if all eight are collected from a fresh state.

Final raw evidence: Docs/QA/Regional_discoveries_20261030_141950.txt. Earlier complete tour: Regional_discoveries_20261030_141213.txt. All eight first-tour captures reviewed; the desert-note preview lens initially clipped nearby scenery. An obstruction-tested lens corrected it, and the final desert-note capture was reviewed clear.

Captures: ClaudeBridge/shots/discovery_frozen-note.png, discovery_frozen-cache.png, discovery_dunes-note.png, discovery_dunes-cache.png, discovery_marches-note.png, discovery_marches-cache.png, discovery_storm-note.png and discovery_storm-cache.png.

0293 never entered Play because an editor test called a runtime-internal helper. 0294's approach search treated Aster at the target as cover. 0295 reached and verified the first note, but the original flat-ground search was too restrictive for the frozen cache. These are not complete passes; the corrections are covered by 0297/0298.

## Files and handoff

- Assets/Scripts/World/OpenWorldRegionDiscoveries.cs: discovery loading, grounding, presentation, access, lore, reward and editor-only legacy test helper.
- Assets/Scripts/OpenWorldAdventure.cs: one InteractionPrompt lookup so the existing mobile button can reach discoveries. Existing GLM interaction dispatch retained.
- Assets/Editor/RegionDiscoveryValidation.cs + meta: protected, repeatable editor tour. Run exec LostRealmsOW.RegionDiscoveryValidation.Begin in Play, wait until Busy is false, then exec LostRealmsOW.RegionDiscoveryValidation.Cleanup before Stop. See bridge 0298 for the complete guarded command sequence.

Tests protect and restore the adventure snapshot; the save-unavailable banner in QA screenshots is intentional. No NPC/horse/vendor/Photon/Aster baked-clip changes in this pass. Physical-device usability and performance remain deferred. Stutter findings: Performance/STUTTER_TAKEOVER_2026-10-30.md.

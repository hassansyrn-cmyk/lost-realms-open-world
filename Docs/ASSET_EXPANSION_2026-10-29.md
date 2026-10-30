# Curated asset and wildlife expansion — 29 October 2026

## Scope and organization

The read-only prefab inventory covers 2,775 vendor prefabs across 81 prefab-bearing asset folders. See `AssetAudit/imported-prefabs-2026-10-29.csv`. Selection is deliberate: imported packs remain source libraries, not objects automatically scattered across the map.

Vendor source assets remain in their original folders. Prepared URP copies, event-free creature clips and materials live under `Assets/GameContent/Imported`. Regional Addressables metadata lives in `Assets/Resources/World/ImportedContent.json`. The selection source is `Docs/AssetAudit/selected-content.json`; expansion entries use the `expansion_` prefix.

`Lost Realms OW > Content > Prepare Wildlife Expansion` rebuilds only the expansion entries plus horse/deer running clips. It preserves existing manifest entries and does not invoke Aster's animation baker. The complete content-preparation route also understands the new running clips.

## Additions

| Content | Intended location/use |
|---|---|
| Small supply crate and barrel | Caravan post near (-102, -99) and (-104, -99) |
| Village cart and food barrel | Oakvale supplies near (72, 60) and (53, 44) |
| Large supply crate | Frostpine near (-158, 270) |
| Desert supply cart | Sunspire near (375, -82) |
| Orc Warrior with Sword and Shield | Small vanguard groups in Stormveil, outside settlements |
| Orc Wizard with Wand | Lone hexers in the Hollow Marches, outside settlements |
| Caravan Iron Sword | Discoverable at the caravan, stable weapon ID 16 |
| Woodsman's Axe | Discoverable at the caravan, stable weapon ID 17 |

All ten candidates passed preparation. Orcs reuse the existing combat, reaction, damage, reward and streaming systems; their original demo scripts are removed from prepared copies. Existing chest reward odds stay unchanged. New weapons are also accessible through Developer mode.

Coordinates are search anchors, not instructions to overlap a building. Settlement dressing checks water, road clearance, terrain and physics occupancy, searches a bounded nearby area, and skips an item when no suitable spot exists. Props receive terrain support as needed. Existing regional load/triangle budgets continue to apply. Wildlife/enemy spawns retain biome, village-distance and slope rules and population limits.

Large settlement blueprints, additional dragon/minotaur packs, HDRP-only dogs, unsupported combat rigs, bows/shields requiring new gameplay, and alternative art styles remain candidates for separate curated passes. This pass does not claim every imported asset is integrated or every pack is licensed for release.

## Animal interactions

- Approach a horse and use Interact (F or the touch prompt) to mount. Move normally; hold Dash/Shift to gallop. Interact again to dismount where there is clear, dry ground.
- Mounted movement uses a horse collision capsule and rejects unsafe terrain, water and world edges. Riding cancels pending attacks. Damage and teleporting release the mount. The mount remains streamed while ridden; a nearby dismounted horse also remains loaded.
- Horses use prepared idle/walk/run clips. Aster's seated pose is procedural; no Aster animation clips are modified. This is a functional riding implementation, not a finished saddle/rein/mount-animation system. Horse ownership and mount persistence across reloads are not implemented.
- Ground animals other than live deer offer a calming pet interaction. Birds remain ambient. Deer flee and can be struck by Aster's existing melee/area attacks; a defeated deer offers a separate collect interaction for two raw meat.
- Harvest IDs prevent repeat collection and are included in the adventure save. Meat is limited to 999; a full pouch leaves the carcass available. Defeated deer without a death clip use a simple fallen pose. This still needs a dedicated authored death animation for final polish.
- Campfire cooking consumes one raw meat and grants the existing meal restoration/buff. Cooking with no meat gives a hint and no reward. Old saves default to zero meat. Normal quest/NPC/campfire interactions take priority over nearby animals; dismounting takes priority while riding.
- Antigravity's subsequent HUD work provides a meat display and mounted controls. That work is preserved.

## Validation and limits

See `wildlife-expansion-validation.txt` for executable checks and `AssetAudit/EXPANSION_PREPARATION_REPORT.md` for accepted assets. The checks cover melee hunting, exactly-once harvesting, meat/harvest-ID serialization, old-save default, cooking cost, mounting and teleport cleanup. A live two-second gallop probe moved the horse approximately 12 metres with the rider attached. Screenshots under `ClaudeBridge/shots/expansion_*` document riding, habitats, orcs and placement tours.

The initial placement tour revealed oversized horizontal weapon pickups and blocked cart anchors. Follow-up changes normalize weapon pickups by their longest dimension and widen the bounded placement search. The latest placement report records observed positions; it can contain older entries from earlier tours, so use the latest validation screenshots/results when comparing.

Android bundle/player rebuilding and physical phone tests have not been performed. Extended slope/obstacle riding, every rider skin, all interaction combinations and sustained mobile performance still need broader QA. Unity also emits pre-existing graphics API import-cache errors during some refreshes; these are separate from gameplay test results.


## Final follow-up results

The fresh-scene review placed the Oakvale cart at approximately (74.45, 5.19, 58.44) and the Sunspire cart at (375, 7.75, -82), clear of surrounding structures. The two new weapon meshes now measure 1.10 m and 1.15 m on their longest dimension. Expansion weapon pickups also use the clearance search, hover above the grass and show only the nearest pickup label to avoid overlapping names.

Unity compiled the final runtime changes with zero C# errors. The gameplay verification passed again after the interaction-priority change. Screenshots `expansion_oakvale_final.png`, `expansion_sunspire_final.png`, `expansion_caravan_final.png` and `expansion_weapon_pickups_final.png` provide the final placement review. Earlier mount/gallop results remain documented in the development log because the basic verification report is overwritten on each run.

A separate imported Photon networking setup notice blocked entering Play mode and made the editor appear frozen. Dismissing the informational notice resumed the bridge. No networking account or service settings were configured. The Photon import may still need isolation/configuration before future unattended tests; do not confuse that dialog with a gameplay deadlock.

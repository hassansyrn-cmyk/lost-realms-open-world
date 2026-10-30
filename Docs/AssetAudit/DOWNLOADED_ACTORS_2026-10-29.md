# Downloaded actor integration — 29 October 2026

## Locations and behaviour

| Actor | Region / anchor X,Z | Behaviour |
|---|---|---|
| Marek, Caravan Provisioner | Caravan Post, 91,-1 | Humanoid idle, short clearance-checked patrol, faces Aster and gestures during conversation. No weapon. |
| Oasis Komodo | Sunwell Oasis dry outer bank, 319,153 | Wary animal, slow walk, retreats when rushed, huntable for two raw meat using the existing animal interaction. |
| Mountain Dragon | Dragon Peak, 10,400 | Animated aerial landmark, 27 m orbit above mountain ground; deliberately no ground combat or collision while airborne. |

Ground actors search a small neighbourhood for dry, clear, level terrain. They are not placed in water or blindly at the anchor if no safe spot is found. Regional mode only. The original small prototype world does not contain the distant habitats.

The three actors stream independently through the `DownloadedActors` Addressables group, at ranges 75 / 85 / 165 m with 35 m unload hysteresis. Only one of each exists. Completed Komodo collection respects the existing saved persistent ID `downloaded:komodo:oasis`. No horse assets or horse preparation code are involved.

## Asset organisation

`Assets/GameContent/DownloadedModels/{Civilian,Komodo,MountainDragon}/Source` holds source FBX/textures. `Prepared` holds URP materials, event-free animation copies and the game prefab. Original files in Downloads are unchanged. Runtime placement lives in `Assets/Scripts/World/OpenWorldDownloadedActors.cs`. Rebuild these actors with **Lost Realms OW > Content > Prepare Downloaded Actors**. This command does not run horse preparation. Addressables need a matching platform content build before Android deployment.

The civilian's original Mixamo skeleton is imported as Humanoid and retargets copies of existing idle/walk/talk motions. Aster's baked clips are read only and unmodified. Komodo animation is authored mesh-shape animation, retained rather than replaced by a skeleton. Mountain dragon retains its original skeleton and an extracted wingbeat loop. No vendor pack is edited.

## Source credits and licence metadata

Source metadata below combines embedded declarations with the civilian licence supplied by the user. This is not a verification of purchase history.

- **Mountain Dragon**, Alexey Zaika / lexazaixa25. Source: https://sketchfab.com/3d-models/mountain-dragon-0a0665396f634b0e9d0872720087f52c . Author: https://sketchfab.com/lexazaixa25 . Licence: **CC BY 4.0**, http://creativecommons.org/licenses/by/4.0/ . Changes: converted GLB to FBX, extracted textures/wingbeat, URP materials, game scale and flight placement. Preserve this attribution in shipped credits.
- **Komodo Dragon Lizard**, Nyilonelycompany. Source: https://sketchfab.com/3d-models/komodo-dragon-lizard-f4c8b2accf50495db3d64ca583f2a148 . Author: https://sketchfab.com/Nyilonelycompany . Embedded licence: **Sketchfab Standard**, https://sketchfab.com/licenses . Changes: GLB to FBX, extracted texture and walk loop, URP material, game scale and wildlife integration. The local file's licence declaration does not establish the user's account entitlement.
- **Medieval Civilian 3**, Leonardo Carvalho (https://sketchfab.com/livrosparacriancas). Source: https://sketchfab.com/3d-models/medieval-civilian-3-d99ca22f06134e8dab28fdfa75d87f31 . Licence: **Sketchfab Free Standard** — commercial use allowed, credit not required; do not redistribute the model file on its own. Confirmed source/licence supplied by the user (Claude review, 30 October 2026). Supplied archive: `medieval-civilian-3.zip`, inner `source/Civilian 3.zip`. Changes: humanoid avatar setup, texture assignment/URP materials, scale and retargeted game motions.

Inspection/conversion scripts and reports: `Tools/downloaded_models/`. Portable Blender and staging downloads are ignored by Git. Source metadata was read locally; no online licence verification was performed.

## Verification

See `DOWNLOADED_ACTORS_PREPARATION.txt` and `DOWNLOADED_ACTORS_VALIDATION.txt` for actual run results. Android performance and built-content verification remain separate from editor validation.

Komodo mobile preparation retains 26 shape keys (basis plus the 25 active walk poses), instead of all 191 source shapes. The prepared FBX is 10,124,396 bytes versus 67,110,924 before pruning. Full untouched animation remains in the user's original GLB. Reproduce with convert_models.py then optimize_komodo.py, followed by the Unity preparation command.

The civilian uses a separate foot-grounding component to compensate the shared motions' flattened body-root height. This leaves the shared animation files unchanged. The skeleton-free Komodo receives an Animator on the prepared model so its blend-shape curves play through the standard game driver.

Final editor checks passed: all three actors' materials and motion; civilian foot contact; intended regional placement; distance unloading; Komodo defeat/two-meat collection/saved ID/no double collection/no respawn after collection. Regional results are retained in DOWNLOADED_ACTORS_REGIONAL_VALIDATION.txt; final focused animation/hunting checks are in DOWNLOADED_ACTORS_VALIDATION.txt. Screenshots: ClaudeBridge/shots/downloaded_civilian_final.jpg, downloaded_komodo_final.jpg, downloaded_dragon_final.jpg. Final runtime compilation 22:12:43 and editor compilation 22:10:29: zero errors. No Android or player-content build was run.



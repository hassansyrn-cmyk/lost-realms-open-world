# Discovery polish and horse extras — 30 October 2026

Completed by Codex. Shared coordination and file claims: DEVELOPMENT_LOG.md.

## Changes

- Verdant hidden cache now uses the prepared world-kit rock_medium_01/02 models with their textured URP materials. Grounded rocks retain a southern opening to the cache.
- The trail note is raised parchment on a stake with ink, seal, pin and a restrained proximity glint. Original interaction remains; no new UI.
- Horse preparation maps Horse_Walk_L/R, Horse_Gallop_L/R, Horse_RunStop, Horse_Buck, Horse_Swim and Horse_Jump. Walk/gallop banks enter above 28 degrees/second and leave below 14; base natural speeds and normalized stride phase are shared.
- Walk banks retain their full authored clips. Gallop banks layer authored torso turns over the corrected base gallop legs. Calibrated contact windows and bounded leg correction reduce turning slip without moving the root/pelvis. Straight-gait correction is unchanged.
- Releasing input from canter/gallop plays RunStop, then idle; renewed input interrupts it. First wild mount plays Buck once with controls/dismount locked until completion. Tamed remount does not repeat it; existing banner explains the lock.
- Swim/Jump clips are prepared capabilities only. Existing deep-water exclusion remains; swimming/jumping traversal was not enabled.
- Civilian audit now records Medieval Civilian 3 by Leonardo Carvalho (sketchfab.com/livrosparacriancas), Sketchfab Free Standard: commercial use allowed, credit not required, no standalone redistribution. This is the user-supplied licence confirmation, not an independent online verification.

## Files and reproduction

Runtime: Assets/Scripts/OpenWorldHuntingDiscoveries.cs, OpenWorldTrailNote.cs (new), OpenWorldImportedModel.cs and OpenWorldAnimalInteraction.cs.
Editor: Assets/Editor/ClaudeAnimalPrep.cs and HorseExtraClipsValidation.cs (new), with corresponding new metas.
Documentation: Docs/AssetAudit/DOWNLOADED_ACTORS_2026-10-29.md and ANIMAL_PREPARATION_REPORT.md, this report and DEVELOPMENT_LOG.md.
Generated outputs: own prepared animal prefabs/materials/clips under Assets/GameContent; the existing preparation command processes all 21 prepared animals, including the horse at Assets/GameContent/Imported/Prefabs/wildlife_a868c1dd18.prefab. No animation curves were hand-edited. Vendor/source FBX and Aster baked clips were untouched.

Run through ClaudeBridge: exec LostRealmsOW.ClaudeAnimalPrep.Run. Calibration is now part of the normal command. Reproducibility job 0284 completed in 51.6 seconds, prepared 21 prefabs, found all eight added clips, produced 29/96 contacts per hoof and measured bank phase offsets 0/0.

One compile-only compatibility correction was applied to GLM's Assets/Editor/GlmMusicImport.cs: use defaultSampleSettings.loadType, assigning the modified struct back before the existing reimport. GLM acknowledged retaining it; music behaviour was not changed by Codex.

## Actual verification

Final job 0286 completed 06:48:20 Asia/Dubai. Raw report: HORSE_EXTRAS_20261030_064622.txt. All functional assertions passed: hysteresis/reversal, world-kit rocks, raised note and original interaction, cache access, all added clips, shared natural speeds, wild mount, buck/control lock, all four bank selections, gallop release to stop/idle, tame remount without buck. Each bank was selected for 151/151 measured frames in its circuit.

ClaudeReviewChecks.SkateStart/SkateReport measurements (hoof horizontal speed divided by body speed):

| Circuit | Near lowest 3 cm | Broader lowest 30% height |
|---|---:|---:|
| Walk left | 6% | 7% |
| Walk right | 5% | 5% |
| Gallop left | 10% | 32% |
| Gallop right | 9% | 23% |

The requested approximately 10% target is met at near-ground stance, not across the broader gallop window. Gallop stance entry/exit still needs polish. These are automated visible-editor circuits with a temporary AlwaysAnimate fixture, not certification of every terrain, camera-culling condition or physical device.

Reviewed captures: ClaudeBridge/shots/discovery_polish_0.png and discovery_polish_1.png; seq_extra_walk_l.jpg, seq_extra_walk_r.jpg, seq_extra_gallop_l.jpg, seq_extra_gallop_r.jpg, seq_extra_buck.jpg and seq_extra_stop.jpg. Final gallop sheets were reviewed again after 0286; no visible leg-chain inversion in these frames.

Runtime compilation reported zero errors at 06:37:52; final editor compilation reported zero errors at 06:44:56. Final job cleanup restored the protected adventure snapshot, stopped Play and paired AllowAutoRefresh with its temporary DisallowAutoRefresh. Editor ended idle, userplay=False, console=ok. Known graphicsApiMask/EnvironmentDependencyValues import messages remained in console.log; they are the previously identified harmless noise.

0285 was not a gameplay pass: Windows locked the old report before checks began. The validator now creates a timestamped report per run; 0286 is the final evidence.

## Remaining work

- Improve broad gallop contact entry/exit blending; retain both slip statistics when comparing changes.
- Wider slope, transition, culling and manual riding coverage; Android build/device testing deferred as requested.
- Claude's reserved stutter files and Addressables settings remain untouched. An earlier Population invalid-operation-handle exception around return warp/stop was reported in the shared log for Claude; this task did not investigate or edit it.
- GLM's regional discoveries runtime validation remains its separate pending task.

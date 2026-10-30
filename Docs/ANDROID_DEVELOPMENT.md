# Android development baseline â€” 20 October 2026

## Product direction

Build a polished, stylized fantasy adventure for Android using the existing Aster,
realm art, weapons, enemies and audio. Keep the original Legends project read-only.
Preserve Unity 6000.6.0f1 and regenerate animations only through the existing baker.

The current deliverable is a prototype foundation, not a store release or a
completed professional open-world game. An earlier phase report covers its own
limited checks; it does not establish completion of the roadmap's 30-minute slice.

## Implemented in this pass

- Runtime uGUI adventure HUD with health/Aether meters, currency, quest counts,
  notifications and navigation; screen safe area adapts to resolution changes.
- Independent pointer ownership for movement and camera dragging. Attack distinguishes
  taps from holds. Jump, dodge, parry, spell, element and lock-on have dedicated controls.
- Input resets on death, open menus, application pause and focus loss.
  UI mouse clicks no longer also request world attacks, and touches do not synthesize mouse clicks.
- Undiscovered gates cannot be used from the map or direct fast-travel entry point.
- Removed the empty legacy `RealmGate` component from the five copied portal prefabs.
  The functioning `TeleportGateController` and all its visual/audio references remain.
- Existing audio reused for attacks, spells, movement abilities, damage, rewards and menus.
  Eight sound-effect voices bound concurrency. Forest music now loads the actual forest clip.
- Combat bursts share one material instead of allocating a new material on every hit.
- Fake ad rewards are explicitly development-only and unavailable in non-development players.
- Confirmed baseline: mid-range Android phones, with an optional High profile for stronger devices.
  Balanced uses 85% render scale, 45 m shadows, 1024 shadow map and a 30 fps cap;
  High uses 100% render scale, 75 m shadows, 2048 shadow map and a 60 fps cap.
  Both use 2x MSAA and SRP batching. The HUD graphics button switches and saves the
  selection. Profiles clone the render asset at runtime, preserving authored assets.
  Frame rates are targets, not benchmarks.

The atlas, treasure vault, arsenal and wardrobe now use scrollable uGUI menus.
The HUD, menus, joystick and combat buttons use the original Legends of the Lost
Realms UI art and fonts. A pause menu offers resume, save and graphics selection.
Physical touch sizing, readability and visual composition still require landscape
device review. See `Docs/LEGENDS_UI.md`.

## Next delivery gates, in order

The first adventure implementation now includes a staged main quest, persistent
world progress, checkpoint reloads, one-time completion rewards, trails and a
ruined sanctum landmark. See `Docs/VERDANT_ADVENTURE.md` for the exact behavior,
save contract and remaining content work.

1. **Reliable adventure loop:** versioned save/load with backup recovery, persistent
   discoveries/camps/loot/equipment, quest stages and rewards, deliberate respawn rules,
   terrain boundaries and camera collision. Validate app termination and interrupted saves.
2. **Verdant content:** authored hub routes and landmarks, readable camp approaches,
   one complete dungeon and guardian encounter, rewarding exploration and at least
   30 minutes of tested progression. Replace generic objective counts with a guided quest arc.
3. **Art and feedback:** consistent art direction using the owned packs, proper terrain
   materials and blending, dressed paths, hero/enemy silhouette readability, tuned
   lighting, restrained post-processing, pooled attack effects, telegraphs and spatial ambience.
   Review captures on target hardware before claiming visual quality.
4. **Mobile performance:** profile CPU/GPU, memory and thermal behavior; build texture,
   mesh, draw-call and particle budgets from measurements. Introduce LOD/culling and
   region streaming. Imported regional additions now load through bounded Addressables;
   the original Resources-based world still needs broader residency work. Quality tiers
   must produce measurable, device-appropriate changes.
5. **Full touch UI:** add settings for sound, sensitivity and
   graphics, scalable text, useful map, clear costs and reward explanations.
6. **Expansion:** ship additional biomes only after the Verdant recipe meets quality gates.
7. **Release:** install Android Build Support if absent, create and test Android builds,
   verify current Google Play requirements from official sources at release time, audit
   asset licenses, prepare signing/versioning, privacy/data disclosures, age rating,
   store assets and internal testing. Ads must have real completion callbacks before enabling rewards.

## Validation

Run `LostRealmsOW.AndroidFoundationValidation.Run` with Unity's batch-mode
`-executeMethod` argument (without `-quit`; the validator exits).
It opens the Verdant scene, enters play mode and writes
`Docs/android-foundation-validation.txt`. The process exits nonzero on a failed
assertion or captured runtime error.

The checks cover initialization, required audio, discovery gating, pointer ownership,
movement release, camera swipe, tap/hold/cancel attack behavior and stale input on menus.
They also verify that the Balanced and High render profiles apply their actual budgets.
Running with a graphics device captures `Docs/android-foundation-preview.png`.
These checks do not establish final rendered quality, physical multi-touch, Android
performance, or Google Play readiness. The additional adventure validator exercises
save/reload and corrupt-save recovery in the editor; forced shutdown and Android
storage behavior still require device testing.

## Device acceptance still required

- On a mid-range phone, move, look and attack simultaneously with three fingers.
- Hold attack, drag off its button and release: no attack should fire.
- Open a menu while moving; return to the game without lifting the original finger:
  movement must remain stopped until a fresh gesture.
- Background/resume during movement and combat; verify no held input carries over.
- Check both landscape orientations, cutouts and gesture navigation insets.
- Explore for 20 minutes in each graphics profile and record frame time, memory,
  temperature, battery usage and crash logs. Select the shipping baseline from evidence.
- Verify sound loudness and voice overlap through phone speakers and headphones.
- Confirm the implemented progression saves survive a forced app shutdown and relaunch.


## Imported content and animation pass — 21 October 2026

The user has an Honor Magic7 Pro for device testing. No authorized Android device was visible to adb during this pass. Keep Balanced as the default; validate High separately. This phone does not substitute for mid-range-device profiling. Current asset decisions and prepared/deferred counts are in Docs/AssetAudit.

The first native Android attempt used the default IL2CPP runtime-speed code generation and generated 450 C++ files, including 196 generic-code files, on this 8 GB development laptop. Its final graph had 532 game/runtime object outputs, including additional C/helper compilation. It failed during native output and IL2CPP source-backup copying because the local disk filled; no APK was produced. The development build entry point now selects `OptimizeSize` for subsequent iteration builds. Unity documents this as generating less code for smaller, faster builds, with a possible generic-code runtime cost: [PlayerSettings.SetIl2CppCodeGeneration](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PlayerSettings.SetIl2CppCodeGeneration.html). Validate release performance separately with explicitly selected shipping code generation; development timings alone do not establish the release budget. This setting does not alter the Balanced/High graphics profiles.

Each subsequent batch build writes a dated RUNNING result before starting, so an old failure cannot be mistaken for a new attempt's outcome. Only a successful report plus the actual APK establishes build success.

Pass `-ow-runtime-speed` to the batch build when testing runtime-speed code generation or reusing the first build's existing native cache. This is still a development APK, not a store release. Switching code-generation modes invalidates affected native outputs; keep the mode consistent across incremental comparisons.

The failed attempt began with a little over 4 GB free after platform imports. Its staging copies and native output exhausted that space despite compression. The build preflight now requires at least 6 GB local free space, with 10 GB recommended. The user freed local space before retry 3: about 20 GiB was available at validation start. The follow-up rendered validation passed with 72/75 prepared entries. Retry 3 is using the preserved runtime-speed native cache after successfully rebuilding Android content. Consult Docs/android-build-result.txt and the actual APK for the outcome; see Docs/DEVELOPMENT_CHECKPOINT.md for the current status.


# Android phone findings — 23 October 2026

The user installed a Unity-built APK on their Honor Magic7 Pro. Their screenshot shows Aster
remaining in an airborne pose, a development console reporting CollisionMeshData errors for
RPGPP fences/banners/buildings, and the user reports that the game can rotate to portrait.

## Changes made by Codex

- ProjectSettings/ProjectSettings.asset: start in LandscapeLeft and disallow both portrait modes.
- Assets/Scripts/OpenWorldManager.cs: request fixed LandscapeLeft on mobile, replacing AutoRotation.
- Assets/Editor/OpenWorldAndroidBuild.cs: enforce those orientation settings in the dedicated APK builder.
- Assets/Scripts/OpenWorldMaterialHelper.cs: do not attempt runtime MeshCollider cooking on an
  unreadable mesh. Use its local bounds for a BoxCollider instead. Readable meshes retain mesh
  collision; existing colliders are preserved. Bounds collision is conservative: building openings
  and irregular scenery may need authored collision shapes later.
- Assets/Editor/AndroidMotionValidation.cs: build a small bundle using the active build target,
  reload its player-serialized clips, and measure Aster's foot/hand movement over twelve samples.
  Run with `LostRealmsOW.AndroidMotionValidation.Run` through the existing editor bridge.
  It writes Docs/android-motion-validation.txt. Temporary bundles go to the system temp folder.

## Verification and limits

Both game and editor assemblies compiled with zero errors. The bundle round-trip passed for
idle, walk, run, fall, jump and attack_1; packed movement matched source movement for all six.
See Docs/android-motion-validation.txt. This checks clip serialization and retargeting in an
editor preview; it does not exercise the phone's live controller, full mixer, or IL2CPP player.

The frozen pose is NOT confirmed fixed. Missing motion data in those six serialized clips was
not reproduced. Next inspect the phone's live hero state, controller grounding, animator/graph
validity and any exceptions. An earlier editor session also logged a null playable in
OpenWorldCharacterVisual.Update; distinguish that earlier failure from this device report.
No phone was visible to adb during this investigation; USB-debugging access was requested.

No APK was rebuilt or installed by Codex. Available C: space was about 5 GiB, below the dedicated
builder's existing 6 GiB minimum. The installed APK therefore does not contain these changes.
The fixes still need an on-device rotation and scenery-collision check after rebuilding.

Claude's clip selection, MotionLab review and baked .anim files were left unchanged. The sibling
project was not modified. Keep all Aster RootT curves zero when continuing the Phase 1 pipeline.

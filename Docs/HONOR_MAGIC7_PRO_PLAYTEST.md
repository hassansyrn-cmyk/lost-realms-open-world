# Honor Magic7 Pro development playtest

Use the separate `com.lostrealms.openworld.dev` development APK when a successful build is available. This is an early development test; the Honor is the user's available test phone, while the shipping baseline remains mid-range Android. Do not infer mid-range performance from this phone.

## Record the run

- APK date and file size:
- Android version, display refresh setting and game graphics profile:
- Battery percentage at start/end; charging off for the measured route:
- Ambient conditions, phone case and screen brightness:
- Cold launch time, peak memory, frame-time captures and thermal observations:
- Crashes, missing art, animation problems and reproduction steps:

## Repeatable route

1. Start on Balanced. In the Sanctuary, move, rotate the camera and attack with three simultaneous fingers. Hold a charged attack, drag off the button and release. Open and close a menu while moving, then background/resume the app. Check for stuck input.
2. Talk to Lyra. Observe her feet, knees, hand gesture and return to idle. Walk and sprint past village exteriors and animals. Watch for floating models, frozen poses, collisions, abrupt animation changes, and missing or pink materials.
3. Discover an imported melee weapon. Equip it, cycle all three light attacks, use a charged attack, parry and dodge. Repeat with sword, axe/hammer, spear and unarmed styles. Record weapon intersections, sliding feet, premature damage and clipped recoveries. Some contact-timing and grip work is still outstanding.
4. Jump from safe terrain and land; test falling and gliding, including stamina exhaustion. Observe transitions back to grounded motion. Do not treat the current falling motion as a finished glider animation.
5. Follow the Verdant quest and camp route, then approach the crypt. Observe skeleton/spider encounters, hit/death reactions, elemental impacts, sound overlap and combat responsiveness. Move away and return to check regional unload/reload.
6. Inspect a cache, collect its reward and reload. Check equipment, quest and cache persistence. Force-stop only after the normal save operation, then relaunch and check again. Repeat NPC first-reward interaction to ensure it does not pay twice.
7. Visit the snowy outskirts, switch weather through the existing menu, and compare grass/snow footsteps. Check mountains, trees, particle visibility and audio through speakers and headphones.
8. Continue exploring for at least 20 minutes. Capture frame time and memory throughout, including dense village and combat views. Record streaming hitches and any sustained slowdown as the phone warms.
9. Cool the phone and repeat the same route on High. Treat 30/60 fps settings as requested caps until measurements establish sustained performance. Test landscape cutouts and gesture navigation on both orientations.

## Evidence and follow-up

Save captures and observations beside this document with the APK date. A useful animation report identifies the character, weapon, combo step, movement direction and whether the fault occurs on entry, contact or recovery. Keep launch, combat and traversal failures separate so fixes can be reproduced.

Physical device results are pending. Editor validation reports in Docs cover the automated quest/save/UI/streaming and animation checks; they do not replace this route or a later mid-range-device test.

For a connected, USB-debugging-authorized phone with the game running, run `Tools/CaptureAndroidEvidence.ps1`. It saves this game's retained logs, a memory snapshot, battery and available thermal reports under `Docs/DeviceTests/<timestamp>`. Select `-Serial` if several phones are connected. It does not install, stop, reset or alter the app or phone settings. Repeat after the sustained route and fill in the generated evidence notes. Use Unity Profiler frame-time captures alongside these snapshots; Android UI frame statistics alone are not a reliable Unity rendering benchmark.

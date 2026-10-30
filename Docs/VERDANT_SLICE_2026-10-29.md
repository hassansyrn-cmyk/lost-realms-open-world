# Verdant combat and adventure slice — 29 October 2026

## Play the quest

Speak to Warden Kael in Oakvale to begin **A Hunter's Promise**. Follow and inspect two sets of tracks outside the village, defeat **Thornhide, Terror of Oakvale**, prepare a meal at Oakvale's campfire, and return to Kael.

- First tracks: approximately (63, 33).
- Second tracks: approximately (90, 44).
- Woodland clearing: approximately (118, 64); the bear searches a nearby clear site rather than spawning inside scenery.
- Reward, once: 120 gold, 5 gems and Ranger Axe ownership (weapon ID 4). Existing enemy drops and first-time cooking rewards remain separate.
- The existing quest tracker and minimap follow this quest while it is active, then resume the main adventure. Cooking has priority over nearby NPC conversation during this quest's meal step.
- Progress uses saved `CollectedWorldIds` keys with the `verdant-hunt:` prefix. Reloads resume the appropriate step; completed quarry/reward flags prevent repeat rewards. No save schema migration is needed.

Thornhide uses the prepared bear at `ow.imported.expansion_bear`. A matching Addressables build is required on Android. The quest adapter owns and releases its load handle; a missing bundle reports a warning and retries instead of substituting a different creature. The encounter streams out at distance and respawns unfinished encounters when approached again.

## Combat changes

- Ordinary melee uses a committed forward arc, so moving behind the attacker can avoid it.
- Warnings stay at the committed impact location. Beast strikes no longer retarget the hero at the end of the warning.
- Lunges damage after their movement, rather than immediately at the projected endpoint. A blocked lunge cannot damage a distant victim through scenery.
- Static cover is checked before beginning an attack and again when resolving damage.
- Staggers, stuns and parries cancel pending impacts and free attack tokens. Existing dodge invulnerability and recovery windows remain in effect.
- Attack identities: wolf/cat Fang Leap; rhino/ravager Horn Rush; spider Venom Pounce; bear Mauling Sweep; caster Arcane Eruption; brute Earthshatter. These adapt existing prepared clips/effects and mechanics; poison and other new player status systems were not added.
- Elite casters can start their marked burst from up to eight metres away. Special recovery remains longer than ordinary recovery, providing a punish window.

## Presentation changes

- Generic weapon grips use the model's longest axis, instead of assuming every mesh points up its Y axis. Existing explicit asset corrections remain.
- The hand socket uses the palm landmark where humanoid finger bones are available, with wrist fallback for unsupported rigs.
- Back carry follows the hip/head torso axis. Swimming and climbing keep weapons stowed; the sword therefore follows the inclined body rather than using a fixed upright-world offset.
- Camera obstruction casts account for the near-plane size and aspect ratio. The old minimum camera offset no longer forces the lens past a very close obstruction.
- Tracking uses the existing HUD rather than adding a card over the loot feed or touch controls. Track clues have ground marks and proximity labels; the quarry uses existing health/attack warning UI.

## Checks actually run

Unity runtime/editor compilation: zero errors in the final compile at 21:19:20. The user-approved single-line GetInstanceID compatibility fix in Antigravity's ImportedModel file was required before live verification; no other horse implementation was changed by Codex.

Live bridge checks covered:

- NPC acceptance and normal Interact handlers at both tracks, cooking and reward.
- Prepared bear loading/spawning, real quarry defeat and meat drop.
- Wrong campfire rejection, cooking cost, reward exactly once.
- Quest/reward disk save/load in an isolated Temp directory, without touching the user's adventure slots.
- Forward melee, behind-the-attacker miss, wall occlusion, fixed AoE mark, delayed lunge impact, stun cancellation, parry and dodge invulnerability.
- All 108 weapon/skin combinations loading with finite plausible world bounds. This is a loading/scale smoke check, not proof of every grip throughout every animation.
- Close-up Dueling Blade poses on all six skins; reviewed after palm refinement on original, plate and assassin skins.
- A live near-wall camera resolver fixture.
- A 24-frame live swimming capture; telemetry confirmed Swim mode at approximately 2.55 m water depth and 62-degree torso tilt. The weapon stayed stowed along the torso.

Evidence: `Docs/verdant-slice-validation.txt`; bridge jobs 0066–0072; `ClaudeBridge/shots/verdant_*.png`, `verdant_grip_skin*.jpg`, `verdant_palm_skin*.jpg` and `seq_verdant_stowed_swim.jpg`.

Test tours disabled adventure saving and restored the prior snapshot, clock and camera distance. The 'Save unavailable' line in those QA screenshots is caused by that test safeguard, not a diagnosed storage fault.

## Remaining work

Physical Android controls/performance have not been tested in this pass. Individual exotic-weapon rolls, finger closure, every combat clip's impact timing, dense foliage without colliders, extreme geometry, extended encounter balance and player-led pacing still need broader QA. This is a playable development slice, not a claim of completed AAA polish. No new terrain bake, vendor edit, multiplayer work, APK build or Asset Store import was performed.

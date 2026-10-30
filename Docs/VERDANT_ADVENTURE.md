# Verdant adventure implementation

## Direction

The requested references are Genshin Impact, Wuthering Waves, Zelda and The Witcher.
For Lost Realms, translate that ambition into discoverable landmarks, responsive
third-person combat, exploration rewards, memorable regional encounters and quests
that change the world. Build and evaluate those qualities within the confirmed
mid-range Android baseline. This implementation does not establish parity with
the scale, content or presentation of those productions.

## Playable quest: A Whisper in the Roots

1. Approach the sanctuary stone in the hub and choose **Listen** (keyboard: F).
   The sanctuary asks Aster to recover the forest's three lost sigils.
2. Follow the trails to the hostile outposts and ruined sanctum.
   Defeat all guards at the three outposts and recover the three temple keys,
   presented as sigils in the quest. Objectives may be completed in any order.
3. Approach the grove stone north of the hub and choose **Open Grove**.
   All three sigils and all three outposts are required. This awakens Heartwood,
   restores health/Aether and establishes the grove as the respawn checkpoint.
4. Defeat Heartwood and return to the sanctuary.
5. Choose **Claim Reward** for the Ornate Curved Blade, +1 maximum health,
   150 gold and 5 gems. Repeated interaction restores health, with no repeated reward.

The HUD shows the current quest step, nearest relevant objective's world compass
bearing and distance, and a contextual interaction button. Keyboard F invokes the
same proximity-checked action. The grove gate can be discovered before the quest
is ready, but discovering it does not awaken or defeat the guardian.

## World additions

- Five trails connect the hub to the three outposts, ruined sanctum and guardian
  grove. A procedural soil texture blends into the terrain's moss layer with
  irregular, softened edges. Nature scattering keeps the routes clear, and stone
  waymarker altars mark the main trail approaches.
- Two tranquil Sanctuary Ponds (in the eastern hub glade and near the guardian grove
  approach) add water landmarks with spatial reservation clearance.
- **Forgotten Sanctum dungeon encounter (`OpenWorldSanctum.cs`):** flattened terrain
  courtyard at `(-60, 0, -58)` featuring a `CryptArch` entrance, `CryptMemoryAltar`,
  dedicated `Forgotten Sanctum Gate` fast-travel waypoint, two Sanctum Sentinels
  (`Sanctum Acolyte` & `Sanctum Warden`), two active elemental hazard traps
  (`Trap_Brazier` & `Trap_Totem`), and three Elemental Focus Pillars (`EmberFocus`,
  `FrostFocus`, `GaleFocus`). Attuning all three pillars (by proximity interaction or
  casting spells near them) and awakening the Memory Altar disarms the sanctum traps,
  grants a permanent **+25 Max Aether** (`MaxAether = 125`), unlocks the **Frost Halberd**,
  awards **100 gold and 4 gems**, and persists across save/load.
- The grove's stone seal disappears when the guardian is awakened.
- Soft perimeter clamping (`±212m`) prevents Aster from walking off the terrain edge.
- World-space enemy status/lock-on labels and Heartwood's Phase 2+ radial Root Quake
  slam telegraph improve combat readability.

The third-person camera now sweeps a small collision volume between Aster and
the desired viewpoint. It pulls inward immediately at solid obstructions and
eases back out, ignoring the hero and other character controllers.

## Persistence contract

The game autosaves changed progress after a one-second debounce and flushes on
application pause/quit. Sanctuary and grove interactions save immediately.
Snapshots include currency, opened caches, collected static pickups, captured
outposts, gate discoveries, equipped/unlocked weapons and skins, active element,
quest milestones and last used checkpoint.

On relaunch, Aster returns to the last used sanctuary/gate at full health and
Aether. Captured camps stay cleared, collected world treasures stay removed and
a defeated guardian stays defeated. An unfinished guardian fight resets on reload.
Individual enemies in an uncleared camp, transient enemy drops and current combat
health are not persistent. Static potions are only consumed when they can restore
the matching resource.

Save files live in `Application.persistentDataPath/Adventure`. Two alternating
JSON snapshots include a format/version, revision and payload checksum. A failed
or interrupted write leaves the other snapshot available. An unreadable pair
or a newer unsupported format is preserved; automatic saves are blocked for that
session rather than overwriting it. A persistent HUD notice makes unsaved progress
visible. Checksums detect corruption, not cheating.

## Validation

`LostRealmsOW.AndroidFoundationValidation.Run` now exercises the real generated
world through the entire quest, repeated rewards, save/rebuild and disk recovery.
It uses unique temporary save directories, not the developer's normal progress.
Evidence is written to `Docs/adventure-validation.txt` and the existing foundation
report. Rendering captures remain editor previews; Android installation,
physical touch behavior, frame pacing and thermal performance need device testing.

## Next production work

- Expand traversal and playtest the camera collision pass, then tune combat timing,
  animation transitions and attack readability through playtesting.
- Replace flat terrain presentation with authored composition, material blending,
  region landmarks, water and appropriate mobile lighting.
- Expand the sanctum into a complete dungeon with encounters, an exploration
  mechanic and a distinctive reward.
- Replace remaining IMGUI inventory/map screens and introduce an actual region map.
- Profile an Android build before increasing region size or asset density.

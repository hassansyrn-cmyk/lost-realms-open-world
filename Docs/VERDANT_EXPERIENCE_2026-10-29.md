# Verdant combat, discoveries and quest journal — 29 October 2026

## Player flow

- Open **JOURNAL** at the upper right, or press **L**. Select a quest and choose **TRACK QUEST**. The screen objective, minimap goal and atlas goal follow that selection. The main story remains selectable while side quests are active. Completed and unstarted quests can be reviewed; unstarted quests lead to their giver rather than being remotely accepted.
- The **DIALOGUE** tab keeps the most recent 32 conversations/field notes, newest first; drag or scroll to read earlier entries. Tracking and dialogue belong to the current adventure and use its existing disk save. The journal pauses gameplay using the existing pause gate; Close/Escape restores play.
- Along Kael's hunting trail, investigate **Eren's fallen marker** near (72,27), follow its clue to a **hidden supply cache** near (81,35), and optionally investigate a **supply-thief camp** near (98,26). Coordinates are X,Z anchors; each site searches locally for dry, level, clear ground with shorter grass. No terrain or grass asset is edited. Approach the object and use the contextual touch button or **F**.
- The note explains how Thornhide scattered the hunters, where Eren hid the spare supplies, and why the southern spur is unsafe. The cache grants 35 gold / 2 gems once. Investigating the camp starts the optional Briarfoot Supply Thief fight; clearing it grants 40 gold / 1 gem once, in addition to ordinary enemy drops. Leaving the local streaming area abandons an uncleared encounter. Neither detour blocks A Hunter's Promise.

## Combat changes

Animation impact frames remain authored by the existing baker. The first Play-mode baseline measured sword/axe/spear impacts at approximately 0.279/0.319/0.235 seconds. Normal sword/spear movement recovery is 80 ms shorter after the impact; axes shorten by 20 ms, while finishers retain their weight. Early presses remain buffered through long windups, with one queued attack and a bounded expiry.

Confirmed contacts add short, capped camera pulses and hero hit-stop; a charged whiff no longer gets the finisher's impact shake. Smooth shake decay uses the actual pulse duration and caps stacked requests at 0.22 intensity / 0.35 seconds. Solid cover blocks melee and elemental strike damage; light melee also requires a forward arc beyond point-blank range.

Impacts have a small pitch range and stronger weight on heavy hits. The existing eight audio voices remain bounded, with higher-priority impacts/hurt allowed to replace lower-priority swishes and incidental sounds. Normal playback resets pitch, so movement/menu sounds cannot inherit a combat pitch. No global time-scale hit-stop or new audio assets.

Common enemies telegraph for 0.72 s (previously 0.6), with a 1.05 s normal recovery. Light hits stagger briefly and grant a 1.25 s interval before another full light stagger, letting an enemy retaliate instead of being reset indefinitely. Heavy attacks and parries retain their interruptions. Boss timing/health and player health remain unchanged. This is a tuning pass from controlled Play-mode encounters, not a claim that broad human difficulty testing is complete.

## Integration and ownership

New runtime files: `OpenWorldCombatFeel`, `OpenWorldHuntingDiscoveries`, `OpenWorldQuestJournal`, `OpenWorldQuestJournalHud`, `OpenWorldPerformanceProbe`. Combat-only edits in Hero/Enemies/Camera/Audio. `OpenWorldVerdantHuntHud` yields tracking to the journal. GLM's Adventure/Villages/QuestBoard and region-quest files were not edited. Manager has exactly one additional line in ShowBanner to capture dialogue synchronously; its Phase 2 claim had been released. Its public quest state is read for journal entries. Horse work, vendor packs, Photon and baked animation files remain outside this pass.

Saved IDs:

- `hunt-discovery:note`, `hunt-discovery:cache`, `hunt-discovery:camp`.
- `journal:track:<id>` where id is main / hunt / sled / braziers / lanterns / rods.
- Up to 32 `journal:line:<8-digit sequence>:<base64 UTF-8 displayed dialogue>` entries. These are additive flags supported by the existing adventure save; no save-schema change.

The transcript records named NPC dialogue synchronously when ShowBanner is called, with a fallback observer and direct field-note recording. New weather notifications can no longer replace a conversation before it is recorded. It cannot reconstruct conversations spoken before this feature existed.

## Verification and performance

`Assets/Editor/VerdantExperienceValidation.cs` drives protected tests. It snapshots/restores the adventure and blocks writes to real save slots; the SAVE UNAVAILABLE label in QA screenshots reflects that guard. Results: `Docs/Performance/Codex_combat.txt`. Journal screenshots: `ClaudeBridge/shots/codexpass_journal_quests.png` and `codexpass_journal_dialogue.png`.

Longer performance runs use `OpenWorldPerformanceProbe`, explicitly started by the QA helper; it does not run or write reports during normal play. Each graphics tier samples 30 seconds at settled Oakvale, 30 seconds of actual touch-input hunt-route traversal with repeated attacks, and 30 seconds near a streamed horse group, after five-second settling periods. Fixed sample arrays and once-per-second memory queries limit the probe's overhead. Reports: `Docs/Performance/Codex_profile_balanced.md` and `Codex_profile_high.md`.

Frame caps remain in place. These are desktop **Editor** wall-clock frame times and Unity/Mono allocator readings, including Editor overhead and pacing waits; main-thread readings are not pure game CPU work. They do not establish phone FPS, VRAM residency or a mobile memory budget. Phone heat and sustained thermal throttling were **not tested in this Editor session**. The user postponed Android builds; no APK, device temperature or Android performance pass is included. Compare against GLM's short desktop baseline for context, not a controlled before/after benchmark.

Desktop GPU temperature snapshots were subsequently read through NVIDIA's local driver utility (68 C in Editor Play, 64 C at a later idle snapshot). See `Docs/Performance/Codex_PERFORMANCE_HANDOFF_2026-10-29.md` for the timestamps and scope. These do not replace a phone thermal test. The same handoff summarises all eight 30-second sample windows, memory readings and the identified stalls.


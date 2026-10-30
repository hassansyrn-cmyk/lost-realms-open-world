# Character animation upgrade

User priority: improve animation for every character, using the imported motion packs. Target responsive, readable fantasy action on mid-range Android; quality must be judged in motion on the actual rigs.

## Implemented first pass

- Aster Phase 1 now accepts verified humanoid Asset Store clips through its existing root-normalizing baker. Every RootT curve remains zero; generated animation assets retain their GUIDs on rebake and discard vendor callback events.
- Selected sprint from Everyday Motion, punches/kick from Fighting Motions, standing conversation and polearm attack from Kevin Iglesias, falling from ExplosiveLLC. Polearm currently has one authored attack reused across the three combo slots; further distinct polearm moves remain needed.
- Hero and existing humanoid NPC skins share the validated Aster animation path. Conversation uses a talk state rather than a spell-cast gesture.
- Locomotion blends idle/walk/run from measured travel speed and adjusts cadence. Hits/attacks have faster transitions. Falling has a separate state.
- Imported actors retain their own rigs and compatible clips; humanoids can use retargeted store motions with their own root-height handling, with action restarts, non-looping legacy attacks/deaths and lifecycle-managed Playables. An actor without verified idle binding is deferred, not silently assigned another species' clip.
- Footstep sounds follow distance traveled; snow uses the Frost region sound set. Bird calls are spatial and distance limited when a compatible bird is prepared.

## Remaining quality gates

1. Rendered review of every hero skin, enemy family and selected animal: hands, feet, knees, weapon grip, ground contact, loop seams, transition pops and silhouette.
2. Frame-specific melee contacts and recovery: synchronize attack timing, trails and hit detection to authored motion rather than guessing from clip length. Expand polearm variants; verify hammer/axe/staff holds separately.
3. Landing, start/stop, lock-on strafing and turn-in-place need dedicated motion selection and integration. Imported packs contain candidates; do not substitute unrelated combat clips without evaluating them.
4. Glider, climbing/swimming and other traversal require dedicated authored animation where those mechanics are supported. A falling clip is not a finished glider pose.
5. NPC role actions (smithing, farming, mining, seated interaction) need matching props, interaction points and schedules before playing those clips.
6. Reduce animation evaluation for distant NPCs and enemies; profile skinning, bone counts and avatar memory on Android. No claim of professional final animation quality or measured phone performance yet.

### Follow-up found during Android packaging

The prepared DungeonCharacters demo skeleton (`enemy_86bd5dc811`) has idle/walk/attack only: its supplied demo library has no hit or death motion. Preparation now requires every combat enemy to have compatible walk, attack, hit and death clips as well as idle, and the 21 October rendered-validation preparation correctly deferred that partial rig. The Wolf_Animated library has a hit clip named `Wolf_damege.FBX`; the reaction selector now accepts this vendor spelling. The follow-up preparation and rendered validation passed: 72/75 entries accepted, with the curated main skeleton, wolf and spider all retaining complete event-free combat clip references. Android retry 3 is rebuilding content with this fix. These checks verify clip availability and basic playback requirements; contact timing and visual quality still need motion review.

See ANIMATION_PREPARATION.md for actual baked/deferred states and PREPARATION_REPORT.md for actor acceptance. The original sibling project and Aster generated .anim files are never hand-edited.


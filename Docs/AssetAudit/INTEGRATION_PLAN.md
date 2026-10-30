# Imported asset integration plan

Mid-range Android is the baseline. The original Legends project remains read-only. Prepared copies retain source GUIDs and materials per slot; gameplay adapters replace demo controllers. This is the first integration pass, not a claim that every candidate has passed visual or device testing.

## Regional use

- Sanctuary and Verdant: village exteriors, windmill, forest vegetation, roaming animals, Lyra appearance, discoverable melee weapons and caches.
- Crypt: skeleton/spider encounters, cemetery and dungeon dressing, fire effects. The imported cave troll remains deferred because its animation bindings do not match.
- Frost and outer terrain: snowy mountain silhouettes, conifers, snow footsteps; existing rain/snow weather remains under the game atmosphere controller.
- Combat: elemental impact effects with bounded pools; imported attack/impact/footstep sounds use the existing audio routing.

## Residency and verification

### World polish expansion — 21 October

The selection now contains 106 entries from 38 folders; preparation accepts 103. The rhino, cave troll and incomplete demo skeleton remain deferred. This adds 31 entries to the first selection, not 31 extra objects kept permanently resident.

- Polytope orchard trees, logging props, fence sections, shrubs, flowers, mushrooms and rocks dress the village edge and woodland. Four Symphonie URP oaks extend the forest selection.
- Polytope peasant models replace Brom and Rowan through the existing NPC interaction and animation adapters; Lyra retains the elf model.
- Free Slash VFX supplies neutral/fire/water slashes. Hovl dust and sparks join the elemental impacts. Existing slash/burst calls select the imported effects once loaded; the generated visuals remain a loading fallback. Combat effects retain orientation, bounded lifetime and a four-instance concurrent limit.
- Three SimpleSky clouds sit above the terrain with no colliders or shadow casting. The existing time/weather controller remains responsible for sky lighting.
- Idyllic grass/dirt/rock layers and Symphonie snow form a four-layer terrain pass. Paths connect the hub, southern village and northern route; slopes and northern snow blend by terrain position. Source terrain layers remain untouched.
- Buildings receive solid stone-textured foundations sampled across their footprint. Wildlife avoids terrain boundaries, steep slopes and excessive steps, and idles when blocked. Prepared scenery removes demo rigidbodies/joints.

These changes require rendered review of the assembled areas, including density, sightlines, foundation heights, weapon effects and NPC poses. The latest automated evidence is Docs/world-polish-validation.txt; its timestamp distinguishes the initial effects-only check from the later terrain/NPC/building check. This is not a full review of all 2,640 source prefabs or a phone performance claim.

Permanent dependencies: equipped/discoverable weapon models, adopted NPC model, combat impact sources, audio. Regional additions: nearest 28 on Balanced or 48 on High, additionally capped at 350k/650k conservative mesh triangles. These are addition budgets, not measured whole-game performance. At most two asynchronous loads and four active imported combat effects. Texture memory and draw calls remain device profiling gates.

Actor clips require matching generic binding paths or a valid humanoid avatar; event-free copies prevent callbacks into stripped demo scripts. Unsupported rigs/deformation are deferred. Existing source art stays in place. Aster motion updates go through Phase 1 with every RootT curve held at zero.

Building boxes are exterior collision fallbacks; interiors require authored colliders and traversal review. Bow, arrow and shield art needs supported gameplay before equipping. Duplicate packs and alternate art styles remain a source library rather than cluttering the world.

## Every imported folder

### _TerrainAutoUpgrade

Supporting library: textures, materials, terrain layers, shaders or tooling. Adopt through compatible prepared model dependencies; no standalone spawn.
Source: 6 files, 0.0 MiB; roles: weather, environment.

### A_piece_of_nature

Selected for preparation: nature_99e4daba35, nature_1c1a55904c, nature_c02a70933c, chest_d1f2603511. See PREPARATION_REPORT.md for actual acceptance.
Source: 29 files, 11.8 MiB; roles: environment.

### Ayo Free Toon Water

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 19 files, 1.6 MiB; roles: environment.

### B&J Studio

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 24 files, 0.4 MiB; roles: other.

### Blink

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 373 files, 436.8 MiB; roles: other.

### BrokenVector

Selected for preparation: building_e8ccafa6ca, building_2fa3ec357c, nature_bc6ecdd561, nature_9e548c3cd8, nature_6d07816f72, nature_a5acedb7be, prop_b0265ed606. See PREPARATION_REPORT.md for actual acceptance.
Source: 245 files, 21.8 MiB; roles: other.

### BTM_Assets

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 113 files, 2.4 MiB; roles: other.

### Cartoon low poly tree

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 29 files, 2.8 MiB; roles: environment.

### cave_troll

Selected for preparation: enemy_5402163816. See PREPARATION_REPORT.md for actual acceptance.
Source: 23 files, 208.5 MiB; roles: enemies.

### CemeteryPack

Selected for preparation: prop_f1871959ce, prop_feff225869, prop_5b032d0023. See PREPARATION_REPORT.md for actual acceptance.
Source: 294 files, 309.6 MiB; roles: props.

### Character_Elf

Selected for preparation: npc_5b95ae1b57. See PREPARATION_REPORT.md for actual acceptance.
Source: 11 files, 43.1 MiB; roles: characters.

### com

Supporting library: textures, materials, terrain layers, shaders or tooling. Adopt through compatible prepared model dependencies; no standalone spawn.
Source: 139 files, 23.7 MiB; roles: other.

### Daniel Mistage

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 650 files, 85.7 MiB; roles: other.

### Darth_Artisan

Selected for preparation: nature_145a6114fe, nature_bc3f425c95. See PREPARATION_REPORT.md for actual acceptance.
Source: 13 files, 0.3 MiB; roles: other.

### Deadly Kombat Free version

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 50 files, 24.6 MiB; roles: other.

### DoubleL

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 284 files, 206.0 MiB; roles: other.

### Dungeon Floor Traps

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 30 files, 30.5 MiB; roles: props.

### DungeonCharacters

Selected for preparation: enemy_86bd5dc811. See PREPARATION_REPORT.md for actual acceptance.
Source: 13 files, 1.7 MiB; roles: characters, props.

### Elven Weapons

Selected for preparation: elf_sword, elf_axe, elf_spear, elf_hammer, elf_staff. See PREPARATION_REPORT.md for actual acceptance.
Source: 33 files, 7.2 MiB; roles: weapons.

### Epic Game Hits SFX

Selected for preparation: audio_sfx_impact. See PREPARATION_REPORT.md for actual acceptance.
Source: 76 files, 9.5 MiB; roles: audio.

### Eric VFX Studio

Reserve effects/weather variants. Check URP rendering, scripts, overdraw and particle counts before adding to bounded combat/weather routing.
Source: 168 files, 24.1 MiB; roles: effects.

### EverydayMotionPackFree

Reserve animation library. Bind only to a verified matching rig/avatar; do not replace Aster baked clips.
Source: 39 files, 10.9 MiB; roles: animation.

### ExplosiveLLC

Reserve effects/weather variants. Check URP rendering, scripts, overdraw and particle counts before adding to bounded combat/weather routing.
Source: 375 files, 93.2 MiB; roles: effects.

### Fantasy Chests PBR

Selected for preparation: prop_6f2814d7fc. See PREPARATION_REPORT.md for actual acceptance.
Source: 14 files, 32.3 MiB; roles: props.

### FantasyEnvironments

Selected for preparation: building_53e44be359, building_2ad730e933, building_c97df2f304. See PREPARATION_REPORT.md for actual acceptance.
Source: 393 files, 216.3 MiB; roles: other.

### fantasySpider

Selected for preparation: enemy_d5e3575646. See PREPARATION_REPORT.md for actual acceptance.
Source: 13 files, 10.7 MiB; roles: enemies.

### FightingMotionsVolume1

Reserve animation library. Bind only to a verified matching rig/avatar; do not replace Aster baked clips.
Source: 53 files, 13.3 MiB; roles: animation.

### Footstep(Snow and Grass)

Selected for preparation: audio_sfx_step, audio_sfx_step_snow. See PREPARATION_REPORT.md for actual acceptance.
Source: 90 files, 41.3 MiB; roles: weather, environment, audio.

### Footsteps - Essentials

Selected for preparation: audio_sfx_step2. See PREPARATION_REPORT.md for actual acceptance.
Source: 479 files, 35.1 MiB; roles: audio.

### FourEvilDragonsHP

Reserve actors. Requires rig/animation validation and a deliberate NPC, wildlife or combat role before additional encounters.
Source: 133 files, 151.3 MiB; roles: enemies.

### Fox

Selected for preparation: wildlife_fe036fe79f. See PREPARATION_REPORT.md for actual acceptance.
Source: 38 files, 28.7 MiB; roles: animals.

### Free medieval weapons

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 42 files, 66.8 MiB; roles: weapons.

### Free Simple Dungeon Props - YP

Selected for preparation: prop_741704a930, prop_bc88208c4b, prop_85755b7969, prop_1b29d47f3a. See PREPARATION_REPORT.md for actual acceptance.
Source: 17 files, 1.0 MiB; roles: props.

### Free Slash VFX

Reserve effects/weather variants. Check URP rendering, scripts, overdraw and particle counts before adding to bounded combat/weather routing.
Source: 145 files, 30.9 MiB; roles: effects.

### Free_Fireworks__44.1k_16bit

Reserve effects/weather variants. Check URP rendering, scripts, overdraw and particle counts before adding to bounded combat/weather routing.
Source: 10 files, 3.8 MiB; roles: effects.

### GabrielAguiarProductions

Supporting library: textures, materials, terrain layers, shaders or tooling. Adopt through compatible prepared model dependencies; no standalone spawn.
Source: 21 files, 25.4 MiB; roles: other.

### Horse Statue

Reserve actors. Requires rig/animation validation and a deliberate NPC, wildlife or combat role before additional encounters.
Source: 12 files, 43.3 MiB; roles: animals.

### Hovl Studio

Selected for preparation: vfx_919460f982, vfx_4673b01fd4, vfx_5d7fd18b4d. See PREPARATION_REPORT.md for actual acceptance.
Source: 230 files, 110.7 MiB; roles: other.

### Idyllic Fantasy Nature

Selected for preparation: nature_39c81322b9, nature_94f5f62232, nature_e8beaee972. See PREPARATION_REPORT.md for actual acceptance.
Source: 432 files, 269.2 MiB; roles: environment.

### Inguz Media Studio

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 186 files, 125.1 MiB; roles: other.

### InnerverseInteractive

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 97 files, 298.2 MiB; roles: other.

### ithappy

Selected for preparation: wildlife_5c1241df03, wildlife_83c3ea98a8, wildlife_439813ed98, wildlife_a868c1dd18. See PREPARATION_REPORT.md for actual acceptance.
Source: 63 files, 19.7 MiB; roles: other.

### Kevin Iglesias

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 536 files, 261.4 MiB; roles: other.

### living birds

Selected for preparation: wildlife_3522f8e030, wildlife_14fe7e3329, audio_sfx_bird. See PREPARATION_REPORT.md for actual acceptance.
Source: 96 files, 42.6 MiB; roles: animals.

### Low Poly Fire Particles

Selected for preparation: vfx_378d89d774. See PREPARATION_REPORT.md for actual acceptance.
Source: 23 files, 1.0 MiB; roles: effects.

### LowpolyBakersHouse

Selected for preparation: building_ebc5d887bc, prop_ab32fa6b20. See PREPARATION_REPORT.md for actual acceptance.
Source: 11 files, 9.1 MiB; roles: buildings.

### Magic Pig Games (Infinity PBR)

Selected for preparation: vfx_ebb557170d. See PREPARATION_REPORT.md for actual acceptance.
Source: 858 files, 282.0 MiB; roles: effects.

### Mountain Terrain rocks and tree

Selected for preparation: nature_1e4a1f94b0, nature_d8050af450, mountain_3fe2db3f8d. See PREPARATION_REPORT.md for actual acceptance.
Source: 40 files, 56.4 MiB; roles: weather, environment.

### MS_WeaponVol01

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 45 files, 10.0 MiB; roles: weapons.

### MYFG-Weapon Pack Lite

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 81 files, 3.3 MiB; roles: weapons.

### Polygrade

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 13 files, 20.8 MiB; roles: other.

### Polylised - Medieval Desert City

Selected for preparation: building_69b55d175c, building_0b2c089ac1, building_0d8d837660. See PREPARATION_REPORT.md for actual acceptance.
Source: 690 files, 38.4 MiB; roles: buildings.

### PolyRonin

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 20 files, 1.0 MiB; roles: other.

### Polytope Studio

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 256 files, 120.9 MiB; roles: other.

### PurePoly

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 59 files, 2.5 MiB; roles: other.

### Rabbits

Selected for preparation: wildlife_10f9a82436. See PREPARATION_REPORT.md for actual acceptance.
Source: 13 files, 1.4 MiB; roles: animals.

### RedDragon 1.2

Reserve actors. Requires rig/animation validation and a deliberate NPC, wildlife or combat role before additional encounters.
Source: 15 files, 92.3 MiB; roles: enemies.

### Rhino

Selected for preparation: wildlife_39b91bb111. See PREPARATION_REPORT.md for actual acceptance.
Source: 19 files, 95.9 MiB; roles: animals.

### Rhino_Jahaziel

Reserve actors. Requires rig/animation validation and a deliberate NPC, wildlife or combat role before additional encounters.
Source: 10 files, 3.8 MiB; roles: animals.

### RPGPP_LT

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 190 files, 4.5 MiB; roles: other.

### SazenGames

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 51 files, 62.3 MiB; roles: other.

### Season2_Jackpot packV1.5

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 20 files, 50.8 MiB; roles: other.

### SFX Packs

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 32 files, 15.6 MiB; roles: audio.

### Silver_Cats

Selected for preparation: nature_ce8ae0652b, nature_803d2346f0, nature_6e0f5ff82f. See PREPARATION_REPORT.md for actual acceptance.
Source: 65 files, 237.0 MiB; roles: other.

### SimpleNaturePack

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 57 files, 2.7 MiB; roles: environment.

### SimpleSky

Reserve effects/weather variants. Check URP rendering, scripts, overdraw and particle counts before adding to bounded combat/weather routing.
Source: 19 files, 0.3 MiB; roles: weather.

### SiuniaevCharacters

Reserve actors. Requires rig/animation validation and a deliberate NPC, wildlife or combat role before additional encounters.
Source: 9 files, 6.9 MiB; roles: characters.

### Skeleton

Selected for preparation: enemy_949fff1745. See PREPARATION_REPORT.md for actual acceptance.
Source: 5 files, 9.0 MiB; roles: enemies.

### slash5-HungNguyen

Reserve effects/weather variants. Check URP rendering, scripts, overdraw and particle counts before adding to bounded combat/weather routing.
Source: 23 files, 3.2 MiB; roles: effects.

### Snow Mountain

Selected for preparation: mountain_c419d664bf. See PREPARATION_REPORT.md for actual acceptance.
Source: 11 files, 114.2 MiB; roles: weather, environment.

### Standard Assets

Supporting library: textures, materials, terrain layers, shaders or tooling. Adopt through compatible prepared model dependencies; no standalone spawn.
Source: 2 files, 4.8 MiB; roles: other.

### Starter Assets

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 152 files, 91.0 MiB; roles: other.

### SwordSoundPack

Selected for preparation: audio_sfx_attack_slash_1, audio_sfx_attack_slash_2, audio_sfx_attack_slash_3. See PREPARATION_REPORT.md for actual acceptance.
Source: 28 files, 7.4 MiB; roles: weapons, audio.

### Symphonie

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 64 files, 55.2 MiB; roles: other.

### The Asset Wizard

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 33 files, 23.8 MiB; roles: other.

### TreasureChestMaker

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 76 files, 8.6 MiB; roles: props.

### Turntable_platforms

Reserve regional dressing/variants. Select coherent materials, collision, LOD and placement after the first regional pass is visually verified.
Source: 55 files, 3.9 MiB; roles: other.

### Vefects

Selected for preparation: vfx_edeef669da. See PREPARATION_REPORT.md for actual acceptance.
Source: 96 files, 51.1 MiB; roles: other.

### VillagePack

Selected for preparation: building_e8c895664f, prop_df3d2463d2. See PREPARATION_REPORT.md for actual acceptance.
Source: 122 files, 9.6 MiB; roles: buildings.

### Wolf_Animated

Selected for preparation: enemy_10b1fc0ab2. See PREPARATION_REPORT.md for actual acceptance.
Source: 19 files, 21.2 MiB; roles: animals.

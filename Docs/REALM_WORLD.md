# The 1 km Realm — how it is built and why

The open world is a 1024 m × 1024 m realm baked from curated store assets. The Verdant core
(hub, quests, POIs) keeps its coordinates; the Frozen Peaks lie north, the Burning Dunes east, the
Hollow Marches (graveyard) south-west and the Stormveil cliffs south-east, ringed by mountains.

Vendor packs are never edited. Everything the game uses is a prepared copy or a reference.

## Pipeline

| Step | Entry point | Output |
|---|---|---|
| Curate | `Docs/AssetAudit/world-kit.json` (478 rows) | role, biomes, tags, tint, surface coverage, rotation fix, height, sink, slope alignment |
| Prepare kit | `OpenWorldKitBuilder.Build` | URP-correct, pivot-at-base prefabs in `Resources/WorldKit`, manifest `Resources/World/WorldKit.json`, converted materials in `GameContent/WorldKit/Materials`, terrain layers in `Resources/World/Terrain` |
| Grade ground | `Tools/worldkit/grade_terrain.py` | colour-graded albedos in `GameContent/Terrain/Textures` |
| Sound bank | `OpenWorldSoundBankBuilder.Build` | `Resources/World/SoundBank.asset` (footsteps per surface, birds, crows, fire loops, magic) |
| Bake villagers | `OpenWorldNpcBaker.Bake` | palette-baked Polytope prefabs and `Resources/World/Villagers` variants |
| Bake realm | `OpenWorldRealmBaker.Bake` | `Resources/World/Realm`: terrain (heights, 8-layer splat, trees, detail grass), placements, road/water fields, map atlas, terrain/water/ice materials |
| Runtime | `OpenWorldRealm.Build` + atmosphere, VFX, soundscape, population | streamed world around the hero |

Menu equivalents live under **Lost Realms OW** in the editor. `ClaudeBridge/inbox/*.txt` scripts drive
the same steps plus play-mode screenshot tours (`ClaudeBridge/shots`).

## Look decisions

- **Ground textures.** Idyllic's ground textures ship pre-tint (pure yellow grass, orange dirt, grey
  rock) for its own shaders. `grade_terrain.py` gradient-maps each one through a natural palette using
  its own luminance, so the painted detail survives: grass, forest floor (moss vs soil split by hue),
  dirt with embedded stones, cobble, rock, cliff, sand, snow.
- **Terrain smoothness.** Unity 6 defaults new `TerrainLayer`s to "diffuse alpha" smoothness. Opaque RGB
  albedos then read as smoothness 1 and the ground mirrors the sky (it looked white). Layers are set to
  `ConstantOnly` with per-layer values.
- **Foliage shader (`LostRealms/Foliage`).** The vendor vegetation graph emits its colour through a fixed
  ramp and ignores the sun and moon (grass glowed at night, never warmed at sunset). The replacement
  lights the same textures and bottom-to-top tints with wrapped diffuse, soft shadows, translucency,
  SH ambient with base occlusion, lantern lights, SSAO and fog. Property names match the vendor graph
  so materials convert by copying values. Wind follows `_LR_Wind` from the atmosphere; grass parts
  around `_LR_PlayerPos`.
- **Foliage near the camera** dissolves with a screen-door dither (0.5–1.7 m), so the follow camera
  never ends up looking at the inside of a bush or a canopy.
- **Water (`LostRealms/Water`).** The vendor water was a pool cyan that saturated within centimetres,
  and its sky reflection turned every lake milky white. The realm water absorbs light with depth
  (clear shallows over the bed, dark teal in the deep), refracts the scene on High/Ultra (alpha-blended
  on Balanced), reflects the atmosphere's sky colours by Fresnel with the far shore darkening the
  low angles, adds a sharp sun/moon glint and animated shore foam, and rivers flow downstream (flow
  directions are baked into the river mesh, two phases cross-faded). The hero leaves rings when wading.
- **Mountains.** The perimeter ranges are domain-warped ridges with sharper summits on a slow massif
  term, so the skyline has peaks and saddles. Heights pass a soft ceiling (knee 205 m, range 320 m)
  instead of a hard clamp, which used to shave every tall range into a flat-topped plateau.
- **Snow line** sits lower in the north (~115 m), higher over the dunes (~210 m), lower again on shaded
  north faces. Sheer faces shed snow, so mid-mountain cliffs stay dark between white shoulders and
  keep snow only on ledges that follow the strata (contour-aligned noise); the highest walls ice over.
- **Rock exposure** follows the natural slope: banks cut by village pads, roads and terraces keep their
  biome ground (earth, grass, snow) and cliff props only stand on naturally steep ground. Rock faces
  band between broken rock and sheer cliff, and transitions (rock/ground, snow line) break into
  patches instead of a uniform cross-fade, which read as veined marble.
- **Snow, sand and rock never veil each other.** A thin share of white snow over dark, cracked rock
  read as veined marble and washed whole ranges pale. Wherever snow or sand meets stone, the painter
  pushes the mix to one side of a noisy threshold (`Separate`): thin shares become scattered drifts,
  even shares half-and-half patches, and every edge is crisp and broken. Frozen rock collects snow
  drifts, desert rock wind-blown sand, and outcrops break through the snowfields as whole patches.
- **Stone.** Cliff faces use the Terrain Toolkit's photographic layered rock, high-passed (its large
  blotches would otherwise repeat across a mountainside), graded dark grey-brown, with a normal map
  generated from its relief. Rock keeps Idyllic's painted cracks roughened with granular grain, so it
  reads as weathered stone rather than polished marble. Both sit on the dark side of real stone so
  faces keep their shape against snow and sand in the bright midday grade; cobble is mid grey-brown.
- **Sand and snow** stay below the tonemapper's shoulder: brighter, the dunes read white from afar and
  snowfields lose all texture. Snow carries a soft wind-ripple normal.
- **Sky.** `LostRealms/Sky`: gradient matched to the fog, sun disc and glow, fbm cirrus, moon, stars.
  The atmosphere also publishes the zenith/horizon colours (`_LR_SkyZenith`, `_LR_SkyHorizon`) for
  reflective surfaces.
- **Grading.** Neutral tonemapping, gentle bloom (threshold 1.15), per-biome mood blended by position.
  The Frozen Peaks mood exposes for the snowfield (-0.3 EV), so sunlit snow keeps its texture. Every
  tier renders HDR (Balanced with the packed 32-bit format, same bandwidth as RGBA8): in LDR every
  highlight clipped at 1.0 before tonemapping and snow, sky and sand turned flat grey-white.

## Grounding rules

- Every placed rock, cliff and crag is grounded against its whole footprint: the rotated base is
  sampled on a 5×5 grid and the pivot drops by the worst gap, so the downhill edge touches the slope and
  the uphill side beds in. Cliffs tilt with the slope (82%) and only appear above 41°.
- Trees sink by slope × trunk radius; bushes and grass sit on the heightmap.
- Buildings stand on flattened pads from the settlement planner; open-backed corner modules
  (`desert_house_40_*`, `desert_house_42`) are kept out of the hut pool.
- Polylised (Medieval Desert City) FBXs are Z-up; the kit applies `rot: -90 X` to all 91 rows
  (verified with `OpenWorldKitPreview.ProbeOrientation`).
- The hero slows when wading (half speed waist-deep) and slides off ground steeper than the controller
  slope limit instead of clinging to cliff faces.

## Living world

- `OpenWorldPopulation`: deterministic groups by biome — deer, foxes, hares, birds, hens, horses and
  hounds in the Verdant; wolves and elk in the Frozen Peaks; spiders in the Dunes; revenants in the
  Marches; spiders and wolves on the Stormveil cliffs; villagers in every settlement. Groups stream in
  within ~90–110 m, enemies get tougher away from the hub, defeated packs return after 4 minutes.
  Legacy small-world enemies and wildlife from `ImportedContent` are skipped in the realm.
- `OpenWorldSoundscape`: procedural wind (gusts, filter), rain, brooks near water, night crickets, a
  drone in the Marches, thunder; positional birdsong and crows; fire loops at campfires and braziers;
  surface footsteps from the sound bank (grass, leaves, dirt, gravel, rock, tile, sand, snow, water,
  wood, mud).
- `OpenWorldVfx`: pooled store effects with semantic helpers (slash, hit, spell, ultimate, death, heal,
  reward, parry, evade, footstep puffs, attached loops for fires, portals and the Sky-Isle). Vefects
  fire emission is calibrated per renderer (property blocks, vendor materials untouched): its stock
  20–400× emission was authored for an ACES demo and flattened into a yellow sheet in the realm grade.
- **Fires.** Hearths and camp fire pits are a stone ring, crossed logs, a hung cook-pot (hearths) and
  the Vefects fire (smokeless variants: their dark smoke/backdrop layers read as soot in daylight)
  with embers, sparks and heat haze (High/Ultra only, it needs the opaque texture); the soundscape
  crackles nearby. Every camp finds a clear spot for its pit (off roads, landmarks and supplies).
- **Firelight and the light budget.** The terrain is a single renderer, so it receives at most the
  per-object light limit (6 on High, 4 on Balanced). The night-light pool is that size and is fed by
  light anchors (lamps, fires, hearths, nearest first); the fires' own particle lights are switched
  off so they cannot take the slots. The fire-pit props' built-in particle fire (Unity's default particle
  material, flat squares in URP) is switched off, and the small world's placed effects (a low-poly
  fire over the hub hearth, a lone fire on open grass) are not loaded in the realm.
- **Villagers and townsfolk** (Polytope modular peasants): their palette lives in a mask texture plus
  region colours that the generic conversion dropped (they rendered white). `OpenWorldNpcBaker.Bake`
  bakes the vendor palette into the prepared prefabs (Brom, Rowan) and writes farmer, merchant,
  artisan and herbalist clothing variants to `Resources/World/Villagers`, which the population uses.
- Green summer oaks (`tree_snowoak_02/03`) stay out of the Frozen Peaks; only the snow-laden oaks and
  conifers grow there. The hub plaza's two poly-pack trees are swapped for painted kit trees.
- **Lumina** (the companion) is a fairy light: glowing core and halo billboards, translucent flapping
  wings, a sparkle trail that drifts up and fades from aether blue to gold, and a soft light trail.

## Quest landmarks in the realm

- Only the quest **Celestial Sky-Isle** (relic shrine 4) floats: 18 m above its updraft, raised if the hill
  below would crowd it, with a rune circle, light pillar, godrays, motes and drifting leaves. The
  small world's decorative islands and the hub's sky archipelago are not built in the realm.
- Grapple anchors and the updraft keep their designed heights relative to the ground beneath them.

## Diagnostics (bridge `call` helpers)

- `OpenWorldPopulation.DebugReport` / `DebugVisit <species or kind>`
- `OpenWorldSoundscape.DebugReport`
- `OpenWorldVfx.DebugPlay <key> [scale] [lift]`
- `OpenWorldVfx.DebugFreeze <seconds>`: freezes game time that long after the next effect starts (for
  screenshots on a slow editor); `-1` resumes
- `OpenWorldRealm.DebugVisitKey <kit key> [n] [metres]`: stands the hero by the n-th placement of a key
- `OpenWorldKitBuilder.BuildKeys key1,key2,…`: rebuilds only those kit rows
- `OpenWorldHero.DebugAction spell|burst|charged|attack|dodge|god [element]`
- `OpenWorldAtmosphere.DebugSetPreset dawn|midday|golden|night|clear|rain|storm|mist`
- `OpenWorldGraphics.DebugReport` / `DebugTier balanced|high|ultra`: logs (and, for a tour, sets without
  saving) the active tier, HDR, grading mode, render scale and MSAA
- Bridge `shot <name>` renders the main camera into an HDR target and copies the tonemapped result out
  (an 8-bit target made URP clip everything above 1.0 before tonemapping, so snow and sky captured
  flat grey-white); `shot <name> ui` grabs the Game view with the HUD
- The bake writes `ClaudeBridge/shots/realm_bake.txt` (splat probes per region) and `realm_splat.png`.

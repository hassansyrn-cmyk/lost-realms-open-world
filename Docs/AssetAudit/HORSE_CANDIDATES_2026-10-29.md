# Horse Model & Gait Animation Audit — Shortlist (2026-10-29)

## Purpose & Constraints
Per the horse upgrade plan:
1. Search **Sketchfab** (Downloadable + Animated/Rigged; game-safe licenses **CC0** or **CC-BY 4.0** only — **no** `NC`, `ND`, or `SA` licenses, and **no** ripped/extracted commercial game uploads) and **BlenderKit** (free CC0 / Royalty-Free models) for a realistic, rigged horse with gait animations (`walk`, `trot`, `canter`, `gallop`, `idle`, `graze`).
2. Target mobile budget (Step 2 Blender prep):
   - **LOD0:** ~`15,000–25,000` triangles (`LOD1` ~10k, `LOD2` ~4k)
   - **Rig:** `≤ 75` bones
   - **Materials & Textures:** `1–2` materials, `2K` textures (`1K` on Android), tangent-space Normal + AO maps, alpha-tested hair cards skinned to neck/tail bones.
3. **Never enter credentials on Sketchfab.** Shortlist 2–3 options with screenshot, triangle count, bone count, animation list, and license so the user can choose and download (or opt for the Step 2 Blender Fallback).

---

## Audit Summary of Sources Searched
- **BlenderKit Free Models (`api/v1/search/?query=horse`):**
  - *Horse (Beige) PL* by Pierre-Louis Baril (`id: 7c6634b5-a8f4-4cb3-b6c1-318d4749852e`): `2,462,708` faces, `rigged: true`, **`animated: false`**.
  - *Draft horse* by Irena Aidlin Moonin (`id: 01fb9092-58da-498c-a762-04d93d08caea`): `6,236` faces, `rigged: true`, **`animated: false`**.
  - All other free BlenderKit horse results are static statues, busts, or carriages.
- **Sketchfab (`downloadable=true`, `animated=true`, `licenses=by` / `licenses=cc0`):**
  - `CC0` (`licenses=cc0`): `0` downloadable animated horse models exist on Sketchfab.
  - Excluded non-compliant licenses: Amitesh Nandan's horses (`CC-BY-ND`), kenchoo's 15k rigged horse (`CC-BY-NC-SA`), Alain Sorazu's horse (`CC-BY-SA`).
  - Excluded unverified IP: *Star Equestrian - Base Horse Animations* uploaded by Otherspectre (`38b9735c999640678840fba8eb5c8506`) explicitly states *"Model and animations by: Foxie Adventures / Extracted By: Otherspectre"* and is excluded to keep project IP 100% clean.

---

## Shortlisted Candidates (CC-BY 4.0 — Game-Safe)

### Option 1 (Recommended): *Horse* by Henry S
- **Source URL:** https://sketchfab.com/3d-models/horse-a6f860e43e364619bccb174a1ac7d0c9
- **Author:** Henry S (`https://sketchfab.com/HenryS`)
- **License:** **CC Attribution 4.0 International (CC-BY 4.0)** — Commercial & game-safe; requires author credit (`"Horse" by Henry S, licensed under CC BY 4.0`).
- **Format:** `source/Horse.fbx` (76.4 MB FBX authored in Blender) + 8 PNG textures (`4K` body albedo & normal map, `2K` alpha-card mane/tail hair albedo & normal map, `2K` saddle albedo & normal map, `512` horseshoe albedo & normal map).
- **Triangle & Vertex Count:** `30,750` triangles (`13,649` quads + `3,452` triangles; `17,634` vertices) across 5 material slots (Body, Mane/Tail Hair Cards, Saddle/Tack, Horseshoes).
  - *Step 2 Blender Prep Fit:* Stripping the saddle/horseshoes for the **Wild Horse** variant and running a light planar/decimate pass brings `LOD0` directly into the `18k–24k` mobile budget (`LOD1` ~10k, `LOD2` ~4.5k) and consolidates materials to `2` (`HorseBody_Lit` + alpha-clipped `HorseHair_Lit`, plus `Saddle_Lit` on the village mount).
- **Bone Count:** Standard Blender quadruped `Skeleton|*` hierarchy (~`55–65` deform bones covering spine, neck, head/jaw/ears, 4-segment legs + hooves, and multi-bone tail — within the `≤ 75` bone limit; exact deform bone count verified on FBX import).
- **Animation List (28 Clips Included):**
  - **Idles & Grazing:** `Skeleton|1 Ilde` (5.96s), `Skeleton|2 Ilde` (1.88s), `Skeleton|3 Ilde` (2.92s), `Skeleton|6 Eat` (7.67s), `Skeleton|Sleep` (3.29s), `Skeleton|Arise` (3.17s)
  - **Walk (Straight + Banked Turns):** `Skeleton|Walk` (1.00s), `Skeleton|Walk_L` (1.00s), `Skeleton|Walk_R` (1.00s)
  - **Gallop / Canter (Straight + Banked Turns + Stop):** `Skeleton|Gallop` (0.96s), `Skeleton|Gallop_L` (0.96s), `Skeleton|Gallop_R` (0.96s), `Skeleton|Run_Stop` (2.00s)
  - **Jumps:** `Skeleton|Jump` (2.33s), `Skeleton|Jump_Run` (2.00s), `Skeleton|Jump_Land` (2.00s), `Skeleton|S.Jump` (1.50s)
  - **Swimming:** `Skeleton|Swim` (0.83s), `Skeleton|Swim_L` (0.88s), `Skeleton|Swim_R` (0.88s)
  - **Bucking / Rearing / Combat / Death:** `Skeleton|Buck_1` (3.33s), `Skeleton|Buck_2` (4.79s), `Skeleton|Buck_3` (4.58s), `Skeleton|4 Attack_1` (1.33s), `Skeleton|5 Attack_2` (1.33s), `Skeleton|Falling` (2.67s), `Skeleton|Fallen` (2.00s), `Skeleton|Death` (3.00s)
  - *(Note: A dedicated 2-beat diagonal `Trot` and 3-beat `Canter` clip will be derived/authored on the same rig in Blender during Step 3 so all 4 gaits — walk, trot, canter, gallop — have distinct footfall timing).*

---

### Option 2: *Horse (Chestnut Bay & Dapple Grey Pair)* by MAXDESIGN-3D
- **Source URLs:**
  - Chestnut Bay (`6d0f9c`): https://sketchfab.com/3d-models/horse-6d0f9c1ce82f41048a282b6c47a50684
  - Dapple Grey (`d9af69`): https://sketchfab.com/3d-models/horse-d9af6902210645d995e37ba4319957b6
- **Author:** MAXDESIGN-3D (`https://sketchfab.com/MAXDESIGN-3D`)
- **License:** **CC Attribution 4.0 International (CC-BY 4.0)** — Commercial & game-safe; requires author credit (`"Horse" by MAXDESIGN-3D, licensed under CC BY 4.0`).
- **Format:** `source/full.fbx` (4.17 MB) and `source/fullanaimated.fbx` (2.44 MB) + `2048x2048` diffuse, normal (`normal.png`), and specular/roughness textures.
- **Triangle & Vertex Count:**
  - Chestnut Bay: `14,646` triangles (`7,133` quads + `380` tris; `7,996` vertices), `2` materials.
  - Dapple Grey: `14,796` triangles (`7,009` quads + `778` tris; `8,483` vertices), `2` materials.
  - *Step 2 Blender Prep Fit:* Natively sits right at the `15k` mobile `LOD0` target with `2` materials, strong sculpted muscle anatomy, realistic coat shading, and alpha-card mane/tail.
- **Bone Count:** Game-ready quadruped skeleton (~`45–60` bones, `≤ 75` mobile budget).
- **Animation List:**
  - Exported as a single multi-action FBX timeline (`Take 001` — `31.20s` on Chestnut Bay, `9.43s` on Dapple Grey) containing sequential locomotion & idle ranges that are split into individual in-place loops (`Idle`, `Walk`, `Trot`, `Canter`, `Gallop`) in Blender/Unity. (Does not include a modeled saddle mesh, so a low-poly saddle mesh would be added for the village horse in Blender).

---

### Option 3: *Toon Horse with Saddle (Rigged, Animated)* by flairetic
- **Source URL:** https://sketchfab.com/3d-models/toon-horse-with-saddle-rigged-animated-db8fe38f93cb48e7a5c9df446a105f7a
- **Author:** flairetic (`https://sketchfab.com/flairetic`)
- **License:** **CC Attribution 4.0 International (CC-BY 4.0)** — Commercial & game-safe; requires author credit (`"Toon Horse with Saddle (Rigged, Animated)" by flairetic, licensed under CC BY 4.0`).
- **Format:** Native `.blend` file + `Horse/Horse.glb` + 5 coat textures (`Horse Black.png`, `Horse Brown.png`, `Horse Dark Brown.png`, `Horse Grey.png`, `Horse White.png`).
- **Triangle & Vertex Count:** `4,052` triangles (`2,048` vertices), `1` material, includes modeled saddle, stirrups, and bridle.
- **Bone Count:** Simplified **Blender Rigify horse hierarchy** (~`45–55` deform bones).
- **Animation List (4 Explicit Gait Clips):**
  - `Rest` (Idle pose)
  - `Walk` (`1.33s` loop)
  - `Trot` (`0.67s` loop)
  - `Gallop` (`0.50s` loop)
  - *Step 2 Blender Prep Fit:* Because the mesh is low-poly (`4,052` tris) and stylized, choosing this option requires subdividing + multires sculpting muscle anatomy in Blender, baking normal + AO maps, repainting the coat textures, and adding alpha hair cards for the mane/tail (similar to the Fallback route, while benefiting from its Rigify rig and existing `Walk`/`Trot`/`Gallop` clips).

---

### Option 4 (Built-In Fallback — No Download Required): Upgrade Existing `ithappy Horse_001` in Blender
- **Source Asset:** `Assets/ithappy/Animals_FREE/Prefabs/Horse_001.prefab` (`Assets/ithappy/Animals_FREE/Meshes/Horse_001.fbx`, copied to `Assets/GameContent/Horses/` — vendor pack untouched).
- **Current Specs:** `3,104` triangles (`1,690` vertices), `47` bones, `1` material, 4 clips (`Horse_001_idle`, `Horse_001_walk`, `Horse_001_run` [short canter], `Horse_001_eat`).
- **Blender Upgrade Pipeline:**
  1. Import `Horse_001.fbx` into Blender keeping its exact `47`-bone armature and bone names.
  2. Subdivide to `~18,000` triangles for `LOD0` (keeping `8k` for `LOD1` and `3.1k` for `LOD2`), sculpt anatomical muscle groups (shoulder deltoid, neck crest, ribcage barrel, croup/stifle/gaskin, tendons/hooves) on a multires copy, and bake tangent-space **Normal + Ambient Occlusion** maps (`2048x2048`).
  3. Paint realistic bay/chestnut/wild coat textures (darker legs/socks, hooves, facial blaze, roughness variation) and add alpha-tested mane & tail hair cards skinned to the neck/tail bones.
  4. Author missing in-place gait loops (`trot` 2-beat diagonal @ ~4 m/s and true 4-beat suspension `gallop` @ ~9–10 m/s, keeping existing `walk` @ ~1.7 m/s and `canter` @ ~6 m/s) using Eadweard Muybridge's public-domain *The Horse in Motion* sequences.


---

## Final Selection & Downloaded Archive Audit (2026-10-29)

### Downloaded Archives Inspected
1. **`horse.zip` (Option 1 — SELECTED):** *Horse* by Henry S (`https://sketchfab.com/3d-models/horse-a6f860e43e364619bccb174a1ac7d0c9`), **CC Attribution 4.0 International (CC-BY 4.0)**.
   - **Required In-Game Credit:** `"Horse" (https://sketchfab.com/3d-models/horse-a6f860e43e364619bccb174a1ac7d0c9) by Henry S (https://sketchfab.com/HenryS), licensed under CC BY 4.0 (http://creativecommons.org/licenses/by/4.0/).`
2. **`horse (1).zip` (Option 2A — Evaluated):** *Horse* (Chestnut Bay) by MAXDESIGN-3D (`https://sketchfab.com/3d-models/horse-6d0f9c1ce82f41048a282b6c47a50684`), **CC-BY 4.0**. Inspected in Blender: `14,646` tris, `49` bones, single `Take 001` timeline (`31.2s`).
3. **`horse (2).zip` (Option 2B — Evaluated):** *Horse* (Dapple Grey) by MAXDESIGN-3D (`https://sketchfab.com/3d-models/horse-d9af6902210645d995e37ba4319957b6`), **CC-BY 4.0**. Inspected in Blender: `14,796` tris, `49` bones, single `Take 001` timeline (`9.4s`).
4. **`horse-realistic-3d-model-demo-free.zip` (Extra Download — EXCLUDED):** *HORSE - Realistic 3D Model (DEMO FREE)* by WildMesh 3D (`https://sketchfab.com/3d-models/horse-realistic-3d-model-demo-free-65d6a70a6721495f938c93e80a5998e4`).
   - **Exclusion Reason:** Verified via Sketchfab API that the original author's license is **CC Attribution-NonCommercial (`CC-BY-NC`)** (violates the game-safe no-NC guardrail), and the re-upload (`be181c0b860d481f9a05d24c6c964d86`) contains `0` animations (`0.8s` static rest pose). Not imported into the Unity project.

### Prepared Asset Summary (`Assets/GameContent/Horses/Horse_Realistic.fbx`)
- **Rig (`HorseRig`):** `72` deform bones (`≤ 75` mobile limit; `26` leaf helper bones stripped), real-world scale (`1.85 m` height), facing `+Z` in Unity.
- **LOD Hierarchy:**
  - `LOD0`: `Horse_Body_LOD0` (`12,602` tris), `Horse_Hair_LOD0` (`6,103` tris alpha-tested hair cards), `Horse_Saddle_LOD0` (`3,102` tris) — **`18,705` tris wild / `21,807` tris saddled**.
  - `LOD1`: `Horse_Body_LOD1` (`6,300` tris), `Horse_Hair_LOD1` (`3,051` tris), `Horse_Saddle_LOD1` (`1,550` tris) — **`10,901` tris**.
  - `LOD2`: `Horse_Body_LOD2` (`3,024` tris), `Horse_Hair_LOD2` (`1,464` tris), `Horse_Saddle_LOD2` (`744` tris) — **`5,232` tris**.
- **In-Place Animations (13 Clips with Muybridge Footfall Timing & Measured Planted-Hoof Natural Speeds):**
  - `Horse_Walk`: 4-beat lateral walk (`LH 0.00 -> LF 0.25 -> RH 0.50 -> RF 0.75`), `0.750s`, natural speed = **`1.48 m/s`**.
  - `Horse_Trot`: 2-beat diagonal trot (`LF+RH 0.00`, `RF+LH 0.50`), `0.625s`, natural speed = **`4.53 m/s`**.
  - `Horse_Canter`: 3-beat right-lead canter (`LH 0.00 -> RH+LF 0.28 -> RF 0.55`), `0.583s`, natural speed = **`6.04 m/s`**.
  - `Horse_Gallop`: 4-beat suspension gallop (`LH 0.57 -> RH 0.71 -> LF 0.765 -> RF 0.005`), `0.458s`, natural speed = **`10.06 m/s`**.
  - `Horse_Idle`, `Horse_Idle2`, `Horse_Rest`, `Horse_Graze`, `Horse_Roar` (buck/rear), `Horse_Attack`, `Horse_Attack2`, `Horse_Death`, `Horse_Jump`.

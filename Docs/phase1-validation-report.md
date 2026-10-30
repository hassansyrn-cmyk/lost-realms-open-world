# Open-World Fantasy Game — Build & Validation Report
- URP Pipeline Asset: `Assets/Settings/LostRealmsURP.asset` active in GraphicsSettings and all 6 QualitySettings levels.
- Materials: convertedLit=0, convertedParticles=0, alreadyUrp=172, customUnlitCompatible=69
  - custom shader kept: `Eric/URP_AdditiveFlow` (44 mats)
  - custom shader kept: `Eric/URP_AlphaBlendFlow` (10 mats)
  - custom shader kept: `GAP_SG/ParallaxOcclusion` (2 mats)
  - custom shader kept: `Skybox/Cubemap` (1 mats)
  - custom shader kept: `Sprites/Default` (5 mats)
  - custom shader kept: `LostRealms/PortalEnergy` (4 mats)
  - custom shader kept: `GUI/Text Shader` (3 mats)
- Aster Animation Guardrail: 28 baked `.anim` clips verified, all `RootT` curves = 0.0, `applyRootMotion` = false.
- Addressables: initialized 4 groups (`Core_Characters`, `Core_Weapons`, `Biome_Verdant`, `Core_VFX_Audio`) with 211 addressable entries.
- Aster Grounding Check: spawn deltaY=0.13m above terrain (zero floor sink).
- Arsenal Check: all 11 weapons equipped cleanly onto `RightHandBone`.
- Collectibles Check: `TempleKey`, `SecretChest`, `HealthPotion`, and `AetherPotion` disappear immediately upon pickup and update stats.
- Outpost Banner Check: `RallyBanner` transitions from Crimson Red to Emerald Green on capture.
- Fast-Travel Gate Check: arrival deltaY=0.13m above terrain (zero floor sink).
- Open-World Render Probe (768x432 D3D11): `magenta%=0.00` (`Docs/open-world-verdant-preview.png`).
- Playable Scene Saved: `Assets/Scenes/VerdantOpenWorld.unity` and `Assets/Scenes/SampleScene.unity` configured in EditorBuildSettings.
PHASE1_AND_VERTICAL_SLICE_STATUS: PASSED

using UnityEngine;

namespace LostRealmsOW {
 // Per-tier terrain detail (GLM Phase 4): the grass instance distance and density were baked once and
 // applied to every tier, so Balanced paid High's grass bill. This follows the graphics tier and tunes
 // the live Terrain — detail distance/density and the basemap distance where distant ground can fall
 // back to the cheap splat base — without touching the baked realm data. Trees and props are unaffected.
 public sealed class OpenWorldTierTerrain : MonoBehaviour {
  static OpenWorldTierTerrain instance;
  Terrain terrain;
  OpenWorldGraphics.Tier applied = (OpenWorldGraphics.Tier)(-1);

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Install() { if (!FindAnyObjectByType<OpenWorldTierTerrain>()) new GameObject("Tier terrain").AddComponent<OpenWorldTierTerrain>(); }
  void OnDestroy() { if (instance == this) instance = null; }

  void Update() {
   var gm = OpenWorldManager.I;
   var gfx = OpenWorldGraphics.Instance;
   if (!gm || !gfx) return;
   var active = gm.ActiveTerrain;
   if (!terrain || active != terrain) { terrain = active; applied = (OpenWorldGraphics.Tier)(-1); }
   if (!terrain || applied == gfx.Current) return;
   applied = gfx.Current;
   switch (gfx.Current) {
    // The realm bake already tiers this (Balanced 65 m @ 0.45, basemap 180 — OpenWorldRealm) — these
    // are strictly cheaper so the phone tier keeps headroom; High and Ultra re-assert the bake values.
    case OpenWorldGraphics.Tier.Balanced:
     terrain.detailObjectDistance = 45f;
     terrain.detailObjectDensity = 0.4f;
     terrain.basemapDistance = 160f;
     break;
    case OpenWorldGraphics.Tier.High:
     terrain.detailObjectDistance = 115f;
     terrain.detailObjectDensity = 0.85f;
     terrain.basemapDistance = 420f;
     break;
    default:
     terrain.detailObjectDistance = 150f;
     terrain.detailObjectDensity = 1f;
     terrain.basemapDistance = 640f;
     break;
   }
   Debug.Log($"[TierTerrain] {gfx.Label}: detail {terrain.detailObjectDistance:0} m @ {terrain.detailObjectDensity:0.00}, basemap {terrain.basemapDistance:0} m");
  }
 }
}

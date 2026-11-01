using UnityEngine;

namespace LostRealmsOW {
 // 3D Elemental Slash Arc & Expanding Shockwave Ring for Wuthering Waves / Genshin style combat feel.
 public sealed class OpenWorldSlashWave : MonoBehaviour {
  float elapsed;
  float duration = 0.26f;
  Vector3 startScale;
  Vector3 endScale;
  Vector3 velocity;
  Material instanceMat;
  Color baseColor;

  public static void SpawnSlash(Vector3 pos, Quaternion rot, Color color, float reach = 2.6f, bool heavy = false) {
   if (OpenWorldVfx.SlashFromColor(pos, rot, color, reach, heavy)) return;
   string effect = color.r > color.g * 1.35f ? "fx_slash_ember" : color.b > color.r * 1.15f ? "fx_slash_frost" : "fx_slash";
   if (OpenWorldImportedContent.Emit(effect, pos, rot, Mathf.Clamp(reach / 3f, .65f, 1.7f))) return;
   var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
   go.name = "OW_SlashWave";
   if (OpenWorldManager.I && OpenWorldManager.I.WorldRoot) {
    go.transform.SetParent(OpenWorldManager.I.WorldRoot, false);
   }
   var col = go.GetComponent<Collider>();
   if (col) {
    if (Application.isPlaying) Destroy(col);
    else DestroyImmediate(col);
   }

   go.transform.position = pos;
   go.transform.rotation = rot * Quaternion.Euler(0f, 0f, heavy ? 0f : 18f);

   var wave = go.AddComponent<OpenWorldSlashWave>();
   wave.duration = heavy ? 0.34f : 0.22f;
   wave.startScale = new Vector3(reach * 0.55f, 0.018f, reach * 0.55f);
   wave.endScale = new Vector3(reach * (heavy ? 1.55f : 1.15f), 0.006f, reach * (heavy ? 1.55f : 1.15f));
   wave.velocity = (rot * Vector3.forward) * (heavy ? 2.2f : 4.5f);
   wave.baseColor = color;
   go.transform.localScale = wave.startScale;

   wave.instanceMat = new Material(OpenWorldMaterialHelper.UnlitShader);
   OpenWorldMaterialHelper.SetLitProperties(wave.instanceMat, color, null, null, 0.9f, 0.2f, color * 0.85f);
   go.GetComponent<Renderer>().sharedMaterial = wave.instanceMat;
  }

  void Update() {
   elapsed += Time.deltaTime;
   if (elapsed >= duration) {
    Destroy(gameObject);
    return;
   }
   float t = elapsed / duration;
   transform.position += velocity * Time.deltaTime;
   transform.localScale = Vector3.Lerp(startScale, endScale, Mathf.SmoothStep(0f, 1f, t));
  }

  void OnDestroy() {
   if (instanceMat) Destroy(instanceMat);
  }
 }

 // Frontier Biome Rift Monuments & Witcher-style Monster Bounties around the outer Verdant Kingdom
 // (Sunscar Desert Rift, Frostpeak Glacial Rift, Stormveil Tempest Overlook).
 public static class OpenWorldFrontierBuilder {
  public static readonly (string title, string gateModel, string gateTex, Vector3 center, string bountyRole, string bountyName, int bountyElem, Color tint)[] Rifts = {
   ("SUNSCAR MIRAGE RIFT", "Gates/Gate_Desert", "Gates/Textures/Gate_Desert_basecolor", new Vector3(108f, 0f, -6f), "StoneBrute", "Sunscar Dune Stalker", 0, new Color(1.0f, 0.68f, 0.32f)),
   ("FROSTPEAK GLACIAL RIFT", "Gates/Gate_Snow", "Gates/Textures/Gate_Snow_basecolor", new Vector3(-104f, 0f, 8f), "Elite", "Whiteout Frost Howler", 1, new Color(0.45f, 0.88f, 1.0f)),
   ("STORMVEIL TEMPEST OVERLOOK", "Gates/Gate_Storm", "Gates/Textures/Gate_Storm_basecolor", new Vector3(102f, 0f, -98f), "Elite", "Tempest Rift Warden", 2, new Color(0.58f, 0.45f, 1.0f))
  };

  // In the 1 km realm each rift sits in its own biome: the Desert Rift in the Burning Dunes, the
  // Glacial Rift high in the Frozen Peaks and the Tempest Overlook on the Stormveil Cliffs.
  public static Vector3 RiftCenter(int i) {
   if (OpenWorldRealm.Available) {
    switch (i) {
     case 0: return OpenWorldRegions.DesertRift;
     case 1: return OpenWorldRegions.FrostRift;
     default: return OpenWorldRegions.StormRift;
    }
   }
   return Rifts[i].center;
  }

  public static void BuildFrontierRifts(OpenWorldManager gm) {
   for (int i = 0; i < Rifts.Length; i++) {
    var r = Rifts[i];
    Vector3 pos = RiftCenter(i);
    pos.y = OpenWorldMaterialHelper.SampleGroundHeight(pos);

    var riftRoot = new GameObject("FrontierRift_" + i);
    riftRoot.transform.SetParent(gm.WorldRoot, false);
    riftRoot.transform.position = pos;

    // Monumental Realm Rift Arch
    var gateVisual = OpenWorldMaterialHelper.SpawnModel(r.gateModel, r.gateTex, riftRoot.transform, pos, 4.8f, Color.white, true);
    if (gateVisual) gateVisual.transform.LookAt(new Vector3(0f, pos.y, 0f));

    // Biome-themed flanking rocks / trees
    for (int s = -1; s <= 1; s += 2) {
     Vector3 flank = pos + new Vector3(s * 5.2f, 0f, s * 2.4f);
     flank.y = OpenWorldMaterialHelper.SampleGroundHeight(flank);
     if (i == 1) {
      OpenWorldMaterialHelper.SpawnModel("Props/Nature/Tree_03", "Props/Textures/Nature_snow", riftRoot.transform, flank, 5.4f, new Color(0.88f, 0.95f, 1f), true);
     } else {
      OpenWorldMaterialHelper.SpawnModel("Props/Nature/Rock_04", "Props/Textures/Nature_basecolor", riftRoot.transform, flank, 2.2f, r.tint, true);
     }
    }

    // Spawn the Frontier Bounty Monster 5.5m in front of the Rift Monument
    Vector3 towardHub = (Vector3.zero - pos);
    towardHub.y = 0f;
    Vector3 bountyPos = pos + towardHub.normalized * 5.5f;
    var bounty = OpenWorldEnemy.Spawn(r.bountyRole, r.bountyName, bountyPos, gm.WorldRoot, false);
    bounty.IsBountyElite = true;
    bounty.BountyIndex = i;
    bounty.MaxHealth = 20f;
    bounty.Health = 20f;
    bounty.AttackDamage = 1.5f;
    bounty.AttackRange = 2.4f;
    if (bounty.Visual) bounty.Visual.transform.localScale = Vector3.one * 1.28f;
    gm.BountyElites.Add(bounty);
    gm.Enemies.Add(bounty);
   }
  }
 }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LostRealmsOW {
 // Polytope modular NPCs (Brom, Rowan and the realm's villagers) were prepared with a generic Lit
 // conversion that kept no texture: their palette lives in a mask texture plus per-region colours, so
 // they rendered white. This bakes the vendor palette into the prepared prefabs and writes a few
 // villager variants with different clothing, hair and skin into Resources/World/Villagers.
 public static class OpenWorldNpcBaker {
  const string Prepared = "Assets/GameContent/Imported/Prefabs/";
  const string Materials = "Assets/GameContent/Imported/Materials/";
  const string Villagers = "Assets/Resources/World/Villagers";

  static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

  // Clothing palettes: farmer, merchant, artisan, herbalist.
  static readonly (string name, Dictionary<string, Color> colors)[] Palettes = {
   ("farmer", new Dictionary<string, Color> {
    ["_CLOTH1COLOR"] = C(0.46f, 0.34f, 0.21f), ["_CLOTH2COLOR"] = C(0.66f, 0.58f, 0.42f), ["_CLOTH3COLOR"] = C(0.32f, 0.38f, 0.22f), ["_CLOTH4COLOR"] = C(0.52f, 0.44f, 0.3f),
    ["_LEATHER1COLOR"] = C(0.36f, 0.24f, 0.15f), ["_LEATHER2COLOR"] = C(0.3f, 0.2f, 0.13f), ["_HAIRCOLOR"] = C(0.26f, 0.17f, 0.1f), ["_SKINCOLOR"] = C(0.86f, 0.68f, 0.56f) }),
   ("merchant", new Dictionary<string, Color> {
    ["_CLOTH1COLOR"] = C(0.45f, 0.13f, 0.13f), ["_CLOTH2COLOR"] = C(0.78f, 0.7f, 0.52f), ["_CLOTH3COLOR"] = C(0.18f, 0.25f, 0.42f), ["_CLOTH4COLOR"] = C(0.6f, 0.48f, 0.2f),
    ["_LEATHER1COLOR"] = C(0.24f, 0.15f, 0.1f), ["_LEATHER2COLOR"] = C(0.4f, 0.27f, 0.16f), ["_HAIRCOLOR"] = C(0.11f, 0.09f, 0.08f), ["_SKINCOLOR"] = C(0.72f, 0.53f, 0.4f) }),
   ("artisan", new Dictionary<string, Color> {
    ["_CLOTH1COLOR"] = C(0.2f, 0.36f, 0.42f), ["_CLOTH2COLOR"] = C(0.56f, 0.52f, 0.46f), ["_CLOTH3COLOR"] = C(0.52f, 0.39f, 0.18f), ["_CLOTH4COLOR"] = C(0.3f, 0.3f, 0.33f),
    ["_LEATHER1COLOR"] = C(0.42f, 0.29f, 0.17f), ["_LEATHER2COLOR"] = C(0.33f, 0.22f, 0.14f), ["_HAIRCOLOR"] = C(0.58f, 0.43f, 0.22f), ["_SKINCOLOR"] = C(0.9f, 0.74f, 0.62f) }),
   ("herbalist", new Dictionary<string, Color> {
    ["_CLOTH1COLOR"] = C(0.27f, 0.4f, 0.25f), ["_CLOTH2COLOR"] = C(0.7f, 0.66f, 0.55f), ["_CLOTH3COLOR"] = C(0.4f, 0.26f, 0.36f), ["_CLOTH4COLOR"] = C(0.45f, 0.36f, 0.24f),
    ["_LEATHER1COLOR"] = C(0.3f, 0.21f, 0.14f), ["_LEATHER2COLOR"] = C(0.46f, 0.33f, 0.2f), ["_HAIRCOLOR"] = C(0.38f, 0.2f, 0.12f), ["_SKINCOLOR"] = C(0.55f, 0.38f, 0.27f) }),
  };

  [MenuItem("Lost Realms OW/Content/Bake Polytope NPCs")]
  public static void Bake() {
   var log = new StringBuilder();
   bakedCache.Clear();
   var manifest = JsonUtility.FromJson<ImportedContentManifest>(File.ReadAllText("Assets/Resources/World/ImportedContent.json"));
   Directory.CreateDirectory(Materials);
   Directory.CreateDirectory(Villagers);
   OpenWorldMaterialConverter.Begin();
   foreach (var e in manifest.entries) {
    if (e.source == null || !e.source.Contains("Polytope Studio") || !e.source.Contains("Modular_NPC")) continue;
    string prepared = Prepared + e.key + ".prefab";
    var source = AssetDatabase.LoadAssetAtPath<GameObject>(e.source);
    if (!source || !AssetDatabase.LoadAssetAtPath<GameObject>(prepared)) { log.AppendLine("missing " + e.key); continue; }
    int fixedCount = Apply(prepared, prepared, source, null, null);
    log.AppendLine($"{e.key} ({e.alias}): {fixedCount} materials baked in place");
    foreach (var palette in Palettes) {
     string variantPath = Villagers + "/" + Path.GetFileNameWithoutExtension(e.source) + "_" + palette.name + ".prefab";
     int n = Apply(prepared, variantPath, source, palette.name, palette.colors);
     log.AppendLine($"  variant {palette.name}: {n} materials -> {variantPath}");
    }
   }
   OpenWorldMaterialConverter.PolytopeOverride = null;
   AssetDatabase.SaveAssets();
   AssetDatabase.Refresh();
   Debug.Log("[NpcBaker]\n" + log);
  }

  // Loads the prepared prefab, replaces every material whose source is the Polytope NPC shader with a
  // baked Lit material (optionally re-coloured) and saves the result to outputPath.
  static int Apply(string preparedPath, string outputPath, GameObject source, string variant, Dictionary<string, Color> palette) {
   var root = PrefabUtility.LoadPrefabContents(preparedPath);
   int count = 0;
   try {
    var sourceRenderers = source.GetComponentsInChildren<Renderer>(true);
    foreach (var r in root.GetComponentsInChildren<Renderer>(true)) {
     var match = sourceRenderers.FirstOrDefault(s => s.name == r.name && s.sharedMaterials.Length == r.sharedMaterials.Length);
     if (!match) continue;
     var mats = r.sharedMaterials;
     bool changed = false;
     for (int i = 0; i < mats.Length; i++) {
      var original = match.sharedMaterials[i];
      if (!original || !original.shader || !original.shader.name.Contains("PT_Medieval Modular NPC Shader")) continue;
      var baked = BakeMaterial(original, variant, palette);
      if (!baked) continue;
      mats[i] = baked; changed = true; count++;
     }
     if (changed) r.sharedMaterials = mats;
    }
    PrefabUtility.SaveAsPrefabAsset(root, outputPath);
   } finally { PrefabUtility.UnloadPrefabContents(root); }
   return count;
  }

  static readonly Dictionary<string, Material> bakedCache = new Dictionary<string, Material>();

  static Material BakeMaterial(Material original, string variant, Dictionary<string, Color> palette) {
   string stem = OpenWorldMaterialConverter.Sanitize(original.name) + (variant != null ? "_" + variant : "");
   if (bakedCache.TryGetValue(stem, out var cached) && cached) return cached;
   OpenWorldMaterialConverter.PolytopeOverride = palette;
   var baked = OpenWorldMaterialConverter.BakePolytopeNpc(original, variant);
   OpenWorldMaterialConverter.PolytopeOverride = null;
   if (!baked) return null;
   string path = Materials + stem + "_baked.mat";
   var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
   if (existing) { EditorUtility.CopySerialized(baked, existing); Object.DestroyImmediate(baked); baked = existing; EditorUtility.SetDirty(baked); }
   else AssetDatabase.CreateAsset(baked, path);
   bakedCache[stem] = baked;
   return baked;
  }
 }
}

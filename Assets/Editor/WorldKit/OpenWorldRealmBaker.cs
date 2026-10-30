using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LostRealmsOW {
 // Bakes the whole realm into Resources/World/Realm: sculpted + painted TerrainData with instanced
 // trees, bushes and grass; placement data for architecture, cliffs, rocks and props; compact
 // road/water fields; the illustrated map atlas; terrain and water materials.
 public static class OpenWorldRealmBaker {
  public const string Folder = "Assets/Resources/World/Realm";
  static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ClaudeBridge", "shots"));

  [MenuItem("Lost Realms OW/World/Bake Realm")]
  public static void Bake() {
   var sw = System.Diagnostics.Stopwatch.StartNew();
   var log = new StringBuilder();
   void Step(string what) { log.AppendLine($"{sw.Elapsed.TotalSeconds,7:0.0}s {what}"); Debug.Log("[Realm] " + what); }
   Directory.CreateDirectory(Folder);
   OpenWorldWorldKit.Reload();
   if (!OpenWorldWorldKit.Loaded) { Debug.LogError("[Realm] World Kit manifest missing — build the kit first."); return; }
   Step("kit entries " + OpenWorldWorldKit.Count);
   OpenWorldTerrainSculptor.ResetPads();
   OpenWorldWorldPlan.RegisterCorePads();
   OpenWorldPlanBridge.RegisterSettlementPads();
   OpenWorldSettlementPlans.Plan();
   Step("settlements planned, building footprints " + OpenWorldSettlementPlans.BuildingFootprints.Count + ", pads " + OpenWorldTerrainSculptor.Pads.Count);
   var td = OpenWorldTerrainSculptor.Build();
   Step("terrain sculpted " + td.heightmapResolution + "²");
   // Persist the TerrainData before painting so its splat textures become proper sub-assets of the
   // asset (painting an in-memory TerrainData and saving afterwards loses the alphamaps at runtime).
   string tdPath = Folder + "/RealmTerrain.asset";
   if (AssetDatabase.LoadAssetAtPath<TerrainData>(tdPath)) AssetDatabase.DeleteAsset(tdPath);
   td.name = "RealmTerrain";
   AssetDatabase.CreateAsset(td, tdPath);
   if (!OpenWorldTerrainPainter.Apply(td)) Debug.LogWarning("[Realm] terrain layers missing; splat left default");
   Step("terrain painted " + td.alphamapResolution + "² x " + td.terrainLayers.Length + " layers");
   var placements = new List<OWPlacement>();
   OpenWorldSettlementPlans.Resolve(placements);
   int settlementCount = placements.Count;
   Step("settlement pieces " + settlementCount);
   var splat = OpenWorldTerrainPainter.LastMap;
   OpenWorldEcosystem.Plan(td, placements, splat);
   Step("ecosystem: " + OpenWorldEcosystem.Last.summary);
   // Sky clouds come from the LostRealms/Sky shader; the low-poly cloud meshes read as boxes at realm scale.
   if (splat != null) td.SetAlphamaps(0, 0, splat);
   foreach (var tex in td.alphamapTextures) {
    if (!tex) continue;
    if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(tex))) AssetDatabase.AddObjectToAsset(tex, td);
    EditorUtility.SetDirty(tex);
   }
   EditorUtility.SetDirty(td);
   AssetDatabase.SaveAssets();
   VerifySplat(td, log);
   WriteSplatPreview(td);
   OpenWorldTerrainPainter.ReleaseMap();
   Step("terrain asset saved (" + td.treeInstanceCount + " trees, " + td.detailPrototypes.Length + " detail layers, " + td.alphamapTextures.Length + " splat textures)");
   File.WriteAllText(Folder + "/RealmPlacements.json", JsonUtility.ToJson(OWPlacementSet.Pack(placements)));
   File.WriteAllBytes(Folder + "/RealmFields.bytes", OpenWorldTerrainSculptor.SerializeFields());
   var atlas = OpenWorldMapRenderer.Build(1024);
   File.WriteAllBytes(Folder + "/RealmAtlas.png", atlas.EncodeToPNG());
   Directory.CreateDirectory(Shots);
   File.WriteAllBytes(Path.Combine(Shots, "realm_atlas.png"), atlas.EncodeToPNG());
   UnityEngine.Object.DestroyImmediate(atlas);
   Step("placements, fields and atlas written (" + placements.Count + " placements)");
   EnsureMaterials();
   AssetDatabase.SaveAssets();
   AssetDatabase.Refresh();
   ConfigureAtlasImporter(Folder + "/RealmAtlas.png");
   // Summary of what went where.
   var byKind = placements.GroupBy(p => p.kind).Select(g => g.Key + " " + g.Count());
   var byZone = placements.Where(p => p.zone != "wild" && p.zone != "shore").GroupBy(p => p.zone).Select(g => g.Key + " " + g.Count());
   log.AppendLine("kinds: " + string.Join(", ", byKind));
   log.AppendLine("zones: " + string.Join(", ", byZone));
   log.AppendLine("missing kit prefabs: " + string.Join(", ", OpenWorldWorldKit.Missing));
   foreach (var lake in OpenWorldRegions.Lakes) log.AppendLine($"lake {lake.Name} surface {lake.SurfaceY:0.00}");
   File.WriteAllText(Path.Combine(Shots, "realm_bake.txt"), log.ToString());
   Step("bake complete");
  }

  // Reads the saved asset back at known places so a broken splat shows up in the bake log.
  static void VerifySplat(TerrainData td, StringBuilder log) {
   (string name, Vector3 p)[] probes = {
    ("hub plaza", new Vector3(0f, 0f, 6f)), ("frostpine", OpenWorldRegions.Frostpine + new Vector3(0f, 0f, 30f)), ("sunspire", OpenWorldRegions.Sunspire + new Vector3(0f, 0f, 30f)),
    ("graveyard", OpenWorldRegions.Graveyard), ("storm", OpenWorldRegions.StormOverlook + new Vector3(20f, 0f, 0f)), ("dragon peak", OpenWorldRegions.DragonPeak)
   };
   var saved = AssetDatabase.LoadAssetAtPath<TerrainData>(AssetDatabase.GetAssetPath(td));
   foreach (var pr in probes) {
    int ax = Mathf.Clamp(Mathf.FloorToInt((pr.p.x + OpenWorldRegions.WorldHalf) / (OpenWorldRegions.WorldHalf * 2f) * saved.alphamapWidth), 0, saved.alphamapWidth - 1);
    int az = Mathf.Clamp(Mathf.FloorToInt((pr.p.z + OpenWorldRegions.WorldHalf) / (OpenWorldRegions.WorldHalf * 2f) * saved.alphamapHeight), 0, saved.alphamapHeight - 1);
    var w = saved.GetAlphamaps(ax, az, 1, 1);
    var sb = new StringBuilder();
    for (int i = 0; i < w.GetLength(2); i++) sb.Append(OpenWorldTerrainPainter.LayerNames[Mathf.Min(i, 7)]).Append('=').Append(w[0, 0, i].ToString("0.00")).Append(' ');
    log.AppendLine("splat " + pr.name + ": " + sb);
   }
  }

  static void WriteSplatPreview(TerrainData td) {
   Color[] palette = { new Color(0.45f, 0.7f, 0.25f), new Color(0.25f, 0.42f, 0.18f), new Color(0.55f, 0.42f, 0.28f), new Color(0.6f, 0.6f, 0.62f),
    new Color(0.45f, 0.45f, 0.47f), new Color(0.3f, 0.3f, 0.32f), new Color(0.9f, 0.78f, 0.52f), new Color(0.97f, 0.98f, 1f) };
   const int size = 512;
   int step = Mathf.Max(1, td.alphamapWidth / size);
   var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
   for (int z = 0; z < size; z++) {
    var row = td.GetAlphamaps(0, Mathf.Min(td.alphamapHeight - 1, z * step), td.alphamapWidth, 1);
    for (int x = 0; x < size; x++) {
     int ax = Mathf.Min(td.alphamapWidth - 1, x * step);
     Color c = Color.black;
     for (int l = 0; l < row.GetLength(2) && l < palette.Length; l++) c += palette[l] * row[0, ax, l];
     tex.SetPixel(x, z, c);
    }
   }
   tex.Apply();
   Directory.CreateDirectory(Shots);
   File.WriteAllBytes(Path.Combine(Shots, "realm_splat.png"), tex.EncodeToPNG());
   UnityEngine.Object.DestroyImmediate(tex);
  }

  static void AddClouds(List<OWPlacement> placements) {
   var rng = new System.Random(5150);
   var clouds = OpenWorldWorldKit.Role("cloud");
   if (clouds.Count == 0) return;
   for (int i = 0; i < 26; i++) {
    var e = clouds[rng.Next(clouds.Count)];
    float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 120f + (float)rng.NextDouble() * 520f;
    float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
    float target = 60f + (float)rng.NextDouble() * 70f;
    float scale = target / Mathf.Max(1f, Mathf.Max(e.size.x, e.size.z));
    float y = 175f + (float)rng.NextDouble() * 70f;
    placements.Add(new OWPlacement { key = e.key, pos = new Vector3(x, y, z), euler = new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), scale = scale, kind = OWPlaceKind.Cloud, zone = "sky" });
   }
  }

  static void ConfigureAtlasImporter(string path) {
   var importer = AssetImporter.GetAtPath(path) as TextureImporter;
   if (!importer) return;
   importer.textureType = TextureImporterType.Default;
   importer.mipmapEnabled = false;
   importer.wrapMode = TextureWrapMode.Clamp;
   importer.textureCompression = TextureImporterCompression.CompressedHQ;
   importer.maxTextureSize = 1024;
   importer.SaveAndReimport();
  }

  // Terrain, water and ice materials the runtime loads from Resources.
  public static void EnsureMaterials() {
   var terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
   if (terrainShader) {
    var m = LoadOrCreate(Folder + "/RealmTerrain.mat", terrainShader);
    // Height-based blending only works with four layers or fewer; with eight it renormalises the
    // first pass and paints grass over snow and sand, so the realm uses regular weight blending.
    if (m.HasProperty("_EnableHeightBlend")) m.SetFloat("_EnableHeightBlend", 0f);
    m.DisableKeyword("_TERRAIN_BLEND_HEIGHT");
    if (m.HasProperty("_EnableInstancedPerPixelNormal")) { m.SetFloat("_EnableInstancedPerPixelNormal", 1f); m.EnableKeyword("_TERRAIN_INSTANCED_PERPIXEL_NORMAL"); }
    m.enableInstancing = true;
    EditorUtility.SetDirty(m);
   }
   // Realm water (LostRealms/Water): refracting on High/Ultra, alpha-blended on Balanced. The vendor
   // stylised water is only the fallback if the shader is missing or failed to compile.
   var vendorWater = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Idyllic Fantasy Nature/Prefabs/Water.prefab");
   var vendorMat = vendorWater ? vendorWater.GetComponentInChildren<Renderer>(true)?.sharedMaterial : null;
   var realmWater = Shader.Find("LostRealms/Water");
   if (realmWater && realmWater.isSupported && !ShaderUtil.ShaderHasError(realmWater)) {
    Texture ripples = vendorMat && vendorMat.HasProperty("_Refraction_Normal") ? vendorMat.GetTexture("_Refraction_Normal") : null;
    ConfigureWater(LoadOrCreate(Folder + "/RealmWater.mat", realmWater), ripples, true);
    ConfigureWater(LoadOrCreate(Folder + "/RealmWaterSimple.mat", realmWater), ripples, false);
   } else if (vendorMat) {
    var copy = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/RealmWater.mat");
    if (!copy) { copy = new Material(vendorMat) { name = "RealmWater" }; AssetDatabase.CreateAsset(copy, Folder + "/RealmWater.mat"); }
    else EditorUtility.CopySerialized(vendorMat, copy);
    copy.name = "RealmWater";
    // The vendor lake is a bright swimming-pool cyan that saturates within 25 cm of depth; the realm's
    // lakes and rivers deepen from clear green shallows to dark teal over a couple of metres.
    SetColor(copy, "_Shallow_Color", new Color(0.27f, 0.56f, 0.55f, 0.5f));
    SetColor(copy, "_Deep_Color", new Color(0.035f, 0.19f, 0.24f, 0.93f));
    SetFloat(copy, "_Depth_Distance", 2.4f);
    SetFloat(copy, "_Coast_Opacity", 0.22f);
    SetFloat(copy, "_Smoothness", 0.78f);
    SetFloat(copy, "_Normal_Strength", 0.32f);
    SetFloat(copy, "_Foam_Amount", 0.11f);
    SetColor(copy, "_Foam_Color", new Color(0.92f, 0.96f, 0.95f, 0.85f));
    EditorUtility.SetDirty(copy);
   }
   var lit = Shader.Find("Universal Render Pipeline/Lit");
   if (!(realmWater && realmWater.isSupported && !ShaderUtil.ShaderHasError(realmWater))) {
    var simple = LoadOrCreate(Folder + "/RealmWaterSimple.mat", lit);
    simple.SetFloat("_Surface", 1f); simple.SetFloat("_Blend", 0f);
    simple.SetColor("_BaseColor", new Color(0.13f, 0.4f, 0.48f, 0.74f));
    simple.SetFloat("_Smoothness", 0.95f); simple.SetFloat("_Metallic", 0f);
    OpenWorldMaterialConverter.FixLitKeywords(simple);
    EditorUtility.SetDirty(simple);
   }
   // Frozen tarn: blue-grey ice with a soft sheen (a mirror finish read as white from most angles).
   var ice = LoadOrCreate(Folder + "/RealmIce.mat", lit);
   ice.SetColor("_BaseColor", new Color(0.6f, 0.76f, 0.86f));
   ice.SetFloat("_Smoothness", 0.62f); ice.SetFloat("_Metallic", 0f);
   var snow = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Resources/World/Terrain/Snow.terrainlayer");
   if (snow && snow.diffuseTexture) { ice.SetTexture("_BaseMap", snow.diffuseTexture); ice.SetTextureScale("_BaseMap", new Vector2(0.08f, 0.08f)); }
   OpenWorldMaterialConverter.FixLitKeywords(ice);
   EditorUtility.SetDirty(ice);
  }

  static void ConfigureWater(Material m, Texture ripples, bool refraction) {
   m.SetColor("_Shallow_Color", new Color(0.24f, 0.52f, 0.5f, 0.55f));
   m.SetColor("_Deep_Color", new Color(0.03f, 0.16f, 0.21f, 0.94f));
   m.SetFloat("_Depth_Distance", 2.2f);
   if (ripples) m.SetTexture("_Refraction_Normal", ripples);
   m.SetFloat("_Ripple_Scale", 0.22f);
   m.SetFloat("_Normal_Strength", 0.55f);
   m.SetFloat("_Water_Speed", 0.5f);
   m.SetFloat("_Refraction_Strength", 0.035f);
   m.SetFloat("_Smoothness", 0.9f);
   m.SetFloat("_Reflection", 0.6f);
   m.SetColor("_Foam_Color", new Color(0.9f, 0.95f, 0.94f, 0.8f));
   m.SetFloat("_Foam_Amount", 0.55f);
   m.SetFloat("_Coast_Opacity", 0.22f);
   m.SetFloat("_Flow", 0f);
   if (refraction) m.EnableKeyword("_LR_REFRACTION"); else m.DisableKeyword("_LR_REFRACTION");
   m.renderQueue = -1;
   EditorUtility.SetDirty(m);
  }

  static void SetColor(Material m, string name, Color c) { if (m.HasProperty(name)) m.SetColor(name, c); }
  static void SetFloat(Material m, string name, float v) { if (m.HasProperty(name)) m.SetFloat(name, v); }

  static Material LoadOrCreate(string path, Shader shader) {
   var m = AssetDatabase.LoadAssetAtPath<Material>(path);
   if (!m) { m = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) }; AssetDatabase.CreateAsset(m, path); }
   else if (m.shader != shader) m.shader = shader;
   return m;
  }
 }
}

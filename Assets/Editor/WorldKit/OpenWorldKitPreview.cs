using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LostRealmsOW {
 // Renders contact sheets of the prepared World Kit so orientation, scale, materials and pivots
 // can be reviewed without opening the editor: ClaudeBridge/shots/kit_<filter>_<page>.png + legend.
 public static class OpenWorldKitPreview {
  const int Tile = 224, Cols = 8, Rows = 6;
  static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ClaudeBridge", "shots"));

  [MenuItem("Lost Realms OW/World Kit/Render Contact Sheets")]
  public static void RenderAll() { Render(""); }
  public static void RenderVegetation() { Render("tree,palm,deadtree,bush,reed,lily,fern,mushroom,grass,flower,plant"); }
  public static void RenderRocks() { Render("rock,rock_small,rock_big,stone,cliff,mountain,log,shrine"); }
  public static void RenderArchitecture() { Render("building,wall,tower,gate,ruin"); }
  public static void RenderProps() { Render("prop,fence,lamp,banner,campfire,tombstone,cloud"); }
  public static void RenderVfx() { Render("vfx"); }

  public static void Render(string roles) {
   var text = AssetDatabase.LoadAssetAtPath<TextAsset>(OpenWorldKitBuilder.ManifestPath);
   if (!text) { Debug.LogError("[KitPreview] manifest missing"); return; }
   var manifest = JsonUtility.FromJson<WorldKitManifest>(text.text);
   var filter = new HashSet<string>((roles ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
   var list = manifest.entries.Where(e => filter.Count == 0 || filter.Contains(e.role)).ToList();
   string tag = filter.Count == 0 ? "all" : filter.First();
   Directory.CreateDirectory(Shots);
   int perPage = Cols * Rows;
   var legend = new StringBuilder();
   for (int page = 0; page * perPage < list.Count; page++) {
    var sheet = new Texture2D(Cols * Tile, Rows * Tile, TextureFormat.RGB24, false);
    var fill = new Color32[sheet.width * sheet.height];
    for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(38, 42, 48, 255);
    sheet.SetPixels32(fill);
    for (int slot = 0; slot < perPage; slot++) {
     int index = page * perPage + slot;
     if (index >= list.Count) break;
     var e = list[index];
     legend.AppendLine($"{tag} p{page} #{slot}: {e.key} [{e.role}] {e.size.x:0.0}x{e.size.y:0.0}x{e.size.z:0.0} m tris {e.triangles}");
     Texture2D tile = null;
     try { tile = RenderEntry(e); } catch (Exception ex) { legend.AppendLine("   render failed: " + ex.Message); }
     int cx = (slot % Cols) * Tile, cy = (Rows - 1 - slot / Cols) * Tile;
     if (tile) {
      var px = tile.GetPixels32();
      for (int y = 0; y < Tile; y++) for (int x = 0; x < Tile; x++) sheet.SetPixel(cx + x, cy + y, px[Mathf.Min(px.Length - 1, y * tile.width + x)]);
      UnityEngine.Object.DestroyImmediate(tile);
     }
     DrawNumber(sheet, cx + 4, cy + Tile - 16, slot, Color.yellow);
    }
    sheet.Apply();
    File.WriteAllBytes(Path.Combine(Shots, $"kit_{tag}_{page}.png"), sheet.EncodeToPNG());
    UnityEngine.Object.DestroyImmediate(sheet);
    EditorUtility.UnloadUnusedAssetsImmediate();
   }
   File.WriteAllText(Path.Combine(Shots, $"kit_{tag}_legend.txt"), legend.ToString());
   Debug.Log($"[KitPreview] rendered {list.Count} {tag} entries");
  }

  static Texture2D RenderEntry(WorldKitEntry e) {
   var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/" + e.resource + ".prefab");
   if (!prefab) return null;
   var pru = new PreviewRenderUtility();
   try {
    var go = pru.InstantiatePrefabInScene(prefab);
    if (e.role == "vfx") foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) { ps.Simulate(Mathf.Clamp(e.life * 0.35f, 0.25f, 1.2f), true, true); }
    Bounds b = new Bounds(go.transform.position, Vector3.one);
    bool has = false;
    foreach (var r in go.GetComponentsInChildren<Renderer>(true)) {
     if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
    }
    float radius = Mathf.Max(0.3f, b.extents.magnitude);
    var cam = pru.camera;
    cam.fieldOfView = 30f;
    cam.nearClipPlane = 0.01f;
    cam.farClipPlane = radius * 20f + 50f;
    cam.clearFlags = CameraClearFlags.SolidColor;
    cam.backgroundColor = new Color(0.32f, 0.38f, 0.45f);
    Vector3 dir = new Vector3(0.75f, 0.55f, -1f).normalized;
    float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
    cam.transform.position = b.center + dir * dist;
    cam.transform.LookAt(b.center);
    pru.lights[0].intensity = 1.3f;
    pru.lights[0].transform.rotation = Quaternion.Euler(45f, -35f, 0f);
    pru.lights[1].intensity = 0.6f;
    pru.ambientColor = new Color(0.45f, 0.47f, 0.5f);
    pru.BeginStaticPreview(new Rect(0, 0, Tile, Tile));
    pru.Render(true, false);
    var tex = pru.EndStaticPreview();
    // Ground line marker: draw the pivot height so floating/sunken pivots are visible.
    return tex;
   } finally { pru.Cleanup(); }
  }

  // Orientation probe for vendor prefabs: row 0 native, row 1 rotated -90 about X, row 2 rotated
  // with double-sided grey materials (reveals inverted faces). Columns look from N, E, S and W.
  // call LostRealmsOW.OpenWorldKitPreview.ProbeOrientation Assets/a.prefab;Assets/b.prefab
  public static void ProbeOrientation(string paths) {
   Directory.CreateDirectory(Shots);
   var lit = Shader.Find("Universal Render Pipeline/Lit");
   var grey = lit ? new Material(lit) : null;
   if (grey) { grey.SetColor("_BaseColor", new Color(0.75f, 0.72f, 0.68f)); grey.SetFloat("_Cull", 0f); grey.doubleSidedGI = true; }
   foreach (var raw in (paths ?? "").Split(';')) {
    string path = raw.Trim();
    if (path.Length == 0) continue;
    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
    if (!prefab) { Debug.LogError("[KitPreview] probe: missing " + path); continue; }
    var sheet = new Texture2D(4 * Tile, 3 * Tile, TextureFormat.RGB24, false);
    for (int row = 0; row < 3; row++) for (int col = 0; col < 4; col++) {
     var tile = RenderObject(prefab, row == 0 ? Quaternion.identity : Quaternion.Euler(-90f, 0f, 0f), col * 90f, row == 2 ? grey : null);
     if (!tile) continue;
     var px = tile.GetPixels32();
     int cx = col * Tile, cy = (2 - row) * Tile;
     for (int y = 0; y < Tile; y++) for (int x = 0; x < Tile; x++) sheet.SetPixel(cx + x, cy + y, px[Mathf.Min(px.Length - 1, y * tile.width + x)]);
     UnityEngine.Object.DestroyImmediate(tile);
    }
    sheet.Apply();
    File.WriteAllBytes(Path.Combine(Shots, "orient_" + Path.GetFileNameWithoutExtension(path).Replace(' ', '_') + ".png"), sheet.EncodeToPNG());
    UnityEngine.Object.DestroyImmediate(sheet);
   }
   if (grey) UnityEngine.Object.DestroyImmediate(grey);
   Debug.Log("[KitPreview] orientation probes written");
  }

  static Texture2D RenderObject(GameObject prefab, Quaternion rotation, float azimuth, Material overrideMat) {
   var pru = new PreviewRenderUtility();
   try {
    var go = pru.InstantiatePrefabInScene(prefab);
    go.transform.rotation = rotation;
    var renderers = go.GetComponentsInChildren<Renderer>(true);
    if (overrideMat) foreach (var r in renderers) { var mats = r.sharedMaterials; for (int i = 0; i < mats.Length; i++) mats[i] = overrideMat; r.sharedMaterials = mats; }
    foreach (var lod in go.GetComponentsInChildren<LODGroup>(true)) lod.ForceLOD(0);
    Bounds b = new Bounds(go.transform.position, Vector3.one);
    bool has = false;
    foreach (var r in renderers) { if (r is ParticleSystemRenderer) continue; if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
    float radius = Mathf.Max(0.3f, b.extents.magnitude);
    var cam = pru.camera;
    cam.fieldOfView = 30f; cam.nearClipPlane = 0.01f; cam.farClipPlane = radius * 20f + 50f;
    cam.clearFlags = CameraClearFlags.SolidColor;
    cam.backgroundColor = new Color(0.32f, 0.38f, 0.45f);
    var dir = Quaternion.Euler(22f, azimuth, 0f) * Vector3.back;
    float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
    cam.transform.position = b.center + dir * dist;
    cam.transform.LookAt(b.center);
    pru.lights[0].intensity = 1.3f;
    pru.lights[0].transform.rotation = Quaternion.Euler(45f, -35f, 0f);
    pru.lights[1].intensity = 0.6f;
    pru.ambientColor = new Color(0.45f, 0.47f, 0.5f);
    pru.BeginStaticPreview(new Rect(0, 0, Tile, Tile));
    pru.Render(true, false);
    return pru.EndStaticPreview();
   } finally { pru.Cleanup(); }
  }

  // 3x5 digit font, scaled 3x.
  static readonly string[] Digits = { "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001", "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111" };
  static void DrawNumber(Texture2D tex, int x, int y, int value, Color color) {
   string s = value.ToString();
   for (int c = 0; c < s.Length; c++) {
    var pattern = Digits[s[c] - '0'];
    for (int py = 0; py < 5; py++) for (int px = 0; px < 3; px++) {
     if (pattern[py * 3 + px] != '1') continue;
     for (int sy = 0; sy < 3; sy++) for (int sx = 0; sx < 3; sx++) {
      int tx = x + c * 12 + px * 3 + sx, ty = y + (4 - py) * 3 + sy;
      if (tx >= 0 && ty >= 0 && tx < tex.width && ty < tex.height) tex.SetPixel(tx, ty, color);
     }
    }
   }
  }
 }
}

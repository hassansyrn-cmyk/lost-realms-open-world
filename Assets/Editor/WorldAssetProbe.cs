using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace LostRealmsOW {
 // Read-only inventory of candidate prefabs: renderable bounds, triangles, LODs, materials/shaders,
 // particle systems, colliders, rigs and clips. Output feeds the curated world catalog.
 public static class WorldAssetProbe {
  static readonly string[] Packs = {
   "A_piece_of_nature","Ayo Free Toon Water","B&J Studio","Blink","BrokenVector","BTM_Assets","Cartoon low poly tree","cave_troll",
   "CemeteryPack","Character_Elf","Daniel Mistage","Darth_Artisan","DungeonCharacters","Dungeon Floor Traps","Elven Weapons","Eric VFX Studio",
   "EverydayMotionPackFree","FantasyEnvironments","fantasySpider","Fantasy Chests PBR","FourEvilDragonsHP","Fox","Free medieval weapons",
   "Free Simple Dungeon Props - YP","Free Slash VFX","GabrielAguiarProductions","Horse Statue","Hovl Studio","Idyllic Fantasy Nature",
   "Inguz Media Studio","InnerverseInteractive","ithappy","Kevin Iglesias","living birds","Low Poly Fire Particles","LowpolyBakersHouse",
   "Magic Pig Games (Infinity PBR)","Mountain Terrain rocks and tree","MS_WeaponVol01","MYFG-Weapon Pack Lite","Polygrade",
   "Polylised - Medieval Desert City","PolyRonin","Polytope Studio","PurePoly","Rabbits","RedDragon 1.2","Rhino","Rhino_Jahaziel","RPGPP_LT",
   "SazenGames","Season2_Jackpot packV1.5","SimpleNaturePack","SimpleSky","Silver_Cats","SiuniaevCharacters","Skeleton","slash5-HungNguyen",
   "Snow Mountain","Symphonie","The Asset Wizard","TreasureChestMaker","Turntable_platforms","Vefects","VillagePack","Wolf_Animated",
   "DoubleL","ExplosiveLLC","FightingMotionsVolume1","Resources/VFX","Resources/Props","Resources/Gates","Resources/Characters","Resources/Islands","Resources/Stormveil","Resources/Astral"
  };

  [MenuItem("Lost Realms OW/Diagnostics/Probe World Asset Candidates")]
  public static void Run() {
   var sb = new StringBuilder();
   sb.Append("{\"prefabs\":[\n");
   bool first = true;
   int count = 0;
   var shaderNames = new Dictionary<Shader, string>();
   foreach (var pack in Packs) {
    string folder = "Assets/" + pack;
    if (!AssetDatabase.IsValidFolder(folder)) continue;
    foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder })) {
     string path = AssetDatabase.GUIDToAssetPath(guid);
     GameObject go = null;
     try { go = AssetDatabase.LoadAssetAtPath<GameObject>(path); } catch { }
     if (!go) continue;
     if (!first) sb.Append(",\n");
     first = false;
     sb.Append(Describe(path, go, shaderNames));
     if (++count % 200 == 0) { EditorUtility.UnloadUnusedAssetsImmediate(); }
    }
   }
   sb.Append("\n],\"shaders\":[\n");
   first = true;
   foreach (var kv in shaderNames) {
    if (!first) sb.Append(",\n");
    first = false;
    sb.Append(kv.Value);
   }
   sb.Append("\n]}\n");
   string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ClaudeBridge", "probe.json"));
   File.WriteAllText(outPath, sb.ToString());
   Debug.Log("[Probe] wrote " + count + " prefabs and " + shaderNames.Count + " shaders to " + outPath);
  }

  static string Esc(string s) => s == null ? "" : s.Replace("\\", "/").Replace("\"", "'").Replace("\n", " ");

  static string Describe(string path, GameObject go, Dictionary<Shader, string> shaders) {
   var sb = new StringBuilder();
   sb.Append("{\"path\":\"").Append(Esc(path)).Append("\"");
   var renderers = go.GetComponentsInChildren<Renderer>(true);
   Bounds b = new Bounds(); bool has = false;
   long tris = 0; int skinned = 0, particle = 0, missingMat = 0, meshRenderers = 0;
   var mats = new List<string>();
   foreach (var r in renderers) {
    if (r is ParticleSystemRenderer) { particle++; }
    else if (r is SkinnedMeshRenderer smr) { skinned++; if (smr.sharedMesh) tris += Tris(smr.sharedMesh); }
    else if (r is MeshRenderer) { meshRenderers++; var mf = r.GetComponent<MeshFilter>(); if (mf && mf.sharedMesh) tris += Tris(mf.sharedMesh); }
    if (!(r is ParticleSystemRenderer)) {
     var rb = BoundsInRoot(go.transform, r);
     if (rb.HasValue) { if (!has) { b = rb.Value; has = true; } else b.Encapsulate(rb.Value); }
    }
    foreach (var m in r.sharedMaterials) {
     if (!m) { missingMat++; continue; }
     string sn = m.shader ? m.shader.name : "NULL";
     string tex = m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") ? "base" : m.HasProperty("_MainTex") && m.GetTexture("_MainTex") ? "main" : "none";
     string key = Esc(m.name) + "|" + Esc(sn) + "|" + tex + "|" + m.renderQueue;
     if (!mats.Contains(key)) mats.Add(key);
     if (m.shader && !shaders.ContainsKey(m.shader)) shaders[m.shader] = DescribeShader(m.shader);
    }
   }
   sb.Append(",\"size\":[").Append(F(b.size.x)).Append(',').Append(F(b.size.y)).Append(',').Append(F(b.size.z)).Append(']');
   sb.Append(",\"center\":[").Append(F(b.center.x)).Append(',').Append(F(b.center.y)).Append(',').Append(F(b.center.z)).Append(']');
   sb.Append(",\"minY\":").Append(F(b.min.y));
   sb.Append(",\"tris\":").Append(tris).Append(",\"mr\":").Append(meshRenderers).Append(",\"smr\":").Append(skinned).Append(",\"ps\":").Append(particle);
   sb.Append(",\"missingMat\":").Append(missingMat);
   var lod = go.GetComponent<LODGroup>();
   sb.Append(",\"lod\":").Append(lod ? lod.lodCount : 0);
   sb.Append(",\"rootMesh\":").Append(go.GetComponent<MeshRenderer>() ? 1 : 0);
   var cols = go.GetComponentsInChildren<Collider>(true);
   sb.Append(",\"colliders\":\"").Append(string.Join(",", cols.Select(c => c.GetType().Name).Distinct())).Append('"');
   var monos = go.GetComponentsInChildren<MonoBehaviour>(true);
   int missingScripts = 0;
   foreach (var t in go.GetComponentsInChildren<Transform>(true)) missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
   sb.Append(",\"scripts\":\"").Append(Esc(string.Join(",", monos.Where(m => m).Select(m => m.GetType().Name).Distinct()))).Append('"');
   sb.Append(",\"missingScripts\":").Append(missingScripts);
   var animator = go.GetComponentInChildren<Animator>(true);
   var legacy = go.GetComponentInChildren<Animation>(true);
   if (animator) {
    sb.Append(",\"animator\":{\"human\":").Append(animator.isHuman ? "true" : "false").Append(",\"avatar\":").Append(animator.avatar && animator.avatar.isValid ? "true" : "false");
    var ctrl = animator.runtimeAnimatorController;
    sb.Append(",\"clips\":\"").Append(ctrl ? Esc(string.Join(",", ctrl.animationClips.Select(c => c ? c.name : "").Distinct().Take(40))) : "").Append("\"}");
   }
   if (legacy) sb.Append(",\"legacy\":\"").Append(Esc(string.Join(",", AnimationUtility.GetAnimationClips(legacy.gameObject).Select(c => c ? c.name : "").Take(40)))).Append('"');
   if (particle > 0) {
    var systems = go.GetComponentsInChildren<ParticleSystem>(true);
    float life = 0; bool loop = false;
    foreach (var s in systems) { var main = s.main; life = Mathf.Max(life, main.duration + main.startLifetime.constantMax + main.startDelay.constantMax); loop |= main.loop; }
    sb.Append(",\"fx\":{\"systems\":").Append(systems.Length).Append(",\"life\":").Append(F(life)).Append(",\"loop\":").Append(loop ? "true" : "false").Append('}');
   }
   sb.Append(",\"mats\":[").Append(string.Join(",", mats.Take(12).Select(m => "\"" + m + "\""))).Append(']');
   sb.Append('}');
   return sb.ToString();
  }

  static string DescribeShader(Shader s) {
   string path = AssetDatabase.GetAssetPath(s);
   bool urpTag = false, unlitPass = false, legacyPass = false, grab = false;
   int passes = 0;
   try {
    var data = ShaderUtil.GetShaderData(s);
    for (int si = 0; si < data.SubshaderCount; si++) {
     var sub = data.GetSubshader(si);
     for (int pi = 0; pi < sub.PassCount; pi++) {
      var pass = sub.GetPass(pi);
      passes++;
      string mode = pass.FindTagValue(new ShaderTagId("LightMode")).name;
      if (mode == "UniversalForward" || mode == "UniversalForwardOnly" || mode == "SRPDefaultUnlit" || mode == "Universal2D") urpTag = true;
      if (string.IsNullOrEmpty(mode) || mode == "Always") unlitPass = true;
      if (mode == "ForwardBase" || mode == "ForwardAdd" || mode == "Deferred" || mode == "PrepassBase") legacyPass = true;
     }
    }
   } catch { }
   if (path.EndsWith(".shader") && File.Exists(path)) {
    try { grab = File.ReadAllText(path).IndexOf("GrabPass", StringComparison.OrdinalIgnoreCase) >= 0; } catch { }
   }
   bool supported = s.isSupported;
   return "{\"name\":\"" + Esc(s.name) + "\",\"path\":\"" + Esc(path) + "\",\"passes\":" + passes + ",\"urp\":" + (urpTag ? "true" : "false") +
    ",\"unlitPass\":" + (unlitPass ? "true" : "false") + ",\"legacyPass\":" + (legacyPass ? "true" : "false") + ",\"grab\":" + (grab ? "true" : "false") +
    ",\"supported\":" + (supported ? "true" : "false") + "}";
  }

  static Bounds? BoundsInRoot(Transform root, Renderer r) {
   Mesh mesh = null;
   if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
   else { var mf = r.GetComponent<MeshFilter>(); if (mf) mesh = mf.sharedMesh; }
   if (!mesh) return null;
   var lb = mesh.bounds;
   var m = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
   Vector3 c = lb.center, e = lb.extents;
   var result = new Bounds(m.MultiplyPoint3x4(c), Vector3.zero);
   for (int i = 0; i < 8; i++) {
    var corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
    result.Encapsulate(m.MultiplyPoint3x4(corner));
   }
   return result;
  }

  static long Tris(Mesh mesh) {
   long n = 0;
   for (int i = 0; i < mesh.subMeshCount; i++) n += (long)mesh.GetIndexCount(i) / 3;
   return n;
  }

  static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
 }
}

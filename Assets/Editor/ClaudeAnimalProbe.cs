using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LostRealmsOW {
 // Read-only survey of the animal assets in the project: rig type, size, materials and every clip that
 // binds to the rig (the same compatibility rule the content preparation uses), so the preparation can
 // be pointed at the right idle / walk / run / attack / hit / death clips. Writes ClaudeBridge/out/animal_probe.txt.
 public static class ClaudeAnimalProbe {
  public static readonly string[] Candidates = {
   "Assets/ithappy/Animals_FREE/Prefabs/Kitty_001.prefab",
   "Assets/ithappy/Animals_FREE/Prefabs/Tiger_001.prefab",
   "Assets/ithappy/Animals_FREE/Prefabs/Pinguin_001.prefab",
   "Assets/ithappy/Animals_FREE/Prefabs/Horse_001.prefab",
   "Assets/ithappy/Animals_FREE/Prefabs/Deer_001.prefab",
   "Assets/ithappy/Animals_FREE/Prefabs/Dog_001.prefab",
   "Assets/ithappy/Animals_FREE/Prefabs/Chicken_001.prefab",
   "Assets/Blink/Art/Animals/Stylized/Bear/Bear_Prefabs/Bear_4.prefab",
   "Assets/Creature_Rhinoceros/Prefab/Rhinoceros.prefab",
   "Assets/Rhino/Prefab/Rhino_PBR.prefab",
   "Assets/Rhino_Jahaziel/Model/Rhino.fbx",
   "Assets/RSG_DogsPack/HDRP/Prefabs/P_GermanShepherd.prefab",
   "Assets/Wolf_Animated/Prefabs/Wolf.prefab",
   "Assets/Fox/Prefabs/Fox.prefab",
   "Assets/Rabbits/Prefabs/Rabbit 1.prefab",
   "Assets/Rabbits/Demo/Prefabs/Demo White Rabbit.prefab",
   "Assets/living birds/resources/lb_crow.prefab",
   "Assets/living birds/resources/lb_blueJay.prefab",
   "Assets/living birds/resources/lb_cardinal.prefab",
   "Assets/living birds/resources/lb_chickadee.prefab",
   "Assets/living birds/resources/lb_goldFinch.prefab",
   "Assets/living birds/resources/lb_robin.prefab",
  };

  public static void Run() {
   var sb = new StringBuilder();
   sb.AppendLine("# Animal asset probe " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
   foreach (var path in Candidates) {
    try { Probe(path, sb); }
    catch (Exception e) { sb.AppendLine("## " + path + "\nERROR " + e.Message); }
    sb.AppendLine();
   }
   Directory.CreateDirectory("ClaudeBridge/out");
   File.WriteAllText("ClaudeBridge/out/animal_probe.txt", sb.ToString());
   Debug.Log("[AnimalProbe] wrote ClaudeBridge/out/animal_probe.txt (" + Candidates.Length + " assets)");
  }

  static void Probe(string path, StringBuilder sb) {
   sb.AppendLine("## " + path);
   var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
   if (!source) { sb.AppendLine("MISSING (not imported)"); return; }
   var stage = new GameObject("probe stage") { hideFlags = HideFlags.HideAndDontSave };
   stage.SetActive(false);
   try {
    var model = UnityEngine.Object.Instantiate(source, stage.transform, false);
    var animator = model.GetComponentInChildren<Animator>(true);
    var legacy = model.GetComponentInChildren<Animation>(true);
    var skinned = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    var renderers = model.GetComponentsInChildren<Renderer>(true);
    var b = OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
    long tris = 0;
    foreach (var m in model.GetComponentsInChildren<MeshFilter>(true).Select(x => x.sharedMesh).Concat(skinned.Select(x => x.sharedMesh)).Where(x => x))
     for (int i = 0; i < m.subMeshCount; i++) tris += (long)m.GetIndexCount(i) / 3;
    sb.AppendLine($"rig: animator={(animator ? (animator.isHuman ? "humanoid" : "generic") : "none")} avatar={(animator && animator.avatar ? animator.avatar.name + (animator.avatar.isValid ? "" : " (invalid)") : "-")} optimized={(animator ? (!animator.hasTransformHierarchy).ToString() : "-")} legacyAnimation={(legacy ? legacy.GetClipCount().ToString() : "none")} controller={(animator && animator.runtimeAnimatorController ? animator.runtimeAnimatorController.name : "-")}");
    sb.AppendLine($"size at prefab scale: {b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00} m; triangles {tris}; skinned {skinned.Length}; renderers {renderers.Length}");
    var scripts = model.GetComponentsInChildren<MonoBehaviour>(true).Where(x => x).Select(x => x.GetType().Name).Distinct();
    sb.AppendLine("scripts: " + string.Join(", ", scripts));
    foreach (var r in renderers) {
     foreach (var mat in r.sharedMaterials) {
      if (!mat) { sb.AppendLine($"  material on {r.name}: NULL"); continue; }
      var props = new[] { "_BaseMap", "_MainTex", "_BaseColorMap", "_BumpMap", "_NormalMap", "_MaskMap", "_MetallicGlossMap" }.Where(p => mat.HasProperty(p) && mat.GetTexture(p)).Select(p => p + "=" + mat.GetTexture(p).name + "(" + mat.GetTexture(p).width + ")");
      sb.AppendLine($"  material on {r.name}: {mat.name} [{(mat.shader ? mat.shader.name : "no shader")}] {string.Join(" ", props)}");
     }
    }
    // Clips: own clips plus the pack library, filtered like OpenWorldContentPreparation, then checked
    // against the rig's transform paths.
    Transform bindingRoot = animator ? animator.transform : legacy ? legacy.transform : model.transform;
    string pack = path.Split('/')[1];
    var library = AssetDatabase.FindAssets("t:AnimationClip t:Model", new[] { "Assets/" + pack })
     .Select(AssetDatabase.GUIDToAssetPath).Distinct().SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<AnimationClip>()
     .Where(c => !c.name.StartsWith("__preview__")).ToArray();
    var own = AnimationUtility.GetAnimationClips(source);
    sb.AppendLine($"clips: own {own.Length}, pack library {library.Length}");
    foreach (var clip in own.Concat(library).Distinct()) {
     var bindings = AnimationUtility.GetCurveBindings(clip);
     int resolved = bindings.Count(x => string.IsNullOrEmpty(x.path) || bindingRoot.Find(x.path));
     bool compatible = clip.humanMotion ? animator && animator.isHuman : bindings.Length > 0 && resolved == bindings.Length;
     if (!compatible && resolved * 2 < bindings.Length) continue; // another species' rig
     bool rootMove = bindings.Any(x => x.propertyName.StartsWith("m_LocalPosition") && x.path.Count(ch => ch == '/') <= 1);
     sb.AppendLine($"  {(compatible ? "OK  " : "PART")} \"{clip.name}\" {clip.length:0.00}s loop={clip.isLooping} legacy={clip.legacy} human={clip.humanMotion} curves {bindings.Length} resolved {resolved}{(rootMove ? " rootmove" : "")} <- {AssetDatabase.GetAssetPath(clip)}");
    }
   } finally { UnityEngine.Object.DestroyImmediate(stage); }
  }
 }
}

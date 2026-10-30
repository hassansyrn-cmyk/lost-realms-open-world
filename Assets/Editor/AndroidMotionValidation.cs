using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LostRealmsOW {
 // Exercises player-serialized motion data, rather than relying on editor curve previews.
 public static class AndroidMotionValidation {
  public static void Run() {
   var report = new StringBuilder();
   int failures = 0;
   string output = Path.Combine(Path.GetTempPath(), "LostRealmsMotion-" + Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(output);
   try {
    var paths = new[] { "idle", "walk", "run", "fall", "jump", "attack_1" }
     .Select(s => "Assets/Resources/Animations/Aster/" + s + ".anim").ToArray();
    var manifest = BuildPipeline.BuildAssetBundles(output, new[] {
     new AssetBundleBuild { assetBundleName = "motion-check", assetNames = paths }
    }, BuildAssetBundleOptions.ForceRebuildAssetBundle, EditorUserBuildSettings.activeBuildTarget);
    if (!manifest) throw new Exception("Motion test bundle did not build.");
    var bundle = AssetBundle.LoadFromFile(Path.Combine(output, "motion-check"));
    if (!bundle) throw new Exception("Motion test bundle did not load.");
    try {
     foreach (string path in paths) {
      var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
      var packed = bundle.LoadAsset<AnimationClip>(path);
      float editorMotion = Motion(source), playerMotion = Motion(packed);
      bool pass = packed && packed.humanMotion && playerMotion > 0.001f;
      if (!pass) failures++;
      report.AppendLine($"{(pass ? "PASS" : "FAIL")} {Path.GetFileName(path)}: editor={editorMotion:F5}, packed={playerMotion:F5}, human={packed && packed.humanMotion}");
     }
    } finally { bundle.Unload(true); }
   } catch (Exception e) { failures++; report.AppendLine(e.ToString()); }
   Directory.CreateDirectory("Docs");
   File.WriteAllText("Docs/android-motion-validation.txt", report.ToString());
   Debug.Log(report.ToString());
   if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
  }

  static float Motion(AnimationClip clip) {
   if (!clip) return 0;
   using (var probe = new OpenWorldMotionLab.Probe()) {
    var bones = new[] { probe.LeftFoot, probe.RightFoot, probe.LeftHand, probe.RightHand };
    probe.Pose(clip, 0);
    var previous = bones.Select(t => probe.Local(t)).ToArray();
    float travel = 0;
    for (int frame = 1; frame <= 12; frame++) {
     probe.Pose(clip, clip.length * frame / 13f);
     for (int i = 0; i < bones.Length; i++) {
      var position = probe.Local(bones[i]);
      travel += Vector3.Distance(previous[i], position);
      previous[i] = position;
     }
    }
    return travel;
   }
  }
 }
}

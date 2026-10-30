using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace LostRealmsOW {
 // Phase 1 setup: installs the packages the open-world project depends on, at the
 // versions Unity recommends for this editor, in ONE request (a domain reload between
 // separate Add calls wipes static state and stalls a chained install).
 //   Unity -batchmode -nographics -projectPath <path> -executeMethod LostRealmsOW.Phase1Setup.InstallPackages
 public static class Phase1Setup {
  static readonly string[] Packages = {
   "com.unity.render-pipelines.universal",
   "com.unity.addressables",
   "com.unity.cinemachine",
   "com.unity.inputsystem",
   "com.unity.ai.navigation",
   "com.unity.ugui"
  };
  static AddAndRemoveRequest request;

  public static void InstallPackages() {
   request = Client.AddAndRemove(Packages, null);
   EditorApplication.update += Tick;
  }

  static void Tick() {
   if (request == null || !request.IsCompleted) return;
   EditorApplication.update -= Tick;
   Directory.CreateDirectory("Docs");
   if (request.Status == StatusCode.Success) {
    var lines = new System.Collections.Generic.List<string>();
    foreach (var p in request.Result) lines.Add("OK   " + p.name + "@" + p.version);
    File.WriteAllLines("Docs/phase1-packages.txt", lines);
    EditorApplication.Exit(0);
   } else {
    File.WriteAllText("Docs/phase1-packages.txt", "FAIL " + request.Error.message);
    EditorApplication.Exit(1);
   }
  }
 }
}

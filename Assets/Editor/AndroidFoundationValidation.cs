using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostRealmsOW {
 // Runs the actual generated scene; exits nonzero on regressions or runtime errors.
 [InitializeOnLoad]
 public static class AndroidFoundationValidation {
  const string Running = "LostRealms.AndroidFoundationValidation";
  static int stage;
  static int modalFrame;
  static ImportedContentEntry importedProbe;
  static OpenWorldNPC importedNpc;
  static double deadline, releaseAt;
  static string failures = "";
  static OpenWorldAttackButton attack;
  static PointerEventData attackPointer;
  static AndroidFoundationValidation() {
   if (SessionState.GetBool(Running, false)) Attach();
  }
  public static void Run() {
   OpenWorldContentPreparation.Prepare();
   Environment.SetEnvironmentVariable("LOST_REALMS_TEST_SAVE_DIR", Path.Combine(Path.GetTempPath(), "LostRealmsValidation", Guid.NewGuid().ToString("N")));
   SessionState.SetBool(Running, true);
   EditorSceneManager.OpenScene("Assets/Scenes/VerdantOpenWorld.unity");
   Attach();
   EditorApplication.isPlaying = true;
  }
  static void Attach() {
   EditorApplication.update -= Tick;
   EditorApplication.update += Tick;
   Application.logMessageReceived -= OnLog;
   Application.logMessageReceived += OnLog;
   deadline = EditorApplication.timeSinceStartup + 180;
  }
  static void OnLog(string message, string trace, LogType type) {
   if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failures += message + "\n";
   if (message.Contains("referenced script") && message.Contains("missing")) failures += message + "\n";
  }
  static void Require(bool condition, string message) {
   if (!condition) throw new Exception(message);
  }
  static void Tick() {
   if (EditorApplication.timeSinceStartup > deadline) { Finish($"Timed out at validation stage {stage}. Focused={Application.isFocused}, hero dead={OpenWorldManager.I?.Hero?.Dead}, grounded={OpenWorldManager.I?.Hero?.Grounded}, state={OpenWorldManager.I?.Hero?.Visual?.CurrentState}."); return; }
   if (!EditorApplication.isPlaying || !OpenWorldManager.I || !OpenWorldManager.I.Hero) return;
   try {
    var gm = OpenWorldManager.I;
    if (stage == 0) {
     Require(OpenWorldMobileHud.Instance, "HUD missing.");
     var prepared = JsonUtility.FromJson<ImportedContentManifest>(File.ReadAllText("Assets/Resources/World/ImportedContent.json"));
     var requiredEffects = new HashSet<string> { "fx_slash", "fx_slash_ember", "fx_slash_frost", "fx_sparks", "fx_dust" };
     foreach (var entry in prepared.entries) {
      if (!entry.alias.StartsWith("fx_")) continue;
      requiredEffects.Remove(entry.alias);
      var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameContent/Imported/Prefabs/" + entry.key + ".prefab");
      var particles = prefab.GetComponentsInChildren<ParticleSystem>(true);
      Require(particles.Length > 0, "Imported combat effect has no particle systems: " + entry.alias);
      Require(prefab.GetComponentsInChildren<Collider>(true).Length == 0, "Combat VFX blocks physics.");
      foreach (var particle in particles) Require(!particle.main.loop && particle.main.stopAction == ParticleSystemStopAction.None,
       "Pooled effect loops or destroys itself: " + entry.alias);
     }
     Require(requiredEffects.Count == 0, "Required imported effects were deferred: " + string.Join(", ", requiredEffects));
     Require(!OpenWorldGrounding.TryWalk(gm.ActiveTerrain, gm.Hero.transform.position, gm.ActiveTerrain.transform.position - Vector3.one, out _), "Wildlife can walk outside the terrain.");
     var terrainData = gm.ActiveTerrain.terrainData;
     Require(terrainData.terrainLayers.Length == 4, "Imported terrain surfaces are missing.");
     Require(terrainData.terrainLayers[3] == Resources.Load<TerrainLayer>("World/Terrain/Snow"), "A later terrain pass replaced the imported surfaces.");
     var weights = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
     float[] coverage = new float[4];
     for (int z = 0; z < weights.GetLength(0); z += 8) for (int x = 0; x < weights.GetLength(1); x += 8) {
      float total = 0;
      for (int i = 0; i < 4; i++) { Require(weights[z,x,i] >= 0, "Negative terrain layer weight."); total += weights[z,x,i]; coverage[i] += weights[z,x,i]; }
      Require(Mathf.Abs(total - 1) < .01f, "Terrain surface weights do not normalize.");
     }
     Require(coverage[0] > 1 && coverage[1] > 1 && coverage[3] > 1, "Grass, path or snow coverage is absent.");
     var requiredEnemies = new HashSet<string> { "enemy_949fff1745", "enemy_10b1fc0ab2", "enemy_d5e3575646" };
     foreach (var entry in prepared.entries) {
      if (entry.category != "enemy") continue;
      requiredEnemies.Remove(entry.key);
      var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameContent/Imported/Prefabs/" + entry.key + ".prefab");
      var motion = prefab ? prefab.GetComponent<OpenWorldImportedModel>() : null;
      Require(motion && motion.Idle && motion.Walk && motion.Attack && motion.Hit && motion.Death,
       "Prepared combat enemy has an incomplete animation set: " + entry.source);
      foreach (var clip in new[] { motion.Idle, motion.Walk, motion.Attack, motion.Hit, motion.Death })
       Require(clip.events.Length == 0, "Prepared enemy animation retains vendor callbacks: " + entry.source + "/" + clip.name);
     }
     Require(requiredEnemies.Count == 0, "Curated combat actors were unexpectedly deferred: " + string.Join(", ", requiredEnemies));
     foreach (string state in new[] { "run", "talk", "fall", "spear_1", "spear_2", "spear_3", "unarmed_1", "unarmed_2", "unarmed_3" }) {
      var clip = Resources.Load<AnimationClip>("Animations/Aster/" + state);
      Require(clip && clip.humanMotion && clip.events.Length == 0, "Imported humanoid motion missing or contains vendor callbacks: " + state);
      foreach (var binding in AnimationUtility.GetCurveBindings(clip)) if (binding.propertyName.StartsWith("RootT"))
       foreach (var key in AnimationUtility.GetEditorCurve(clip, binding).keys) Require(Mathf.Approximately(key.value, 0), "Aster root translation was not flattened: " + state);
     }
     var graphics = OpenWorldGraphics.Instance;
     Require(graphics, "Graphics profiles missing.");
     bool originalTier = graphics.High;
     graphics.Apply(false, false);
     var pipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
     Require(pipeline && Mathf.Approximately(pipeline.renderScale, 0.85f) && Application.targetFrameRate == 30,
      "Balanced graphics profile did not apply.");
     graphics.Apply(true, false);
     Require(Mathf.Approximately(pipeline.renderScale, 1f) && pipeline.shadowDistance == 75f && Application.targetFrameRate == 60,
      "High graphics profile did not apply.");
     graphics.Apply(originalTier, false);
     Require(gm.Camps.Count == 3 && gm.Enemies.Count >= 10, "World content failed to initialize.");
     Require(Resources.Load<AudioClip>("Audio/verdant_realm_theme"), "Forest music missing.");
     Require(Resources.Load<AudioClip>("Audio/sfx_attack_charged_1"), "Charged attack sound missing.");
     Require(!Input.simulateMouseWithTouches, "Touches still generate mouse attacks.");
     var gate = gm.Gates.Find(g => !g.Discovered);
     Require(gate, "No undiscovered gate available for test.");
     Vector3 before = gm.Hero.transform.position;
     gm.FastTravelTo(gate);
     Require(Vector3.Distance(before, gm.Hero.transform.position) < 0.001f && !gate.Discovered,
      "Undiscovered fast travel bypassed exploration.");
     Canvas.ForceUpdateCanvases();
     var surfaces = UnityEngine.Object.FindObjectsByType<OpenWorldTouchSurface>(FindObjectsInactive.Include);
     var stick = Array.Find(surfaces, s => !s.Look);
     Require(stick, "Movement stick missing.");
     var rect = (RectTransform)stick.transform;
     var e = new PointerEventData(EventSystem.current) {
      pointerId = 10, position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(64, 0, 0)))
     };
     stick.OnPointerDown(e);
     Require(gm.Hero.TouchMoveInput.x > 0.9f, "Stick does not drive movement.");
     stick.OnPointerUp(new PointerEventData(EventSystem.current) { pointerId = 11 });
     Require(gm.Hero.TouchMoveInput.x > 0.9f, "A second finger cancelled the movement finger.");
     stick.OnPointerUp(e);
     Require(gm.Hero.TouchMoveInput == Vector2.zero, "Stick stays active after release.");
     var look = Array.Find(surfaces, s => s.Look);
     float yaw = gm.CameraRig.Yaw;
     e.delta = new Vector2(20, 0);
     look.OnPointerDown(e); look.OnDrag(e); look.OnPointerUp(e);
     Require(gm.CameraRig.Yaw > yaw, "Swipe does not rotate camera.");
     attack = UnityEngine.Object.FindAnyObjectByType<OpenWorldAttackButton>(FindObjectsInactive.Include);
     gm.Hero.enabled = false;
     OpenWorldMobileHud.Instance.enabled = false;
     attack.transform.parent.gameObject.SetActive(true);
     attackPointer = new PointerEventData(EventSystem.current) { pointerId = 12 };
     attack.OnPointerDown(attackPointer); attack.OnPointerUp(attackPointer);
     Require(gm.Hero.TouchAttack && !gm.Hero.TouchCharged, "Tap does not request a light attack.");
     gm.Hero.TouchAttack = false;
     attack.OnPointerDown(attackPointer);
     releaseAt = EditorApplication.timeSinceStartup + 0.6;
     stage = 1;
    } else if (stage == 1 && EditorApplication.timeSinceStartup >= releaseAt) {
     attack.OnPointerUp(attackPointer);
     Require(gm.Hero.TouchCharged && !gm.Hero.TouchAttack, "Holding attack does not request a charged attack.");
     gm.Hero.TouchCharged = false;
     attack.OnPointerDown(attackPointer);
     attack.OnPointerExit(attackPointer);
     attack.OnPointerUp(attackPointer);
     Require(!gm.Hero.TouchAttack && !gm.Hero.TouchCharged, "Cancelled attack fires after pointer exits.");
     gm.ShowMapModal = true;
     gm.Hero.TouchJump = true;
     gm.Hero.TouchMoveInput = Vector2.one;
     gm.Hero.enabled = true;
     OpenWorldMobileHud.Instance.enabled = true;
     modalFrame = Time.frameCount;
     stage = 2;
    } else if (stage == 2 && Time.frameCount > modalFrame + 1) {
     Require(gm.Hero.TouchMoveInput == Vector2.zero && !gm.Hero.TouchJump, "Modal leaves stale hero input.");
     gm.ShowMapModal = false;
     AdventureValidation.Verify(gm);
     LegendsUIValidation.Verify(gm);
     if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) {
      CapturePreview();
      gm.ShowArsenalModal = true;
      OpenWorldMobileHud.Instance.SendMessage("Update");
      CapturePreview("Docs/legends-arsenal-preview.png");
      gm.CloseMenus();
      gm.ShowMapModal = true;
      OpenWorldMobileHud.Instance.SendMessage("Update");
      CapturePreview("Docs/legends-atlas-preview.png");
      gm.CloseMenus();
     }
     var catalog = Resources.Load<TextAsset>("World/ImportedContent");
     Require(catalog, "Prepared imported-content catalog missing.");
     var manifest = JsonUtility.FromJson<ImportedContentManifest>(catalog.text);
     importedProbe = Array.Find(manifest.entries, e => e.category == "chest");
     Require(importedProbe != null && manifest.entries.Length >= 40, "Imported selection was not prepared.");
     gm.Hero.WarpTo(importedProbe.position + Vector3.back * 3);
     releaseAt = EditorApplication.timeSinceStartup + 12;
     deadline = EditorApplication.timeSinceStartup + 240;
     stage = 3;
    } else if (stage == 3 && EditorApplication.timeSinceStartup > releaseAt) {
     var content = OpenWorldImportedContent.Instance;
     Require(content && content.FailedCount == 0, "Imported Addressables failed to load.");
     if (!content.HasInstance(importedProbe.key) || content.PendingCount > 0) return;
     Require(content.ActiveCount <= 60, "Regional instance budget exceeded.");
     if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) CapturePreview("Docs/imported-content-preview.png");
     gm.Hero.WarpTo(new Vector3(240, 0, 240));
     releaseAt = EditorApplication.timeSinceStartup + 18;
     stage = 4;
    } else if (stage == 4 && EditorApplication.timeSinceStartup > releaseAt) {
     var content = OpenWorldImportedContent.Instance;
     Require(content && content.FailedCount == 0 && !content.HasInstance(importedProbe.key), "Distant regional chest failed to unload.");
     Require(gm.Enemies.TrueForAll(e => e) && gm.Collectibles.TrueForAll(c => c), "Streaming left destroyed actors in manager registries.");
     File.WriteAllText("Docs/imported-content-validation.txt", "PASS: Prepared catalog, actual Addressables load, nearby imported cache residency, distant unload, bounded instances and registry cleanup. Editor validation only; Android performance unverified.");
     gm.CloseMenus();
     gm.Hero.ReviveAt(new Vector3(0, 0, -6));
     releaseAt = EditorApplication.timeSinceStartup + 1;
     stage = 5;
    } else if (stage == 5 && EditorApplication.timeSinceStartup > releaseAt && gm.Hero.Grounded) {
     gm.Hero.TouchJump = true;
     modalFrame = Time.frameCount;
     stage = 6;
    } else if (stage == 6 && Time.frameCount > modalFrame && !gm.Hero.TouchJump) {
     Require(!gm.Hero.Grounded && gm.Hero.Visual.CurrentState == "jump", "Takeoff replaced the jump animation with grounded locomotion.");
     importedNpc = gm.NPCs.Find(n => n && n.NpcId == "lyra");
     Require(importedNpc && importedNpc.Visual.ModelInstance.GetComponent<OpenWorldImportedModel>(), "Imported elf NPC was not adopted.");
     importedNpc.Visual.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
     gm.Hero.WarpTo(importedNpc.transform.position + Vector3.back * 5);
     gm.CameraRig.SnapToTarget();
     releaseAt = EditorApplication.timeSinceStartup + .75;
     stage = 7;
    } else if (stage == 7 && EditorApplication.timeSinceStartup > releaseAt) {
     CheckNpcGroundContact(importedNpc);
     importedNpc.Converse(gm);
     releaseAt = EditorApplication.timeSinceStartup + .75;
     stage = 8;
    } else if (stage == 8 && EditorApplication.timeSinceStartup > releaseAt) {
     if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) CapturePreview("Docs/imported-npc-animation-preview.png");
     CheckNpcGroundContact(importedNpc);
     Require(importedNpc.Visual.CurrentState == "talk", "NPC conversation did not use the talk animation.");
     File.WriteAllText("Docs/animation-validation.txt", "PASS: Nine humanoid motions, event-free clips, zero RootT keys, jump pose survives takeoff, imported elf idle/talk foot placement within tolerance. Rendered scene test; other species and combat contacts require motion review.");
     for (int i = 0; i < 12; i++) Require(OpenWorldImportedContent.Emit("fx_slash", gm.Hero.transform.position + Vector3.up, gm.Hero.transform.rotation, 1), "Imported slash effect was not loaded.");
     Require(OpenWorldImportedContent.Instance.ActiveEffectCount <= 4, "Combat VFX exceeded the simultaneous effect budget.");
     releaseAt = EditorApplication.timeSinceStartup + 7;
     stage = 9;
    } else if (stage == 9 && EditorApplication.timeSinceStartup > releaseAt) {
     Require(OpenWorldImportedContent.Instance.ActiveEffectCount == 0, "Imported effects did not return to their pool.");
     foreach (string id in new[] { "brom", "rowan" }) {
      var npc = gm.NPCs.Find(n => n && n.NpcId == id);
      Require(npc && npc.Visual.ModelInstance.GetComponent<OpenWorldImportedModel>(), "Village NPC model was not adopted: " + id);
      npc.Visual.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
     }
     gm.Hero.WarpTo(new Vector3(-90, 0, -75));
     gm.CameraRig.SnapToTarget();
     releaseAt = EditorApplication.timeSinceStartup + 12;
     stage = 10;
    } else if (stage == 10 && EditorApplication.timeSinceStartup > releaseAt) {
     foreach (string id in new[] { "brom", "rowan" }) CheckNpcGroundContact(gm.NPCs.Find(n => n && n.NpcId == id));
     int buildings = 0;
     foreach (var entry in JsonUtility.FromJson<ImportedContentManifest>(File.ReadAllText("Assets/Resources/World/ImportedContent.json")).entries) {
      if (entry.category != "building" || !OpenWorldImportedContent.Instance.HasInstance(entry.key)) continue;
      buildings++;
      var root = GameObject.Find("Imported " + entry.key);
      Require(root && root.GetComponentInChildren<Collider>(), "Streamed building has no collision.");
      Require(root.GetComponentsInChildren<Rigidbody>().Length == 0, "Building retains demo rigidbody physics.");
     }
     Require(buildings > 0, "Village building checks had no loaded buildings.");
     if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) CapturePreview("Docs/world-polish-village-preview.png");
     Require(OpenWorldImportedContent.Emit("fx_slash_ember", gm.Hero.transform.position + gm.Hero.transform.forward * 2 + Vector3.up, gm.Hero.transform.rotation, 1), "Authored fire slash unavailable.");
     releaseAt = EditorApplication.timeSinceStartup + .18;
     stage = 11;
    } else if (stage == 11 && EditorApplication.timeSinceStartup > releaseAt) {
     if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) CapturePreview("Docs/world-polish-combat-preview.png");
     File.WriteAllText("Docs/world-polish-validation.txt", "PASS: Four imported terrain layers with normalized grass/path/snow coverage; required imported slash/fire/water/sparks/dust prefabs with collision-free non-looping particles; actual Addressables effect load, four-effect saturation and return to pool; terrain boundary rejection; adopted Brom/Rowan models and ground contact; loaded village building collision without demo rigidbodies. Existing scene/UI/save/animation checks completed. Full species/contact-frame and device performance review remain pending.");
     Finish(failures);
    }
   } catch (Exception e) { Finish(e.ToString() + "\n" + failures); }
  }
  static void CheckNpcGroundContact(OpenWorldNPC npc) {
   float minimum = float.PositiveInfinity;
   var mesh = new Mesh();
   try {
    foreach (var skin in npc.Visual.ModelInstance.GetComponentsInChildren<SkinnedMeshRenderer>()) {
     skin.BakeMesh(mesh);
     foreach (var vertex in mesh.vertices) minimum = Mathf.Min(minimum, skin.transform.TransformPoint(vertex).y);
    }
   } finally { UnityEngine.Object.DestroyImmediate(mesh); }
   float offset = minimum - npc.transform.position.y;
   Require(offset > -.35f && offset < .6f, "Imported NPC ground contact is invalid: " + offset + "m in " + npc.Visual.CurrentState);
  }
  static void CapturePreview(string path = "Docs/android-foundation-preview.png") {
   var canvas = OpenWorldMobileHud.Instance.GetComponent<Canvas>();
   var camera = Camera.main;
   var texture = RenderTexture.GetTemporary(Screen.width, Screen.height, 24);
   RenderTexture previous = RenderTexture.active;
   var oldTarget = camera.targetTexture;
   var oldMode = canvas.renderMode;
   try {
    OpenWorldMobileHud.Instance.SendMessage("Update");
    attack.transform.parent.gameObject.SetActive(!OpenWorldManager.I.IsModalOpen);
    canvas.renderMode = RenderMode.ScreenSpaceCamera;
    canvas.worldCamera = camera;
    canvas.planeDistance = 0.5f;
    camera.targetTexture = texture;
    Canvas.ForceUpdateCanvases();
    camera.Render();
    RenderTexture.active = texture;
    var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
    image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
    image.Apply();
    File.WriteAllBytes(path, image.EncodeToPNG());
    UnityEngine.Object.DestroyImmediate(image);
   } finally {
    camera.targetTexture = oldTarget;
    canvas.renderMode = oldMode;
    RenderTexture.active = previous;
    RenderTexture.ReleaseTemporary(texture);
   }
  }
  static void Finish(string error) {
   SessionState.SetBool(Running, false);
   EditorApplication.update -= Tick;
   Application.logMessageReceived -= OnLog;
   Directory.CreateDirectory("Docs");
   File.WriteAllText("Docs/android-foundation-validation.txt", string.IsNullOrEmpty(error)
    ? "PASS: Generated Verdant scene, HUD, Balanced/High graphics profiles, audio resources, discovery gate, independent touch ownership, movement release, camera swipe, tap and held attacks, cancelled attack, modal input reset. Adventure tests passed: staged quest, guardian gate, one-time loot/reward, restored world state, save roundtrip, interrupted-write recovery, unreadable-save protection. Unity 6000.6.0f1 editor play mode. Device performance and physical multi-touch remain unverified."
    : "FAIL\n" + error);
   EditorApplication.Exit(string.IsNullOrEmpty(error) ? 0 : 1);
  }
 }
}

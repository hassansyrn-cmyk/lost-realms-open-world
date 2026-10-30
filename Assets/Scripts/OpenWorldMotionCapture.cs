#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace LostRealmsOW {
 // Editor-only motion review in the live game: plays a timed input script on the hero and records a
 // contact sheet of evenly spaced frames at a fixed 30 fps game clock, from a side, front or follow view.
 // Bridge:  call LostRealmsOW.OpenWorldMotionCapture.Sequence <name> <frames> <every> <view> <script>
 //   view: side | front | back | follow | high
 //   script: comma separated "time:command" (seconds from start). Commands: move x y, sprint 0|1,
 //   attack, charged, spell, burst, dash, jump, glide [vy], parry, element n, fill, weapon id,
 //   spawn role [dist], wall (face a climbable wall), water (into deep water), prop (next to clutter), face yaw,
 //   play state [speed] (any baked state as an action), knockdown, clear, spawnprop key [dist]
 //   views also: pair (hero and the nearest foe, following a thrown body), left and quarter (riding, from
 //   the near side), wide (a high view far behind, for herds and a whistled horse arriving)
 //   animal commands: mount, dismount, whistle, interact, approach <species> <distance>, pose
 // Example: call LostRealmsOW.OpenWorldMotionCapture.Sequence dash 16 2 side 0:move 0 1,0.6:dash
 public sealed class OpenWorldMotionCapture : MonoBehaviour {
  const int TileW = 360, TileH = 300;
  public static bool Busy { get; private set; }
  // "subject <name>": the animal the subject view frames (nearest live wildlife whose name contains it).
  static string subject = "";

  public static void Sequence(string arg) {
   if (Busy || string.IsNullOrEmpty(arg)) return;
   var p = arg.Trim().Split(new[] { ' ' }, 5);
   if (p.Length < 4) { Debug.LogError("[MotionCapture] usage: name frames every view [script]"); return; }
   var go = new GameObject("MotionCapture");
   var mc = go.AddComponent<OpenWorldMotionCapture>();
   mc.StartCoroutine(mc.Run(p[0], int.Parse(p[1]), int.Parse(p[2]), p[3], p.Length > 4 ? p[4] : ""));
  }

  IEnumerator Run(string name, int frames, int every, string view, string script) {
   Busy = true;
   var gm = OpenWorldManager.I;
   var hero = gm ? gm.Hero : null;
   if (!hero) { Busy = false; Destroy(gameObject); yield break; }
   OpenWorldHero.DebugAlwaysFocused = true;
   int originalWeapon = hero.EquippedWeaponId;
   var steps = new List<(float t, string cmd)>();
   foreach (var part in script.Split(',')) {
    int colon = part.IndexOf(':');
    if (colon <= 0) continue;
    if (float.TryParse(part.Substring(0, colon), NumberStyles.Float, CultureInfo.InvariantCulture, out float t)) steps.Add((t, part.Substring(colon + 1).Trim()));
   }
   steps.Sort((a, b) => a.t.CompareTo(b.t));
   var cam = new GameObject("CaptureCam").AddComponent<Camera>();
   var main = Camera.main;
   if (main) { cam.CopyFrom(main); cam.fieldOfView = 42f; }
   var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
   if (data) data.renderPostProcessing = true;
   cam.enabled = false;
   var rt = new RenderTexture(TileW, TileH, 24, RenderTextureFormat.DefaultHDR) { antiAliasing = 4 };
   var ldr = new RenderTexture(TileW, TileH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
   var tiles = new List<Color32[]>();
   var stamps = new List<float>();
   Time.captureFramerate = 30;
   float start = Time.time;
   int next = 0, frame = 0;
   while (tiles.Count < frames) {
    float now = Time.time - start;
    while (next < steps.Count && steps[next].t <= now + 0.0001f) { Apply(hero, steps[next].cmd); next++; }
    yield return new WaitForEndOfFrame();
    if (frame++ % every != 0) continue;
    Place(cam, hero.transform, view);
    cam.targetTexture = rt;
    cam.Render();
    cam.targetTexture = null;
    Graphics.Blit(rt, ldr);
    var prev = RenderTexture.active;
    RenderTexture.active = ldr;
    var tex = new Texture2D(TileW, TileH, TextureFormat.RGB24, false);
    tex.ReadPixels(new Rect(0, 0, TileW, TileH), 0, 0);
    tex.Apply();
    RenderTexture.active = prev;
    tiles.Add(tex.GetPixels32());
    stamps.Add(now);
    Destroy(tex);
   }
   Time.captureFramerate = 0;
   hero.TouchMoveInput = Vector2.zero;
   hero.TouchSprintHeld = false;
   if (hero.EquippedWeaponId != originalWeapon) hero.EquipWeapon(originalWeapon);
   OpenWorldHero.DebugAlwaysFocused = false;
   int cols = Mathf.Min(8, tiles.Count), rows = Mathf.CeilToInt(tiles.Count / (float)cols);
   var sheet = new Texture2D(cols * TileW, rows * TileH, TextureFormat.RGB24, false);
   for (int i = 0; i < tiles.Count; i++) {
    int cx = (i % cols) * TileW, cy = (rows - 1 - i / cols) * TileH;
    sheet.SetPixels32(cx, cy, TileW, TileH, tiles[i]);
   }
   sheet.Apply();
   string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ClaudeBridge", "shots"));
   Directory.CreateDirectory(dir);
   File.WriteAllBytes(Path.Combine(dir, "seq_" + name + ".jpg"), sheet.EncodeToJPG(86));
   File.WriteAllText(Path.Combine(dir, "seq_" + name + ".txt"), string.Join(" ", stamps.ConvertAll(s => s.ToString("0.00", CultureInfo.InvariantCulture))) + "\n" + script + "\n");
   Destroy(sheet);
   if (RenderTexture.active == rt || RenderTexture.active == ldr) RenderTexture.active = null;
   rt.Release(); ldr.Release(); Destroy(rt); Destroy(ldr);
   Destroy(cam.gameObject);
   Debug.Log("[MotionCapture] " + name + ": " + tiles.Count + " frames");
   Busy = false;
   Destroy(gameObject);
  }

  static void Place(Camera cam, Transform hero, string view) {
   Vector3 focus = hero.position + Vector3.up * 1.0f;
   Vector3 offset;
   switch (view) {
    case "front": offset = hero.forward * 3.6f + Vector3.up * 0.35f; break;
    case "back": offset = -hero.forward * 4.2f + Vector3.up * 1.0f; break;
    case "high": offset = -hero.forward * 5f + hero.right * 3f + Vector3.up * 4.5f; break;
    // Riding reviews: from the near (left, mounting) side, and a three-quarter view from the front left.
    case "left": offset = -hero.right * 4.8f + Vector3.up * 0.7f; break;
    case "quarter": offset = -hero.right * 4.2f + hero.forward * 3.2f + Vector3.up * 1.1f; break;
    case "wide": offset = -hero.forward * 9f + hero.right * 5f + Vector3.up * 5.5f; break;
    case "subject": {
      // A close side view of the chosen animal (birds and small game, which a hero camera cannot show).
      OpenWorldWildlife best = null; float bd = 45f * 45f;
      foreach (var w in FindObjectsByType<OpenWorldWildlife>(FindObjectsSortMode.None)) {
       if (!w || !w.isActiveAndEnabled || (subject.Length > 0 && w.name.IndexOf(subject, System.StringComparison.OrdinalIgnoreCase) < 0)) continue;
       float d = (w.transform.position - hero.position).sqrMagnitude;
       if (d < bd) { bd = d; best = w; }
      }
      if (best) {
       var b = OpenWorldMaterialHelper.ComputeRendererBounds(best.transform);
       float size = Mathf.Max(0.15f, Mathf.Max(b.size.y, Mathf.Max(b.size.x, b.size.z)));
       Vector3 c = b.center;
       Vector3 side = Vector3.Cross(Vector3.up, best.transform.forward).normalized;
       cam.transform.position = c + side * Mathf.Max(1.1f, size * 3.2f) + Vector3.up * size * 0.6f;
       cam.transform.LookAt(c);
       return;
      }
      offset = -hero.forward * 4.2f + Vector3.up * 1.0f; break;
     }
    case "pair": {
      // Hero and the nearest foe (or its thrown body) side by side in frame.
      var foe = NearestFoe(hero, 22f, out Vector3 fp);
      if (foe) {
       Vector3 a = hero.position, ab = fp - a; ab.y = 0f;
       float sep = ab.magnitude;
       Vector3 across = sep > 0.1f ? Vector3.Cross(Vector3.up, ab / sep) : hero.right;
       Vector3 mid = (a + fp) * 0.5f + Vector3.up * 0.9f;
       cam.transform.position = mid + across * Mathf.Max(4.4f, sep * 1.3f) + Vector3.up * 0.9f;
       cam.transform.LookAt(mid);
       return;
      }
      offset = hero.right * 3.8f + Vector3.up * 0.45f; break;
     }
    case "follow": {
      var main = Camera.main;
      if (main) { cam.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation); cam.fieldOfView = main.fieldOfView; return; }
      offset = -hero.forward * 5f + Vector3.up * 2f; break;
     }
    default: offset = hero.right * 3.8f + Vector3.up * 0.45f; break;
   }
   cam.transform.position = focus + offset;
   cam.transform.LookAt(focus);
  }

  // Nearest foe within range (dying ones included); fp is its hips, which follow a thrown ragdoll.
  static OpenWorldEnemy NearestFoe(Transform hero, float range, out Vector3 fp) {
   fp = Vector3.zero;
   var gm = OpenWorldManager.I;
   if (!gm) return null;
   OpenWorldEnemy best = null; float bd = range * range;
   foreach (var e in gm.Enemies) {
    if (!e) continue;
    Vector3 p = e.transform.position;
    var anim = e.Visual ? e.Visual.Animator : null;
    if (anim && anim.isHuman) { var hips = anim.GetBoneTransform(HumanBodyBones.Hips); if (hips) p = hips.position - Vector3.up * 0.9f; }
    float d = (p - hero.position).sqrMagnitude;
    if (d < bd) { bd = d; best = e; fp = p; }
   }
   return best;
  }

  static void Apply(OpenWorldHero hero, string cmd) {
   var p = cmd.Split(' ');
   switch (p[0]) {
    case "move": hero.TouchMoveInput = new Vector2(F(p, 1), F(p, 2)); break;
    case "sprint": hero.TouchSprintHeld = F(p, 1) > 0.5f; break;
    case "attack": hero.TouchAttack = true; break;
    case "charged": hero.TouchCharged = true; break;
    case "spell": hero.TouchSpell = true; break;
    case "burst": hero.TouchBurst = true; break;
    case "dash": hero.TouchDodge = true; break;
    case "jump": hero.TouchJump = true; break;
    case "parry": hero.TouchParry = true; break;
    case "glide": hero.LaunchIntoGlide(F(p, 1, 14f)); break;
    case "element": hero.RestoreElement((int)F(p, 1)); break;
    case "fill": hero.Aether = hero.MaxAether; hero.Stamina = hero.MaxStamina; break;
    case "weapon": hero.EquipWeapon((int)F(p, 1)); break;
    case "spawn": OpenWorldEnemy.DebugSpawn(cmd.Length > 6 ? cmd.Substring(6) : ""); break;
    case "spawnprop": OpenWorldRealm.DebugSpawnProp(cmd.Length > 10 ? cmd.Substring(10) : ""); break;
    case "wall": OpenWorldHero.DebugFaceWall(""); break;
    case "water": OpenWorldHero.DebugFindWater(""); break;
    case "prop": OpenWorldPhysicsProps.DebugFindProp(""); break;
    case "face": hero.transform.rotation = Quaternion.Euler(0f, F(p, 1), 0f); break;
    case "play": hero.DebugPlayState(cmd.Length > 5 ? cmd.Substring(5) : "idle"); break;
    case "knockdown": hero.DebugKnockdown(); break;
    case "fwd": {
      // Moves straight along her current facing: move input is relative to the gameplay camera, so that
      // camera is swung behind her first (the capture lens is a separate camera).
      var rig = OpenWorldManager.I ? OpenWorldManager.I.CameraRig : null;
      if (rig) rig.Yaw = hero.transform.eulerAngles.y;
      hero.TouchMoveInput = new Vector2(0f, F(p, 1, 1f));
      break;
     }
    case "weaken": {
      // Nearest foe down to a sliver of health, so the next blow kills.
      var foe = NearestFoe(hero.transform, 12f, out _);
      if (foe) foe.Health = Mathf.Min(foe.Health, F(p, 1, 0.5f));
      break;
     }
    case "lift": hero.DebugLift(F(p, 1, 6f)); break;
    case "mount": {
      // Nearest horse within 30 m: stand beside it and vault on.
      OpenWorldAnimalInteraction best = null; float bestD = 900f;
      foreach (var a in FindObjectsByType<OpenWorldAnimalInteraction>(FindObjectsSortMode.None)) {
       if (!a.Horse || a.Dead || a.Rider) continue;
       float d = (a.transform.position - hero.transform.position).sqrMagnitude;
       if (d < bestD) { bestD = d; best = a; }
      }
      if (best) { hero.WarpTo(best.transform.position - best.transform.right * 1.5f); hero.transform.rotation = best.transform.rotation; hero.EnterMount(best); }
      break;
     }
    case "dismount": hero.LeaveMount(); break;
    case "whistle": OpenWorldAnimalInteraction.CallHorse(hero); break;
    case "interact": OpenWorldManager.I?.Interact(); break;
    case "approach": OpenWorldPopulation.DebugApproach(cmd.Length > 9 ? cmd.Substring(9) : ""); break;
    case "subject": subject = cmd.Length > 8 ? cmd.Substring(8).Trim() : ""; break;
    case "faceopen": {
      // Turns her (or her horse) toward the most open ground: 16 directions, rays at knee and chest height.
      Transform t = hero.IsMounted && hero.Mount ? hero.Mount.transform : hero.transform;
      float bestYaw = t.eulerAngles.y, best = -1f;
      for (int i = 0; i < 16; i++) {
       float yaw = i * 22.5f;
       Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
       float clear = 40f;
       foreach (float h in new[] { 0.6f, 1.6f })
        if (Physics.Raycast(t.position + Vector3.up * h, dir, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(t) && !hit.transform.GetComponentInParent<OpenWorldHero>())
         clear = Mathf.Min(clear, hit.distance);
       if (clear > best) { best = clear; bestYaw = yaw; }
      }
      t.rotation = Quaternion.Euler(0f, bestYaw, 0f);
      if (!hero.IsMounted) hero.transform.rotation = t.rotation;
      var cam = OpenWorldManager.I ? OpenWorldManager.I.CameraRig : null;
      if (cam) { cam.Yaw = bestYaw; cam.SnapToTarget(); }
      break;
     }
    case "pose": Debug.Log("[Capture] pose " + hero.DebugTraversalInfo() + " | " + (hero.Visual ? hero.Visual.DebugPoseInfo() : "no visual")); break;
    case "clear": {
      // Removes foes within 40 m (left over from an earlier review sequence).
      var gm = OpenWorldManager.I;
      if (gm) foreach (var e in new List<OpenWorldEnemy>(gm.Enemies)) if (e && (e.transform.position - hero.transform.position).sqrMagnitude < 1600f) { gm.Enemies.Remove(e); Destroy(e.gameObject); }
      break;
     }
    default: OpenWorldHero.DebugAction(cmd); break;
   }
  }

  static float F(string[] p, int i, float fallback = 0f) => p.Length > i && float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
 }
}
#endif

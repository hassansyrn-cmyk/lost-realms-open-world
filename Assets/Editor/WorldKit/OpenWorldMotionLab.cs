using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LostRealmsOW {
 // Shared by the Phase 1 motion baker and the animation review sheets, so what is reviewed is exactly
 // what gets baked: a copy of the source clip with neutral root settings (rotation, height and
 // position baked into the pose, no height-from-feet, no level offset) and every RootT curve
 // flattened to 0. Measurements run on the Aster prefab inside a preview scene. Source packs and
 // baked clips are only read.
 public static class OpenWorldMotionLab {
  public const string AsterPrefab = "Assets/Resources/Characters/Aster.prefab";
  public const float MetaRate = 30f;

  public static AnimationClip LoadClip(string path, string name) {
   if (string.IsNullOrEmpty(path)) return null;
   if (path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)) return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
   var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToList();
   if (!string.IsNullOrEmpty(name)) return clips.FirstOrDefault(c => c.name == name);
   return clips.FirstOrDefault(c => c.name != "tpose") ?? clips.FirstOrDefault();
  }

  // Copy with neutral root settings, optionally cropped to [start, end] seconds of the source.
  // keepY leaves RootT.y in place: measures body height for Aster (never baked for her) and bakes the
  // shared enemy reactions, which keep their root height like the other enemy clips.
  public static AnimationClip BakeCopy(AnimationClip source, bool keepY, float start = 0f, float end = -1f) {
   var clip = UnityEngine.Object.Instantiate(source);
   var s = AnimationUtility.GetAnimationClipSettings(clip);
   s.keepOriginalOrientation = true; s.keepOriginalPositionY = true; s.keepOriginalPositionXZ = true;
   s.loopBlendOrientation = true; s.loopBlendPositionY = true; s.loopBlendPositionXZ = true;
   s.heightFromFeet = false; s.level = 0f; s.orientationOffsetY = 0f;
   AnimationUtility.SetAnimationClipSettings(clip, s);
   float length = source.length;
   bool crop = start > 0.0001f || (end > 0f && end < length - 0.0001f);
   float a = Mathf.Clamp(start, 0f, length), b = end > 0f ? Mathf.Clamp(end, a + 0.05f, length) : length;
   foreach (var binding in AnimationUtility.GetCurveBindings(clip)) {
    bool root = binding.propertyName.StartsWith("RootT", StringComparison.Ordinal);
    if (root && !(keepY && binding.propertyName == "RootT.y")) {
     AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, crop ? b - a : length, 0f));
     continue;
    }
    if (!crop) continue;
    var curve = AnimationUtility.GetEditorCurve(clip, binding);
    if (curve == null) continue;
    AnimationUtility.SetEditorCurve(clip, binding, Crop(curve, a, b));
   }
   AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
   return clip;
  }

  static AnimationCurve Crop(AnimationCurve curve, float a, float b) {
   var keys = new List<Keyframe> { new Keyframe(0f, curve.Evaluate(a)) };
   foreach (var k in curve.keys) if (k.time > a + 0.0005f && k.time < b - 0.0005f) keys.Add(new Keyframe(k.time - a, k.value, k.inTangent, k.outTangent));
   keys.Add(new Keyframe(b - a, curve.Evaluate(b)));
   var result = new AnimationCurve(keys.ToArray());
   // Endpoint tangents follow the neighbouring key so the cut does not introduce a kink.
   for (int i = 0; i < result.length; i += Mathf.Max(1, result.length - 1)) {
    if (result.length > 1) AnimationUtility.SetKeyLeftTangentMode(result, i, AnimationUtility.TangentMode.ClampedAuto);
    if (result.length > 1) AnimationUtility.SetKeyRightTangentMode(result, i, AnimationUtility.TangentMode.ClampedAuto);
   }
   return result;
  }

  // Aster in a preview scene with a manual PlayableGraph; Pose() evaluates a clip at a time.
  public sealed class Probe : IDisposable {
   public readonly PreviewRenderUtility Pru;
   public readonly GameObject Go;
   public readonly Animator Animator;
   public readonly Transform Hips, LeftFoot, RightFoot, LeftToes, RightToes, LeftHand, RightHand, Head, Chest;
   readonly Transform[] contactBones;
   readonly SkinnedMeshRenderer[] skins;
   readonly Mesh[] baked;
   PlayableGraph graph;
   AnimationPlayableOutput output;
   readonly Dictionary<AnimationClip, AnimationClipPlayable> playables = new Dictionary<AnimationClip, AnimationClipPlayable>();
   AnimationClip currentClip;

   public Probe() {
    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AsterPrefab);
    if (!prefab) throw new Exception("Aster prefab missing at " + AsterPrefab);
    Pru = new PreviewRenderUtility();
    Go = Pru.InstantiatePrefabInScene(prefab);
    foreach (var lod in Go.GetComponentsInChildren<LODGroup>(true)) lod.ForceLOD(0);
    Animator = Go.GetComponentInChildren<Animator>(true);
    if (!Animator || !Animator.isHuman) throw new Exception("Aster prefab has no humanoid animator");
    Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    Animator.applyRootMotion = false;
    Hips = Animator.GetBoneTransform(HumanBodyBones.Hips);
    LeftFoot = Animator.GetBoneTransform(HumanBodyBones.LeftFoot);
    RightFoot = Animator.GetBoneTransform(HumanBodyBones.RightFoot);
    LeftToes = Animator.GetBoneTransform(HumanBodyBones.LeftToes) ?? LeftFoot;
    RightToes = Animator.GetBoneTransform(HumanBodyBones.RightToes) ?? RightFoot;
    LeftHand = Animator.GetBoneTransform(HumanBodyBones.LeftHand);
    RightHand = Animator.GetBoneTransform(HumanBodyBones.RightHand);
    Head = Animator.GetBoneTransform(HumanBodyBones.Head);
    Chest = Animator.GetBoneTransform(HumanBodyBones.Chest) ?? Animator.GetBoneTransform(HumanBodyBones.Spine);
    contactBones = new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
     HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
     HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes }
     .Select(b => Animator.GetBoneTransform(b)).Where(t => t).ToArray();
    // A preview render does not re-skin the mesh between poses (only the root bone moves), so frames are
    // rendered from CPU-baked meshes instead of the live skinned renderers.
    skins = Go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    baked = new Mesh[skins.Length];
    foreach (var skin in skins) skin.forceRenderingOff = true;
    graph = PlayableGraph.Create("MotionLab");
    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
    output = AnimationPlayableOutput.Create(graph, "out", Animator);
    graph.Play();
   }

   public void Pose(AnimationClip clip, float t) {
    if (!playables.TryGetValue(clip, out var cp) || !cp.IsValid()) {
     cp = AnimationClipPlayable.Create(graph, clip);
     cp.SetApplyFootIK(false);
     playables[clip] = cp;
    }
    if (clip != currentClip) { output.SetSourcePlayable(cp); currentClip = clip; }
    cp.SetTime(Mathf.Clamp(t, 0f, Mathf.Max(0f, clip.length - 0.0005f)));
    graph.Evaluate(0f);
   }

   // Drop cached playables (call before destroying the clips they wrap).
   public void Forget() {
    foreach (var cp in playables.Values) if (cp.IsValid()) cp.Destroy();
    playables.Clear();
    currentClip = null;
   }

   public Vector3 Local(Transform t) => Go.transform.InverseTransformPoint(t.position);

   // Renders the current pose (call after Pose) into a texture owned by the caller.
   public Texture2D RenderFrame(int size) {
    Pru.BeginStaticPreview(new Rect(0, 0, size, size));
    for (int i = 0; i < skins.Length; i++) {
     var skin = skins[i];
     if (!skin || !skin.sharedMesh || !skin.gameObject.activeInHierarchy) continue;
     if (!baked[i]) baked[i] = new Mesh { name = "MotionLabBake" };
     skin.BakeMesh(baked[i], true);
     var matrix = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
     var mats = skin.sharedMaterials;
     for (int sub = 0; sub < baked[i].subMeshCount; sub++) {
      var mat = mats.Length == 0 ? null : mats[Mathf.Min(sub, mats.Length - 1)];
      if (mat) Pru.DrawMesh(baked[i], matrix, mat, sub);
     }
    }
    Pru.Render(true, false);
    return Pru.EndStaticPreview();
   }

   // Lowest point of the body above the ground plane, with rough flesh/sole margins.
   public float Lowest() {
    float min = 99f;
    foreach (var b in contactBones) {
     float margin = b == LeftToes || b == RightToes || b == LeftFoot || b == RightFoot ? 0.09f : 0.1f;
     min = Mathf.Min(min, Local(b).y - margin);
    }
    return min;
   }

   public void Dispose() {
    Forget();
    foreach (var m in baked) if (m) UnityEngine.Object.DestroyImmediate(m);
    if (graph.IsValid()) graph.Destroy();
    Pru.Cleanup();
   }
  }

  [Serializable] public sealed class Measure {
   public float length;
   public bool loop;
   public float rhPeak, rhPeakSpeed, lhPeak, lhPeakSpeed;
   public float[] strikes = Array.Empty<float>();   // seconds: right-hand speed peaks above 55% of the max
   public float hipsMin, hipsMax, feetMin, feetMax;
   public float strideSpeed;                          // planted-foot backward speed (m/s) for locomotion
   public float leftPlant = -1f;                      // normalized time the left foot plants
   public float heightRef;                            // body-height reference used for the offsets
   public float[] height = Array.Empty<float>();      // body height offset (m) at MetaRate, relative to heightRef
   public float[] lowest = Array.Empty<float>();      // lowest body point (m, flattened pose) at MetaRate
  }

  // flat and keepY must be BakeCopy results of the same source.
  public static Measure Analyse(Probe probe, AnimationClip flat, AnimationClip keepY, bool loop) {
   var m = new Measure { length = flat.length, loop = loop };
   float len = Mathf.Max(0.05f, flat.length);
   int samples = Mathf.Clamp(Mathf.CeilToInt(len * 60f), 8, 1200);
   float dt = len / samples;
   var raw = new float[samples + 1];
   var rhSpeed = new float[samples + 1];
   Vector3 prevR = Vector3.zero, prevL = Vector3.zero, prevLF = Vector3.zero, prevRF = Vector3.zero;
   var footMinL = 9f; var footMinR = 9f;
   var lfY = new float[samples + 1]; var rfY = new float[samples + 1];
   var lfV = new float[samples + 1]; var rfV = new float[samples + 1];
   m.hipsMin = 9; m.hipsMax = -9; m.feetMin = 9; m.feetMax = -9;
   var low = new float[samples + 1];
   for (int i = 0; i <= samples; i++) {
    float t = len * i / samples;
    probe.Pose(flat, t);
    Vector3 r = probe.Local(probe.RightHand), l = probe.Local(probe.LeftHand);
    Vector3 lf = probe.Local(probe.LeftToes), rf = probe.Local(probe.RightToes);
    float hy = probe.Local(probe.Hips).y;
    if (i > 0) {
     float sr = (r - prevR).magnitude / dt, sl = (l - prevL).magnitude / dt;
     rhSpeed[i] = sr;
     if (sr > m.rhPeakSpeed) { m.rhPeakSpeed = sr; m.rhPeak = t; }
     if (sl > m.lhPeakSpeed) { m.lhPeakSpeed = sl; m.lhPeak = t; }
     lfV[i] = (lf.z - prevLF.z) / dt; rfV[i] = (rf.z - prevRF.z) / dt;
    }
    prevR = r; prevL = l; prevLF = lf; prevRF = rf;
    lfY[i] = lf.y; rfY[i] = rf.y;
    footMinL = Mathf.Min(footMinL, lf.y); footMinR = Mathf.Min(footMinR, rf.y);
    m.hipsMin = Mathf.Min(m.hipsMin, hy); m.hipsMax = Mathf.Max(m.hipsMax, hy);
    float fy = Mathf.Min(lf.y, rf.y);
    m.feetMin = Mathf.Min(m.feetMin, fy); m.feetMax = Mathf.Max(m.feetMax, fy);
    low[i] = probe.Lowest();
    probe.Pose(keepY, t);
    raw[i] = probe.Local(probe.Hips).y - hy;
   }
   // Strike frames: local maxima of right-hand speed above 55% of the peak, at least 0.18 s apart.
   var strikes = new List<float>();
   float threshold = m.rhPeakSpeed * 0.55f;
   for (int i = 2; i < samples - 1; i++) {
    if (rhSpeed[i] < threshold || rhSpeed[i] < rhSpeed[i - 1] || rhSpeed[i] < rhSpeed[i + 1]) continue;
    float t = len * i / samples;
    if (strikes.Count > 0 && t - strikes[strikes.Count - 1] < 0.18f) {
     int j = Mathf.RoundToInt(strikes[strikes.Count - 1] / len * samples);
     if (rhSpeed[i] > rhSpeed[j]) strikes[strikes.Count - 1] = t;
     continue;
    }
    strikes.Add(t);
   }
   m.strikes = strikes.ToArray();
   // Stride: a planted foot slides backward at the speed the ground should move under it.
   var planted = new List<float>();
   for (int i = 1; i <= samples; i++) {
    if (lfY[i] < footMinL + 0.025f) planted.Add(-lfV[i]);
    if (rfY[i] < footMinR + 0.025f) planted.Add(-rfV[i]);
   }
   if (planted.Count > 4) { planted.Sort(); m.strideSpeed = Mathf.Max(0f, planted[planted.Count / 2]); }
   for (int i = 1; i <= samples; i++) {
    bool down = lfY[i] < footMinL + 0.025f, wasDown = lfY[i - 1] < footMinL + 0.025f;
    if (down && !wasDown) { m.leftPlant = (float)i / samples; break; }
   }
   // Height offsets and lowest points at MetaRate (the caller chooses heightRef).
   int count = Mathf.Max(2, Mathf.CeilToInt(len * MetaRate) + 1);
   m.height = new float[count];
   m.lowest = new float[count];
   for (int k = 0; k < count; k++) {
    float f = Mathf.Clamp01(k / MetaRate / len) * samples;
    int i0 = Mathf.FloorToInt(f), i1 = Mathf.Min(samples, i0 + 1);
    float w = f - i0;
    m.height[k] = Mathf.Lerp(raw[i0], raw[i1], w);
    m.lowest[k] = Mathf.Lerp(low[i0], low[i1], w);
   }
   return m;
  }

  // Raw body height (hips keepY minus hips flattened) averaged over a clip: the stance reference.
  public static float RawHeightMean(Probe probe, AnimationClip source) {
   var flat = BakeCopy(source, false); var keep = BakeCopy(source, true);
   try {
    float sum = 0; int n = 0;
    for (int i = 0; i <= 20; i++) {
     float t = source.length * i / 20f;
     probe.Pose(flat, t); float hy = probe.Local(probe.Hips).y;
     probe.Pose(keep, t); sum += probe.Local(probe.Hips).y - hy; n++;
    }
    return sum / n;
   } finally { probe.Forget(); UnityEngine.Object.DestroyImmediate(flat); UnityEngine.Object.DestroyImmediate(keep); }
  }
 }
}

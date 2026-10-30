using UnityEngine;

namespace LostRealmsOW {
 // Saddle data of a rideable animal, measured from its skinned mesh at preparation time
 // (ClaudeAnimalPrep.MeasureSaddle). Lengths are in the prepared prefab's own units; the bone-space
 // points scale and move with the animated body, so the seat bounces with the horse's real gait.
 public sealed class OpenWorldMountRig : MonoBehaviour {
  public Transform SaddleBone;
  public Vector3 SaddleLocal;                       // seat point in the saddle bone's space
  public Quaternion SaddleRestRotation = Quaternion.identity; // saddle bone rotation relative to the prefab root at rest
  public Vector3 SeatRest;                          // seat point in the prefab root's space at rest
  public Transform BitBone;
  public Vector3 BitLocal;                          // mouth / bit point in the bit bone's space
  public float BarrelHalfWidth = 0.28f, SeatHeight = 1.3f, BellyDepth = 0.6f, BodyLength = 2.4f;

  bool estimated;
  public float Scale => Mathf.Abs(transform.lossyScale.y);
  public float BarrelHalf => BarrelHalfWidth * Scale;
  public float Belly => BellyDepth * Scale;

  // Rigs without measurements (older prepared prefabs) get an estimate from the body's bounds.
  void EnsureEstimate() {
   if (SaddleBone || estimated) return;
   estimated = true;
   var b = OpenWorldMaterialHelper.ComputeRendererBounds(transform);
   float s = Mathf.Max(0.0001f, Scale);
   SeatHeight = b.size.y * 0.74f / s;
   BodyLength = Mathf.Max(b.size.x, b.size.z) / s;
   BarrelHalfWidth = Mathf.Min(b.size.x, b.size.z) * 0.24f / s;
   BellyDepth = SeatHeight * 0.42f;
   Vector3 center = transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));
   SeatRest = center + Vector3.up * SeatHeight - Vector3.forward * BodyLength * 0.04f;
  }

  // Seat on the moving back.
  public Vector3 Seat {
   get {
    if (SaddleBone) return SaddleBone.TransformPoint(SaddleLocal);
    EnsureEstimate();
    return transform.TransformPoint(SeatRest);
   }
  }
  // Seat with no gait bounce (follows the body root only): the rider's root and the camera use it.
  public Vector3 SeatSteady { get { EnsureEstimate(); return transform.TransformPoint(SeatRest); } }
  // The back's frame: yaw of the body plus the pitch and roll the gait gives the saddle bone.
  public Quaternion SeatRotation => SaddleBone ? SaddleBone.rotation * Quaternion.Inverse(SaddleRestRotation) : transform.rotation;
  public Vector3 Bit {
   get {
    if (BitBone) return BitBone.TransformPoint(BitLocal);
    EnsureEstimate();
    return transform.TransformPoint(SeatRest + new Vector3(0f, 0.05f * SeatHeight, BodyLength * 0.46f));
   }
  }

  public static OpenWorldMountRig For(GameObject animal) {
   if (!animal) return null;
   var rig = animal.GetComponentInChildren<OpenWorldMountRig>();
   if (rig) return rig;
   var model = animal.GetComponentInChildren<OpenWorldImportedModel>();
   return model ? model.gameObject.AddComponent<OpenWorldMountRig>() : null;
  }
 }

 // Leather reins from the rider's hands to the bit: a sagging line drawn each frame while mounted.
 public sealed class OpenWorldReins : MonoBehaviour {
  LineRenderer line;
  static Material material;
  const int Points = 9;
  public static OpenWorldReins Ensure(Transform owner) {
   var r = owner.GetComponentInChildren<OpenWorldReins>(true);
   if (r) return r;
   var go = new GameObject("Reins");
   go.transform.SetParent(owner, false);
   return go.AddComponent<OpenWorldReins>();
  }
  void Awake() {
   line = gameObject.AddComponent<LineRenderer>();
   if (!material) material = OpenWorldMaterialHelper.CreateLit("Reins leather", new Color(0.23f, 0.13f, 0.07f));
   line.sharedMaterial = material;
   line.positionCount = Points * 2 - 1;
   line.widthMultiplier = 0.014f;
   line.numCapVertices = 2;
   line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
   line.receiveShadows = false;
   line.useWorldSpace = true;
   line.enabled = false;
  }
  // Left hand -> bit -> right hand, each half sagging a little under its own weight.
  public void Draw(Vector3 leftHand, Vector3 bit, Vector3 rightHand, float slack) {
   if (!line) return;
   line.enabled = true;
   for (int i = 0; i < Points; i++) {
    float t = i / (float)(Points - 1);
    line.SetPosition(i, Vector3.Lerp(leftHand, bit, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * slack));
   }
   for (int i = 1; i < Points; i++) {
    float t = i / (float)(Points - 1);
    line.SetPosition(Points - 1 + i, Vector3.Lerp(bit, rightHand, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * slack));
   }
  }
  public void Hide() { if (line) line.enabled = false; }
 }

 // Tilts an animal's model with the ground under it (pitch from front to back hooves, roll across) and
 // banks it into fast turns. Pivots about the body root (the hooves' centre), so the feet stay down.
 public struct OpenWorldBodyTilt {
  Vector3 basePos; Quaternion baseRot; bool ready;
  float pitch, roll;
  public void Step(Transform model, Transform root, float length, float width, float bank, float dt, float strength = 1f) {
   if (!model || !root || model == root) return;
   if (!ready) { basePos = model.localPosition; baseRot = model.localRotation; ready = true; }
   var terrain = OpenWorldManager.I ? OpenWorldManager.I.ActiveTerrain : Terrain.activeTerrain;
   float wantPitch = 0f, wantRoll = bank;
   if (terrain) {
    Vector3 p = root.position, f = root.forward, r = root.right;
    float H(Vector3 q) => terrain.SampleHeight(q) + terrain.transform.position.y;
    float half = Mathf.Max(0.1f, length * 0.42f), side = Mathf.Max(0.08f, width * 0.5f);
    float front = H(p + f * half), back = H(p - f * half);
    float left = H(p - r * side), right = H(p + r * side);
    wantPitch = Mathf.Clamp(-Mathf.Atan2(front - back, half * 2f) * Mathf.Rad2Deg, -24f, 24f) * strength;
    // Across a side slope the body leans a little toward the low side (legs take the rest).
    wantRoll += Mathf.Clamp(Mathf.Atan2(right - left, side * 2f) * Mathf.Rad2Deg, -10f, 10f) * 0.35f * strength;
   }
   pitch = Mathf.Lerp(pitch, wantPitch, 1f - Mathf.Exp(-6f * dt));
   roll = Mathf.Lerp(roll, wantRoll, 1f - Mathf.Exp(-6f * dt));
   var q2 = Quaternion.Euler(pitch, 0f, roll);
   model.localRotation = q2 * baseRot;
   model.localPosition = q2 * basePos;
  }
  public void Reset(Transform model) {
   if (!ready || !model) return;
   model.localRotation = baseRot; model.localPosition = basePos;
   pitch = roll = 0f;
  }
 }
}

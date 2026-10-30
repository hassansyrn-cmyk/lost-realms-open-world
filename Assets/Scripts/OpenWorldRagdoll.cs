using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 // Death physics (Genshin / Wuthering Waves style): the killing blow throws the body.
 //  - Humanoids become ragdolls, built at the moment of death from the Animator's humanoid bones
 //    (11 bodies with capsule, box and sphere colliders and character joints, like Unity's ragdoll
 //    wizard), starting from the animated pose and launched along the blow.
 //  - Other creatures tumble as one rigid body while their own death clip plays.
 // Bodies freeze as soon as they come to rest (or after their life), then sink into the ground before
 // the owner is destroyed. A small budget keeps mobile physics cheap: when a new ragdoll starts beyond
 // it, the oldest one freezes and sinks early.
 public sealed class OpenWorldRagdoll : MonoBehaviour {
  const int MaxActive = 4;
  static readonly List<OpenWorldRagdoll> active = new List<OpenWorldRagdoll>();

  readonly List<Rigidbody> bodies = new List<Rigidbody>();
  readonly List<Collider> colliders = new List<Collider>();
  Transform model;
  float born, life, sinkStart = -1f, nextRestCheck;
  int restChecks;
  Vector3 sinkFrom;
  Action onSink;
  bool frozen;
  const float SinkDuration = 1.15f, SinkDepth = 0.9f;

  public static int ActiveCount => active.Count;

  // Humanoid ragdoll. velocity is the launch (m/s); life is how long the body lies before it sinks.
  public static OpenWorldRagdoll Activate(OpenWorldCharacterVisual visual, Vector3 velocity, float life, Action onSink = null) {
   if (!visual || !visual.Animator || !visual.Animator.isHuman || !visual.ModelInstance) return null;
   var anim = visual.Animator;
   Transform B(HumanBodyBones b) => anim.GetBoneTransform(b);
   Transform hips = B(HumanBodyBones.Hips), spine = B(HumanBodyBones.Chest) ?? B(HumanBodyBones.Spine), head = B(HumanBodyBones.Head);
   Transform lul = B(HumanBodyBones.LeftUpperLeg), lll = B(HumanBodyBones.LeftLowerLeg), lf = B(HumanBodyBones.LeftFoot);
   Transform rul = B(HumanBodyBones.RightUpperLeg), rll = B(HumanBodyBones.RightLowerLeg), rf = B(HumanBodyBones.RightFoot);
   Transform lua = B(HumanBodyBones.LeftUpperArm), lla = B(HumanBodyBones.LeftLowerArm), lh = B(HumanBodyBones.LeftHand);
   Transform rua = B(HumanBodyBones.RightUpperArm), rla = B(HumanBodyBones.RightLowerArm), rh = B(HumanBodyBones.RightHand);
   if (!hips || !spine || !head || !lul || !lll || !lf || !rul || !rll || !rf || !lua || !lla || !lh || !rua || !rla || !rh) return null;

   MakeRoom();
   visual.EnterRagdoll();
   var rd = visual.ModelInstance.AddComponent<OpenWorldRagdoll>();
   rd.model = visual.ModelInstance.transform;
   rd.born = Time.time; rd.life = Mathf.Max(1f, life); rd.onSink = onSink;
   rd.nextRestCheck = Time.time + 0.8f;

   Transform root = visual.transform.parent ? visual.transform.parent : visual.transform;
   Vector3 right = root.right, up = Vector3.up, fwd = root.forward;
   float shoulder = Mathf.Max(0.12f, Vector3.Distance(lua.position, rua.position));
   float hipWidth = Mathf.Max(0.1f, Vector3.Distance(lul.position, rul.position));
   float height = Mathf.Max(0.6f, head.position.y - Mathf.Min(lf.position.y, rf.position.y) + 0.12f);
   float totalMass = Mathf.Clamp(58f * Mathf.Pow(height / 1.75f, 2.2f), 12f, 260f);

   // Bodies (mass share, collider) in parent-first order.
   var rHips = rd.Body(hips, totalMass * 0.16f);
   rd.Box(hips, hips.position + (spine.position - hips.position) * 0.32f, right, up, fwd, new Vector3(hipWidth * 1.75f, Vector3.Distance(hips.position, spine.position) * 0.85f, hipWidth * 1.25f));
   var rSpine = rd.Body(spine, totalMass * 0.2f);
   Vector3 neckTop = Vector3.Lerp(spine.position, head.position, 0.85f);
   rd.Box(spine, Vector3.Lerp(spine.position, neckTop, 0.45f), right, up, fwd, new Vector3(shoulder * 0.95f, Vector3.Distance(spine.position, neckTop) * 1.05f, shoulder * 0.55f));
   var rHead = rd.Body(head, totalMass * 0.07f);
   rd.Sphere(head, head.position + up * shoulder * 0.18f, shoulder * 0.27f);
   var rLul = rd.Body(lul, totalMass * 0.1f); rd.Capsule(lul, lll.position, 0.27f);
   var rLll = rd.Body(lll, totalMass * 0.055f); rd.Capsule(lll, lf.position + (lf.position - lll.position).normalized * 0.06f, 0.22f);
   var rRul = rd.Body(rul, totalMass * 0.1f); rd.Capsule(rul, rll.position, 0.27f);
   var rRll = rd.Body(rll, totalMass * 0.055f); rd.Capsule(rll, rf.position + (rf.position - rll.position).normalized * 0.06f, 0.22f);
   var rLua = rd.Body(lua, totalMass * 0.035f); rd.Capsule(lua, lla.position, 0.24f);
   var rLla = rd.Body(lla, totalMass * 0.025f); rd.Capsule(lla, lh.position + (lh.position - lla.position).normalized * 0.09f, 0.2f);
   var rRua = rd.Body(rua, totalMass * 0.035f); rd.Capsule(rua, rla.position, 0.24f);
   var rRla = rd.Body(rla, totalMass * 0.025f); rd.Capsule(rla, rh.position + (rh.position - rla.position).normalized * 0.09f, 0.2f);

   // Joints with the ragdoll wizard's limits (relative to the pose at death), slightly widened so a
   // bent knee or elbow may also straighten a little.
   Joint(rLul, rHips, right, fwd, -20f, 70f, 30f);
   Joint(rRul, rHips, right, fwd, -20f, 70f, 30f);
   Joint(rLll, rLul, right, fwd, -85f, 6f, 0f);
   Joint(rRll, rRul, right, fwd, -85f, 6f, 0f);
   Joint(rSpine, rHips, right, fwd, -22f, 22f, 12f);
   Joint(rHead, rSpine, right, fwd, -40f, 25f, 25f);
   Joint(rLua, rSpine, up, fwd, -70f, 15f, 55f);
   Joint(rRua, rSpine, up, fwd, -70f, 15f, 55f);
   Joint(rLla, rLua, fwd, up, -95f, 6f, 0f);
   Joint(rRla, rRua, fwd, up, -95f, 6f, 0f);

   rd.FinishSetup(velocity, rHips, rSpine);
   return rd;
  }

  // Non-humanoid creatures: one rigid body around the model that tumbles away.
  public static OpenWorldRagdoll Tumble(OpenWorldCharacterVisual visual, Vector3 velocity, float life, Action onSink = null) {
   if (!visual || !visual.ModelInstance) return null;
   var go = visual.ModelInstance;
   var bounds = OpenWorldMaterialHelper.ComputeRendererBounds(go.transform);
   if (bounds.size.sqrMagnitude < 0.0001f) return null;
   MakeRoom();
   visual.EnterRagdoll(keepAnimating: true);
   var rd = go.AddComponent<OpenWorldRagdoll>();
   rd.model = go.transform;
   rd.born = Time.time; rd.life = Mathf.Max(1f, life); rd.onSink = onSink;
   rd.nextRestCheck = Time.time + 0.8f;
   var box = go.AddComponent<BoxCollider>();
   box.center = go.transform.InverseTransformPoint(bounds.center);
   Vector3 ls = go.transform.lossyScale;
   box.size = new Vector3(bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(ls.x)), bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(ls.y)), bounds.size.z / Mathf.Max(0.001f, Mathf.Abs(ls.z))) * 0.85f;
   rd.colliders.Add(box);
   var rb = go.AddComponent<Rigidbody>();
   rb.mass = Mathf.Clamp(bounds.size.x * bounds.size.y * bounds.size.z * 160f, 6f, 400f);
   rb.linearDamping = 0.15f; rb.angularDamping = 0.8f;
   rb.interpolation = RigidbodyInterpolation.Interpolate;
   rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
   rb.maxDepenetrationVelocity = 3f;
   rd.bodies.Add(rb);
   rd.FinishSetup(velocity, rb, null);
   return rd;
  }

  static void MakeRoom() {
   active.RemoveAll(r => !r);
   while (active.Count >= MaxActive) {
    var oldest = active[0];
    active.RemoveAt(0);
    if (oldest) oldest.BeginSink();
   }
  }

  Rigidbody Body(Transform bone, float mass) {
   var rb = bone.gameObject.AddComponent<Rigidbody>();
   rb.mass = Mathf.Max(0.5f, mass);
   rb.linearDamping = 0.05f;
   rb.angularDamping = 0.6f;
   rb.interpolation = RigidbodyInterpolation.Interpolate;
   rb.solverIterations = 10;
   rb.solverVelocityIterations = 4;
   rb.maxDepenetrationVelocity = 4f;
   bodies.Add(rb);
   return rb;
  }

  // Capsule from the bone to an end point, along the bone's closest local axis.
  void Capsule(Transform bone, Vector3 end, float radiusScale) {
   Vector3 local = bone.InverseTransformPoint(end);
   int axis = Dominant(local);
   float length = local[axis];
   var c = bone.gameObject.AddComponent<CapsuleCollider>();
   c.direction = axis;
   Vector3 center = Vector3.zero; center[axis] = length * 0.5f;
   c.center = center;
   c.height = Mathf.Abs(length);
   c.radius = Mathf.Max(0.02f, Mathf.Abs(length) * radiusScale);
   colliders.Add(c);
  }

  // Axis-aligned box in the bone's space, sized along the character's right/up/forward.
  void Box(Transform bone, Vector3 worldCenter, Vector3 right, Vector3 up, Vector3 fwd, Vector3 worldSize) {
   var b = bone.gameObject.AddComponent<BoxCollider>();
   b.center = bone.InverseTransformPoint(worldCenter);
   Vector3 size = Vector3.one * 0.1f;
   Vector3 scale = bone.lossyScale;
   int ar = Dominant(bone.InverseTransformDirection(right)), au = Dominant(bone.InverseTransformDirection(up)), af = Dominant(bone.InverseTransformDirection(fwd));
   if (ar == au || ar == af || au == af) { ar = 0; au = 1; af = 2; }
   size[ar] = worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(scale[ar]));
   size[au] = worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(scale[au]));
   size[af] = worldSize.z / Mathf.Max(0.0001f, Mathf.Abs(scale[af]));
   b.size = size;
   colliders.Add(b);
  }

  void Sphere(Transform bone, Vector3 worldCenter, float worldRadius) {
   var s = bone.gameObject.AddComponent<SphereCollider>();
   s.center = bone.InverseTransformPoint(worldCenter);
   float scale = Mathf.Max(0.0001f, Mathf.Abs(bone.lossyScale.x));
   s.radius = worldRadius / scale;
   colliders.Add(s);
  }

  static int Dominant(Vector3 v) {
   float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
   return ax >= ay && ax >= az ? 0 : ay >= az ? 1 : 2;
  }

  static Vector3 AxisOf(Transform t, Vector3 world) {
   Vector3 l = t.InverseTransformDirection(world);
   int i = Dominant(l);
   Vector3 a = Vector3.zero; a[i] = Mathf.Sign(l[i]);
   return a;
  }

  static void Joint(Rigidbody body, Rigidbody parent, Vector3 twistAxis, Vector3 swingAxis, float lowTwist, float highTwist, float swing) {
   var j = body.gameObject.AddComponent<CharacterJoint>();
   j.axis = AxisOf(body.transform, twistAxis);
   j.swingAxis = AxisOf(body.transform, swingAxis);
   j.anchor = Vector3.zero;
   j.connectedBody = parent;
   j.enablePreprocessing = false;
   j.enableProjection = true;
   j.lowTwistLimit = new SoftJointLimit { limit = lowTwist };
   j.highTwistLimit = new SoftJointLimit { limit = highTwist };
   j.swing1Limit = new SoftJointLimit { limit = swing };
   j.swing2Limit = new SoftJointLimit { limit = Mathf.Min(swing, 12f) };
  }

  void FinishSetup(Vector3 velocity, Rigidbody core, Rigidbody upper) {
   // The body only collides with the world: not with itself (a mid-pose start would explode), and
   // not with the hero, who should be able to walk through a falling foe.
   for (int i = 0; i < colliders.Count; i++)
    for (int k = i + 1; k < colliders.Count; k++) Physics.IgnoreCollision(colliders[i], colliders[k], true);
   var hero = OpenWorldManager.I ? OpenWorldManager.I.Hero : null;
   if (hero && hero.Controller) foreach (var c in colliders) Physics.IgnoreCollision(c, hero.Controller, true);
   var gm = OpenWorldManager.I;
   if (gm) foreach (var e in gm.Enemies) if (e && e.Controller) foreach (var c in colliders) Physics.IgnoreCollision(c, e.Controller, true);
   foreach (var rb in bodies) rb.linearVelocity = velocity;
   Vector3 flat = Vector3.ProjectOnPlane(velocity, Vector3.up);
   if (core) {
    core.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    // A little spin from the blow: the body pivots back over its hips.
    core.angularVelocity = Vector3.Cross(Vector3.up, flat.normalized) * Mathf.Min(7f, flat.magnitude * 0.7f);
    core.gameObject.AddComponent<OpenWorldRagdollContact>();
   }
   if (upper) {
    upper.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    upper.linearVelocity = velocity + flat.normalized * Mathf.Min(3f, flat.magnitude * 0.35f);
    upper.gameObject.AddComponent<OpenWorldRagdollContact>();
   }
   active.Add(this);
  }

  void Update() {
   float now = Time.time;
   if (sinkStart < 0f) {
    // Freeze once everything is at rest (cheap on mobile), sink at the end of the body's life.
    if (!frozen && now >= nextRestCheck) {
     nextRestCheck = now + 0.25f;
     bool resting = true;
     foreach (var rb in bodies) if (rb && (rb.linearVelocity.sqrMagnitude > 0.04f || rb.angularVelocity.sqrMagnitude > 0.25f)) { resting = false; break; }
     restChecks = resting ? restChecks + 1 : 0;
     if (restChecks >= 3 || now - born > life + 2f) Freeze();
    }
    if (now - born >= life) BeginSink();
    return;
   }
   float t = Mathf.Clamp01((now - sinkStart) / SinkDuration);
   if (model) model.position = sinkFrom + Vector3.down * (SinkDepth * t * t);
  }

  void Freeze() {
   if (frozen) return;
   frozen = true;
   foreach (var rb in bodies) if (rb) { rb.interpolation = RigidbodyInterpolation.None; rb.isKinematic = true; }
  }

  void BeginSink() {
   if (sinkStart >= 0f) return;
   Freeze();
   foreach (var c in colliders) if (c) c.enabled = false;
   sinkStart = Time.time;
   if (model) sinkFrom = model.position;
   var cb = onSink; onSink = null;
   cb?.Invoke();
   active.Remove(this);
  }

  void OnDestroy() { active.Remove(this); }
 }

 // On the ragdoll's hips and chest: a body thrown into barrels or crates knocks them over or breaks them.
 public sealed class OpenWorldRagdollContact : MonoBehaviour {
  float nextAt;
  void OnCollisionEnter(Collision c) {
   if (Time.time < nextAt || c.relativeVelocity.sqrMagnitude < 9f) return;
   nextAt = Time.time + 0.12f;
   OpenWorldPhysicsProps.BodyImpact(c.collider, c.GetContact(0).point, -c.relativeVelocity);
  }
 }
}

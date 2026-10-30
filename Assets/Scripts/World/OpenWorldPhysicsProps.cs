using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 // Breakable and pushable clutter (Zelda / Genshin): barrels, crates, jars, jugs, baskets, sacks,
 // benches and tables in towns and camps stay static and cheap until something hits or shoves them.
 // Then they become rigidbodies, tumble and come to rest. Crates, jars, jugs and baskets shatter on
 // any blow (barrels on heavy ones) into a few pooled shards, sometimes dropping coins. Streaming
 // returns every prop whole: the realm calls ResetForReuse when a pooled placement comes back.
 public sealed class OpenWorldPhysicsProp : MonoBehaviour {
  public enum Kind { Push, Break }
  public Kind kind;
  public int toughness;   // 0 breaks on any hit, 1 on heavy hits, 2 never (push only)
  public float baseMass = 10f;
  Rigidbody rb;
  bool broken;
  readonly List<Renderer> hidden = new List<Renderer>();
  readonly List<Collider> disabled = new List<Collider>();

  public bool Broken => broken;
  public Rigidbody Body => rb;
  public float Mass => baseMass * Mathf.Pow(Mathf.Max(0.2f, transform.lossyScale.x), 3f);
  public bool Breakable(bool heavy) => kind == Kind.Break && (toughness == 0 || toughness == 1 && heavy);

  public Rigidbody Wake() {
   if (broken) return null;
   if (!rb) {
    // A rigidbody needs convex colliders.
    foreach (var mc in GetComponentsInChildren<MeshCollider>()) if (!mc.convex) mc.convex = true;
    rb = gameObject.AddComponent<Rigidbody>();
    rb.mass = Mathf.Clamp(Mass, 2f, 400f);
    rb.linearDamping = 0.05f;
    rb.angularDamping = 0.3f;
    rb.interpolation = RigidbodyInterpolation.Interpolate;
    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    rb.maxDepenetrationVelocity = 2f;
   }
   rb.isKinematic = false;
   rb.WakeUp();
   OpenWorldPhysicsProps.Track(this);
   return rb;
  }

  // Budget overflow: the prop keeps its new pose but stops simulating.
  public void Freeze() { if (rb && !rb.isKinematic) rb.isKinematic = true; }

  public void Break(Vector3 velocity) {
   if (broken) return;
   broken = true;
   var bounds = OpenWorldMaterialHelper.ComputeRendererBounds(transform);
   Material mat = null;
   foreach (var r in GetComponentsInChildren<Renderer>()) {
    if (!r.enabled) continue;
    if (!mat && r.sharedMaterial && !(r is ParticleSystemRenderer)) mat = r.sharedMaterial;
    r.enabled = false; hidden.Add(r);
   }
   foreach (var c in GetComponentsInChildren<Collider>()) { if (!c.enabled) continue; c.enabled = false; disabled.Add(c); }
   if (rb) rb.isKinematic = true;
   OpenWorldPhysicsProps.Untrack(this);
   OpenWorldPhysicsProps.SpawnDebris(bounds, mat, velocity);
   if (!OpenWorldVfx.Play("impact_generic", bounds.center, Quaternion.identity, Mathf.Clamp(bounds.size.magnitude * 0.6f, 0.4f, 1.2f), new Color(0.85f, 0.72f, 0.55f)))
    OpenWorldManager.I?.SpawnBurst(bounds.center, new Color(0.75f, 0.6f, 0.42f), 10);
   if (!OpenWorldSoundscape.Sfx("prop_smash", bounds.center, 0.55f)) OpenWorldAudio.Play("sfx_impact", 0.3f);
   var gm = OpenWorldManager.I;
   if (gm && Random.value < 0.3f) OpenWorldCollectible.Spawn(OWCollectibleKind.GoldCoin, bounds.center, gm.WorldRoot, Random.Range(2, 7));
  }

  public void ResetForReuse() {
   OpenWorldPhysicsProps.Untrack(this);
   if (rb) { Destroy(rb); rb = null; }
   foreach (var r in hidden) if (r) r.enabled = true;
   foreach (var c in disabled) if (c) c.enabled = true;
   hidden.Clear(); disabled.Clear();
   broken = false;
  }
 }

 public static class OpenWorldPhysicsProps {
  const int MaxAwake = 20, MaxShards = 36;
  sealed class Info { public OpenWorldPhysicsProp.Kind kind; public int toughness; public float mass; }
  static readonly Dictionary<string, Info> infos = new Dictionary<string, Info>();
  static readonly List<OpenWorldPhysicsProp> awake = new List<OpenWorldPhysicsProp>();
  static readonly Collider[] overlap = new Collider[64];
  static readonly HashSet<OpenWorldPhysicsProp> touched = new HashSet<OpenWorldPhysicsProp>();
  static Mesh shardMesh;
  static readonly Stack<GameObject> shardPool = new Stack<GameObject>();
  static readonly List<OpenWorldShard> shards = new List<OpenWorldShard>();

  internal static void Track(OpenWorldPhysicsProp p) {
   awake.Remove(p);
   awake.Add(p);
   while (awake.Count > MaxAwake) { var old = awake[0]; awake.RemoveAt(0); if (old) old.Freeze(); }
  }
  internal static void Untrack(OpenWorldPhysicsProp p) => awake.Remove(p);

  // Which kit clutter reacts, and how heavy it is (native bounds of the prepared prefab).
  static Info Classify(string key) {
   if (infos.TryGetValue(key, out var info)) return info;
   info = null;
   var e = OpenWorldWorldKit.Entry(key);
   if (e != null && (e.role == "prop" || e.role == "log") && e.collider != "mesh" && e.collider != "none" && key.IndexOf("group", System.StringComparison.Ordinal) < 0 && key.IndexOf("mounted", System.StringComparison.Ordinal) < 0) {
    string t = "," + e.tags + ",";
    bool Has(string tag) => t.Contains("," + tag + ",");
    float volume = Mathf.Max(0.001f, e.size.x * e.size.y * e.size.z);
    if (Has("crate") || Has("jar") || Has("jug") || Has("basket")) info = new Info { kind = OpenWorldPhysicsProp.Kind.Break, toughness = 0, mass = Mathf.Clamp(volume * 160f, 3f, 60f) };
    else if (Has("barrel")) info = new Info { kind = OpenWorldPhysicsProp.Kind.Break, toughness = 1, mass = Mathf.Clamp(volume * 260f, 8f, 90f) };
    else if (Has("sack") || Has("bench") || Has("table") || Has("firewood") || Has("stool") || Has("bucket")) info = new Info { kind = OpenWorldPhysicsProp.Kind.Push, toughness = 2, mass = Mathf.Clamp(volume * 220f, 6f, 120f) };
    else if (Has("cart")) info = new Info { kind = OpenWorldPhysicsProp.Kind.Push, toughness = 2, mass = Mathf.Clamp(volume * 120f, 120f, 320f) };
   }
   infos[key] = info;
   return info;
  }

  // The streamed placement a collider belongs to, as a physics prop (or null for anything else).
  public static OpenWorldPhysicsProp Resolve(Collider c) {
   if (!c || c.isTrigger) return null;
   var realm = OpenWorldRealm.Instance;
   if (!realm || !realm.StreamedRoot) return null;
   Transform root = realm.StreamedRoot, t = c.transform;
   while (t && t.parent != root) t = t.parent;
   if (!t) return null;
   if (t.TryGetComponent<OpenWorldPhysicsProp>(out var prop)) return prop.Broken ? null : prop;
   string key = realm.KeyOf(t.gameObject);
   if (string.IsNullOrEmpty(key)) return null;
   var info = Classify(key);
   if (info == null) return null;
   prop = t.gameObject.AddComponent<OpenWorldPhysicsProp>();
   prop.kind = info.kind; prop.toughness = info.toughness; prop.baseMass = info.mass;
   return prop;
  }

  static float Share(float mass) => Mathf.Clamp(22f / Mathf.Max(1f, mass), 0.12f, 1.1f);

  // A weapon swing: props in the arc in front of the attacker are smashed or sent flying.
  public static int Strike(Vector3 origin, Vector3 forward, float reach, float push, float lift, bool heavy, float cone = 0.2f) {
   forward.y = 0f;
   if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
   forward.Normalize();
   int n = Physics.OverlapSphereNonAlloc(origin + forward * reach * 0.45f + Vector3.up * 0.6f, reach * 0.65f + 0.35f, overlap, ~0, QueryTriggerInteraction.Ignore);
   return Apply(n, origin, forward, push, lift, heavy, cone);
  }

  // Spells and bursts: everything around the centre is thrown outward.
  public static int Explode(Vector3 center, float radius, float push, float lift, bool heavy) {
   int n = Physics.OverlapSphereNonAlloc(center + Vector3.up * 0.5f, radius, overlap, ~0, QueryTriggerInteraction.Ignore);
   return Apply(n, center, Vector3.zero, push, lift, heavy, -2f);
  }

  static int Apply(int n, Vector3 origin, Vector3 forward, float push, float lift, bool heavy, float cone) {
   touched.Clear();
   int count = 0;
   for (int i = 0; i < n; i++) {
    var prop = Resolve(overlap[i]);
    if (!prop || !touched.Add(prop)) continue;
    Vector3 center = overlap[i].bounds.center;
    Vector3 to = center - origin; to.y = 0f;
    float d = to.magnitude;
    Vector3 dir = d > 0.01f ? to / d : (forward.sqrMagnitude > 0f ? forward : Vector3.forward);
    if (cone > -1f && d > 0.9f && Vector3.Dot(forward, dir) < cone) continue;
    float falloff = cone <= -1f ? Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(d / 6f)) : 1f;
    Vector3 velocity = (dir * push + Vector3.up * lift) * falloff;
    if (prop.Breakable(heavy)) prop.Break(velocity * 0.6f);
    else {
     var rb = prop.Wake();
     if (!rb) continue;
     rb.AddForceAtPosition(velocity * Share(rb.mass), center + Vector3.up * 0.15f, ForceMode.VelocityChange);
     rb.AddTorque(Vector3.Cross(Vector3.up, dir) * push * 0.6f * Share(rb.mass), ForceMode.VelocityChange);
     OpenWorldSoundscape.Sfx("prop_knock", center, 0.4f);
    }
    count++;
   }
   return count;
  }

  // A character controller walking, sprinting or sliding into clutter pushes it; a dash or a body
  // thrown at speed breaks the fragile kinds.
  public static void Shove(ControllerColliderHit hit, Vector3 velocity, bool canBreak) {
   if (hit.moveDirection.y < -0.4f) return;          // standing on it
   Vector3 dir = Vector3.ProjectOnPlane(hit.moveDirection, Vector3.up);
   float speed = Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude;
   if (dir.sqrMagnitude < 0.01f || speed < 0.8f) return;
   dir.Normalize();
   Rigidbody rb = hit.rigidbody;
   if (!rb || rb.isKinematic) {
    var prop = Resolve(hit.collider);
    if (!prop) return;
    if (canBreak && speed > 8.5f && prop.Breakable(false)) { prop.Break(dir * speed * 0.5f); return; }
    rb = prop.Wake();
    if (!rb) return;
   }
   float wanted = speed * Mathf.Clamp(26f / Mathf.Max(1f, rb.mass), 0.08f, 0.95f);
   float along = Vector3.Dot(rb.linearVelocity, dir);
   if (along < wanted) rb.AddForceAtPosition(dir * (wanted - along), hit.point, ForceMode.VelocityChange);
  }

  // Ragdolls hitting clutter (OpenWorldRagdollContact).
  public static void BodyImpact(Collider c, Vector3 point, Vector3 velocity) {
   var prop = Resolve(c);
   if (!prop) return;
   float speed = velocity.magnitude;
   if (speed > 6f && prop.Breakable(false)) { prop.Break(velocity * 0.4f); return; }
   var rb = prop.Wake();
   if (rb) rb.AddForceAtPosition(velocity * 0.45f * Share(rb.mass), point, ForceMode.VelocityChange);
  }

  // Capture helper: "prop": stands the hero 2.4 m from the nearest reactive prop within 120 m.
  public static void DebugFindProp(string arg) {
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero) return;
   Vector3 o = gm.Hero.transform.position;
   var hits = Physics.OverlapSphere(o, 120f, ~0, QueryTriggerInteraction.Ignore);
   float best = float.MaxValue; OpenWorldPhysicsProp found = null;
   foreach (var c in hits) {
    var p = Resolve(c);
    if (!p) continue;
    float d = (p.transform.position - o).sqrMagnitude;
    if (d < best) { best = d; found = p; }
   }
   if (!found) { Debug.LogWarning("[Props] no reactive prop within 120 m"); return; }
   Vector3 to = found.transform.position - o; to.y = 0f;
   Vector3 dir = to.sqrMagnitude > 0.01f ? to.normalized : Vector3.forward;
   gm.Hero.WarpTo(found.transform.position - dir * 2.4f);
   gm.Hero.transform.rotation = Quaternion.LookRotation(dir);
   if (gm.CameraRig) { gm.CameraRig.Yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + 35f; gm.CameraRig.SnapToTarget(); }
   Debug.Log("[Props] next to " + found.name + " (" + found.kind + ", " + found.Mass.ToString("0") + " kg)");
  }

  // ---------------------------------------------------------------- shards

  internal static void SpawnDebris(Bounds bounds, Material mat, Vector3 velocity) {
   if (!mat) return;
   if (!shardMesh) shardMesh = BuildBoxMesh();
   int count = Mathf.Clamp(Mathf.RoundToInt(bounds.size.magnitude * 5f), 4, 8);
   var hero = OpenWorldManager.I ? OpenWorldManager.I.Hero : null;
   for (int i = 0; i < count; i++) {
    shards.RemoveAll(s => !s);
    while (shards.Count >= MaxShards) { var old = shards[0]; shards.RemoveAt(0); if (old) old.Retire(); }
    GameObject go = shardPool.Count > 0 ? shardPool.Pop() : null;
    if (!go) {
     go = new GameObject("Shard");
     go.AddComponent<MeshFilter>().sharedMesh = shardMesh;
     go.AddComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
     go.AddComponent<BoxCollider>();
     var body = go.AddComponent<Rigidbody>();
     body.interpolation = RigidbodyInterpolation.Interpolate;
     body.maxDepenetrationVelocity = 3f;
     go.AddComponent<OpenWorldShard>();
    }
    go.SetActive(true);
    go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    bool plank = Random.value < 0.6f;
    float s = Mathf.Clamp(bounds.size.magnitude * 0.16f, 0.06f, 0.3f);
    go.transform.localScale = plank ? new Vector3(s * Random.Range(0.25f, 0.4f), s * Random.Range(0.15f, 0.3f), s * Random.Range(1.2f, 2.2f)) : Vector3.one * s * Random.Range(0.5f, 0.9f);
    Vector3 p = bounds.center + Vector3.Scale(Random.insideUnitSphere * 0.4f, bounds.size);
    go.transform.SetPositionAndRotation(p, Random.rotationUniform);
    var rb = go.GetComponent<Rigidbody>();
    rb.isKinematic = false;
    rb.mass = 0.6f;
    Vector3 burst = (p - bounds.center).normalized * Random.Range(1.5f, 3.5f) + Vector3.up * Random.Range(1.5f, 3.8f);
    rb.linearVelocity = velocity + burst;
    rb.angularVelocity = Random.insideUnitSphere * 12f;
    var col = go.GetComponent<BoxCollider>();
    if (hero && hero.Controller) Physics.IgnoreCollision(col, hero.Controller, true);
    var shard = go.GetComponent<OpenWorldShard>();
    shard.Begin(Random.Range(2.4f, 3.4f));
    shards.Add(shard);
   }
  }

  internal static void ReturnShard(OpenWorldShard s) {
   shards.Remove(s);
   if (!s) return;
   s.gameObject.SetActive(false);
   if (shardPool.Count < MaxShards) shardPool.Push(s.gameObject);
   else Object.Destroy(s.gameObject);
  }

  // Unit box with per-face UVs, so the prop's texture shows on its pieces.
  static Mesh BuildBoxMesh() {
   var m = new Mesh { name = "PropShard" };
   var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
   Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
   foreach (var d in dirs) {
    Vector3 a = d.y != 0f ? Vector3.right : Vector3.up;
    Vector3 b = Vector3.Cross(d, a);
    int i0 = v.Count;
    v.Add((d - a - b) * 0.5f); v.Add((d + a - b) * 0.5f); v.Add((d + a + b) * 0.5f); v.Add((d - a + b) * 0.5f);
    for (int k = 0; k < 4; k++) n.Add(d);
    uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
    tri.Add(i0); tri.Add(i0 + 2); tri.Add(i0 + 1); tri.Add(i0); tri.Add(i0 + 3); tri.Add(i0 + 2);
   }
   m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
   m.RecalculateBounds();
   return m;
  }
 }

 // A piece of a smashed prop: falls, rests, shrinks away and returns to the pool.
 public sealed class OpenWorldShard : MonoBehaviour {
  float until, shrinkFrom = -1f;
  Vector3 scale;
  public void Begin(float life) { until = Time.time + life; shrinkFrom = -1f; scale = transform.localScale; }
  public void Retire() { OpenWorldPhysicsProps.ReturnShard(this); }
  void Update() {
   float now = Time.time;
   if (now < until) return;
   if (shrinkFrom < 0f) { shrinkFrom = now; var rb = GetComponent<Rigidbody>(); if (rb) rb.isKinematic = true; }
   float t = (now - shrinkFrom) / 0.35f;
   if (t >= 1f) { Retire(); return; }
   transform.localScale = scale * (1f - t);
  }
 }
}

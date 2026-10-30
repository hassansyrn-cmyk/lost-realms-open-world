using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LostRealmsOW {
 // Runtime host of the baked 1 km realm: terrain (with instanced trees, bushes, grass), the
 // persistent architecture and cliffs, streamed rocks/props/foliage around the hero, and the
 // lake/river surfaces. Everything is deterministic data produced by the editor bake.
 public sealed class OpenWorldRealm : MonoBehaviour {
  public const string Folder = "World/Realm/";
  public static OpenWorldRealm Instance { get; private set; }
  public Terrain Terrain { get; private set; }
  public int PersistentCount { get; private set; }
  public int StreamedActive => active.Count;
  public int PlacementCount => set != null ? set.Count : 0;
  public struct LightAnchor { public Vector3 position; public Color color; public float range; }
  // Lamp heads and fires that receive one of the pooled night lights when the hero is near.
  public readonly List<LightAnchor> LightAnchors = new List<LightAnchor>();

  OWPlacementSet set;
  readonly Dictionary<long, List<int>> buckets = new Dictionary<long, List<int>>();
  readonly Dictionary<long, List<GameObject>> live = new Dictionary<long, List<GameObject>>();
  readonly HashSet<long> active = new HashSet<long>();
  readonly Queue<long> spawnQueue = new Queue<long>();
  readonly Dictionary<string, Stack<GameObject>> pools = new Dictionary<string, Stack<GameObject>>();
  readonly Dictionary<GameObject, string> poolKey = new Dictionary<GameObject, string>();
  Transform persistentRoot, streamedRoot, waterRoot, poolRoot;
  const float CellSize = 48f;
  float streamRadius = 300f;
  int spawnBudget = 80;
  long lastCell = long.MinValue;
  readonly List<Material> ownedMaterials = new List<Material>();
  readonly List<Mesh> ownedMeshes = new List<Mesh>();
  static readonly int PlayerPositionId = Shader.PropertyToID("_Player_Position");

  static TerrainData cachedData;
  public static bool Available {
   get {
    if (!cachedData) cachedData = Resources.Load<TerrainData>(Folder + "RealmTerrain");
    return cachedData;
   }
  }

  static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

  public static OpenWorldRealm Build(Transform parent, int tier) {
   if (!Available) return null;
   var go = new GameObject("Lost Realm");
   go.transform.SetParent(parent, false);
   var realm = go.AddComponent<OpenWorldRealm>();
   realm.Initialise(tier);
   return realm;
  }

  void Initialise(int tier) {
   bool high = tier >= 1;
   Instance = this;
   OpenWorldRegions.Initialise();
   OpenWorldWorldKit.Load();
   // Terrain
   var data = cachedData;
   var tgo = Terrain.CreateTerrainGameObject(data);
   tgo.name = "Realm Terrain";
   tgo.transform.SetParent(transform, false);
   // Heights are stored from the terrain's base: TerrainBase for current bakes (size.y = TerrainHeight -
   // TerrainBase), 0 for bakes made before beds could go below 0 (size.y = TerrainHeight).
   tgo.transform.position = new Vector3(-OpenWorldRegions.WorldHalf, OpenWorldRegions.TerrainHeight - data.size.y, -OpenWorldRegions.WorldHalf);
   int worldLayer = LayerMask.NameToLayer("WorldStatic");
   if (worldLayer >= 0) tgo.layer = worldLayer;
   Terrain = tgo.GetComponent<Terrain>();
   var mat = Resources.Load<Material>(Folder + "RealmTerrain");
   if (mat) Terrain.materialTemplate = mat;
   else {
    var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
    if (shader) { var m = new Material(shader) { name = "Realm Terrain (runtime)" }; ownedMaterials.Add(m); Terrain.materialTemplate = m; }
   }
   ApplyQuality(tier);
   var fields = Resources.Load<TextAsset>(Folder + "RealmFields");
   if (fields) OpenWorldTerrainSculptor.LoadFields(fields.bytes, Terrain);
   // Water
   waterRoot = new GameObject("Water").transform; waterRoot.SetParent(transform, false);
   BuildWater(high);
   // Placements
   persistentRoot = new GameObject("Architecture & Cliffs").transform; persistentRoot.SetParent(transform, false);
   streamedRoot = new GameObject("Streamed Detail").transform; streamedRoot.SetParent(transform, false);
   poolRoot = new GameObject("Pool").transform; poolRoot.SetParent(transform, false); poolRoot.gameObject.SetActive(false);
   var json = Resources.Load<TextAsset>(Folder + "RealmPlacements");
   if (json) {
    set = JsonUtility.FromJson<OWPlacementSet>(json.text);
    for (int i = 0; i < set.Count; i++) {
     var p = set.Get(i);
     CollectLight(p);
     if (Persistent(p.kind)) { if (Spawn(p, persistentRoot)) PersistentCount++; continue; }
     long k = Key(Mathf.FloorToInt(p.pos.x / CellSize), Mathf.FloorToInt(p.pos.z / CellSize));
     if (!buckets.TryGetValue(k, out var list)) buckets[k] = list = new List<int>();
     list.Add(i);
    }
   }
   Physics.SyncTransforms();
  }

  void CollectLight(in OWPlacement p) {
   if (p.kind == OWPlaceKind.Lamp) {
    var e = OpenWorldWorldKit.Entry(p.key);
    float h = e != null ? e.size.y * p.scale : 2.6f;
    bool eerie = p.zone == "graveyard" || p.zone == "storm";
    LightAnchors.Add(new LightAnchor { position = p.pos + Vector3.up * Mathf.Max(0.6f, h * 0.86f), color = eerie ? new Color(0.55f, 0.95f, 0.8f) : new Color(1f, 0.74f, 0.44f), range = 9f });
   } else if (p.kind == OWPlaceKind.Fx && p.key != null && p.key.StartsWith("fire")) {
    LightAnchors.Add(new LightAnchor { position = p.pos + Vector3.up * 0.9f, color = new Color(1f, 0.58f, 0.26f), range = 12f });
   }
  }

  static bool Persistent(OWPlaceKind k) => k == OWPlaceKind.Building || k == OWPlaceKind.Cliff || k == OWPlaceKind.Landmark || k == OWPlaceKind.Cloud;

  public void ApplyQuality(bool high) => ApplyQuality(high ? 1 : 0);

  // 0 = Balanced, 1 = High, 2 = Ultra
  public void ApplyQuality(int tier) {
   if (!Terrain) return;
   bool high = tier >= 1, ultra = tier >= 2;
   Terrain.drawInstanced = true;
   Terrain.heightmapPixelError = ultra ? 2f : high ? 3.5f : 6f;
   Terrain.basemapDistance = ultra ? 640f : high ? 420f : 180f; // keeps rock detail on the nearer ranges
   Terrain.treeDistance = ultra ? 900f : high ? 720f : 420f;
   Terrain.treeBillboardDistance = 2000f;
   Terrain.treeMaximumFullLODCount = ultra ? 1400 : high ? 800 : 300;
   Terrain.treeLODBiasMultiplier = ultra ? 1.6f : high ? 1.3f : 0.8f;
   Terrain.detailObjectDistance = ultra ? 150f : high ? 115f : 65f;
   Terrain.detailObjectDensity = ultra ? 1f : high ? 0.85f : 0.45f;
   Terrain.shadowCastingMode = ShadowCastingMode.On;
   Terrain.allowAutoConnect = false;
   streamRadius = ultra ? 380f : high ? 320f : 220f;
   spawnBudget = ultra ? 120 : high ? 90 : 45;
   if (lastCell != long.MinValue) lastCell = long.MinValue + 1; // re-evaluate streaming with the new radius
  }

  // ---------- streaming ----------
  void Update() {
   var gm = OpenWorldManager.I;
   Vector3 focus = gm && gm.Hero ? gm.Hero.transform.position : Camera.main ? Camera.main.transform.position : Vector3.zero;
   Shader.SetGlobalVector(PlayerPositionId, new Vector4(focus.x, focus.y, focus.z, 1f));
   if (set == null) return;
   int cx = Mathf.FloorToInt(focus.x / CellSize), cz = Mathf.FloorToInt(focus.z / CellSize);
   long cell = Key(cx, cz);
   if (cell != lastCell) {
    bool first = lastCell == long.MinValue;
    lastCell = cell;
    Refresh(cx, cz, focus, first);
   }
   long tickStamp = OpenWorldStreamingDiagnostics.Start();
   double deadline = Time.realtimeSinceStartupAsDouble + .003;
   int budget = spawnBudget;
   while (budget > 0 && spawnQueue.Count > 0 && Time.realtimeSinceStartupAsDouble < deadline) {
    long k = spawnQueue.Peek();
    if (!active.Contains(k)) { spawnQueue.Dequeue(); continue; }
    budget -= FillCell(k, budget, deadline);
    if (IsFilled(k)) spawnQueue.Dequeue();
   }
   OpenWorldStreamingDiagnostics.End(OpenWorldStreamingDiagnostics.Work.RealmTick, tickStamp);
  }

  readonly Dictionary<long, int> fillCursor = new Dictionary<long, int>();
  bool IsFilled(long k) => !buckets.TryGetValue(k, out var list) || (fillCursor.TryGetValue(k, out int c) && c >= list.Count);

  int FillCell(long k, int budget, double deadline = double.PositiveInfinity) {
   if (!buckets.TryGetValue(k, out var list)) return 0;
   fillCursor.TryGetValue(k, out int cursor);
   if (!live.TryGetValue(k, out var objs)) live[k] = objs = new List<GameObject>();
   long fillStamp = OpenWorldStreamingDiagnostics.Start();
   int used = 0;
   while (cursor < list.Count && used < budget && Time.realtimeSinceStartupAsDouble < deadline) {
    var go = Spawn(set.Get(list[cursor]), streamedRoot);
    if (go) objs.Add(go);
    cursor++; used++;
   }
   OpenWorldStreamingDiagnostics.End(OpenWorldStreamingDiagnostics.Work.RealmFill, fillStamp, used);
   fillCursor[k] = cursor;
   return Mathf.Max(1, used);
  }

  void Refresh(int cx, int cz, Vector3 focus, bool immediate) {
   int reach = Mathf.CeilToInt(streamRadius / CellSize);
   var wanted = new HashSet<long>();
   var near = new List<(float d, long k)>();
   for (int dz = -reach; dz <= reach; dz++) for (int dx = -reach; dx <= reach; dx++) {
    float d = new Vector2(dx, dz).magnitude * CellSize;
    if (d > streamRadius + CellSize) continue;
    long k = Key(cx + dx, cz + dz);
    if (!buckets.ContainsKey(k)) continue;
    wanted.Add(k);
    if (!active.Contains(k)) near.Add((d, k));
   }
   // Release cells that fell well outside the radius (hysteresis avoids thrashing at the edge).
   var release = new List<long>();
   foreach (var k in active) if (!wanted.Contains(k)) release.Add(k);
   foreach (var k in release) {
    int kx = (int)(k >> 32), kz = (int)(k & 0xffffffff);
    if (new Vector2(kx - cx, kz - cz).magnitude * CellSize < streamRadius + CellSize * 2f) continue;
    Release(k);
   }
   near.Sort((a, b) => a.d.CompareTo(b.d));
   foreach (var n in near) {
    active.Add(n.k);
    if (immediate && n.d < 140f) { while (!IsFilled(n.k)) FillCell(n.k, 512); }
    else spawnQueue.Enqueue(n.k);
   }
  }

  void Release(long k) {
   active.Remove(k);
   fillCursor.Remove(k);
   if (!live.TryGetValue(k, out var objs)) return;
   foreach (var go in objs) {
    if (!go) continue;
    if (poolKey.TryGetValue(go, out var key)) {
     go.transform.SetParent(poolRoot, false);
     if (!pools.TryGetValue(key, out var stack)) pools[key] = stack = new Stack<GameObject>();
     stack.Push(go);
    } else Destroy(go);
   }
   objs.Clear();
  }

  // Streamed placements and their kit keys (the physics props look their clutter up here).
  public Transform StreamedRoot => streamedRoot;
  public string KeyOf(GameObject go) => go && poolKey.TryGetValue(go, out var k) ? k : null;

  // Review helper: "village_crate_01 2.5" puts a kit prop that far in front of the hero, as a streamed
  // placement (so the physics props treat it like any clutter). It lasts until the play session ends.
  public static void DebugSpawnProp(string arg) {
   var realm = Instance; var gm = OpenWorldManager.I;
   if (!realm || !gm || !gm.Hero || string.IsNullOrEmpty(arg)) return;
   var p = arg.Trim().Split(' ');
   var prefab = OpenWorldWorldKit.Prefab(p[0]);
   if (!prefab) { Debug.LogWarning("[Realm] no kit prop " + p[0]); return; }
   float d = p.Length > 1 && float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 2.5f;
   var h = gm.Hero.transform;
   Vector3 pos = h.position + h.forward * d;
   pos.y = OpenWorldMaterialHelper.SampleGroundHeight(pos, h);
   var go = Instantiate(prefab, realm.streamedRoot);
   realm.poolKey[go] = p[0];
   go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
  }

  GameObject Spawn(OWPlacement p, Transform parent) {
   GameObject go = null;
   if (pools.TryGetValue(p.key, out var stack) && stack.Count > 0) go = stack.Pop();
   // A barrel knocked over or a crate smashed on an earlier visit comes back whole.
   if (go && go.TryGetComponent<OpenWorldPhysicsProp>(out var physicsProp)) physicsProp.ResetForReuse();
   if (!go) {
    var prefab = OpenWorldWorldKit.Prefab(p.key);
    if (!prefab) return null;
    go = Instantiate(prefab);
    poolKey[go] = p.key;
    var entry = OpenWorldWorldKit.Entry(p.key);
    if (entry != null && entry.role == "campfire") StripLegacyFire(go);
    if (p.kind == OWPlaceKind.Fx) OpenWorldVfx.Calibrate(go);
   }
   var t = go.transform;
   t.SetParent(parent, false);
   t.SetPositionAndRotation(p.pos, Quaternion.Euler(p.euler));
   t.localScale = Vector3.one * Mathf.Max(0.01f, p.scale);
   return go;
  }

  // ---------- water ----------
  void BuildWater(bool high) {
   var water = Resources.Load<Material>(Folder + (high ? "RealmWater" : "RealmWaterSimple")) ?? Resources.Load<Material>(Folder + "RealmWaterSimple");
   if (!water) water = FallbackWater();
   var ice = Resources.Load<Material>(Folder + "RealmIce") ?? FallbackIce();
   int waterLayer = LayerMask.NameToLayer("Water");
   foreach (var lake in OpenWorldRegions.Lakes) {
    var mesh = LakeMesh(lake);
    AddSurface("Lake " + lake.Name, mesh, lake.Frozen ? ice : water, waterLayer, lake.Frozen);
   }
   // Rivers share the lake material with flow switched on (ripples travel downstream).
   var river = water;
   if (water && water.HasProperty("_Flow")) {
    river = new Material(water) { name = water.name + " (River)" };
    river.SetFloat("_Flow", 0.6f);
    ownedMaterials.Add(river);
   }
   var surfaces = OpenWorldTerrainSculptor.RiverSurfaces;
   for (int i = 0; i < OpenWorldRegions.Rivers.Count && i < surfaces.Count; i++) {
    var mesh = RiverMesh(OpenWorldRegions.Rivers[i], surfaces[i]);
    AddSurface("River " + OpenWorldRegions.Rivers[i].Name, mesh, river, waterLayer, false);
   }
  }

  void AddSurface(string name, Mesh mesh, Material mat, int layer, bool solid) {
   var go = new GameObject(name);
   go.transform.SetParent(waterRoot, false);
   if (layer >= 0) go.layer = layer;
   go.AddComponent<MeshFilter>().sharedMesh = mesh;
   var mr = go.AddComponent<MeshRenderer>();
   mr.sharedMaterial = mat;
   mr.shadowCastingMode = ShadowCastingMode.Off;
   mr.receiveShadows = solid;
   if (solid) { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; } // frozen tarn is walkable ice
   ownedMeshes.Add(mesh);
  }

  static Mesh LakeMesh(OWLake lake) {
   const int seg = 96;
   var verts = new Vector3[seg + 1]; var uvs = new Vector2[seg + 1]; var cols = new Color[seg + 1];
   var tris = new int[seg * 3];
   verts[0] = new Vector3(lake.Center.x, lake.SurfaceY, lake.Center.y); uvs[0] = new Vector2(lake.Center.x, lake.Center.y) / 12f; cols[0] = Color.white;
   for (int i = 0; i < seg; i++) {
    float a = i / (float)seg * Mathf.PI * 2f;
    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    float r = lake.ShoreRadius(lake.Center + dir * lake.Radius) * 1.1f + 1.5f;
    var p = lake.Center + dir * r;
    verts[i + 1] = new Vector3(p.x, lake.SurfaceY, p.y);
    uvs[i + 1] = p / 12f;
    cols[i + 1] = new Color(1f, 1f, 1f, 0f); // shoreline fade for shaders that read vertex alpha
    tris[i * 3] = 0; tris[i * 3 + 1] = 1 + (i + 1) % seg; tris[i * 3 + 2] = 1 + i;
   }
   var mesh = new Mesh { name = lake.Name + " surface" };
   mesh.vertices = verts; mesh.uv = uvs; mesh.uv2 = new Vector2[verts.Length]; mesh.colors = cols; mesh.triangles = tris;
   mesh.RecalculateNormals(); mesh.RecalculateBounds();
   return mesh;
  }

  static Mesh RiverMesh(OWRiver river, float[] surface) {
   var path = river.Path;
   int n = path.Length;
   var verts = new Vector3[n * 3]; var uvs = new Vector2[n * 3]; var cols = new Color[n * 3];
   var flow = new Vector2[n * 3]; // downstream direction for the water shader's flow map
   var tris = new List<int>();
   float total = 0f; var cum = new float[n];
   for (int i = 1; i < n; i++) { total += Vector2.Distance(path[i - 1], path[i]); cum[i] = total; }
   for (int i = 0; i < n; i++) {
    Vector2 dir = i < n - 1 ? (path[i + 1] - path[i]) : (path[i] - path[i - 1]);
    if (i > 0 && i < n - 1) dir = path[i + 1] - path[i - 1];
    dir.Normalize();
    var side = new Vector2(-dir.y, dir.x);
    float t = total > 0f ? cum[i] / total : 0f;
    float half = river.WidthAt(t) * 0.5f * 1.3f + 1.2f;
    float y = surface[Mathf.Min(i, surface.Length - 1)] + 0.02f;
    var c = path[i];
    var l = c - side * half; var r = c + side * half;
    verts[i * 3] = new Vector3(l.x, y, l.y); verts[i * 3 + 1] = new Vector3(c.x, y, c.y); verts[i * 3 + 2] = new Vector3(r.x, y, r.y);
    float v = cum[i] / 10f;
    uvs[i * 3] = new Vector2(0f, v); uvs[i * 3 + 1] = new Vector2(0.5f, v); uvs[i * 3 + 2] = new Vector2(1f, v);
    cols[i * 3] = new Color(1, 1, 1, 0); cols[i * 3 + 1] = Color.white; cols[i * 3 + 2] = new Color(1, 1, 1, 0);
    flow[i * 3] = flow[i * 3 + 1] = flow[i * 3 + 2] = dir;
    if (i < n - 1) {
     int a0 = i * 3, b0 = (i + 1) * 3;
     // Wound clockwise seen from above so the surface faces up (single-sided water shaders cull the
     // underside; the earlier order faced down and the realm water culled every river).
     tris.AddRange(new[] { a0, a0 + 1, b0, a0 + 1, b0 + 1, b0, a0 + 1, a0 + 2, b0 + 1, a0 + 2, b0 + 2, b0 + 1 });
    }
   }
   var mesh = new Mesh { name = river.Name + " surface" };
   mesh.vertices = verts; mesh.uv = uvs; mesh.uv2 = flow; mesh.colors = cols; mesh.SetTriangles(tris, 0);
   mesh.RecalculateNormals(); mesh.RecalculateBounds();
   return mesh;
  }

  // Bridge helper: "key [n] [metres]" stands the hero a few metres from the n-th placement of a kit key
  // (default the first, 7 m), on the side with the clearest view, and turns the camera to face it.
  public static void DebugVisitKey(string arg) {
   var realm = Instance;
   var gm = OpenWorldManager.I;
   if (!realm || realm.set == null || !gm || !gm.Hero) { Debug.Log("[Realm] visit: not ready"); return; }
   var parts = (arg ?? "").Trim().Split(' ');
   string key = parts[0];
   int nth = parts.Length > 1 && int.TryParse(parts[1], out int n) ? n : 0;
   float dist = parts.Length > 2 && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d) ? d : 7f;
   int seen = 0; bool found = false; Vector3 c = Vector3.zero;
   for (int i = 0; i < realm.set.Count; i++) {
    var p = realm.set.Get(i);
    if (p.key != key) continue;
    if (seen++ == nth) { c = p.pos; found = true; break; }
   }
   if (!found) { Debug.Log("[Realm] visit: no placement #" + nth + " of " + key + " (" + seen + " placed)"); return; }
   var terrain = gm.ActiveTerrain;
   float GroundY(Vector3 q) => terrain ? terrain.SampleHeight(q) + terrain.transform.position.y : q.y;
   var focus = c + Vector3.up * 1.2f;
   Vector3 stand = c + new Vector3(-dist, 0f, 0f);
   float best = -1f;
   for (int i = 0; i < 8; i++) {
    float a = i * Mathf.PI / 4f;
    var cand = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * dist;
    var eye = cand; eye.y = GroundY(cand) + 2.2f;
    var dir = eye - focus;
    float clear = Physics.Raycast(focus, dir.normalized, out var hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : dir.magnitude + 5f;
    clear -= Mathf.Abs(eye.y - focus.y) * 0.3f;
    if (clear > best) { best = clear; stand = cand; }
   }
   gm.Hero.WarpTo(stand);
   var rig = gm.CameraRig;
   if (rig) {
    var look = c - stand; look.y = 0f;
    rig.Yaw = Quaternion.LookRotation(look.normalized).eulerAngles.y + 24f; // target beside the hero, not behind
    rig.Pitch = 16f;
    rig.SnapToTarget();
   }
   Debug.Log("[Realm] visiting " + key + " #" + nth + " at " + c.ToString("0.0") + " of " + seen + "+");
  }

  // Fire-pit props ship a built-in particle fire on Unity's default particle material, which renders as
  // flat squares in URP; the realm lights its fire pits with the Vefects fire instead.
  public static void StripLegacyFire(GameObject go) {
   if (!go) return;
   foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) {
    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    var main = ps.main; main.playOnAwake = false;
    var r = ps.GetComponent<ParticleSystemRenderer>();
    if (r) r.enabled = false;
   }
  }

  Material FallbackWater() {
   var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Realm Water (fallback)" };
   m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
   m.SetColor("_BaseColor", new Color(0.16f, 0.42f, 0.5f, 0.72f));
   m.SetFloat("_Smoothness", 0.94f); m.SetFloat("_Metallic", 0f);
   m.SetOverrideTag("RenderType", "Transparent");
   m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
   m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
   m.renderQueue = (int)RenderQueue.Transparent;
   ownedMaterials.Add(m);
   return m;
  }

  Material FallbackIce() {
   var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Realm Ice (fallback)" };
   m.SetColor("_BaseColor", new Color(0.78f, 0.9f, 0.97f));
   m.SetFloat("_Smoothness", 0.88f);
   ownedMaterials.Add(m);
   return m;
  }

  void OnDestroy() {
   if (Instance == this) Instance = null;
   foreach (var m in ownedMaterials) if (m) Destroy(m);
   foreach (var m in ownedMeshes) if (m) Destroy(m);
   OpenWorldWorldKit.Unload();
  }
 }
}




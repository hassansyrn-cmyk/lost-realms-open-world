using UnityEngine;

namespace LostRealmsOW {
 // Abandoned hunter's rest (GLM): a bedroll, a few supplies and a working cooking campfire in a
 // clearing off the Verdant hunting route, with one readable hunter's note. Streams like the
 // hunting-route discoveries (build <95 m, tear down >135 m) and rebuilds identically, so returning
 // never duplicates anything. The campfire is the existing cooking campfire (CookMeal consumes one
 // raw meat via the standard Interact); the note uses the existing FIELD NOTE banner + journal
 // record. No enemies, no rewards, no new assets.
 public sealed class OpenWorldHuntersRest : MonoBehaviour {
  public static OpenWorldHuntersRest Instance { get; private set; }
  public static readonly Vector3 CampAnchor = new Vector3(55f, 0f, 58f);
  public const string NoteId = "glm-rest:note";
  public const string NoteText = "Rest easy, hunter. The deer bolt if you sprint — walk the last stretch. Meat keeps no longer than your hunger: one piece per campfire, cooked hot. — E.";
  public const string FireTitle = "HUNTER'S REST CAMPFIRE";

  OpenWorldManager gm;
  GameObject cluster;
  OpenWorldCampfire campfire;
  Vector3 noteSpot;
  bool noteBuilt;
  float nextTick;

  // Diagnostics for the bridge validation.
  public static bool Built => Instance && Instance.cluster;
  public static int CampfireCount => Instance && Instance.campfire ? 1 : 0;

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Install() { if (!FindAnyObjectByType<OpenWorldHuntersRest>()) new GameObject("Hunter's rest").AddComponent<OpenWorldHuntersRest>(); }
  void Awake() { Instance = this; }
  void OnDestroy() { Clear(); if (Instance == this) Instance = null; }

  void Update() {
   gm = OpenWorldManager.I;
   if (!gm || !gm.Hero || !gm.ActiveTerrain || !gm.RealmActive) return;
   // The note reads with F when close, exactly like the hunting-route field notes.
   if (noteSpot != Vector3.zero && cluster && !gm.IsModalOpen && !gm.Hero.Dead
       && Vector3.Distance(gm.Hero.transform.position, noteSpot) < 2.6f
       && Input.GetKeyDown(KeyCode.F)) ReadNote();
   if (Time.unscaledTime < nextTick) return;
   nextTick = Time.unscaledTime + 0.2f;
   Vector3 delta = gm.Hero.transform.position - CampAnchor; delta.y = 0;
   if (delta.magnitude > 135f) { Clear(); return; }
   if (!cluster && delta.magnitude < 95f) Build();
  }

  // The F-key path and the bridge call share this entry point.
  public static void ReadNote() {
   var rest = Instance;
   var gm = OpenWorldManager.I;
   if (!rest || !gm || !rest.cluster || !rest.noteBuilt) return;
   bool first = gm.CollectedWorldIds.Add(NoteId);
   if (first) gm.MarkProgressDirty();
   gm.ShowBanner("FIELD NOTE: " + NoteText);
   if (first) OpenWorldQuestJournal.Instance?.Record("FIELD NOTE", NoteText);
  }

  void Build() {
   cluster = new GameObject("Hunter's rest camp");
   cluster.transform.SetParent(gm.WorldRoot, false);
   if (!SafeGround(CampAnchor, out var centre)) centre = gm.HuntGround(CampAnchor);

   // Bedroll: composed from primitives (no bedroll mesh exists in any pack) — leather roll, pillow,
   // and a folded jacket at the foot, angled off the fire.
   Part(cluster.transform, "Bedroll roll", centre + Quaternion.Euler(0, 24, 0) * new Vector3(-1.5f, 0.07f, 0.1f),
        Quaternion.Euler(0, 24, 0), new Vector3(0.85f, 0.13f, 1.9f), new Color(0.36f, 0.22f, 0.16f));
   Part(cluster.transform, "Bedroll pillow", centre + Quaternion.Euler(0, 24, 0) * new Vector3(-1.85f, 0.13f, -0.62f),
        Quaternion.Euler(0, 24, 0), new Vector3(0.42f, 0.1f, 0.34f), new Color(0.72f, 0.68f, 0.58f));
   Part(cluster.transform, "Folded jacket", centre + Quaternion.Euler(0, 24, 0) * new Vector3(-1.2f, 0.06f, 0.72f),
        Quaternion.Euler(0, 40, 0), new Vector3(0.4f, 0.09f, 0.3f), new Color(0.2f, 0.26f, 0.3f));

   // Supplies: a kit crate, a supply sack and a barrel by a fallen log seat.
   Kit(cluster.transform, "village_crate_01", centre + new Vector3(1.6f, 0, 0.9f), 12f);
   Kit(cluster.transform, "town_storage_bag_potato", centre + new Vector3(2.3f, 0, 0.2f), 78f);
   Kit(cluster.transform, "village_barrel_01", centre + new Vector3(-2.4f, 0, -1.1f), 41f);
   Kit(cluster.transform, "log_fallen", centre + new Vector3(0.6f, 0, -1.7f), 64f);

   // The campfire: the existing cooking campfire, registered so the normal Interact path cooks there.
   var firePos = gm.HuntGround(centre + new Vector3(0.4f, 0, 0.3f));
   campfire = OpenWorldCampfire.Spawn(FireTitle, firePos, cluster.transform);
   if (!gm.Campfires.Contains(campfire)) gm.Campfires.Add(campfire);

   // The hunter's note: parchment pinned to a log beside the bedroll.
   var noteRoot = new GameObject("Hunter's note");
   noteRoot.transform.SetParent(cluster.transform, false);
   noteSpot = gm.HuntGround(centre + Quaternion.Euler(0, 24, 0) * new Vector3(-2.5f, 0, -0.9f));
   noteRoot.transform.position = noteSpot;
   OpenWorldTrailNote.Build(noteRoot.transform);
   noteBuilt = true;
  }

  void Kit(Transform parent, string key, Vector3 pos, float yaw) {
   var prefab = OpenWorldWorldKit.Prefab(key);
   if (!prefab) { Debug.LogWarning("[HuntersRest] missing kit prefab " + key); return; }
   var go = Instantiate(prefab, cluster.transform);
   go.name = "Hunter's rest • " + key;
   var p = gm.HuntGround(pos);
   go.transform.position = p;
   go.transform.rotation = Quaternion.Euler(0, yaw, 0);
   // Settle onto the ground by the renderer's lowest point so nothing floats or sinks.
   var bounds = OpenWorldMaterialHelper.ComputeRendererBounds(go.transform);
   go.transform.position += new Vector3(0, p.y - bounds.min.y, 0);
  }

  bool SafeGround(Vector3 anchor, out Vector3 p) {
   Vector3 bestPoint = anchor; float best = float.MaxValue; bool found = false;
   for (int i = 0; i < 48; i++) {
    var candidate = gm.HuntGround(anchor + Quaternion.Euler(0, i * 137.5f, 0) * Vector3.forward * (i == 0 ? 0 : 1 + i * 0.13f));
    if (OpenWorldTerrainSculptor.WaterAt(candidate.x, candidate.z, out _, out _)) continue;
    if (OpenWorldPopulation.Blocked(candidate, 2.5f, 2f)) continue;
    bool level = true;
    for (int k = 0; k < 8; k++) {
     var q = candidate + Quaternion.Euler(0, k * 45, 0) * Vector3.forward * 1.8f;
     if (Mathf.Abs(gm.HuntGround(q).y - candidate.y) > 0.3f) { level = false; break; }
    }
    if (!level) continue;
    float grass = OpenWorldWildlife.GrassAt(gm.ActiveTerrain, candidate);
    if (grass > 0.72f) continue; // avoid deep meadow grass swallowing the props
    float score = grass + (candidate - anchor).sqrMagnitude * 0.18f;
    if (score < best) { best = score; bestPoint = candidate; found = true; }
   }
   p = bestPoint;
   return found;
  }

  static void Part(Transform parent, string name, Vector3 worldPos, Quaternion rot, Vector3 size, Color color) {
   var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
   obj.name = name;
   obj.transform.SetParent(parent, false);
   obj.transform.position = worldPos;
   obj.transform.rotation = rot;
   obj.transform.localScale = size;
   Destroy(obj.GetComponent<Collider>());
   obj.GetComponent<Renderer>().sharedMaterial = OpenWorldMaterialHelper.CreateLit("Hunters rest " + name, color);
  }

  static void Check(bool condition, string text) { if (!condition) throw new System.Exception(text); }
  static void Log(string line) {
   System.IO.File.AppendAllText("Docs/glm-huntersrest-validation.txt", System.DateTime.Now.ToString("HH:mm:ss ") + line + "\n");
   Debug.Log("[HuntersRest] " + line);
  }

  // Bridge checks (Docs/glm-huntersrest-validation.txt).
  public static void ValidateBuilt() {
   var rest = Instance; var gm = OpenWorldManager.I;
   Check(rest && rest.cluster && rest.campfire, "Camp not built at the anchor");
   int fires = 0; foreach (var c in gm.Campfires) if (c && c.CampfireTitle == FireTitle) fires++;
   Check(fires == 1, "Duplicate campfires registered: " + fires);
   Log("built OK: cluster live, campfire registered once, 0 duplicates");
  }
  public static void SeedMeat() {
   var gm = OpenWorldManager.I; Check(gm, "World required");
   gm.RawMeat = 2;
   Log("RawMeat seeded to 2 for the cooking check");
  }
  public static void ValidateCook() {
   var rest = Instance; var gm = OpenWorldManager.I;
   Check(rest && rest.campfire, "Camp not built");
   int before = gm.RawMeat;
   Check(before >= 1, "RawMeat not seeded (is " + before + ")");
   rest.campfire.CookMeal(gm);
   Check(gm.RawMeat == before - 1, "Cooking consumed " + (before - gm.RawMeat) + " meat, expected exactly 1");
   Log("cook OK: RawMeat " + before + " -> " + gm.RawMeat + " at the rest campfire (1 consumed)");
  }
  public static void ValidateRebuilt() {
   var rest = Instance; var gm = OpenWorldManager.I;
   Check(rest && rest.cluster && rest.campfire, "Camp did not rebuild after leaving");
   int fires = 0; foreach (var c in gm.Campfires) if (c && c.CampfireTitle == FireTitle) fires++;
   Check(fires == 1, "Duplicate campfires after return: " + fires);
   Log("rebuild OK: cluster rebuilt after leave/return, exactly one campfire, no duplicates");
  }
  public static void ValidateNote() {
   var journal = OpenWorldQuestJournal.Instance; Check(journal, "Journal missing");
   bool found = false;
   foreach (var line in journal.Dialogue()) if (line.Contains("Rest easy, hunter")) found = true;
   Check(found, "Note not found in the journal");
   var gm = OpenWorldManager.I; Check(gm && gm.CollectedWorldIds.Contains(NoteId), "Note flag missing");
   Log("note OK: recorded in the journal and flagged glm-rest:note");
  }
  public static void ValidateGone() {
   var rest = Instance; var gm = OpenWorldManager.I;
   Check(rest && !rest.cluster, "Camp did not tear down after leaving");
   int fires = 0; foreach (var c in gm.Campfires) if (c && c.CampfireTitle == FireTitle) fires++;
   Check(fires == 0, "Campfire still registered after tear-down");
   Log("leave OK: cluster torn down, campfire unregistered");
  }

  void Clear() {
   if (gm && campfire) gm.Campfires.Remove(campfire);
   if (cluster) Destroy(cluster);
   cluster = null; campfire = null;
  }
 }
}

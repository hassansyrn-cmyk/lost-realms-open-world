using UnityEngine;

namespace LostRealmsOW {
 // Gathering (GLM, shortlist item 2): herb bushes and ore rocks near the village, the hunter's rest
 // camp and each region's anchor. Walking into a node (2.2 m, no button — Android-safe) yields one
 // Wild Herb or one Ore, with a combat popup like the meat pickups; nodes hide and respawn after
 // 120 s. Herbs feed two new cooking recipes (herbal stew, aether tea) and both gatherables sell to
 // Trader Fenn. Counters persist through the adventure save like RawMeat does.
 public static class GlmGathering {
  public const string HerbFlagBase = "glm-gather:";
  public const float RespawnSeconds = 120f;

  public struct NodeSpec { public string Kind, Prop, Texture, Label; public Vector3 Spot; public float Scale, Tint; }
  // Positions reuse anchors validated by earlier content (village plaza, camp, discovery notes).
  static readonly NodeSpec[] Specs = {
   new NodeSpec { Kind = "herb", Prop = "Props/Nature/Flowers_01", Texture = "Props/Textures/Nature_basecolor", Label = "WILD HERBS", Spot = new Vector3(6f, 0f, 8f), Scale = .8f, Tint = 1f },
   new NodeSpec { Kind = "herb", Prop = "Props/Nature/Flowers_01", Texture = "Props/Textures/Nature_basecolor", Label = "WILD HERBS", Spot = new Vector3(-7f, 0f, 15f), Scale = .8f, Tint = 1f },
   new NodeSpec { Kind = "ore", Prop = "Props/Nature/Rock_03", Texture = "Props/Textures/Nature_basecolor", Label = "ORE VEIN", Spot = new Vector3(-24f, 0f, -20f), Scale = .7f, Tint = 1f },
   new NodeSpec { Kind = "herb", Prop = "Props/Nature/Flowers_01", Texture = "Props/Textures/Nature_basecolor", Label = "WILD HERBS", Spot = new Vector3(52.5f, 0f, 55.5f), Scale = .8f, Tint = 1f },
   new NodeSpec { Kind = "ore", Prop = "Props/Nature/Rock_03", Texture = "Props/Textures/Nature_basecolor", Label = "ORE VEIN", Spot = new Vector3(60.5f, 0f, 56.5f), Scale = .7f, Tint = 1f },
   new NodeSpec { Kind = "herb", Prop = "Props/Nature/Flowers_01", Texture = "Props/Textures/Nature_snow", Label = "FROST HERBS", Spot = new Vector3(-82f, 0f, 15f), Scale = .8f, Tint = 1f },
   new NodeSpec { Kind = "ore", Prop = "Props/Nature/Rock_03", Texture = "Props/Textures/Nature_snow", Label = "ORE VEIN", Spot = new Vector3(-74f, 0f, 8f), Scale = .7f, Tint = 1f },
   new NodeSpec { Kind = "herb", Prop = "Props/Nature/Flowers_01", Texture = "Props/Textures/Desert_palette", Label = "SUN HERBS", Spot = new Vector3(293f, 0f, -4f), Scale = .8f, Tint = 1f },
   new NodeSpec { Kind = "ore", Prop = "Props/Nature/Rock_03", Texture = "Props/Textures/Desert_palette", Label = "ORE VEIN", Spot = new Vector3(300f, 0f, -12f), Scale = .7f, Tint = 1f },
   new NodeSpec { Kind = "herb", Prop = "Props/Nature/Flowers_01", Texture = "Props/Textures/Nature_basecolor", Label = "MARSH HERBS", Spot = new Vector3(-186f, 0f, -202f), Scale = .8f, Tint = 1f },
   new NodeSpec { Kind = "ore", Prop = "Props/Nature/Rock_03", Texture = "Props/Textures/Nature_basecolor", Label = "ORE VEIN", Spot = new Vector3(-194f, 0f, -210f), Scale = .7f, Tint = 1f },
   new NodeSpec { Kind = "herb", Prop = "Props/Nature/Flowers_01", Texture = "Props/Textures/Nature_basecolor", Label = "STORM HERBS", Spot = new Vector3(226f, 0f, -246f), Scale = .8f, Tint = 1f },
   new NodeSpec { Kind = "ore", Prop = "Props/Nature/Rock_03", Texture = "Props/Textures/Nature_basecolor", Label = "ORE VEIN", Spot = new Vector3(218f, 0f, -254f), Scale = .7f, Tint = 1f },
  };

  static GameObject root;
  public static int NodeCount => Specs.Length;

  // Called every frame from GlmQuests.Tick; builds the node field once, then the nodes run themselves.
  public static void Tick(OpenWorldManager gm) {
   if (!gm || !gm.Hero || !gm.ActiveTerrain) return;
   if (root) return;
   root = new GameObject("Gathering nodes");
   root.transform.SetParent(gm.WorldRoot, false);
   int built = 0;
   for (int i = 0; i < Specs.Length; i++) {
    if (!FindSpot(gm, Specs[i].Spot, out var spot)) { Debug.LogWarning("[Gather] no safe spot for node " + i + " at " + Specs[i].Spot.ToString("0.0")); continue; }
    var node = new GameObject("GatherNode " + Specs[i].Kind + " " + i);
    node.transform.SetParent(root.transform, false);
    node.transform.position = spot;
    var tag = node.AddComponent<GatherNode>();
    tag.Init(gm, Specs[i], i, spot);
    built++;
   }
   Debug.Log("[Gather] " + built + "/" + Specs.Length + " nodes built");
  }

  // Primary spot with two fallback offsets, flat-ground and water checks (same bar as discovery spots).
  static bool FindSpot(OpenWorldManager gm, Vector3 primary, out Vector3 best) {
   Vector3[] candidates = { primary, primary + new Vector3(-2f, 0f, -2f), primary + new Vector3(2.4f, 0f, 1.6f), primary + new Vector3(-1.6f, 0f, 2.4f) };
   foreach (var c in candidates) {
    var p = gm.HuntGround(c);
    if (OpenWorldTerrainSculptor.WaterAt(p.x, p.z, out _, out _)) continue;
    float lo = p.y, hi = p.y;
    for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) {
     var q = p + new Vector3(x, 0, z) * 1.1f;
     float y = gm.ActiveTerrain.SampleHeight(q) + gm.ActiveTerrain.transform.position.y;
     lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
    }
    if (hi - lo <= 1.1f) { best = new Vector3(p.x, hi + .02f, p.z); return true; }
   }
   best = default;
   return false;
  }
 }

 public sealed class GatherNode : MonoBehaviour {
  string kind, label;
  int index;
  GameObject prop;
  TextMesh sign;
  float respawnAt;
  bool gathered;
  static readonly System.Collections.Generic.HashSet<GatherNode> Live = new System.Collections.Generic.HashSet<GatherNode>();
  void OnEnable(){Live.Add(this);}void OnDisable(){Live.Remove(this);}
  public bool CanMine(OpenWorldManager gm) => kind=="ore"&&!gathered&&prop&&isActiveAndEnabled&&gm&&gm.Hero&&!gm.Hero.Dead&&!gm.Hero.IsMounted&&!gm.IsModalOpen&&gm.Ore<999&&Vector3.Distance(gm.Hero.transform.position,transform.position)<=2.8f;
  public static GatherNode NearestOre(OpenWorldManager gm){GatherNode best=null;float distance=float.MaxValue;foreach(var n in Live)if(n&&n.CanMine(gm)){float d=(n.transform.position-gm.Hero.transform.position).sqrMagnitude;if(d<distance){distance=d;best=n;}}return best;}
  public bool TryMine(OpenWorldManager gm){if(!CanMine(gm))return false;Collect(gm);return true;}


  public void Init(OpenWorldManager gm, GlmGathering.NodeSpec? spec, int i, Vector3 spot) {
   var s = spec.Value;
   kind = s.Kind; label = s.Label; index = i;
   prop = kind == "ore" ? OpenWorldOreVisual.Build(transform) : OpenWorldMaterialHelper.SpawnModel(s.Prop, s.Texture, transform, spot, s.Scale, new Color(s.Tint, s.Tint, s.Tint), true);
   if (!prop) { Debug.LogWarning("[Gather] prop missing for node " + i); enabled = false; return; }
   foreach (var col in prop.GetComponentsInChildren<Collider>(true)) col.enabled = false; // cosmetic; gather is proximity-based
   var signGo = new GameObject("Node label");
   signGo.transform.SetParent(transform, false);
   signGo.transform.localPosition = Vector3.up * 1.15f;
   sign = signGo.AddComponent<TextMesh>();
   sign.text = label;
   sign.anchor = TextAnchor.MiddleCenter; sign.characterSize = .05f; sign.fontSize = 30;
   sign.color = kind == "herb" ? new Color(.75f, 1f, .78f) : new Color(.85f, .88f, .95f);
   // Stagger respawns so the field never refills all at once.
   gathered = kind == "ore" && OreReadyAt(gm) > System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
   if (gathered) { prop.SetActive(false); sign.gameObject.SetActive(false); }
  }

  long OreReadyAt(OpenWorldManager gm){string prefix="ore-ready:"+index+":";foreach(string flag in gm.CollectedWorldIds)if(flag.StartsWith(prefix)&&long.TryParse(flag.Substring(prefix.Length),out long ready))return ready;return 0;}
  void Update() {
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero || !gm.ActiveTerrain) return;
   if (gathered) {
    if (kind == "ore" ? System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= OreReadyAt(gm) : Time.unscaledTime >= respawnAt) {
     gathered = false;
     if (prop) prop.SetActive(true);
     if (sign) sign.gameObject.SetActive(true);
    }
    return;
   }
   float dist = Vector3.Distance(gm.Hero.transform.position, transform.position);
   if (sign) {
    bool show = dist <= 11f;
    if (sign.gameObject.activeSelf != show) sign.gameObject.SetActive(show);
    if (show && Camera.main) sign.transform.rotation = Camera.main.transform.rotation;
   }
   if (dist > 2.2f || gm.IsModalOpen || gm.Hero.Dead || gm.Hero.IsMounted) return;
   if (kind == "ore") return; // Ore requires an explicit Mine press.
   Collect(gm);
  }

  void Collect(OpenWorldManager gm) {
   // Consume before granting rewards, so repeated clicks cannot double-collect.
   gathered = true;
   if(kind=="ore"){string prefix="ore-ready:"+index+":";gm.CollectedWorldIds.RemoveWhere(f=>f.StartsWith(prefix));gm.CollectedWorldIds.Add(prefix+(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()+86400));}
   respawnAt = Time.unscaledTime + GlmGathering.RespawnSeconds + index * 3f;
   if (prop) prop.SetActive(false);
   if (sign) sign.gameObject.SetActive(false);
   if (kind == "herb") {
    gm.Herbs = Mathf.Min(999, gm.Herbs + 1);
    OpenWorldCombatPopup.Spawn(transform.position + Vector3.up * 1.2f, "+1 WILD HERB", new Color(.6f, 1f, .65f), 1.1f);
   } else {
    gm.Ore = Mathf.Min(999, gm.Ore + 1);
    OpenWorldCombatPopup.Spawn(transform.position + Vector3.up * 1.2f, "+1 ORE", new Color(.82f, .87f, 1f), 1.1f);
   }
   gm.SpawnBurst(transform.position + Vector3.up * .6f, kind == "herb" ? new Color(.5f, .95f, .55f) : new Color(.7f, .8f, 1f), 10);
   OpenWorldAudio.Play("sfx_upgrade", .4f);
   gm.MarkProgressDirty();
   Debug.Log("[Gather] " + label + " -> " + (kind == "herb" ? "herbs " + gm.Herbs : "ore " + gm.Ore));
  }
 }

 public sealed partial class OpenWorldManager {
  // Session-carried by the adventure save (see CaptureAdventure/RestoreAdventure edits).
  public int Herbs;
  public int Ore;
 }

 public static class OpenWorldGatheringDebug {
  // Bridge entry points (static only).
  public static void Seed(string arg) {
   var gm = OpenWorldManager.I;
   if (!gm) return;
   gm.Herbs = Mathf.Min(999, gm.Herbs + 4);
   gm.Ore = Mathf.Min(999, gm.Ore + 2);
   Debug.Log("[Gather] seeded: herbs " + gm.Herbs + ", ore " + gm.Ore);
  }
  public static void Read(string arg) {
   var gm = OpenWorldManager.I;
   if (!gm) return;
   Debug.Log("[Gather] counts: herbs " + gm.Herbs + ", ore " + gm.Ore + ", raw meat " + gm.RawMeat);
  }
  // Cycles through all five recipes at the nearest campfire, logging inventory deltas per cook.
  public static void CookCycle(string arg) {
   var gm = OpenWorldManager.I;
   if (!gm) return;
   var fire = Object.FindAnyObjectByType<OpenWorldCampfire>();
   if (!fire) { Debug.LogError("[Gather] no campfire found"); return; }
   gm.RawMeat = Mathf.Max(gm.RawMeat, 6);
   gm.Herbs = Mathf.Max(gm.Herbs, 6);
   for (int i = 0; i < 5; i++) {
    int meat = gm.RawMeat, herbs = gm.Herbs;
    fire.CookMeal(gm);
    Debug.Log("[Gather] cook " + i + ": meat " + meat + "->" + gm.RawMeat + ", herbs " + herbs + "->" + gm.Herbs);
   }
  }
 }
}

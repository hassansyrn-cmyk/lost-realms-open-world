using UnityEngine;

namespace LostRealmsOW {
 // Living, animated, interactive Open-World NPCs (Blacksmith, Lorekeeper, Apothecary,
 // Vanguard Captain, Woodland Scout, and Highland Ranger) using the rigged humanoid prefabs
 // and glitch-free Playables animation driver.
 public sealed class OpenWorldNPC : MonoBehaviour {
  public string NpcId;
  public string DisplayName;
  public string RoleTitle;
  public string DialogueLine;
  public OpenWorldCharacterVisual Visual { get; private set; }

  Vector3 waypointA;
  Vector3 waypointB;
  bool patrols;
  bool headingToB = true;
  float pauseTimer;
  float gestureTimer;
  TextMesh nameplate;
  OpenWorldNpcRoutine routine;

  public static OpenWorldNPC Spawn(
   string id, string name, string roleTitle, string dialogue,
   int skinIndex, int weaponId, Vector3 posA, Vector3 posB, Transform parent) {

   posA.y = OpenWorldMaterialHelper.SampleGroundHeight(posA);
   posB.y = OpenWorldMaterialHelper.SampleGroundHeight(posB);

   var go = new GameObject("NPC_" + name);
   if (parent) go.transform.SetParent(parent, false);
   go.transform.position = posA;

   var npc = go.AddComponent<OpenWorldNPC>();
   npc.NpcId = id;
   npc.DisplayName = name;
   npc.RoleTitle = roleTitle;
   npc.DialogueLine = dialogue;
   npc.waypointA = posA;
   npc.waypointB = posB;
   npc.patrols = Vector3.Distance(posA, posB) > 1.5f;

   npc.Visual = OpenWorldCharacterVisual.Attach(go.transform, "Aster", skinIndex);
   if (npc.Visual && npc.Visual.RightHandBone && weaponId >= 0) {
    OpenWorldWeaponCatalog.AttachToHand(npc.Visual.RightHandBone, weaponId, out _);
   }
   npc.Visual?.Play("idle");
   npc.BuildNameplate();
   npc.routine = OpenWorldNpcRoutine.Attach(npc, posA, posB);
   return npc;
  }

  void BuildNameplate() {
   var lgo = new GameObject("NPCNameplate");
   lgo.transform.SetParent(transform, false);
   lgo.transform.localPosition = new Vector3(0f, 2.35f, 0f);
   nameplate = lgo.AddComponent<TextMesh>();
   nameplate.text = $"{DisplayName}\n<{RoleTitle}>";
   nameplate.fontSize = 28;
   nameplate.characterSize = 0.04f;
   nameplate.anchor = TextAnchor.MiddleCenter;
   nameplate.alignment = TextAlignment.Center;
   nameplate.color = new Color(1.0f, 0.90f, 0.45f);
   lgo.SetActive(false);
  }

  public void Converse(OpenWorldManager gm) {
   if (!gm || !gm.Hero) return;
   if (NpcId == "kael" && gm.ConverseVerdantHunt()) {
    gestureTimer = 2.5f; Visual?.Play("talk", true);
    Vector3 facing = gm.Hero.transform.position - transform.position; facing.y = 0f;
    if (facing.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(facing);
    return;
   }
   if (NpcId.StartsWith("glm-q") && gm.GlmQuestConverse(NpcId)) {
    gestureTimer = 2.5f; Visual?.Play("talk", true);
    Vector3 questFacing = gm.Hero.transform.position - transform.position; questFacing.y = 0f;
    if (questFacing.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(questFacing);
    return;
   }
   gestureTimer = Mathf.Clamp(Visual ? Visual.GetClipLength("talk") : 2f, 1.8f, 5f);
   Visual?.Play("talk", true);
   OpenWorldAudio.Play("sfx_upgrade", 0.55f);
   gm.SpawnBurst(transform.position + Vector3.up * 1.2f, new Color(1f, 0.88f, 0.38f), 16);

   // Face Aster
   Vector3 toHero = gm.Hero.transform.position - transform.position;
   toHero.y = 0f;
   if (toHero.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toHero.normalized);

   gm.Hero.Heal(gm.Hero.MaxHealth);
   gm.Hero.RestoreAether(gm.Hero.MaxAether);

   string spoken = routine && gm.CollectedWorldIds.Contains("npc-greeting:" + NpcId) ? routine.Dialogue(gm, DialogueLine) : DialogueLine;
   if (gm.CollectedWorldIds.Add("npc-greeting:" + NpcId)) {
    gm.MarkProgressDirty();
    gm.AddGold(20);
    gm.ShowBanner($"{DisplayName.ToUpperInvariant()}: \"{spoken}\" (+20 Gold, HP & Aether Restored)");
   } else {
    gm.ShowBanner($"{DisplayName.ToUpperInvariant()}: \"{spoken}\" (HP & Aether Restored)");
   }
  }

  void Update() {
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero || gm.IsModalOpen) return;

   Vector3 hp = gm.Hero.transform.position;
   float distToHero = Vector3.Distance(transform.position, hp);

   if (nameplate) {
    bool show = distToHero <= 18f;
    if (nameplate.gameObject.activeSelf != show) nameplate.gameObject.SetActive(show);
    if (show && Camera.main) nameplate.transform.rotation = Camera.main.transform.rotation;
   }

   if (gestureTimer > 0f) {
    gestureTimer -= Time.deltaTime;
    if (gestureTimer <= 0f) Visual?.Play("idle");
    return;
   }

   // When Aster is close, pause patrol and turn warmly toward Aster
   if (distToHero <= 4.2f) {
    Visual?.Play("idle");
    Vector3 toHero = hp - transform.position;
    toHero.y = 0f;
    if (toHero.sqrMagnitude > 0.01f) {
     transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toHero.normalized), 8f * Time.deltaTime);
    }
    return;
   }

   if (routine && routine.Tick(gm, distToHero)) return;
   if (!patrols) {
    Visual?.Play("idle");
    return;
   }

   if (pauseTimer > 0f) {
    pauseTimer -= Time.deltaTime;
    Visual?.Play("idle");
    return;
   }

   Vector3 target = headingToB ? waypointB : waypointA;
   Vector3 delta = target - transform.position;
   delta.y = 0f;
   if (delta.magnitude <= 0.45f) {
    headingToB = !headingToB;
    pauseTimer = 2.4f;
    Visual?.Play("idle");
    return;
   }

   Vector3 dir = delta.normalized;
   transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 9f * Time.deltaTime);
   Vector3 next = transform.position + dir * (2.35f * Time.deltaTime);
   next.y = OpenWorldMaterialHelper.SampleGroundHeight(next, transform);
   transform.position = next;
   Visual?.Play("walk");
  }
 }

 public static class OpenWorldVillageBuilder {
  public static void BuildVillagesAndNPCs(OpenWorldManager gm) {
   Transform root = new GameObject("VillagesAndSettlements").transform;
   root.SetParent(gm.WorldRoot, false);

   BuildCapitalOuterRing(gm, root);
   // The baked realm builds Oakvale, Frostpine and the caravan post from the World Kit at their
   // settlement sites; the legacy prop clusters would duplicate them.
   if (!OpenWorldRealm.Instance) {
    BuildOakvaleHamlet(gm, root);
    BuildFrostpineVillage(gm, root);
    BuildSunscarCaravanPost(gm, root);
   }
   SpawnKingdomNPCs(gm, root);
  }

  static void BuildCapitalOuterRing(OpenWorldManager gm, Transform parent) {
   var cap = new GameObject("CapitalOuterBuildings").transform;
   cap.SetParent(parent, false);

   // 5 Grand Multi-Story Medieval Buildings around the Hub outer perimeter (clear of camera corridor)
   (string path, Vector3 pos, float height, Vector3 lookAt)[] buildings = {
    ("Props/Village/rpgpp_lt_building_01", new Vector3(-21f, 0f, 11f), 6.4f, new Vector3(0f, 0f, 4f)),
    ("Props/Village/rpgpp_lt_building_02", new Vector3(21f, 0f, 11f), 6.4f, new Vector3(0f, 0f, 4f)),
    ("Props/Village/rpgpp_lt_building_03", new Vector3(-23f, 0f, -7f), 6.2f, new Vector3(0f, 0f, -2f)),
    ("Props/Village/rpgpp_lt_building_04", new Vector3(23f, 0f, -7f), 6.2f, new Vector3(0f, 0f, -2f)),
    ("Props/Village/rpgpp_lt_building_05", new Vector3(-16f, 0f, 22f), 6.5f, new Vector3(0f, 0f, 14f))
   };
   foreach (var b in buildings) {
    gm.ReserveSpot(b.pos, 6.5f);
    Vector3 p = b.pos;
    p.y = OpenWorldMaterialHelper.SampleGroundHeight(p);
    var go = OpenWorldMaterialHelper.SpawnModel(b.path, "Props/Textures/rpgpp_lt_tex_a", cap, p, b.height, Color.white, true);
    if (go) go.transform.LookAt(new Vector3(b.lookAt.x, p.y, b.lookAt.z));
   }

   // Market awnings, wooden fences, tables, chairs & fluttering village banners
   (string path, Vector3 pos, float height, float yaw)[] marketProps = {
    ("Props/Village/rpgpp_lt_awning_standing_01a", new Vector3(-15.5f, 0f, -3.5f), 2.8f, 75f),
    ("Props/Village/rpgpp_lt_awning_standing_01b", new Vector3(15.5f, 0f, -3.5f), 2.8f, -75f),
    ("Props/Village/rpgpp_lt_table_01", new Vector3(14.5f, 0f, 5.5f), 0.95f, 20f),
    ("Props/Village/rpgpp_lt_chair_01a", new Vector3(13.6f, 0f, 5.5f), 0.95f, 90f),
    ("Props/Village/rpgpp_lt_fence_wood_01a", new Vector3(-18f, 0f, 16f), 1.25f, 35f),
    ("Props/Village/rpgpp_lt_fence_wood_01b", new Vector3(18f, 0f, 16f), 1.25f, -35f),
    ("Props/Village/rpgpp_lt_banner_01a", new Vector3(-4.5f, 0f, 12.5f), 2.8f, 180f),
    ("Props/Village/rpgpp_lt_banner_01b", new Vector3(4.5f, 0f, 12.5f), 2.8f, 180f)
   };
   foreach (var mp in marketProps) {
    gm.ReserveSpot(mp.pos, 2.5f);
    Vector3 p = mp.pos;
    p.y = OpenWorldMaterialHelper.SampleGroundHeight(p);
    var go = OpenWorldMaterialHelper.SpawnModel(mp.path, "Props/Textures/rpgpp_lt_tex_a", cap, p, mp.height, Color.white, true);
    if (go) go.transform.rotation = Quaternion.Euler(0f, mp.yaw, 0f);
   }
  }

  static void BuildOakvaleHamlet(OpenWorldManager gm, Transform parent) {
   Vector3 center = new Vector3(44f, 0f, 42f);
   gm.ReserveSpot(center, 15f);
   var hamlet = new GameObject("OakvaleMillHamlet").transform;
   hamlet.SetParent(parent, false);

   (string path, Vector3 off, float height)[] houses = {
    ("Props/Village/rpgpp_lt_building_02", new Vector3(-6.5f, 0f, 4.5f), 5.8f),
    ("Props/Village/rpgpp_lt_building_03", new Vector3(6.5f, 0f, 4.5f), 5.8f),
    ("Props/Village/rpgpp_lt_building_04", new Vector3(0f, 0f, 8.2f), 6.0f),
    ("Props/Village/rpgpp_lt_well_01", new Vector3(0f, 0f, 0f), 2.3f),
    ("Props/Village/rpgpp_lt_awning_standing_01a", new Vector3(-5.2f, 0f, -2.5f), 2.6f),
    ("Props/Village/rpgpp_lt_bench_wood_01", new Vector3(3.5f, 0f, -1.8f), 0.95f)
   };
   foreach (var h in houses) {
    Vector3 p = center + h.off;
    p.y = OpenWorldMaterialHelper.SampleGroundHeight(p);
    var go = OpenWorldMaterialHelper.SpawnModel(h.path, "Props/Textures/rpgpp_lt_tex_a", hamlet, p, h.height, Color.white, true);
    if (go && h.off.sqrMagnitude > 1f) go.transform.LookAt(new Vector3(center.x, p.y, center.z));
   }
  }

  static void BuildFrostpineVillage(OpenWorldManager gm, Transform parent) {
   Vector3 center = new Vector3(-82f, 0f, 14f);
   gm.ReserveSpot(center, 15f);
   var snowVillage = new GameObject("FrostpineHighlandVillage").transform;
   snowVillage.SetParent(parent, false);

   (string path, Vector3 off, float height)[] snowStructures = {
    ("Props/Snow/Cottage_01", new Vector3(-6.2f, 0f, 5.0f), 5.4f),
    ("Props/Snow/Cottage_02", new Vector3(6.2f, 0f, 5.0f), 5.4f),
    ("Props/Snow/Well_01", new Vector3(0f, 0f, 0.5f), 2.3f),
    ("Props/Snow/Fence_01", new Vector3(-4.5f, 0f, -3.5f), 1.2f),
    ("Props/Snow/Fence_02", new Vector3(4.5f, 0f, -3.5f), 1.2f),
    ("Props/Snow/Sign_01", new Vector3(2.8f, 0f, -2.2f), 1.6f),
    ("Props/Snow/Pine_01", new Vector3(-9.5f, 0f, 2.0f), 7.2f),
    ("Props/Snow/Pine_02", new Vector3(9.5f, 0f, 2.0f), 7.5f),
    ("Props/Snow/Pine_03", new Vector3(0f, 0f, 9.5f), 8.0f)
   };
   foreach (var s in snowStructures) {
    Vector3 p = center + s.off;
    p.y = OpenWorldMaterialHelper.SampleGroundHeight(p);
    var go = OpenWorldMaterialHelper.SpawnModel(s.path, "Props/Textures/Nature_snow", snowVillage, p, s.height, Color.white, true);
    if (go && s.off.sqrMagnitude > 1f) go.transform.LookAt(new Vector3(center.x, p.y, center.z));
   }
  }

  static void BuildSunscarCaravanPost(OpenWorldManager gm, Transform parent) {
   Vector3 center = new Vector3(84f, 0f, -8f);
   gm.ReserveSpot(center, 14f);
   var oasis = new GameObject("SunscarCaravanPost").transform;
   oasis.SetParent(parent, false);

   (string path, Vector3 off, float height)[] caravanProps = {
    ("Props/Village/rpgpp_lt_awning_standing_01a", new Vector3(-4.5f, 0f, 3.8f), 2.9f),
    ("Props/Village/rpgpp_lt_awning_standing_01b", new Vector3(4.5f, 0f, 3.8f), 2.9f),
    ("Props/Village/rpgpp_lt_wagon_01", new Vector3(-5.8f, 0f, -2.2f), 2.2f),
    ("Props/Village/rpgpp_lt_barrel_01", new Vector3(-2.4f, 0f, 3.2f), 0.95f),
    ("Props/Village/rpgpp_lt_crate_01", new Vector3(2.4f, 0f, 3.2f), 0.90f),
    ("Props/Village/rpgpp_lt_vase_01", new Vector3(0f, 0f, 2.5f), 0.85f)
   };
   foreach (var cp in caravanProps) {
    Vector3 p = center + cp.off;
    p.y = OpenWorldMaterialHelper.SampleGroundHeight(p);
    var go = OpenWorldMaterialHelper.SpawnModel(cp.path, "Props/Textures/rpgpp_lt_tex_a", oasis, p, cp.height, Color.white, true);
    if (go && cp.off.sqrMagnitude > 1f) go.transform.LookAt(new Vector3(center.x, p.y, center.z));
   }
  }

  static void SpawnKingdomNPCs(OpenWorldManager gm, Transform parent) {
   // 1. Master Forge-Smith Brom (Capital Blacksmith)
   gm.NPCs.Add(OpenWorldNPC.Spawn(
    "brom", "Master Smith Brom", "Royal Forgemaster",
    "Keep your blade tempered, Aster! Heavy chops break shields, and Melt reactions shatter armor.",
    3, 6, new Vector3(-16.5f, 0f, 8.2f), new Vector3(-16.5f, 0f, 8.2f), parent));

   // 2. Elder Lorekeeper Lyra (Capital Study)
   gm.NPCs.Add(OpenWorldNPC.Spawn(
    "lyra", "Lorekeeper Lyra", "Scholar of the First Wardens",
    "Four Ancient Relic Shrines and the Sunken Crypt await across the kingdom.",
    5, -1, new Vector3(16.5f, 0f, 8.2f), new Vector3(16.5f, 0f, 8.2f), parent));

   // 3. Apothecary Rowan (Capital Herbalist Stall)
   gm.NPCs.Add(OpenWorldNPC.Spawn(
    "rowan", "Apothecary Rowan", "Alchemist & Healer",
    "Drink deep of this emerald draught—your vitality and Aether are restored!",
    4, -1, new Vector3(-14.2f, 0f, -3.2f), new Vector3(-14.2f, 0f, -3.2f), parent));

   // 4. Captain Vane (Patrolling Capital North Road)
   gm.NPCs.Add(OpenWorldNPC.Spawn(
    "vane", "Captain Vane", "Vanguard Commander",
    "Our scouts watch the three frontier rifts. Use grapple anchors to reach the skyward paths!",
    2, 1, new Vector3(-6.5f, 0f, 16.5f), new Vector3(6.5f, 0f, 16.5f), parent));

   // 5. Scout Warden Kael (Patrolling Oakvale Mill Hamlet)
   gm.NPCs.Add(OpenWorldNPC.Spawn(
    "kael", "Warden Kael", "Oakvale Forest Scout",
    "Welcome to Oakvale Hamlet! The northern trail leads straight to Heartwood's Grove.",
    1, 5, new Vector3(41f, 0f, 38.5f), new Vector3(47f, 0f, 38.5f), parent));

   // 6. Highland Ranger Mira (Stationed at Frostpine Village)
   Vector3 miraPost = OpenWorldRealm.Instance ? OpenWorldRegions.Frostpine + new Vector3(4.5f, 0f, -6f) : new Vector3(-78.5f, 0f, 11.5f);
   gm.NPCs.Add(OpenWorldNPC.Spawn(
    "mira", "Ranger Mira", "Frostpine Sentinel",
    "Beyond Frostpine Village lies the Glacial Rift and the Whiteout Frost Howler!",
    3, 7, miraPost, miraPost, parent));
  }
 }
}

using System.Collections.Generic;
using LostRealms;
using UnityEngine;

namespace LostRealmsOW {
 public sealed partial class OpenWorldManager : MonoBehaviour {
  public static OpenWorldManager I { get; private set; }

  public Transform WorldRoot { get; private set; }
  public Terrain ActiveTerrain { get; private set; }
  public OpenWorldRealm Realm { get; private set; }
  public bool RealmActive => Realm != null;
  public OpenWorldHero Hero { get; private set; }
  public OpenWorldCamera CameraRig { get; private set; }

  public readonly List<OpenWorldEnemy> Enemies = new List<OpenWorldEnemy>();
  public readonly List<OpenWorldOutpostCamp> Camps = new List<OpenWorldOutpostCamp>();
  public readonly List<TeleportGateController> Gates = new List<TeleportGateController>();
  public readonly List<OpenWorldCollectible> Collectibles = new List<OpenWorldCollectible>();

  public int Gold = 120;
  public int Gems = 10;
  public int KeysCollected;
  public const int TotalKeys = 3;
  public int ChestsOpened;
  public int CampsCaptured;
  public bool GuardianDefeated;

  public readonly HashSet<int> UnlockedWeapons = new HashSet<int> { 0, 1, 2 };
  public readonly HashSet<int> UnlockedSkins = new HashSet<int> { 0 };
  public bool DeveloperMode { get; private set; }
  int testOriginalWeapon, testOriginalSkin;
  public bool SetDeveloperMode(string password) {
   if (!DeveloperMode && password != "3258") return false;
   if (!DeveloperMode) {
    testOriginalWeapon = Hero ? Hero.EquippedWeaponId : 0;
    testOriginalSkin = Hero ? Hero.EquippedSkinId : 0;
    DeveloperMode = true;
   } else {
    DeveloperMode = false;
    if (Hero) { Hero.EquipSkin(testOriginalSkin); Hero.EquipWeapon(testOriginalWeapon); }
   }
   return true;
  }
  public int SavedWeapon => DeveloperMode ? testOriginalWeapon : Hero ? Hero.EquippedWeaponId : 0;
  public int SavedSkin => DeveloperMode ? testOriginalSkin : Hero ? Hero.EquippedSkinId : 0;

  public bool ShowPauseModal;
  bool pausedByMenu;
  float previousTimeScale = 1f;
  public void CloseMenus() {
   ShowPauseModal = ShowMapModal = ShowVaultModal = ShowArsenalModal = ShowWardrobeModal = ShowClockModal = ShowQuestBoardModal = ShowCreditsModal = false;
   SyncPause();
  }
  void SyncPause() {
   if (ShowPauseModal && !pausedByMenu) {
    previousTimeScale = Time.timeScale;
    Time.timeScale = 0;
    pausedByMenu = true;
   } else if (!ShowPauseModal && pausedByMenu) {
    Time.timeScale = previousTimeScale;
    pausedByMenu = false;
   }
  }
  public bool ShowMapModal;
  public bool ShowVaultModal;
  public bool ShowArsenalModal;
  public bool ShowWardrobeModal;
  public bool ShowClockModal;
  public bool ShowCreditsModal;
  public bool IsModalOpen => ShowPauseModal || ShowMapModal || ShowVaultModal || ShowArsenalModal || ShowWardrobeModal || ShowClockModal || ShowQuestBoardModal || ShowCreditsModal;

  string bannerText = "";
  float bannerUntil;
  public string CurrentBanner => Time.time < bannerUntil ? bannerText : "";
  Material burstMaterial;
  string lastChestResult = "";
  public string LastChestResult => lastChestResult;
  Vector3 respawnPoint = new Vector3(0f, 0f, -6f);

  struct ReservedCircle { public Vector2 xz; public float r; }
  readonly List<ReservedCircle> reserved = new List<ReservedCircle>();

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void AutoBootstrap() {
   if (I != null) return;
   var existing = FindAnyObjectByType<OpenWorldManager>();
   if (existing) { I = existing; return; }
   var go = new GameObject("OpenWorldManager");
   I = go.AddComponent<OpenWorldManager>();
  }

  void Awake() {
   if (I != null && I != this) { Destroy(gameObject); return; }
   I = this;
   if (Application.isMobilePlatform) {
    Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = false;
    Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
    Screen.orientation = ScreenOrientation.LandscapeLeft;
   }
   Input.simulateMouseWithTouches = false;
   gameObject.AddComponent<OpenWorldAudio>();
   gameObject.AddComponent<OpenWorldGraphics>();
  }

  void Start() {
   if (WorldRoot == null) BuildOpenWorld();
  }

  public void BuildOpenWorld() {
   if (adventureReady && saveDirty) SaveAdventure();
   adventureReady = false;
   saveDirty = saveBlocked = saveFailed = false;
   QuestStarted = GuardianAwakened = QuestCompleted = GuardianDefeated = SanctumCompleted = StormRelayAligned = false;
   SanctumFociMask = RelicsMask = BountiesMask = CryptMask = 0;
   CollectedWorldIds.Clear(); CapturedCampIds.Clear();
   SanctumFoci.Clear(); SanctumTraps.Clear(); SanctumGuards.Clear(); Updrafts.Clear();
   RelicShrines.Clear(); BountyElites.Clear(); GrappleAnchors.Clear(); NPCs.Clear(); Campfires.Clear();
   CryptDungeon = null; StormRelay = null;
   Gold = 120; Gems = 10; KeysCollected = ChestsOpened = CampsCaptured = 0;
   CheckpointId = "hub";
   if (WorldRoot) {
    WorldRoot.gameObject.SetActive(false);
    Destroy(WorldRoot.gameObject);
   }
   Enemies.Clear();
   Camps.Clear();
   Gates.Clear();
   Collectibles.Clear();
   reserved.Clear();

   WorldRoot = new GameObject("VerdantOpenWorld").transform;

   SetupLightingAndAudio();
   // The baked 1 km realm replaces the legacy 480 m procedural terrain when present.
   Realm = OpenWorldRealm.Available ? OpenWorldRealm.Build(WorldRoot, OpenWorldGraphics.Instance ? (int)OpenWorldGraphics.Instance.Current : 1) : null;
   if (Realm) {
    ActiveTerrain = Realm.Terrain;
    WorldRoot.gameObject.AddComponent<OpenWorldTrails>();
    Physics.SyncTransforms();
   } else {
    BuildTerrain();
    WorldRoot.gameObject.AddComponent<OpenWorldTrails>().Build(ActiveTerrain);
   }
   BuildHubTown();
   BuildOutpostCamps();
   BuildKeysChestsAndPotions();
   BuildGuardianBossArena();
   BuildForgottenSanctum();
   BuildLandmarksAndWaters();
   OpenWorldImportedContent.ReserveSites(this);
   if (!Realm) ScatterBiomeNature();
   BuildSkyIslands();
   OpenWorldVfx.Ensure(WorldRoot);
   OpenWorldAtmosphere.Attach(WorldRoot);
   OpenWorldSoundscape.Attach(WorldRoot);

   Physics.SyncTransforms();

   // Spawn Aster in the Heart of Realms Hub Plaza
   respawnPoint = new Vector3(0f, 0f, -6f);
   Hero = OpenWorldHero.Spawn(respawnPoint, WorldRoot, 0, 0);

   var cam = Camera.main;
   if (!cam) {
    var camGo = new GameObject("Main Camera");
    camGo.tag = "MainCamera";
    cam = camGo.AddComponent<Camera>();
    camGo.AddComponent<AudioListener>();
   }
   cam.nearClipPlane = 0.15f;
   cam.farClipPlane = Realm ? 1600f : 650f;
   cam.clearFlags = CameraClearFlags.Skybox;
   if (OpenWorldGraphics.Instance) OpenWorldGraphics.Instance.ConfigureCamera(cam);
   CameraRig = cam.GetComponent<OpenWorldCamera>() ?? cam.gameObject.AddComponent<OpenWorldCamera>();
   CameraRig.Target = Hero.transform;
   CameraRig.SnapToTarget();
   if (!OpenWorldMobileHud.Instance) new GameObject("Adventure HUD").AddComponent<OpenWorldMobileHud>();

   InitializeAdventure();
   OpenWorldImportedContent.Attach(WorldRoot);
   OpenWorldPopulation.Attach(WorldRoot);
  }

  void SetupLightingAndAudio() {
   RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
   RenderSettings.ambientSkyColor = new Color(0.56f, 0.68f, 0.78f);
   RenderSettings.ambientEquatorColor = new Color(0.45f, 0.52f, 0.46f);
   RenderSettings.ambientGroundColor = new Color(0.24f, 0.28f, 0.22f);
   RenderSettings.fog = true;
   RenderSettings.fogMode = FogMode.Linear;
   RenderSettings.fogColor = new Color(0.68f, 0.82f, 0.78f);
   RenderSettings.fogStartDistance = 85f;
   RenderSettings.fogEndDistance = 320f;

   var light = FindAnyObjectByType<Light>();
   if (!light) {
    var lgo = new GameObject("SunLight");
    lgo.transform.SetParent(WorldRoot, false);
    light = lgo.AddComponent<Light>();
   }
   light.type = LightType.Directional;
   light.color = new Color(1f, 0.96f, 0.86f);
   light.intensity = 1.25f;
   light.shadows = LightShadows.Soft;
   light.transform.rotation = Quaternion.Euler(46f, -38f, 0f);

   var musicGo = new GameObject("BiomeMusic");
   musicGo.transform.SetParent(WorldRoot, false);
   var audio = musicGo.AddComponent<AudioSource>();
   var clip = Resources.Load<AudioClip>("Audio/verdant_realm_theme");
   if (clip) {
    audio.clip = clip;
    audio.loop = true;
    audio.volume = 0.38f;
    audio.Play();
   }
  }

  void BuildTerrain() {
   const int res = 129;
   const float sizeXZ = 480f;
   const float sizeY = 44f;

   var td = new TerrainData {
    heightmapResolution = res,
    size = new Vector3(sizeXZ, sizeY, sizeXZ)
   };

   float[,] heights = new float[res, res];
   for (int z = 0; z < res; z++) {
    for (int x = 0; x < res; x++) {
     float wx = ((float)x / (res - 1) - 0.5f) * sizeXZ;
     float wz = ((float)z / (res - 1) - 0.5f) * sizeXZ;
     float distFromCenter = Mathf.Sqrt(wx * wx + wz * wz);

     // Multi-octave rolling hills with dramatic perimeter mountain ridges and flat playable POIs
     float hillPrimary = (Mathf.Sin(wx * 0.035f) * Mathf.Cos(wz * 0.035f) * 0.5f + 0.5f) * 0.075f;
     float hillDetail = Mathf.PerlinNoise(wx * 0.022f + 19f, wz * 0.022f + 37f) * 0.038f;
     float hill = hillPrimary + hillDetail;

     float ridgeNoise = 0.72f + 0.28f * Mathf.PerlinNoise(wx * 0.03f + 71f, wz * 0.03f + 91f);
     float rim = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((distFromCenter - 135f) / 85f)) * 0.42f * ridgeNoise;

     float hubFlat = Mathf.Clamp01((distFromCenter - 16f) / 26f);
     float bossDist = Vector2.Distance(new Vector2(wx, wz), new Vector2(0f, 115f));
     float bossFlat = Mathf.Clamp01((bossDist - 12f) / 22f);
     float sanctumDist = Vector2.Distance(new Vector2(wx, wz), new Vector2(-60f, -58f));
     float sanctumFlat = Mathf.Clamp01((sanctumDist - 10f) / 20f);

     heights[z, x] = (hill * hubFlat * bossFlat * sanctumFlat) + rim;
    }
   }
   td.SetHeights(0, 0, heights);

   var mossTex = Resources.Load<Texture2D>("Art/verdant_moss_tile") ?? Resources.Load<Texture2D>("Props/Textures/Nature_basecolor");
   if (mossTex) {
    var layer = new TerrainLayer {
     diffuseTexture = mossTex,
     tileSize = new Vector2(14f, 14f)
    };
    td.terrainLayers = new[] { layer };
   }

   var tgo = Terrain.CreateTerrainGameObject(td);
   tgo.name = "VerdantTerrain";
   tgo.transform.SetParent(WorldRoot, false);
   tgo.transform.position = new Vector3(-sizeXZ * 0.5f, 0f, -sizeXZ * 0.5f);
   ActiveTerrain = tgo.GetComponent<Terrain>();
   var mat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit") ?? OpenWorldMaterialHelper.LitShader);
   if (mossTex) {
    mat.mainTexture = mossTex;
    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mossTex);
   }
   ActiveTerrain.materialTemplate = mat;
   Physics.SyncTransforms();
  }

  public void ReserveSpot(Vector3 pos, float radius) {
   reserved.Add(new ReservedCircle { xz = new Vector2(pos.x, pos.z), r = radius });
  }

  bool SpawnKitTree(string key, Vector3 pos, float scale) {
   var prefab = OpenWorldWorldKit.Prefab(key);
   if (!prefab) return false;
   var tree = Instantiate(prefab, WorldRoot);
   tree.name = key;
   tree.transform.SetPositionAndRotation(pos - Vector3.up * 0.1f, Quaternion.Euler(0f, Mathf.Abs(pos.x * 37f) % 360f, 0f));
   tree.transform.localScale = Vector3.one * scale;
   return true;
  }

  bool IsSpotClear(Vector3 pos, float radius) {
   Vector2 p = new Vector2(pos.x, pos.z);
   foreach (var c in reserved) {
    if (Vector2.Distance(p, c.xz) < c.r + radius) return false;
   }
   return true;
  }

  void BuildHubTown() {
   ReserveSpot(Vector3.zero, 24f);

   // Hub Town structures arranged in a welcoming semicircle
   (string path, Vector3 pos, float height)[] structures = {
    ("Props/Village/rpgpp_lt_shed_wood_01", new Vector3(-13f, 0f, 8f), 4.6f),
    ("Props/Village/rpgpp_lt_shed_wood_02", new Vector3(13f, 0f, 8f), 4.6f),
    ("Props/Village/rpgpp_lt_well_01", new Vector3(-8f, 0f, 3f), 2.4f),
    ("Props/Village/rpgpp_lt_wagon_01", new Vector3(9f, 0f, 2f), 2.1f),
    ("Props/Village/rpgpp_lt_tree_01", new Vector3(-16f, 0f, -6f), 6.2f),
    ("Props/Village/rpgpp_lt_tree_02", new Vector3(16f, 0f, -6f), 6.2f)
   };
   foreach (var s in structures) {
    Vector3 p = s.pos;
    p.y = OpenWorldMaterialHelper.SampleGroundHeight(p);
    // In the realm the two plaza trees are the same painted trees as the rest of the Verdant (the
    // faceted poly-pack trees stood out among them).
    if (Realm && s.path.Contains("_tree_") && SpawnKitTree(s.path.EndsWith("01") ? "tree_blossom_02" : "tree_broadleaf_03", p, 0.85f)) continue;
    var b = OpenWorldMaterialHelper.SpawnModel(s.path, "Props/Textures/rpgpp_lt_tex_a", WorldRoot, p, s.height, Color.white, true);
    if (b) b.transform.LookAt(new Vector3(0f, p.y, 0f));
   }

   // Village props (benches, barrels, crates) around the cottages
   (string path, Vector3 pos, float height, float yaw)[] villageDetails = {
    ("Props/Village/rpgpp_lt_bench_wood_01", new Vector3(-5.2f, 0f, 5.5f), 0.95f, 145f),
    ("Props/Village/rpgpp_lt_bench_wood_01", new Vector3(5.2f, 0f, 5.5f), 0.95f, -145f),
    ("Props/Village/rpgpp_lt_barrel_01", new Vector3(-10.5f, 0f, 5.8f), 0.95f, 20f),
    ("Props/Village/rpgpp_lt_crate_01", new Vector3(10.8f, 0f, 5.6f), 0.88f, -30f),
    ("Props/Village/rpgpp_lt_vase_01", new Vector3(-3.2f, 0f, 4.4f), 0.78f, 0f),
    ("Props/Village/rpgpp_lt_vase_01", new Vector3(3.2f, 0f, 4.4f), 0.78f, 0f)
   };
   foreach (var vd in villageDetails) {
    Vector3 p = vd.pos;
    p.y = OpenWorldMaterialHelper.SampleGroundHeight(p);
    var prop = OpenWorldMaterialHelper.SpawnModel(vd.path, "Props/Textures/rpgpp_lt_tex_a", WorldRoot, p, vd.height, Color.white, true);
    if (prop) prop.transform.rotation = Quaternion.Euler(0f, vd.yaw, 0f);
   }

   // Lush floral & meadow accents framing the Hub Sanctuary (non-colliding so movement is silky smooth)
   (string path, string tex, Vector3 pos, float height)[] hubFoliage = {
    ("Props/Nature/Flowers_01", "Props/Textures/Nature_basecolor", new Vector3(-2.6f, 0f, 3.2f), 0.55f),
    ("Props/Nature/Flowers_01", "Props/Textures/Nature_basecolor", new Vector3(2.6f, 0f, 3.2f), 0.55f),
    ("Props/Village/rpgpp_lt_flower_01", "Props/Textures/rpgpp_lt_tex_a", new Vector3(-6.4f, 0f, 1.8f), 0.52f),
    ("Props/Village/rpgpp_lt_flower_02", "Props/Textures/rpgpp_lt_tex_a", new Vector3(6.4f, 0f, 1.8f), 0.52f),
    ("Props/Nature/Grass_01", "Props/Textures/Nature_basecolor", new Vector3(-4.2f, 0f, 7.2f), 0.62f),
    ("Props/Nature/Grass_02", "Props/Textures/Nature_basecolor", new Vector3(4.2f, 0f, 7.2f), 0.62f)
   };
   foreach (var hf in hubFoliage) {
    Vector3 p = hf.pos;
    p.y = OpenWorldMaterialHelper.SampleGroundHeight(p);
    OpenWorldMaterialHelper.SpawnModel(hf.path, hf.tex, WorldRoot, p, hf.height, Color.white, false);
   }

   // Central Sanctuary Trail Shrine
   Vector3 shrinePos = new Vector3(0f, 0f, 4f);
   shrinePos.y = OpenWorldMaterialHelper.SampleGroundHeight(shrinePos);
   OpenWorldMaterialHelper.SpawnModel("Props/Expansion/TrailShrine", "Props/Expansion/Textures/TrailShrine_basecolor", WorldRoot, shrinePos, 2.2f, Color.white, true);

   // Hub Fast-Travel Gate (discovered by default, grounded cleanly on terrain)
   SpawnFastTravelGate("hub", "Heart of Realms Hub", new Vector3(0f, 0f, 14f), true);
  }

  TeleportGateController SpawnFastTravelGate(string id, string title, Vector3 position, bool discovered) {
   ReserveSpot(position, 6f);
   position.y = OpenWorldMaterialHelper.SampleGroundHeight(position);

   var prefab = Resources.Load<GameObject>("Gates/TeleportGate_Verdant") ?? Resources.Load<GameObject>("Gates/TeleportGate_Root");
   GameObject gateGo;
   if (prefab) {
    gateGo = Instantiate(prefab, WorldRoot, false);
    gateGo.transform.position = position;
    // Align bottom of GateModel to terrain surface so the gate never sinks into the floor
    var gm = gateGo.transform.Find("GateModel");
    if (gm) {
     Bounds b = OpenWorldMaterialHelper.ComputeRendererBounds(gm);
     if (b.size.y > 0.01f) {
      gateGo.transform.position += Vector3.up * (position.y - b.min.y);
     }
    }
   } else {
    gateGo = new GameObject("Gate_" + id);
    gateGo.transform.SetParent(WorldRoot, false);
    gateGo.transform.position = position;
   }

   var ctrl = gateGo.GetComponent<TeleportGateController>() ?? gateGo.AddComponent<TeleportGateController>();
   ctrl.WaypointId = id;
   ctrl.WaypointTitle = title;
   ctrl.Discovered = discovered;
   ctrl.SetState(GateState.Active);
   Gates.Add(ctrl);
   return ctrl;
  }

  void BuildOutpostCamps() {
   (string title, Vector3 pos, string gateId, string[] guards)[] defs = {
    ("Whispering Woods Outpost", new Vector3(-65f, 0f, 65f), "woods", new[] { "Goblin", "Goblin", "IronGoblin" }),
    ("Sunlit Ruins Outpost", new Vector3(68f, 0f, 58f), "ruins", new[] { "IronGoblin", "Bomber", "StoneBrute" }),
    ("Bramble Ridge Outpost", new Vector3(62f, 0f, -62f), "ridge", new[] { "Goblin", "Caster", "Elite" })
   };

   foreach (var d in defs) {
    ReserveSpot(d.pos, 16f);
    var camp = OpenWorldOutpostCamp.Create(d.title, d.pos, WorldRoot, d.guards);
    camp.PersistentId = d.gateId;
    Camps.Add(camp);
    foreach (var g in camp.Guards) Enemies.Add(g);

    // Place a fast-travel gate 11m outside each outpost
    SpawnFastTravelGate(d.gateId, d.title + " Gate", d.pos + new Vector3(0f, 0f, -11f), false);
   }
  }

  void BuildKeysChestsAndPotions() {
   // 3 Collectible Temple Keys (disappear on pickup)
   Vector3[] keySpots = {
    new Vector3(-58f, 0f, 72f),
    new Vector3(74f, 0f, 64f),
    new Vector3(-60f, 0f, -58f)
   };
   string[] keyTitles = { "WOODLAND KEY", "SUNLIT KEY", "CRYPT KEY" };
   for (int i = 0; i < keySpots.Length; i++) {
    ReserveSpot(keySpots[i], 3f);
    Collectibles.Add(OpenWorldCollectible.Spawn(OWCollectibleKind.TempleKey, keySpots[i], WorldRoot, 1, keyTitles[i], "key:" + i));
   }

   // 4 World Treasure Chests (disappear on pickup)
   Vector3[] chestSpots = {
    new Vector3(-10f, 0f, -14f),
    new Vector3(-72f, 0f, 58f),
    new Vector3(60f, 0f, 68f),
    new Vector3(68f, 0f, -56f)
   };
   for (int i = 0; i < chestSpots.Length; i++) {
    ReserveSpot(chestSpots[i], 3f);
    Collectibles.Add(OpenWorldCollectible.Spawn(OWCollectibleKind.SecretChest, chestSpots[i], WorldRoot, 1, "REALM CACHE #" + (i + 1), "chest:" + i));
   }

   // Health Potions (Red) & Aether Potions (Purple) along the hub roads
   Vector3[] healthSpots = {
    new Vector3(-6f, 0f, -2f),
    new Vector3(-32f, 0f, 32f),
    new Vector3(34f, 0f, 28f),
    new Vector3(0f, 0f, 82f)
   };
   for (int i = 0; i < healthSpots.Length; i++) {
    var hp = healthSpots[i];
    ReserveSpot(hp, 2f);
    Collectibles.Add(OpenWorldCollectible.Spawn(OWCollectibleKind.HealthPotion, hp, WorldRoot, 1, "", "health:" + i));
   }

   Vector3[] aetherSpots = {
    new Vector3(9.5f, 0f, -4.5f), // beside the Sanctuary Hearth, not in its fire
    new Vector3(-28f, 0f, 36f),
    new Vector3(30f, 0f, -30f),
    new Vector3(5f, 0f, 86f)
   };
   for (int i = 0; i < aetherSpots.Length; i++) {
    var ap = aetherSpots[i];
    ReserveSpot(ap, 2f);
    Collectibles.Add(OpenWorldCollectible.Spawn(OWCollectibleKind.AetherPotion, ap, WorldRoot, 1, "", "aether:" + i));
   }
  }

  void BuildGuardianBossArena() {
   Vector3 arenaCenter = new Vector3(0f, 0f, 115f);
   ReserveSpot(arenaCenter, 26f);

   SpawnFastTravelGate("grove", "Heartwood Guardian Grove", arenaCenter + new Vector3(0f, 0f, -20f), false);

   var boss = OpenWorldEnemy.Spawn("Heartwood", "Heartwood the Ancient", arenaCenter, WorldRoot, true);
   guardian = boss;
   guardian.gameObject.SetActive(false);
   Vector3 sealPosition = GroveSealPosition;
   sealPosition.y = OpenWorldMaterialHelper.SampleGroundHeight(sealPosition);
   groveSeal = OpenWorldMaterialHelper.SpawnModel("Props/Expansion/TrailShrine", "Props/Expansion/Textures/TrailShrine_basecolor",
    WorldRoot, sealPosition, 2.4f, new Color(0.55f, 0.8f, 0.65f), true);
   Enemies.Add(boss);
  }

  void ScatterBiomeNature() {
   var rng = new System.Random(42026);
   string[] trees = {
    "Props/Village/rpgpp_lt_tree_01",
    "Props/Village/rpgpp_lt_tree_02",
    "Props/Village/rpgpp_lt_tree_pine_01",
    "Props/Nature/Tree_01",
    "Props/Nature/Tree_02",
    "Props/Nature/Tree_03",
    "Props/Nature/Tree_04",
    "Props/Nature/Tree_05"
   };
   string[] rocks = {
    "Props/Village/rpgpp_lt_rock_01",
    "Props/Village/rpgpp_lt_rock_02",
    "Props/Village/rpgpp_lt_bush_01",
    "Props/Nature/Rock_01",
    "Props/Nature/Rock_02",
    "Props/Nature/Rock_03",
    "Props/Nature/Bush_01",
    "Props/Nature/Bush_02",
    "Props/Nature/Stump_01"
   };

   string[] snowPines = {
    "Props/Snow/Pine_01",
    "Props/Snow/Pine_02",
    "Props/Snow/Pine_03"
   };

   for (int i = 0; i < 155; i++) {
    float x = (float)(rng.NextDouble() * 320.0 - 160.0);
    float z = (float)(rng.NextDouble() * 320.0 - 160.0);
    Vector3 p = new Vector3(x, 0f, z);
    if (!IsSpotClear(p, 4.2f) || OpenWorldTrails.NearTrail(p, 5f)) continue;
    p.y = OpenWorldMaterialHelper.SampleGroundHeight(p);
    bool isTree = i % 3 != 0;
    bool inSnowBelt = x < -68f && z > -24f && z < 48f;
    string asset = isTree
     ? (inSnowBelt ? snowPines[i % snowPines.Length] : trees[i % trees.Length])
     : rocks[i % rocks.Length];
    string tex = asset.StartsWith("Props/Snow/")
     ? "Props/Textures/Nature_snow"
     : (asset.StartsWith("Props/Village/") ? "Props/Textures/rpgpp_lt_tex_a" : "Props/Textures/Nature_basecolor");
    float h = isTree ? 5.4f + (i % 4) * 1.05f : 1.3f + (i % 3) * 0.4f;
    var go = OpenWorldMaterialHelper.SpawnModel(asset, tex, WorldRoot, p, h, Color.white, true);
    if (go) {
     go.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
     ReserveSpot(p, isTree ? 3.2f : 1.8f);
    }
   }

   // Scatter non-colliding wildflower patches, grass tufts & woodland mushrooms
   string[] groundFlora = {
    "Props/Nature/Flowers_01",
    "Props/Nature/Grass_01",
    "Props/Nature/Grass_02",
    "Props/Nature/Mushroom_01",
    "Props/Village/rpgpp_lt_flower_01",
    "Props/Village/rpgpp_lt_flower_02"
   };
   for (int i = 0; i < 115; i++) {
    float x = (float)(rng.NextDouble() * 260.0 - 130.0);
    float z = (float)(rng.NextDouble() * 260.0 - 130.0);
    Vector3 p = new Vector3(x, 0f, z);
    if (OpenWorldTrails.NearTrail(p, 2.3f)) continue;
    p.y = OpenWorldMaterialHelper.SampleGroundHeight(p);
    string asset = groundFlora[i % groundFlora.Length];
    string tex = asset.StartsWith("Props/Village/") ? "Props/Textures/rpgpp_lt_tex_a" : "Props/Textures/Nature_basecolor";
    float h = asset.Contains("Mushroom") ? 0.42f : 0.56f + (i % 3) * 0.12f;
    var fgo = OpenWorldMaterialHelper.SpawnModel(asset, tex, WorldRoot, p, h, Color.white, false);
    if (fgo) {
     fgo.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
    }
   }
  }

  void BuildForgottenSanctum() {
   Vector3 center = new Vector3(-60f, 0f, -58f);
   ReserveSpot(center, 18f);

   // Fast-travel gate outside the Forgotten Sanctum courtyard
   SpawnFastTravelGate("sanctum", "Forgotten Sanctum Gate", new Vector3(-60f, 0f, -43f), false);

   var arch = center + new Vector3(0f, 0f, 9f);
   arch.y = OpenWorldMaterialHelper.SampleGroundHeight(arch);
   OpenWorldMaterialHelper.SpawnModel("Props/Crypt/CryptArch", "Props/Crypt/Textures/CryptArch_basecolor", WorldRoot, arch, 4.5f, Color.white, true);

   var altar = SanctumAltarPosition;
   altar.y = OpenWorldMaterialHelper.SampleGroundHeight(altar);
   OpenWorldMaterialHelper.SpawnModel("Props/Crypt/CryptMemoryAltar", "Props/Crypt/Textures/CryptMemoryAltar_basecolor", WorldRoot, altar, 2.2f, Color.white, true);

   // 3 Elemental Focus Pillars around the sanctum courtyard
   Vector3[] focusPositions = {
    new Vector3(-67f, 0f, -54f),
    new Vector3(-53f, 0f, -54f),
    new Vector3(-60f, 0f, -67f)
   };
   for (int i = 0; i < 3; i++) {
    ReserveSpot(focusPositions[i], 3f);
    SanctumFoci.Add(OpenWorldSanctumFocus.Spawn(i, focusPositions[i], WorldRoot));
   }

   // 2 Sanctum Hazard Traps (Flame Brazier & Frost Totem) with clean spatial clearance
   Vector3 brazierPos = new Vector3(-64f, 0f, -51f);
   Vector3 totemPos = new Vector3(-56f, 0f, -51f);
   ReserveSpot(brazierPos, 3f);
   ReserveSpot(totemPos, 3f);
   SanctumTraps.Add(OpenWorldSanctumTrap.Spawn(false, brazierPos, WorldRoot));
   SanctumTraps.Add(OpenWorldSanctumTrap.Spawn(true, totemPos, WorldRoot));

   // 2 Sanctum Sentinels guarding the Memory Altar
   var acolyte = OpenWorldEnemy.Spawn("Caster", "Sanctum Acolyte", new Vector3(-65f, 0f, -58f), WorldRoot, false);
   var warden = OpenWorldEnemy.Spawn("Elite", "Sanctum Warden", new Vector3(-55f, 0f, -58f), WorldRoot, false);
   SanctumGuards.Add(acolyte);
   SanctumGuards.Add(warden);
   Enemies.Add(acolyte);
   Enemies.Add(warden);

   Vector3 cache = center + new Vector3(7f, 0f, -5f);
   ReserveSpot(cache, 3f);
   Collectibles.Add(OpenWorldCollectible.Spawn(OWCollectibleKind.SecretChest, cache, WorldRoot, 1, "FORGOTTEN SANCTUM CACHE", "chest:sanctum"));
  }

  void BuildLandmarksAndWaters() {
   // Waymarker shrines along the 5 main trails to guide exploration
   Vector3[] waymarkers = {
    new Vector3(-28f, 0f, 24f),
    new Vector3(29f, 0f, 20f),
    new Vector3(28f, 0f, -28f),
    new Vector3(-26f, 0f, -26f),
    new Vector3(5f, 0f, 52f)
   };
   foreach (var wm in waymarkers) {
    if (!IsSpotClear(wm, 3.2f)) continue;
    ReserveSpot(wm, 3.2f);
    Vector3 p = wm;
    p.y = OpenWorldMaterialHelper.SampleGroundHeight(p);
    OpenWorldMaterialHelper.SpawnModel("Props/Expansion/RitualAltar", "Props/Expansion/Textures/RitualAltar_basecolor", WorldRoot, p, 1.45f, Color.white, true);
   }

   // Two tranquil Verdant Sanctuary Ponds (Hub East Glade & Grove Approach)
   Vector3[] ponds = {
    new Vector3(26f, 0f, -18f),
    new Vector3(-22f, 0f, 84f)
   };
   foreach (var pondPos in ponds) {
    if (Realm) { ReserveSpot(pondPos, 9f); continue; }
    if (!IsSpotClear(pondPos, 9f)) continue;
    ReserveSpot(pondPos, 9f);
    float py = OpenWorldMaterialHelper.SampleGroundHeight(pondPos);
    var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    water.name = "SanctuaryPond";
    water.transform.SetParent(WorldRoot, false);
    water.transform.position = new Vector3(pondPos.x, py + 0.04f, pondPos.z);
    water.transform.localScale = new Vector3(11f, 0.03f, 9f);
    var col = water.GetComponent<Collider>();
    if (col) {
     if (Application.isPlaying) Destroy(col);
     else DestroyImmediate(col);
    }
    water.GetComponent<Renderer>().sharedMaterial =
     OpenWorldMaterialHelper.CreateLit("VerdantWater", new Color(0.14f, 0.56f, 0.68f), null, null, 0.92f, 0.15f, new Color(0.04f, 0.18f, 0.25f));
   }

   // 2 Zelda-style Gale Updraft Vents for vertical sky-gliding
   Vector3 hubUpdraftPos = new Vector3(20f, 0f, 16f);
   Vector3 skyUpdraftPos = new Vector3(-78f, 0f, 82f);
   ReserveSpot(hubUpdraftPos, 4f);
   ReserveSpot(skyUpdraftPos, 4f);
   Updrafts.Add(OpenWorldUpdraft.Spawn("SANCTUARY UPDRAFT", hubUpdraftPos, WorldRoot, 20.5f));
   // In the realm the sky-isle may sit higher than its updraft's ground; the launch makes up the difference.
   float skyBoost = Realm ? Mathf.Max(0f, SkyLift - TerrainY(skyUpdraftPos.x, skyUpdraftPos.z)) : 0f;
   Updrafts.Add(OpenWorldUpdraft.Spawn("CELESTIAL UPDRAFT", skyUpdraftPos, WorldRoot, Mathf.Sqrt(24.5f * 24.5f + 2f * 22f * skyBoost)));

   // 3 Ground-level Ancient Relic Shrines (4th spawns on Celestial Sky Island)
   Vector3[] groundRelics = {
    new Vector3(26f, 0f, -8f),
    new Vector3(-18f, 0f, 88f),
    new Vector3(-60f, 0f, -49f)
   };
   for (int i = 0; i < groundRelics.Length; i++) {
    ReserveSpot(groundRelics[i], 4.5f);
    RelicShrines.Add(OpenWorldRelicShrine.Spawn(i, groundRelics[i], WorldRoot));
   }

   // 3 Frontier Biome Rifts & Witcher Bounties
   for (int ri = 0; ri < 3; ri++) ReserveSpot(OpenWorldFrontierBuilder.RiftCenter(ri), 12f);
   OpenWorldFrontierBuilder.BuildFrontierRifts(this);

   // Warden's Contract Board in Hub Plaza
   Vector3 cbPos = ContractBoardPosition;
   cbPos.y = OpenWorldMaterialHelper.SampleGroundHeight(cbPos);
   ReserveSpot(cbPos, 3.5f);
   OpenWorldMaterialHelper.SpawnModel("Stormveil/MemoryPlaque", "Stormveil/Textures/MemoryPlaque_basecolor",
    WorldRoot, cbPos, 2.0f, Color.white, true);

   // Sunken Crypt of the First Wardens & Stormveil Relay Mechanism
   CryptDungeon = OpenWorldCryptDungeon.Build(this);
   StormRelay = OpenWorldStormRelay.Build(this);

   // 3 Wuthering Waves / Zelda Aether Grapple Anchors
   GrappleAnchors.Add(OpenWorldGrappleAnchor.Spawn("SANCTUM HIGH ARCH", AboveGround(new Vector3(-56f, 6.5f, -45f)), WorldRoot));
   GrappleAnchors.Add(OpenWorldGrappleAnchor.Spawn("CELESTIAL SKY-ISLAND", new Vector3(-80f, 19.8f + SkyLift, 88f), WorldRoot));
   GrappleAnchors.Add(OpenWorldGrappleAnchor.Spawn("HEARTWOOD CANOPY", AboveGround(new Vector3(10f, 5.8f, 102f)), WorldRoot));

   // 4 Settlements / Villages & 6 Living Interactive NPCs + Grand Northern Horizon Vista & Campfires
   OpenWorldVillageBuilder.BuildVillagesAndNPCs(this);
   OpenWorldVistaBuilder.BuildHubVistaAndCampfires(this);
  }

  // Terrain height only (ignores props and buildings), 0 before any terrain exists.
  float TerrainY(float x, float z) {
   var t = ActiveTerrain ? ActiveTerrain : Terrain.activeTerrain;
   return t ? t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y : 0f;
  }

  // Small-world heights were measured from flat ground at y=0; in the realm they ride on the land.
  Vector3 AboveGround(Vector3 p) => Realm ? new Vector3(p.x, TerrainY(p.x, p.z) + p.y, p.z) : p;

  // The Celestial Sky-Isle keeps its designed 18 m hover above its updraft, raised further when the
  // hill under its footprint would otherwise crowd its underside.
  float skyLift = float.NaN;
  float SkyLift {
   get {
    if (!float.IsNaN(skyLift)) return skyLift;
    if (!Realm) return skyLift = 0f;
    float under = TerrainY(-84f, 92f);
    for (int a = 0; a < 12; a++) {
     float ang = a * Mathf.PI / 6f;
     under = Mathf.Max(under, TerrainY(-84f + Mathf.Cos(ang) * 10f, 92f + Mathf.Sin(ang) * 10f));
    }
    return skyLift = Mathf.Max(TerrainY(-78f, 82f), under - 12f);
   }
  }

  void BuildSkyIslands() {
   Vector3[] skyPositions = {
    new Vector3(-84f, 18f, 92f),
    new Vector3(115f, 44f, 110f),
    new Vector3(-120f, 38f, -95f),
    new Vector3(120f, 42f, -105f)
   };
   // The realm keeps only the quest isle; the decorative ones hovered without purpose over its hills.
   int count = Realm ? 1 : skyPositions.Length;
   for (int i = 0; i < count; i++) {
    string mesh = i % 2 == 0 ? "Islands/Island_Meadow" : "Islands/Island_Plateau";
    string tex = i % 2 == 0 ? "Islands/Textures/Island_Meadow_basecolor" : "Islands/Textures/Island_Plateau_basecolor";
    OpenWorldMaterialHelper.SpawnModel(mesh, tex, WorldRoot, skyPositions[i] + Vector3.up * SkyLift, 8f, Color.white, true);
   }
   Physics.SyncTransforms();
   Vector3 skyCenter = new Vector3(-84f, 19.5f + SkyLift, 92f);
   float skyDeckY = OpenWorldMaterialHelper.SampleGroundHeight(skyCenter);
   if (Realm) DressSkyIsle(new Vector3(-84f, 18f + SkyLift, 92f), new Vector3(-78f, TerrainY(-78f, 82f), 82f));
   RelicShrines.Add(OpenWorldRelicShrine.Spawn(3, new Vector3(-84f, skyDeckY, 92f), WorldRoot));
   OpenWorldMaterialHelper.SpawnModel("Astral/Arch", "Astral/Textures/Arch_basecolor",
    WorldRoot, new Vector3(-84f, skyDeckY, 95.2f), 4.2f, Color.white, false);
   OpenWorldMaterialHelper.SpawnModel("Astral/WardFocus", "Astral/Textures/WardFocus_basecolor",
    WorldRoot, new Vector3(-87.2f, skyDeckY, 92f), 2.2f, Color.white, true);
   OpenWorldMaterialHelper.SpawnModel("Astral/Record", "Astral/Textures/Record_basecolor",
    WorldRoot, new Vector3(-80.8f, skyDeckY, 92f), 2.1f, Color.white, true);
  }

  // Makes the isle read as held aloft by ley-light: a rune circle and light pillar over its updraft,
  // godrays and drifting motes beneath it, leaves shedding from its meadow.
  void DressSkyIsle(Vector3 isle, Vector3 updraftBase) {
   var root = new GameObject("CelestialSkyIsle_Aether").transform;
   root.SetParent(WorldRoot, false);
   root.position = isle;
   var aether = new Color(0.45f, 1f, 0.85f);
   var vent = new GameObject("AetherVent").transform;
   vent.SetParent(root, false);
   vent.position = updraftBase;
   OpenWorldVfx.Attach("circle_magic", vent, Vector3.up * 0.06f, 2.2f, aether);
   OpenWorldVfx.Attach("loot_beam", vent, Vector3.zero, 3.2f, aether);
   OpenWorldVfx.Attach("godrays", root, Vector3.down * 2f, 2.4f, new Color(0.85f, 1f, 0.95f));
   OpenWorldVfx.Attach("motes", root, Vector3.down * 4f, 3f, aether);
   OpenWorldVfx.Attach("leaves_green", root, Vector3.up * 1.5f, 1.6f, null);
  }

  void Update() {
   TickAdventure();
   TickVerdantHunt();
   TickGlmQuests();
   if (Input.GetKeyDown(KeyCode.Escape)) {
    if (IsModalOpen) CloseMenus();
    else ShowPauseModal = true;
   }
   if (Input.GetKeyDown(KeyCode.M)) {
    ShowPauseModal = false;
    ShowMapModal = !ShowMapModal;
    ShowVaultModal = ShowArsenalModal = ShowWardrobeModal = false;
   }
   if (Input.GetKeyDown(KeyCode.V)) {
    ShowPauseModal = false;
    ShowVaultModal = !ShowVaultModal;
    ShowMapModal = ShowArsenalModal = ShowWardrobeModal = false;
   }
   if (Input.GetKeyDown(KeyCode.I)) {
    ShowPauseModal = false;
    ShowArsenalModal = !ShowArsenalModal;
    ShowMapModal = ShowVaultModal = ShowWardrobeModal = false;
   }
   if (Input.GetKeyDown(KeyCode.U)) {
    ShowPauseModal = false;
    ShowWardrobeModal = !ShowWardrobeModal;
    ShowMapModal = ShowVaultModal = ShowArsenalModal = false;
   }
   if (Input.GetKeyDown(KeyCode.T)) {
    bool open = !ShowClockModal;
    CloseMenus();
    ShowClockModal = open;
   }

   SyncPause();
   if (Hero && !Hero.Dead && !IsModalOpen) {
    foreach (var gate in Gates) {
     if (!gate || gate.Discovered) continue;
     if (Vector3.Distance(Hero.transform.position, gate.transform.position) < 9f) {
      gate.Discover();
      MarkProgressDirty();
      ShowBanner("FAST-TRAVEL GATE DISCOVERED: " + gate.WaypointTitle.ToUpperInvariant());
     }
    }
   }
  }

  public void FastTravelTo(TeleportGateController gate) {
   if (!gate || !Hero || Hero.Dead || !gate.Discovered) return;
   Vector3 dest = (gate.ArrivalPoint && Vector3.Distance(gate.ArrivalPoint.position, gate.transform.position) >= 1.8f)
    ? gate.ArrivalPoint.position
    : gate.transform.position - gate.transform.forward * 3.5f;
   Hero.WarpTo(dest);
   respawnPoint = Hero.transform.position;
   SetCheckpoint(gate.WaypointId);
   MarkProgressDirty();
   CameraRig?.SnapToTarget();
   ShowMapModal = false;
   ShowBanner("Fast Traveled to " + gate.WaypointTitle);
   OpenWorldAudio.Play("sfx_checkpoint");
   SpawnBurst(Hero.transform.position + Vector3.up * 0.9f, new Color(0.35f, 0.95f, 0.75f), 18);
  }

  public void AddGold(int amount) { Gold += amount; MarkProgressDirty(); }
  public void AddGems(int amount) { Gems += amount; MarkProgressDirty(); }

  public void CollectKey(string keyTitle) {
   KeysCollected = Mathf.Min(TotalKeys, KeysCollected + 1);
   ShowBanner($"{keyTitle} RECOVERED! ({KeysCollected}/{TotalKeys} Keys)");
   OpenWorldAudio.Play("sfx_weapon_pickup");
   MarkProgressDirty();
  }

  public void CollectWorldChest(string title) {
   ChestsOpened++;
   Gold += 45;
   Gems += 2;
   ShowBanner($"{title} OPENED! +45 Gold, +2 Gems");
   OpenWorldAudio.Play("sfx_coin");
   MarkProgressDirty();
  }

  public void OnCampCaptured(OpenWorldOutpostCamp camp) {
   if (!CapturedCampIds.Add(camp.PersistentId)) return;
   CampsCaptured++;
   Gold += 60;
   Gems += 3;
   OpenWorldAudio.Play("sfx_complete");
   ShowBanner($"{camp.CampTitle.ToUpperInvariant()} CAPTURED! Banner turned Green (+60 Gold, +3 Gems)");
   MarkProgressDirty();
  }

  public void OnEnemyDefeated(OpenWorldEnemy enemy) {
   RecordVerdantQuarry(enemy);
   RecordGlmDefeat(enemy);
   if (enemy.IsBoss && GuardianAwakened && !GuardianDefeated) {
    GuardianDefeated = true;
    Gold += 200;
    Gems += 10;
    ShowBanner("VICTORY! HEARTWOOD THE ANCIENT DEFEATED! (+200 Gold, +10 Gems)");
    MarkProgressDirty();
   } else if (enemy.IsBountyElite && enemy.BountyIndex >= 0 && (BountiesMask & (1 << enemy.BountyIndex)) == 0) {
    BountiesMask |= (1 << enemy.BountyIndex);
    Gold += 85;
    Gems += 3;
    OpenWorldAudio.Play("sfx_complete");
    ShowBanner($"BOUNTY COMPLETED: {enemy.DisplayName.ToUpperInvariant()} SLAIN! (+85 Gold, +3 Gems • {BountiesCount}/3 Bounties)");
    MarkProgressDirty();
   }
  }

  public void OnHeroDefeated() {
   OpenWorldAudio.Play("sfx_defeat");
   ShowBanner("Aster fell in battle — reviving at your last sanctuary or Realm Gate...");
   Invoke(nameof(ReviveHero), 2.0f);
  }

  void ReviveHero() {
   if (Hero) {
    Hero.ReviveAt(respawnPoint);
    CameraRig?.SnapToTarget();
   }
  }

  public void OpenGachaChest(int tier, bool viaAd = false) {
   if (tier < 0 || tier > 2) return;
   // Rewarded ads need a verified SDK completion callback before release.
   if (viaAd && !Application.isEditor && !Debug.isDebugBuild) return;
   int goldCost = tier == 0 ? 50 : tier == 1 ? 140 : 0;
   int gemCost = tier == 2 ? 8 : 0;

   if (!viaAd) {
    if (Gold < goldCost || Gems < gemCost) {
     lastChestResult = tier == 2 ? "Need 8 Gems for Epic Chest!" : $"Need {goldCost} Gold!";
     return;
    }
    Gold -= goldCost;
    Gems -= gemCost;
   }
   MarkProgressDirty();

   float roll = Random.value;
   if (tier == 2 && roll < 0.38f) {
    int skin = Random.Range(1, OpenWorldCharacterVisual.AsterSkins.Length);
    UnlockedSkins.Add(skin);
    Hero?.EquipSkin(skin);
    lastChestResult = "EPIC SKIN UNLOCKED: " + OpenWorldCharacterVisual.AsterSkinTitles[skin] + "!";
    return;
   }
   if ((tier == 2 && roll < 0.78f) || (tier == 1 && roll < 0.45f) || (tier == 0 && roll < 0.25f)) {
    var epicPool = OpenWorldWeaponCatalog.EpicVaultWeaponIds;
    int wid = tier == 2 ? epicPool[Random.Range(0, epicPool.Length)] : Random.Range(tier == 1 ? 3 : 0, tier == 1 ? 9 : 6);
    UnlockedWeapons.Add(wid);
    Hero?.EquipWeapon(wid);
    var w = OpenWorldWeaponCatalog.Get(wid);
    lastChestResult = $"{w.Rarity.ToString().ToUpperInvariant()} WEAPON UNLOCKED: {w.Name}!";
    return;
   }
   int bonusGold = tier == 2 ? 120 : tier == 1 ? 75 : 35;
   int bonusGems = tier == 2 ? 4 : tier == 1 ? 2 : 1;
   Gold += bonusGold;
   Gems += bonusGems;
   lastChestResult = $"CHEST REWARD: +{bonusGold} Gold & +{bonusGems} Gems!";
  }

  public void ShowBanner(string text) {
   bannerText = text;
   bannerUntil = Time.time + Mathf.Clamp(text.Length / 14f, 4.2f, 10f);
   OpenWorldQuestJournal.Instance?.ObserveBanner(text);
  }

  Texture2D burstGlowTex;
  Texture2D GetBurstGlowTexture() {
   if (burstGlowTex) return burstGlowTex;
   burstGlowTex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "OW_BurstGlow", wrapMode = TextureWrapMode.Clamp };
   var pixels = new Color[32 * 32];
   for (int y = 0; y < 32; y++) {
    for (int x = 0; x < 32; x++) {
     float dx = (x - 15.5f) / 15.5f;
     float dy = (y - 15.5f) / 15.5f;
     float r = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
     float a = r * r;
     pixels[y * 32 + x] = new Color(1f, 1f, 1f, a);
    }
   }
   burstGlowTex.SetPixels(pixels);
   burstGlowTex.Apply(false, true);
   return burstGlowTex;
  }

  public void SpawnBurst(Vector3 pos, Color color, int count = 12) {
   if (OpenWorldVfx.Burst(pos, color, count)) return;
   string effect = count <= 3 ? "fx_dust" : color.r > color.g * 1.35f ? "impact_ember" : color.b > color.r * 1.25f ? "impact_frost" : color.g > color.r * 1.2f ? "impact_gale" : "fx_sparks";
   if (OpenWorldImportedContent.Emit(effect, pos, Quaternion.identity, count <= 3 ? .4f : Mathf.Clamp(count / 24f, .5f, 1.4f))) return;
   var go = new GameObject("OW_Burst");
   go.transform.position = pos;
   var ps = go.AddComponent<ParticleSystem>();
   var main = ps.main;
   main.startLifetime = 0.55f;
   main.startSpeed = 3.2f;
   main.startSize = 0.24f;
   main.startColor = color;
   main.loop = false;
   main.playOnAwake = false;
   var emission = ps.emission;
   emission.enabled = false;
   var shape = ps.shape;
   shape.shapeType = ParticleSystemShapeType.Sphere;
   shape.radius = 0.25f;
   var psr = go.GetComponent<ParticleSystemRenderer>();
   if (!burstMaterial) {
    burstMaterial = new Material(Shader.Find("Sprites/Default")) {
     mainTexture = GetBurstGlowTexture()
    };
   }
   psr.sharedMaterial = burstMaterial;
   ps.Play();
   ps.Emit(count);
   if (Application.isPlaying) Destroy(go, 1.0f);
   else DestroyImmediate(go);
  }

  void OnDestroy() {
   if (I != this) return;
   CloseMenus();
   I = null;
   if (burstMaterial) Destroy(burstMaterial);
   if (burstGlowTex) Destroy(burstGlowTex);
   if (OpenWorldMobileHud.Instance) Destroy(OpenWorldMobileHud.Instance.gameObject);
  }
 }
}

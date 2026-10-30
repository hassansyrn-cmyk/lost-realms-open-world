using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LostRealmsOW {
 // Living population of the 1 km realm. Creatures reuse the prepared, animated imported-content prefabs
 // (Addressables) and stream in around the hero, nearest groups first; spawn points are deterministic so
 // the world is the same on every visit, while hunted herds come back the next time they stream in.
 //  - Verdant heartlands: deer herds and wild horse herds on the open meadows, foxes, hares and five
 //    kinds of songbird; timber wolf packs and bears deep in the woods, far from any village.
 //  - Villages (every biome): cats, hounds, shepherd dogs, hens and saddle horses on the pastures.
 //  - Frozen Peaks: snow elk, snow hares, penguin colonies on the lake shores, chickadees, frostfang
 //    wolf packs and frost bears.
 //  - Burning Dunes: rhinos, horned ravagers and sand tigers away from the oasis towns, dune stalkers.
 //  - Hollow Marches: crow flocks, revenants and hexers. Stormveil: ironhide rhinos, thunder wolves,
 //    stormweb spiders and orc vanguards.
 // Predators are territorial (OpenWorldEnemy.Territorial): they warn Aster off, attack when she comes
 // close or hits them and give up when she leaves. Danger rises with distance from the heartland.
 public sealed class OpenWorldPopulation : MonoBehaviour {
  public static OpenWorldPopulation Instance { get; private set; }

  enum Kind { Wildlife, Bird, Enemy, Villager }

  sealed class Species {
   public string source, role, display; public Kind kind; public float height; public OWBiome biome; public int groups, minSize, maxSize;
   public float minVillage, maxVillage, minSlopeFree = 24f, damage = 1f, health = 1f;
   public string address; public AsyncOperationHandle<GameObject> handle; public bool requested; public float lastUse, retryAt;
   public GameObject direct; // baked prefab shipped in Resources (villager palettes); no Addressables load
   // Behaviour
   public OpenWorldWildlife.Temper temper = OpenWorldWildlife.Temper.Skittish;
   public float walk = 1.1f, run = 5f, aware = 9f, flee = 26f, homeRadius = 9f, turn = 170f, spacing = 60f, sizeJitter = 0.07f, range = 55f;
   public bool friendly, zigzag, anyBiome, nearWater, openGround, forest, territorial, beast;
   public float aggro = 9f, warn = 15f, chase = 5.5f, leash = 34f;
   public int meat;
   public string special = "";
   public Color[] coats;
   public string coat; // alternative albedo under Resources/World/Coats (ClaudeAnimalPrep makes it)
  }

  sealed class Group {
   public Species species; public Vector3 center; public int size; public float radius;
   public readonly List<GameObject> live = new List<GameObject>();
   public bool active; public float clearedUntil;
   public OpenWorldHerd herd;
   public int generation, spawnCursor; public bool spawning;
  }

  readonly List<Species> species = new List<Species>();
  readonly List<Group> groups = new List<Group>();
  readonly List<(float d, Group g)> candidates = new List<(float, Group)>();
  Group pendingGroup;
  float nextUpdate;
  int activeEnemies;
  int enemyBudget = 14, creatureBudget = 36;
  System.Random rng;
  int session; // set in Initialise (Unity's Random cannot run in a field initializer)
  float[,] treeGrid;
  const float TreeCell = 32f;

  static readonly (Vector2 c, float r, int count)[] Settlements = {
   (Vector2.zero, 16f, 4), (new Vector2(44f, 42f), 12f, 3), (new Vector2(-96f, -104f), 12f, 3), (new Vector2(84f, -8f), 10f, 3),
   (new Vector2(-150f, 262f), 12f, 3), (new Vector2(318f, -48f), 16f, 5), (new Vector2(292f, 150f), 12f, 3)
  };

  static readonly Color[] HorseCoats = {
   Color.white, new Color(1.06f, 0.8f, 0.66f), new Color(0.45f, 0.42f, 0.44f), new Color(0.86f, 0.86f, 0.9f), new Color(1.14f, 1.02f, 0.78f), new Color(0.72f, 0.56f, 0.46f)
  };
  static readonly Color[] DeerCoats = { Color.white, new Color(1.05f, 0.92f, 0.82f), new Color(0.88f, 0.8f, 0.74f) };
  static readonly Color[] ElkCoats = { new Color(1.12f, 1.08f, 1.02f), new Color(0.98f, 0.94f, 0.9f) };
  static readonly Color[] SnowCoats = { new Color(1.35f, 1.35f, 1.38f) };

  public static OpenWorldPopulation Attach(Transform parent) {
   if (Instance || !OpenWorldRealm.Instance) return Instance;
   var go = new GameObject("Realm Population");
   go.transform.SetParent(parent, false);
   var p = go.AddComponent<OpenWorldPopulation>();
   p.Initialise();
   return p;
  }

  const string DeerSrc = "Assets/ithappy/Animals_FREE/Prefabs/Deer_001.prefab", HorseSrc = "Assets/ithappy/Animals_FREE/Prefabs/Horse_001.prefab",
   DogSrc = "Assets/ithappy/Animals_FREE/Prefabs/Dog_001.prefab", HenSrc = "Assets/ithappy/Animals_FREE/Prefabs/Chicken_001.prefab",
   CatSrc = "Assets/ithappy/Animals_FREE/Prefabs/Kitty_001.prefab", TigerSrc = "Assets/ithappy/Animals_FREE/Prefabs/Tiger_001.prefab",
   PenguinSrc = "Assets/ithappy/Animals_FREE/Prefabs/Pinguin_001.prefab", FoxSrc = "Assets/Fox/Prefabs/Fox.prefab",
   HareSrc = "Assets/Rabbits/Prefabs/Rabbit 1.prefab", ShepherdSrc = "Assets/RSG_DogsPack/HDRP/Prefabs/P_GermanShepherd.prefab",
   WolfSrc = "Assets/Wolf_Animated/Prefabs/Wolf.prefab", BearSrc = "Assets/Blink/Art/Animals/Stylized/Bear/Bear_Prefabs/Bear_4.prefab",
   RhinoSrc = "Assets/Creature_Rhinoceros/Prefab/Rhinoceros.prefab", IronhideSrc = "Assets/Rhino/Prefab/Rhino_PBR.prefab",
   Birds = "Assets/living birds/resources/";

  void Initialise() {
   Instance = this;
   rng = new System.Random(20261022);
   session = UnityEngine.Random.Range(1000, 9999);
   bool high = !OpenWorldGraphics.Instance || OpenWorldGraphics.Instance.High;
   enemyBudget = high ? 18 : 10;
   creatureBudget = high ? 54 : 32;
   var data = Resources.Load<TextAsset>("World/ImportedContent");
   var addresses = new Dictionary<string, string>();
   if (data) foreach (var e in JsonUtility.FromJson<ImportedContentManifest>(data.text).entries) if (!string.IsNullOrEmpty(e.source) && !addresses.ContainsKey(e.source)) addresses[e.source] = e.address;
   Species Add(string source, Kind kind, string role, string display, float height, OWBiome biome, int groupCount, int min, int max, float minVillage = 45f, float maxVillage = 9999f, float health = 1f, float damage = 1f) {
    if (!addresses.TryGetValue(source, out var address)) return null;
    var s = new Species { source = source, address = address, kind = kind, role = role, display = display, height = height, biome = biome, groups = groupCount, minSize = min, maxSize = max, minVillage = minVillage, maxVillage = maxVillage, health = health, damage = damage };
    species.Add(s);
    return s;
   }
   void Prey(Species s, float walk, float run, float aware, float flee, int meat = 0, bool zig = false) {
    if (s == null) return;
    s.temper = OpenWorldWildlife.Temper.Skittish; s.walk = walk; s.run = run; s.aware = aware; s.flee = flee; s.meat = meat; s.zigzag = zig;
   }
   void Wary(Species s, float walk, float run, float aware, float flee) {
    if (s == null) return;
    s.temper = OpenWorldWildlife.Temper.Wary; s.walk = walk; s.run = run; s.aware = aware; s.flee = flee;
   }
   void Tame(Species s, float walk, float run, bool friendly, float aware = 0f) {
    if (s == null) return;
    s.temper = OpenWorldWildlife.Temper.Tame; s.walk = walk; s.run = run; s.friendly = friendly; s.aware = aware; s.flee = 8f;
    s.anyBiome = true; s.spacing = 14f; s.homeRadius = 7f; s.range = 22f;
   }
   void Predator(Species s, float aggro, float warn, float chase, string special, int meat, float leash = 34f) {
    if (s == null) return;
    s.territorial = true; s.beast = true; s.aggro = aggro; s.warn = warn; s.chase = chase; s.special = special; s.meat = meat; s.leash = leash;
   }
   void Flock(Species s) { if (s != null) { s.homeRadius = 6f; s.range = 30f; s.spacing = 45f; } }

   // Verdant heartlands: herds on the meadows, small game, songbirds.
   var deer = Add(DeerSrc, Kind.Wildlife, "", "Meadow Deer", 1.45f, OWBiome.Verdant, 12, 3, 5, 65f);
   // Deer and elk outrun Aster's sprint (7.7 m/s) in a burst and then lope: on foot she needs a spell or a
   // careful stalk; a galloping horse (9 m/s) closes on a herd once it tires. Hares bound and zig-zag.
   Prey(deer, 1.15f, 10.5f, 10f, 40f, 2); if (deer != null) deer.coats = DeerCoats;
   // Horse sizes put the seat about 1.4 m up (a 15-hand riding horse); the prefab's bounds stop short of
   // its raised head, so the specified height is a little under the visible one.
   var wildHorse = Add(HorseSrc, Kind.Wildlife, "", "Wild Horse", 1.78f, OWBiome.Verdant, 5, 3, 5, 75f);
   Prey(wildHorse, 1.4f, 9.8f, 7f, 36f); if (wildHorse != null) { wildHorse.openGround = true; wildHorse.coats = HorseCoats; wildHorse.homeRadius = 14f; wildHorse.range = 70f; }
   // Small animals a little larger than life too, so they still read above the meadow grass.
   Wary(Add(FoxSrc, Kind.Wildlife, "", "Red Fox", 0.68f, OWBiome.Verdant, 10, 1, 2, 50f), 1.1f, 6.5f, 6f, 18f);
   Prey(Add(HareSrc, Kind.Wildlife, "", "Hare", 0.4f, OWBiome.Verdant, 14, 1, 3, 30f), 0.9f, 5.5f, 6f, 18f, 1, true);
   // Songbirds a little larger than life (as in Genshin or Zelda) so a flock on the path reads at a glance.
   Flock(Add(Birds + "lb_robin.prefab", Kind.Bird, "", "Robin", 0.27f, OWBiome.Verdant, 8, 2, 3, 20f));
   Flock(Add(Birds + "lb_sparrow.prefab", Kind.Bird, "", "Sparrow", 0.24f, OWBiome.Verdant, 8, 2, 4, 0f));
   Flock(Add(Birds + "lb_blueJay.prefab", Kind.Bird, "", "Blue Jay", 0.3f, OWBiome.Verdant, 6, 2, 3, 30f));
   Flock(Add(Birds + "lb_cardinal.prefab", Kind.Bird, "", "Cardinal", 0.28f, OWBiome.Verdant, 6, 1, 3, 30f));
   Flock(Add(Birds + "lb_goldFinch.prefab", Kind.Bird, "", "Goldfinch", 0.2f, OWBiome.Verdant, 6, 2, 4, 25f));
   Flock(Add(Birds + "lb_crow.prefab", Kind.Bird, "", "Crow", 0.38f, OWBiome.Verdant, 4, 2, 3, 40f));
   // Villages in every biome: pets, hens, saddle horses on the pastures.
   Wary(Add(HenSrc, Kind.Wildlife, "", "Hen", 0.5f, OWBiome.Verdant, 7, 3, 5, 8f, 30f), 0.7f, 3.2f, 3.5f, 6f);
   foreach (var s in species) if (s.display == "Hen") { s.anyBiome = true; s.spacing = 14f; s.range = 18f; s.homeRadius = 5f; }
   var horse = Add(HorseSrc, Kind.Wildlife, "", "Horse", 1.85f, OWBiome.Verdant, 6, 1, 2, 16f, 45f);
   Tame(horse, 1.3f, 8f, false); if (horse != null) { horse.coats = HorseCoats; horse.spacing = 20f; }
   Tame(Add(DogSrc, Kind.Wildlife, "", "Village Hound", 0.85f, OWBiome.Verdant, 5, 1, 1, 4f, 24f), 1.2f, 5.5f, true);
   Tame(Add(ShepherdSrc, Kind.Wildlife, "", "Shepherd Dog", 0.9f, OWBiome.Verdant, 5, 1, 1, 6f, 28f), 1.25f, 6f, true);
   Tame(Add(CatSrc, Kind.Wildlife, "", "Village Cat", 0.42f, OWBiome.Verdant, 8, 1, 2, 2f, 20f), 0.6f, 4.5f, false, 2.5f);
   // Deep woods of the heartland: territorial wolves and bears, well away from villages and the start.
   var timber = Add(WolfSrc, Kind.Enemy, "Goblin", "Timber Wolf", 1.05f, OWBiome.Verdant, 4, 2, 3, 125f, 9999f, 1.4f, 1.15f);
   Predator(timber, 9f, 16f, 6.3f, "SAVAGE LUNGE", 1); if (timber != null) timber.forest = true;
   var bear = Add(BearSrc, Kind.Enemy, "StoneBrute", "Forest Bear", 1.5f, OWBiome.Verdant, 4, 1, 1, 135f, 9999f, 2.6f, 1.6f);
   Predator(bear, 8f, 15f, 5.4f, "MAULING SWIPE", 3); if (bear != null) bear.forest = true;
   // Frozen Peaks
   var frostfang = Add(WolfSrc, Kind.Enemy, "Goblin", "Frostfang Wolf", 1.15f, OWBiome.Frozen, 7, 2, 3, 55f, 9999f, 1.6f, 1.3f);
   Predator(frostfang, 11f, 18f, 6.5f, "SAVAGE LUNGE", 1);
   var elk = Add(DeerSrc, Kind.Wildlife, "", "Snow Elk", 1.55f, OWBiome.Frozen, 6, 2, 4, 60f);
   Prey(elk, 1.1f, 9.5f, 10f, 38f, 2); if (elk != null) elk.coats = ElkCoats;
   var snowHare = Add(HareSrc, Kind.Wildlife, "", "Snow Hare", 0.42f, OWBiome.Frozen, 6, 1, 2, 40f);
   Prey(snowHare, 0.9f, 5.5f, 6f, 18f, 1, true); if (snowHare != null) snowHare.coats = SnowCoats;
   var frostBear = Add(BearSrc, Kind.Enemy, "StoneBrute", "Frost Bear", 1.75f, OWBiome.Frozen, 3, 1, 1, 110f, 9999f, 3.2f, 1.9f);
   Predator(frostBear, 8f, 15f, 5.6f, "MAULING SWIPE", 3); if (frostBear != null) frostBear.coat = "FrostBear";
   var penguin = Add(PenguinSrc, Kind.Wildlife, "", "Penguin", 0.8f, OWBiome.Frozen, 4, 3, 6, 60f);
   Wary(penguin, 0.55f, 1.6f, 3f, 6f); if (penguin != null) { penguin.nearWater = true; penguin.spacing = 30f; penguin.homeRadius = 6f; penguin.range = 20f; }
   Flock(Add(Birds + "lb_chickadee.prefab", Kind.Bird, "", "Chickadee", 0.2f, OWBiome.Frozen, 6, 2, 4, 0f));
   // Burning Dunes
   Add("Assets/fantasySpider/spider_myOldOne.FBX", Kind.Enemy, "Goblin", "Dune Stalker", 1.1f, OWBiome.Dunes, 6, 1, 3, 60f, 9999f, 1.5f, 1.25f);
   // A horned brute on two legs (the Creature_Rhinoceros pack), not a rhino on four: a desert monster.
   var ravager = Add(RhinoSrc, Kind.Enemy, "StoneBrute", "Horned Ravager", 2.6f, OWBiome.Dunes, 4, 1, 2, 80f, 9999f, 2.8f, 1.8f);
   Predator(ravager, 9f, 16f, 6f, "GORING CHARGE", 0);
   var tiger = Add(TigerSrc, Kind.Enemy, "Elite", "Sand Tiger", 1.15f, OWBiome.Dunes, 3, 1, 1, 90f, 9999f, 1.1f, 1.4f);
   Predator(tiger, 10f, 14f, 6.8f, "POUNCE", 2);
   // Hollow Marches
   Add("Assets/Skeleton/Prefabs/Skeleton.prefab", Kind.Enemy, "Goblin", "Hollow Revenant", 1.8f, OWBiome.Graveyard, 7, 2, 3, 26f, 9999f, 1.7f, 1.35f);
   Flock(Add(Birds + "lb_crow.prefab", Kind.Bird, "", "Crow", 0.38f, OWBiome.Graveyard, 8, 3, 5, 0f));
   // Stormveil Cliffs
   Add("Assets/fantasySpider/spider_myOldOne.FBX", Kind.Enemy, "Goblin", "Stormweb Spider", 1.25f, OWBiome.Storm, 4, 1, 2, 40f, 9999f, 1.9f, 1.45f);
   var thunder = Add(WolfSrc, Kind.Enemy, "Goblin", "Thunder Wolf", 1.2f, OWBiome.Storm, 3, 2, 3, 40f, 9999f, 1.9f, 1.45f);
   Predator(thunder, 12f, 18f, 6.6f, "SAVAGE LUNGE", 1);
   var ironhide = Add(IronhideSrc, Kind.Enemy, "StoneBrute", "Ironhide Rhino", 1.9f, OWBiome.Storm, 3, 1, 2, 70f, 9999f, 3f, 1.9f);
   Predator(ironhide, 8f, 15f, 6f, "GORING CHARGE", 3);
   // Expansion packs enter only if the preparation gate accepted their rigs and clips.
   Add("Assets/Orcs Pack/Prefabs/Orc Warrior with Sword and Shield.prefab", Kind.Enemy, "StoneBrute", "Orc Vanguard", 2.2f, OWBiome.Storm, 3, 1, 2, 70f, 9999f, 1.2f, 1.2f);
   Add("Assets/Orcs Pack/Prefabs/Orc Wizard with Wand.prefab", Kind.Enemy, "Elite", "Hollow Marches Hexer", 2f, OWBiome.Graveyard, 3, 1, 1, 70f, 9999f, 1.1f, 1.2f);
   // Villagers
   var villagers = new List<string>();
   foreach (var src in addresses.Keys) if (src.Contains("Polytope Studio") && src.Contains("Peasant")) villagers.Add(src);
   PlanWild();
   PlanVillagers(villagers, addresses);
  }

  float VillageDistance(Vector2 p) {
   float best = 9999f;
   foreach (var s in Settlements) best = Mathf.Min(best, Vector2.Distance(p, s.c));
   return best;
  }

  bool GoodGround(Vector2 p, float slopeLimit) {
   if (Mathf.Abs(p.x) > OpenWorldRegions.PlayableHalf - 12f || Mathf.Abs(p.y) > OpenWorldRegions.PlayableHalf - 12f) return false;
   if (OpenWorldRegions.EdgeMetric(p.x, p.y) > OpenWorldRegions.PlayableHalf - 14f) return false;
   if (OpenWorldTerrainSculptor.WaterAt(p.x, p.y, out _, out _) || OpenWorldTerrainSculptor.DistanceToWater(p.x, p.y) < 3f) return false;
   if (OpenWorldTerrainSculptor.RoadDistance(p.x, p.y) < 2.5f) return false;
   var n = OpenWorldTerrainSculptor.NormalAt(p.x, p.y);
   return Vector3.Angle(n, Vector3.up) <= slopeLimit;
  }

  // Trees per 32 m cell around a point (3x3 cell average), from the realm's terrain trees.
  float TreeDensity(Vector2 p) {
   if (treeGrid == null) {
    var t = Terrain.activeTerrain;
    if (!t || !t.terrainData) { treeGrid = new float[1, 1]; return 0f; }
    var td = t.terrainData; var size = td.size; var origin = t.transform.position;
    int n = Mathf.Max(1, Mathf.CeilToInt(size.x / TreeCell));
    treeGrid = new float[n, n];
    foreach (var ti in td.treeInstances) {
     int ix = Mathf.Clamp((int)(ti.position.x * size.x / TreeCell), 0, n - 1);
     int iz = Mathf.Clamp((int)(ti.position.z * size.z / TreeCell), 0, n - 1);
     treeGrid[ix, iz] += 1f;
    }
   }
   int len = treeGrid.GetLength(0);
   if (len <= 1) return 0f;
   var terrain = Terrain.activeTerrain;
   Vector3 o = terrain ? terrain.transform.position : new Vector3(-OpenWorldRegions.WorldHalf, 0f, -OpenWorldRegions.WorldHalf);
   int cx = (int)((p.x - o.x) / TreeCell), cz = (int)((p.y - o.z) / TreeCell);
   float sum = 0f; int count = 0;
   for (int x = cx - 1; x <= cx + 1; x++) for (int z = cz - 1; z <= cz + 1; z++) {
    if (x < 0 || z < 0 || x >= len || z >= len) continue;
    sum += treeGrid[x, z]; count++;
   }
   return count > 0 ? sum / count : 0f;
  }

  void PlanWild() {
   var w = new float[5];
   foreach (var sp in species) {
    int placed = 0;
    bool village = sp.maxVillage < 90f;
    for (int attempt = 0; attempt < 5000 && placed < sp.groups; attempt++) {
     bool strict = attempt < 3800; // habitat preferences first, then any suitable ground
     Vector2 p;
     if (village) {
      var s = Settlements[rng.Next(Settlements.Length)];
      float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Lerp(sp.minVillage, sp.maxVillage, (float)rng.NextDouble());
      p = s.c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
     } else p = new Vector2(((float)rng.NextDouble() * 2f - 1f) * OpenWorldRegions.PlayableHalf, ((float)rng.NextDouble() * 2f - 1f) * OpenWorldRegions.PlayableHalf);
     if (!sp.anyBiome) {
      OpenWorldRegions.BiomeWeights(p.x, p.y, w);
      if (w[(int)sp.biome] < 0.7f) continue;
     }
     float vd = VillageDistance(p);
     if (vd < sp.minVillage || vd > sp.maxVillage) continue;
     if (!GoodGround(p, sp.openGround && strict ? 12f : sp.minSlopeFree)) continue;
     if (sp.nearWater && OpenWorldTerrainSculptor.DistanceToWater(p.x, p.y) > (strict ? 16f : 40f)) continue;
     if (strict && sp.forest && TreeDensity(p) < 5f) continue;
     if (strict && sp.openGround && TreeDensity(p) > 2.5f) continue;
     // Songbird flocks feed along paths and clearings, where they can be seen above the grass.
     if (strict && sp.kind == Kind.Bird && OpenWorldTerrainSculptor.RoadDistance(p.x, p.y) > 16f) continue;
     bool crowded = false;
     foreach (var g in groups) if (g.species == sp && Vector3.Distance(g.center, new Vector3(p.x, 0f, p.y)) < sp.spacing) { crowded = true; break; }
     if (crowded) continue;
     var group = new Group { species = sp, center = new Vector3(p.x, 0f, p.y), size = sp.minSize + rng.Next(sp.maxSize - sp.minSize + 1), radius = sp.kind == Kind.Enemy ? 6f : sp.kind == Kind.Bird ? 4f : 10f };
     group.herd = new OpenWorldHerd { Origin = group.center, Range = sp.range };
     groups.Add(group);
     placed++;
    }
   }
  }

  void PlanVillagers(List<string> sources, Dictionary<string, string> addresses) {
   var villagerSpecies = new List<Species>();
   // Palette variants baked by OpenWorldNpcBaker (farmer, merchant, artisan, herbalist clothing on
   // the Polytope peasants) give every settlement a varied crowd; the prepared imported prefabs are the
   // fallback when the bake has not run.
   var baked = Resources.LoadAll<GameObject>("World/Villagers");
   if (baked != null) foreach (var prefab in baked) if (prefab) villagerSpecies.Add(new Species { source = "Resources/World/Villagers/" + prefab.name, direct = prefab, kind = Kind.Villager, display = "Villager", height = prefab.name.Contains("Female") ? 1.68f : 1.78f });
   if (villagerSpecies.Count == 0) foreach (var src in sources) villagerSpecies.Add(new Species { source = src, address = addresses[src], kind = Kind.Villager, display = "Villager", height = 1.75f });
   if (villagerSpecies.Count == 0) return;
   species.AddRange(villagerSpecies);
   foreach (var s in Settlements) {
    for (int i = 0; i < s.count; i++) {
     var sp = villagerSpecies[rng.Next(villagerSpecies.Count)];
     float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = s.r * (0.3f + (float)rng.NextDouble() * 0.6f);
     var p = s.c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
     groups.Add(new Group { species = sp, center = new Vector3(p.x, 0f, p.y), size = 1, radius = s.r * 0.8f });
    }
   }
  }

  static bool Ridden(GameObject x) {
   var a = x ? x.GetComponent<OpenWorldAnimalInteraction>() : null;
   return a && (a.Rider || a == OpenWorldAnimalInteraction.LastRidden);
  }

  void Update() {
   var owner = OpenWorldManager.I;
   if (pendingGroup != null) {
    if (!owner || !owner.Hero || owner.IsModalOpen) return;
    if (Vector2.Distance(new Vector2(owner.Hero.transform.position.x, owner.Hero.transform.position.z), new Vector2(pendingGroup.center.x, pendingGroup.center.z)) > 170f) { Despawn(pendingGroup, owner); pendingGroup = null; return; }
    long stamp = OpenWorldStreamingDiagnostics.Start();
    var group = pendingGroup; Spawn(group, owner); if (!group.spawning) pendingGroup = null;
    nextUpdate = Time.time; OpenWorldStreamingDiagnostics.End(OpenWorldStreamingDiagnostics.Work.PopulationTick, stamp); return;
   }
   if (Time.time < nextUpdate) return;
   nextUpdate = Time.time + 0.75f;
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero) return;
   long tickStamp = OpenWorldStreamingDiagnostics.Start();
   Vector3 hero = gm.Hero.transform.position;
   int liveCreatures = 0;
   activeEnemies = 0;
   foreach (var g in groups) {
    g.live.RemoveAll(x => !x);
    if (g.herd != null) g.herd.Members.RemoveAll(x => !x);
    // Villagers have their own limit (the settlements' points); the creature budget is for animals.
    if (g.species.kind == Kind.Enemy) activeEnemies += g.live.Count; else if (g.species.kind != Kind.Villager) liveCreatures += g.live.Count;
   }
   candidates.Clear();
   foreach (var g in groups) {
    float d = Vector2.Distance(new Vector2(hero.x, hero.z), new Vector2(g.center.x, g.center.z));
    if (g.active && g.species.kind != Kind.Enemy) foreach (var actor in g.live) {
     if (actor) d = Mathf.Min(d, Vector2.Distance(new Vector2(hero.x, hero.z), new Vector2(actor.transform.position.x, actor.transform.position.z)));
    }
    float enter = g.species.kind == Kind.Enemy ? 95f : g.species.kind == Kind.Villager ? 90f : g.species.kind == Kind.Bird ? 80f : 110f;
    if (!g.active && d < enter && Time.time >= g.clearedUntil) candidates.Add((d, g));
    else if (g.active && d > enter + 55f && !g.live.Exists(Ridden)) Despawn(g, gm);
    else if (g.active && g.species.kind == Kind.Enemy && g.live.Count == 0) {
     // A defeated pack returns after a while, as wildlife would.
     if (pendingGroup == g) pendingGroup = null;
   g.active = false; g.spawning = false; g.spawnCursor = 0; g.clearedUntil = Time.time + 240f;
    } else if (g.active && g.species.kind != Kind.Enemy && g.species.kind != Kind.Villager && g.live.Count == 0 && d > 60f) {
     // A herd hunted out of existence regrows once Aster is out of sight.
     if (pendingGroup == g) pendingGroup = null;
   g.active = false; g.spawning = false; g.spawnCursor = 0; g.clearedUntil = Time.time + 180f;
    }
    if (g.active) g.species.lastUse = Time.time;
   }
   // Nearest first, so the budget fills around Aster rather than in list order.
   candidates.Sort((a, b) => a.d.CompareTo(b.d));
   foreach (var (d, g) in candidates) {
    bool enemy = g.species.kind == Kind.Enemy, villager = g.species.kind == Kind.Villager;
    bool budgetOk = enemy ? activeEnemies + g.size <= enemyBudget : villager || liveCreatures + g.size <= creatureBudget;
    if (!budgetOk && d < 70f) {
     // Make room near Aster: the group still streamed in farthest behind her goes first.
     Group far = null; float farD = Mathf.Max(enemy ? 100f : 80f, d + 40f);
     foreach (var a in groups) {
      if (!a.active || (a.species.kind == Kind.Enemy) != enemy || a.species.kind == Kind.Villager || a.live.Count == 0 || a.live.Exists(Ridden)) continue;
      float ad = Nearest(a, hero);
      if (ad > farD) { farD = ad; far = a; }
     }
     if (far != null) {
      if (enemy) activeEnemies -= far.live.Count; else liveCreatures -= far.live.Count;
      Despawn(far, gm);
      budgetOk = enemy ? activeEnemies + g.size <= enemyBudget : liveCreatures + g.size <= creatureBudget;
     }
    }
    if (!budgetOk) continue;
    if (Ensure(g.species)) { Spawn(g, gm); if (g.spawning) pendingGroup = g; if (enemy) activeEnemies += g.size; else if (!villager) liveCreatures += g.size; if (pendingGroup != null) break; }
    g.species.lastUse = Time.time;
   }
   // Release prefabs nobody has needed for a minute.
   foreach (var sp in species) {
    if (!sp.requested || Time.time - sp.lastUse < 60f) continue;
    bool used = false;
    foreach (var g in groups) if (g.species == sp && g.active) { used = true; break; }
    if (used) continue;
    // Species sharing a prefab (deer and elk) each hold their own reference-counted handle.
    if (sp.handle.IsValid()) Addressables.Release(sp.handle);
    sp.requested = false; sp.handle = default;
   }
   OpenWorldStreamingDiagnostics.End(OpenWorldStreamingDiagnostics.Work.PopulationTick, tickStamp);
  }

  // Distance from Aster to the nearest member of a streamed-in group (its centre when none is left).
  static float Nearest(Group g, Vector3 hero) {
   float d = Vector2.Distance(new Vector2(hero.x, hero.z), new Vector2(g.center.x, g.center.z));
   foreach (var actor in g.live) if (actor) d = Mathf.Min(d, Vector2.Distance(new Vector2(hero.x, hero.z), new Vector2(actor.transform.position.x, actor.transform.position.z)));
   return d;
  }

  bool Ensure(Species sp) {
   if (sp.direct) return true;
   // A released/failed handle must be reacquired, including while a multi-frame group is pending.
   if (sp.requested && !sp.handle.IsValid()) sp.requested = false;
   if (!sp.requested) {
    if (Time.time < sp.retryAt) return false;
    sp.lastUse = Time.time;
    try { sp.handle = Addressables.LoadAssetAsync<GameObject>(sp.address);sp.requested = true; }
    catch (Exception e) { sp.requested = false;sp.handle = default;sp.retryAt = Time.time + 10;Debug.LogWarning("[Population] cannot load " + sp.source + ": " + e.Message); }
    return false;
   }
   if (!sp.handle.IsDone) return false;
   if (sp.handle.Status == AsyncOperationStatus.Succeeded && sp.handle.Result) return true;
   Addressables.Release(sp.handle);sp.handle = default;sp.requested = false;sp.retryAt = Time.time + 10;
   Debug.LogWarning("[Population] load failed; retrying later: " + sp.source);return false;
  }
  void Spawn(Group g, OpenWorldManager gm) {
   if (!Ensure(g.species)) return;
   long spawnStamp = OpenWorldStreamingDiagnostics.Start();
   if (!g.spawning) { g.active = true; g.generation++; g.spawnCursor = 0; g.spawning = true; }
   var prefab = g.species.direct ? g.species.direct : g.species.handle.Result;
   var terrain = gm.ActiveTerrain;
   var sp = g.species;
   float GroundY(Vector3 q) => terrain ? terrain.SampleHeight(q) + terrain.transform.position.y : OpenWorldMaterialHelper.SampleGroundHeight(q);
   int priorCount = g.live.Count; int end = Mathf.Min(g.size, g.spawnCursor + 1);
   for (int i = g.spawnCursor; i < end; i++) {
    g.spawnCursor = i + 1;
    // A free spot in the group's circle (not inside a house, a fence or a rock); birds take the spot with
    // the shortest grass, so a flock on the ground can be seen.
    Vector3 pos = g.center; float best = float.MaxValue;
    for (int attempt = 0; attempt < 8; attempt++) {
     float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = (float)rng.NextDouble() * g.radius * (attempt < 4 ? 0.6f : 1.2f);
     var cand = g.center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
     cand.y = GroundY(cand);
     if (Blocked(cand, Mathf.Clamp(sp.height, 0.3f, 2.2f), Mathf.Clamp(sp.height * 0.3f, 0.12f, 0.6f))) continue;
     bool preferShortGrass = sp.kind == Kind.Bird || (sp.kind == Kind.Wildlife && sp.height < 1.2f);
     float score = preferShortGrass && terrain ? OpenWorldWildlife.GrassAt(terrain, cand) : attempt;
     if (score < best) { best = score; pos = cand; }
     if (!preferShortGrass) break;
    }
    if (best == float.MaxValue) continue; // Do not spawn inside the blocked fallback spot.
    float jitter = 1f + ((float)rng.NextDouble() * 2f - 1f) * sp.sizeJitter;
    switch (sp.kind) {
     case Kind.Enemy: {
      float h = sp.height * jitter;
      var enemy = OpenWorldEnemy.Spawn(sp.role, sp.display, pos, transform, importedPrefab: prefab, importedHeight: h);
      enemy.Controller.height = Mathf.Max(0.95f, h);
      enemy.Controller.center = Vector3.up * enemy.Controller.height * 0.5f;
      enemy.Controller.radius = Mathf.Clamp(h * (sp.beast ? 0.32f : 0.3f), 0.3f, sp.beast ? 0.75f : 0.6f);
      // Danger rises with distance from the heartland.
      float tier = 1f + Mathf.Clamp01(new Vector2(pos.x, pos.z).magnitude / 420f);
      enemy.MaxHealth *= sp.health * tier; enemy.Health = enemy.MaxHealth;
      enemy.AttackDamage *= sp.damage * (0.85f + tier * 0.15f);
      enemy.LeashRadius = sp.territorial ? sp.leash : 30f;
      if (sp.beast) {
       enemy.Beast = true; enemy.Territorial = sp.territorial; enemy.AggroRadius = sp.aggro; enemy.WarnRadius = sp.warn; enemy.BodyHeight = h;
       enemy.MoveSpeed = sp.chase; enemy.SpecialName = sp.special; enemy.MeatDrop = sp.meat;
       enemy.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
      }
      if (!string.IsNullOrEmpty(sp.coat)) Coat(enemy.gameObject, sp.coat);
      gm.Enemies.Add(enemy);
      g.live.Add(enemy.gameObject);
      break;
     }
     case Kind.Villager: {
      var root = CreatureRoot(prefab, sp.height, pos, "Villager");
      var v = root.AddComponent<OpenWorldVillager>();
      v.Home = g.center; v.Radius = g.radius;
      g.live.Add(root);
      break;
     }
     default: {
      // Each streaming of a herd is a new generation: a carcass is collected once, and the herd regrows.
      string animalId = "hunted:" + sp.source + ":" + g.center.x.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ":" + g.center.z.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ":" + i + ":" + session + "-" + g.generation;
      if (gm.CollectedWorldIds.Contains(animalId)) break;
      var root = CreatureRoot(prefab, sp.height * jitter, pos, sp.display);
      var wild = root.AddComponent<OpenWorldWildlife>();
      wild.Flying = sp.kind == Kind.Bird;
      wild.Nature = sp.temper; wild.WalkSpeed = sp.walk; wild.RunSpeed = sp.run; wild.Awareness = sp.aware; wild.FleeDistance = sp.flee;
      wild.HomeRadius = sp.homeRadius; wild.TurnRate = sp.turn; wild.Friendly = sp.friendly; wild.ZigZag = sp.zigzag; wild.Bounding = sp.zigzag;
      wild.Herd = g.herd; g.herd?.Members.Add(wild);
      if (sp.display == "Crow") wild.Voice = "crows";
      if (!wild.Flying) {
       var interaction = OpenWorldAnimalInteraction.Attach(root, sp.display, animalId);
       if (interaction && sp.meat > 0) interaction.Meat = sp.meat;
      }
      if (sp.coats != null && sp.coats.Length > 0) Tint(root, sp.coats[rng.Next(sp.coats.Length)]);
      if (!string.IsNullOrEmpty(sp.coat)) Coat(root, sp.coat);
      g.live.Add(root);
      break;
     }
    }
   }
   g.spawning = g.spawnCursor < g.size;
   OpenWorldStreamingDiagnostics.End(OpenWorldStreamingDiagnostics.Work.PopulationSpawn, spawnStamp, g.live.Count - priorCount);
  }

  static readonly Collider[] overlap = new Collider[8];
  // Something solid (a building, fence, rock or tree trunk) where a body of this size would stand.
  internal static bool Blocked(Vector3 pos, float height, float radius) {
   int n = Physics.OverlapCapsuleNonAlloc(pos + Vector3.up * (radius + 0.12f), pos + Vector3.up * Mathf.Max(radius + 0.15f, height - radius), radius, overlap, ~0, QueryTriggerInteraction.Ignore);
   for (int i = 0; i < n; i++) {
    var c = overlap[i];
    if (!c || c is TerrainCollider || c.GetComponentInParent<OpenWorldHero>()) continue;
    if (c.attachedRigidbody && !c.attachedRigidbody.isKinematic) continue; // loose props
    return true;
   }
   return false;
  }

  static MaterialPropertyBlock block;
  static readonly Dictionary<string, Texture2D> coatCache = new Dictionary<string, Texture2D>();
  // Swaps the body's albedo for a coat variant (keeps any tint already set on the renderer).
  static void Coat(GameObject root, string name) {
   if (!coatCache.TryGetValue(name, out var tex)) { tex = Resources.Load<Texture2D>("World/Coats/" + name); coatCache[name] = tex; }
   if (!tex) return;
   block ??= new MaterialPropertyBlock();
   foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
    r.GetPropertyBlock(block);
    block.SetTexture("_BaseMap", tex);
    r.SetPropertyBlock(block);
   }
  }

  static void Tint(GameObject root, Color c) {
   if (c == Color.white) return;
   block ??= new MaterialPropertyBlock();
   foreach (var r in root.GetComponentsInChildren<Renderer>(true)) {
    r.GetPropertyBlock(block);
    block.SetColor("_BaseColor", c);
    r.SetPropertyBlock(block);
   }
  }

  GameObject CreatureRoot(GameObject prefab, float height, Vector3 pos, string name) {
   var root = new GameObject(name);
   root.transform.SetParent(transform, false);
   root.transform.position = pos;
   root.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
   var model = Instantiate(prefab, root.transform, false);
   var bounds = OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
   if (bounds.size.y > 0.001f) model.transform.localScale *= height / bounds.size.y;
   bounds = OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
   model.transform.position += new Vector3(pos.x - bounds.center.x, pos.y - bounds.min.y, pos.z - bounds.center.z);
   return root;

  }

  void Despawn(Group g, OpenWorldManager gm) {
   if (pendingGroup == g) pendingGroup = null;
   g.active = false; g.spawning = false; g.spawnCursor = 0;
   foreach (var go in g.live) {
    if (!go) continue;
    var enemy = go.GetComponent<OpenWorldEnemy>();
    if (enemy) gm.Enemies.Remove(enemy);
    Destroy(go);
   }
   g.live.Clear();
   g.herd?.Members.Clear();
  }

  void OnDestroy() {
   if (Instance == this) Instance = null;
   foreach (var sp in species) if (sp.requested && sp.handle.IsValid()) Addressables.Release(sp.handle);
  }

  // ---------- bridge diagnostics ----------
  // Logs every species with planned / active groups and live actors.
  public static void DebugReport(string arg) {
   var p = Instance;
   if (!p) { Debug.Log("[Population] not active (realm missing?)"); return; }
   var sb = new System.Text.StringBuilder();
   var sb2 = new System.Text.StringBuilder();
   sb.AppendLine("[Population] species " + p.species.Count + ", groups " + p.groups.Count + ", enemy budget " + p.enemyBudget + ", creature budget " + p.creatureBudget);
   var seen = new HashSet<Species>();
   foreach (var g in p.groups) {
    if (!seen.Add(g.species)) continue;
    int planned = 0, active = 0, live = 0;
    foreach (var h in p.groups) if (h.species == g.species) { planned++; if (h.active) active++; live += h.live.Count; }
    string status = g.species.direct ? "baked " + g.species.direct.name : !g.species.requested ? "idle" : !g.species.handle.IsValid() ? "invalid" : !g.species.handle.IsDone ? "loading" : g.species.handle.Status.ToString();
    sb.AppendLine($"  {g.species.display} [{g.species.kind}{(g.species.territorial ? ", territorial" : "")}] {g.species.biome} groups {planned}/{g.species.groups}, active {active}, live {live}, asset {status}");
   }
   // "live" / "all" list every live actor; "live <name>" only the species whose name contains it.
   string only = arg != null && arg.StartsWith("live ") ? arg.Substring(5).Trim() : "";
   if (arg == "all" || arg == "live" || only.Length > 0)
    foreach (var g in p.groups) {
     if (only.Length > 0 && g.species.display.IndexOf(only, StringComparison.OrdinalIgnoreCase) < 0) continue;
     foreach (var go in g.live) {
      if (!go) continue;
      var b = OpenWorldMaterialHelper.ComputeRendererBounds(go.transform);
      var renderers = go.GetComponentsInChildren<Renderer>(true);
      int enabled = 0, visible = 0; string shader = "";
      foreach (var r in renderers) { if (r.enabled && r.gameObject.activeInHierarchy) enabled++; if (r.isVisible) visible++; if (shader.Length == 0 && r.sharedMaterial) shader = r.sharedMaterial.shader.name + " / " + r.sharedMaterial.name; }
      var model = go.GetComponentInChildren<OpenWorldImportedModel>();
      string motion = model ? $" state {model.State} speed {model.MeasuredSpeed:0.0} (walk {model.NaturalSpeed(false):0.0}, run {model.NaturalSpeed(true):0.0})" : "";
      var wild = go.GetComponent<OpenWorldWildlife>();
      if (wild) motion += $" mode {wild.ModeName} from hero {Vector3.Distance(go.transform.position, OpenWorldManager.I && OpenWorldManager.I.Hero ? OpenWorldManager.I.Hero.transform.position : go.transform.position):0.0} m";
      sb2.AppendLine($"  live {g.species.display} at {go.transform.position.ToString("0.0")} size {b.size.ToString("0.00")} renderers {renderers.Length} enabled {enabled} visible {visible} shader {shader}{motion}");
     }
    }
   Debug.Log(sb.ToString() + sb2);
  }

  // Walks the hero to 10 m from the nearest planned group whose species or kind matches, facing it,
  // so the group streams in for a screenshot.
  public static void DebugVisit(string what) {
   var p = Instance;
   var gm = OpenWorldManager.I;
   if (!p || !gm || !gm.Hero) { Debug.Log("[Population] visit: not active"); return; }
   what = (what ?? "").Trim();
   Group best = null;
   float bestD = float.MaxValue;
   var hero = gm.Hero.transform.position;
   foreach (var g in p.groups) {
    bool match = g.species.display.IndexOf(what, StringComparison.OrdinalIgnoreCase) >= 0 || string.Equals(g.species.kind.ToString(), what, StringComparison.OrdinalIgnoreCase);
    if (!match) continue;
    float d = Vector2.Distance(new Vector2(hero.x, hero.z), new Vector2(g.center.x, g.center.z));
    if (d < bestD) { bestD = d; best = g; }
   }
   if (best == null) { Debug.Log("[Population] visit: no group matches " + what); return; }
   // Stand 10 m out on the side with the clearest line of sight to the group.
   var terrain = gm.ActiveTerrain;
   float GroundY(Vector3 q) => terrain ? terrain.SampleHeight(q) + terrain.transform.position.y : 0f;
   Vector3 center = best.center; center.y = GroundY(center) + 1.2f;
   Vector3 stand = best.center + new Vector3(-7f, 0f, -7f);
   float bestClear = -1f;
   for (int i = 0; i < 8; i++) {
    float a = i * Mathf.PI / 4f;
    var cand = best.center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 10f;
    var eye = cand; eye.y = GroundY(cand) + 2.2f;
    var dir = eye - center;
    float clear = Physics.Raycast(center, dir.normalized, out var hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : dir.magnitude + 5f;
    clear -= Mathf.Abs(eye.y - center.y) * 0.3f; // prefer level ground
    if (clear > bestClear) { bestClear = clear; stand = cand; }
   }
   gm.Hero.WarpTo(stand);
   var rig = gm.CameraRig;
   if (rig) {
    var look = best.center - stand; look.y = 0f;
    rig.Yaw = Quaternion.LookRotation(look.normalized).eulerAngles.y;
    rig.Pitch = 10f;
    rig.SnapToTarget();
   }
   Debug.Log("[Population] visiting " + best.species.display + " group at " + best.center.ToString("0") + " (" + best.size + ")");
  }

  // Puts the hero <distance> m from the nearest live animal of a species (e.g. "Meadow Deer 9"), facing
  // it, with the camera behind her: the start of a hunting or riding capture.
  public static void DebugApproach(string arg) {
   var p = Instance; var gm = OpenWorldManager.I;
   if (!p || !gm || !gm.Hero) return;
   arg = (arg ?? "").Trim();
   float distance = 9f;
   int cut = arg.LastIndexOf(' ');
   if (cut > 0 && float.TryParse(arg.Substring(cut + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d)) { distance = d; arg = arg.Substring(0, cut).Trim(); }
   GameObject best = null; float bestD = float.MaxValue;
   var h = gm.Hero.transform.position;
   foreach (var g in p.groups) {
    if (g.species.display.IndexOf(arg, StringComparison.OrdinalIgnoreCase) < 0) continue;
    foreach (var go in g.live) {
     if (!go) continue;
     var a = go.GetComponent<OpenWorldAnimalInteraction>();
     if (a && a.Dead) continue;
     float dd = (go.transform.position - h).sqrMagnitude;
     if (dd < bestD) { bestD = dd; best = go; }
    }
   }
   if (!best) { Debug.Log("[Population] approach: no live " + arg + " streamed in"); return; }
   var ap = best.transform.position;
   Vector3 from = ap - best.transform.right * distance;
   gm.Hero.WarpTo(from);
   var look = ap - from; look.y = 0f;
   gm.Hero.transform.rotation = Quaternion.LookRotation(look.normalized);
   if (gm.CameraRig) { gm.CameraRig.Yaw = gm.Hero.transform.eulerAngles.y; gm.CameraRig.Pitch = 12f; gm.CameraRig.SnapToTarget(); }
   Debug.Log($"[Population] approach: {arg} at {ap:0.0}, hero at {from:0.0}");
  }

  // Contact sheet of animals: "name state [filter, filter...]" spawns one of each matching species (all
  // non-villager species when no filter is given) in a row in front of the hero, in side profile, playing
  // one state ("idle", "walk", "run", "graze", "rest", "roar", "fly", "attack", "death"), and saves
  // ClaudeBridge/shots/<name>.jpg from a lens that frames the whole row. The row is removed afterwards.
  public static bool LineupBusy { get; private set; }
  public static void DebugLineupShot(string args) {
   var p = Instance; var gm = OpenWorldManager.I;
   if (!p || !gm || !gm.Hero || LineupBusy) return;
   var parts = (args ?? "").Trim().Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
   string name = parts.Length > 0 ? parts[0] : "lineup";
   string state = parts.Length > 1 ? parts[1] : "idle";
   var filters = parts.Length > 2 ? parts[2].Split(',') : new string[0];
   p.StartCoroutine(p.Lineup(name, state, filters));
  }

  System.Collections.IEnumerator Lineup(string name, string state, string[] filters) {
   LineupBusy = true;
   var gm = OpenWorldManager.I;
   var hero = gm.Hero.transform;
   Vector3 right = hero.right, forward = hero.forward;
   var row = new List<(GameObject go, float h)>();
   float x = 0f;
   var seen = new HashSet<string>();
   var sb = new System.Text.StringBuilder("[Population] lineup " + name + " " + state + ":");
   foreach (var sp in species) {
    if (sp.kind == Kind.Villager || !seen.Add(sp.display)) continue;
    if (filters.Length > 0 && Array.FindIndex(filters, f => sp.display.IndexOf(f.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) < 0) continue;
    if (!sp.requested) { sp.requested = true; sp.handle = Addressables.LoadAssetAsync<GameObject>(sp.address); }
    if (!sp.handle.IsValid()) continue;
    var prefab = sp.handle.WaitForCompletion();
    sp.lastUse = Time.time + 120f;
    if (!prefab) continue;
    float width = Mathf.Max(sp.height < 0.4f ? 0.45f : 0.9f, sp.height * 1.8f);
    x += width * 0.5f;
    Vector3 pos = hero.position + forward * 6f + right * x;
    var terrain = gm.ActiveTerrain;
    pos.y = terrain ? terrain.SampleHeight(pos) + terrain.transform.position.y : pos.y;
    var root = CreatureRoot(prefab, sp.height, pos, "Lineup " + sp.display);
    root.transform.rotation = Quaternion.LookRotation(right);
    var model = root.GetComponentInChildren<OpenWorldImportedModel>();
    if (model) model.State = state;
    if (sp.coats != null && sp.coats.Length > 1) Tint(root, sp.coats[1]);
    if (!string.IsNullOrEmpty(sp.coat)) Coat(root, sp.coat);
    row.Add((root, sp.height));
    sb.Append($" {sp.display}");
    x += width * 0.5f + 0.3f;
   }
   Debug.Log(sb.ToString());
   // Let the clips run for a moment.
   float until = Time.time + 1.2f;
   while (Time.time < until) yield return null;
   if (row.Count > 0) {
    var b = OpenWorldMaterialHelper.ComputeRendererBounds(row[0].go.transform);
    foreach (var r in row) b.Encapsulate(OpenWorldMaterialHelper.ComputeRendererBounds(r.go.transform));
    var camGo = new GameObject("Lineup lens");
    var cam = camGo.AddComponent<Camera>();
    cam.fieldOfView = 30f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 600f;
    float aspect = 2.4f;
    float vHalf = cam.fieldOfView * 0.5f * Mathf.Deg2Rad, hHalf = Mathf.Atan(Mathf.Tan(vHalf) * aspect);
    float fitW = (x * 0.5f + 0.4f) / Mathf.Tan(hHalf) * 1.05f;
    float fitH = (b.size.y * 0.5f + 0.3f) / Mathf.Tan(vHalf);
    float dist = Mathf.Max(fitW, fitH, 3f);
    Vector3 look = hero.position + forward * 6f + right * (x * 0.5f); look.y = b.center.y;
    // From beyond the row looking back toward Aster, so she is never in front of the lens.
    camGo.transform.position = look + forward * dist + Vector3.up * dist * 0.1f;
    camGo.transform.LookAt(look);
    var main = Camera.main;
    if (main) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; }
    int w = 2400, h = 1000;
    var rt = RenderTexture.GetTemporary(w, h, 24);
    cam.targetTexture = rt; cam.aspect = (float)w / h;
    cam.Render();
    var prev = RenderTexture.active; RenderTexture.active = rt;
    var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
    RenderTexture.active = prev; cam.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
    System.IO.Directory.CreateDirectory("ClaudeBridge/shots");
    System.IO.File.WriteAllBytes("ClaudeBridge/shots/" + name + ".jpg", tex.EncodeToJPG(88));
    Destroy(tex); Destroy(camGo);
    Debug.Log("[Population] lineup saved ClaudeBridge/shots/" + name + ".jpg (" + row.Count + " animals)");
   }
   foreach (var r in row) if (r.go) Destroy(r.go);
   LineupBusy = false;
  }
 }

 // Ambient villager: strolls between points of its settlement, pauses, and chats with neighbours.
 public sealed class OpenWorldVillager : MonoBehaviour {
  public Vector3 Home; public float Radius = 10f;
  Vector3 target; float waitUntil; OpenWorldImportedModel anim; string state = "idle";
  void Start() { anim = GetComponentInChildren<OpenWorldImportedModel>(); Pick(); waitUntil = Time.time + UnityEngine.Random.Range(0f, 4f); }
  void Pick() {
   var r = UnityEngine.Random.insideUnitCircle * Radius;
   target = Home + new Vector3(r.x, 0f, r.y);
  }
  void Update() {
   var gm = OpenWorldManager.I;
   if (!gm || gm.IsModalOpen) return;
   if (Time.time < waitUntil) { Set(state == "talk" ? "talk" : "idle"); return; }
   Vector3 to = target - transform.position; to.y = 0f;
   if (to.magnitude < 0.6f) {
    waitUntil = Time.time + UnityEngine.Random.Range(3f, 9f);
    state = UnityEngine.Random.value < 0.35f ? "talk" : "idle";
    Pick();
    return;
   }
   Vector3 dir = to.normalized;
   Vector3 next = transform.position + dir * 1.25f * Time.deltaTime;
   var terrain = gm.ActiveTerrain;
   if (terrain && OpenWorldGrounding.TryWalk(terrain, transform.position, next, out float ground)) {
    next.y = ground + 0.02f;
    transform.position = next;
    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 4f);
    Set("walk");
   } else { Pick(); }
  }
  void Set(string s) { if (anim) anim.State = s; }
 }
}





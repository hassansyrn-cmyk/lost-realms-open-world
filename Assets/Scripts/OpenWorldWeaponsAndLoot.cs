using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LostRealmsOW {
 public enum OWWeaponRarity { Common = 0, Rare = 1, Epic = 2 }

 public readonly struct OWWeaponDef {
  public readonly int Id;
  public readonly string Name;
  public readonly string Resource;
  public readonly float Damage;
  public readonly float Reach;
  public readonly float Tempo;
  public readonly float ModelScale;
  public readonly Vector3 EquipEuler;
  public readonly string Style; // "slash", "chop", "spear"
  public readonly OWWeaponRarity Rarity;
  public readonly int ElementAffinity; // -1 none, 0 Ember, 1 Frost, 2 Gale
  public readonly string Summary;

  public OWWeaponDef(int id, string name, string resource, float damage, float reach, float tempo, float modelScale, Vector3 equipEuler, string style, OWWeaponRarity rarity, int affinity, string summary) {
   Id = id; Name = name; Resource = resource; Damage = damage; Reach = reach; Tempo = tempo;
   ModelScale = modelScale; EquipEuler = equipEuler; Style = style; Rarity = rarity;
   ElementAffinity = affinity; Summary = summary;
  }
 }

 public static class OpenWorldWeaponCatalog {
  // Preserve the original vault odds when exploration-only weapons are appended.
  public static readonly int[] EpicVaultWeaponIds = { 6, 7, 8, 9, 10 };
  // Curated open-world arsenal (excludes removed weapons LongSword, BrassFangs, VerdantFang)
  public static readonly OWWeaponDef[] All = {
   new OWWeaponDef(0, "CURVED SWORD", "Weapons/Aster_CurvedSword", 1.05f, 1.0f, 1.18f, 1.05f, new Vector3(12, 0, 0), "slash", OWWeaponRarity.Common, -1, "Agile starter blade • swift combo"),
   new OWWeaponDef(1, "DUELING BLADE", "Weapons/PureSword1H", 1.15f, 1.05f, 1.10f, 1.05f, new Vector3(10, 90, 90), "slash", OWWeaponRarity.Common, -1, "Balanced knight blade • steady arcs"),
   new OWWeaponDef(2, "WAR AXE", "Weapons/Aster_Axe", 1.55f, 0.92f, 0.80f, 0.95f, new Vector3(-82, 0, 180), "chop", OWWeaponRarity.Common, -1, "Heavy cleaver • hyper-armor on swing"),
   new OWWeaponDef(3, "BATTLE AXE", "Weapons/AxeBattle", 1.50f, 0.95f, 0.78f, 1.20f, new Vector3(8, 0, 0), "chop", OWWeaponRarity.Common, -1, "Broad iron axe • guard-breaking chops"),
   new OWWeaponDef(4, "RANGER AXE", "Weapons/RangerAxe", 1.32f, 0.98f, 1.04f, 1.15f, new Vector3(8, 0, 0), "chop", OWWeaponRarity.Common, -1, "Woodland hatchet • brisk recovery"),
   new OWWeaponDef(5, "REAPER'S SPEAR", "Weapons/PureSpear", 1.18f, 1.38f, 0.96f, 1.50f, new Vector3(10, 0, 0), "spear", OWWeaponRarity.Common, -1, "Long polearm • +30% tipper bonus at max reach"),
   new OWWeaponDef(6, "CRUSHER HAMMER", "Weapons/PureHammer", 1.65f, 0.92f, 0.74f, 1.22f, new Vector3(8, 0, 0), "chop", OWWeaponRarity.Rare, 0, "Rare • Hold Attack: Ember Quake shockwave"),
   new OWWeaponDef(7, "FROST HALBERD", "Weapons/FrostHalberd", 1.35f, 1.44f, 0.92f, 1.52f, new Vector3(10, 0, 0), "spear", OWWeaponRarity.Rare, 1, "Rare • Hold Attack: Piercing Ice Lance (chills foes)"),
   new OWWeaponDef(8, "ORNATE CURVED BLADE", "Weapons/OrnateCurvedBlade", 1.38f, 1.18f, 1.28f, 1.18f, new Vector3(12, 90, 90), "slash", OWWeaponRarity.Rare, 2, "Rare • Hold Attack: Gale Crosscut wind wave"),
   new OWWeaponDef(9, "DUSKBLADE", "Weapons/Duskblade", 1.68f, 1.25f, 0.98f, 1.30f, new Vector3(10, 90, 90), "slash", OWWeaponRarity.Epic, 2, "Epic • Hold Attack: Starfall Lance & Aether surge"),
   new OWWeaponDef(10, "SOULREAPER", "Weapons/Soulreaper", 1.82f, 1.42f, 0.82f, 1.55f, new Vector3(10, 90, 90), "slash", OWWeaponRarity.Epic, 0, "Epic • Hold Attack: Soul Vortex (+8 Aether on hit)"),
   new OWWeaponDef(11, "ELVEN SWORD", "elf_sword", 1.25f, 1.1f, 1.1f, 1.15f, new Vector3(10, 0, 0), "slash", OWWeaponRarity.Rare, 2, "Discovered at the elven caravan - slash mastery"),
   new OWWeaponDef(12, "ELVEN AXE", "elf_axe", 1.55f, 1.0f, 0.85f, 1.2f, new Vector3(10, 0, 0), "chop", OWWeaponRarity.Rare, 0, "Discovered at the elven caravan - chop mastery"),
   new OWWeaponDef(13, "ELVEN SPEAR", "elf_spear", 1.25f, 1.45f, 0.95f, 1.55f, new Vector3(10, 0, 0), "spear", OWWeaponRarity.Rare, 2, "Discovered at the elven caravan - spear mastery"),
   new OWWeaponDef(14, "ELVEN HAMMER", "elf_hammer", 1.7f, 0.88f, 0.75f, 1.2f, new Vector3(10, 0, 0), "chop", OWWeaponRarity.Rare, 0, "Discovered at the elven caravan - chop mastery"),
   new OWWeaponDef(15, "ELVEN STAFF", "elf_staff", 1.2f, 1.4f, 1.0f, 1.5f, new Vector3(10, 0, 0), "spear", OWWeaponRarity.Rare, 1, "Discovered at the elven caravan - spear mastery"),
   new OWWeaponDef(16, "CARAVAN IRON SWORD", "expansion_iron_sword", 1.2f, 1.05f, 1.1f, 1.1f, Vector3.zero, "slash", OWWeaponRarity.Common, -1, "Caravan equipment • balanced sword"),
   new OWWeaponDef(17, "WOODSMAN'S AXE", "expansion_iron_axe", 1.45f, .95f, .85f, 1.1f, Vector3.zero, "chop", OWWeaponRarity.Common, -1, "Caravan equipment • heavy chopping attacks")
  };

  public static OWWeaponDef Get(int id) => All[Mathf.Clamp(id, 0, All.Length - 1)];

  public static Quaternion ModelCorrection(int id) {
   string res = Get(id).Resource;
   if (res.EndsWith("Duskblade") || res.EndsWith("OrnateCurvedBlade"))
    return Quaternion.Inverse(Quaternion.LookRotation(Vector3.forward, Vector3.down));
   if (res.EndsWith("Soulreaper"))
    return Quaternion.Inverse(Quaternion.LookRotation(Vector3.right, Vector3.up)) * Quaternion.AngleAxis(180f, Vector3.up);
   return Quaternion.identity;
  }

  public static Vector3 GripOffset(int id, Bounds localBounds) {
   string res = Get(id).Resource;
   if (res.EndsWith("Duskblade")) return new Vector3(0f, 0.34f, 0f);
   if (res.EndsWith("Soulreaper")) return new Vector3(-0.008f, -0.20f, -0.145f);
   if (res.EndsWith("OrnateCurvedBlade")) return new Vector3(0.020f, 0.728f, 0f);
   if (res.EndsWith("Aster_Axe")) return new Vector3(0f, 0f, 0.18f);
   if (res.EndsWith("Aster_CurvedSword")) return new Vector3(0f, 0f, 0.10f);
   float frac = Get(id).Style == "spear" ? 0.27f : Get(id).Style == "chop" ? 0.22f : 0.08f;
   Vector3 grip = localBounds.center;
   if (localBounds.size.z > localBounds.size.y && localBounds.size.z > localBounds.size.x) grip.z = localBounds.min.z + localBounds.size.z * frac;
   else if (localBounds.size.x > localBounds.size.y) grip.x = localBounds.min.x + localBounds.size.x * frac;
   else grip.y = localBounds.min.y + localBounds.size.y * frac;
   return grip;
  }

  public static GameObject AttachToHand(Transform hand, int weaponId, out TrailRenderer trail) {
   trail = null;
   if (!hand) return null;
   for (int i = hand.childCount - 1; i >= 0; i--) {
    var ch = hand.GetChild(i);
    if (ch.name.StartsWith("OW_Equipped_")) {
     ch.gameObject.SetActive(false);
     if (Application.isPlaying) Object.Destroy(ch.gameObject);
     else Object.DestroyImmediate(ch.gameObject);
    }
   }

   var def = Get(weaponId);
   var prefab = Resources.Load<GameObject>(def.Resource) ?? OpenWorldImportedContent.Prefab(def.Resource);
   if (!prefab) return null;

   var root = new GameObject("OW_Equipped_" + def.Name);
   // Measure in an identity frame, independent of the animated hand and rig scale.
   root.transform.localPosition = Vector3.zero;
   root.transform.localRotation = Quaternion.identity;

   var model = Object.Instantiate(prefab, root.transform, false);
   model.name = def.Name + "_Mesh";
   foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);

   var renderers = model.GetComponentsInChildren<Renderer>(true);
   float longest = 0f;
   foreach (var r in renderers) {
    var sz = r.bounds.size;
    float m = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z));
    if (m > longest) longest = m;
   }
   if (longest > 0.0001f) model.transform.localScale *= def.ModelScale / longest;

   string shortName = def.Resource.Substring(def.Resource.LastIndexOf('/') + 1);
   var albedo = Resources.Load<Texture2D>("Weapons/Textures/" + shortName + "_basecolor");
   var normal = Resources.Load<Texture2D>("Weapons/Textures/" + shortName + "_normal");
   var mat = OpenWorldMaterialHelper.CreateLit("Weapon_" + shortName, Color.white, albedo, normal, 0.45f, 0.35f);
   foreach (var r in renderers) {
    if (albedo != null || r.sharedMaterial == null || r.sharedMaterial.shader == null || r.sharedMaterial.shader.name.Contains("InternalError") || r.sharedMaterial.shader.name == "Standard") {
     r.sharedMaterial = mat;
    }
   }

   var meshRenderer = model.GetComponentInChildren<Renderer>();
   if (meshRenderer) {
    Vector3 gripLocal = GripOffset(weaponId, meshRenderer.localBounds);
    // Meshes use different length axes. Align the handle-to-blade direction to the
    // same hand socket instead of applying unrelated prefab Euler angles.
    var b = meshRenderer.localBounds;
    Vector3 axis = b.size.z > b.size.y && b.size.z > b.size.x ? Vector3.forward : b.size.x > b.size.y ? Vector3.right : Vector3.up;
    if (Vector3.Dot(b.center - gripLocal, axis) < 0f) axis = -axis;
    Vector3 modelAxis = root.transform.InverseTransformDirection(meshRenderer.transform.TransformDirection(axis));
    model.transform.localRotation = Quaternion.FromToRotation(modelAxis, Vector3.forward) * model.transform.localRotation;
    model.transform.localPosition -= root.transform.InverseTransformPoint(meshRenderer.transform.TransformPoint(gripLocal));

    var tip = new GameObject("BladeTip").transform;
    tip.SetParent(meshRenderer.transform, false);
    var lb = meshRenderer.localBounds;
    tip.localPosition = lb.center + Vector3.Scale(axis, lb.extents);
    trail = tip.gameObject.AddComponent<TrailRenderer>();
    trail.time = 0.14f;
    trail.minVertexDistance = 0.05f;
    trail.emitting = false;
    trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.06f), new Keyframe(1f, 0f));
    var tmat = new Material(Shader.Find("Sprites/Default"));
    Color tc = def.ElementAffinity == 0 ? new Color(1f, 0.45f, 0.15f, 0.85f) :
               def.ElementAffinity == 1 ? new Color(0.35f, 0.85f, 1f, 0.85f) :
               def.ElementAffinity == 2 ? new Color(0.35f, 1f, 0.65f, 0.85f) :
               new Color(0.9f, 0.95f, 1f, 0.8f);
    tmat.color = tc;
    trail.sharedMaterial = tmat;
    trail.shadowCastingMode = ShadowCastingMode.Off;
   }
   root.transform.SetParent(hand, true);
   Vector3 palm = Vector3.zero;
   var rig = hand.GetComponentInParent<Animator>();
   if (rig && rig.isHuman) {
    var knuckle = rig.GetBoneTransform(HumanBodyBones.RightMiddleProximal) ?? rig.GetBoneTransform(HumanBodyBones.RightIndexProximal);
    if (knuckle) palm = hand.InverseTransformPoint(Vector3.Lerp(hand.position, knuckle.position, .6f));
   }
   root.transform.localPosition = palm;
   root.transform.localRotation = Quaternion.identity;
   var carry = root.AddComponent<OpenWorldWeaponCarry>();
   carry.Hand = hand;
   carry.GripLocal = palm;
   carry.Hero = hand.GetComponentInParent<OpenWorldHero>();
   return root;
  }
 }

 // Keep ownership under the hand so equipment/skin replacement cleans up both carry modes.
 [DefaultExecutionOrder(10000)]
 public sealed class OpenWorldWeaponCarry : MonoBehaviour {
  public Transform Hand;
  public Vector3 GripLocal;
  public OpenWorldHero Hero;
  float drawnUntil;
  void LateUpdate() {
   if (!Hand || !Hero || !Hero.Visual) return;
   var visual = Hero.Visual;
   string state = visual.CurrentState ?? "";
   bool attacking = state.StartsWith("attack") || state.StartsWith("chop") || state.StartsWith("spear") || state.StartsWith("charged") || state == "block";
   if (attacking || Hero.LockOnTarget) drawnUntil = Time.time + 1.5f;
   if (Time.time < drawnUntil && !Hero.IsGliding && !Hero.IsMounted && !Hero.IsSwimming && !Hero.IsClimbing) {
    transform.position = Hand.TransformPoint(GripLocal);
    transform.rotation = Hand.rotation;
   } else {
    var animator = visual.Animator;
    Transform chest = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Chest) : null;
    Vector3 anchor = chest ? chest.position : Hero.transform.position + Vector3.up * 1.35f;
    Transform hips = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
    Transform head = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
    Vector3 spine = hips && head ? (head.position - hips.position).normalized : Vector3.up;
    Vector3 back = Vector3.Cross(spine, Hero.transform.right).normalized;
    if (back.sqrMagnitude < .01f) back = -Hero.transform.forward;
    // A fixed clearance behind the torso keeps both blade and hilt outside armour.
    transform.position = anchor + back * .38f + Hero.transform.right * .28f + spine * .12f;
    transform.rotation = Quaternion.LookRotation((-Hero.transform.right * .5f - spine).normalized, back);
   }
  }
 }

 public enum OWCollectibleKind {
  GoldCoin,
  StarGem,
  HealthPotion,
  AetherPotion,
  TempleKey,
  SecretChest
 }

 // World collectible that cleanly disappears upon collection (fixes chests & keys staying visible
 // and implements both crimson Health Potions and violet/emerald Aether Potions).
 public sealed class OpenWorldCollectible : MonoBehaviour {
  public OWCollectibleKind Kind;
  public int Amount = 1;
  public string Title = "";
  public string PersistentId = "";
  public bool Collected { get; private set; }
  float baseY;
  float phase;
  TextMesh floatingLabel;

  public static OpenWorldCollectible Spawn(OWCollectibleKind kind, Vector3 position, Transform parent, int amount = 1, string title = "", string persistentId = "") {
   float gy = OpenWorldMaterialHelper.SampleGroundHeight(position);
   position.y = gy + (kind == OWCollectibleKind.SecretChest ? 0.02f : 0.45f);

   var root = new GameObject("Collectible_" + kind);
   if (parent) root.transform.SetParent(parent, false);
   root.transform.position = position;

   var col = root.AddComponent<OpenWorldCollectible>();
   col.Kind = kind;
   col.Amount = amount;
   col.Title = title;
   col.PersistentId = persistentId;
   col.baseY = position.y;
   col.phase = Random.Range(0f, 6.28f);
   col.BuildVisual();
   return col;
  }

  static void SafeDestroy(Object obj) {
   if (!obj) return;
   if (Application.isPlaying) Destroy(obj);
   else DestroyImmediate(obj);
  }

  void BuildVisual() {
   switch (Kind) {
    case OWCollectibleKind.HealthPotion:
     BuildPotionBottle(new Color(0.95f, 0.16f, 0.20f), new Color(0.45f, 0.05f, 0.08f), "HP POTION");
     break;
    case OWCollectibleKind.AetherPotion:
     BuildPotionBottle(new Color(0.48f, 0.16f, 0.98f), new Color(0.22f, 0.06f, 0.55f), "AETHER POTION");
     break;
    case OWCollectibleKind.GoldCoin: {
     var m = OpenWorldMaterialHelper.SpawnModel("Pickups/StarCoin", null, transform, transform.position, 0.42f, new Color(1f, 0.84f, 0.22f), false);
     if (m) {
      OpenWorldMaterialHelper.TintHierarchy(m, new Color(1f, 0.84f, 0.22f), new Color(0.38f, 0.26f, 0.04f));
     } else {
      var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      cyl.transform.SetParent(transform, false);
      cyl.transform.localScale = new Vector3(0.36f, 0.05f, 0.36f);
      cyl.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
      SafeDestroy(cyl.GetComponent<Collider>());
      cyl.GetComponent<Renderer>().sharedMaterial = OpenWorldMaterialHelper.CreateLit("GoldCoin", new Color(1f, 0.82f, 0.2f), null, null, 0.75f, 0.65f, new Color(0.35f, 0.25f, 0.02f));
     }
     break;
    }
    case OWCollectibleKind.StarGem: {
     var m = OpenWorldMaterialHelper.SpawnModel("Pickups/5SideDiamond", null, transform, transform.position, 0.46f, new Color(0.32f, 0.90f, 1f), false);
     if (m) {
      OpenWorldMaterialHelper.TintHierarchy(m, new Color(0.32f, 0.90f, 1f), new Color(0.12f, 0.48f, 0.65f));
     } else {
      var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      s.transform.SetParent(transform, false);
      s.transform.localScale = new Vector3(0.32f, 0.45f, 0.32f);
      SafeDestroy(s.GetComponent<Collider>());
      s.GetComponent<Renderer>().sharedMaterial = OpenWorldMaterialHelper.CreateLit("StarGem", new Color(0.3f, 0.88f, 1f), null, null, 0.85f, 0.2f, new Color(0.1f, 0.45f, 0.6f));
     }
     break;
    }
    case OWCollectibleKind.TempleKey: {
     var m = OpenWorldMaterialHelper.SpawnModel("Props/Expansion/TempleKey", "Props/Expansion/Textures/TempleKey_basecolor", transform, transform.position, 0.95f, new Color(1f, 0.9f, 0.55f), false);
     if (!m) {
      var fallback = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      fallback.transform.SetParent(transform, false);
      fallback.transform.localScale = new Vector3(0.25f, 0.45f, 0.25f);
      SafeDestroy(fallback.GetComponent<Collider>());
      fallback.GetComponent<Renderer>().sharedMaterial = OpenWorldMaterialHelper.CreateLit("KeyFallback", new Color(1f, 0.85f, 0.25f));
     }
     AddFloatingLabel(string.IsNullOrEmpty(Title) ? "TEMPLE KEY" : Title, 1.25f, new Color(1f, 0.88f, 0.4f));
     break;
    }
    case OWCollectibleKind.SecretChest: {
     var m = OpenWorldMaterialHelper.SpawnModel("Props/Expansion/SecretCache", "Props/Expansion/Textures/SecretCache_basecolor", transform, transform.position, 0.92f, Color.white, false);
     if (!m) {
      var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
      fallback.transform.SetParent(transform, false);
      fallback.transform.localPosition = new Vector3(0f, 0.4f, 0f);
      fallback.transform.localScale = new Vector3(0.9f, 0.65f, 0.6f);
      SafeDestroy(fallback.GetComponent<Collider>());
      fallback.GetComponent<Renderer>().sharedMaterial = OpenWorldMaterialHelper.CreateLit("ChestFallback", new Color(0.65f, 0.45f, 0.18f));
     }
     AddFloatingLabel(string.IsNullOrEmpty(Title) ? "TREASURE CHEST" : Title, 1.35f, new Color(1f, 0.85f, 0.35f));
     break;
    }
   }
  }

  void BuildPotionBottle(Color liquidColor, Color emissionColor, string labelText) {
   var m = OpenWorldMaterialHelper.SpawnModel("Pickups/RedPotion", null, transform, transform.position, 0.56f, liquidColor, false);
   if (m) {
    OpenWorldMaterialHelper.TintHierarchy(m, liquidColor, emissionColor);
   } else {
    var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    body.name = "FlaskBody";
    body.transform.SetParent(transform, false);
    body.transform.localScale = new Vector3(0.36f, 0.40f, 0.36f);
    SafeDestroy(body.GetComponent<Collider>());
    body.GetComponent<Renderer>().sharedMaterial = OpenWorldMaterialHelper.CreateLit("Potion_" + labelText, liquidColor, null, null, 0.8f, 0.15f, emissionColor);

    var neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    neck.name = "FlaskNeck";
    neck.transform.SetParent(transform, false);
    neck.transform.localPosition = new Vector3(0f, 0.22f, 0f);
    neck.transform.localScale = new Vector3(0.13f, 0.10f, 0.13f);
    SafeDestroy(neck.GetComponent<Collider>());
    neck.GetComponent<Renderer>().sharedMaterial = OpenWorldMaterialHelper.CreateLit("PotionCork", new Color(0.75f, 0.6f, 0.4f));
   }

   AddFloatingLabel(labelText, 0.72f, liquidColor);
  }

  void AddFloatingLabel(string text, float height, Color color) {
   var go = new GameObject("Label");
   go.transform.SetParent(transform, false);
   go.transform.localPosition = new Vector3(0f, height, 0f);
   floatingLabel = go.AddComponent<TextMesh>();
   floatingLabel.text = text;
   floatingLabel.fontSize = 32;
   floatingLabel.characterSize = 0.04f;
   floatingLabel.anchor = TextAnchor.MiddleCenter;
   floatingLabel.color = color;
   floatingLabel.gameObject.SetActive(false);
  }

  void Update() {
   if (Collected) return;
   if (Kind != OWCollectibleKind.SecretChest) {
    transform.Rotate(0f, 85f * Time.deltaTime, 0f, Space.World);
    Vector3 p = transform.position;
    p.y = baseY + Mathf.Sin(Time.time * 2.6f + phase) * 0.09f;
    transform.position = p;
   }
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero || gm.Hero.Dead || gm.IsModalOpen) return;

   float dist = Vector3.Distance(gm.Hero.transform.position, transform.position);
   if (floatingLabel) {
    bool showLabel = dist <= 13.5f;
    if (floatingLabel.gameObject.activeSelf != showLabel) floatingLabel.gameObject.SetActive(showLabel);
    if (showLabel && Camera.main) floatingLabel.transform.rotation = Camera.main.transform.rotation;
   }

   float pickupRadius = Kind == OWCollectibleKind.SecretChest ? 2.1f : 1.55f;
   if (dist <= pickupRadius) {
    Collect(gm);
   }
  }

  public void Collect(OpenWorldManager gm) {
   if (Collected || !gm || !gm.Hero || gm.Hero.Dead || gm.IsModalOpen) return;
   if (Kind == OWCollectibleKind.HealthPotion && gm.Hero.Health >= gm.Hero.MaxHealth) return;
   if (Kind == OWCollectibleKind.AetherPotion && gm.Hero.Aether >= gm.Hero.MaxAether) return;
   if (!string.IsNullOrEmpty(PersistentId) && !gm.CollectedWorldIds.Add(PersistentId)) {
    RestoreCollected();
    return;
   }
   Collected = true;

   switch (Kind) {
    case OWCollectibleKind.GoldCoin:
     gm.AddGold(Amount);
     gm.SpawnBurst(transform.position, new Color(1f, 0.85f, 0.2f), 8);
     break;
    case OWCollectibleKind.StarGem:
     gm.AddGems(Amount);
     gm.SpawnBurst(transform.position, new Color(0.3f, 0.9f, 1f), 10);
     break;
    case OWCollectibleKind.HealthPotion:
     gm.Hero.Heal(2f);
     gm.ShowBanner("HEALTH POTION  •  +2 HP Restored");
     gm.SpawnBurst(transform.position, new Color(1f, 0.25f, 0.3f), 14);
     break;
    case OWCollectibleKind.AetherPotion:
     gm.Hero.RestoreAether(35f);
     gm.ShowBanner("AETHER POTION  •  +35 Aether Restored");
     gm.SpawnBurst(transform.position, new Color(0.68f, 0.28f, 1f), 14);
     break;
    case OWCollectibleKind.TempleKey:
     gm.CollectKey(Title);
     gm.SpawnBurst(transform.position, new Color(1f, 0.88f, 0.35f), 16);
     break;
    case OWCollectibleKind.SecretChest:
     gm.CollectWorldChest(Title);
     gm.SpawnBurst(transform.position + Vector3.up * 0.5f, new Color(1f, 0.8f, 0.25f), 20);
     break;
   }

   // Completely hide and destroy the object so chests and keys never remain behind after collection
   gameObject.SetActive(false);
   gm.MarkProgressDirty();
   SafeDestroy(gameObject);
  }

  public void RestoreCollected() {
   Collected = true;
   gameObject.SetActive(false);
   // Keep the identifier available until the freshly generated world is discarded.
  }
 }
}

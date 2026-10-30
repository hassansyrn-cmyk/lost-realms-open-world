using UnityEngine;

namespace LostRealmsOW {
 // Enterable subterranean-style Sunken Crypt of the First Wardens (built using
 // Props/Crypt/CryptEntrance on the surface and CryptRoomDeck, CryptArch, CryptMemoryAltar,
 // ReturnPassage, and Astral/Orrery + SunPad + MoonPad in the Abyssal Crypt Chamber).
 public sealed class OpenWorldCryptDungeon : MonoBehaviour {
  public static readonly Vector3 EntrancePos = new Vector3(30f, 0f, -24f);
  public static readonly Vector3 ChamberCenter = new Vector3(88f, 34f, -42f);
  public static readonly Vector3 SunPadPos = new Vector3(84.2f, 34f, -41.5f);
  public static readonly Vector3 MoonPadPos = new Vector3(91.8f, 34f, -41.5f);
  public static readonly Vector3 MemoryAltarPos = new Vector3(88f, 34f, -37.2f);
  public static readonly Vector3 ReturnPortalPos = new Vector3(88f, 34f, -46.6f);

  Transform orreryModel;
  GameObject sunHalo;
  GameObject moonHalo;
  GameObject altarHalo;
  TextMesh entranceLabel;
  TextMesh returnLabel;
  TextMesh sunLabel;
  TextMesh moonLabel;
  TextMesh altarLabel;

  public bool SunAligned { get; private set; }
  public bool MoonAligned { get; private set; }
  public bool Completed { get; private set; }

  public static OpenWorldCryptDungeon Build(OpenWorldManager gm) {
   var root = new GameObject("SunkenCryptDungeon");
   root.transform.SetParent(gm.WorldRoot, false);
   var crypt = root.AddComponent<OpenWorldCryptDungeon>();
   crypt.BuildSurfaceEntrance(gm, root.transform);
   crypt.BuildAbyssalChamber(gm, root.transform);
   return crypt;
  }

  void BuildSurfaceEntrance(OpenWorldManager gm, Transform parent) {
   Vector3 pos = EntrancePos;
   pos.y = OpenWorldMaterialHelper.SampleGroundHeight(pos);
   gm.ReserveSpot(pos, 8f);

   var entranceRoot = new GameObject("CryptSurfaceEntrance").transform;
   entranceRoot.SetParent(parent, false);
   entranceRoot.position = pos;

   OpenWorldMaterialHelper.SpawnModel("Props/Crypt/CryptEntrance", "Props/Crypt/Textures/CryptEntrance_basecolor",
    entranceRoot, pos, 4.4f, Color.white, true);
   OpenWorldMaterialHelper.SpawnModel("Stormveil/MemoryPlaque", "Stormveil/Textures/MemoryPlaque_basecolor",
    entranceRoot, pos + new Vector3(-2.6f, 0f, 1.4f), 1.6f, Color.white, true);

   var lightGo = new GameObject("CryptEntranceLight");
   lightGo.transform.SetParent(entranceRoot, false);
   lightGo.transform.localPosition = new Vector3(0f, 2.4f, 0.8f);
   var pl = lightGo.AddComponent<Light>();
   pl.type = LightType.Point;
   pl.color = new Color(0.58f, 0.38f, 1.0f);
   pl.intensity = 1.8f;
   pl.range = 12f;
   pl.shadows = LightShadows.None;

   entranceLabel = CreateLabel(entranceRoot, pos + new Vector3(0f, 4.2f, 0f),
    "SUNKEN CRYPT OF THE FIRST WARDENS\n[PRESS INTERACT TO DESCEND]", new Color(0.76f, 0.58f, 1.0f));
  }

  void BuildAbyssalChamber(OpenWorldManager gm, Transform parent) {
   var chamber = new GameObject("AbyssalCryptChamber").transform;
   chamber.SetParent(parent, false);
   chamber.position = ChamberCenter;

   // Solid walkable stone deck + fallback box collider so raycasts & character controller are 100% rock-solid
   var deck = OpenWorldMaterialHelper.SpawnModel("Props/Crypt/CryptRoomDeck", "Props/Crypt/Textures/CryptRoomDeck_basecolor",
    chamber, ChamberCenter, 3.2f, Color.white, true);
   if (deck) deck.transform.localScale = new Vector3(1.65f, 1.0f, 1.65f);

   var floorColGo = new GameObject("CryptFloorCollider");
   floorColGo.transform.SetParent(chamber, false);
   floorColGo.transform.position = ChamberCenter + new Vector3(0f, -0.35f, 0f);
   var box = floorColGo.AddComponent<BoxCollider>();
   box.size = new Vector3(18f, 0.7f, 18f);

   Physics.SyncTransforms();
   float deckY = OpenWorldMaterialHelper.SampleGroundHeight(ChamberCenter + Vector3.up * 2f);

   // 4 Crypt Arches framing the subterranean hall
   Vector3[] archOffsets = {
    new Vector3(-6.2f, 0f, -2.5f),
    new Vector3(6.2f, 0f, -2.5f),
    new Vector3(-6.2f, 0f, 3.5f),
    new Vector3(6.2f, 0f, 3.5f)
   };
   foreach (var off in archOffsets) {
    Vector3 ap = new Vector3(ChamberCenter.x + off.x, deckY, ChamberCenter.z + off.z);
    OpenWorldMaterialHelper.SpawnModel("Props/Crypt/CryptArch", "Props/Crypt/Textures/CryptArch_basecolor",
     chamber, ap, 4.6f, Color.white, false);
   }

   // Centerpiece Astral Orrery
   Vector3 orreryPos = new Vector3(ChamberCenter.x, deckY, ChamberCenter.z + 0.5f);
   var orreryGo = OpenWorldMaterialHelper.SpawnModel("Astral/Orrery", "Astral/Textures/Orrery_basecolor",
    chamber, orreryPos, 2.8f, Color.white, true);
   if (orreryGo) orreryModel = orreryGo.transform;

   // West Sun Pad & East Moon Pad
   Vector3 sunPos = new Vector3(SunPadPos.x, deckY, SunPadPos.z);
   OpenWorldMaterialHelper.SpawnModel("Astral/SunPad", "Astral/Textures/SunPad_basecolor",
    chamber, sunPos, 0.35f, Color.white, false);
   sunHalo = CreateDisc(chamber, sunPos + Vector3.up * 0.08f, new Color(1.0f, 0.78f, 0.24f));
   sunLabel = CreateLabel(chamber, sunPos + Vector3.up * 1.65f, "SOLAR SEAL PAD\n[STEP OR INTERACT]", new Color(1.0f, 0.84f, 0.32f));

   Vector3 moonPos = new Vector3(MoonPadPos.x, deckY, MoonPadPos.z);
   OpenWorldMaterialHelper.SpawnModel("Astral/MoonPad", "Astral/Textures/MoonPad_basecolor",
    chamber, moonPos, 0.35f, Color.white, false);
   moonHalo = CreateDisc(chamber, moonPos + Vector3.up * 0.08f, new Color(0.38f, 0.82f, 1.0f));
   moonLabel = CreateLabel(chamber, moonPos + Vector3.up * 1.65f, "LUNAR SEAL PAD\n[STEP OR INTERACT]", new Color(0.48f, 0.88f, 1.0f));

   // North Crypt Memory Altar
   Vector3 altarPos = new Vector3(MemoryAltarPos.x, deckY, MemoryAltarPos.z);
   OpenWorldMaterialHelper.SpawnModel("Props/Crypt/CryptMemoryAltar", "Props/Crypt/Textures/CryptMemoryAltar_basecolor",
    chamber, altarPos, 2.3f, Color.white, true);
   altarHalo = CreateDisc(chamber, altarPos + Vector3.up * 0.1f, new Color(0.75f, 0.45f, 1.0f));
   altarLabel = CreateLabel(chamber, altarPos + Vector3.up * 2.8f, "CRYPT MEMORY ALTAR\n[ALIGN SOLAR & LUNAR PADS]", new Color(0.82f, 0.62f, 1.0f));

   // South Return Passage back to surface
   Vector3 retPos = new Vector3(ReturnPortalPos.x, deckY, ReturnPortalPos.z);
   OpenWorldMaterialHelper.SpawnModel("Props/Expansion/ReturnPassage", "Props/Expansion/Textures/ReturnPassage_basecolor",
    chamber, retPos, 3.2f, Color.white, false);
   returnLabel = CreateLabel(chamber, retPos + Vector3.up * 3.1f, "RETURN PASSAGE\n[INTERACT TO ASCEND TO HUB]", new Color(0.45f, 0.96f, 0.82f));

   // Subterranean ambient point light
   var chamberLightGo = new GameObject("CryptChamberLight");
   chamberLightGo.transform.SetParent(chamber, false);
   chamberLightGo.transform.position = new Vector3(ChamberCenter.x, deckY + 4.5f, ChamberCenter.z);
   var cl = chamberLightGo.AddComponent<Light>();
   cl.type = LightType.Point;
   cl.color = new Color(0.55f, 0.72f, 1.0f);
   cl.intensity = 2.4f;
   cl.range = 24f;
   cl.shadows = LightShadows.None;

   ApplyState(false, false, false);
  }

  static GameObject CreateDisc(Transform parent, Vector3 worldPos, Color c) {
   var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
   disc.name = "SealHalo";
   disc.transform.SetParent(parent, false);
   disc.transform.position = worldPos;
   disc.transform.localScale = new Vector3(1.5f, 0.02f, 1.5f);
   var col = disc.GetComponent<Collider>();
   if (col) {
    if (Application.isPlaying) Destroy(col);
    else DestroyImmediate(col);
   }
   disc.GetComponent<Renderer>().sharedMaterial =
    OpenWorldMaterialHelper.CreateLit("CryptDisc_" + c, c, null, null, 0.88f, 0.25f, c * 0.65f);
   return disc;
  }

  static TextMesh CreateLabel(Transform parent, Vector3 worldPos, string text, Color color) {
   var go = new GameObject("CryptLabel");
   go.transform.SetParent(parent, false);
   go.transform.position = worldPos;
   var tm = go.AddComponent<TextMesh>();
   tm.text = text;
   tm.fontSize = 30;
   tm.characterSize = 0.042f;
   tm.anchor = TextAnchor.MiddleCenter;
   tm.alignment = TextAlignment.Center;
   tm.color = color;
   go.SetActive(false);
   return tm;
  }

  public void ApplyState(bool sun, bool moon, bool completed) {
   SunAligned = sun || completed;
   MoonAligned = moon || completed;
   Completed = completed;

   if (sunLabel) {
    sunLabel.text = SunAligned ? "SOLAR SEAL PAD\n[ALIGNED]" : "SOLAR SEAL PAD\n[STEP OR INTERACT]";
    sunLabel.color = SunAligned ? new Color(0.4f, 1.0f, 0.65f) : new Color(1.0f, 0.84f, 0.32f);
   }
   if (moonLabel) {
    moonLabel.text = MoonAligned ? "LUNAR SEAL PAD\n[ALIGNED]" : "LUNAR SEAL PAD\n[STEP OR INTERACT]";
    moonLabel.color = MoonAligned ? new Color(0.4f, 1.0f, 0.65f) : new Color(0.48f, 0.88f, 1.0f);
   }
   if (altarLabel) {
    altarLabel.text = Completed
     ? "CRYPT MEMORY ALTAR\n[FIRST WARDEN'S MEMORY AWAKENED]"
     : (SunAligned && MoonAligned)
      ? "CRYPT MEMORY ALTAR\n[PRESS INTERACT TO CLAIM MEMORY]"
      : "CRYPT MEMORY ALTAR\n[ALIGN SOLAR & LUNAR PADS]";
    altarLabel.color = Completed ? new Color(0.4f, 1.0f, 0.72f) : new Color(0.82f, 0.62f, 1.0f);
   }
   if (sunHalo) sunHalo.transform.localScale = new Vector3(SunAligned ? 1.85f : 1.35f, 0.02f, SunAligned ? 1.85f : 1.35f);
   if (moonHalo) moonHalo.transform.localScale = new Vector3(MoonAligned ? 1.85f : 1.35f, 0.02f, MoonAligned ? 1.85f : 1.35f);
   if (altarHalo) altarHalo.transform.localScale = new Vector3(Completed ? 2.1f : 1.45f, 0.02f, Completed ? 2.1f : 1.45f);
  }

  void Update() {
   if (orreryModel) {
    float speed = Completed ? 85f : (SunAligned || MoonAligned) ? 45f : 18f;
    orreryModel.Rotate(0f, speed * Time.deltaTime, 0f, Space.World);
   }

   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero || gm.Hero.Dead || gm.IsModalOpen) return;

   Vector3 hp = gm.Hero.transform.position;
   UpdateLabelVisibility(entranceLabel, hp, 20f);
   UpdateLabelVisibility(returnLabel, hp, 16f);
   UpdateLabelVisibility(sunLabel, hp, 14f);
   UpdateLabelVisibility(moonLabel, hp, 14f);
   UpdateLabelVisibility(altarLabel, hp, 16f);

   // Stepping onto Solar or Lunar Pad inside the Crypt automatically aligns it
   if (!SunAligned && Vector3.Distance(hp, sunHalo ? sunHalo.transform.position : SunPadPos) <= 2.3f) {
    gm.AlignCryptPad(0);
   }
   if (!MoonAligned && Vector3.Distance(hp, moonHalo ? moonHalo.transform.position : MoonPadPos) <= 2.3f) {
    gm.AlignCryptPad(1);
   }
  }

  static void UpdateLabelVisibility(TextMesh tm, Vector3 heroPos, float maxDist) {
   if (!tm) return;
   bool show = Vector3.Distance(heroPos, tm.transform.position) <= maxDist;
   if (tm.gameObject.activeSelf != show) tm.gameObject.SetActive(show);
   if (show && Camera.main) tm.transform.rotation = Camera.main.transform.rotation;
  }
 }

 // Stormveil Relay Tower & Conductor mechanism at the Stormveil Tempest Overlook.
 // Stepping on or interacting with the TurnPad rotates the RelayArm and activates the Storm Conduit.
 public sealed class OpenWorldStormRelay : MonoBehaviour {
  public static readonly Vector3 TowerPos = new Vector3(106f, 0f, -103f);
  public static Vector3 ConductorPos => OpenWorldRealm.Available ? OpenWorldRegions.StormRelay : new Vector3(98f, 0f, -95.5f);
  public static readonly Vector3 TurnPadPos = new Vector3(95.2f, 0f, -93.2f);

  Transform relayArm;
  TextMesh padLabel;
  GameObject conduitOrb;
  public bool Aligned { get; private set; }
  float targetYaw;

  public static OpenWorldStormRelay Build(OpenWorldManager gm) {
   var root = new GameObject("StormveilRelayMechanism");
   root.transform.SetParent(gm.WorldRoot, false);
   var relay = root.AddComponent<OpenWorldStormRelay>();
   relay.SpawnVisuals(gm, root.transform);
   return relay;
  }

  void SpawnVisuals(OpenWorldManager gm, Transform parent) {
   Vector3 tPos = TowerPos;
   tPos.y = OpenWorldMaterialHelper.SampleGroundHeight(tPos);
   OpenWorldMaterialHelper.SpawnModel("Stormveil/Tower", "Stormveil/Textures/Tower_basecolor",
    parent, tPos, 8.2f, Color.white, true);

   Vector3 cPos = ConductorPos;
   cPos.y = OpenWorldMaterialHelper.SampleGroundHeight(cPos);
   OpenWorldMaterialHelper.SpawnModel("Stormveil/Conductor", "Stormveil/Textures/Conductor_basecolor",
    parent, cPos, 3.6f, Color.white, true);

   var armGo = OpenWorldMaterialHelper.SpawnModel("Stormveil/RelayArm", "Stormveil/Textures/RelayArm_basecolor",
    parent, cPos + Vector3.up * 2.8f, 1.8f, Color.white, false);
   if (armGo) relayArm = armGo.transform;

   Vector3 pPos = TurnPadPos;
   pPos.y = OpenWorldMaterialHelper.SampleGroundHeight(pPos);
   OpenWorldMaterialHelper.SpawnModel("Stormveil/TurnPad", "Stormveil/Textures/TurnPad_basecolor",
    parent, pPos, 0.35f, Color.white, false);

   conduitOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
   conduitOrb.name = "StormConduitOrb";
   conduitOrb.transform.SetParent(parent, false);
   conduitOrb.transform.position = cPos + Vector3.up * 4.3f;
   conduitOrb.transform.localScale = Vector3.one * 0.55f;
   var col = conduitOrb.GetComponent<Collider>();
   if (col) {
    if (Application.isPlaying) Destroy(col);
    else DestroyImmediate(col);
   }
   Color sc = new Color(0.45f, 0.78f, 1.0f);
   conduitOrb.GetComponent<Renderer>().sharedMaterial =
    OpenWorldMaterialHelper.CreateLit("StormConduitOrb", sc, null, null, 0.92f, 0.3f, sc * 0.85f);

   var lgo = new GameObject("StormRelayLabel");
   lgo.transform.SetParent(parent, false);
   lgo.transform.position = pPos + Vector3.up * 1.85f;
   padLabel = lgo.AddComponent<TextMesh>();
   padLabel.fontSize = 30;
   padLabel.characterSize = 0.042f;
   padLabel.anchor = TextAnchor.MiddleCenter;
   padLabel.alignment = TextAlignment.Center;
   lgo.SetActive(false);

   ApplyState(false);
  }

  public void ApplyState(bool aligned) {
   Aligned = aligned;
   targetYaw = aligned ? 0f : 90f;
   if (relayArm) relayArm.rotation = Quaternion.Euler(0f, targetYaw, 0f);
   if (padLabel) {
    padLabel.text = aligned
     ? "STORMVEIL CONDUCTOR\n[CONDUIT ACTIVE: +12% SPEED]"
     : "STORMVEIL TURN PAD\n[STEP OR INTERACT TO ALIGN RELAY]";
    padLabel.color = aligned ? new Color(0.38f, 1.0f, 0.72f) : new Color(0.55f, 0.82f, 1.0f);
   }
   if (conduitOrb) {
    conduitOrb.transform.localScale = Vector3.one * (aligned ? 0.85f : 0.48f);
   }
  }

  void Update() {
   if (relayArm) {
    if (Aligned) relayArm.Rotate(0f, 65f * Time.deltaTime, 0f, Space.World);
    else relayArm.rotation = Quaternion.Slerp(relayArm.rotation, Quaternion.Euler(0f, targetYaw, 0f), 8f * Time.deltaTime);
   }
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero || gm.Hero.Dead || gm.IsModalOpen) return;

   float d = Vector3.Distance(gm.Hero.transform.position, padLabel ? padLabel.transform.position - Vector3.up * 1.85f : TurnPadPos);
   if (padLabel) {
    bool show = d <= 18f;
    if (padLabel.gameObject.activeSelf != show) padLabel.gameObject.SetActive(show);
    if (show && Camera.main) padLabel.transform.rotation = Camera.main.transform.rotation;
   }
   if (!Aligned && d <= 2.35f) {
    gm.ActivateStormRelay();
   }
  }
 }

 // Wuthering Waves / Zelda style high-altitude Aether Grapple Anchor.
 // Pressing Q (or Interact within range) zips Aster up to the vantage point and deploys the Glider.
 public sealed class OpenWorldGrappleAnchor : MonoBehaviour {
  public string AnchorTitle;

  TextMesh label;

  public static OpenWorldGrappleAnchor Spawn(string title, Vector3 worldPos, Transform parent) {
   var go = new GameObject("GrappleAnchor_" + title);
   if (parent) go.transform.SetParent(parent, false);
   go.transform.position = worldPos;

   var anchor = go.AddComponent<OpenWorldGrappleAnchor>();
   anchor.AnchorTitle = title;
   anchor.BuildVisuals();
   return anchor;
  }

  void BuildVisuals() {
   Color c = new Color(0.28f, 1.0f, 0.82f);
   OpenWorldGrappleVisual.Build(transform);

   var lgo = new GameObject("GrappleLabel");
   lgo.transform.SetParent(transform, false);
   lgo.transform.localPosition = new Vector3(0f, 1.8f, 0f);
   label = lgo.AddComponent<TextMesh>();
   label.text = $"AETHER ANCHOR: {AnchorTitle}";
   label.fontSize = 28;
   label.characterSize = 0.042f;
   label.anchor = TextAnchor.MiddleCenter;
   label.color = c;
   lgo.SetActive(false);
  }

  void Update() {

   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero || gm.Hero.Dead || gm.IsModalOpen) return;

   float d = Vector3.Distance(gm.Hero.transform.position, transform.position);
   if (label) {
    bool show = d <= 24f;
    if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
    if (show && Camera.main) label.transform.rotation = Camera.main.transform.rotation;
   }
  }
 }
}


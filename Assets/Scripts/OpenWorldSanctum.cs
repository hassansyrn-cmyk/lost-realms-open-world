using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 // Forgotten Sanctum open-world ruin encounter:
 // - 3 Elemental Focus Pillars (Ember, Frost, Gale) around the sunken crypt courtyard
 // - 2 active Sanctum Hazard Traps (Flame Brazier & Frost Totem) that disarm when the altar awakens
 // - 2 Sanctum Sentinel guards
 // - Awakening the Memory Altar after attuning all 3 foci grants +25 Max Aether, unlocks Frost Halberd,
 //   unseals the Forgotten Sanctum Cache, and persists across save/load.
 public sealed class OpenWorldSanctumFocus : MonoBehaviour {
  public int FocusIndex; // 0 Ember, 1 Frost, 2 Gale
  public string FocusTitle;
  public bool Attuned { get; private set; }
  GameObject auraBeacon;
  TextMesh label;

  static readonly Color[] FocusColors = {
   new Color(1f, 0.42f, 0.14f),
   new Color(0.28f, 0.86f, 1f),
   new Color(0.32f, 0.98f, 0.62f)
  };

  public static OpenWorldSanctumFocus Spawn(int index, Vector3 pos, Transform parent) {
   string[] models = { "Props/Expansion/EmberFocus", "Props/Expansion/FrostFocus", "Props/Expansion/GaleFocus" };
   string[] textures = {
    "Props/Expansion/Textures/EmberFocus_basecolor",
    "Props/Expansion/Textures/FrostFocus_basecolor",
    "Props/Expansion/Textures/GaleFocus_basecolor"
   };
   string[] titles = { "EMBER FOCUS", "FROST FOCUS", "GALE FOCUS" };

   pos.y = OpenWorldMaterialHelper.SampleGroundHeight(pos);
   var root = new GameObject("Sanctum_" + titles[index]);
   if (parent) root.transform.SetParent(parent, false);
   root.transform.position = pos;

   OpenWorldMaterialHelper.SpawnModel(models[index], textures[index], root.transform, pos, 2.15f, Color.white, true);

   var focus = root.AddComponent<OpenWorldSanctumFocus>();
   focus.FocusIndex = index;
   focus.FocusTitle = titles[index];
   focus.BuildVisuals();
   return focus;
  }

  void BuildVisuals() {
   Color c = FocusColors[Mathf.Clamp(FocusIndex, 0, 2)];
   auraBeacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
   auraBeacon.name = "FocusOrb";
   auraBeacon.transform.SetParent(transform, false);
   auraBeacon.transform.localPosition = new Vector3(0f, 2.35f, 0f);
   auraBeacon.transform.localScale = Vector3.one * 0.34f;
   var col = auraBeacon.GetComponent<Collider>();
   if (col) {
    if (Application.isPlaying) Destroy(col);
    else DestroyImmediate(col);
   }

   var lgo = new GameObject("FocusLabel");
   lgo.transform.SetParent(transform, false);
   lgo.transform.localPosition = new Vector3(0f, 2.85f, 0f);
   label = lgo.AddComponent<TextMesh>();
   label.fontSize = 30;
   label.characterSize = 0.042f;
   label.anchor = TextAnchor.MiddleCenter;

   ApplyState(false);
  }

  public void ApplyState(bool attuned) {
   Attuned = attuned;
   Color c = FocusColors[Mathf.Clamp(FocusIndex, 0, 2)];
   if (auraBeacon) {
    auraBeacon.SetActive(true);
    auraBeacon.transform.localScale = Vector3.one * (attuned ? 0.44f : 0.24f);
    Color bodyCol = attuned ? c : new Color(0.35f, 0.38f, 0.42f);
    Color emCol = attuned ? c * 0.75f : Color.clear;
    auraBeacon.GetComponent<Renderer>().sharedMaterial =
     OpenWorldMaterialHelper.CreateLit($"FocusOrb_{FocusIndex}_{attuned}", bodyCol, null, null, 0.85f, 0.2f, emCol);
   }
   if (label) {
    label.text = FocusTitle + (attuned ? " [ATTUNED]" : " [DORMANT]");
    label.color = attuned ? c : new Color(0.78f, 0.82f, 0.86f);
   }
  }

  void Update() {
   if (auraBeacon) {
    float pulse = Attuned ? 0.42f + Mathf.Sin(Time.time * 4f + FocusIndex) * 0.05f : 0.24f;
    auraBeacon.transform.localScale = Vector3.one * pulse;
    auraBeacon.transform.localPosition = new Vector3(0f, 2.35f + Mathf.Sin(Time.time * 2.5f + FocusIndex) * 0.08f, 0f);
   }
   if (label && OpenWorldManager.I && OpenWorldManager.I.Hero) {
    float d = Vector3.Distance(OpenWorldManager.I.Hero.transform.position, transform.position);
    bool show = d <= 28f;
    if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
    if (show && Camera.main) label.transform.rotation = Camera.main.transform.rotation;
   }
  }
 }

 public sealed class OpenWorldSanctumTrap : MonoBehaviour {
  public bool IsFrost;
  public bool Disarmed { get; private set; }
  public float Radius = 2.2f;
  float timer;
  bool telegraphing;
  GameObject warningRing;

  public static OpenWorldSanctumTrap Spawn(bool frost, Vector3 pos, Transform parent) {
   pos.y = OpenWorldMaterialHelper.SampleGroundHeight(pos);
   string model = frost ? "Props/Traps/Trap_Totem" : "Props/Traps/Trap_Brazier";
   string tex = frost ? "Props/Traps/Textures/Trap_Totem_0" : "Props/Traps/Textures/Trap_Brazier_0";
   float h = frost ? 2.1f : 1.15f;

   var root = new GameObject(frost ? "Sanctum_FrostTotem" : "Sanctum_FlameBrazier");
   if (parent) root.transform.SetParent(parent, false);
   root.transform.position = pos;

   OpenWorldMaterialHelper.SpawnModel(model, tex, root.transform, pos, h, Color.white, true);
   var trap = root.AddComponent<OpenWorldSanctumTrap>();
   trap.IsFrost = frost;
   trap.timer = frost ? 1.6f : 0.4f;
   return trap;
  }

  public void SetDisarmed(bool disarmed) {
   Disarmed = disarmed;
   if (disarmed && warningRing) warningRing.SetActive(false);
   telegraphing = false;
  }

  void Update() {
   if (Disarmed) return;
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero || gm.Hero.Dead || gm.IsModalOpen) return;

   float distToHero = Vector3.Distance(transform.position, gm.Hero.transform.position);
   if (distToHero > 28f) {
    if (warningRing && warningRing.activeSelf) warningRing.SetActive(false);
    telegraphing = false;
    return;
   }

   timer += Time.deltaTime;
   if (!telegraphing && timer >= 2.3f) {
    telegraphing = true;
    EnsureWarningRing();
    warningRing.SetActive(true);
   }
   if (timer >= 3.2f) {
    timer = 0f;
    telegraphing = false;
    if (warningRing) warningRing.SetActive(false);
    Erupt(gm);
   }
  }

  void EnsureWarningRing() {
   if (warningRing) return;
   warningRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
   warningRing.name = "TrapTelegraph";
   warningRing.transform.SetParent(transform, false);
   warningRing.transform.localPosition = new Vector3(0f, 0.04f, 0f);
   warningRing.transform.localScale = new Vector3(Radius * 2f, 0.015f, Radius * 2f);
   var col = warningRing.GetComponent<Collider>();
   if (col) {
    if (Application.isPlaying) Destroy(col);
    else DestroyImmediate(col);
   }
   Color c = IsFrost ? new Color(0.25f, 0.78f, 1f) : new Color(1f, 0.35f, 0.12f);
   warningRing.GetComponent<Renderer>().sharedMaterial =
    OpenWorldMaterialHelper.CreateLit("TrapRing_" + IsFrost, c, null, null, 0.2f, 0f, c * 0.55f);
  }

  void Erupt(OpenWorldManager gm) {
   Color burstCol = IsFrost ? new Color(0.35f, 0.88f, 1f) : new Color(1f, 0.42f, 0.14f);
   gm.SpawnBurst(transform.position + Vector3.up * 0.8f, burstCol, 16);
   if (Vector3.Distance(transform.position, gm.Hero.transform.position) <= Radius) {
    gm.Hero.TakeDamage(1f, transform);
   }
  }
 }
}

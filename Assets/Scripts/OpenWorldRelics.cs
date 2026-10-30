using UnityEngine;

namespace LostRealmsOW {
 // 4 Discoverable Ancient Relic Shrines using the migrated 3D Tripo Relic models
 // (DawnSigil, EmberSeal, FrostEmblem, Skyglass). Attuning a relic grants a permanent
 // passive blessing (Witcher 3 Place of Power / Genshin Impact Shrine style) and persists in save data.
 public sealed class OpenWorldRelicShrine : MonoBehaviour {
  public int RelicIndex; // 0 DawnSigil, 1 EmberSeal, 2 FrostEmblem, 3 Skyglass
  public string RelicName;
  public string BlessingSummary;
  public bool Attuned { get; private set; }

  Transform relicModel;
  GameObject haloOrb;
  TextMesh label;
  float baseRelicY;

  static readonly (string model, string tex, string title, string blessing, Color color)[] Defs = {
   ("Relics/DawnSigil", "Relics/Textures/DawnSigil_basecolor", "DAWN SIGIL", "+25% Potion Healing & Sanctuary Regeneration", new Color(1.0f, 0.86f, 0.36f)),
   ("Relics/EmberSeal", "Relics/Textures/EmberSeal_basecolor", "EMBER SEAL", "+20% Charged Attack & Elemental Reaction Damage", new Color(1.0f, 0.45f, 0.16f)),
   ("Relics/FrostEmblem", "Relics/Textures/FrostEmblem_basecolor", "FROST EMBLEM", "15% Damage Reduction & Extended Parry Window", new Color(0.32f, 0.88f, 1.0f)),
   ("Relics/Skyglass", "Relics/Textures/Skyglass_basecolor", "CELESTIAL SKYGLASS", "+20% Glider Speed & +1.5/s Aether Regen", new Color(0.35f, 1.0f, 0.76f))
  };

  public static OpenWorldRelicShrine Spawn(int index, Vector3 position, Transform parent) {
   index = Mathf.Clamp(index, 0, Defs.Length - 1);
   var def = Defs[index];
   position.y = OpenWorldMaterialHelper.SampleGroundHeight(position);

   var root = new GameObject("RelicShrine_" + def.title);
   if (parent) root.transform.SetParent(parent, false);
   root.transform.position = position;

   // Stone pedestal base
   OpenWorldMaterialHelper.SpawnModel("Props/Expansion/RitualAltar", "Props/Expansion/Textures/RitualAltar_basecolor",
    root.transform, position, 1.25f, Color.white, true);

   // Floating 3D Tripo Relic above pedestal
   Vector3 relicPos = position + Vector3.up * 1.65f;
   var rgo = OpenWorldMaterialHelper.SpawnModel(def.model, def.tex, root.transform, relicPos, 0.85f, Color.white, false);

   var shrine = root.AddComponent<OpenWorldRelicShrine>();
   shrine.RelicIndex = index;
   shrine.RelicName = def.title;
   shrine.BlessingSummary = def.blessing;
   if (rgo) {
    shrine.relicModel = rgo.transform;
    shrine.baseRelicY = rgo.transform.localPosition.y;
   }
   shrine.BuildVisuals(def.color);
   return shrine;
  }

  void BuildVisuals(Color c) {
   haloOrb = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
   haloOrb.name = "RelicRing";
   haloOrb.transform.SetParent(transform, false);
   haloOrb.transform.localPosition = new Vector3(0f, 1.32f, 0f);
   haloOrb.transform.localScale = new Vector3(1.1f, 0.02f, 1.1f);
   var col = haloOrb.GetComponent<Collider>();
   if (col) {
    if (Application.isPlaying) Destroy(col);
    else DestroyImmediate(col);
   }
   haloOrb.GetComponent<Renderer>().sharedMaterial =
    OpenWorldMaterialHelper.CreateLit("RelicHalo_" + RelicIndex, c, null, null, 0.88f, 0.25f, c * 0.65f);

   var lgo = new GameObject("RelicLabel");
   lgo.transform.SetParent(transform, false);
   lgo.transform.localPosition = new Vector3(0f, 2.75f, 0f);
   label = lgo.AddComponent<TextMesh>();
   label.fontSize = 30;
   label.characterSize = 0.04f;
   label.anchor = TextAnchor.MiddleCenter;
   label.gameObject.SetActive(false);

   ApplyState(false);
  }

  public void ApplyState(bool attuned) {
   Attuned = attuned;
   var def = Defs[Mathf.Clamp(RelicIndex, 0, Defs.Length - 1)];
   if (label) {
    label.text = def.title + (attuned ? "\n[BLESSING ACTIVE]" : "\n[APPROACH TO ATTUNE]");
    label.color = attuned ? def.color : new Color(0.95f, 0.92f, 0.78f);
   }
   if (haloOrb) {
    haloOrb.transform.localScale = new Vector3(attuned ? 1.35f : 0.95f, 0.02f, attuned ? 1.35f : 0.95f);
   }
  }

  void Update() {
   if (relicModel) {
    relicModel.Rotate(0f, (Attuned ? 95f : 55f) * Time.deltaTime, 0f, Space.World);
    Vector3 lp = relicModel.localPosition;
    lp.y = baseRelicY + Mathf.Sin(Time.time * 2.4f + RelicIndex) * 0.10f;
    relicModel.localPosition = lp;
   }

   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero || gm.Hero.Dead || gm.IsModalOpen) return;

   float d = Vector3.Distance(gm.Hero.transform.position, transform.position);
   if (label) {
    bool show = d <= 20f;
    if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
    if (show && Camera.main) label.transform.rotation = Camera.main.transform.rotation;
   }

   if (!Attuned && d <= 2.35f) {
    gm.AttuneRelic(RelicIndex);
   }
  }
 }
}

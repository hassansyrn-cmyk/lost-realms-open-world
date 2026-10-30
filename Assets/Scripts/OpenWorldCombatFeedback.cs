using UnityEngine;

namespace LostRealmsOW {
 // Floating world-space damage numbers, elemental reaction callouts ("MELT!", "SWIRL!"),
 // Wuthering Waves "EXTREME EVASION!" popups, and Zelda-style Gale Updraft Vents.
 public sealed class OpenWorldCombatPopup : MonoBehaviour {
  TextMesh label;
  Vector3 velocity;
  float elapsed;
  float duration = 0.95f;
  Color baseColor;

  public static void Spawn(Vector3 worldPos, string text, Color color, float scale = 1f) {
   if (OpenWorldMobileHud.Instance) { OpenWorldMobileHud.SpawnCombatPopup(worldPos, text, color, scale); return; }
   var go = new GameObject("OW_CombatPopup");
   if (OpenWorldManager.I && OpenWorldManager.I.WorldRoot) {
    go.transform.SetParent(OpenWorldManager.I.WorldRoot, false);
   }
   Vector2 jitter = Random.insideUnitCircle * 0.35f;
   go.transform.position = worldPos + new Vector3(jitter.x, 0.25f, jitter.y);

   var tm = go.AddComponent<TextMesh>();
   tm.text = text;
   tm.fontSize = Mathf.RoundToInt(36f * Mathf.Clamp(scale, 0.8f, 1.6f));
   tm.characterSize = 0.042f;
   tm.anchor = TextAnchor.MiddleCenter;
   tm.fontStyle = FontStyle.Bold;
   tm.color = color;

   var popup = go.AddComponent<OpenWorldCombatPopup>();
   popup.label = tm;
   popup.baseColor = color;
   popup.velocity = new Vector3(jitter.x * 0.4f, 1.85f * scale, jitter.y * 0.4f);
  }

  void Update() {
   elapsed += Time.deltaTime;
   if (elapsed >= duration) {
    Destroy(gameObject);
    return;
   }
   transform.position += velocity * Time.deltaTime;
   velocity.y = Mathf.Max(0.4f, velocity.y - 1.6f * Time.deltaTime);
   if (Camera.main) {
    transform.rotation = Camera.main.transform.rotation;
   }
   if (label) {
    float alpha = 1f - Mathf.SmoothStep(0.45f, 1f, elapsed / duration);
    label.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
   }
  }
 }

 // Zelda BotW / Genshin style Gale Updraft Vent that launches Aster skyward
 // and deploys her Aether Windwings Glider to reach floating Sky Islands.
 public sealed class OpenWorldUpdraft : MonoBehaviour {
  public string UpdraftTitle = "GALE UPDRAFT";
  public float Radius = 2.8f;
  public float LaunchVelocity = 21.5f;
  float nextLaunchTime;
  Transform ringOuter, ringInner;

  public static OpenWorldUpdraft Spawn(string title, Vector3 pos, Transform parent, float launchVel = 21.5f) {
   pos.y = OpenWorldMaterialHelper.SampleGroundHeight(pos);
   var root = new GameObject("Updraft_" + title);
   if (parent) root.transform.SetParent(parent, false);
   root.transform.position = pos;

   // Base wind grate / stone ring
   OpenWorldMaterialHelper.SpawnModel("Props/Expansion/WindGrate", "Props/Expansion/Textures/WindGrate_basecolor",
    root.transform, pos, 0.35f, new Color(0.55f, 0.95f, 0.78f), false);

   var updraft = root.AddComponent<OpenWorldUpdraft>();
   updraft.UpdraftTitle = title;
   updraft.LaunchVelocity = launchVel;
   updraft.BuildWindVortex();
   return updraft;
  }

  void BuildWindVortex() {
   Color galeCol = new Color(0.32f, 1.0f, 0.72f);
   ringOuter = CreateWindRing("WindRingOuter", 1.1f, 2.2f, 0.04f, galeCol);
   ringInner = CreateWindRing("WindRingInner", 2.6f, 1.5f, 0.035f, new Color(0.55f, 1.0f, 0.88f));

   var lgo = new GameObject("UpdraftLabel");
   lgo.transform.SetParent(transform, false);
   lgo.transform.localPosition = new Vector3(0f, 3.6f, 0f);
   var tm = lgo.AddComponent<TextMesh>();
   tm.text = UpdraftTitle + "\n[STEP IN TO GLIDE]";
   tm.fontSize = 28;
   tm.characterSize = 0.04f;
   tm.anchor = TextAnchor.MiddleCenter;
   tm.color = galeCol;
  }

  Transform CreateWindRing(string name, float height, float diameter, float thickness, Color c) {
   var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
   cyl.name = name;
   cyl.transform.SetParent(transform, false);
   cyl.transform.localPosition = new Vector3(0f, height, 0f);
   cyl.transform.localScale = new Vector3(diameter, thickness, diameter);
   var col = cyl.GetComponent<Collider>();
   if (col) {
    if (Application.isPlaying) Destroy(col);
    else DestroyImmediate(col);
   }
   cyl.GetComponent<Renderer>().sharedMaterial =
    OpenWorldMaterialHelper.CreateLit(name, c, null, null, 0.85f, 0.1f, c * 0.65f);
   return cyl.transform;
  }

  void Update() {
   if (ringOuter) {
    ringOuter.Rotate(0f, 140f * Time.deltaTime, 0f, Space.Self);
    ringOuter.localPosition = new Vector3(0f, 1.0f + Mathf.Sin(Time.time * 3f) * 0.25f, 0f);
   }
   if (ringInner) {
    ringInner.Rotate(0f, -190f * Time.deltaTime, 0f, Space.Self);
    ringInner.localPosition = new Vector3(0f, 2.4f + Mathf.Cos(Time.time * 3.5f) * 0.3f, 0f);
   }

   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero || gm.Hero.Dead || gm.IsModalOpen) return;

   var labelT = transform.Find("UpdraftLabel");
   if (labelT) {
    float d = Vector3.Distance(gm.Hero.transform.position, transform.position);
    labelT.gameObject.SetActive(d <= 18f);
    if (d <= 18f && Camera.main) labelT.rotation = Camera.main.transform.rotation;
   }

   if (gm.Hero.IsMounted || Time.time < nextLaunchTime) return;
   Vector2 heroXZ = new Vector2(gm.Hero.transform.position.x, gm.Hero.transform.position.z);
   Vector2 myXZ = new Vector2(transform.position.x, transform.position.z);
   float dy = gm.Hero.transform.position.y - transform.position.y;
   if (Vector2.Distance(heroXZ, myXZ) <= Radius && dy >= -1.2f && dy <= 5.5f) {
    nextLaunchTime = Time.time + 1.1f;
    gm.Hero.LaunchIntoGlide(LaunchVelocity);
    OpenWorldAudio.Play("sfx_gale_cast", 0.65f);
    gm.SpawnBurst(gm.Hero.transform.position + Vector3.up * 0.5f, new Color(0.35f, 1f, 0.75f), 20);
    gm.ShowBanner("GALE UPDRAFT! Press Space / Jump in midair to open or fold the paraglider!");
   }
  }
 }
}

using System;
using System.Linq;
using UnityEngine;

namespace LostRealmsOW {
 public sealed class OpenWorldHorseCoat : MonoBehaviour {
  void Start() {
   var a = GetComponent<OpenWorldAnimalInteraction>();
   if (a && a.Horse) OpenWorldHorseProgression.ApplyCoat(a);
  }
 }

 // Horse ownership, the Verdant stables and the wild-horse taming challenge.
 //
 // Taming is a screen-space "gentling" challenge rather than a left/right counter:
 //  - A ring appears at a random point of the screen and shrinks quickly until it vanishes.
 //    Tap it before it disappears to calm the horse.
 //  - Each calm advances trust. An expired ring is a miss; three misses and the horse bolts
 //    (with the same 45 s retry gate as before).
 //  - The horse visibly fights the whole time: it bucks when a ring appears, settles into a
 //    graze when a ring is tapped and bucks again after a miss. On success it calms, and the
 //    saddled owned horse replaces it.
 //  - Handling skill (one point per successful tame, capped at 5) makes the challenge easier:
 //    fewer rings are needed and each ring lives longer, starts larger and leaves longer gaps.
 public sealed class OpenWorldHorseProgression : MonoBehaviour {
  public struct BreedSpec {
   public string Name;
   public int Price, Stamina, Trials;
   public float Speed;
   public Color Coat;
   public BreedSpec(string name, int price, float speed, int stamina, int trials, Color coat) {
    Name = name; Price = price; Speed = speed; Stamina = stamina; Trials = trials; Coat = coat;
   }
  }

  public static readonly BreedSpec[] Breeds = {
   new BreedSpec("Bay Road Horse", 350, 8.2f, 100, 8, new Color(.75f, .48f, .30f)),
   new BreedSpec("Midnight Courser", 900, 11f, 80, 12, new Color(.24f, .27f, .32f)),
   new BreedSpec("Golden Highlander", 700, 9.2f, 150, 10, new Color(1.3f, 1.12f, .72f))
  };

  // One shrinking ring. Pos is normalised (0..1) inside the play field; the HUD maps it to
  // pixels so the same round reads correctly on any aspect ratio. All times are unscaled.
  public struct TameBeat {
   public Vector2 Pos;
   public float Start, Life, Diameter;
   public bool Active;
  }

  public const int MistakeLimit = 3;
  const float RetryDelay = 45f;
  const float OpeningPause = .65f;
  const float AfterHitPause = .42f;
  const float AfterMissPause = .62f;
  const float SuccessHold = 2.4f;
  const float GraceWindow = .06f;

  static OpenWorldHorseProgression instance;
  public static OpenWorldHorseProgression Instance => instance ? instance : (instance = FindAnyObjectByType<OpenWorldHorseProgression>());
  public static readonly Vector3 Stable = new Vector3(-36, 0, 8);

  public OpenWorldAnimalInteraction Target;
  public int Successes, Mistakes;
  public TameBeat Beat;
  public int BeatSerial { get; private set; }
  public bool BeatActive => Beat.Active;
  public float BeatProgress => Beat.Active ? Mathf.Clamp01((Time.unscaledTime - Beat.Start) / Mathf.Max(.05f, Beat.Life)) : 0f;
  public float RoundStartedAt { get; private set; }
  public int TameState { get; private set; } // 0 playing, 1 tamed and showing the result card
  public float TameStateAt { get; private set; }

  int roundRequired;
  float retryAt, nextBeatAt;
  bool firstBeat;
  GameObject stall;
  Material keeperMaterial;
  public bool ShopOpen {get;private set;}
  public OpenWorldNPC Stablekeeper {get;private set;}
  public void CloseShop()=>ShopOpen=false;
  public bool OpenShop(){if(!GM||!GM.Hero||GM.IsModalOpen||GM.Hero.Dead||GM.Hero.IsMounted||Target||!Stablekeeper||Vector3.Distance(GM.Hero.transform.position,Stablekeeper.transform.position)>3.8f)return false;ShopOpen=true;return true;}
  void BuildDisplay(int breed,Vector3 at){
   OpenWorldMaterialHelper.SpawnModel("Props/Village/rpgpp_lt_awning_standing_01a", "Props/Textures/rpgpp_lt_tex_a", stall.transform, Ground(at), 3.1f, Color.white, true);
   var root=new GameObject("Stable display "+Breeds[breed].Name);root.transform.SetParent(stall.transform,false);root.transform.position=Ground(at);
   var model=Instantiate(Resources.Load<GameObject>("World/Stables/Horse"),root.transform);var b=OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);model.transform.localScale*=1.82f/Mathf.Max(.1f,b.size.y);b=OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);model.transform.position+=root.transform.position-new Vector3(b.center.x,b.min.y,b.center.z);
   model.GetComponentInChildren<OpenWorldImportedModel>().State=breed==1?"graze":"idle";Tint(model,breed);
   var solid=root.AddComponent<CapsuleCollider>();solid.height=1.7f;solid.radius=.65f;solid.center=Vector3.up*.85f;
  }
  OpenWorldAnimalInteraction personal;
  OpenWorldWildlife restrained;
  OpenWorldImportedModel motion;

  // Procedural resistance. The wild Horse_001 prefab carries idle/walk/run/eat clips but no
  // buck, so the whole body rears, sways and stamps while the player works the rings.
  Transform body;
  Vector3 bodyHome;
  Quaternion bodyFacing;
  float rear, rearVel, calm;

  OpenWorldManager GM => OpenWorldManager.I ? OpenWorldManager.I : FindAnyObjectByType<OpenWorldManager>();

  // One point per successful tame, capped, so every horse the player gentles makes the next
  // one a little easier even when it is the same breed.
  public int Skill => GM ? Mathf.Min(5, GM.CollectedWorldIds.Count(f => f.StartsWith("horse-skill:"))) : 0;
  public int Required => Target ? roundRequired : 0;

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Install() { new GameObject("Horse ownership and stables").AddComponent<OpenWorldHorseProgression>(); }

  void Awake() { instance = this; }

  public static int BreedFor(string id) {
   unchecked {
    uint h = 2166136261;
    foreach (char c in id ?? "") h = (h ^ c) * 16777619;
    return (int)(h % 3);
   }
  }

  // Ring life: a little over a second with no skill, tightening slightly as the round
  // advances and opening up as handling improves. Rings start bigger with skill as well.
  float BeatLife => (1.30f + Skill * .12f) * Mathf.Lerp(1f, .86f, roundRequired > 0 ? Mathf.Clamp01((float)Successes / roundRequired) : 0f);
  float BeatDiameter => 216f + Skill * 12f;
  public float BeatSize => BeatDiameter;

  public static void Begin(OpenWorldAnimalInteraction horse) {
   var p = Instance;
   if (!p || p.Target || Time.unscaledTime < p.retryAt || !horse || horse.Owned) return;
   p.Target = horse;
   p.Successes = p.Mistakes = 0;
   p.TameState = 0;
   p.roundRequired = Mathf.Max(5, Breeds[horse.Breed].Trials - p.Skill);
   p.RoundStartedAt = Time.unscaledTime;
   p.motion = horse.GetComponentInChildren<OpenWorldImportedModel>();
   p.restrained = horse.GetComponent<OpenWorldWildlife>();
   if (p.restrained) p.restrained.enabled = false;
   var animator = horse.GetComponentInChildren<Animator>();
   if (animator) animator.applyRootMotion = false;
   p.body = horse.transform.childCount > 0 ? horse.transform.GetChild(0) : null;
   if (p.body) { p.bodyHome = p.body.localPosition; p.bodyFacing = p.body.localRotation; }
   p.rear = 1f; p.rearVel = 0f; p.calm = 0f; p.firstBeat = true;
   p.Beat = new TameBeat { Pos = new Vector2(.5f, .52f) };
   p.BeatSerial++;
   p.nextBeatAt = Time.unscaledTime + OpeningPause;
   p.PlayHorse("buck");
   p.GM?.ShowBanner("A wild horse shies away • Gentle her with a steady hand");
  }

  // The player touched the ring. A tap right on the last visible frame still counts. Public
  // and ring-independent so the editor validation can resolve a whole round deterministically.
  public void Tap() {
   if (!Target || TameState != 0) return;
   // A ring is consumed by the first tap: extra pointers landing on the same ring in the same
   // frame must not score again, and a ring that already vanished cannot be collected.
   if (!Beat.Active) return;
   if (Time.unscaledTime > Beat.Start + Beat.Life + GraceWindow) return;
   Beat.Active = false;
   Successes++;
   calm = 1f; rear *= .2f; rearVel = 0f;
   PlayHorse("graze");
   OpenWorldAudio.Play("sfx_gem", .30f, 1.12f, 1);
   if (Successes >= roundRequired) { Succeed(); return; }
   nextBeatAt = Time.unscaledTime + AfterHitPause + Skill * .02f;
  }

  // An expired (or forfeited) ring. Public so the editor validation can exercise the fail path.
  public void Miss() {
   if (!Target || TameState != 0) return;
   Beat.Active = false;
   Mistakes++;
   rear = 1.15f; rearVel = 0f; calm = 0f;
   PlayHorse("buck");
   OpenWorldAudio.Play("sfx_power_fail", .32f, .92f, 1);
   if (Mistakes >= MistakeLimit) { Fail(); return; }
   nextBeatAt = Time.unscaledTime + AfterMissPause;
  }

  public void Cancel() { if (Target) Fail(); }

  void Succeed() {
   int breed = Target.Breed;
   GM.CollectedWorldIds.Add("horse-owned:" + breed);
   // Unique per tame, so repeated captures of the same breed also raise handling.
   GM.CollectedWorldIds.Add("horse-skill:" + breed + ":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
   Select(breed);
   Beat.Active = false;
   PlayHorse("graze");
   TameState = 1;
   TameStateAt = Time.unscaledTime;
   GM.MarkProgressDirty();
   GM.SaveAdventure();
   OpenWorldAudio.Play("sfx_complete", .5f, 1f, 2);
   GM.ShowBanner("TAMED • " + Breeds[breed].Name + " • Handling improved");
  }

  // After the result card has been up for a moment, swap the wild horse for the saddled
  // owned horse at the same spot.
  void FinishTame() {
   var old = Target;
   int breed = old ? old.Breed : 0;
   Vector3 at = old ? old.transform.position : Stable;
   Release();
   if (personal) Destroy(personal.gameObject);
   personal = Spawn(breed, at);
   if (old) old.gameObject.SetActive(false);
   TameState = 0;
  }

  void PlayHorse(string state) {
   if (!motion) return;
   if (state == "buck" && !motion.Buck) state = "idle";
   if (state == "graze" && !motion.Graze) state = "idle";
   motion.State = state;
   motion.RestartVersion++;
  }

  // Rearing, swaying and stamping, carried by the body root so it works on the wild horse
  // prefab, which has no buck clip. Values are restored the moment the round ends.
  void Animate(float dt) {
   if (!body) return;
   rear = Mathf.SmoothDamp(rear, 0f, ref rearVel, .32f);
   calm = Mathf.MoveTowards(calm, 0f, dt * 1.5f);
   float t = Time.unscaledTime;
   float pitch = -rear * 12f + calm * 3.4f + Mathf.Sin(t * 1.7f) * .35f;
   float roll = Mathf.Sin(t * 2.3f) * (.4f + rear * 2.2f);
   float yaw = Mathf.Sin(t * .9f) * (rear * 1.6f);
   body.localRotation = bodyFacing * Quaternion.Euler(pitch, yaw, roll);
   float stamp = Mathf.Abs(Mathf.Sin(t * 8.6f)) * .045f * rear;
   body.localPosition = bodyHome + Vector3.up * (rear * .05f + stamp);
  }

  void SpawnBeat() {
   BeatSerial++;
   Beat.Active = true;
   Beat.Start = Time.unscaledTime;
   Beat.Life = BeatLife;
   Beat.Diameter = BeatDiameter;
   Beat.Pos = firstBeat ? new Vector2(.5f, .52f) : PickPoint();
   firstBeat = false;
   rear = Mathf.Max(rear, .55f);
   calm = 0f;
   PlayHorse("buck");
  }

  // Random points across the middle of the screen, never on a control cluster. The first ring
  // is centred so a new player learns the tap before the placement starts moving around.
  Vector2 PickPoint() {
   float bestGap = -1f;
   var best = new Vector2(.5f, .52f);
   for (int i = 0; i < 24; i++) {
    var p = new Vector2(UnityEngine.Random.Range(.14f, .86f), UnityEngine.Random.Range(.28f, .68f));
    if (InControlCluster(p)) continue;
    float gap = Vector2.Distance(p, Beat.Pos);
    if (gap > bestGap) { bestGap = gap; best = p; }
    if (gap >= .28f) return p;
   }
   return best;
  }

  // Normalised screen space: stick (bottom left), action wheel (bottom right), minimap and
  // quest tracker (top left), top-right icon row. Rings landing there read as clutter.
  static bool InControlCluster(Vector2 p) {
   if (p.x < .33f && p.y < .36f) return true;
   if (p.x > .65f && p.y < .47f) return true;
   if (p.x < .30f && p.y > .60f) return true;
   if (p.x > .70f && p.y > .72f) return true;
   return false;
  }

  void Release() {
   if (motion) { motion.State = "idle"; motion.RestartVersion++; }
   if (body) { body.localPosition = bodyHome; body.localRotation = bodyFacing; }
   if (restrained) restrained.enabled = true;
   body = null;
   restrained = null;
   motion = null;
   rear = calm = 0f;
   Beat.Active = false;
   Target = null;
  }

  void Fail() {
   var horse = Target;
   TameState = 0;
   Release();
   retryAt = Time.unscaledTime + RetryDelay;
   if (horse) {
    var wild = horse.GetComponent<OpenWorldWildlife>();
    if (wild && GM && GM.Hero) wild.Startle(GM.Hero.transform.position);
   }
   GM?.ShowBanner("The horse breaks away • Give her time before trying again");
  }

  void Select(int breed) {
   GM.CollectedWorldIds.RemoveWhere(f => f.StartsWith("horse-active:"));
   GM.CollectedWorldIds.Add("horse-active:" + breed);
  }

  public bool Buy(int breed) {
   if (!GM || !GM.Hero || GM.IsModalOpen || GM.Hero.Dead || GM.Hero.IsMounted || Target || breed < 0 || breed >= 3 || Vector3.Distance(GM.Hero.transform.position, Ground(Stable)) > 9) return false;
   bool owned = GM.CollectedWorldIds.Contains("horse-owned:" + breed);
   if (!owned && GM.Gold < Breeds[breed].Price) { GM.ShowBanner("Not enough gold for this horse"); return false; }
   var next = Spawn(breed, Ground(Stable) + new Vector3(3,0,7));
   if (!next) return false;
   if (!owned) { GM.Gold -= Breeds[breed].Price; GM.CollectedWorldIds.Add("horse-owned:" + breed); }
   if (personal) Destroy(personal.gameObject);
   personal = next;
   Select(breed);
   GM.MarkProgressDirty();
   GM.SaveAdventure();
   return true;
  }

  Vector3 Ground(Vector3 p) {
   p.y = GM.ActiveTerrain.SampleHeight(p) + GM.ActiveTerrain.transform.position.y;
   return p;
  }

  OpenWorldAnimalInteraction Spawn(int breed, Vector3 at) {
   var prefab = Resources.Load<GameObject>("World/Stables/Horse");
   if (!prefab || !prefab.GetComponentInChildren<OpenWorldImportedModel>()) { Debug.LogError("Stable horse lacks prepared animation driver"); return null; }
   var root = new GameObject("Owned " + Breeds[breed].Name);
   root.transform.SetParent(GM.WorldRoot, false);
   root.transform.position = Ground(at);
   var model = Instantiate(prefab, root.transform);
   var b = OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
   model.transform.localScale *= 1.82f / Mathf.Max(.1f, b.size.y);
   b = OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
   model.transform.position += root.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
   var wild = root.AddComponent<OpenWorldWildlife>();
   wild.Nature = OpenWorldWildlife.Temper.Tame;
   wild.HomeRadius = 4;
   var horse = OpenWorldAnimalInteraction.Attach(root, "Horse", "owned-horse:" + breed);
   horse.Breed = breed;
   horse.SpeciesName = Breeds[breed].Name;
   horse.HorseStamina = Breeds[breed].Stamina;
   ApplyCoat(horse);
   return horse;
  }

  public static void ApplyCoat(OpenWorldAnimalInteraction horse) => Tint(horse.gameObject,horse.Breed);
  static void Tint(GameObject model,int breed) {
   foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>()) {
    if (r.name.IndexOf("Saddle", StringComparison.OrdinalIgnoreCase) >= 0 || r.name.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0) continue;
    var block = new MaterialPropertyBlock();
    r.GetPropertyBlock(block);
    block.SetColor("_BaseColor", Breeds[breed].Coat);
    r.SetPropertyBlock(block);
   }
  }

  void Update() {
   if (!GM || !GM.Hero || !GM.ActiveTerrain) return;
   if (!stall) {
    stall = new GameObject("Verdant stables");
    stall.transform.SetParent(GM.WorldRoot, false);
    stall.transform.position = Ground(Stable);

    var board = new GameObject("Stable ledger");
    board.transform.SetParent(stall.transform, false);
    board.transform.localPosition = new Vector3(3, 0, 0);
    board.transform.localRotation = Quaternion.Euler(0, -90, 0);
    OpenWorldNoticeBoard.Build(board.transform, "VERDANT STABLES", "Road Horse • 350 gold\nCourser • 900 gold\nHighlander • 700 gold\n\nTamed horses welcome");
    for(int i=0;i<3;i++)BuildDisplay(i,Stable+new Vector3(-7+i*3.6f,0,0));
    var post=Ground(Stable+new Vector3(0,0,5));Stablekeeper=OpenWorldNPC.Spawn("stablekeeper","Stablemaster Elian","Horse Breeder & Trainer","A steady hand earns a loyal companion.",2,-1,post,post,GM.WorldRoot);GM.NPCs.Add(Stablekeeper);
    var keeperModel=Resources.Load<GameObject>("World/NPCs/Stablekeeper");if(keeperModel){Stablekeeper.Visual.AdoptImportedModel(keeperModel,1.8f);keeperMaterial=OpenWorldMaterialHelper.CreateLit("Elian craftsman clothing",Color.white,Resources.Load<Texture2D>("World/BlacksmithShop/Textures/BromClothing"));foreach(var r in Stablekeeper.Visual.ModelInstance.GetComponentsInChildren<SkinnedMeshRenderer>())r.sharedMaterials=Enumerable.Repeat(keeperMaterial,r.sharedMaterials.Length).ToArray();}
    var solid=Stablekeeper.gameObject.AddComponent<CapsuleCollider>();solid.height=1.8f;solid.radius=.3f;solid.center=Vector3.up*.9f;
   }
   if(ShopOpen&&(!Stablekeeper||GM.Hero.Dead||GM.Hero.IsMounted||GM.IsModalOpen||Target||Vector3.Distance(GM.Hero.transform.position,Stablekeeper.transform.position)>5f))CloseShop();
   if (!personal && !Target) {
    for (int i = 0; i < 3; i++)
     if (GM.CollectedWorldIds.Contains("horse-active:" + i) && GM.CollectedWorldIds.Contains("horse-owned:" + i)) {
      personal = Spawn(i, Ground(Stable) + new Vector3(3,0,7));
      break;
     }
   }
   if (!Target) return;
   Animate(Mathf.Min(.05f, Time.unscaledDeltaTime));
   if (TameState == 1) {
    if (Time.unscaledTime - TameStateAt >= SuccessHold) FinishTame();
    return;
   }
   if (GM.Hero.Dead || GM.IsModalOpen || Vector3.Distance(Target.transform.position, GM.Hero.transform.position) > 4.5f) { Fail(); return; }
   if (Beat.Active) {
    if (Time.unscaledTime >= Beat.Start + Beat.Life) Miss();
    return;
   }
   if (Time.unscaledTime >= nextBeatAt) SpawnBeat();
  }

  void OnDestroy() {
   if(keeperMaterial)Destroy(keeperMaterial);
   Release();
   if (instance == this) instance = null;
  }
 }
}

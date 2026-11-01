using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 public sealed class OpenWorldEnemy : MonoBehaviour {
  public const int MaxMeleeTokens = 2;
  static readonly List<OpenWorldEnemy> TokenHolders = new List<OpenWorldEnemy>();

  public string Role = "Goblin";
  public string DisplayName = "Wildwood Goblin";
  public bool IsBoss;
  public bool IsBountyElite;
  public int BountyIndex = -1;
  public float MaxHealth = 6f;
  public float Health = 6f;
  public float MoveSpeed = 3.6f;
  public float AttackRange = 1.9f;
  public float AttackDamage = 1f;
  public float LeashRadius = 26f;
  public Vector3 HomePosition;
  public OpenWorldOutpostCamp Camp;
  // Territorial wild beasts (wolves, bears, big cats, rhinos): they ignore Aster at range, warn her off
  // (roar, howl) when she comes near their ground, attack when she comes close or strikes them, give up
  // when she leaves, and recover once they are back home. Beasts lunge into their attacks.
  public bool Territorial, Beast;
  public float AggroRadius = 14f, WarnRadius = 0f;
  public int MeatDrop;
  public string SpecialName = "";

  public CharacterController Controller { get; private set; }
  public OpenWorldCharacterVisual Visual { get; private set; }
  public bool Dead => Health <= 0f;
  public bool Telegraphing => state == AIState.Windup;
  public int BossPhase => bossPhase;
  public int AppliedElement { get; private set; } = -1; // -1 none, 0 Ember, 1 Frost, 2 Gale

  enum AIState { Patrol, Chase, Windup, Strike, Recover, Stagger, Down, Dead }
  AIState state = AIState.Patrol;
  float stateTimer;
  float elementTimer;
  float burnTickTimer;
  Vector3 patrolTarget;
  bool hasToken;
  int bossPhase = 1;
  int bossSwingCount;
  bool isSlamAttack;
  bool specialAttack;
  Vector3 strikeCenter;
  float strikeSize;
  Color strikeColor;
  float warnUntil, nextWarn, calmSince, lungeUntil, restUntil;
  string restState = "idle";
  Vector3 lungeVelocity;
  bool awaitingImpact;
  float impactAt;
  float lightStaggerReadyAt;
  float nextHeavyAt;
  bool heavyIsSkeleton;
  Vector3 strikeFacing;
  public string CombatMove { get; private set; }
  bool RangedCaster => Role == "Elite" && !Beast && !IsBoss;
  public float CombatHeight => BodyHeight > 0f ? BodyHeight + 0.2f : IsBoss ? 4.2f : Role == "StoneBrute" ? 3.1f : Role == "Elite" ? 2.4f : 2.05f;
  // Real height of an adopted creature model (beasts): health bars and the lock-on mark sit on its body.
  public float BodyHeight;

  public void FitCombatSize() {
   if (!Visual || !Visual.ModelInstance) return;
   var bounds = OpenWorldMaterialHelper.ComputeRendererBounds(Visual.ModelInstance.transform);
   if (bounds.size.y > 0.01f) Visual.transform.localScale *= CombatHeight / bounds.size.y;
   Controller.height = CombatHeight;
   Controller.radius = IsBoss ? 0.9f : Role == "StoneBrute" ? 0.65f : 0.45f;
   Controller.center = Vector3.up * (CombatHeight * 0.5f);
   AttackRange = IsBoss ? 3.4f : Role == "StoneBrute" ? 2.6f : 2.15f;
   if (statusLabel) statusLabel.transform.localPosition = Vector3.up * (CombatHeight + 0.35f);
  }
  GameObject telegraphRing;
  OpenWorldTelegraph telegraph;
  TextMesh statusLabel;

  // Physical reactions (Genshin / Wuthering Waves): blows shove, heavy ones launch; a launched foe is
  // knocked down and gets back up; the killing blow throws the body (ragdoll for humanoids).
  const float Gravity = -22f;
  Vector3 knockVelocity, queuedKnock;
  float verticalVelocity, queuedLift, knockReleaseAt;
  bool airborne, rising;
  float downUntil, upUntil;
  Vector3 lastHitDir = Vector3.forward;
  float lastPush, lastLift;
  public bool IsAirborne => airborne;
  public bool IsDown => state == AIState.Down;
  // Heavier foes move less: bosses barely, brutes and elites half as far.
  float KnockMass => IsBoss ? 7f : (Role == "StoneBrute" || Role == "Elite") ? 2.2f : 1f;

  public static OpenWorldEnemy Spawn(string role, string displayName, Vector3 position, Transform parent, bool isBoss = false, OpenWorldOutpostCamp camp = null, GameObject importedPrefab = null, float importedHeight = 1.8f) {
   float gy = OpenWorldMaterialHelper.SampleGroundHeight(position);
   position.y = gy + 0.05f;

   var go = new GameObject((isBoss ? "Boss_" : "Enemy_") + role);
   if (parent) go.transform.SetParent(parent, false);
   go.transform.position = position;

   var cc = go.AddComponent<CharacterController>();
   cc.radius = isBoss ? 0.85f : 0.45f;
   cc.height = isBoss ? 2.6f : 1.8f;
   cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
   cc.stepOffset = 0.4f;

   var enemy = go.AddComponent<OpenWorldEnemy>();
   enemy.Controller = cc;
   enemy.Role = role;
   enemy.DisplayName = displayName;
   enemy.IsBoss = isBoss;
   enemy.HomePosition = position;
   enemy.Camp = camp;
   enemy.MaxHealth = isBoss ? 45f : (role == "StoneBrute" || role == "Elite" ? 12f : 6f);
   enemy.Health = enemy.MaxHealth;
   enemy.MoveSpeed = isBoss ? 4.1f : (role == "StoneBrute" ? 3.1f : 3.8f);
   enemy.AttackRange = isBoss ? 3.1f : 1.95f;
   enemy.AttackDamage = isBoss ? 2f : 1f;

   enemy.Visual = importedPrefab ? OpenWorldCharacterVisual.AttachImported(go.transform, role, importedPrefab, importedHeight) : OpenWorldCharacterVisual.Attach(go.transform, role);
   if (importedPrefab) enemy.BodyHeight = importedHeight; else enemy.FitCombatSize();
   enemy.BuildStatusLabel();
   enemy.PickNewPatrolPoint();
   return enemy;
  }

  // Bridge / capture helper: "Goblin 3.5" spawns a foe that many metres in front of the hero, facing her.
  public static void DebugSpawn(string arg) {
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero) return;
   var p = string.IsNullOrEmpty(arg) ? new string[0] : arg.Trim().Split(' ');
   string role = p.Length > 0 && p[0].Length > 0 ? p[0] : "Goblin";
   float distance = p.Length > 1 && float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d) ? d : 3.5f;
   var h = gm.Hero.transform;
   var e = Spawn(role, role, h.position + h.forward * distance, gm.WorldRoot, false);
   e.transform.rotation = Quaternion.LookRotation(-h.forward);
   e.HomePosition = e.transform.position;
   gm.Enemies.Add(e);
  }

  void BuildStatusLabel() {
   var lgo = new GameObject("EnemyStatusLabel");
   lgo.transform.SetParent(transform, false);
   lgo.transform.localPosition = new Vector3(0f, CombatHeight + 0.35f, 0f);
   statusLabel = lgo.AddComponent<TextMesh>();
   statusLabel.fontSize = 30;
   statusLabel.characterSize = 0.04f;
   statusLabel.anchor = TextAnchor.MiddleCenter;
   statusLabel.color = new Color(1f, 0.85f, 0.45f);
   statusLabel.gameObject.SetActive(false);
  }

  void PickNewPatrolPoint() {
   // Beasts roam a real territory; camp guards and monsters keep to their post.
   Vector2 r = Random.insideUnitCircle * (Territorial ? 14f : 5.5f);
   patrolTarget = HomePosition + new Vector3(r.x, 0f, r.y);
   patrolTarget.y = OpenWorldMaterialHelper.SampleGroundHeight(patrolTarget, transform);
  }

  static void PruneTokens() {
   for (int i = TokenHolders.Count - 1; i >= 0; i--) {
    var e = TokenHolders[i];
    if (!e || e.Dead || e.state == AIState.Patrol) TokenHolders.RemoveAt(i);
   }
  }

  bool AcquireToken() {
   if (IsBoss) return true;
   PruneTokens();
   if (hasToken) return true;
   if (TokenHolders.Count >= MaxMeleeTokens) return false;
   TokenHolders.Add(this);
   hasToken = true;
   return true;
  }

  void ReleaseToken() {
   if (!hasToken) return;
   hasToken = false;
   TokenHolders.Remove(this);
  }

  void Update() {
   if (state == AIState.Dead) return;
   var gm = OpenWorldManager.I;
   if (!gm || !gm.Hero || gm.IsModalOpen) return;

   if (elementTimer > 0f) {
    elementTimer -= Time.deltaTime;
    if (AppliedElement == 0) {
     burnTickTimer += Time.deltaTime;
     if (burnTickTimer >= 1.0f) {
      burnTickTimer = 0f;
      Health = Mathf.Max(0.1f, Health - 0.35f);
      OpenWorldCombatPopup.Spawn(transform.position + Vector3.up * 1.9f, "BURN 4", new Color(1f, 0.48f, 0.16f), 0.82f);
     }
    }
    if (elementTimer <= 0f) AppliedElement = -1;
   }

   if (IsBoss) {
    int targetPhase = Health <= MaxHealth * 0.35f ? 3 : (Health <= MaxHealth * 0.7f ? 2 : 1);
    if (targetPhase > bossPhase) {
     bossPhase = targetPhase;
     Visual?.Play("roar", true);
     OpenWorldAudio.Play("sfx_boss_heartwood_roar", 0.5f);
     gm.SpawnBurst(transform.position + Vector3.up * 1.4f, new Color(1f, 0.55f, 0.22f), 20);
     gm.ShowBanner($"{DisplayName.ToUpperInvariant()} ENTERS PHASE {bossPhase}!");
    }
   }

   Vector3 heroPos = gm.Hero.transform.position;
   float distToHero = Vector3.Distance(transform.position, heroPos);
   float distFromHome = Vector3.Distance(transform.position, HomePosition);

   if (statusLabel) {
    bool locked = gm.Hero.LockOnTarget == this;
    bool show = !Dead && distToHero < 26f && (locked || Health < MaxHealth || state != AIState.Patrol || IsBoss || IsBountyElite);
    statusLabel.gameObject.SetActive(show);
    if (show) {
     string elemTag = AppliedElement == 0 ? " [BURN]" : AppliedElement == 1 ? " [CHILL]" : "";
     string prefix = locked ? "◆ " : (IsBountyElite ? "★ " : "");
     statusLabel.text = prefix + $"{DisplayName}{elemTag}  {Mathf.CeilToInt(Health)}/{Mathf.CeilToInt(MaxHealth)}";
     statusLabel.color = locked ? new Color(1f, 0.92f, 0.35f) : (IsBoss || IsBountyElite ? new Color(1f, 0.62f, 0.32f) : new Color(0.95f, 0.78f, 0.72f));
     if (Camera.main) statusLabel.transform.rotation = Camera.main.transform.rotation;
    }
   }

   stateTimer -= Time.deltaTime;
   float chillMult = AppliedElement == 1 ? 0.7f : 1f;

   StepPhysics(Time.deltaTime);
   if (airborne) return; // flying from a launch: no AI until she lands

   switch (state) {
    case AIState.Patrol:
     ReleaseToken();
     if (!gm.Hero.Dead && distToHero < (IsBoss ? 22f : Territorial ? AggroRadius : 14f) && distFromHome < LeashRadius) {
      state = AIState.Chase;
      if (Territorial) RallyPack(gm);
      break;
     }
     if (Territorial) {
      // Back on its own ground and left alone: wounds close over.
      if (Health < MaxHealth && Time.time - calmSince > 6f) Health = Mathf.Min(MaxHealth, Health + MaxHealth * 0.06f * Time.deltaTime);
      // Too close for comfort: stop, face her and warn her off.
      if (!gm.Hero.Dead && WarnRadius > 0f && distToHero < WarnRadius && distFromHome < LeashRadius && Time.time >= nextWarn) {
       warnUntil = Time.time + 1.8f; nextWarn = Time.time + 9f;
       Visual?.Play("roar", true);
       OpenWorldSoundscape.Sfx("body_thud", transform.position, 0.12f);
      }
      if (Time.time < warnUntil) { FaceTarget(heroPos); break; }
     }
     if (Beast && Time.time < restUntil) { Visual?.Play(restState); break; }
     MoveTowards(patrolTarget, (Beast ? Mathf.Min(1.5f, MoveSpeed * 0.3f) : MoveSpeed * 0.55f) * chillMult, "walk");
     if (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(patrolTarget.x, patrolTarget.z)) < 0.8f || stateTimer <= 0f) {
      if (Beast) { restUntil = Time.time + Random.Range(3f, 9f); restState = Random.value < 0.5f ? "graze" : "idle"; }
      PickNewPatrolPoint();
      stateTimer = Beast ? Random.Range(8f, 14f) : Random.Range(3f, 5.5f);
     }
     break;

    case AIState.Chase:
     if (gm.Hero.Dead || distToHero > (Territorial ? AggroRadius + 14f : 24f) || distFromHome > LeashRadius + 6f) {
      ReleaseToken();
      state = AIState.Patrol;
      stateTimer = 3f;
      calmSince = Time.time;
      if (Territorial) { patrolTarget = HomePosition; patrolTarget.y = OpenWorldMaterialHelper.SampleGroundHeight(patrolTarget, transform); }
      break;
     }
     if (AcquireToken()) {
      float mult = (IsBoss && bossPhase == 3 ? 1.3f : 1f) * chillMult;
      MoveTowards(heroPos, MoveSpeed * mult, "run");
      if (distToHero <= (RangedCaster ? 8f : AttackRange) && ClearStrikePath(gm.Hero)) {
       if (RangedCaster && distToHero > AttackRange) bossSwingCount |= 1;
       BeginWindup();
      }
     } else {
      // Hold a ~3.2m ring and strafe tangentially while waiting for an attack token
      Vector3 toEnemy = (transform.position - heroPos);
      toEnemy.y = 0f;
      if (toEnemy.sqrMagnitude < 0.01f) toEnemy = Vector3.forward;
      toEnemy.Normalize();
      Vector3 tangent = Vector3.Cross(Vector3.up, toEnemy) * ((GetHashCode() & 1) == 0 ? 1f : -1f);
      Vector3 ringTarget = heroPos + toEnemy * 3.2f + tangent * 1.4f;
      MoveTowards(ringTarget, MoveSpeed * 0.7f * chillMult, "walk");
      FaceTarget(heroPos);
     }
     break;

    case AIState.Windup:
     // The strike stays committed to its warning, giving the player a dodge window.
     if (stateTimer <= 0f) {
      DestroyTelegraph();
      state = AIState.Strike;
      stateTimer = 0.25f;
      PerformStrike(gm.Hero);
     }
     break;

    case AIState.Strike:
     if (Time.time < lungeUntil && Controller && Controller.enabled) {
      Vector3 lv = lungeVelocity * Time.deltaTime; lv.y = -9.8f * Time.deltaTime;
      Controller.Move(lv);
     }
     if (awaitingImpact && Time.time >= impactAt) { awaitingImpact = false; ResolveStrike(gm.Hero); }
     if (stateTimer <= 0f) {
      ReleaseToken();
      state = AIState.Recover;
      stateTimer = specialAttack ? (heavyIsSkeleton ? 1.1f : 1.5f) : IsBoss ? 0.75f : 1.05f;
      if (heavyIsSkeleton) {
       // The swing's follow-through runs to ~1.55 s of the 1.567 s clip; blending to idle at the
       // Recover transition would cut it mid-return, so the idle switch is delayed to the clip end.
       Schedule(0.5f, () => Visual?.Play("idle"));
      } else Visual?.Play("idle");
     }
     break;

    case AIState.Recover:
    case AIState.Stagger:
     if (stateTimer <= 0f) {
      state = AIState.Chase;
     }
     break;

    case AIState.Down:
     // Lying after a knockdown, then the get-up (unhurried, the hero gets a window to follow up).
     if (!rising && Time.time >= downUntil) {
      rising = true;
      float sp = IsBountyElite ? 1.4f : 1.2f;
      Visual?.Play("getup", true, "slash", sp);
      var strikes = Visual ? Visual.GetStrikes("getup") : new float[0];
      float stand = strikes.Length > 0 && strikes[0] > 0.2f ? strikes[0] : (Visual ? Visual.GetClipLength("getup") * 0.85f : 1f);
      upUntil = Time.time + stand / sp;
     }
     if (rising && Time.time >= upUntil) {
      rising = false;
      state = AIState.Recover;
      stateTimer = 0.25f;
      Visual?.Play("idle");
     }
     break;
   }
  }

  // ---------------------------------------------------------------- physics

  void StepPhysics(float dt) {
   if (!Controller || !Controller.enabled || dt <= 0f) return;
   float now = Time.time;
   if (now < knockReleaseAt) return; // the hit-stop holds the body for a beat, then it flies
   if (queuedKnock.sqrMagnitude > 0f || queuedLift > 0f) {
    knockVelocity = queuedKnock;
    if (queuedLift > 0f) { verticalVelocity = Mathf.Max(verticalVelocity, queuedLift); airborne = true; }
    queuedKnock = Vector3.zero; queuedLift = 0f;
   }
   bool active = airborne || knockVelocity.sqrMagnitude > 0.0025f || verticalVelocity > 0f;
   if (!active) return;
   verticalVelocity += Gravity * dt;
   var flags = Controller.Move((knockVelocity + Vector3.up * verticalVelocity) * dt);
   bool grounded = (flags & CollisionFlags.Below) != 0;
   float gy = OpenWorldMaterialHelper.SampleGroundHeight(transform.position, transform);
   if (transform.position.y < gy) { transform.position = new Vector3(transform.position.x, gy + 0.02f, transform.position.z); grounded = true; }
   if ((flags & CollisionFlags.Sides) != 0) knockVelocity *= 0.35f;  // slammed into a wall
   if (grounded && verticalVelocity <= 0f) {
    float impact = -verticalVelocity;
    verticalVelocity = 0f;
    knockVelocity = Vector3.MoveTowards(knockVelocity, Vector3.zero, 16f * dt);
    if (airborne) { airborne = false; OnKnockLanded(impact); }
   } else if (!grounded) {
    knockVelocity = Vector3.MoveTowards(knockVelocity, Vector3.zero, 1.2f * dt);
    if (!airborne && verticalVelocity < -5f) airborne = true; // shoved off a ledge
   }
  }

  void OnKnockLanded(float impact) {
   if (Dead) return;
   OpenWorldSoundscape.Sfx("body_thud", transform.position, 0.3f);
   if (impact > 4f && !IsBoss && Visual && Visual.HasState("knockdown")) {
    // Down on the ground for a moment, then up.
    state = AIState.Down;
    rising = false;
    downUntil = Time.time + (IsBountyElite ? 0.5f : 0.85f);
    if (Visual.CurrentState != "knockdown" || Visual.ActionTime < Visual.GetStrikes("knockdown")[0] * 0.9f) {
     var strikes = Visual.GetStrikes("knockdown");
     float lie = strikes.Length > 0 ? strikes[0] : Visual.GetClipLength("knockdown") * 0.6f;
     Visual.PlayAt("knockdown", "slash", 1.2f, Mathf.Max(0f, lie - 0.25f), 0.1f);
    }
    if (Visual.HasState("knockdown_loop")) Schedule(0.35f, () => { if (state == AIState.Down && !rising) Visual?.Play("knockdown_loop"); });
   } else {
    state = AIState.Stagger;
    stateTimer = 0.45f;
    Visual?.Play("gethit", true);
   }
  }

  readonly List<(float time, System.Action action)> pendingActions = new List<(float, System.Action)>();
  void Schedule(float delay, System.Action action) => pendingActions.Add((Time.time + delay, action));
  void LateUpdate() {
   if (pendingActions.Count == 0) return;
   float now = Time.time;
   for (int i = 0; i < pendingActions.Count; i++) {
    if (pendingActions[i].time > now) continue;
    var a = pendingActions[i].action;
    pendingActions.RemoveAt(i--);
    a?.Invoke();
   }
  }

  void OnControllerColliderHit(ControllerColliderHit hit) {
   if (knockVelocity.sqrMagnitude > 9f && Mathf.Abs(hit.normal.y) < 0.62f) OpenWorldPhysicsProps.Shove(hit, knockVelocity, true);
  }

  void BeginWindup() {
   state = AIState.Windup;
   bossSwingCount++;
   isSlamAttack = (IsBoss || IsBountyElite) && (bossSwingCount % 3 == 0);
   specialAttack = !isSlamAttack && bossSwingCount % 2 == 0;
   // Hollow Revenant heavy: an occasional BONE SLAM when Aster is in melee range, on its own
   // cooldown so it stays occasional. Every other behaviour keeps the ordinary cadence.
   bool skeletonHeavy = false;
   var heroRef = OpenWorldManager.I && OpenWorldManager.I.Hero ? OpenWorldManager.I.Hero.transform : null;
   if (!isSlamAttack && !Beast && (DisplayName + " " + SpecialName).ToLowerInvariant().Contains("revenant")) {
    // The Hollow Revenant special slot IS its heavy: when the cooldown is ready and Aster is in
    // melee range it becomes the BONE SLAM; while cooling down the revenant uses ordinary attacks.
    if (Time.time >= nextHeavyAt && heroRef && Vector3.Distance(transform.position, heroRef.position) <= AttackRange + 1.5f) {
     specialAttack = true;
     skeletonHeavy = true;
     nextHeavyAt = Time.time + 6.5f;
    } else {
     specialAttack = false;
    }
   }
   heavyIsSkeleton = specialAttack && skeletonHeavy;
   float windup = IsBoss ? (isSlamAttack ? 0.9f : (bossPhase >= 2 ? 0.65f : 0.8f)) : 0.72f;
   if (specialAttack) windup = heavyIsSkeleton ? 0.8f : 1.15f;
   stateTimer = windup;
   strikeCenter = transform.position;
   strikeFacing = transform.forward;
   CombatMove = "CLEAVE";
   strikeSize = isSlamAttack ? 4.5f : AttackRange + 0.55f;
   strikeColor = new Color(1f, .25f, .12f);
   if (specialAttack && Beast) {
    // A beast's big move is a committed lunge at where she stands now.
    strikeSize = 1.7f;
    if (OpenWorldManager.I && OpenWorldManager.I.Hero) strikeCenter = OpenWorldManager.I.Hero.transform.position;
    strikeColor = new Color(1f, .42f, .18f);
    string species = (DisplayName + " " + SpecialName).ToLowerInvariant();
    CombatMove = species.Contains("bear") || species.Contains("sweep") ? "MAULING SWEEP" : species.Contains("rhino") || species.Contains("ravager") ? "HORN RUSH" : species.Contains("spider") ? "VENOM POUNCE" : "FANG LEAP";
    if (CombatMove == "MAULING SWEEP") { strikeCenter = transform.position; strikeSize = 3.2f; }
    OpenWorldCombatPopup.Spawn(transform.position + Vector3.up * 1.6f, CombatMove, strikeColor, 1f);
   } else if (specialAttack) {
    bool brute = Role == "StoneBrute" || IsBoss;
    bool skeleton = heavyIsSkeleton;
    strikeSize = brute ? 4f : skeleton ? 2.6f : Role == "Elite" ? 3.2f : 1.6f;
    if (!brute && !skeleton && OpenWorldManager.I && OpenWorldManager.I.Hero) strikeCenter = OpenWorldManager.I.Hero.transform.position;
    // The skeleton's amber is slightly brighter than the brute's: the Graveyard's dark, misty ground
    // swallows the ring otherwise. Same hue family, no extra particles.
    strikeColor = brute ? new Color(1f, .6f, .15f) : skeleton ? new Color(1f, .72f, .12f) : Role == "Elite" ? new Color(.65f, .35f, 1f) : new Color(.25f, 1f, .55f);
    CombatMove = brute ? "EARTHSHATTER" : skeleton ? "BONE SLAM" : Role == "Elite" ? "ARCANE ERUPTION" : "VENOM BURST";
    OpenWorldCombatPopup.Spawn(transform.position + Vector3.up * CombatHeight,
     CombatMove, strikeColor, 1f);
   }
   string atkClip = isSlamAttack ? "slam" : ((bossSwingCount & 1) == 0 ? "attack_2" : "attack_1");
   Visual?.Play(atkClip, true);
   DestroyTelegraph();
   telegraph = OpenWorldTelegraph.Show(strikeCenter, strikeSize, windup, strikeColor);
  }

  void PerformStrike(OpenWorldHero hero) {
   if (!hero || hero.Dead) return;
   if (Beast && !(specialAttack && CombatMove == "MAULING SWEEP")) {
    // Spring at the target point: the strike lands where the lunge ends.
    Vector3 to = strikeCenter - transform.position;
    if (!specialAttack) to = strikeFacing * 1.2f;
    to.y = 0f;
    float reach = Mathf.Max(0f, to.magnitude - 0.6f);
    if (to.sqrMagnitude > 0.01f) {
     transform.rotation = Quaternion.LookRotation(to.normalized);
     lungeVelocity = to.normalized * Mathf.Min(14f, reach / 0.22f);
     lungeUntil = Time.time + 0.22f;
    }
   }
   awaitingImpact = true;
   impactAt = Time.time + (Beast && CombatMove != "MAULING SWEEP" ? .22f : 0f);
   if (impactAt <= Time.time) { awaitingImpact = false; ResolveStrike(hero); }
  }

  void ResolveStrike(OpenWorldHero hero) {
   if (!hero || hero.Dead || Dead) return;
   float strikeRadius = isSlamAttack ? 4.5f : AttackRange + 0.55f;
   if (isSlamAttack) {
    OpenWorldSlashWave.SpawnSlash(transform.position + Vector3.up * 0.35f, transform.rotation, new Color(1f, 0.45f, 0.16f), 3.6f, true);
    OpenWorldManager.I?.SpawnBurst(transform.position + Vector3.up * 0.4f, new Color(0.95f, 0.42f, 0.18f), 22);
   }
   if (specialAttack) {
    OpenWorldManager.I?.SpawnBurst(strikeCenter + Vector3.up * .3f, strikeColor, 20);
    OpenWorldSlashWave.SpawnSlash(strikeCenter + Vector3.up * .2f, transform.rotation, strikeColor, strikeSize, true);
   }
   Vector3 delta = hero.transform.position - strikeCenter;
   float heightDifference = Mathf.Abs(delta.y);
   delta.y = 0f;
   Vector3 fromAttacker = hero.transform.position - transform.position; fromAttacker.y = 0f;
   bool facing = specialAttack || isSlamAttack || fromAttacker.sqrMagnitude < .04f || Vector3.Dot(strikeFacing, fromAttacker.normalized) >= .25f;
   bool reached = !Beast || CombatMove == "MAULING SWEEP" || Vector3.Distance(transform.position, hero.transform.position) <= AttackRange + .5f;
   if (delta.sqrMagnitude <= strikeSize * strikeSize && heightDifference < 2.2f && facing && reached && ClearStrikePath(hero)) {
    // Slams throw her down; ordinary blows shove her back.
    hero.TakeDamage((isSlamAttack || specialAttack) ? AttackDamage + 0.5f : AttackDamage, transform, IsBoss ? 1.1f : 0.7f, isSlamAttack);
   }
  }
  bool ClearStrikePath(OpenWorldHero hero) {
   Vector3 origin = transform.position + Vector3.up * Mathf.Min(1f, CombatHeight * .6f);
   Vector3 end = hero.transform.position + Vector3.up * .9f;
   foreach (var hit in Physics.RaycastAll(origin, end - origin, Vector3.Distance(origin, end), ~0, QueryTriggerInteraction.Ignore)) {
    if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(hero.transform)) continue;
    if (hit.collider.GetComponentInParent<OpenWorldEnemy>() || hit.collider.GetComponentInParent<OpenWorldAnimalInteraction>()) continue;
    return false;
   }
   return true;
  }

  public void Stun(float duration) {
   DestroyTelegraph();
   ReleaseToken();
   state = AIState.Stagger;
   stateTimer = duration;
   Visual?.Play("hit", true);
   OpenWorldCombatPopup.Spawn(transform.position + Vector3.up * (IsBoss ? 2.8f : 1.9f), "PARRIED!", new Color(1f, 0.92f, 0.32f), 1.2f);
  }

  // push / lift: knockback and launch speeds (m/s) before the foe's weight; push < 0 picks a default.
  public void TakeHit(float damage, Vector3 attackerPos, bool heavyHit = false, int element = -1, float push = -1f, float lift = 0f) {
   if (Dead) return;
   if (push < 0f) push = heavyHit ? 6.5f : 2.4f;
   Vector3 away = transform.position - attackerPos; away.y = 0f;
   lastHitDir = away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;
   lastPush = push; lastLift = lift;

   float reactionMult = 1f;
   string reactionText = null;
   Color reactionColor = Color.white;

   if (element >= 0 && element <= 2) {
    if ((AppliedElement == 0 && element == 1) || (AppliedElement == 1 && element == 0)) {
     // Genshin-style MELT Elemental Reaction!
     reactionMult = 1.65f;
     reactionText = "MELT!";
     reactionColor = new Color(1f, 0.62f, 0.22f);
     AppliedElement = -1;
     elementTimer = 0f;
     OpenWorldManager.I?.SpawnBurst(transform.position + Vector3.up * 1.1f, reactionColor, 20);
    } else if (element == 2 && (AppliedElement == 0 || AppliedElement == 1)) {
     // Genshin-style SWIRL Elemental Reaction!
     int swirledElem = AppliedElement;
     reactionMult = 1.35f;
     reactionText = "SWIRL!";
     reactionColor = new Color(0.35f, 1f, 0.78f);
     AppliedElement = -1;
     elementTimer = 0f;
     TriggerSwirlSpread(swirledElem, damage * 0.55f);
    } else if (element == 0 || element == 1) {
     AppliedElement = element;
     elementTimer = 4.0f;
     burnTickTimer = 0f;
    }
   }

   OpenWorldAudio.PlayCombatImpact(heavyHit);
   OpenWorldVfx.Hit(element, transform.position + Vector3.up * (IsBoss ? 1.9f : 1.1f), heavyHit);
   float finalDamage = damage * reactionMult;
   Health = Mathf.Max(0f, Health - finalDamage);

   Vector3 popPos = transform.position + Vector3.up * (IsBoss ? 2.7f : 1.85f);
   int displayDmg = Mathf.Max(1, Mathf.RoundToInt(finalDamage * 10f));
   Color dmgColor = heavyHit ? new Color(1f, 0.86f, 0.28f) : new Color(1f, 0.96f, 0.90f);
   OpenWorldCombatPopup.Spawn(popPos, (heavyHit ? "CRIT " : "") + displayDmg, dmgColor, heavyHit ? 1.2f : 1.0f);
   if (!string.IsNullOrEmpty(reactionText)) {
    OpenWorldCombatPopup.Spawn(popPos + Vector3.up * 0.45f, reactionText, reactionColor, 1.35f);
   }

   if (Health <= 0f) {
    Die();
    return;
   }
   calmSince = Time.time;
   if (Territorial && state == AIState.Patrol) { warnUntil = 0f; RallyPack(OpenWorldManager.I); }

   Visual?.TriggerFlinch(attackerPos, heavyHit ? 1.35f : 0.85f);
   float hitStop = heavyHit ? 0.055f : 0.032f;
   Visual?.TriggerHitStop(hitStop);
   OpenWorldManager.I?.Hero?.Visual?.TriggerHitStop(heavyHit ? 0.05f : 0.03f);

   // Knockback after the hit-stop; a strong lift launches light foes into the air.
   float mass = KnockMass;
   queuedKnock = lastHitDir * (push / mass);
   float launch = lift / mass;
   bool launched = launch > 2.6f && !IsBoss;
   queuedLift = launched ? launch : 0f;
   knockReleaseAt = Time.time + hitStop;
   if (state == AIState.Down) {
    // Hits on a downed foe keep it down a little longer, without launching it again.
    queuedLift = 0f;
    downUntil = Mathf.Max(downUntil, Time.time + 0.25f);
    return;
   }
   if (launched) {
    DestroyTelegraph();
    ReleaseToken();
    state = AIState.Stagger;
    stateTimer = 0.6f;
    // Face the blow and fall away from it.
    transform.rotation = Quaternion.LookRotation(-lastHitDir);
    if (Visual && Visual.HasState("knockdown")) Visual.PlayAt("knockdown", "slash", 1.15f, 0f, 0.06f);
    else Visual?.Play("gethit", true);
    return;
   }

   if (heavyHit || (!IsBoss && Time.time >= lightStaggerReadyAt) || (IsBoss && state == AIState.Windup && finalDamage >= 3.5f)) {
    DestroyTelegraph();
    ReleaseToken();
    state = AIState.Stagger;
    // A heavy blow staggers a foe back on its heels; light ones flinch.
    bool reel = heavyHit && !IsBoss && Visual && Visual.HasState("knockback");
    stateTimer = reel ? 0.7f : heavyHit ? .45f : .28f;
    lightStaggerReadyAt = Time.time + 1.25f;
    Visual?.Play(reel ? "knockback" : "gethit", true, "slash", reel ? 1.25f : 1f);
   }
  }

  void TriggerSwirlSpread(int swirledElement, float aoeDamage) {
   var gm = OpenWorldManager.I;
   if (!gm) return;
   Color c = swirledElement == 0 ? new Color(1f, 0.45f, 0.16f) : new Color(0.32f, 0.88f, 1f);
   gm.SpawnBurst(transform.position + Vector3.up * 1.0f, new Color(0.35f, 1f, 0.75f), 22);
   for (int i = gm.Enemies.Count - 1; i >= 0; i--) {
    var other = gm.Enemies[i];
    if (!other || other == this || other.Dead || !other.isActiveAndEnabled) continue;
    if (Vector3.Distance(transform.position, other.transform.position) <= 4.8f) {
     other.AppliedElement = swirledElement;
     other.elementTimer = 3.5f;
     other.Health = Mathf.Max(0.1f, other.Health - aoeDamage);
     OpenWorldCombatPopup.Spawn(other.transform.position + Vector3.up * 1.8f, "SWIRL " + Mathf.Max(1, Mathf.RoundToInt(aoeDamage * 10f)), c, 0.95f);
    }
   }
  }

  void Die() {
   DestroyTelegraph();
   ReleaseToken();
   if (statusLabel) statusLabel.gameObject.SetActive(false);
   state = AIState.Dead;
   if (Controller) Controller.enabled = false;

   // The killing blow throws the body: humanoids ragdoll, other creatures tumble on heavy blows; the
   // smoke dissolve plays when the body sinks away. Bosses keep their death animation.
   var gm = OpenWorldManager.I;
   float mass = KnockMass;
   Vector3 throwVelocity = lastHitDir * (1.2f + lastPush * 0.62f / Mathf.Sqrt(mass)) + Vector3.up * (1.0f + lastLift * 0.6f / Mathf.Sqrt(mass));
   float lie = 3.2f;
   bool thrown = false;
   if (!IsBoss && Visual) {
    System.Action dissolve = () => { if (this && !OpenWorldVfx.Death(transform.position + Vector3.up * 0.4f, 1f, false) && OpenWorldManager.I) OpenWorldManager.I.SpawnBurst(transform.position + Vector3.up * 0.6f, new Color(1f, 0.72f, 0.28f), 12); };
    var body = OpenWorldRagdoll.Activate(Visual, throwVelocity, lie, dissolve);
    if (!body && (lastPush >= 6f || lastLift > 2f)) body = OpenWorldRagdoll.Tumble(Visual, throwVelocity * 0.8f, lie, dissolve);
    thrown = body;
   }
   if (!thrown) {
    Visual?.Play("death", true);
    if (!IsBoss) StartCoroutine(CorpseSlide(lastHitDir * Mathf.Min(6f, lastPush / mass)));
   }
   if (gm) {
    // Smoke-and-ember dissolve on defeat (bosses get a full explosion).
    if (!thrown && !OpenWorldVfx.Death(transform.position + Vector3.up * 0.6f, IsBoss ? 2.2f : 1f, IsBoss))
     gm.SpawnBurst(transform.position + Vector3.up * 0.9f, new Color(1f, 0.72f, 0.28f), 14);
    gm.OnEnemyDefeated(this);
    if (MeatDrop > 0) {
     gm.RawMeat = Mathf.Min(999, gm.RawMeat + MeatDrop); gm.MarkProgressDirty();
     OpenWorldCombatPopup.Spawn(transform.position + Vector3.up * 1.4f, "+" + MeatDrop + " RAW MEAT", new Color(1f, 0.68f, 0.45f), 1.1f);
    }

    // Drop open-world loot: coins, gems, and a chance for Health or Aether Potions
    Vector3 dropCenter = transform.position;
    OpenWorldCollectible.Spawn(OWCollectibleKind.GoldCoin, dropCenter + new Vector3(-0.5f, 0f, 0.3f), gm.WorldRoot, IsBoss ? 50 : 12);
    if (IsBoss || Random.value < 0.45f) {
     OpenWorldCollectible.Spawn(OWCollectibleKind.StarGem, dropCenter + new Vector3(0.5f, 0f, 0.3f), gm.WorldRoot, IsBoss ? 5 : 1);
    }
    if (IsBoss || Random.value < 0.55f) {
     var potionKind = Random.value < 0.55f ? OWCollectibleKind.AetherPotion : OWCollectibleKind.HealthPotion;
     OpenWorldCollectible.Spawn(potionKind, dropCenter + new Vector3(0f, 0f, -0.55f), gm.WorldRoot, 1);
    }
   }

   if (Camp) Camp.NotifyGuardDefeated();
   Destroy(gameObject, thrown ? lie + 1.4f : 2.2f);
  }

  // Non-ragdoll deaths still slide back with the blow while the death clip plays.
  System.Collections.IEnumerator CorpseSlide(Vector3 velocity) {
   float t = 0f;
   while (t < 0.45f && velocity.sqrMagnitude > 0.01f) {
    float dt = Time.deltaTime;
    t += dt;
    Vector3 p = transform.position + velocity * dt;
    p.y = OpenWorldMaterialHelper.SampleGroundHeight(p, transform) + 0.02f;
    transform.position = p;
    velocity = Vector3.MoveTowards(velocity, Vector3.zero, 14f * dt);
    yield return null;
   }
  }

  // Pack animals answer each other: the rest of the pack within earshot joins the fight.
  void RallyPack(OpenWorldManager gm) {
   if (!gm) return;
   foreach (var other in gm.Enemies) {
    if (!other || other == this || other.Dead || !other.Territorial || other.DisplayName != DisplayName) continue;
    if (other.state != AIState.Patrol || (other.transform.position - transform.position).sqrMagnitude > 16f * 16f) continue;
    other.state = AIState.Chase; other.warnUntil = 0f; other.calmSince = Time.time;
   }
  }

  void MoveTowards(Vector3 target, float speed, string animState) {
   Vector3 delta = target - transform.position;
   delta.y = 0f;
   if (delta.sqrMagnitude < 0.04f) {
    Visual?.Play("idle");
    return;
   }
   Vector3 dir = delta.normalized;
   transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);

   // Foes cannot swim: they stop at the edge of water deeper than ~1 m and wait there (a swimming hero
   // is out of their reach, as in BotW).
   Vector3 ahead = transform.position + dir * 0.7f;
   if (OpenWorldRealm.Instance && OpenWorldTerrainSculptor.WaterAt(ahead.x, ahead.z, out float waterY, out float waterDepth)
       && waterDepth > 1f && waterY - transform.position.y < 1f) {
    Visual?.Play("idle");
    return;
   }

   if (Controller && Controller.enabled) {
    Vector3 step = dir * (speed * Time.deltaTime);
    step.y = -9.8f * Time.deltaTime;
    Controller.Move(step);
   } else {
    transform.position += dir * (speed * Time.deltaTime);
   }
   float gy = OpenWorldMaterialHelper.SampleGroundHeight(transform.position, transform);
   if (transform.position.y < gy) {
    transform.position = new Vector3(transform.position.x, gy + 0.02f, transform.position.z);
   }
   Visual?.Play(animState);
  }

  void FaceTarget(Vector3 target) {
   Vector3 d = target - transform.position;
   d.y = 0f;
   if (d.sqrMagnitude > 0.001f) {
    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d.normalized), 12f * Time.deltaTime);
   }
  }

  void ShowTelegraph(float radius) {
   DestroyTelegraph();
   var tint = isSlamAttack ? new Color(1f, 0.36f, 0.1f) : new Color(1f, 0.14f, 0.1f);
   telegraph = OpenWorldTelegraph.Show(transform.position, radius, Mathf.Max(0.2f, stateTimer), tint);
  }

  void DestroyTelegraph() {
   awaitingImpact = false;
   if (telegraph) { telegraph.Dismiss(); telegraph = null; }
   if (telegraphRing) {
    Destroy(telegraphRing);
    telegraphRing = null;
   }
  }

  void OnDestroy() {
   if (OpenWorldManager.I) OpenWorldManager.I.Enemies.Remove(this);
   DestroyTelegraph();
   ReleaseToken();
  }
  void OnDisable() { ReleaseToken(); DestroyTelegraph(); }
 }

 // Open-World Outpost Camp with a Rally Banner that starts CRIMSON RED and turns
 // EMERALD GREEN when activated/captured.
 public sealed class OpenWorldOutpostCamp : MonoBehaviour {
  public string CampTitle = "Outpost Camp";
  public string PersistentId = "";
  public bool Captured { get; private set; }
  public readonly List<OpenWorldEnemy> Guards = new List<OpenWorldEnemy>();
  GameObject bannerVisual;
  TextMesh labelText;

  static readonly Color CrimsonBanner = new Color(0.92f, 0.18f, 0.18f);
  static readonly Color EmeraldBanner = new Color(0.18f, 0.92f, 0.38f);

  public static OpenWorldOutpostCamp Create(string title, Vector3 position, Transform parent, string[] guardRoles) {
   float gy = OpenWorldMaterialHelper.SampleGroundHeight(position);
   position.y = gy + 0.02f;

   var root = new GameObject("Outpost_" + title);
   if (parent) root.transform.SetParent(parent, false);
   root.transform.position = position;

   var camp = root.AddComponent<OpenWorldOutpostCamp>();
   camp.CampTitle = title;
   camp.BuildBanner();

   for (int i = 0; i < guardRoles.Length; i++) {
    float ang = (i * 360f / Mathf.Max(1, guardRoles.Length)) * Mathf.Deg2Rad;
    Vector3 gp = position + new Vector3(Mathf.Cos(ang) * 4.2f, 0f, Mathf.Sin(ang) * 4.2f);
    var e = OpenWorldEnemy.Spawn(guardRoles[i], guardRoles[i], gp, parent, false, camp);
    camp.Guards.Add(e);
   }
   return camp;
  }

  void BuildBanner() {
   bannerVisual = OpenWorldMaterialHelper.SpawnModel("Props/Expansion/RallyBanner", "Props/Expansion/Textures/RallyBanner_basecolor", transform, transform.position, 2.2f, Color.white, true);
   if (!bannerVisual) {
    bannerVisual = new GameObject("ProceduralBanner");
    bannerVisual.transform.SetParent(transform, false);
    var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pole.name = "Banner pole";
    pole.GetComponent<Renderer>().sharedMaterial = OpenWorldMaterialHelper.CreateLit("Banner bronze pole",new Color(.34f,.23f,.11f));
    pole.transform.SetParent(bannerVisual.transform, false);
    pole.transform.localPosition = new Vector3(0f, 1.1f, 0f);
    pole.transform.localScale = new Vector3(0.1f, 1.1f, 0.1f);
    var cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
    cloth.name = "Banner fabric";
    cloth.transform.SetParent(bannerVisual.transform, false);
    cloth.transform.localPosition = new Vector3(0.45f, 1.65f, 0f);
    cloth.transform.localScale = new Vector3(0.8f, 0.95f, 0.05f);
   }
   ApplyBannerColor(CrimsonBanner, new Color(0.35f, 0.04f, 0.04f));

   var lgo = new GameObject("CampLabel");
   lgo.transform.SetParent(transform, false);
   lgo.transform.localPosition = new Vector3(0f, 2.65f, 0f);
   labelText = lgo.AddComponent<TextMesh>();
   labelText.text = CampTitle + " [HOSTILE]";
   labelText.fontSize = 34;
   labelText.characterSize = 0.045f;
   labelText.anchor = TextAnchor.MiddleCenter;
   labelText.color = new Color(1f, 0.45f, 0.45f);
  }

  void ApplyBannerColor(Color tint, Color emission) {
   OpenWorldBannerCloth.Apply(bannerVisual, tint.g > tint.r);
  }

  public void NotifyGuardDefeated() {
   Guards.RemoveAll(g => g == null || g.Dead);
   if (Guards.Count == 0 && !Captured) {
    Capture();
   }
  }

  void Update() {
   var gm = OpenWorldManager.I;
   if (labelText && gm && gm.Hero) {
    float d = Vector3.Distance(gm.Hero.transform.position, transform.position);
    labelText.gameObject.SetActive(d <= 44f);
    if (d <= 44f && Camera.main) labelText.transform.rotation = Camera.main.transform.rotation;
   }
   if (Captured) return;
   Guards.RemoveAll(g => g == null || g.Dead);
   if (Guards.Count == 0) {
    if (gm && gm.Hero && Vector3.Distance(gm.Hero.transform.position, transform.position) < 4.5f) {
     Capture();
    }
   }
  }

  public void Capture() {
   if (Captured) return;
   Captured = true;
   // Turn the red flag to bright EMERALD GREEN when activated!
   ApplyBannerColor(EmeraldBanner, new Color(0.06f, 0.42f, 0.14f));
   if (labelText) {
    labelText.text = CampTitle + " [CAPTURED]";
    labelText.color = new Color(0.35f, 1f, 0.55f);
   }
   var gm = OpenWorldManager.I;
   if (gm) {
    gm.OnCampCaptured(this);
    gm.SpawnBurst(transform.position + Vector3.up * 1.4f, EmeraldBanner, 22);
   }
  }

  public void RestoreCaptured() {
   Captured = true;
   foreach (var guard in Guards) if (guard) guard.gameObject.SetActive(false);
   Guards.Clear();
   ApplyBannerColor(EmeraldBanner, new Color(0.06f, 0.42f, 0.14f));
   if (labelText) {
    labelText.text = CampTitle + " [CAPTURED]";
    labelText.color = EmeraldBanner;
   }
  }
 }
}



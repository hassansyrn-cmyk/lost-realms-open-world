using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LostRealmsOW {
 // Aster: third-person controller with Genshin / Wuthering Waves style movement and combat.
 //  - Walk, run and sprint with acceleration, quick turns and stride-matched animation.
 //  - Dash: tap Shift (or DASH) for a short evasive burst with i-frames and afterimages; keep holding to
 //    sprint on stamina. One air dash per jump.
 //  - Jumps with takeoff, air and fall poses, soft landings (a dip while running) and heavy landings from
 //    high falls, a flip double jump, and a cloth paraglider held overhead with both hands.
 //  - Four-hit combos per weapon style: damage lands on the animation's impact frame, each hit lunges
 //    toward the nearest foe in front, the next press is buffered, and movement or a dash cancels the
 //    recovery. Charged attacks, elemental casts and the burst have their own animations with effects
 //    timed to their impact frames.
 public sealed partial class OpenWorldHero : MonoBehaviour {
  public const float WalkSpeed = 2.0f;
  public const float RunSpeed = 5.4f;
  public const float SprintSpeed = 7.7f;
  public const float GlideSpeed = 8.4f;
  public const float JumpVelocity = 8.4f;
  public const float Gravity = -22f;
  public const float CoyoteWindow = 0.14f;
  public const float JumpBufferWindow = 0.13f;
  const float DashTime = 0.3f, AirDashTime = 0.24f, DashDistance = 4.6f, AirDashDistance = 3.4f;
  const float DashStamina = 18f, AirDashStamina = 14f, SprintStaminaPerSecond = 13f;
  // The baked realm extends the playable area to the foot of the perimeter ranges.
  public static float WorldBound => OpenWorldRealm.Instance ? OpenWorldRegions.PlayableHalf : 212f;

  public CharacterController Controller { get; private set; }
  public OpenWorldCharacterVisual Visual { get; private set; }

  public float MaxHealth = 5f;
  public float Health = 5f;
  public float MaxAether = 100f;
  public float Aether = 100f;
  float importedStepDistance;
  bool alternateStep;
  public float MaxStamina = 100f;
  public float Stamina = 100f;
  public bool IsMealBuffActive => Time.time < mealBuffUntil;
  public OpenWorldCompanionWisp CompanionWisp { get; private set; }

  public int EquippedWeaponId { get; private set; }
  public int EquippedSkinId { get; private set; }
  public int ActiveElement { get; private set; } // 0 Ember, 1 Frost, 2 Gale
  public bool Dead => Health <= 0f;
  public bool Grounded { get; private set; }
  public bool IsGliding { get; private set; }
  public bool IsSprinting => sprinting;
  public bool IsDashing => dashing;
  public bool IsCounterReady => Time.time < counterReadyUntil;
  public OpenWorldEnemy LockOnTarget { get; set; }

  // External / UI touch input hooks
  public Vector2 TouchMoveInput;
  // Editor capture tools drive the hero while the editor window is in the background.
  public static bool DebugAlwaysFocused;
  public bool TouchJump, TouchDodge, TouchAttack, TouchCharged, TouchParry, TouchSpell, TouchEcho, TouchBurst;
  public bool TouchSprintHeld;

  Vector3 velocity;          // y only (gravity / jumps); horizontal motion lives in planarVelocity
  Vector3 planarVelocity;
  float lastGroundedTime = -10f;
  float lastJumpPressedTime = -10f;
  bool doubleJumpAvailable;
  bool wasGrounded = true;
  float airTime, minAirVelocity;
  float airAnimHoldUntil;
  readonly RaycastHit[] groundHits = new RaycastHit[16];
  readonly RaycastHit[] groundRayHits = new RaycastHit[12];

  // Sprint / dash
  bool sprinting, sprintHeld, dashing, airDashUsed;
  float sprintHeldTime;
  float dashStart, dashDuration, dashDistance, dashReadyAt, staminaRegenAt, exhaustedUntil;
  Vector3 dashDir;
  int afterimagesLeft;
  float nextAfterimage;

  // Actions (attacks, casts, landings): the animation holds until actionUntil; movement may cancel it
  // after cancelAt, the next attack may start after chainAt.
  float actionUntil, cancelAt, chainAt, bufferedAttackUntil;
  bool actionMovable;
  float lungeUntil, lungeSpeed;
  Vector3 lungeDir;
  float animHoldUntil;
  bool animHoldCancelByMove;
  float trailOnAt = -1f, trailOffAt = -1f;
  readonly List<(float time, Action action)> pending = new List<(float, Action)>();

  int comboStep;
  float comboResetAt;
  float invulnerableUntil;
  float parryWindowUntil;
  float hyperArmorUntil;
  float counterReadyUntil;
  float mealBuffUntil;
  float echoCooldownUntil;
  float chargeHoldTime;
  bool mouseAttackStarted;
  TrailRenderer weaponTrail;
  OpenWorldParaglider glider;

  // Kept for the bridge/debug helpers and older callers that set it directly.
  float actionLockUntil { get => actionUntil; set { actionUntil = value; cancelAt = value; chainAt = value; } }

  struct Hit {
   public string state; public float start, speed, damage, reach, lunge; public bool heavy;
   public Hit(string state, float start, float speed, float damage, float reach, float lunge, bool heavy = false) {
    this.state = state; this.start = start; this.speed = speed; this.damage = damage; this.reach = reach; this.lunge = lunge; this.heavy = heavy;
   }
  }

  // Four-hit chains per weapon style (states are styled by the driver: attack_2 + chop = chop_2). start
  // skips the clip's idle lead-in; damage lands on the baked impact frame.
  static readonly Dictionary<string, Hit[]> Combos = new Dictionary<string, Hit[]> {
   { "slash", new[] { new Hit("attack_1", 0.26f, 1.3f, 1f, 1f, 0.7f), new Hit("attack_2", 0.5f, 1.35f, 1.05f, 1f, 0.7f), new Hit("attack_3", 0.36f, 1.35f, 1.1f, 1.05f, 0.8f), new Hit("attack_4", 0.48f, 1.25f, 1.55f, 1.2f, 1.1f, true) } },
   { "chop", new[] { new Hit("attack_1", 0.16f, 1.15f, 1f, 1f, 0.5f), new Hit("attack_2", 0.14f, 1.15f, 1.1f, 1.05f, 0.6f), new Hit("attack_3", 0.14f, 1.12f, 1.2f, 1.1f, 0.6f), new Hit("attack_4", 0.18f, 1.05f, 1.75f, 1.3f, 0.9f, true) } },
   { "spear", new[] { new Hit("attack_1", 0.12f, 1.2f, 1f, 1.05f, 0.8f), new Hit("attack_2", 0.1f, 1.15f, 1.05f, 1.1f, 0.6f), new Hit("attack_3", 0.12f, 1.15f, 1.15f, 1.1f, 0.7f), new Hit("attack_4", 0.06f, 1.0f, 1.6f, 1.25f, 1.6f, true) } },
   { "unarmed", new[] { new Hit("attack_1", 0.1f, 1.25f, 1f, 0.9f, 0.5f), new Hit("attack_2", 0.1f, 1.25f, 1.05f, 0.9f, 0.5f), new Hit("attack_3", 0.1f, 1.2f, 1.15f, 0.95f, 0.6f), new Hit("attack_4", 0.1f, 1.1f, 1.6f, 1.1f, 0.9f, true) } }
  };

  public void RefillStamina() => Stamina = MaxStamina;
  public void ActivateMealBuff(float duration) => mealBuffUntil = Time.time + duration;

  public static OpenWorldHero Spawn(Vector3 position, Transform parent, int weaponId = 0, int skinId = 0) {
   float gy = OpenWorldMaterialHelper.SampleGroundHeight(position);
   position.y = gy + 0.06f;

   var go = new GameObject("Aster");
   if (parent) go.transform.SetParent(parent, false);
   go.transform.position = position;
   go.tag = "Player";

   var cc = go.AddComponent<CharacterController>();
   cc.radius = 0.38f;
   cc.height = 1.78f;
   cc.center = new Vector3(0f, 0.89f, 0f);
   cc.stepOffset = 0.38f;
   cc.slopeLimit = 50f;
   cc.skinWidth = 0.04f;
   cc.minMoveDistance = 0f;

   var hero = go.AddComponent<OpenWorldHero>();
   hero.Controller = cc;
   hero.Visual = OpenWorldCharacterVisual.Attach(go.transform, "Aster", skinId);
   hero.Visual.EnableHeroRig(go.transform);
   hero.HookTraversalEvents();
   hero.EquippedSkinId = skinId;
   hero.EquipWeapon(weaponId);
   hero.BuildGliderVisual();
   hero.CompanionWisp = OpenWorldCompanionWisp.Attach(go.transform, parent);
   return hero;
  }

  void BuildGliderVisual() {
   glider = OpenWorldParaglider.Build(transform);
  }

  public void LaunchIntoGlide(float upwardVelocity = 21.5f) {
   if (IsMounted) return;
   velocity.y = upwardVelocity;
   Grounded = false;
   lastGroundedTime = -10f;
   doubleJumpAvailable = false;
   SetGliding(true);
   Visual?.PlayAt("double_jump", "slash", 0.9f, 0f, 0.05f);
   airAnimHoldUntil = Time.time + 0.5f;
  }

  void SetGliding(bool gliding) {
   if (IsMounted) gliding = false;
   if (gliding && Stamina <= 0f) gliding = false;
   if (IsGliding == gliding) return;
   IsGliding = gliding;
   if (glider) glider.SetOpen(gliding);
   if (Visual) Visual.SetGlide(gliding, glider ? glider.GripLeft : null, glider ? glider.GripRight : null);
   if (gliding) {
    sprinting = false;
    // Keep most of the horizontal momentum when the canopy opens.
    planarVelocity = Vector3.ClampMagnitude(planarVelocity, GlideSpeed);
    OpenWorldAudio.Play("sfx_gale_cast", 0.32f);
    if (Time.time >= airAnimHoldUntil) Visual?.PlayAt("glide", "slash", 1f, 0f, 0.22f);
   }
  }

  public void EquipWeapon(int weaponId) {
   if(weaponId==17)weaponId=OpenWorldWeaponCatalog.UnarmedId;
   EquippedWeaponId = Mathf.Clamp(weaponId, OpenWorldWeaponCatalog.UnarmedId, OpenWorldWeaponCatalog.All.Length - 1);
   if (Visual && Visual.RightHandBone) {
    OpenWorldWeaponCatalog.AttachToHand(Visual.RightHandBone, EquippedWeaponId, out weaponTrail);
   }
   OpenWorldManager.I?.MarkProgressDirty();
  }

  public void EquipSkin(int skinId) {
   EquippedSkinId = Mathf.Clamp(skinId, 0, OpenWorldCharacterVisual.AsterSkins.Length - 1);
   if (Visual) {
    Visual.SetAsterSkin(EquippedSkinId);
    if (Visual.RightHandBone) {
     OpenWorldWeaponCatalog.AttachToHand(Visual.RightHandBone, EquippedWeaponId, out weaponTrail);
    }
    if (IsGliding && glider) Visual.SetGlide(true, glider.GripLeft, glider.GripRight);
   }
   OpenWorldManager.I?.MarkProgressDirty();
  }

  public void CycleElement() {
   ActiveElement = (ActiveElement + 1) % 3;
   string[] names = { "EMBER [FIRE]", "FROST [ICE]", "GALE [WIND]" };
   OpenWorldManager.I?.ShowBanner("Element Attuned: " + names[ActiveElement]);
   OpenWorldAudio.Play("sfx_power_select", 0.5f);
   OpenWorldManager.I?.MarkProgressDirty();
  }

  public void RestoreElement(int element) { ActiveElement = Mathf.Clamp(element, 0, 2); }

  public void WarpTo(Vector3 destination) {
   LeaveMount(true);
   ResetTraversal();
   SetGliding(false);
   EndAction();
   dashing = false; sprinting = false;
   destination.x = Mathf.Clamp(destination.x, -WorldBound, WorldBound);
   destination.z = Mathf.Clamp(destination.z, -WorldBound, WorldBound);
   if (Controller) Controller.enabled = false;
   Physics.SyncTransforms();
   float gy = OpenWorldMaterialHelper.SampleGroundHeight(destination, transform);
   transform.position = new Vector3(destination.x, gy + 0.08f, destination.z);
   velocity = Vector3.zero;
   planarVelocity = Vector3.zero;
   Physics.SyncTransforms();
   if (Controller) Controller.enabled = true;
  }

  public void Heal(float amount) {
   if (Dead) return;
   float bonus = (OpenWorldManager.I && OpenWorldManager.I.HasRelic(0)) ? 1.25f : 1f;
   Health = Mathf.Min(MaxHealth, Health + amount * bonus);
   OpenWorldAudio.Play("sfx_heal");
  }

  public void RestoreAether(float amount) {
   if (Dead) return;
   Aether = Mathf.Min(MaxAether, Aether + amount);
  }

  static Color ElementColor(int e) => e == 0 ? new Color(1f, 0.45f, 0.14f) : e == 1 ? new Color(0.35f, 0.88f, 1f) : e == 2 ? new Color(0.35f, 1f, 0.68f) : new Color(0.92f, 0.96f, 1f);

  void Update() {
   if (IsMounted) SetGliding(false);
   var gm = OpenWorldManager.I;
   if (!gm || gm.IsModalOpen || Dead || (!Application.isFocused && !DebugAlwaysFocused && !(Application.isEditor && Application.isBatchMode))) {
    ClearInput();
    if (weaponTrail) weaponTrail.emitting = false;
    return;
   }
   float now = Time.time;

   // Passive Aether trickle (+1.5/s bonus from Skyglass Relic #3)
   float aetherRate = gm.HasRelic(3) ? 4.0f : 2.5f;
   Aether = Mathf.Min(MaxAether, Aether + aetherRate * Time.deltaTime);

   // Dawn Sigil Relic #0 passive sanctuary / out-of-combat health trickle
   if (gm.HasRelic(0) && Health < MaxHealth && Vector3.Distance(transform.position, Vector3.zero) < 28f) {
    Health = Mathf.Min(MaxHealth, Health + 0.15f * Time.deltaTime);
   }

   // Scheduled impact frames.
   for (int i = 0; i < pending.Count; i++) {
    if (pending[i].time > now) continue;
    var act = pending[i].action;
    pending.RemoveAt(i--);
    act?.Invoke();
   }
   if (weaponTrail) weaponTrail.emitting = now >= trailOnAt && now <= trailOffAt;

   // Hopping off a horse: the hop owns the body for a moment.
   if (StepDismount()) { ClearMountedInput(); return; }

   // Poll input (both keyboard/mouse and UI touch controls)
   Vector2 move = TouchMoveInput;
   if (move.sqrMagnitude < 0.01f) {
    float hx = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
    float hz = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
    move = Vector2.ClampMagnitude(new Vector2(hx, hz), 1f);
   }

   bool jumpDown = TouchJump || Input.GetKeyDown(KeyCode.Space);
   bool dodgeDown = TouchDodge || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
   sprintHeld = TouchSprintHeld || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
   if (Mount) {
    Mount.Move(move, sprintHeld);
    Mount.SeatRider(Time.deltaTime);
    if (weaponTrail) weaponTrail.emitting = false;
    Visual?.SetLocomotion(0f, false);
    Visual?.Play("idle");
    ClearMountedInput(); // not ClearInput(): that zeroed the touch stick every frame and stopped the horse
    return;
   }
   if (Input.GetKeyDown(KeyCode.H)) OpenWorldAnimalInteraction.CallHorse(this);
   bool overUI = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
   if (Input.GetMouseButtonDown(0)) mouseAttackStarted = !overUI;
   bool parryDown = TouchParry || Input.GetKeyDown(KeyCode.Q) || (!overUI && Input.GetMouseButtonDown(1));
   bool spellDown = TouchSpell || Input.GetKeyDown(KeyCode.E);
   bool attackDown = TouchAttack || Input.GetKeyDown(KeyCode.J) || (mouseAttackStarted && Input.GetMouseButtonDown(0));
   bool attackHeld = Input.GetKey(KeyCode.J) || (mouseAttackStarted && Input.GetMouseButton(0));
   bool attackUp = Input.GetKeyUp(KeyCode.J) || (mouseAttackStarted && Input.GetMouseButtonUp(0));
   if (Input.GetMouseButtonUp(0)) mouseAttackStarted = false;
   bool chargedTrigger = TouchCharged || Input.GetKeyDown(KeyCode.K);

   TouchJump = TouchDodge = TouchAttack = TouchCharged = TouchParry = TouchSpell = false;

   if (Input.GetKeyDown(KeyCode.R)) CycleElement();
   if (Input.GetKeyDown(KeyCode.Tab)) ToggleLockOn();

   // Climbing, swimming and knockdowns take the controls over (no attacks or glider there).
   if (traverse != Traverse.None) {
    TraversalInput(move, jumpDown, dodgeDown);
    StepLocomotion(move);
    return;
   }

   // Paraglider: G in the air, or jump in the air once the double jump is spent and falling.
   if (!IsMounted && !Grounded && Input.GetKeyDown(KeyCode.G)) {
    SetGliding(!IsGliding);
   } else if (!IsMounted && jumpDown) {
    if (airTime > 0.35f && !doubleJumpAvailable && velocity.y < -0.5f && now - lastGroundedTime > CoyoteWindow) {
     SetGliding(!IsGliding);
    } else {
     lastJumpPressedTime = now;
    }
   }

   if (attackHeld) chargeHoldTime += Time.deltaTime;
   if ((attackUp && chargeHoldTime >= 0.42f) || chargedTrigger) {
    chargeHoldTime = 0f;
    PerformChargedAttack();
   } else if (attackDown) {
    chargeHoldTime = 0f;
    PerformLightAttack();
   }
   if (attackUp) chargeHoldTime = 0f;
   // A press during the previous hit fires as soon as the chain window opens.
   if (bufferedAttackUntil > now && now >= chainAt && !dashing) { bufferedAttackUntil = 0f; PerformLightAttack(); }

   if (parryDown) PerformParry();
   if (dodgeDown) PerformDodge(move);
   if (spellDown) PerformSpell();
   if (TouchEcho || Input.GetKeyDown(KeyCode.C)) { TouchEcho = false; PerformEchoSkill(); }
   if (TouchBurst || Input.GetKeyDown(KeyCode.X)) { TouchBurst = false; PerformElementalBurst(); }

   StepLocomotion(move);
  }

  void ClearInput() {
   TouchMoveInput = Vector2.zero;
   TouchJump = TouchDodge = TouchAttack = TouchCharged = TouchParry = TouchSpell = TouchEcho = TouchBurst = false;
   TouchSprintHeld = false;
   chargeHoldTime = 0f;
   mouseAttackStarted = false;
   sprintHeld = false;
  }
  void OnApplicationFocus(bool focused) { if (!focused) ClearInput(); }
  void OnApplicationPause(bool paused) { if (paused) ClearInput(); }

  // CharacterController.isGrounded flickers on slopes, steps and mesh seams (and on Android devices
  // stays false while standing still on TerrainCollider); this multi-tier ground probe ensures
  // Aster is reliably grounded on mainland terrain, slopes, rocks, and floating isles.
  bool ProbeGround() {
   // 1. Direct Terrain height check: on mainland terrain, Terrain.SampleHeight provides the exact
   // surface elevation without relying on PhysX raycast/spherecast queries that fail on mobile heightfields.
   var terrain = OpenWorldManager.I ? OpenWorldManager.I.ActiveTerrain : Terrain.activeTerrain;
   if (terrain && terrain.terrainData) {
    Vector3 local = transform.position - terrain.transform.position;
    Vector3 tSize = terrain.terrainData.size;
    if (local.x >= 0f && local.z >= 0f && local.x <= tSize.x && local.z <= tSize.z) {
     float th = terrain.SampleHeight(transform.position) + terrain.transform.position.y;
     float diff = transform.position.y - th;
     if (diff >= -0.15f && diff <= 0.28f) {
      Vector3 norm = terrain.terrainData.GetInterpolatedNormal(local.x / tSize.x, local.z / tSize.z);
      if (norm.y > 0.55f) return true;
     }
    }
   }

   // 2. SphereCast for rocks, wooden decks, bridges, and floating isle mesh colliders.
   // Start sphere 0.22m above feet so it never overlaps bumps/vertices at the origin (which causes
   // PhysX SphereCast to return 0 hits), sweeping 0.42m down to 0.20m below capsule bottom.
   float r = Controller ? Controller.radius * 0.9f : 0.34f;
   float footY = transform.position.y;
   Vector3 origin = transform.position + Vector3.up * (r + 0.22f);
   int n = Physics.SphereCastNonAlloc(origin, r, Vector3.down, groundHits, 0.42f, ~0, QueryTriggerInteraction.Ignore);
   for (int i = 0; i < n; i++) {
    var h = groundHits[i];
    if (!h.collider || h.collider == Controller || h.collider.transform.IsChildOf(transform)) continue;
    // Ground surface must have walkable slope AND be physically beneath feet (not an obstacle at knee/waist height)
    if (h.normal.y > 0.55f && h.point.y >= footY - 0.25f && h.point.y <= footY + 0.12f) return true;
   }

   // 3. Fallback downward Raycast from capsule center down through feet:
   // Casts all hits to ensure the CharacterController capsule itself never blocks detection of the ground below.
   float ccCenter = Controller ? Controller.center.y : 0.89f;
   Vector3 rayOrigin = transform.position + Vector3.up * ccCenter;
   int rayCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, groundRayHits, ccCenter + 0.35f, ~0, QueryTriggerInteraction.Ignore);
   for (int i = 0; i < rayCount; i++) {
    var h = groundRayHits[i];
    if (!h.collider || h.collider == Controller || h.collider.transform.IsChildOf(transform)) continue;
    // Ground surface must be beneath feet (not a wall or ledge passed during a fall)
    if (h.normal.y > 0.55f && h.point.y >= footY - 0.25f && h.point.y <= footY + 0.12f) return true;
   }


   return false;
  }

  void EndAction() {
   actionUntil = cancelAt = chainAt = 0f;
   lungeUntil = 0f;
   actionMovable = false;
  }

  void CancelPendingStrikes() {
   pending.Clear();
   trailOnAt = trailOffAt = -1f;
  }

  void Schedule(float delay, Action action) => pending.Add((Time.time + Mathf.Max(0f, delay), action));

  void StepLocomotion(Vector2 move) {
   if (traverse != Traverse.None) { StepTraversal(move); return; }
   float dt = Mathf.Min(Time.deltaTime, 0.05f);
   float now = Time.time;
   // Rising fast means airborne, whatever the controller's last contact says (a launch into the glider
   // from the ground would otherwise read as grounded for one frame and fold the canopy).
   Grounded = velocity.y <= 0.5f && (Controller.isGrounded || ProbeGround());
   if (Grounded) {
    lastGroundedTime = now;
    doubleJumpAvailable = true;
    airDashUsed = false;
    SetGliding(false);
    if (velocity.y < 0f) velocity.y = -2f;
   }

   // Landing: a dip while running, a short recovery from high falls.
   if (Grounded && !wasGrounded && airTime > 0.2f) OnLanded(-minAirVelocity, move.sqrMagnitude > 0.05f);
   if (Grounded) { airTime = 0f; minAirVelocity = 0f; }
   else { airTime += dt; minAirVelocity = Mathf.Min(minAirVelocity, velocity.y); }
   wasGrounded = Grounded;

   // Jump with Coyote Time & Jump Buffer
   if (now - lastJumpPressedTime <= JumpBufferWindow && !dashing && !(now < actionUntil && !actionMovable && now < cancelAt)) {
    // Grounded now takes priority over the stale lastGroundedTime: a Grounded flicker on the phone's
    // terrain used to push the hero into the double-jump or glider path instead of the ground jump.
    if (Grounded || now - lastGroundedTime <= CoyoteWindow) {
     SetGliding(false);
     EndAction(); CancelPendingStrikes();
     velocity.y = JumpVelocity;
     Grounded = false; wasGrounded = false;
     lastJumpPressedTime = -10f;
     lastGroundedTime = -10f;
     Visual?.PlayAt("jump", "slash", 1f, 0f, 0.06f);
     airAnimHoldUntil = now + 0.42f;
     OpenWorldAudio.Play("sfx_jump", 0.5f);
    } else if (doubleJumpAvailable) {
     doubleJumpAvailable = false;
     SetGliding(false);
     EndAction(); CancelPendingStrikes();
     velocity.y = JumpVelocity * 0.92f;
     lastJumpPressedTime = -10f;
     Visual?.PlayAt("double_jump", "slash", 0.9f, 0f, 0.05f);
     airAnimHoldUntil = now + 0.5f;
     OpenWorldAudio.Play("sfx_double_jump", 0.5f);
    }
   }

   // Stamina: sprinting and gliding drain it, regeneration waits a moment after the last use.
   sprintHeldTime = sprintHeld ? sprintHeldTime + dt : 0f;
   if (sprinting && (!sprintHeld || move.sqrMagnitude < 0.04f || !Grounded && !dashing)) sprinting = false;
   // Still holding after a jump or an attack: pick the sprint back up.
   if (!sprinting && !dashing && sprintHeld && sprintHeldTime > 0.25f && move.sqrMagnitude > 0.09f && Grounded && now >= exhaustedUntil && Stamina > 10f && now >= actionUntil) sprinting = true;
   if (sprinting) {
    Stamina = Mathf.Max(0f, Stamina - SprintStaminaPerSecond * dt);
    staminaRegenAt = now + 0.5f;
    if (Stamina <= 0f) { sprinting = false; exhaustedUntil = now + 1.6f; }
   }
   if (IsGliding && Stamina <= 0f) SetGliding(false);
   if (IsGliding && !Grounded) {
    Stamina = Mathf.Max(0f, Stamina - 5.5f * dt);
    staminaRegenAt = now + 0.5f;
    if (Stamina <= 0f) SetGliding(false);
    if (velocity.y < 0f) velocity.y = Mathf.Max(-2.15f, velocity.y + Gravity * 0.18f * dt);
    else velocity.y += Gravity * 0.65f * dt;
   } else {
    if (Grounded && !sprinting && !dashing && now >= staminaRegenAt) Stamina = Mathf.Min(MaxStamina, Stamina + (IsMealBuffActive ? 42f : 32f) * dt);
    if (dashing && !Grounded) velocity.y = Mathf.Max(velocity.y + Gravity * 0.25f * dt, -1.5f); // air dash hangs
    else velocity.y += Gravity * dt;
   }

   Transform camT = Camera.main ? Camera.main.transform : null;
   Vector3 fwd = camT ? Vector3.ProjectOnPlane(camT.forward, Vector3.up).normalized : Vector3.forward;
   Vector3 right = camT ? Vector3.ProjectOnPlane(camT.right, Vector3.up).normalized : Vector3.right;
   if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
   Vector3 moveDir = (fwd * move.y + right * move.x);
   if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();
   bool wantsMove = moveDir.sqrMagnitude > 0.02f;

   // Movement input cancels an action's recovery (after its impact frame).
   if (now < actionUntil && wantsMove && now >= cancelAt && !dashing) { EndAction(); if (pending.Count == 0) trailOffAt = now; }
   bool inAction = now < actionUntil;
   if (animHoldUntil > now && animHoldCancelByMove && wantsMove) animHoldUntil = 0f;

   float relic = OpenWorldManager.I && OpenWorldManager.I.StormRelayAligned ? 1.12f : 1f;
   Vector3 desired;
   float accel;
   if (dashing) {
    float u = Mathf.Clamp01((now - dashStart) / dashDuration);
    float end = wantsMove ? (sprintHeld ? SprintSpeed : RunSpeed) : RunSpeed * 0.3f;
    float peak = Mathf.Max(end, 2f * dashDistance / dashDuration - end);
    // Steer the dash a little toward the stick.
    if (wantsMove) dashDir = Vector3.RotateTowards(dashDir, moveDir.normalized, Mathf.Deg2Rad * 120f * dt, 0f);
    planarVelocity = dashDir * Mathf.Lerp(peak, end, u);
    desired = planarVelocity; accel = 0f;
    if (dashDir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dashDir);
    if (afterimagesLeft > 0 && now >= nextAfterimage) {
     afterimagesLeft--;
     nextAfterimage = now + 0.07f;
     Visual?.SpawnAfterimage(ElementColor(ActiveElement) * 0.9f, 0.3f);
    }
    if (u >= 1f) {
     dashing = false;
     if (sprintHeld && wantsMove && Grounded && now >= exhaustedUntil && Stamina > 2f) sprinting = true;
    }
   } else if (inAction && !actionMovable) {
    // Attacks root the hero, except for the lunge into the strike.
    desired = now < lungeUntil ? lungeDir * lungeSpeed : Vector3.zero;
    accel = now < lungeUntil ? 80f : 30f;
    if (now < lungeUntil) planarVelocity = desired;
   } else if (IsGliding && !Grounded) {
    float glide = (OpenWorldManager.I && OpenWorldManager.I.HasRelic(3)) ? GlideSpeed * 1.2f : GlideSpeed;
    desired = wantsMove ? moveDir.normalized * glide : transform.forward * glide * 0.55f;
    accel = 6f;
   } else {
    float m = Mathf.Clamp01(move.magnitude);
    float speed = m < 0.55f ? Mathf.Lerp(0f, WalkSpeed, m / 0.55f) : (sprinting ? SprintSpeed : RunSpeed);
    desired = wantsMove ? moveDir.normalized * speed * relic : Vector3.zero;
    accel = Grounded ? (desired.sqrMagnitude > planarVelocity.sqrMagnitude ? 28f : 36f) : 9f;
    // Sharp reversals at speed shed momentum instead of sliding (a Genshin-style pivot).
    if (Grounded && wantsMove && planarVelocity.sqrMagnitude > 9f && Vector3.Dot(planarVelocity.normalized, moveDir.normalized) < -0.5f) planarVelocity *= 0.4f;
   }

   // Wading: water above the shins slows Aster, down to half speed when waist-deep.
   if (Grounded && OpenWorldRealm.Instance && OpenWorldTerrainSculptor.WaterAt(transform.position.x, transform.position.z, out float waterY, out _)) {
    float depth = waterY - transform.position.y;
    if (depth > 0.25f) desired *= Mathf.Lerp(1f, 0.5f, Mathf.InverseLerp(0.25f, 1.1f, depth));
   }
   if (!dashing && !(inAction && now < lungeUntil)) planarVelocity = Vector3.MoveTowards(planarVelocity, desired, accel * dt);

   // Facing: turn toward the stick (fast on the ground, gently in the air), or toward the lock target.
   if (!dashing && !inAction && wantsMove) {
    float turn = IsGliding ? 110f : Grounded ? (sprinting ? 560f : 760f) : 320f;
    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(IsGliding && planarVelocity.sqrMagnitude > 1f ? Vector3.Lerp(planarVelocity.normalized, moveDir.normalized, 0.5f) : moveDir.normalized), turn * dt);
   } else if (!dashing && !inAction && LockOnTarget && !LockOnTarget.Dead) {
    Vector3 toTarget = LockOnTarget.transform.position - transform.position;
    toTarget.y = 0f;
    if (toTarget.sqrMagnitude > 0.05f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toTarget.normalized), 540f * dt);
   }

   Vector3 displacement = (planarVelocity + new Vector3(0f, velocity.y, 0f)) * dt;
   // Ground steeper than the controller can climb sheds her instead of letting her cling to cliff faces.
   if (Grounded && Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var groundHit, 0.9f, ~0, QueryTriggerInteraction.Ignore)) {
    float steep = Vector3.Angle(groundHit.normal, Vector3.up);
    if (steep > Controller.slopeLimit + 3f) {
     var downhill = Vector3.ProjectOnPlane(Vector3.down, groundHit.normal).normalized;
     displacement += downhill * Mathf.Lerp(3f, 9f, Mathf.InverseLerp(Controller.slopeLimit, 75f, steep)) * dt;
    }
   }
   Vector3 beforeMove = transform.position;
   Controller.Move(displacement);
   Vector3 moved = Vector3.ProjectOnPlane(transform.position - beforeMove, Vector3.up);
   float actualSpeed = dt > 0f ? moved.magnitude / dt : 0f;
   // Walls stop the stored momentum too, so she doesn't keep "running" into them.
   if (dt > 0f && planarVelocity.sqrMagnitude > 0.01f && actualSpeed < planarVelocity.magnitude * 0.5f && !dashing) planarVelocity = Vector3.Lerp(planarVelocity, moved / dt, 0.5f);
   // Deep water, or a rock face she is pushing into, hands over to swimming or climbing.
   if (TryEnterTraversal(moveDir, wantsMove)) return;
   // Footsteps follow the animation's foot plants (OnFootPlanted); distance-based steps remain only
   // as a fallback before the first plant event.
   if (Grounded && !inAction && !plantFootsteps) {
    importedStepDistance += moved.magnitude;
    if (importedStepDistance >= (sprinting ? 1.7f : 1.45f)) {
     importedStepDistance = 0; alternateStep = !alternateStep;
     if (!OpenWorldSoundscape.Footstep(transform.position, actualSpeed > 3.2f)) OpenWorldAudio.Play(alternateStep ? "sfx_step" : "sfx_step2", .22f);
    }
   } else importedStepDistance = 0;

   // Clamp to the playable region boundary so Aster never walks off the terrain edge
   Vector3 clamped = transform.position;
   if (Mathf.Abs(clamped.x) > WorldBound || Mathf.Abs(clamped.z) > WorldBound) {
    clamped.x = Mathf.Clamp(clamped.x, -WorldBound, WorldBound);
    clamped.z = Mathf.Clamp(clamped.z, -WorldBound, WorldBound);
    transform.position = clamped;
    OpenWorldManager.I?.ShowBanner("The dense mist of the outer realm turns you back.");
   }

   // Keep Aster above the terrain floor even on steep transitions without snapping into mid-air
   var activeTerrain = OpenWorldManager.I ? OpenWorldManager.I.ActiveTerrain : Terrain.activeTerrain;
   if (activeTerrain != null) {
    float th = activeTerrain.SampleHeight(transform.position) + activeTerrain.transform.position.y;
    if (transform.position.y < th - 0.02f) {
     transform.position = new Vector3(transform.position.x, th, transform.position.z);
     Physics.SyncTransforms();
     if (velocity.y < 0f) velocity.y = -2f;
     SetGliding(false);
     Grounded = true; wasGrounded = true; airTime = 0f;
    }
   }

   UpdateAnimation(actualSpeed, wantsMove, inAction);
  }

  void UpdateAnimation(float actualSpeed, bool wantsMove, bool inAction) {
   if (!Visual) return;
   float now = Time.time;
   Visual.SetLocomotion(Grounded ? actualSpeed : planarVelocity.magnitude, sprinting || dashing);
   Visual.SetFootIk(Grounded && !dashing && !IsGliding ? (inAction ? 0.6f : 1f) : 0f);
   Visual.SetLookTarget(LockOnTarget && !LockOnTarget.Dead ? LockOnTarget.transform : null);
   OpenWorldManager.I?.CameraRig?.SetMotionFov(IsGliding ? 6f : sprinting || dashing ? 5f : 0f);
   if (inAction || dashing || now < animHoldUntil) return;
   if (IsGliding && !Grounded) {
    if (now >= airAnimHoldUntil) Visual.Play("glide");
   } else if (!Grounded) {
    if (now >= airAnimHoldUntil && airTime > 0.12f && velocity.y < 0.8f) Visual.Play("fall");
   } else {
    Visual.Play(actualSpeed > 0.15f || wantsMove ? "run" : "idle");
   }
  }

  void OnLanded(float impact, bool moving) {
   float now = Time.time;
   if (impact < 5f) return;
   OpenWorldSoundscape.Land(transform.position);
   if (impact > 16f) {
    // Heavy landing: a deep crouch and a beat of recovery.
    EndAction(); CancelPendingStrikes(); dashing = false;
    Visual?.PlayAt("land_hard", "slash", 0.9f, 0f, 0.04f);
    actionUntil = now + 0.42f; cancelAt = now + 0.3f; chainAt = now + 0.3f; actionMovable = false;
    planarVelocity *= 0.25f;
    OpenWorldManager.I?.CameraRig?.AddShake(0.1f, 0.14f);
   } else if (moving) {
    Visual?.PlayOverlay("land", 0.5f, 1.25f, 0.32f);
   } else if (!(now < actionUntil)) {
    Visual?.PlayAt("land", "slash", 1.2f, 0f, 0.04f);
    animHoldUntil = now + 0.34f; animHoldCancelByMove = true;
   }
  }

  // Bridge / debug: "spell 0", "burst 1", "attack", "charged 2" (element optional), "dash", "glide".
  public static void DebugAction(string arg) {
   var gm = OpenWorldManager.I;
   var hero = gm ? gm.Hero : null;
   if (!hero || string.IsNullOrEmpty(arg)) return;
   var p = arg.Trim().ToLowerInvariant().Split(' ');
   if (p.Length > 1 && int.TryParse(p[1], out int element)) hero.ActiveElement = Mathf.Clamp(element, 0, 2);
   hero.Aether = hero.MaxAether;
   hero.Stamina = hero.MaxStamina;
   hero.actionLockUntil = 0f;
   switch (p[0]) {
    case "spell": hero.PerformSpell(); break;
    case "burst": hero.PerformElementalBurst(); break;
    case "charged": hero.PerformChargedAttack(); break;
    case "dodge": case "dash": hero.PerformDodge(Vector2.zero); break;
    case "jump": hero.lastJumpPressedTime = Time.time; break;
    case "glide": hero.LaunchIntoGlide(14f); break;
    case "god": hero.invulnerableUntil = Time.time + 3600f; hero.Health = hero.MaxHealth; break;
    default: hero.PerformLightAttack(); break;
   }
  }

  // First baked impact frame at or after the start offset (seconds at playback rate 1).
  float FirstStrike(string state, string style, float start, out float clipLength) {
   clipLength = Visual ? Visual.GetClipLength(state, style) : 1f;
   var strikes = Visual ? Visual.GetStrikes(state, style) : new[] { 0.4f };
   foreach (var s in strikes) if (s >= start + 0.06f) return s;
   return Mathf.Max(start + 0.12f, strikes.Length > 0 ? strikes[strikes.Length - 1] : clipLength * 0.4f);
  }

  // Turns toward the nearest foe in front (within range) and returns the distance to it.
  float SoftLock(float range, float cone = 0.25f) {
   var gm = OpenWorldManager.I;
   if (!gm) return -1f;
   OpenWorldEnemy best = LockOnTarget && !LockOnTarget.Dead ? LockOnTarget : null;
   float bestScore = float.MaxValue;
   if (!best) {
    foreach (var e in gm.Enemies) {
     if (!e || !e.isActiveAndEnabled || e.Dead) continue;
     Vector3 to = e.transform.position - transform.position; to.y = 0f;
     float d = to.magnitude;
     if (d > range || d < 0.01f) continue;
     float facing = Vector3.Dot(transform.forward, to / d);
     if (facing < cone) continue;
     float score = d * (1.6f - facing);
     if (score < bestScore) { bestScore = score; best = e; }
    }
   }
   if (!best) return -1f;
   Vector3 dir = best.transform.position - transform.position; dir.y = 0f;
   if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir.normalized);
   return dir.magnitude;
  }

  void BeginAttackMotion(float strikeIn, float lunge, float stopDistance, float targetDistance) {
   float now = Time.time;
   float dist = targetDistance > 0f ? Mathf.Clamp(targetDistance - stopDistance, 0f, lunge * 1.6f) : lunge;
   lungeDir = transform.forward;
   lungeUntil = now + Mathf.Max(0.06f, strikeIn * 0.9f);
   lungeSpeed = dist / Mathf.Max(0.06f, strikeIn * 0.9f);
   planarVelocity = Vector3.zero;
   sprinting = false;
  }

  public void PerformLightAttack() {
   if (Dead) return;
   float now = Time.time;
   if (now < actionUntil && now < chainAt) { bufferedAttackUntil = Mathf.Min(now + 1.1f, Mathf.Max(now + .22f, chainAt + .16f)); return; }
   if (dashing && now - dashStart < dashDuration * 0.45f) { bufferedAttackUntil = now + 0.3f; return; }
   SetGliding(false);
   dashing = false;
   var w = OpenWorldWeaponCatalog.Get(EquippedWeaponId);
   string style = Combos.ContainsKey(w.Style) ? w.Style : "slash";
   var chain = Combos[style];
   bool isCounter = IsCounterReady;
   counterReadyUntil = 0f;

   if (now > comboResetAt) comboStep = 0;
   var hit = chain[comboStep % chain.Length];
   int stepIndex = comboStep % chain.Length;
   comboStep = stepIndex + 1;

   float strike = FirstStrike(hit.state, style, hit.start, out float clipLength);
   float start = Mathf.Min(hit.start, Mathf.Max(0f, strike - 0.12f));
   float tempo = Mathf.Clamp(w.Tempo, 0.75f, 1.35f);
   float sp = hit.speed * Mathf.Lerp(1f, tempo, 0.5f);
   float strikeIn = (strike - start) / sp;
   CancelPendingStrikes();
   Visual?.PlayAt(hit.state, style, sp, start, 0.08f);
   float targetDistance = SoftLock(5.5f);
   BeginAttackMotion(strikeIn, hit.lunge * (isCounter ? 2f : 1f), w.Reach * 0.6f + 0.6f, targetDistance);
   actionUntil = now + Mathf.Min((clipLength - start) / sp, strikeIn + (style == "chop" || hit.heavy ? .62f : .46f));
   chainAt = now + strikeIn + 0.1f;
   cancelAt = now + strikeIn + (hit.heavy ? .28f : style == "chop" ? .22f : .16f);
   actionMovable = false;
   comboResetAt = now + strikeIn + (stepIndex == chain.Length - 1 ? 0.45f : 0.9f);
   if (style == "chop" || isCounter || hit.heavy) hyperArmorUntil = now + strikeIn + 0.1f;
   trailOnAt = now + Mathf.Max(0f, strikeIn - 0.13f);
   trailOffAt = now + strikeIn + 0.1f;
   int stepNumber = stepIndex + 1;
   Schedule(Mathf.Max(0f, strikeIn - 0.08f), () => OpenWorldAudio.Play("sfx_attack_" + style + "_" + Mathf.Min(3, stepNumber), 0.5f, UnityEngine.Random.Range(.96f, 1.04f), 1));

   float rankBonus = OpenWorldManager.I ? OpenWorldManager.I.AdventurerDamageMultiplier : 1f;
   float dmg = w.Damage * rankBonus * hit.damage * (isCounter ? 1.85f : 1f);
   float reach = w.Reach * (isCounter ? 3.2f : 2.5f) * hit.reach;
   int elem = isCounter ? ActiveElement : w.ElementAffinity;
   bool heavy = isCounter || hit.heavy;
   bool pole = style == "spear";
   Schedule(strikeIn, () => {
    if (Dead) return;
    OpenWorldSlashWave.SpawnSlash(transform.position + transform.forward * 1.1f + Vector3.up * 0.95f, transform.rotation, ElementColor(elem), reach * 0.85f, heavy);
    if (isCounter) {
     OpenWorldManager.I?.SpawnBurst(transform.position + transform.forward * 1.2f + Vector3.up * 0.9f, new Color(1f, 0.88f, 0.32f), 20);
     OpenWorldCombatPopup.Spawn(transform.position + Vector3.up * 2.1f, "COUNTERSTRIKE!", new Color(1f, 0.9f, 0.32f), 1.25f);
     OpenWorldManager.I?.CameraRig?.AddShake(0.16f, 0.18f);
    }
    // Light hits shove; finishers and counters knock foes back (axes pop them up a little).
    float push = isCounter ? 8f : heavy ? (pole ? 9f : 7f) : 2.4f;
    float lift = isCounter ? 4f : heavy && style == "chop" ? 2.5f : 0f;
    if (StrikeNearbyEnemies(dmg, reach, heavy, pole, elem, push, lift) > 0) {
     Visual?.TriggerHitStop(heavy ? 0.07f : 0.045f);

    }
   });
  }

  public void PerformChargedAttack() {
   if (Dead) return;
   float now = Time.time;
   if (now < actionUntil && now < chainAt) return;
   SetGliding(false);
   var w = OpenWorldWeaponCatalog.Get(EquippedWeaponId);
   if (Aether < 15f) {
    OpenWorldManager.I?.ShowBanner("Need 15 Aether for Charged Hold Attack!");
    return;
   }
   Aether -= 15f;
   dashing = false;
   CancelPendingStrikes();
   string style = Combos.ContainsKey(w.Style) ? w.Style : "slash";
   float start = style == "slash" ? 0.25f : style == "chop" ? 0f : 0.05f;
   float sp = style == "chop" ? 1.15f : style == "spear" ? 0.95f : style == "unarmed" ? 1.1f : 1.25f;
   float strike = FirstStrike("charged", style, start, out float clipLength);
   float strikeIn = (strike - start) / sp;
   Visual?.PlayAt("charged", style, sp, start, 0.1f);
   float targetDistance = SoftLock(6f);
   BeginAttackMotion(strikeIn, style == "spear" ? 4.2f : style == "unarmed" ? 1.6f : 0.8f, 1.2f, targetDistance);
   OpenWorldAudio.Play("sfx_attack_charged_1");

   float rankBonus = OpenWorldManager.I ? OpenWorldManager.I.AdventurerDamageMultiplier : 1f;
   float relicBonus = (OpenWorldManager.I && OpenWorldManager.I.HasRelic(1)) ? 1.2f : 1f;
   float radius = w.Rarity == OWWeaponRarity.Epic ? 4.8f : 3.6f;
   float dmg = w.Damage * rankBonus * relicBonus * (w.Rarity == OWWeaponRarity.Epic ? 2.6f : 1.9f);
   int elem = w.ElementAffinity >= 0 ? w.ElementAffinity : ActiveElement;
   Color burstCol = ElementColor(elem);

   // Spinning claymore charge: several hits over two turns; everything else lands once.
   var hitTimes = new List<float> { strikeIn };
   float total = Mathf.Min((clipLength - start) / sp, strikeIn + 0.7f);
   if (style == "chop" && Visual) {
    hitTimes.Clear();
    var strikes = Visual.GetStrikes("charged", style);
    float loopLen = clipLength / sp;
    for (int turn = 0; turn < 2; turn++) foreach (var s in strikes) hitTimes.Add(turn * loopLen + s / sp);
    if (hitTimes.Count == 0) hitTimes.Add(0.4f);
    total = 2f * loopLen;
    radius *= 0.9f;
   }
   actionUntil = now + total;
   chainAt = now + total;
   cancelAt = now + (style == "chop" ? total * 0.85f : strikeIn + 0.3f);
   hyperArmorUntil = now + total;
   trailOnAt = now + Mathf.Max(0f, hitTimes[0] - 0.12f);
   trailOffAt = now + hitTimes[hitTimes.Count - 1] + 0.1f;
   for (int i = 0; i < hitTimes.Count; i++) {
    bool last = i == hitTimes.Count - 1;
    float share = style == "chop" ? (last ? 0.45f : 0.55f / Mathf.Max(1, hitTimes.Count - 1)) : 1f;
    Schedule(hitTimes[i], () => {
     if (Dead) return;
     OpenWorldSlashWave.SpawnSlash(transform.position + transform.forward * 1.3f + Vector3.up * 0.85f, transform.rotation, burstCol, radius, true);
     if (last) {
      OpenWorldManager.I?.SpawnBurst(transform.position + transform.forward * 1.6f + Vector3.up * 0.8f, burstCol, 22);

     }
     // Charged finishers launch: a rising cut, the spin's last turn, a driving thrust.
     float push = last ? (style == "spear" ? 9f : 6f) : 2.6f;
     float lift = last ? (style == "spear" ? 2f : 5.5f) : 0f;
     if (StrikeNearbyEnemies(dmg * share, radius, last, style == "spear", elem, push, lift) > 0) Visual?.TriggerHitStop(last ? 0.08f : 0.035f);
     if (last && w.Id == 10) RestoreAether(8f); // Soulreaper passive
    });
   }
  }

  public void PerformParry() {
   float now = Time.time;
   if (Dead || now < actionUntil && now < cancelAt) return;
   SetGliding(false);
   EndAction(); CancelPendingStrikes(); dashing = false;
   actionUntil = now + 0.45f; cancelAt = now + 0.3f; chainAt = now + 0.2f;
   float extraWindow = (OpenWorldManager.I && OpenWorldManager.I.HasRelic(2)) ? 0.12f : 0f;
   parryWindowUntil = now + 0.38f + extraWindow;
   Visual?.Play("block", true);

   // Immediate parry check against telegraphing foes within 3.4m
   var gm = OpenWorldManager.I;
   if (!gm) return;
   foreach (var e in gm.Enemies) {
    if (!e || !e.isActiveAndEnabled || e.Dead) continue;
    if (e.Telegraphing && Vector3.Distance(transform.position, e.transform.position) <= 3.4f) {
     e.Stun(1.8f);
     RestoreAether(20f);
     counterReadyUntil = now + 2.6f;
     gm.ShowBanner("PERFECT PARRY! (+20 Aether • Counterstrike Ready!)");
     if (!OpenWorldVfx.Parry(e.transform.position + Vector3.up * 1.1f)) gm.SpawnBurst(e.transform.position + Vector3.up * 1.1f, new Color(1f, 0.9f, 0.35f), 16);
     gm.CameraRig?.AddShake(0.18f, 0.2f);
    }
   }
  }

  // Dash (Genshin / Wuthering Waves): a short burst with i-frames and afterimages. Holding the key or
  // button afterwards keeps sprinting. It cancels attacks and their recovery at any point.
  public void PerformDodge(Vector2 moveInput) {
   if (Dead) return;
   float now = Time.time;
   if (dashing || now < dashReadyAt) return;
   if (now < actionUntil && CurrentActionIsHeavyLanding()) return;
   bool air = !Grounded;
   if (air && (airDashUsed || IsGliding && airTime < 0.1f)) return;
   float cost = air ? AirDashStamina : DashStamina;
   if (Stamina < cost * 0.5f) { OpenWorldCombatPopup.Spawn(transform.position + Vector3.up * 2f, "Out of stamina", new Color(0.9f, 0.9f, 0.6f), 0.8f); return; }
   Stamina = Mathf.Max(0f, Stamina - cost);
   staminaRegenAt = now + 0.6f;
   SetGliding(false);
   EndAction(); CancelPendingStrikes();

   Vector3 dir = transform.forward;
   if (moveInput.sqrMagnitude > 0.05f && Camera.main) {
    Vector3 fwd = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
    Vector3 right = Vector3.ProjectOnPlane(Camera.main.transform.right, Vector3.up).normalized;
    dir = (fwd * moveInput.y + right * moveInput.x).normalized;
   }
   dir.y = 0f;
   if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
   dashDir = dir.normalized;
   transform.rotation = Quaternion.LookRotation(dashDir);
   dashing = true;
   dashStart = now;
   dashDuration = air ? AirDashTime : DashTime;
   dashDistance = air ? AirDashDistance : DashDistance;
   dashReadyAt = now + dashDuration + 0.12f;
   invulnerableUntil = Mathf.Max(invulnerableUntil, now + 0.26f);
   afterimagesLeft = 4;
   nextAfterimage = now;
   if (air) { airDashUsed = true; velocity.y = Mathf.Max(velocity.y, 0.8f); }
   Visual?.PlayAt("dash", "slash", 1.35f, 0.08f, 0.04f);
   Visual?.AddLean(14f);
   OpenWorldAudio.Play("sfx_player_dash", 0.5f);

   // Wuthering Waves style "Extreme Evasion" check when dodging near a telegraphing enemy
   var gm = OpenWorldManager.I;
   if (gm) {
    foreach (var e in gm.Enemies) {
     if (!e || !e.isActiveAndEnabled || e.Dead) continue;
     if (e.Telegraphing && Vector3.Distance(transform.position, e.transform.position) <= 4.0f) {
      invulnerableUntil = now + 0.85f;
      counterReadyUntil = now + 2.6f;
      RestoreAether(18f);
      if (!OpenWorldVfx.Evade(transform.position + Vector3.up * 0.9f)) gm.SpawnBurst(transform.position + Vector3.up * 0.9f, new Color(0.35f, 0.95f, 1f), 18);
      OpenWorldCombatPopup.Spawn(transform.position + Vector3.up * 2.0f, "EXTREME EVASION!", new Color(0.38f, 0.96f, 1f), 1.2f);
      gm.ShowBanner("EXTREME EVASION! (+18 Aether • Attack now for Phantom Counterstrike!)");
      break;
     }
    }
   }
  }

  bool CurrentActionIsHeavyLanding() => Visual && Visual.CurrentState == "land_hard";

  static readonly string[] CastStates = { "cast_ember", "cast_frost", "cast_gale" };

  public void PerformSpell() {
   if (Dead) return;
   float now = Time.time;
   if (now < actionUntil && now < chainAt) return;
   SetGliding(false);
   if (Aether < 25f) {
    OpenWorldManager.I?.ShowBanner("Not enough Aether! Collect a purple Aether Potion.");
    return;
   }
   Aether -= 25f;
   dashing = false;
   CancelPendingStrikes();
   int element = ActiveElement;
   string state = CastStates[element];
   float start = element == 1 ? 0.12f : 0.05f;
   float sp = element == 2 ? 1.05f : 1.15f;
   float strike = FirstStrike(state, "slash", start, out float clipLength);
   float strikeIn = (strike - start) / sp;
   Visual?.PlayAt(state, "slash", sp, start, 0.1f);
   SoftLock(7f);
   BeginAttackMotion(strikeIn, element == 2 ? 0.6f : 0.2f, 2f, -1f);
   actionUntil = now + Mathf.Min((clipLength - start) / sp, strikeIn + 0.5f);
   chainAt = now + strikeIn + 0.18f;
   cancelAt = now + strikeIn + 0.22f;
   OpenWorldAudio.Play(element == 0 ? "sfx_ember_cast" : element == 1 ? "sfx_frost_cast" : "sfx_gale_cast");

   Schedule(strikeIn, () => {
    if (Dead) return;
    Color[] elemColors = { new Color(1f, 0.38f, 0.12f), new Color(0.32f, 0.85f, 1f), new Color(0.35f, 1f, 0.62f) };
    if (!OpenWorldVfx.Spell(element, transform.position + Vector3.up * 0.05f, 5.2f, transform.rotation)) {
     OpenWorldImportedContent.Emit(element == 0 ? "impact_ember" : element == 1 ? "impact_frost" : "impact_gale", transform.position + transform.forward * 2f + Vector3.up);
     OpenWorldSlashWave.SpawnSlash(transform.position + Vector3.up * 0.9f, transform.rotation, elemColors[element], 4.6f, true);
     OpenWorldManager.I?.SpawnBurst(transform.position + Vector3.up * 1f, elemColors[element], 26);
    }
    OpenWorldManager.I?.CameraRig?.AddShake(0.20f, 0.22f);
    float rankBonus = OpenWorldManager.I ? OpenWorldManager.I.AdventurerDamageMultiplier : 1f;
    float relicBonus = (OpenWorldManager.I && OpenWorldManager.I.HasRelic(1)) ? 1.2f : 1f;
    float cryptBonus = (OpenWorldManager.I && OpenWorldManager.I.CryptCompleted) ? 1.15f : 1f;
    // Ember blasts outward, frost barely moves them, gale throws them up on the wind.
    float push = element == 0 ? 7f : element == 1 ? 3f : 3.5f;
    float lift = element == 0 ? 4.5f : element == 1 ? 0f : 9.5f;
    if (StrikeNearbyEnemies(3.2f * rankBonus * relicBonus * cryptBonus, 5.2f, true, false, element, push, lift, true) > 0) Visual?.TriggerHitStop(0.05f);
    if (OpenWorldManager.I) {
     foreach (var f in OpenWorldManager.I.SanctumFoci) {
      if (f && !f.Attuned && Vector3.Distance(transform.position, f.transform.position) <= 5.2f) {
       OpenWorldManager.I.AttuneSanctumFocus(f.FocusIndex);
      }
     }
    }
   });
  }

  public void PerformEchoSkill() {
   float now = Time.time;
   if (now < echoCooldownUntil || now < actionUntil && now < chainAt) return;
   echoCooldownUntil = now + 3.5f;
   actionUntil = now + 0.42f; cancelAt = now + 0.3f; chainAt = now + 0.3f;
   OpenWorldEchoSpirit.Summon(this);
   float rankBonus = OpenWorldManager.I ? OpenWorldManager.I.AdventurerDamageMultiplier : 1f;
   float mealBonus = IsMealBuffActive ? 1.15f : 1f;
   StrikeNearbyEnemies(3.6f * rankBonus * mealBonus, 5.4f, true, false, ActiveElement, 5f, 2f, true);
  }

  public void PerformElementalBurst() {
   if (Dead) return;
   float now = Time.time;
   if (now < actionUntil && now < chainAt) return;
   if (Aether < 35f) {
    OpenWorldManager.I?.ShowBanner("Elemental Burst requires 35 Aether! Land attacks or drink an Aether Elixir.");
    return;
   }
   Aether -= 35f;
   SetGliding(false);
   dashing = false;
   CancelPendingStrikes();
   int element = ActiveElement;
   // Overhead two-handed slam: a slower wind-up for the close-up, the blade lands on the impact frame.
   const float start = 0.05f, sp = 0.9f;
   float strike = FirstStrike("burst", "slash", start, out float clipLength);
   float strikeIn = (strike - start) / sp;
   Visual?.PlayAt("burst", "slash", sp, start, 0.1f);
   SoftLock(8f, -0.2f);
   BeginAttackMotion(strikeIn, 1.2f, 2f, -1f);
   actionUntil = now + Mathf.Min((clipLength - start) / sp, strikeIn + 0.55f);
   chainAt = now + strikeIn + 0.3f;
   cancelAt = now + strikeIn + 0.35f;
   invulnerableUntil = Mathf.Max(invulnerableUntil, now + strikeIn + 0.3f);
   hyperArmorUntil = now + strikeIn + 0.3f;
   OpenWorldManager.I?.CameraRig?.BurstShot(transform, strikeIn + 0.25f);

   string[] burstNames = { "ELEMENTAL BURST: PHOENIX STARFALL!", "ELEMENTAL BURST: ABSOLUTE ZERO!", "ELEMENTAL BURST: TEMPEST VORTEX!" };
   Color[] elemColors = { new Color(1f, 0.42f, 0.14f), new Color(0.32f, 0.88f, 1f), new Color(0.32f, 1f, 0.72f) };
   Color c = elemColors[element];
   OpenWorldAudio.Play(element == 0 ? "sfx_ember_cast" : element == 1 ? "sfx_frost_cast" : "sfx_gale_cast");
   OpenWorldCombatPopup.Spawn(transform.position + Vector3.up * 2.4f, burstNames[element], c, 1.35f);
   OpenWorldManager.I?.ShowBanner(burstNames[element]);

   Schedule(strikeIn, () => {
    if (Dead) return;
    if (!OpenWorldVfx.Ultimate(element, transform.position + Vector3.up * 0.05f, 6.8f, transform.rotation)) {
     OpenWorldImportedContent.Emit(element == 0 ? "impact_ember" : element == 1 ? "impact_frost" : "impact_gale", transform.position + transform.forward * 2f + Vector3.up);
     OpenWorldSlashWave.SpawnSlash(transform.position + Vector3.up * 0.6f, transform.rotation, c, 6.2f, true);
     OpenWorldSlashWave.SpawnSlash(transform.position + Vector3.up * 1.2f, transform.rotation * Quaternion.Euler(0f, 90f, 0f), c, 5.2f, true);
     OpenWorldManager.I?.SpawnBurst(transform.position + Vector3.up * 1.2f, c, 34);
    }
    OpenWorldManager.I?.CameraRig?.AddShake(0.3f, 0.3f);
    float rankBonus = OpenWorldManager.I ? OpenWorldManager.I.AdventurerDamageMultiplier : 1f;
    float relicBonus = (OpenWorldManager.I && OpenWorldManager.I.HasRelic(1)) ? 1.2f : 1f;
    float cryptBonus = (OpenWorldManager.I && OpenWorldManager.I.CryptCompleted) ? 1.15f : 1f;
    float mealBonus = IsMealBuffActive ? 1.15f : 1f;
    if (StrikeNearbyEnemies(4.6f * rankBonus * relicBonus * cryptBonus * mealBonus, 6.8f, true, false, element, 8.5f, 6.5f, true) > 0) Visual?.TriggerHitStop(0.09f);
   });
  }

  // push / lift: knockback and launch speeds (m/s) given to struck foes and clutter; radial blows
  // (spells, bursts) throw them outward from Aster, weapon swings along her facing.
  int StrikeNearbyEnemies(float baseDamage, float reach, bool heavy, bool poleTipper, int element = -1, float push = -1f, float lift = 0f, bool radial = false) {
   var gm = OpenWorldManager.I;
   if (!gm) return 0;
   if (push < 0f) push = heavy ? 6.5f : 2.4f;
   if (radial) OpenWorldPhysicsProps.Explode(transform.position, reach, push, lift, heavy);
   else OpenWorldPhysicsProps.Strike(transform.position, transform.forward, reach, push, lift, heavy);
   OpenWorldEnemy closestHit = null;
   float closestDist = float.MaxValue;
   int hits = OpenWorldAnimalInteraction.Strike(transform.position, transform.forward, reach, baseDamage, radial || heavy);

   for (int i = gm.Enemies.Count - 1; i >= 0; i--) {
    var e = gm.Enemies[i];
    if (!e || !e.isActiveAndEnabled || e.Dead) continue;
    Vector3 toE = e.transform.position - transform.position;
    toE.y = 0f;
    float d = toE.magnitude;
    if (d > reach) continue;
    if (!radial && d > 1.2f && Vector3.Dot(transform.forward, toE.normalized) < (heavy ? -.15f : .05f)) continue;
    if (!OpenWorldCombatFeel.ClearHit(transform.position, e, transform)) continue;

    float mult = 1f;
    if (poleTipper && d >= reach * 0.62f) mult = 1.3f;
    e.TakeHit(baseDamage * mult, transform.position, heavy, element, push * mult, lift);
    Aether = Mathf.Min(MaxAether, Aether + 6f);
    hits++;
    if (d < closestDist) { closestDist = d; closestHit = e; }
   }
   OpenWorldCombatFeel.Confirm(this, heavy, hits);
   return hits;
  }

  // knockback: metres of shove for a normal hit; knockdown: heavy blows (slams) throw her to the ground.
  public void TakeDamage(float dmg, Transform attacker, float knockback = 0.7f, bool knockdown = false) {
   float now = Time.time;
   if (Dead || now < invulnerableUntil) return;
   if (now < parryWindowUntil && attacker) {
    var foe = attacker.GetComponent<OpenWorldEnemy>();
    if (foe) foe.Stun(1.8f);
    RestoreAether(20f);
    counterReadyUntil = now + 2.6f;
    OpenWorldManager.I?.ShowBanner("PARRIED! (+20 Aether • Counterstrike Ready!)");
    return;
   }

   SetGliding(false);
   float mitigation = (OpenWorldManager.I && OpenWorldManager.I.HasRelic(2)) ? 0.85f : 1f;
   float finalDmg = dmg * mitigation;
   LeaveMount(true);
   Health = Mathf.Max(0f, Health - finalDmg);
   OpenWorldAudio.Play("sfx_hurt", 0.6f, 1f, 3);
   invulnerableUntil = now + 0.65f;
   OpenWorldCombatPopup.Spawn(transform.position + Vector3.up * 1.9f, "-" + Mathf.CeilToInt(finalDmg) + " HP", new Color(1f, 0.32f, 0.32f), 1.1f);

   if (Health <= 0f) {
    ResetTraversal();
    EndAction(); CancelPendingStrikes(); dashing = false; sprinting = false;
    Visual?.Play("death", true);
    OpenWorldManager.I?.OnHeroDefeated();
    return;
   }

   Vector3 away = attacker ? Vector3.ProjectOnPlane(transform.position - attacker.position, Vector3.up) : -transform.forward;
   away = away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;
   if (IsClimbing) { ExitClimb(true); planarVelocity = away * 2f; }
   if (IsSwimming) { planarVelocity += away * 2.5f; return; }
   if (now >= hyperArmorUntil) {
    if (knockdown && Grounded && BeginKnockdown(away)) return;
    EndAction(); CancelPendingStrikes(); dashing = false; sprinting = false;
    actionUntil = now + 0.32f; cancelAt = now + 0.26f; chainAt = now + 0.26f;
    // The old hit_2/hit_3 retargets fold both legs into the torso.
    string[] hitStates = { "hit_4" };
    Visual?.Play(hitStates[UnityEngine.Random.Range(0, hitStates.Length)], true);
    if (attacker) Visual?.TriggerFlinch(attacker.position, 1.1f);
    // The blow shoves her back (a heavy one further) before she recovers.
    float shove = knockdown ? knockback * 2.2f : knockback;
    lungeDir = away; lungeUntil = now + 0.16f; lungeSpeed = shove / 0.16f;
    if (knockdown) velocity.y = Mathf.Max(velocity.y, 3f);
    OpenWorldManager.I?.CameraRig?.AddShake(0.16f, 0.16f);
   } else if (attacker) {
    Visual?.TriggerFlinch(attacker.position, 0.5f);
   }
  }

  public void ReviveAt(Vector3 respawnPos) {
   ResetTraversal();
   SetGliding(false);
   EndAction(); CancelPendingStrikes();
   Health = MaxHealth;
   Aether = MaxAether;
   Stamina = MaxStamina;
   WarpTo(respawnPos);
   Visual?.Play("idle", true);
   OpenWorldAudio.Play("sfx_respawn");
  }

  public void ToggleLockOn() {
   var gm = OpenWorldManager.I;
   if (!gm) return;
   if (LockOnTarget && !LockOnTarget.Dead) {
    LockOnTarget = null;
    return;
   }
   OpenWorldEnemy best = null;
   float bestD = 18f;
   foreach (var e in gm.Enemies) {
    if (!e || !e.isActiveAndEnabled || e.Dead) continue;
    float d = Vector3.Distance(transform.position, e.transform.position);
    if (d < bestD) { bestD = d; best = e; }
   }
   LockOnTarget = best;
  }
 }
}



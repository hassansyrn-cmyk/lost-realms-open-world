using UnityEngine;

namespace LostRealmsOW {
 // Aster's traversal and physical reactions (Genshin / Zelda / Wuthering Waves):
 //  - Climbing: push into any rock face or wall steeper than ~54 degrees to grab it. Move along it on
 //    stamina, climb-jump up (Jump), leap away (Jump while pulling back), let go (Dash), vault over the
 //    top edge; low walls and crates are vaulted straight from the ground.
 //  - Swimming: deep lakes and rivers float her at the surface with a breaststroke; hold Dash for a fast
 //    stroke. Running out of stamina in the water sends her back to the last safe shore.
 //  - Knockdowns: heavy blows throw her down, she gets back up with a moment of invulnerability; Dash
 //    during the get-up turns it into a recovery roll.
 //  - Clutter: walking or sprinting into barrels and crates pushes them, a dash smashes the fragile ones.
 public sealed partial class OpenWorldHero {
  enum Traverse { None, Climb, ClimbJump, Mantle, Swim, Knockdown }
  Traverse traverse;
  public bool IsClimbing => traverse == Traverse.Climb || traverse == Traverse.ClimbJump || traverse == Traverse.Mantle;
  public bool IsSwimming => traverse == Traverse.Swim;
  public bool IsKnockedDown => traverse == Traverse.Knockdown;

  const float ClimbSpeed = 1.65f, ClimbStaminaPerSecond = 9f, ClimbJumpStamina = 20f, ClimbGrabStamina = 4f;
  const float ClimbMinAngle = 54f;   // slopes steeper than this (from horizontal) are climbed, not walked
  const float ChestHeight = 1.15f;
  Vector3 wallNormal = Vector3.back;
  Vector3 wallContactNormal;
  float wallContactAt = -10f, wallPushTime, climbRegrabAt;
  float traverseStart, traverseLength;
  Vector3 mantleFrom, mantleTo;
  Vector3 climbJumpDir;
  string climbState;

  const float SwimSpeed = 2.3f, FastSwimSpeed = 4.3f, SwimEnterDepth = 1.32f, SwimExitDepth = 1.12f, SwimBodyDepth = 1.14f;
  float swimSurface, drownAt = -1f, nextRipple;
  Vector3 lastSafeGround;
  float nextSafeGroundCheck;
  bool hasSafeGround;

  float knockGetUpAt, knockEndAt;
  bool knockRising;

  bool plantFootsteps;
  float lastFeetY = float.NaN, entrySplashAt = -10f;
  readonly RaycastHit[] traverseHits = new RaycastHit[8];
  readonly Collider[] traverseOverlap = new Collider[8];

  static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero; }

  // ---------------------------------------------------------------- hooks from the controller

  void HookTraversalEvents() {
   if (!Visual) return;
   Visual.FootPlanted -= OnFootPlanted;
   Visual.FootPlanted += OnFootPlanted;
   Visual.SwimStroke -= OnSwimStroke;
   Visual.SwimStroke += OnSwimStroke;
  }

  // Footsteps land when the animation plants a foot, at that foot.
  void OnFootPlanted(bool left) {
   plantFootsteps = true;
   if (!Grounded || traverse != Traverse.None || dashing || Time.time < actionUntil) return;
   Vector3 foot = Visual ? Visual.FootPosition(left) : transform.position;
   float speed = planarVelocity.magnitude;
   if (speed < 0.6f) return;
   if (!OpenWorldSoundscape.Footstep(foot, speed > 3.2f)) OpenWorldAudio.Play(left ? "sfx_step" : "sfx_step2", .22f);
  }

  // Each stroke: the hands part the surface ahead and a ring spreads from the shoulders.
  void OnSwimStroke() {
   if (traverse != Traverse.Swim) return;
   Vector3 p = new Vector3(transform.position.x, swimSurface, transform.position.z) + transform.forward * 0.5f;
   OpenWorldSoundscape.Sfx("step_water_run", p, 0.3f);
   OpenWorldWaterFx.Ripple(p, 1.7f, 0.85f);
   if (Visual) {
    for (int i = 0; i < 2; i++) {
     Vector3 hand = Visual.HandPosition(i == 0);
     OpenWorldWaterFx.Splash(new Vector3(hand.x, swimSurface, hand.z), 0.22f);
    }
   }
  }

  void OnControllerColliderHit(ControllerColliderHit hit) {
   var c = hit.collider;
   if (!c || c.isTrigger || Mathf.Abs(hit.normal.y) > 0.62f) return; // floors and ceilings
   OpenWorldPhysicsProps.Shove(hit, planarVelocity, dashing);
   if (c is CharacterController || hit.rigidbody && !hit.rigidbody.isKinematic) return;
   wallContactAt = Time.time;
   wallContactNormal = hit.normal;
  }

  void ResetTraversal() {
   if (traverse == Traverse.Mantle && Controller) Controller.enabled = true;
   traverse = Traverse.None;
   drownAt = -1f;
   knockRising = false;
   if (Visual) { Visual.SetClimb(false, wallNormal); Visual.SetSwim(false, 0f); }
  }

  // Input while climbing, swimming or knocked down. Returns true when the controller should skip the
  // normal combat and glider input this frame.
  bool TraversalInput(Vector2 move, bool jumpDown, bool dodgeDown) {
   if (traverse == Traverse.None) return false;
   float now = Time.time;
   switch (traverse) {
    case Traverse.Climb:
     if (dodgeDown) { ExitClimb(true); climbRegrabAt = now + 0.55f; }
     else if (jumpDown) ClimbJump(move);
     break;
    case Traverse.Knockdown:
     // Recovery roll out of the get-up.
     if (dodgeDown && knockRising && now >= knockGetUpAt + 0.12f) {
      traverse = Traverse.None;
      EndAction();
      PerformDodge(move);
     }
     break;
   }
   return true;
  }

  // Runs instead of the normal locomotion while a traversal state is active.
  void StepTraversal(Vector2 move) {
   switch (traverse) {
    case Traverse.Climb: StepClimb(move); break;
    case Traverse.ClimbJump: StepClimbJump(move); break;
    case Traverse.Mantle: StepMantle(); break;
    case Traverse.Swim: StepSwim(move); break;
    case Traverse.Knockdown: StepKnockdown(); break;
   }
  }

  // Called by the locomotion step after the controller moved; may hand over to a traversal state.
  bool TryEnterTraversal(Vector3 moveDir, bool wantsMove) {
   WaterEntrySplash();
   if (CheckSwim()) return true;
   if (TryStartClimb(moveDir, wantsMove)) return true;
   if (Grounded && Time.time >= nextSafeGroundCheck) {
    nextSafeGroundCheck = Time.time + 0.5f;
    if (!OpenWorldRealm.Instance || !OpenWorldTerrainSculptor.WaterAt(transform.position.x, transform.position.z, out _, out float d) || d < 0.4f) {
     lastSafeGround = transform.position; hasSafeGround = true;
    }
   }
   return false;
  }

  // ---------------------------------------------------------------- climbing

  bool Climbable(in RaycastHit h) {
   if (!h.collider || h.collider.isTrigger || h.collider is CharacterController) return false;
   if (h.rigidbody && !h.rigidbody.isKinematic) return false;
   if (h.collider.transform.IsChildOf(transform)) return false;
   float angle = Vector3.Angle(h.normal, Vector3.up);
   return angle >= ClimbMinAngle && angle <= 140f;
  }

  // Rock in front of a point at the given height above the feet, along dir.
  bool ProbeWall(Vector3 dir, float height, float range, out RaycastHit best) {
   best = default;
   Vector3 origin = transform.position + Vector3.up * height - dir * 0.3f;
   int n = Physics.SphereCastNonAlloc(origin, 0.1f, dir, traverseHits, range + 0.3f, ~0, QueryTriggerInteraction.Ignore);
   float d = float.MaxValue; bool found = false;
   for (int i = 0; i < n; i++) {
    var h = traverseHits[i];
    if (h.distance <= 0f || !Climbable(h)) continue;
    if (h.distance < d) { d = h.distance; best = h; found = true; }
   }
   if (found) best.distance -= 0.3f;
   return found;
  }

  bool TryStartClimb(Vector3 moveDir, bool wantsMove) {
   float now = Time.time;
   if (now - wallContactAt > 0.12f || !wantsMove) { wallPushTime = 0f; return false; }
   if (now < climbRegrabAt || now < actionUntil || dashing && Grounded || Dead) return false;
   Vector3 flatN = Flat(wallContactNormal);
   if (flatN == Vector3.zero || Vector3.Dot(moveDir.normalized, -flatN) < 0.55f) { wallPushTime = 0f; return false; }
   // Ledges up to chest height are vaulted rather than climbed.
   if (Grounded && TryMantle(-flatN, true)) return true;
   if (Stamina < ClimbGrabStamina || now < exhaustedUntil) return false;
   if (!ProbeWall(-flatN, ChestHeight, 0.95f, out var hit)) return false;
   if (Grounded) {
    wallPushTime += Time.deltaTime;
    if (wallPushTime < 0.14f) return false;
   }
   wallPushTime = 0f;
   BeginClimb(hit.normal);
   return true;
  }

  void BeginClimb(Vector3 normal) {
   float now = Time.time;
   traverse = Traverse.Climb;
   wallNormal = normal;
   SetGliding(false);
   EndAction(); CancelPendingStrikes();
   dashing = false; sprinting = false;
   velocity = Vector3.zero; planarVelocity = Vector3.zero;
   Grounded = false;
   Stamina = Mathf.Max(0f, Stamina - ClimbGrabStamina);
   staminaRegenAt = now + 0.6f;
   climbState = null;
   Visual?.SetClimb(true, wallNormal);
   Visual?.SetFootIk(0f);
   OpenWorldSoundscape.Land(transform.position + Vector3.up * ChestHeight - Flat(wallNormal) * 0.4f);
  }

  void ExitClimb(bool falling) {
   traverse = Traverse.None;
   Visual?.SetClimb(false, wallNormal);
   climbRegrabAt = Time.time + (falling ? 0.45f : 0.2f);
   if (falling) {
    velocity.y = -0.5f;
    planarVelocity = Flat(wallNormal) * 1.4f;
    Grounded = false; wasGrounded = false;
    lastGroundedTime = -10f;
    doubleJumpAvailable = false; // the next jump press opens the glider
    airTime = 0.13f;
    Visual?.Play("fall");
   } else {
    Visual?.Play("idle", true);
   }
  }

  void StepClimb(Vector2 move) {
   float dt = Time.deltaTime, now = Time.time;
   if (!ProbeWall(-wallNormal, ChestHeight, 1.0f, out var hit)) {
    // The rock ended under the hands: over the top, or off the edge.
    if (!TryMantle(-Flat(wallNormal), false)) ExitClimb(true);
    return;
   }
   wallNormal = Vector3.Slerp(wallNormal, hit.normal, 1f - Mathf.Exp(-10f * dt)).normalized;
   if (Vector3.Angle(wallNormal, Vector3.up) < ClimbMinAngle - 8f) { ExitClimb(false); return; } // it levelled off
   Vector3 up = Vector3.ProjectOnPlane(Vector3.up, wallNormal).normalized;
   Vector3 side = Vector3.Cross(wallNormal, up).normalized;
   Vector2 m = Vector2.ClampMagnitude(move, 1f);
   bool moving = m.sqrMagnitude > 0.02f && now >= exhaustedUntil;
   Vector3 vel = moving ? (side * m.x + up * m.y) * ClimbSpeed * (m.y < -0.2f ? 1.3f : 1f) : Vector3.zero;
   float gap = hit.distance - (Controller.radius + 0.07f);
   Vector3 stick = -wallNormal * Mathf.Clamp(gap * 9f, -2f, 5f);
   Vector3 before = transform.position;
   Controller.Move((vel + stick) * dt);
   float moved = (transform.position - before).magnitude / Mathf.Max(0.0001f, dt);
   Vector3 face = Flat(-wallNormal);
   if (face != Vector3.zero) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), 360f * dt);
   Visual?.SetClimb(true, wallNormal);

   if (moving) {
    Stamina = Mathf.Max(0f, Stamina - ClimbStaminaPerSecond * dt);
    staminaRegenAt = now + 0.6f;
   }
   if (Stamina <= 0f) {
    exhaustedUntil = now + 1.6f;
    OpenWorldCombatPopup.Spawn(transform.position + Vector3.up * 2f, "Too tired to hold on", new Color(0.9f, 0.9f, 0.6f), 0.9f);
    ExitClimb(true);
    return;
   }
   // Over the top when the rock ends above the head.
   if (m.y > 0.3f && !ProbeWall(-Flat(wallNormal), 1.95f, 0.9f, out _) && TryMantle(-Flat(wallNormal), false)) return;
   // Down onto the ground.
   if (m.y < -0.2f && Physics.Raycast(transform.position + Vector3.up * 0.25f, Vector3.down, out var below, 0.45f, ~0, QueryTriggerInteraction.Ignore) && below.normal.y > 0.7f && !(below.collider is CharacterController)) {
    ExitClimb(false);
    return;
   }
   // Animation: the ladder cycle at the rate that matches the climb speed.
   string st = !moving ? "climb_idle" : m.y > 0.25f || Mathf.Abs(m.x) > Mathf.Abs(m.y) ? "climb_up" : m.y < -0.25f ? "climb_down" : "climb_up";
   float stride = Visual ? Visual.GetStride(st) : 0f;
   float rate = st == "climb_idle" ? 1f : Mathf.Clamp(moved / Mathf.Max(0.3f, stride > 0.05f ? stride : 0.9f), 0.55f, 1.8f);
   if (Visual) {
    if (climbState != st) { Visual.PlayAt(st, "slash", rate, 0f, 0.2f); climbState = st; }
    else Visual.PlayAt(st, "slash", rate, 0f, 0.2f, false);
   }
   OpenWorldManager.I?.CameraRig?.SetMotionFov(0f);
  }

  void ClimbJump(Vector2 move) {
   float now = Time.time;
   if (Stamina < ClimbJumpStamina * 0.5f || now < exhaustedUntil) return;
   Stamina = Mathf.Max(0f, Stamina - ClimbJumpStamina);
   staminaRegenAt = now + 0.8f;
   if (move.y < -0.5f) {
    // Leap away from the wall; the glider can open from there.
    Vector3 away = Flat(wallNormal);
    ExitClimb(true);
    velocity.y = 6.5f;
    planarVelocity = away * 5.5f;
    if (away != Vector3.zero) transform.rotation = Quaternion.LookRotation(away);
    Visual?.PlayAt("jump", "slash", 1f, 0f, 0.08f);
    airAnimHoldUntil = now + 0.4f;
    OpenWorldAudio.Play("sfx_jump", 0.5f);
    return;
   }
   Vector3 up = Vector3.ProjectOnPlane(Vector3.up, wallNormal).normalized;
   Vector3 side = Vector3.Cross(wallNormal, up).normalized;
   Vector3 dir = side * move.x + up * Mathf.Max(0.35f, move.y);
   climbJumpDir = dir.sqrMagnitude > 0.01f ? dir.normalized : up;
   traverse = Traverse.ClimbJump;
   traverseStart = now; traverseLength = 0.42f;
   Visual?.PlayAt("climb_up", "slash", 2.3f, 0f, 0.08f);
   climbState = null;
   OpenWorldAudio.Play("sfx_jump", 0.45f);
  }

  void StepClimbJump(Vector2 move) {
   float dt = Time.deltaTime, now = Time.time;
   float u = Mathf.Clamp01((now - traverseStart) / traverseLength);
   if (!ProbeWall(-wallNormal, ChestHeight, 1.05f, out var hit)) {
    if (!TryMantle(-Flat(wallNormal), false)) ExitClimb(true);
    return;
   }
   wallNormal = Vector3.Slerp(wallNormal, hit.normal, 1f - Mathf.Exp(-10f * dt)).normalized;
   float speed = Mathf.Lerp(5.6f, 0.4f, u * u);
   float gap = hit.distance - (Controller.radius + 0.07f);
   Controller.Move((climbJumpDir * speed - wallNormal * Mathf.Clamp(gap * 9f, -2f, 5f)) * dt);
   Visual?.SetClimb(true, wallNormal);
   if (!ProbeWall(-Flat(wallNormal), 1.95f, 0.9f, out _) && TryMantle(-Flat(wallNormal), false)) return;
   if (u >= 1f) traverse = Traverse.Climb;
  }

  // Vault onto the ledge in front (from a climb, or straight from the ground for low walls).
  bool TryMantle(Vector3 forward, bool fromGround) {
   if (forward == Vector3.zero || !Controller) return false;
   float maxRise = fromGround ? 1.5f : 2.2f;
   Vector3 probe = transform.position + Vector3.up * (maxRise + 0.4f) + forward * 0.72f;
   int n = Physics.RaycastNonAlloc(probe, Vector3.down, traverseHits, maxRise + 0.45f, ~0, QueryTriggerInteraction.Ignore);
   float best = float.MaxValue; RaycastHit top = default; bool found = false;
   for (int i = 0; i < n; i++) {
    var h = traverseHits[i];
    if (!h.collider || h.collider is CharacterController || h.collider.transform.IsChildOf(transform) || h.rigidbody && !h.rigidbody.isKinematic) continue;
    if (h.distance < best) { best = h.distance; top = h; found = true; }
   }
   if (!found || top.normal.y < 0.72f) return false;
   float rise = top.point.y - transform.position.y;
   if (rise < (fromGround ? 0.5f : 0.2f) || rise > maxRise) return false;
   // Room to stand on the ledge.
   Vector3 stand = top.point + forward * 0.2f + Vector3.up * 0.03f;
   float r = Controller.radius * 0.85f;
   int o = Physics.OverlapCapsuleNonAlloc(stand + Vector3.up * (r + 0.08f), stand + Vector3.up * (Controller.height - r), r, traverseOverlap, ~0, QueryTriggerInteraction.Ignore);
   for (int i = 0; i < o; i++) {
    var c = traverseOverlap[i];
    if (!c || c == Controller || c.transform.IsChildOf(transform) || c.attachedRigidbody && !c.attachedRigidbody.isKinematic) continue;
    return false;
   }
   BeginMantle(stand, rise, forward, fromGround);
   return true;
  }

  void BeginMantle(Vector3 stand, float rise, Vector3 forward, bool fromGround) {
   float now = Time.time;
   traverse = Traverse.Mantle;
   SetGliding(false);
   EndAction(); CancelPendingStrikes();
   dashing = false; sprinting = false;
   traverseStart = now;
   traverseLength = Mathf.Lerp(0.42f, 0.8f, Mathf.InverseLerp(0.3f, 2.2f, rise)) * (fromGround ? 0.85f : 1f);
   mantleFrom = transform.position;
   mantleTo = stand;
   if (forward != Vector3.zero) transform.rotation = Quaternion.LookRotation(forward);
   velocity = Vector3.zero; planarVelocity = Vector3.zero;
   Controller.enabled = false;
   Visual?.SetClimb(true, -forward);
   float clip = Visual ? Visual.GetClipLength("climb_top") : 1f;
   Visual?.PlayAt("climb_top", "slash", Mathf.Clamp(clip / traverseLength * 0.8f, 0.9f, 2.6f), 0f, 0.1f);
   climbState = null;
  }

  void StepMantle() {
   float t = Mathf.Clamp01((Time.time - traverseStart) / Mathf.Max(0.1f, traverseLength));
   // Up first, then over the edge.
   float rise = Mathf.Clamp01(t / 0.62f); rise = 1f - (1f - rise) * (1f - rise);
   float over = Mathf.Clamp01((t - 0.38f) / 0.62f); over = over * over * (3f - 2f * over);
   Vector3 flat = mantleTo - mantleFrom; flat.y = 0f;
   transform.position = mantleFrom + Vector3.up * ((mantleTo.y - mantleFrom.y) * rise) + flat * over;
   if (t < 1f) return;
   Controller.enabled = true;
   Physics.SyncTransforms();
   traverse = Traverse.None;
   Visual?.SetClimb(false, wallNormal);
   Grounded = true; wasGrounded = true; airTime = 0f;
   lastGroundedTime = Time.time;
   planarVelocity = transform.forward * 1.2f;
   animHoldUntil = Time.time + 0.12f; animHoldCancelByMove = true;
   Visual?.Play("idle", true);
  }

  // ---------------------------------------------------------------- swimming

  // The moment her feet break the surface on the way down (a jump into a stream, a dive into a lake),
  // splash there at once: the swim itself only starts once she is deep enough.
  void WaterEntrySplash() {
   float y = transform.position.y;
   float before = lastFeetY;
   lastFeetY = y;
   if (float.IsNaN(before) || velocity.y > -3f || !OpenWorldRealm.Instance) return;
   if (!OpenWorldTerrainSculptor.WaterAt(transform.position.x, transform.position.z, out float surface, out _)) return;
   if (before <= surface || y > surface) return;
   float impact = -velocity.y;
   Vector3 p = new Vector3(transform.position.x, surface, transform.position.z);
   OpenWorldWaterFx.Splash(p, Mathf.Clamp(impact / 6f, 0.5f, 3f));
   OpenWorldSoundscape.Sfx(impact > 9f ? "splash_big" : "splash_light", p, impact > 9f ? 0.65f : 0.45f);
   if (impact > 12f) OpenWorldManager.I?.CameraRig?.AddShake(0.05f, 0.18f);
   entrySplashAt = Time.time;
  }

  bool CheckSwim() {
   if (!OpenWorldRealm.Instance || traverse != Traverse.None) return false;
   if (!OpenWorldTerrainSculptor.WaterAt(transform.position.x, transform.position.z, out float surface, out float depth)) return false;
   if (surface - transform.position.y < SwimEnterDepth || depth < SwimEnterDepth) return false;
   BeginSwim(surface);
   return true;
  }

  void BeginSwim(float surface) {
   float impact = -velocity.y;
   traverse = Traverse.Swim;
   swimSurface = surface;
   SetGliding(false);
   EndAction(); CancelPendingStrikes();
   dashing = false; sprinting = false;
   velocity = Vector3.zero;
   planarVelocity *= 0.45f;
   Visual?.SetSwim(true, 0f, surface);
   Visual?.SetFootIk(0f);
   Visual?.Play("idle", true);
   Vector3 p = new Vector3(transform.position.x, surface, transform.position.z);
   // Wading in from the shallows: a small splash (a fall or dive already splashed as she broke the
   // surface, see WaterEntrySplash).
   if (Time.time - entrySplashAt > 0.6f) { OpenWorldSoundscape.Sfx("splash_light", p, 0.45f); OpenWorldWaterFx.Splash(p, impact > 2f ? 0.7f : 0.4f); }
  }

  void ExitSwim() {
   traverse = Traverse.None;
   Visual?.SetSwim(false, 0f);
   Visual?.Play("idle", true);
  }

  void StepSwim(Vector2 move) {
   float dt = Time.deltaTime, now = Time.time;
   bool water = OpenWorldTerrainSculptor.WaterAt(transform.position.x, transform.position.z, out float surface, out float depth);
   if (drownAt > 0f) {
    // Exhausted: she slips under, then comes to on the last shore.
    Controller.Move(Vector3.down * 0.7f * dt);
    if (now >= drownAt) {
     drownAt = -1f;
     ResetTraversal();
     Vector3 shore = hasSafeGround ? lastSafeGround : transform.position;
     WarpTo(shore);
     Health = Mathf.Max(1f, Health - 1f);
     Stamina = MaxStamina * 0.5f;
     OpenWorldManager.I?.ShowBanner("Too exhausted to swim... Aster made it back to shore.");
     OpenWorldManager.I?.CameraRig?.SnapToTarget();
     Visual?.Play("idle", true);
    }
    return;
   }
   if (!water || depth < SwimExitDepth) { ExitSwim(); return; }
   swimSurface = Mathf.Lerp(swimSurface, surface, 1f - Mathf.Exp(-8f * dt));

   Transform camT = Camera.main ? Camera.main.transform : null;
   Vector3 fwd = camT ? Flat(camT.forward) : Vector3.forward, right = camT ? Flat(camT.right) : Vector3.right;
   if (fwd == Vector3.zero) fwd = Vector3.forward;
   Vector3 moveDir = Vector3.ClampMagnitude(fwd * move.y + right * move.x, 1f);
   bool wantsMove = moveDir.sqrMagnitude > 0.02f;
   bool fast = sprintHeld && wantsMove && Stamina > 0f && now >= exhaustedUntil;
   float speed = wantsMove ? (fast ? FastSwimSpeed : SwimSpeed) * Mathf.Clamp01(moveDir.magnitude * 1.4f) : 0f;
   Vector3 desired = wantsMove ? moveDir.normalized * speed : Vector3.zero;
   // Water resists: slow to speed up, long glide between strokes.
   planarVelocity = Vector3.MoveTowards(planarVelocity, desired, (desired.sqrMagnitude > planarVelocity.sqrMagnitude ? 3.6f : 2.2f) * dt);
   float vy = Mathf.Clamp((swimSurface - SwimBodyDepth - transform.position.y) * 4f, -3f, 3f);
   Controller.Move((planarVelocity + Vector3.up * vy) * dt);
   if (wantsMove) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(moveDir.normalized), 300f * dt);

   float drain = fast ? 18f : wantsMove ? 3.5f : 1.2f;
   Stamina = Mathf.Max(0f, Stamina - drain * dt);
   staminaRegenAt = now + 0.8f;
   if (Stamina <= 0f) {
    drownAt = now + 1.1f;
    Visual?.SetSwim(false, 0f);
    Visual?.PlayAt("fall", "slash", 0.6f, 0f, 0.3f);
    Vector3 at = new Vector3(transform.position.x, swimSurface, transform.position.z);
    OpenWorldSoundscape.Sfx("splash_light", at, 0.5f);
    OpenWorldWaterFx.Splash(at, 0.6f);
    return;
   }
   // Pace 1 is the breaststroke, up to ~1.9 the fast swim.
   float pace = planarVelocity.magnitude / SwimSpeed;
   Visual?.SetSwim(true, pace, swimSurface);
   Visual?.SetLocomotion(0f, false);
   Visual?.SetFootIk(0f);
   OpenWorldManager.I?.CameraRig?.SetMotionFov(fast ? 4f : 0f);
   // Wake: rings from the shoulders as she moves (more often, with kicked spray, when swimming fast);
   // slow, small rings while treading water.
   if (now >= nextRipple) {
    Vector3 at = new Vector3(transform.position.x, swimSurface, transform.position.z);
    if (pace > 0.3f) {
     nextRipple = now + (fast ? 0.4f : 0.7f);
     OpenWorldWaterFx.Ripple(at + transform.forward * 0.3f, Mathf.Lerp(1.3f, 2.4f, Mathf.Clamp01(pace - 0.3f)), 0.7f);
     if (fast) OpenWorldWaterFx.Splash(at - transform.forward * 1.05f, 0.2f);
    } else {
     nextRipple = now + 1.25f;
     OpenWorldWaterFx.Ripple(at, 1.5f, 0.5f);
    }
   }
  }

  // ---------------------------------------------------------------- capture helpers

  // "wall": stands Aster in front of the nearest tall climbable face within 40 m, facing it.
  public static void DebugFaceWall(string arg) {
   var hero = OpenWorldManager.I ? OpenWorldManager.I.Hero : null;
   if (!hero) return;
   Vector3 origin = hero.transform.position;
   float bestDistance = float.MaxValue; Vector3 bestPoint = Vector3.zero, bestNormal = Vector3.zero;
   for (int i = 0; i < 48; i++) {
    Vector3 dir = Quaternion.Euler(0f, i * 7.5f, 0f) * Vector3.forward;
    if (!Physics.Raycast(origin + Vector3.up * 1.2f, dir, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore) || !hero.Climbable(hit)) continue;
    Vector3 flatN = Flat(hit.normal);
    if (flatN == Vector3.zero) continue;
    // Tall enough to climb for a while.
    if (!Physics.Raycast(hit.point + flatN * 0.6f + Vector3.up * 2.5f, -flatN, out var high, 1.6f, ~0, QueryTriggerInteraction.Ignore) || !hero.Climbable(high)) continue;
    if (hit.distance < bestDistance) { bestDistance = hit.distance; bestPoint = hit.point; bestNormal = flatN; }
   }
   if (bestDistance == float.MaxValue) { Debug.LogWarning("[Hero] no climbable wall within 40 m"); return; }
   hero.WarpTo(bestPoint + bestNormal * 1.1f);
   hero.transform.rotation = Quaternion.LookRotation(-bestNormal);
   var rig = OpenWorldManager.I.CameraRig;
   if (rig) { rig.Yaw = Mathf.Atan2(-bestNormal.x, -bestNormal.z) * Mathf.Rad2Deg; rig.SnapToTarget(); }
   Debug.Log("[Hero] facing a wall " + bestDistance.ToString("0.0") + " m away at " + bestPoint.ToString("F1"));
  }

  // "water": moves Aster into the nearest deep water (lake or river) within 400 m: the deepest point
  // near there, facing (with the camera behind her) the longest stretch of deep water.
  public static void DebugFindWater(string arg) {
   var hero = OpenWorldManager.I ? OpenWorldManager.I.Hero : null;
   if (!hero || !OpenWorldRealm.Instance) return;
   Vector3 o = hero.transform.position;
   for (float r = 0f; r <= 400f; r += 6f) {
    int steps = Mathf.Max(1, Mathf.CeilToInt(r * Mathf.PI * 2f / 6f));
    for (int i = 0; i < steps; i++) {
     float a = i * Mathf.PI * 2f / steps;
     float x = o.x + Mathf.Cos(a) * r, z = o.z + Mathf.Sin(a) * r;
     if (!OpenWorldTerrainSculptor.WaterAt(x, z, out float surface, out float depth) || depth < 2.4f) continue;
     // Deepest point within 25 m.
     for (float dx = -25f; dx <= 25f; dx += 2.5f)
      for (float dz = -25f; dz <= 25f; dz += 2.5f)
       if (OpenWorldTerrainSculptor.WaterAt(x + dx, z + dz, out float s2, out float d2) && d2 > depth + 0.05f) { depth = d2; surface = s2; x += dx; z += dz; dx = dz = -25f; }
     // The heading with the most deep water ahead.
     float bestYaw = 0f; int bestRun = -1;
     for (int h = 0; h < 16; h++) {
      float yaw = h * 22.5f;
      Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
      int run = 0;
      while (run < 12 && OpenWorldTerrainSculptor.WaterAt(x + dir.x * (run + 1) * 2f, z + dir.z * (run + 1) * 2f, out _, out float d3) && d3 >= 1.8f) run++;
      if (run > bestRun) { bestRun = run; bestYaw = yaw; }
     }
     hero.WarpTo(new Vector3(x, surface, z));
     hero.transform.rotation = Quaternion.Euler(0f, bestYaw, 0f);
     var rig = OpenWorldManager.I.CameraRig;
     if (rig) { rig.Yaw = bestYaw; rig.SnapToTarget(); }
     float bed = OpenWorldMaterialHelper.SampleGroundHeight(new Vector3(x, surface, z), hero.transform);
     Debug.Log("[Hero] into water at " + new Vector3(x, surface, z).ToString("F1") + " depth " + depth.ToString("0.0") + " (terrain bed at " + bed.ToString("0.00") + "), " + (bestRun * 2) + " m of deep water ahead");
     return;
    }
   }
   Debug.LogWarning("[Hero] no deep water within 400 m");
  }

  // "lift 7": puts Aster the given height above the ground or water surface under her, to fall from there.
  public void DebugLift(float height) {
   ResetTraversal();
   SetGliding(false);
   EndAction();
   float y = OpenWorldMaterialHelper.SampleGroundHeight(transform.position, transform);
   if (OpenWorldRealm.Instance && OpenWorldTerrainSculptor.WaterAt(transform.position.x, transform.position.z, out float s, out _)) y = Mathf.Max(y, s);
   if (Controller) Controller.enabled = false;
   transform.position = new Vector3(transform.position.x, y + Mathf.Max(0.5f, height), transform.position.z);
   velocity = Vector3.zero; planarVelocity = Vector3.zero;
   Physics.SyncTransforms();
   if (Controller) Controller.enabled = true;
  }

  // Capture diagnostics: traversal state, height against the water and speed.
  public string DebugTraversalInfo() {
   string water = OpenWorldRealm.Instance && OpenWorldTerrainSculptor.WaterAt(transform.position.x, transform.position.z, out float s, out float d)
    ? " surface " + s.ToString("0.00") + " depth " + d.ToString("0.00") : " dry";
   return traverse + " y " + transform.position.y.ToString("0.00") + water + " speed " + planarVelocity.magnitude.ToString("0.00");
  }

  // "hit_2 1.2": plays a state as a held action, for reviewing clips in game.
  public void DebugPlayState(string arg) {
   if (!Visual || string.IsNullOrEmpty(arg)) return;
   var p = arg.Trim().Split(' ');
   float sp = p.Length > 1 && float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? Mathf.Max(0.1f, v) : 1f;
   ResetTraversal();
   EndAction(); CancelPendingStrikes();
   float now = Time.time;
   actionUntil = cancelAt = chainAt = now + Visual.GetClipLength(p[0]) / sp;
   actionMovable = false;
   Visual.PlayAt(p[0], "slash", sp, 0f, 0.1f);
  }

  // Knocked down as if by a heavy blow from the front (review helper; ignores invulnerability).
  public void DebugKnockdown() {
   ResetTraversal();
   BeginKnockdown(-transform.forward);
  }

  // ---------------------------------------------------------------- knockdown

  bool BeginKnockdown(Vector3 away) {
   if (!Visual || !Visual.HasState("knockdown") || traverse != Traverse.None) return false;
   float now = Time.time;
   traverse = Traverse.Knockdown;
   traverseStart = now;
   SetGliding(false);
   EndAction(); CancelPendingStrikes();
   dashing = false; sprinting = false;
   away = Flat(away);
   if (away == Vector3.zero) away = -transform.forward;
   transform.rotation = Quaternion.LookRotation(-away);
   planarVelocity = away * 6.5f;
   velocity.y = 3.2f;
   Grounded = false; wasGrounded = false;
   const float sp = 1.3f;
   var strikes = Visual.GetStrikes("knockdown");
   float lie = strikes.Length > 0 ? strikes[0] : Visual.GetClipLength("knockdown") * 0.6f;
   Visual.PlayAt("knockdown", "slash", sp, 0f, 0.06f);
   knockGetUpAt = now + Mathf.Max(0.55f, lie / sp + 0.28f);
   knockRising = false;
   knockEndAt = knockGetUpAt + 1.2f;
   invulnerableUntil = Mathf.Max(invulnerableUntil, knockEndAt);
   OpenWorldManager.I?.CameraRig?.AddShake(0.2f, 0.22f);
   return true;
  }

  void StepKnockdown() {
   float dt = Time.deltaTime, now = Time.time;
   velocity.y += Gravity * dt;
   var flags = Controller.Move((planarVelocity + Vector3.up * velocity.y) * dt);
   bool grounded = (flags & CollisionFlags.Below) != 0 || ProbeGround();
   if (grounded) {
    if (velocity.y < 0f) velocity.y = -1f;
    planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, 11f * dt);
   } else planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, 1.5f * dt);
   Grounded = grounded;
   if (!knockRising && now >= knockGetUpAt && grounded) {
    knockRising = true;
    const float sp = 1.45f;
    var strikes = Visual ? Visual.GetStrikes("getup") : new float[0];
    float stand = strikes.Length > 0 && strikes[0] > 0.2f ? strikes[0] : (Visual ? Visual.GetClipLength("getup") * 0.8f : 1f);
    Visual?.PlayAt("getup", "slash", sp, 0f, 0.12f);
    knockEndAt = now + stand / sp;
    invulnerableUntil = Mathf.Max(invulnerableUntil, knockEndAt);
    OpenWorldSoundscape.Sfx("body_thud", transform.position, 0.35f);
   }
   if (knockRising && now >= knockEndAt) {
    traverse = Traverse.None;
    knockRising = false;
    animHoldUntil = 0f;
    Visual?.Play("idle", true);
   }
  }
 }
}

using UnityEngine;
using UnityEngine.EventSystems;

namespace LostRealmsOW {
 // Third-person follow camera with a Genshin / Wuthering Waves feel:
 //  - The focus is smoothed vertically (no jitter on steps, landings or the swimming bob) and stays
 //    tight horizontally.
 //  - Framing adapts: a little wider when sprinting or dashing, wide while gliding, pulled back and
 //    tilted up the rock while climbing, lower over the water while swimming, wider near foes.
 //  - When the player leaves the camera alone, it eases round behind the run (never fights input).
 //  - Lock-on keeps Aster and her target in frame.
 //  - Obstructions pull the lens in at once; thrown bodies, props and debris (moving rigidbodies)
 //    never block it.
 //  - Elemental burst close-up, and impact shake from smooth noise with a touch of roll.
 public sealed class OpenWorldCamera : MonoBehaviour {
  public Transform Target;
  public float Distance = 6.2f;
  public float HeightOffset = 1.65f;
  public float Yaw = 0f;
  public float Pitch = 22f;
  const string SensPref = "LostRealms.LookSensitivity";
  public int SensitivityTier { get; private set; } // 0 Normal, 1 High, 2 Low
  public float SensitivityMultiplier => SensitivityTier == 1 ? 1.4f : SensitivityTier == 2 ? 0.65f : 1f;
  public string SensitivityLabel => SensitivityTier == 1 ? "HIGH" : SensitivityTier == 2 ? "LOW" : "NORMAL";
  readonly RaycastHit[] obstructions = new RaycastHit[32];
  float shakeIntensity;
  float shakeTimer, shakeDuration;
  float shakeSeed;
  Camera cam;
  float baseFov = 60f, motionFov, motionFovTarget;
  Transform burstTarget;
  float burstStart, burstDuration, burstAngle = 38f;

  OpenWorldHero hero;
  float lastManualLook = -10f;
  Vector3 smoothFocus, lastTargetPosition;
  bool focusReady;
  float focusYVelocity;
  float distanceExtra, distanceVelocity, heightExtra, pitchBias;
  bool foeNear;
  float nextFoeCheck;

  void Awake() {
   SensitivityTier = Mathf.Clamp(PlayerPrefs.GetInt(SensPref, 0), 0, 2);
   cam = GetComponent<Camera>();
   if (cam) baseFov = cam.fieldOfView;
   shakeSeed = Random.value * 100f;
  }

  // A few degrees of extra field of view while sprinting, dashing or gliding.
  public void SetMotionFov(float extraDegrees) { motionFovTarget = extraDegrees; }

  // Elemental burst close-up: swings round to a low three-quarter view of the hero's face for the
  // wind-up, then eases back to the follow camera.
  public void BurstShot(Transform target, float duration) {
   if (!target) return;
   // Pick a side the lens can actually reach: walls, arches or crowds next to her would otherwise push
   // the close-up into her shoulder. No clear side: skip the close-up.
   Vector3 focus = target.position + Vector3.up * 1.15f;
   burstTarget = null;
   foreach (float angle in new[] { 38f, -38f, 65f, -65f, 15f, -15f }) {
    Vector3 side = Quaternion.AngleAxis(angle, Vector3.up) * target.forward;
    Vector3 want = focus + side * 3.1f + Vector3.up * 0.25f;
    Vector3 got = ResolvePosition(focus, want, out bool blocked);
    if (blocked && (got - focus).magnitude < 2.3f) continue;
    burstAngle = angle;
    burstTarget = target;
    break;
   }
   burstStart = Time.time;
   burstDuration = Mathf.Max(0.3f, duration);
  }

  public void CycleSensitivity() {
   SensitivityTier = (SensitivityTier + 1) % 3;
   PlayerPrefs.SetInt(SensPref, SensitivityTier);
   PlayerPrefs.Save();
  }

  public void AddShake(float intensity, float duration) {
   shakeIntensity = Mathf.Clamp(Mathf.Max(shakeIntensity, intensity), 0f, .22f);
   shakeDuration = Mathf.Clamp(Mathf.Max(shakeTimer, duration), .025f, .35f);
   shakeTimer = shakeDuration;
  }

  Vector3 ResolvePosition(Vector3 focus, Vector3 desired, out bool blocked) {
   Vector3 offset = desired - focus;
   float distance = offset.magnitude;
   blocked = false;
   if (distance < 0.01f) return desired;
   float lensRadius = cam ? Mathf.Max(.22f, cam.nearClipPlane * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) * Mathf.Sqrt(1f + cam.aspect * cam.aspect)) : .22f;
   int count = Physics.SphereCastNonAlloc(focus, lensRadius, offset / distance, obstructions, distance, ~0, QueryTriggerInteraction.Ignore);
   float nearest = distance;
   for (int i = 0; i < count; i++) {
    var hit = obstructions[i];
    if (!hit.collider || hit.collider is CharacterController || (Target && hit.transform.IsChildOf(Target))) continue;
    if (hit.rigidbody && !hit.rigidbody.isKinematic) continue; // ragdolls, tumbling props, shards
    nearest = Mathf.Min(nearest, Mathf.Max(0.03f, hit.distance - 0.12f));
    blocked = true;
   }
   return focus + offset.normalized * nearest;
  }

  public void AddLookDelta(Vector2 delta) {
   float mult = SensitivityMultiplier;
   Yaw += delta.x * mult;
   Pitch = Mathf.Clamp(Pitch - delta.y * mult, 8f, 52f);
   if (delta.sqrMagnitude > 0.0001f) lastManualLook = Time.time;
  }

  void LateUpdate() {
   if (!Target) return;
   float dt = Mathf.Max(0.0001f, Time.deltaTime);
   var gm = OpenWorldManager.I;
   if (!gm || !gm.IsModalOpen) {
    if (Input.GetMouseButton(1) && !(EventSystem.current && EventSystem.current.IsPointerOverGameObject())) {
     float mult = SensitivityMultiplier;
     float dx = Input.GetAxis("Mouse X"), dy = Input.GetAxis("Mouse Y");
     Yaw += dx * 3.5f * mult;
     Pitch = Mathf.Clamp(Pitch - dy * 2.5f * mult, 8f, 52f);
     if (Mathf.Abs(dx) + Mathf.Abs(dy) > 0.001f) lastManualLook = Time.time;
    }
    if (Input.GetKey(KeyCode.LeftBracket)) { Yaw -= 75f * dt; lastManualLook = Time.time; }
    if (Input.GetKey(KeyCode.RightBracket)) { Yaw += 75f * dt; lastManualLook = Time.time; }
   }
   if (!hero || hero.transform != Target) hero = Target.GetComponent<OpenWorldHero>();

   // Context framing.
   float wantDistance = 0f, wantHeight = 0f, wantPitch = 0f;
   if (hero) {
    if (hero.IsMounted) {
     // Horse and rider in frame, pulling back further at the gallop.
     float gallop = hero.Mount ? hero.Mount.Gallop01 : 0f;
     wantDistance += 1.6f + 1.1f * gallop; wantHeight += 0.25f; wantPitch -= 2f;
    } else if (hero.IsGliding) wantDistance += 1.4f;
    else if (hero.IsSprinting || hero.IsDashing) wantDistance += 0.5f;
    if (hero.IsClimbing) { wantDistance += 0.9f; wantPitch -= 7f; }
    if (hero.IsSwimming) { wantDistance -= 0.4f; wantHeight -= 0.35f; }
    if (Time.time >= nextFoeCheck) { nextFoeCheck = Time.time + 0.3f; foeNear = FoeWithin(9f); }
    if (foeNear && !hero.IsGliding) wantDistance += 0.7f;
   }
   distanceExtra = Mathf.SmoothDamp(distanceExtra, wantDistance, ref distanceVelocity, 0.6f, 6f, dt);
   heightExtra = Mathf.Lerp(heightExtra, wantHeight, 1f - Mathf.Exp(-3f * dt));
   pitchBias = Mathf.Lerp(pitchBias, wantPitch, 1f - Mathf.Exp(-3f * dt));

   Vector3 moved = Target.position - lastTargetPosition; moved.y = 0f;
   float speed = focusReady ? moved.magnitude / dt : 0f;
   lastTargetPosition = Target.position;
   var lockTarget = hero && hero.LockOnTarget && !hero.LockOnTarget.Dead ? hero.LockOnTarget.transform : null;
   bool cinematic = burstTarget;
   if (lockTarget && !cinematic) {
    // Lock-on: look past Aster at the target.
    Vector3 to = lockTarget.position - Target.position; to.y = 0f;
    if (to.sqrMagnitude > 1f) {
     float wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
     Yaw = Mathf.LerpAngle(Yaw, wantYaw, 1f - Mathf.Exp(-4f * dt));
     if (Time.time - lastManualLook > 0.6f) Pitch = Mathf.Lerp(Pitch, 18f, 1f - Mathf.Exp(-2f * dt));
    }
   } else if (hero && !cinematic && Time.time - lastManualLook > 1.2f && speed > 2.5f && !hero.IsClimbing && speed < 30f) {
    // Auto-recentre behind the run, gently, and never when she runs toward the lens.
    float moveYaw = Mathf.Atan2(moved.x, moved.z) * Mathf.Rad2Deg;
    float delta = Mathf.DeltaAngle(Yaw, moveYaw);
    if (Mathf.Abs(delta) < 110f) Yaw += delta * (1f - Mathf.Exp(-0.9f * dt)) * Mathf.Clamp01((speed - 2.5f) / 3f);
   }

   // Smoothed focus: tight horizontally, damped vertically.
   Vector3 raw = Target.position + Vector3.up * (HeightOffset + heightExtra);
   if (!focusReady) { smoothFocus = raw; focusReady = true; }
   bool airborne = hero && !hero.Grounded && !hero.IsClimbing && !hero.IsSwimming;
   smoothFocus.x = raw.x; smoothFocus.z = raw.z;
   smoothFocus.y = Mathf.SmoothDamp(smoothFocus.y, raw.y, ref focusYVelocity, airborne ? 0.16f : 0.07f, 40f, dt);
   smoothFocus.y = Mathf.Clamp(smoothFocus.y, raw.y - 1.2f, raw.y + 1.2f);
   Vector3 focus = smoothFocus;
   if (lockTarget) focus = Vector3.Lerp(focus, (Target.position + lockTarget.position) * 0.5f + Vector3.up * HeightOffset, 0.28f);

   Quaternion rot = Quaternion.Euler(Mathf.Clamp(Pitch + pitchBias, 2f, 60f), Yaw, 0f);
   Vector3 desired = focus - rot * Vector3.forward * (Distance + distanceExtra);

   float groundY = OpenWorldMaterialHelper.SampleGroundHeight(desired, Target) + 0.6f;
   if (desired.y < groundY) desired.y = groundY;

   desired = ResolvePosition(focus, desired, out bool blocked);
   // Pull inward immediately so easing cannot leave the lens behind a wall.
   Vector3 eased = blocked ? desired : Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-14f * dt));
   Vector3 finalPos = AboveWater(ResolvePosition(focus, eased, out _));

   Quaternion look = Quaternion.LookRotation(focus - finalPos);
   if (burstTarget) {
    float t = (Time.time - burstStart) / burstDuration;
    if (t >= 1.35f) burstTarget = null;
    else {
     float blend = t < 0f ? 0f : t < 0.18f ? Mathf.SmoothStep(0f, 1f, t / 0.18f) : t < 1f ? 1f : Mathf.SmoothStep(1f, 0f, (t - 1f) / 0.35f);
     Vector3 heroFocus = burstTarget.position + Vector3.up * 1.15f;
     Vector3 side = Quaternion.AngleAxis(burstAngle, Vector3.up) * burstTarget.forward;
     Vector3 cinePos = ResolvePosition(heroFocus, heroFocus + side * 3.1f + Vector3.up * 0.25f, out _);
     // Swing round her on an arc (angle, distance and height blended separately) rather than cutting
     // straight across: the follow camera sits behind her and the close-up in front, so a straight
     // line would pass through her head.
     Vector3 from = finalPos - heroFocus, to = cinePos - heroFocus;
     float fromYaw = Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg, toYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
     float fromFlat = new Vector2(from.x, from.z).magnitude, toFlat = new Vector2(to.x, to.z).magnitude;
     float arcYaw = Mathf.LerpAngle(fromYaw, toYaw, blend);
     float arcFlat = Mathf.Max(Mathf.Lerp(fromFlat, toFlat, blend), Mathf.Min(2.4f, Mathf.Min(fromFlat, toFlat)));
     Vector3 arcPos = heroFocus + Quaternion.Euler(0f, arcYaw, 0f) * Vector3.forward * arcFlat + Vector3.up * Mathf.Lerp(from.y, to.y, blend);
     finalPos = AboveWater(ResolvePosition(heroFocus, arcPos, out _));
     look = Quaternion.LookRotation(Vector3.Lerp(focus, heroFocus + Vector3.up * 0.1f, blend) - finalPos);
    }
   }

   // Impact shake on top of whatever framing is active (the burst slam included).
   float roll = 0f;
   if (shakeTimer > 0f) {
    shakeTimer -= dt;
    float k = shakeIntensity * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(shakeTimer / shakeDuration));
    float st = Time.time * 26f;
    float nx = Mathf.PerlinNoise(st, shakeSeed) - 0.5f, ny = Mathf.PerlinNoise(shakeSeed, st) - 0.5f, nr = Mathf.PerlinNoise(st, st + shakeSeed) - 0.5f;
    finalPos += (look * Vector3.right * nx + look * Vector3.up * ny) * (2f * k);
    roll = nr * k * 12f;
    if (shakeTimer <= 0f) shakeIntensity = 0f;
   }
   transform.position = finalPos;
   transform.rotation = look * Quaternion.Euler(0f, 0f, roll);
   motionFov = Mathf.Lerp(motionFov, motionFovTarget, 1f - Mathf.Exp(-4f * dt));
   if (cam) cam.fieldOfView = baseFov + motionFov;
  }

  // The lens never dips under a lake or river surface (the water is only drawn from above), e.g. when
  // she plunges in from a height.
  static Vector3 AboveWater(Vector3 p) {
   if (OpenWorldRealm.Instance && OpenWorldTerrainSculptor.WaterAt(p.x, p.z, out float surface, out _) && p.y < surface + 0.3f) p.y = surface + 0.3f;
   return p;
  }

  bool FoeWithin(float range) {
   var gm = OpenWorldManager.I;
   if (!gm || !Target) return false;
   float r2 = range * range;
   foreach (var e in gm.Enemies) {
    if (!e || e.Dead || !e.isActiveAndEnabled) continue;
    if ((e.transform.position - Target.position).sqrMagnitude < r2) return true;
   }
   return false;
  }

  public void SnapToTarget() {
   if (!Target) return;
   shakeTimer = 0f;
   shakeIntensity = 0f;
   focusReady = false;
   distanceExtra = 0f; distanceVelocity = 0f;
   Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
   Vector3 focus = Target.position + Vector3.up * HeightOffset;
   lastTargetPosition = Target.position;
   transform.position = ResolvePosition(focus, focus - rot * Vector3.forward * Distance, out _);
   transform.LookAt(focus);
  }
 }
}



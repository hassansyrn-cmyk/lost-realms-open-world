using System;
using UnityEngine;

namespace LostRealms {
 public enum GateState {
  Locked,
  Activating,
  Active,
  Teleporting,
  Completed
 }

 [SelectionBase]
 public class TeleportGateController : MonoBehaviour {
  [Header("Hierarchy References")]
  public Transform GateModel;
  public Transform PortalSurface;
  public Transform PortalGlow;
  public ParticleSystem PortalParticles;
  public ParticleSystem[] RealmParticles;
  public Light PortalLight;
  public AudioSource PortalAudio;
  public Collider TeleportTrigger;
  public Transform ArrivalPoint;

  [Header("Gate Settings")]
  public int RealmIndex = 0;
  public GateState State = GateState.Active;
  public Color RealmColor = Color.cyan;
  public Color SecondaryColor = Color.white;
  public float BaseLightIntensity = 0.8f;
  public float ActivationDuration = 1.2f;

  [Header("Open World Fast Travel")]
  public string WaypointId = "";
  public string WaypointTitle = "Realm Gate";
  public bool Discovered = false;

  [Header("Audio Clips")]
  public AudioClip AmbientLoop;
  public AudioClip ActivationClip;
  public AudioClip TeleportClip;

  public static event Action<TeleportGateController> OnGateDiscovered;
  public static event Action<TeleportGateController> OnGateInteracted;

  Material portalMat;
  Material glowMat;
  bool initialized;

  static readonly System.Collections.Generic.Dictionary<string, Material> gateMats = new System.Collections.Generic.Dictionary<string, Material>();
  static readonly System.Collections.Generic.Dictionary<string, Material> portalMats = new System.Collections.Generic.Dictionary<string, Material>();
  static readonly System.Collections.Generic.Dictionary<string, Material> glowMats = new System.Collections.Generic.Dictionary<string, Material>();
  static Material particleMat;

  public static string GetRealmName(int index) {
   string[] names = { "Verdant", "Desert", "Snow", "Lava", "Storm", "Astral" };
   return index >= 0 && index < names.Length ? names[index] : "Verdant";
  }

  static Shader FindLitShader() {
   return Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
  }

  public static Material GetGateMaterial(string realm) {
   if (gateMats.TryGetValue(realm, out var m) && m != null) return m;
   var mat = Resources.Load<Material>($"Gates/Materials/Gate_{realm}_Mat");
   if (!mat) {
    var tex = Resources.Load<Texture2D>($"Gates/Textures/Gate_{realm}_basecolor");
    var s = FindLitShader();
    if (s) {
     mat = new Material(s) { name = $"Gate_{realm}_RuntimeMat" };
     if (tex) {
      mat.mainTexture = tex;
      if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
     }
     if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
     if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.25f);
     if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.05f);
    }
   }
   if (mat) gateMats[realm] = mat;
   return mat;
  }

  public static Material GetPortalEnergyMaterial(string realm, Color prime, Color sec) {
   if (portalMats.TryGetValue(realm, out var m) && m != null) return m;
   var mat = Resources.Load<Material>($"Gates/Materials/PortalEnergy_{realm}");
   if (!mat) {
    var s = Resources.Load<Shader>("Shaders/PortalEnergy") ?? Shader.Find("LostRealms/PortalEnergy");
    if (s) {
     mat = new Material(s) { name = $"PortalEnergy_{realm}_Runtime" };
     mat.SetColor("_Color", prime);
     mat.SetColor("_SecondaryColor", sec);
     mat.SetFloat("_Speed", 1.0f);
     mat.SetFloat("_Distort", 0.32f);
     mat.SetFloat("_Opacity", 0.88f);
     mat.SetFloat("_Softness", 0.35f);
     mat.SetFloat("_Emission", 2.0f);
     mat.SetFloat("_Rotation", 1.5f);
    }
   }
   if (mat) portalMats[realm] = mat;
   return mat;
  }

  public static Material GetPortalGlowMaterial(string realm, Color prime) {
   if (glowMats.TryGetValue(realm, out var m) && m != null) return m;
   var mat = Resources.Load<Material>($"Gates/Materials/PortalGlow_{realm}");
   if (!mat) {
    var s = Shader.Find("Sprites/Default");
    if (s) {
     mat = new Material(s) { name = $"PortalGlow_{realm}_Runtime" };
     mat.color = new Color(prime.r, prime.g, prime.b, 0.32f);
     var smokeTex = Resources.Load<Texture2D>("VFX/Textures/smoke_04");
     if (smokeTex) mat.mainTexture = smokeTex;
    }
   }
   if (mat) glowMats[realm] = mat;
   return mat;
  }

  public static Material GetParticleMaterial() {
   if (particleMat != null) return particleMat;
   particleMat = Resources.Load<Material>("Gates/Materials/Gate_Particle_Smoke");
   if (!particleMat) {
    var s = Shader.Find("Sprites/Default");
    if (s) {
     particleMat = new Material(s) { name = "GateParticle_Runtime" };
     var smokeTex = Resources.Load<Texture2D>("VFX/Textures/smoke_04");
     if (smokeTex) particleMat.mainTexture = smokeTex;
    }
   }
   return particleMat;
  }

  void Awake() {
   InitializeHierarchy();
   EnsureMaterials();
   EnsureBaseColliders();
  }

  void OnEnable() { EnsureMaterials(); }

#if UNITY_EDITOR
  void OnValidate() { EnsureMaterials(); }
#endif

  void Start() {
   if (!initialized) InitializeHierarchy();
   EnsureMaterials();
   EnsureBaseColliders();
   ApplyRealmColors();
   SetState(State);
  }

  public void InitializeHierarchy() {
   if (initialized) return;
   if (!GateModel) GateModel = transform.Find("GateModel");
   if (!PortalSurface) PortalSurface = transform.Find("PortalSurface");
   if (!PortalGlow) PortalGlow = transform.Find("PortalGlow");
   if (!PortalParticles) {
    var pp = transform.Find("PortalParticles");
    if (pp) PortalParticles = pp.GetComponent<ParticleSystem>();
   }
   if (RealmParticles == null || RealmParticles.Length == 0) {
    var rp = transform.Find("RealmParticles");
    if (rp) RealmParticles = rp.GetComponentsInChildren<ParticleSystem>(true);
   }
   if (!PortalLight) {
    var pl = transform.Find("PortalLight");
    if (pl) PortalLight = pl.GetComponent<Light>();
   }
   if (!PortalAudio) {
    var pa = transform.Find("PortalAudio");
    if (pa) PortalAudio = pa.GetComponent<AudioSource>();
   }
   if (!TeleportTrigger) {
    var tt = transform.Find("TeleportTrigger");
    if (tt) TeleportTrigger = tt.GetComponent<Collider>();
   }
   if (!ArrivalPoint) ArrivalPoint = transform.Find("ArrivalPoint");
   EnsureMaterials();
   initialized = true;
  }

  public void EnsureBaseColliders() {
   if (!GateModel) GateModel = transform.Find("GateModel");
   if (!GateModel) return;
   if (RealmIndex >= 4 && GateModel.GetComponentInChildren<MeshCollider>()) return;
   foreach (var c in GateModel.GetComponents<BoxCollider>()) {
    if (Mathf.Abs(c.center.x) < 0.1f && c.center.y < 1.0f) return;
   }
   float baseH = RealmIndex == 1 ? 0.43f : RealmIndex == 2 ? 0.40f : RealmIndex == 3 ? 0.50f : 0.52f;
   var colBase = GateModel.gameObject.AddComponent<BoxCollider>();
   colBase.center = new Vector3(0f, baseH * 0.5f, 0f);
   colBase.size = new Vector3(2.9f, baseH, 2.4f);
   var colStepFront = GateModel.gameObject.AddComponent<BoxCollider>();
   colStepFront.center = new Vector3(0f, baseH * 0.25f, -1.65f);
   colStepFront.size = new Vector3(2.7f, baseH * 0.5f, 0.9f);
   var colStepBack = GateModel.gameObject.AddComponent<BoxCollider>();
   colStepBack.center = new Vector3(0f, baseH * 0.25f, 1.65f);
   colStepBack.size = new Vector3(2.7f, baseH * 0.5f, 0.9f);
  }

  public void EnsureMaterials() {
   string realm = GetRealmName(RealmIndex);
   if (GateModel) {
    var gmat = GetGateMaterial(realm);
    if (gmat) {
     foreach (var r in GateModel.GetComponentsInChildren<Renderer>(true)) {
      if (r is MeshRenderer mr) {
       if (mr.sharedMaterial == null || mr.sharedMaterial.shader == null || mr.sharedMaterial.shader.name.Contains("InternalError") || mr.sharedMaterial.name.StartsWith("Default")) {
        mr.sharedMaterial = gmat;
       }
      }
     }
    }
   }
   if (PortalSurface) {
    var r = PortalSurface.GetComponent<Renderer>();
    if (r) {
     if (r.sharedMaterial == null || r.sharedMaterial.shader == null || r.sharedMaterial.shader.name.Contains("InternalError")) {
      var pmat = GetPortalEnergyMaterial(realm, RealmColor, SecondaryColor);
      if (pmat) r.sharedMaterial = pmat;
     }
     if (portalMat == null) portalMat = r.sharedMaterial;
    }
   }
   if (PortalGlow) {
    var r = PortalGlow.GetComponent<Renderer>();
    if (r) {
     if (r.sharedMaterial == null || r.sharedMaterial.shader == null || r.sharedMaterial.shader.name.Contains("InternalError")) {
      var gm = GetPortalGlowMaterial(realm, RealmColor);
      if (gm) r.sharedMaterial = gm;
     }
     if (glowMat == null) glowMat = r.sharedMaterial;
    }
   }
   var pmatShared = GetParticleMaterial();
   if (pmatShared) {
    if (PortalParticles) {
     var psr = PortalParticles.GetComponent<ParticleSystemRenderer>();
     if (psr && (psr.sharedMaterial == null || psr.sharedMaterial.shader == null || psr.sharedMaterial.shader.name.Contains("InternalError"))) psr.sharedMaterial = pmatShared;
    }
    if (RealmParticles != null) {
     foreach (var ps in RealmParticles) {
      if (!ps) continue;
      var psr = ps.GetComponent<ParticleSystemRenderer>();
      if (psr && (psr.sharedMaterial == null || psr.sharedMaterial.shader == null || psr.sharedMaterial.shader.name.Contains("InternalError"))) psr.sharedMaterial = pmatShared;
     }
    }
   }
  }

  public void ApplyRealmColors() {
   if (portalMat) {
    portalMat.SetColor("_Color", RealmColor);
    portalMat.SetColor("_SecondaryColor", SecondaryColor);
   }
   if (glowMat) glowMat.color = new Color(RealmColor.r, RealmColor.g, RealmColor.b, 0.45f);
   if (PortalLight) PortalLight.color = RealmColor;
  }

  public void SetState(GateState newState) {
   State = newState;
   bool active = State == GateState.Active || State == GateState.Activating;
   if (PortalSurface) PortalSurface.gameObject.SetActive(active);
   if (PortalGlow) PortalGlow.gameObject.SetActive(active);
   if (PortalLight) {
    PortalLight.enabled = active;
    PortalLight.intensity = BaseLightIntensity;
   }
   if (TeleportTrigger) TeleportTrigger.enabled = State == GateState.Active;
  }

  public void Discover() {
   if (Discovered) return;
   Discovered = true;
   if (State == GateState.Locked) SetState(GateState.Active);
   OnGateDiscovered?.Invoke(this);
  }

  public void Interact() {
   Discover();
   OnGateInteracted?.Invoke(this);
  }
 }
}

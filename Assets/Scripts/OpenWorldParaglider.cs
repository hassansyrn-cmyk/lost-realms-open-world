using UnityEngine;

namespace LostRealmsOW {
 // Cloth paraglider held overhead with both hands (Breath of the Wild style, original design): a cambered
 // fabric canopy on six suspension lines over a wooden bar. Built procedurally (mesh and a painted fabric
 // texture), it unfolds with a snap when opened, folds away on landing, banks into turns and its trailing
 // edge flutters in the airflow. The hero's driver hangs the body from GripLeft / GripRight.
 public sealed class OpenWorldParaglider : MonoBehaviour {
  const int SpanSegments = 24, ChordSegments = 6;
  const float HalfSpan = 1.42f, CanopyHeight = 1.28f, Droop = 0.52f, BarHeight = 2.12f;

  public Transform GripLeft { get; private set; }
  public Transform GripRight { get; private set; }
  public bool IsOpen => openTarget > 0.5f;
  public float Openness => open;

  Transform canopy;
  Mesh mesh;
  Vector3[] rest, live;
  LineRenderer[] lines;
  Vector3[] lineAnchors;
  Transform owner;
  float open, openTarget, bank, lastYaw, yawRate;
  int frame;
  static Texture2D fabric;

  public static OpenWorldParaglider Build(Transform heroRoot) {
   var go = new GameObject("Paraglider");
   go.transform.SetParent(heroRoot, false);
   go.transform.localPosition = new Vector3(0f, BarHeight, 0.02f);
   var g = go.AddComponent<OpenWorldParaglider>();
   g.owner = heroRoot;
   g.Construct();
   go.SetActive(false);
   return g;
  }

  public void SetOpen(bool on) {
   openTarget = on ? 1f : 0f;
   if (on && !gameObject.activeSelf) {
    gameObject.SetActive(true);
    open = 0f;
    lastYaw = owner ? owner.eulerAngles.y : 0f;
   }
  }

  void Construct() {
   // Bar and grips.
   var wood = OpenWorldMaterialHelper.CreateLit("ParagliderBar", new Color(0.36f, 0.22f, 0.12f), null, null, 0.3f, 0f);
   var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
   bar.name = "Bar";
   Destroy(bar.GetComponent<Collider>());
   bar.transform.SetParent(transform, false);
   bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
   bar.transform.localScale = new Vector3(0.045f, 0.33f, 0.045f);
   bar.GetComponent<Renderer>().sharedMaterial = wood;
   bar.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
   GripLeft = new GameObject("GripLeft").transform;
   GripLeft.SetParent(transform, false);
   GripLeft.localPosition = new Vector3(-0.2f, -0.075f, 0f);
   GripRight = new GameObject("GripRight").transform;
   GripRight.SetParent(transform, false);
   GripRight.localPosition = new Vector3(0.2f, -0.075f, 0f);

   // Canopy.
   canopy = new GameObject("Canopy").transform;
   canopy.SetParent(transform, false);
   var mf = canopy.gameObject.AddComponent<MeshFilter>();
   var mr = canopy.gameObject.AddComponent<MeshRenderer>();
   mr.sharedMaterial = OpenWorldMaterialHelper.CreateLit("ParagliderFabric", Color.white, Fabric(), null, 0.12f, 0f);
   mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
   mesh = new Mesh { name = "ParagliderCanopy" };
   mesh.MarkDynamic();
   int cols = SpanSegments + 1, rows = ChordSegments + 1, side = cols * rows;
   rest = new Vector3[side * 2];
   live = new Vector3[side * 2];
   var uv = new Vector2[side * 2];
   for (int j = 0; j < rows; j++)
    for (int i = 0; i < cols; i++) {
     float s = i / (float)SpanSegments * 2f - 1f, c = j / (float)ChordSegments;
     var p = CanopyPoint(s, c);
     int k = j * cols + i;
     rest[k] = rest[k + side] = p;
     uv[k] = uv[k + side] = new Vector2((s + 1f) * 0.5f, c);
    }
   var tris = new int[SpanSegments * ChordSegments * 12];
   int t = 0;
   for (int j = 0; j < ChordSegments; j++)
    for (int i = 0; i < SpanSegments; i++) {
     int a = j * cols + i, b = a + 1, d = a + cols, e = d + 1;
     // Top face (normals up), then the underside with reversed winding on its own vertices.
     tris[t++] = a; tris[t++] = d; tris[t++] = b;
     tris[t++] = b; tris[t++] = d; tris[t++] = e;
     tris[t++] = a + side; tris[t++] = b + side; tris[t++] = d + side;
     tris[t++] = b + side; tris[t++] = e + side; tris[t++] = d + side;
    }
   System.Array.Copy(rest, live, rest.Length);
   mesh.vertices = live;
   mesh.uv = uv;
   mesh.triangles = tris;
   mesh.RecalculateNormals();
   mesh.RecalculateBounds();
   mf.sharedMesh = mesh;

   // Suspension lines from the bar ends to the canopy.
   lineAnchors = new[] {
    new Vector3(-1f, 0.18f, 0f), new Vector3(-0.55f, 0f, 0f), new Vector3(-0.55f, 1f, 0f),
    new Vector3(1f, 0.18f, 0f), new Vector3(0.55f, 0f, 0f), new Vector3(0.55f, 1f, 0f)
   };
   var lineMat = new Material(Shader.Find("Sprites/Default")) { name = "ParagliderLines", color = new Color(0.16f, 0.12f, 0.1f, 0.9f) };
   lines = new LineRenderer[lineAnchors.Length];
   for (int i = 0; i < lines.Length; i++) {
    var lr = new GameObject("Line" + i).AddComponent<LineRenderer>();
    lr.transform.SetParent(transform, false);
    lr.useWorldSpace = false;
    lr.positionCount = 2;
    lr.widthMultiplier = 0.008f;
    lr.sharedMaterial = lineMat;
    lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    lr.receiveShadows = false;
    lines[i] = lr;
   }
  }

  static Vector3 CanopyPoint(float s, float c) {
   float x = s * HalfSpan * (1f - 0.06f * s * s);
   float y = CanopyHeight - Droop * s * s + 0.1f * Mathf.Sin(Mathf.PI * c) * (1f - 0.3f * s * s);
   float z = Mathf.Lerp(0.56f, -0.5f, c) * (1f - 0.26f * s * s);
   return new Vector3(x, y, z);
  }

  // Painted fabric: alternating crimson and rust panels, a cream leading edge, gold trim and seams and an
  // original wind sigil in the middle.
  static Texture2D Fabric() {
   if (fabric) return fabric;
   const int W = 256, H = 128;
   fabric = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "ParagliderFabric", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
   var px = new Color32[W * H];
   var crimson = new Color(0.6f, 0.15f, 0.12f); var rust = new Color(0.47f, 0.14f, 0.1f);
   var cream = new Color(0.94f, 0.87f, 0.7f); var gold = new Color(0.86f, 0.64f, 0.26f); var seam = new Color(0.25f, 0.07f, 0.06f);
   for (int y = 0; y < H; y++)
    for (int x = 0; x < W; x++) {
     float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
     int panel = Mathf.FloorToInt(u * 8f);
     Color col = panel % 2 == 0 ? crimson : rust;
     // Fine weave.
     float weave = ((x + y) % 4 == 0 ? 0.97f : 1f) * (0.96f + 0.04f * Mathf.PerlinNoise(x * 0.21f, y * 0.21f));
     col *= weave;
     if (v < 0.14f) col = Color.Lerp(cream, cream * 0.92f, v / 0.14f);
     if (v > 0.9f || u < 0.035f || u > 0.965f) col = gold;
     float seamDist = Mathf.Abs(u * 8f - Mathf.Round(u * 8f));
     if (seamDist < 0.035f && v > 0.14f) col = Color.Lerp(seam, col, seamDist / 0.035f);
     // Wind sigil: three nested arcs over a dot, centred on the canopy.
     float dx = (u - 0.5f) * 2.0f, dy = (v - 0.55f) * 1.0f;
     float r = Mathf.Sqrt(dx * dx + dy * dy);
     float ang = Mathf.Atan2(dy, dx);
     if (dy < 0.02f && r < 0.36f) {
      for (int k = 0; k < 3; k++) {
       float ring = 0.12f + k * 0.09f;
       if (Mathf.Abs(r - ring) < 0.018f && ang < -0.35f && ang > -2.8f) col = cream;
      }
     }
     if (r < 0.045f) col = gold;
     col.a = 1f;
     px[y * W + x] = col;
    }
   fabric.SetPixels32(px);
   fabric.Apply(true, true);
   return fabric;
  }

  void LateUpdate() {
   float dt = Time.deltaTime;
   if (dt <= 0f) return;
   open = Mathf.MoveTowards(open, openTarget, dt / (openTarget > open ? 0.32f : 0.18f));
   if (open <= 0f && openTarget <= 0f) { gameObject.SetActive(false); return; }

   // Bank into turns, with a slow breathing bob.
   if (owner) {
    float yaw = owner.eulerAngles.y;
    yawRate = Mathf.Lerp(yawRate, Mathf.DeltaAngle(lastYaw, yaw) / dt, 1 - Mathf.Exp(-6 * dt));
    lastYaw = yaw;
   }
   bank = Mathf.Lerp(bank, Mathf.Clamp(-yawRate * 0.075f, -22f, 22f), 1 - Mathf.Exp(-5 * dt));
   float time = Time.time;
   transform.localPosition = new Vector3(0f, BarHeight + Mathf.Sin(time * 1.7f) * 0.025f, 0.02f);
   transform.localRotation = Quaternion.Euler(-4f + Mathf.Sin(time * 1.3f) * 1.5f, 0f, bank);

   // Unfold with a little overshoot.
   float e = EaseOutBack(open);
   canopy.localScale = new Vector3(Mathf.Lerp(0.1f, 1f, e), Mathf.Lerp(0.35f, 1f, e), Mathf.Lerp(0.55f, 1f, e));
   canopy.localPosition = new Vector3(0f, Mathf.Lerp(-0.6f, 0f, e), 0f);

   // Trailing-edge flutter and a soft billow.
   int cols = SpanSegments + 1, side = cols * (ChordSegments + 1);
   for (int k = 0; k < side; k++) {
    var p = rest[k];
    float s = p.x / HalfSpan, c = (k / cols) / (float)ChordSegments;
    float w = Mathf.Max(0f, c - 0.45f) / 0.55f;
    float flutter = Mathf.Sin(time * 17f + s * 7f + c * 3f) * 0.024f * w + Mathf.Sin(time * 9.3f + s * 3.1f) * 0.012f * w;
    float billow = Mathf.Sin(time * 2.2f + s * 1.7f) * 0.018f * (1f - Mathf.Abs(s));
    var q = new Vector3(p.x, p.y + flutter + billow, p.z - Mathf.Abs(flutter) * 0.4f);
    live[k] = q; live[k + side] = q;
   }
   mesh.vertices = live;
   if ((frame++ & 1) == 0) mesh.RecalculateNormals();

   bool showLines = open > 0.25f;
   for (int i = 0; i < lines.Length; i++) {
    if (lines[i].enabled != showLines) lines[i].enabled = showLines;
    if (!showLines) continue;
    var a = lineAnchors[i];
    var top = canopy.localPosition + Vector3.Scale(CanopyPoint(a.x, a.y), canopy.localScale);
    var bottom = new Vector3(Mathf.Sign(a.x) * 0.3f, 0.01f, 0f);
    lines[i].SetPosition(0, bottom);
    lines[i].SetPosition(1, top);
   }
  }

  static float EaseOutBack(float x) {
   const float c1 = 1.70158f, c3 = c1 + 1f;
   x = Mathf.Clamp01(x);
   return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
  }

  void OnDestroy() { if (mesh) Destroy(mesh); }
 }
}

using UnityEngine;
using UnityEngine.Rendering;

namespace LostRealmsOW {
 // Ground-conforming attack warning: a glowing rim and an inner fill that sweeps outwards over the
 // wind-up, draped over the terrain (and stone decks) instead of a flat cylinder that clips slopes.
 public sealed class OpenWorldTelegraph : MonoBehaviour {
  const int Segments = 48, Rings = 8;
  static Material rimMaterial, fillMaterial;
  static Texture2D rimTexture, fillTexture;
  static int groundMask = -2;

  Mesh rimMesh, fillMesh;
  MeshRenderer rimRenderer, fillRenderer;
  MaterialPropertyBlock block;
  float radius, duration, start, fadeStart = -1f;
  Color color;
  float[,] heights;  // [ring, segment] world heights on a polar grid
  Vector3[] fillVerts;

  public static OpenWorldTelegraph Show(Vector3 center, float radius, float duration, Color color) {
   var go = new GameObject("Attack Telegraph");
   go.transform.position = new Vector3(center.x, 0f, center.z);
   var t = go.AddComponent<OpenWorldTelegraph>();
   t.radius = Mathf.Max(0.5f, radius);
   t.duration = Mathf.Max(0.1f, duration);
   t.color = color;
   t.start = Time.time;
   t.Build(center.y);
   return t;
  }

  public void Dismiss() {
   if (fadeStart < 0f) fadeStart = Time.time;
  }

  static float Ground(float x, float z, float refY) {
   if (groundMask == -2) {
    int m = LayerMask.GetMask("Default", "WorldStatic");
    groundMask = m != 0 ? m : ~0;
   }
   var terrain = Terrain.activeTerrain;
   float th = terrain ? terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y : refY;
   float top = Mathf.Max(th, refY) + 2.5f;
   if (Physics.Raycast(new Vector3(x, top, z), Vector3.down, out var hit, 6f, groundMask, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.6f && hit.point.y > th - 0.15f)
    return hit.point.y;
   return th;
  }

  void Build(float refY) {
   EnsureMaterials();
   block = new MaterialPropertyBlock();
   Vector3 c = transform.position;
   heights = new float[Rings + 1, Segments];
   for (int r = 0; r <= Rings; r++) {
    float rr = radius * r / Rings;
    for (int s = 0; s < Segments; s++) {
     float a = s / (float)Segments * Mathf.PI * 2f;
     heights[r, s] = Ground(c.x + Mathf.Cos(a) * rr, c.z + Mathf.Sin(a) * rr, refY) + 0.07f;
    }
   }
   rimMesh = BuildRim();
   fillMesh = new Mesh { name = "Telegraph Fill" };
   fillMesh.MarkDynamic();
   fillVerts = new Vector3[Segments * (Rings + 1) + 1];
   var uvs = new Vector2[fillVerts.Length];
   var tris = new int[Segments * 3 + Segments * Rings * 6];
   int ti = 0;
   for (int s = 0; s < Segments; s++) {
    int a = 1 + s, b = 1 + (s + 1) % Segments;
    tris[ti++] = 0; tris[ti++] = b; tris[ti++] = a;
   }
   for (int r = 0; r < Rings; r++) for (int s = 0; s < Segments; s++) {
    int i0 = 1 + r * Segments + s, i1 = 1 + r * Segments + (s + 1) % Segments;
    int j0 = i0 + Segments, j1 = i1 + Segments;
    tris[ti++] = i0; tris[ti++] = i1; tris[ti++] = j0;
    tris[ti++] = i1; tris[ti++] = j1; tris[ti++] = j0;
   }
   // UV.x carries the normalised radius so the fill texture brightens towards its leading edge.
   uvs[0] = new Vector2(0.5f, 0.5f);
   for (int r = 0; r <= Rings; r++) for (int s = 0; s < Segments; s++) {
    float a = s / (float)Segments * Mathf.PI * 2f;
    float f = (r + 1f) / (Rings + 1f);
    uvs[1 + r * Segments + s] = new Vector2(0.5f + Mathf.Cos(a) * 0.5f * f, 0.5f + Mathf.Sin(a) * 0.5f * f);
   }
   UpdateFill(0.02f);
   fillMesh.uv = uvs;
   fillMesh.colors = White(fillVerts.Length);
   fillMesh.triangles = tris;
   rimRenderer = AddPart("Rim", rimMesh, rimMaterial);
   fillRenderer = AddPart("Fill", fillMesh, fillMaterial);
  }

  MeshRenderer AddPart(string name, Mesh mesh, Material mat) {
   var go = new GameObject(name);
   go.transform.SetParent(transform, false);
   go.AddComponent<MeshFilter>().sharedMesh = mesh;
   var mr = go.AddComponent<MeshRenderer>();
   mr.sharedMaterial = mat;
   mr.shadowCastingMode = ShadowCastingMode.Off;
   mr.receiveShadows = false;
   return mr;
  }

  float HeightAt(float normRadius, int segment) {
   float fr = Mathf.Clamp01(normRadius) * Rings;
   int r0 = Mathf.Min(Rings - 1, Mathf.FloorToInt(fr));
   float t = fr - r0;
   return Mathf.Lerp(heights[r0, segment], heights[r0 + 1, segment], t);
  }

  Mesh BuildRim() {
   var verts = new Vector3[Segments * 2];
   var uvs = new Vector2[Segments * 2];
   var tris = new int[Segments * 6];
   const float inner = 0.9f;
   for (int s = 0; s < Segments; s++) {
    float a = s / (float)Segments * Mathf.PI * 2f;
    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
    verts[s * 2] = dir * radius * inner + Vector3.up * HeightAt(inner, s);
    verts[s * 2 + 1] = dir * radius + Vector3.up * HeightAt(1f, s);
    float u = s / (float)Segments;
    uvs[s * 2] = new Vector2(u, 0f); uvs[s * 2 + 1] = new Vector2(u, 1f);
    int n = (s + 1) % Segments;
    int t = s * 6;
    tris[t] = s * 2; tris[t + 1] = n * 2; tris[t + 2] = s * 2 + 1;
    tris[t + 3] = n * 2; tris[t + 4] = n * 2 + 1; tris[t + 5] = s * 2 + 1;
   }
   var mesh = new Mesh { name = "Telegraph Rim", vertices = verts, uv = uvs, colors = White(verts.Length), triangles = tris };
   mesh.RecalculateBounds();
   return mesh;
  }

  static Color[] White(int n) {
   var c = new Color[n];
   for (int i = 0; i < n; i++) c[i] = Color.white;
   return c;
  }

  void UpdateFill(float progress) {
   fillVerts[0] = Vector3.up * HeightAt(0f, 0);
   for (int r = 0; r <= Rings; r++) {
    float nr = progress * (r + 1f) / (Rings + 1f);
    for (int s = 0; s < Segments; s++) {
     float a = s / (float)Segments * Mathf.PI * 2f;
     fillVerts[1 + r * Segments + s] = new Vector3(Mathf.Cos(a) * radius * nr, HeightAt(nr, s), Mathf.Sin(a) * radius * nr);
    }
   }
   fillMesh.vertices = fillVerts;
   fillMesh.RecalculateBounds();
  }

  void Update() {
   float p = Mathf.Clamp01((Time.time - start) / duration);
   float eased = 1f - (1f - p) * (1f - p);
   UpdateFill(Mathf.Max(0.02f, eased));
   float fade = fadeStart < 0f ? 1f : 1f - Mathf.Clamp01((Time.time - fadeStart) / 0.18f);
   float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 18f);
   block.SetColor("_BaseColor", new Color(color.r, color.g, color.b, (p >= 1f ? 1f : 0.65f + 0.35f * pulse) * fade));
   rimRenderer.SetPropertyBlock(block);
   block.SetColor("_BaseColor", new Color(color.r, color.g, color.b, (0.28f + 0.32f * eased) * fade));
   fillRenderer.SetPropertyBlock(block);
   if (fadeStart >= 0f && fade <= 0f) Destroy(gameObject);
  }

  static void EnsureMaterials() {
   if (rimMaterial) return;
   rimTexture = new Texture2D(64, 8, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Telegraph Rim" };
   var px = new Color[64 * 8];
   for (int y = 0; y < 8; y++) for (int x = 0; x < 64; x++) {
    float v = y / 7f;
    float a = Mathf.Pow(Mathf.Sin(v * Mathf.PI), 0.7f) * (v > 0.55f ? 1f : 0.65f);
    px[y * 64 + x] = new Color(1f, 1f, 1f, a);
   }
   rimTexture.SetPixels(px); rimTexture.Apply(false, true);
   fillTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Telegraph Fill" };
   px = new Color[64 * 64];
   for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) {
    float dx = (x - 31.5f) / 31.5f, dy = (y - 31.5f) / 31.5f;
    float r = Mathf.Sqrt(dx * dx + dy * dy);
    float a = r > 1f ? 0f : Mathf.Lerp(0.25f, 1f, Mathf.Pow(r, 2.2f));
    px[y * 64 + x] = new Color(1f, 1f, 1f, a);
   }
   fillTexture.SetPixels(px); fillTexture.Apply(false, true);
   rimMaterial = Make("Telegraph Rim", rimTexture);
   fillMaterial = Make("Telegraph Fill", fillTexture);
  }

  static Material Make(string name, Texture2D tex) {
   var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
   var m = new Material(shader) { name = name };
   m.SetTexture("_BaseMap", tex);
   m.mainTexture = tex;
   m.SetFloat("_Surface", 1f);
   m.SetFloat("_Blend", 2f);
   m.SetOverrideTag("RenderType", "Transparent");
   m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
   m.SetInt("_DstBlend", (int)BlendMode.One);
   m.SetInt("_ZWrite", 0);
   m.SetFloat("_Cull", 0f);
   m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
   m.renderQueue = (int)RenderQueue.Transparent + 5;
   return m;
  }

  void OnDestroy() {
   if (rimMesh) Destroy(rimMesh);
   if (fillMesh) Destroy(fillMesh);
  }
 }
}

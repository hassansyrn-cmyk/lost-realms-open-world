using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 public enum OWPlaceKind : byte { Rock = 0, Cliff = 1, Building = 2, Prop = 3, Fence = 4, Lamp = 5, Foliage = 6, WaterPlant = 7, Fx = 8, Cloud = 9, Landmark = 10 }

 // One placed kit object. Rotation is stored as yaw/pitch/roll so the JSON stays compact and readable.
 [Serializable] public struct OWPlacement {
  public string key;
  public Vector3 pos;
  public Vector3 euler;
  public float scale;
  public OWPlaceKind kind;
  public string zone;   // settlement or region id (streaming / debugging)
 }

 [Serializable] public sealed class OWPlacementSet {
  public string[] keys;
  public float[] data;      // per placement: keyIndex, x, y, z, ex, ey, ez, scale, kind
  public string[] zones;    // per placement zone ids (parallel)
  public string generated = "";

  public const int Stride = 9;

  public static OWPlacementSet Pack(List<OWPlacement> list) {
   var keys = new List<string>(); var index = new Dictionary<string, int>();
   var data = new float[list.Count * Stride];
   var zones = new string[list.Count];
   for (int i = 0; i < list.Count; i++) {
    var p = list[i];
    if (!index.TryGetValue(p.key, out int k)) { k = keys.Count; keys.Add(p.key); index[p.key] = k; }
    int o = i * Stride;
    data[o] = k; data[o + 1] = p.pos.x; data[o + 2] = p.pos.y; data[o + 3] = p.pos.z;
    data[o + 4] = p.euler.x; data[o + 5] = p.euler.y; data[o + 6] = p.euler.z; data[o + 7] = p.scale; data[o + 8] = (float)p.kind;
    zones[i] = p.zone ?? "";
   }
   return new OWPlacementSet { keys = keys.ToArray(), data = data, zones = zones, generated = DateTime.Now.ToString("s") };
  }

  public int Count => data == null ? 0 : data.Length / Stride;

  public OWPlacement Get(int i) {
   int o = i * Stride;
   return new OWPlacement {
    key = keys[(int)data[o]], pos = new Vector3(data[o + 1], data[o + 2], data[o + 3]),
    euler = new Vector3(data[o + 4], data[o + 5], data[o + 6]), scale = data[o + 7], kind = (OWPlaceKind)(int)data[o + 8],
    zone = zones != null && i < zones.Length ? zones[i] : ""
   };
  }
 }

 // Circle occupancy on a coarse grid: keeps trees out of houses, rocks off roads, props apart.
 public sealed class OWOccupancy {
  const float Cell = 8f;
  readonly Dictionary<long, List<Vector3>> cells = new Dictionary<long, List<Vector3>>(); // x, z, radius
  static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

  public void Add(Vector2 p, float r) {
   int x = Mathf.FloorToInt(p.x / Cell), z = Mathf.FloorToInt(p.y / Cell);
   long k = Key(x, z);
   if (!cells.TryGetValue(k, out var list)) cells[k] = list = new List<Vector3>();
   list.Add(new Vector3(p.x, p.y, r));
  }

  public bool Free(Vector2 p, float r, float maxOther = 24f) {
   int reach = Mathf.CeilToInt((r + maxOther) / Cell);
   int cx = Mathf.FloorToInt(p.x / Cell), cz = Mathf.FloorToInt(p.y / Cell);
   for (int dz = -reach; dz <= reach; dz++) for (int dx = -reach; dx <= reach; dx++) {
    if (!cells.TryGetValue(Key(cx + dx, cz + dz), out var list)) continue;
    foreach (var c in list) {
     float rr = c.z + r;
     float ex = c.x - p.x, ez = c.y - p.y;
     if (ex * ex + ez * ez < rr * rr) return false;
    }
   }
   return true;
  }

  public void Clear() { cells.Clear(); }
 }

 // Oriented building footprints (used by the ecosystem to keep vegetation out of yards).
 public struct OWFootprint {
  public Vector2 Center, Half; public float Yaw;
  public bool Contains(Vector2 p, float margin) {
   // World -> local for a Unity yaw (Quaternion.Euler(0, Yaw, 0)).
   float c = Mathf.Cos(Yaw * Mathf.Deg2Rad), s = Mathf.Sin(Yaw * Mathf.Deg2Rad);
   Vector2 d = p - Center;
   float lx = d.x * c - d.y * s, lz = d.x * s + d.y * c;
   return Mathf.Abs(lx) < Half.x + margin && Mathf.Abs(lz) < Half.y + margin;
  }
 }
}

using System.Threading;
namespace AbstractOcclusion.WebGpuWater {
 internal static class WaterQueryOwnerIds {
  static int next;
  internal static int Next() => Interlocked.Increment(ref next);
 }
}

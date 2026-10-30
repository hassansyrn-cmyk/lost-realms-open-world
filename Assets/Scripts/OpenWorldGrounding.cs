using UnityEngine;

namespace LostRealmsOW {
 // Terrain-backed foundations and walkable steps; never raycast onto an actor's own mesh.
 public static class OpenWorldGrounding {
  static Material foundationMaterial;
  public static bool TryWalk(Terrain terrain, Vector3 current, Vector3 next, out float height) {
   height = current.y;
   if (!terrain || !terrain.terrainData) return false;
   Vector3 local = next - terrain.transform.position;
   Vector3 size = terrain.terrainData.size;
   if (local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z) return false;
   height = terrain.SampleHeight(next) + terrain.transform.position.y;
   return terrain.terrainData.GetSteepness(local.x / size.x, local.z / size.z) <= 35f && Mathf.Abs(height - current.y) < .65f;
  }
  public static void SupportBuilding(GameObject model, Terrain terrain) {
   if (!terrain) return;
   var bounds = OpenWorldMaterialHelper.ComputeRendererBounds(model.transform);
   float low = float.PositiveInfinity, high = float.NegativeInfinity;
   // Include centre, edges and corners: a centre-only sample leaves corners floating.
   for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) {
    var point = bounds.center + new Vector3(x * bounds.extents.x, 0, z * bounds.extents.z);
    float y = terrain.SampleHeight(point) + terrain.transform.position.y;
    low = Mathf.Min(low, y); high = Mathf.Max(high, y);
   }
   model.transform.position += Vector3.up * (high + .04f - bounds.min.y);
   if (high - low < .08f) return;
   // A solid plinth supports the exterior; it is part of the same streamed instance.
   var foundation = GameObject.CreatePrimitive(PrimitiveType.Cube);
   foundation.name = "Terrain fitted stone foundation";
   foundation.transform.SetParent(model.transform.parent, false);
   foundation.transform.rotation = Quaternion.identity;
   foundation.transform.position = new Vector3(bounds.center.x, (low + high) * .5f - .08f, bounds.center.z);
   foundation.transform.localScale = new Vector3(bounds.size.x, high - low + .24f, bounds.size.z);
   if (!foundationMaterial) {
    foundationMaterial = new Material(OpenWorldMaterialHelper.LitShader) { name = "Village foundation stone" };
    var stone = Resources.Load<TerrainLayer>("World/Terrain/Rock");
    OpenWorldMaterialHelper.SetLitProperties(foundationMaterial, new Color(.65f, .66f, .61f), stone ? stone.diffuseTexture : null, stone ? stone.normalMapTexture : null, .05f, 0f, Color.black);
    foundationMaterial.mainTextureScale = new Vector2(3, 1);
   }
   foundation.GetComponent<Renderer>().sharedMaterial = foundationMaterial;
  }
  public static void ReleaseMaterials() {
   if (foundationMaterial) Object.Destroy(foundationMaterial);
   foundationMaterial = null;
  }
 }
}

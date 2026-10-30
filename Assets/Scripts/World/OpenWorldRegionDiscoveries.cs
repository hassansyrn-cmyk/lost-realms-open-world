using System.Collections.Generic;
using UnityEngine;

namespace LostRealmsOW {
 // Part C (brief Docs/TASKS_GLM_2026-10-30.md): quiet discoveries for the four regions Codex's Verdant
 // route does not cover — a story note and a hidden cache near each region's quest area, but off its
 // paths. Notes are readable from a few metres (TextMesh markers, like the hunt tracks); caches are kit
 // props holding a one-time reward. Progress is saved in CollectedWorldIds under the glm-disc: prefix,
 // so every reward pays exactly once per save.
 public sealed class OpenWorldRegionDiscoveriesRuntime : MonoBehaviour {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Install() { if (!FindAnyObjectByType<OpenWorldRegionDiscoveriesRuntime>()) new GameObject("Region discoveries").AddComponent<OpenWorldRegionDiscoveriesRuntime>(); }
  void Update() {
   var gm = OpenWorldManager.I;
   if (gm && OpenWorldMobileHud.Instance) OpenWorldMobileHud.Instance.TickRegionDiscoveries(gm);
  }
 }

 public struct RegionDiscovery {
  public string Id, Title, Note, Region;
  public Vector3 Position;
  public int Gold, Gems;
  public string PropResource, PropTexture;
  public bool Cache; // caches hold the reward inside a prop; notes are free-standing lore
 }

 public sealed partial class OpenWorldMobileHud {
  GameObject discRoot;
  readonly List<GameObject> discMarkers = new List<GameObject>();
  static readonly RegionDiscovery[] Discoveries = {
   // Frozen Peaks — off the sled path, overlooking Frostpine.
   new RegionDiscovery { Id = "glm-disc:frozen-note", Region = "Frozen Peaks", Title = "SCOUT'S LAST LETTER",
    Note = "The sled tracks end here, but the scout's letter went farther: 'The peak remembers every name. Leave yours and it may let you pass.'",
    Position = new Vector3(-160f, 0f, 288f), Gold = 20, Gems = 1, PropResource = "Props/Expansion/TrailShrine", PropTexture = "Props/Expansion/Textures/TrailShrine_basecolor", Cache = false },
   new RegionDiscovery { Id = "glm-disc:frozen-cache", Region = "Frozen Peaks", Title = "FROZEN SUPPLY CACHE",
    Note = "A crate buried under the drifts — spare rations and a handful of gems, still frozen solid.",
    Position = new Vector3(-96f, 0f, 300f), Gold = 30, Gems = 3, PropResource = "Props/Expansion/SecretCache", PropTexture = "Props/Expansion/Textures/SecretCache_basecolor", Cache = true },
   // Burning Dunes — between Sunspire and the ruins, away from the brazier pillars.
   new RegionDiscovery { Id = "glm-disc:dunes-note", Region = "Burning Dunes", Title = "CARAVAN LEDGER PAGE",
    Note = "A ledger page baked half-legible: 'Paid the Sunspire keepers in water and silence. The dunes take both eventually.'",
    Position = new Vector3(296f, 0f, -8f), Gold = 20, Gems = 1, PropResource = "Props/Expansion/TrailShrine", PropTexture = "Props/Expansion/Textures/TrailShrine_basecolor", Cache = false },
   new RegionDiscovery { Id = "glm-disc:dunes-cache", Region = "Burning Dunes", Title = "SUNKEN CARAVAN STRONGBOX",
    Note = "A strongbox lost when the caravan cut east. The lock rusted open generations ago.",
    Position = new Vector3(372f, 0f, 88f), Gold = 35, Gems = 3, PropResource = "Props/Expansion/SecretCache", PropTexture = "Props/Expansion/Textures/SecretCache_basecolor", Cache = true },
   // Hollow Marches — outside the graveyard fence line, away from the lantern circle.
   new RegionDiscovery { Id = "glm-disc:marches-note", Region = "Hollow Marches", Title = "MOURNER'S VOW",
    Note = "Scratched into the marker: 'I lit four lanterns so the fifth could rest. Whoever finds this — let them all rest.'",
    Position = new Vector3(-190f, 0f, -206f), Gold = 25, Gems = 1, PropResource = "Props/Expansion/TrailShrine", PropTexture = "Props/Expansion/Textures/TrailShrine_basecolor", Cache = false },
   new RegionDiscovery { Id = "glm-disc:marches-cache", Region = "Hollow Marches", Title = "MOURNER'S OFFERING",
    Note = "An offering box the revenants never touched. Perhaps they approved of what it held.",
    Position = new Vector3(-238f, 0f, -244f), Gold = 35, Gems = 2, PropResource = "Props/Expansion/SecretCache", PropTexture = "Props/Expansion/Textures/SecretCache_basecolor", Cache = true },
   // Stormveil — below the overlook, away from the lightning rods.
   new RegionDiscovery { Id = "glm-disc:storm-note", Region = "Stormveil", Title = "STORMWARDEN'S TEST",
    Note = "A weathered plate: 'Rhega's test is not the storm. The storm is honest. The test is whether you climb back up.'",
    Position = new Vector3(222f, 0f, -250f), Gold = 25, Gems = 1, PropResource = "Props/Expansion/TrailShrine", PropTexture = "Props/Expansion/Textures/TrailShrine_basecolor", Cache = false },
   new RegionDiscovery { Id = "glm-disc:storm-cache", Region = "Stormveil", Title = "LIGHTNING-SEALED CHEST",
    Note = "A chest struck so many times the bolts fused shut — then burst open one night. The storm got there first and left the best parts.",
    Position = new Vector3(258f, 0f, -296f), Gold = 40, Gems = 3, PropResource = "Props/Expansion/SecretCache", PropTexture = "Props/Expansion/Textures/SecretCache_basecolor", Cache = true },
  };

  public static IReadOnlyList<RegionDiscovery> RegionalDiscoveries => Discoveries;
  internal void TickRegionDiscoveries(OpenWorldManager gm) {
   if (!gm.Hero || !gm.ActiveTerrain) return;
   if (!discRoot) {
    discMarkers.Clear();discRoot=new GameObject("Region discoveries");discRoot.transform.SetParent(gm.WorldRoot,false);
    foreach(var data in Discoveries){
     var root=new GameObject("Discovery "+data.Id);root.transform.SetParent(discRoot.transform,false);root.transform.position=gm.HuntGround(data.Position);
     var tag=root.AddComponent<RegionDiscoveryTag>();tag.Data=data;discMarkers.Add(root);
    }
   }
   bool built=false;
   foreach(var marker in discMarkers){
    if(!marker)continue;var tag=marker.GetComponent<RegionDiscoveryTag>();
    float d=Vector3.Distance(gm.Hero.transform.position,marker.transform.position);
    if(d<65&&!tag.LoadingStarted){tag.Request();break;} // Start only one site's requests in a frame.
   }
   foreach(var marker in discMarkers){
    if(!marker)continue;var tag=marker.GetComponent<RegionDiscoveryTag>();float d=Vector3.Distance(gm.Hero.transform.position,marker.transform.position);
    if(!tag.Ready&&tag.Loaded&&d<55&&!built&&Time.unscaledTime>=tag.RetryAt){built=true;tag.Build(gm);}
    bool show=tag.Ready&&d<45;marker.SetActive(show);
    bool collected=gm.CollectedWorldIds.Contains(tag.Id);
    if(tag.Model)tag.Model.SetActive(!tag.Data.Cache||!collected);
    if(tag.Label){tag.Label.gameObject.SetActive(show&&!collected&&d<11);if(Camera.main)tag.Label.transform.rotation=Camera.main.transform.rotation;}
   }
  }
  RegionDiscoveryTag NearestRegionDiscovery(OpenWorldManager gm){
   if(!gm||!gm.Hero||gm.Hero.Dead||gm.Hero.IsMounted||gm.IsModalOpen)return null;
   RegionDiscoveryTag best=null;float range=3.5f;
   foreach(var marker in discMarkers){
    if(!marker||!marker.activeInHierarchy)continue;var tag=marker.GetComponent<RegionDiscoveryTag>();
    if(!tag.Ready||gm.CollectedWorldIds.Contains(tag.Id))continue;
    float distance=Vector3.Distance(gm.Hero.transform.position,marker.transform.position);if(distance>=range)continue;
    Vector3 from=gm.Hero.transform.position+Vector3.up,to=marker.transform.position+Vector3.up*.9f;
    if(Physics.Linecast(from,to,out var hit,~0,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(gm.Hero.transform))continue;
    best=tag;range=distance;
   }
   return best;
  }
  public string RegionDiscoveryPrompt {
   get{var tag=NearestRegionDiscovery(OpenWorldManager.I);return tag?(tag.Data.Cache?"OPEN CACHE":"READ NOTE"):null;}
  }
  public bool TryDiscover(OpenWorldManager gm){
   var tag=NearestRegionDiscovery(gm);if(!tag||!gm.CollectedWorldIds.Add(tag.Id))return false;
   gm.Gold+=tag.Data.Gold;gm.Gems+=tag.Data.Gems;
   OpenWorldQuestJournal.Instance?.Record(tag.Title,tag.Data.Note);
   gm.MarkProgressDirty();gm.ShowBanner(tag.Title+": "+tag.Data.Note+" • +"+tag.Data.Gold+" gold, +"+tag.Data.Gems+" gems");
   OpenWorldAudio.Play("sfx_upgrade",.5f);TickRegionDiscoveries(gm);return true;
  }
#if UNITY_EDITOR
  // Legacy bridge helper belongs to the HUD partial; runtime gameplay never writes into Docs.
  public static void Discover(string id){
   var gm=OpenWorldManager.I;var hud=Instance;if(!gm||!hud)throw new System.Exception("World/HUD required");
   int gold=gm.Gold,gems=gm.Gems;
   if(!hud.TryDiscover(gm)||!gm.CollectedWorldIds.Contains(id))throw new System.Exception("Discovery not in reach: "+id);
   if(hud.TryDiscover(gm))throw new System.Exception("Discovery paid twice: "+id);
   System.IO.Directory.CreateDirectory("Docs");System.IO.File.AppendAllText("Docs/glm-discoveries-validation.txt","PASS: "+id+" paid once (+"+(gm.Gold-gold)+" gold, +"+(gm.Gems-gems)+" gems)\n");
  }
#endif
 }
 public sealed class RegionDiscoveryTag:MonoBehaviour {
  public RegionDiscovery Data;
  public string Id=>Data.Id;
  public string Title=>Data.Title;
  public string Region=>Data.Region;
  public bool Ready{get;private set;}
  public string PreparationState{get;private set;}="waiting";
  public bool LoadingStarted{get;private set;}
  public float RetryAt{get;private set;}
  ResourceRequest prefab,texture,normal;
  public bool Loaded=>LoadingStarted&&prefab.isDone&&texture.isDone&&normal.isDone;
  public GameObject Model{get;private set;}
  public TextMesh Label{get;private set;}
  // SpawnModel materials belong to the shared material cache; never destroy another site's material.
  public void Request(){LoadingStarted=true;prefab=Resources.LoadAsync<GameObject>(Data.PropResource);texture=Resources.LoadAsync<Texture2D>(Data.PropTexture);normal=Resources.LoadAsync<Texture2D>(Data.PropTexture.Replace("_basecolor","_normal"));}
  public void Build(OpenWorldManager gm){
   RetryAt=Time.unscaledTime+2;
   if(!FindSpot(gm,out var spot))return;
   transform.position=spot;
   Model=OpenWorldMaterialHelper.SpawnModel(Data.PropResource,Data.PropTexture,transform,spot,Data.Cache ? .9f : 1.4f,Color.white,false);
   if(!Model){PreparationState="prefab unavailable";return;}
   OpenWorldGrounding.SupportBuilding(Model,gm.ActiveTerrain);
   foreach(var collider in Model.GetComponentsInChildren<Collider>(true))collider.enabled=false;
   if(!Data.Cache){var paper=new GameObject("Letter stand");paper.transform.SetParent(transform,false);var baseAt=spot+new Vector3(.25f,0,-.65f);float baseY=gm.ActiveTerrain.SampleHeight(baseAt)+gm.ActiveTerrain.transform.position.y;paper.transform.localPosition=Vector3.back*.8f+Vector3.up*(baseY-spot.y);OpenWorldTrailNote.Build(paper.transform);}
   var label=new GameObject("Marker label");label.transform.SetParent(transform,false);label.transform.localPosition=Vector3.up*1.9f;
   Label=label.AddComponent<TextMesh>();Label.text=Data.Title+"\n"+(Data.Cache?"OPEN CACHE":"READ NOTE");Label.anchor=TextAnchor.MiddleCenter;Label.characterSize=.05f;Label.fontSize=30;
   Label.color=Data.Cache?new Color(1,.84f,.52f):new Color(.8f,.88f,1);Ready=true;PreparationState="ready";
  }
  bool FindSpot(OpenWorldManager gm,out Vector3 best){
   best=Vector3.zero;float score=float.PositiveInfinity,minRelief=float.PositiveInfinity;var terrain=gm.ActiveTerrain;int dry=0,clear=0;
   for(int ring=0;ring<=8;ring++)for(int angle=0;angle<(ring==0?1:12);angle++){
    float a=angle*Mathf.PI/6;var p=Data.Position+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*ring*4;
    p.y=terrain.SampleHeight(p)+terrain.transform.position.y;
    if(OpenWorldTerrainSculptor.WaterAt(p.x,p.z,out _,out _))continue;dry++;if(OpenWorldPopulation.Blocked(p,1.5f,1.1f))continue;clear++;
    float lo=p.y,hi=p.y;
    for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++){var q=p+new Vector3(x,0,z)*1.1f;float y=terrain.SampleHeight(q)+terrain.transform.position.y;lo=Mathf.Min(lo,y);hi=Mathf.Max(hi,y);}
    minRelief=Mathf.Min(minRelief,hi-lo);if(hi-lo>.35f)continue;
    float value=ring*4+(hi-lo)*20;if(value>=score)continue;score=value;best=new Vector3(p.x,hi+.015f,p.z);
   }
   PreparationState="dry "+dry+", clear "+clear+", minimum relief "+minRelief.ToString("F2")+" m";return score<float.PositiveInfinity;
  }

 }
}

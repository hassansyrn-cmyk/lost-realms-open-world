using UnityEngine;
namespace LostRealmsOW {
 // A small authored detour: a broken hunter marker, its hidden supplies, and an optional raider camp.
 public sealed class OpenWorldHuntingDiscoveries : MonoBehaviour {
  public static OpenWorldHuntingDiscoveries Instance{get;private set;}
  public static readonly Vector3 NoteAnchor=new Vector3(72,0,27),CacheAnchor=new Vector3(81,0,35),CampAnchor=new Vector3(98,0,26);
  public const string NoteId="hunt-discovery:note",CacheId="hunt-discovery:cache",CampId="hunt-discovery:camp";
  OpenWorldManager gm;GameObject cluster;OpenWorldEnemy raider;OpenWorldDiscoverySite[] sites;float nextTick;
  public bool EncounterActive=>raider&&!raider.Dead;
  public OpenWorldDiscoverySite Nearby{get;private set;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Install(){if(!FindAnyObjectByType<OpenWorldHuntingDiscoveries>())new GameObject("Hunting route discoveries").AddComponent<OpenWorldHuntingDiscoveries>();}
  void Awake(){Instance=this;}
  void Update(){
   gm=OpenWorldManager.I;if(!gm||!gm.Hero||!gm.ActiveTerrain||!gm.RealmActive)return;
   if(!gm.IsModalOpen&&!gm.Hero.Dead&&Input.GetKeyDown(KeyCode.F)&&Nearby)Interact(Nearby);
   if(Time.unscaledTime<nextTick)return;nextTick=Time.unscaledTime+.2f;
   Vector3 delta=gm.Hero.transform.position-new Vector3(85,0,31);delta.y=0;
   if(delta.magnitude>135){Clear();return;}
   if(!cluster&&delta.magnitude<95)Build();
   Nearby=null;if(gm.IsModalOpen||gm.Hero.Dead||sites==null)return;
   float best=2.6f;foreach(var site in sites){if(!site)continue;site.Refresh(gm);if(!site.Available)continue;float d=Vector3.Distance(gm.Hero.transform.position,site.transform.position);if(d<best){best=d;Nearby=site;}}
   if(raider&&raider.Dead&&gm.CollectedWorldIds.Add(CampId)){
    gm.Gold+=40;gm.Gems+=1;gm.MarkProgressDirty();gm.ShowBanner("BRIARFOOT CAMP CLEARED • 40 gold, 1 gem");
    OpenWorldQuestJournal.Instance?.Record("FIELD NOTE","The raider carried Oakvale's stolen trail tokens. The hunters' path is safer, even before Thornhide falls.");
   }
  }
  public void Interact(OpenWorldDiscoverySite site){
   if(!gm||!site||gm.IsModalOpen||!gm.Hero||gm.Hero.Dead||Vector3.Distance(gm.Hero.transform.position,site.transform.position)>3)return;
   if(site.Kind==0){
    const string note="Kael—Thornhide scattered us at the ridge. Lin led the wounded home. I hid our spare coin behind the split stone, nine paces northeast of this fallen marker. Keep off the southern spur: someone is taking the hunters' supplies. —Eren";
    gm.CollectedWorldIds.Add(NoteId);gm.MarkProgressDirty();gm.ShowBanner("FIELD NOTE: "+note);OpenWorldQuestJournal.Instance?.Record("FIELD NOTE",note);
   }else if(site.Kind==1){
    if(!gm.CollectedWorldIds.Add(CacheId))return;gm.Gold+=35;gm.Gems+=2;gm.MarkProgressDirty();gm.ShowBanner("HUNTERS' HIDDEN CACHE • 35 gold, 2 gems");OpenWorldAudio.Play("sfx_upgrade",.55f);
    OpenWorldQuestJournal.Instance?.Record("FIELD NOTE","Recovered Eren's supplies behind the split stone. An Oakvale ribbon still circles the latch.");site.Refresh(gm);
   }else{
    if(gm.CollectedWorldIds.Contains(CampId)||raider)return;
    var p=site.transform.position+Vector3.forward*3;p=gm.HuntGround(p);
    if(OpenWorldPopulation.Blocked(p,2,.6f)){gm.ShowBanner("The camp's approach is blocked. Circle around the marker.");return;}
    raider=OpenWorldEnemy.Spawn("IronGoblin","Briarfoot Supply Thief",p,cluster.transform);raider.MaxHealth=raider.Health=14;raider.AttackDamage=.85f;raider.MoveSpeed=3.2f;raider.LeashRadius=18;gm.Enemies.Add(raider);
    gm.ShowBanner("OPTIONAL ENCOUNTER • Briarfoot Supply Thief • Leave the spur to retreat");
   }
  }
  void Build(){
   cluster=new GameObject("Oakvale hunting-route discoveries");cluster.transform.SetParent(gm.WorldRoot,false);
   sites=new[]{BuildSite(0,NoteAnchor,"EREN'S FALLEN MARKER"),BuildSite(1,CacheAnchor,"HUNTERS' HIDDEN CACHE"),BuildSite(2,CampAnchor,"ABANDONED SUPPLY CAMP")};
   cluster.AddComponent<OpenWorldTrailPresentation>().Build(sites,gm);
  }
  OpenWorldDiscoverySite BuildSite(int kind,Vector3 anchor,string title){
   if(!SafeGround(anchor,out var p)){Debug.LogWarning("[HuntDiscoveries] No clear ground for "+title);return null;}
   var root=new GameObject(title);root.transform.SetParent(cluster.transform,false);root.transform.position=p;
   var site=root.AddComponent<OpenWorldDiscoverySite>();site.Kind=kind;site.Title=title;
   if(kind==1){
    site.CacheVisual=OpenWorldMaterialHelper.SpawnModel("Props/Expansion/SecretCache","Props/Expansion/Textures/SecretCache_basecolor",root.transform,p,.65f,Color.white,false);
    // Two natural stones shelter the cache; the opening is left clear to approach from the south.
    for(int k=0;k<2;k++){
     var prefab=OpenWorldWorldKit.Prefab(k==0?"rock_medium_01":"rock_medium_02");if(!prefab)continue;
     var rock=Instantiate(prefab,root.transform);rock.name="Split stone • world-kit rock "+k;rock.transform.rotation=Quaternion.Euler(0,k*87+31,0);
     var bounds=OpenWorldMaterialHelper.ComputeRendererBounds(rock.transform);rock.transform.localScale*= (k==0?1.6f:1.15f)/Mathf.Max(.01f,bounds.size.y);bounds=OpenWorldMaterialHelper.ComputeRendererBounds(rock.transform);
     var q=gm.HuntGround(p+new Vector3(k==0?-1.4f:1.4f,0,.9f));rock.transform.position+=q-new Vector3(bounds.center.x,bounds.min.y+.1f,bounds.center.z);
    }
   }else if(kind==0){
    var banner=OpenWorldMaterialHelper.SpawnModel("Props/Expansion/RallyBanner","Props/Expansion/Textures/RallyBanner_basecolor",root.transform,p,1.3f,new Color(.6f,.72f,.48f),false);if(banner)banner.transform.Rotate(0,35,72);
    OpenWorldTrailNote.Build(root.transform);
    Part(root.transform,"Broken shaft",new Vector3(-.1f,.09f,-.3f),new Vector3(.065f,.065f,1.8f),new Color(.23f,.16f,.09f));
   }else{
    var campBanner=OpenWorldMaterialHelper.SpawnModel("Props/Expansion/RallyBanner","Props/Expansion/Textures/RallyBanner_basecolor",root.transform,p+new Vector3(-1.7f,0,.9f),1.7f,new Color(.65f,.38f,.2f),false);if(campBanner)campBanner.transform.Rotate(0,28,-12);
    // Spent fire and two abandoned bedrolls imply a camp that was taken over, without a quest wall.
    for(int k=0;k<7;k++){float a=k*Mathf.PI*2/7;Part(root.transform,"Cold hearth stone",new Vector3(Mathf.Cos(a)*.6f,.1f,Mathf.Sin(a)*.6f),new Vector3(.28f,.18f,.24f),new Color(.22f,.23f,.2f));}
    Part(root.transform,"Ash",new Vector3(0,.025f,0),new Vector3(.8f,.04f,.8f),new Color(.07f,.065f,.05f));
    Part(root.transform,"Abandoned bedroll",new Vector3(-1.6f,.07f,0),new Vector3(.8f,.12f,1.8f),new Color(.19f,.25f,.2f));
    Part(root.transform,"Cut bedroll",new Vector3(1.5f,.07f,.1f),new Vector3(.8f,.12f,1.8f),new Color(.3f,.24f,.16f));
   }
   site.Refresh(gm);return site;
  }
  bool SafeGround(Vector3 anchor,out Vector3 p){
   Vector3 bestPoint=anchor;float best=float.MaxValue;bool found=false;
   for(int i=0;i<48;i++){
    var candidate=gm.HuntGround(anchor+Quaternion.Euler(0,i*137.5f,0)*Vector3.forward*(i==0?0:1+i*.13f));
    if(OpenWorldTerrainSculptor.WaterAt(candidate.x,candidate.z,out _,out _)||OpenWorldPopulation.Blocked(candidate,2,2))continue;
    bool level=true;for(int k=0;k<8;k++){var q=candidate+Quaternion.Euler(0,k*45,0)*Vector3.forward*1.8f;if(Mathf.Abs(gm.HuntGround(q).y-candidate.y)>.3f){level=false;break;}}
    if(!level)continue;bool separate=true;if(cluster)foreach(Transform t in cluster.transform)if((candidate-t.position).sqrMagnitude<25){separate=false;break;}if(!separate)continue;
    float score=OpenWorldWildlife.GrassAt(gm.ActiveTerrain,candidate)+(candidate-anchor).sqrMagnitude*.18f;
    if(score<best){best=score;bestPoint=candidate;found=true;}
   }
   p=bestPoint;return found;
  }
  static void Part(Transform root,string name,Vector3 p,Vector3 size,Color color){var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(root,false);obj.transform.localPosition=p;obj.transform.localScale=size;Destroy(obj.GetComponent<Collider>());obj.GetComponent<Renderer>().sharedMaterial=OpenWorldMaterialHelper.CreateLit("Hunter "+name,color);}
  void Clear(){Nearby=null;if(raider&&gm)gm.Enemies.Remove(raider);if(cluster)Destroy(cluster);cluster=null;raider=null;sites=null;}
  void OnDestroy(){Clear();if(Instance==this)Instance=null;}
 }
 public sealed class OpenWorldDiscoverySite:MonoBehaviour {
  public int Kind;public string Title;public GameObject CacheVisual;public bool Available{get;private set;}
  public string Prompt=>Kind==0?"READ FIELD NOTE  [F]":Kind==1?"OPEN HIDDEN CACHE  [F]":"INVESTIGATE CAMP  [F]";
  public void Refresh(OpenWorldManager gm){Available=Kind==0||!gm.CollectedWorldIds.Contains(Kind==1?OpenWorldHuntingDiscoveries.CacheId:OpenWorldHuntingDiscoveries.CampId);if(Kind==2&&OpenWorldHuntingDiscoveries.Instance&&OpenWorldHuntingDiscoveries.Instance.EncounterActive)Available=false;if(CacheVisual)CacheVisual.SetActive(Available);}
 }
 public sealed partial class OpenWorldMobileHud {
  UnityEngine.UI.Button discoveryButton;UnityEngine.UI.Text discoveryText;
  internal void UpdateDiscoveryControl(OpenWorldManager gm){
   if(!safeRoot)return;if(!discoveryButton){discoveryButton=PillButton(safeRoot,"DISCOVER",new Vector2(-200,315),new Vector2(310,50),new Vector2(1,0),17);discoveryText=discoveryButton.GetComponentInChildren<UnityEngine.UI.Text>();discoveryButton.onClick.AddListener(()=>{var d=OpenWorldHuntingDiscoveries.Instance;if(d&&d.Nearby)d.Interact(d.Nearby);});}
   var owner=OpenWorldHuntingDiscoveries.Instance;bool show=owner&&owner.Nearby&&!gm.IsModalOpen&&!gm.Hero.Dead&&(!titleRoot||!titleRoot.activeSelf);discoveryButton.gameObject.SetActive(show);if(show&&discoveryText)discoveryText.text=owner.Nearby.Prompt;
  }
 }
 public sealed class OpenWorldDiscoveryHud:MonoBehaviour {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install(){new GameObject("Discovery controls").AddComponent<OpenWorldDiscoveryHud>();}
  void LateUpdate(){if(OpenWorldMobileHud.Instance&&OpenWorldManager.I&&OpenWorldManager.I.Hero)OpenWorldMobileHud.Instance.UpdateDiscoveryControl(OpenWorldManager.I);}
 }
}




using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
namespace LostRealmsOW {
 public struct OWJournalQuest {
  public string Id,Title,Region,Objective;public Vector3 Target;public int Stage,End;public bool Main;
  public bool Complete=>Stage>=End;
  public string Status=>Complete?"COMPLETE":Stage==0&&!Main?"NOT STARTED":"IN PROGRESS";
 }
 // Progress and transcript share the existing per-adventure save, not machine-wide preferences.
 [DefaultExecutionOrder(20000)]
 public sealed class OpenWorldQuestJournal : MonoBehaviour {
  const string TrackPrefix="journal:track:",DialoguePrefix="journal:line:";
  public static OpenWorldQuestJournal Instance{get;private set;}
  public bool Open{get;private set;}
  public int Revision{get;private set;}
  OpenWorldManager gm;string previousBanner="";float nextObserve;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Install(){if(!FindAnyObjectByType<OpenWorldQuestJournal>())new GameObject("Quest journal").AddComponent<OpenWorldQuestJournal>();}
  void Awake(){Instance=this;}
  public string TrackedId {get{if(gm)foreach(var flag in gm.CollectedWorldIds)if(flag.StartsWith(TrackPrefix,StringComparison.Ordinal))return flag.Substring(TrackPrefix.Length);return "main";}}
  public void Track(string id){if(!gm||Array.IndexOf(Ids,id)<0)return;gm.CollectedWorldIds.RemoveWhere(f=>f.StartsWith(TrackPrefix,StringComparison.Ordinal));gm.CollectedWorldIds.Add(TrackPrefix+id);gm.MarkProgressDirty();Revision++;}
  public void Toggle(){gm=OpenWorldManager.I;if(!gm||!gm.Hero||gm.Hero.Dead)return;if(Open){Close();return;}gm.CloseMenus();gm.ShowPauseModal=true;Open=true;Revision++;}
  public void Close(){if(gm&&Open)gm.CloseMenus();Open=false;Revision++;}
  void LateUpdate(){
   gm=OpenWorldManager.I;if(!gm||!gm.Hero)return;
   if(Open&&!gm.ShowPauseModal){Open=false;Revision++;}
   if(Input.GetKeyDown(KeyCode.L))Toggle();
   if(Time.unscaledTime>=nextObserve){nextObserve=Time.unscaledTime+.15f;string banner=gm.CurrentBanner;
    if(banner!=previousBanner)ObserveBanner(banner);
    previousBanner=banner;
   }
   if(OpenWorldMobileHud.Instance)OpenWorldMobileHud.Instance.UpdateQuestJournal(gm,this);
  }
  public void ObserveBanner(string banner){
   gm=OpenWorldManager.I;if(!gm||string.IsNullOrEmpty(banner))return;
   int colon=banner.IndexOf(':');if(colon<=0||colon>=48)return;
   string speaker=banner.Substring(0,colon);bool known=speaker=="KAEL"||speaker=="FIELD NOTE";
   if(!known)foreach(var n in gm.NPCs)if(n&&speaker.Equals(n.DisplayName,StringComparison.OrdinalIgnoreCase)){known=true;break;}
   if(known)Record(speaker,banner.Substring(colon+1).Trim());previousBanner=banner;
  }
  public void Record(string speaker,string words){
   gm=OpenWorldManager.I;if(!gm||string.IsNullOrWhiteSpace(words))return;
   string text=speaker+": "+words;if(text.Length>500)text=text.Substring(0,500);
   var old=new List<string>();int seq=0;string mostRecent=null;
   foreach(string flag in gm.CollectedWorldIds)if(flag.StartsWith(DialoguePrefix,StringComparison.Ordinal)){old.Add(flag);int start=DialoguePrefix.Length;if(int.TryParse(flag.Substring(start,Math.Min(8,flag.Length-start)),out int n)&&n>=seq){seq=n;mostRecent=flag;}}
   string encoded=Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
   if(mostRecent!=null&&mostRecent.EndsWith(":"+encoded,StringComparison.Ordinal))return;
   old.Sort(StringComparer.Ordinal);while(old.Count>=32){gm.CollectedWorldIds.Remove(old[0]);old.RemoveAt(0);}
   gm.CollectedWorldIds.Add(DialoguePrefix+(seq+1).ToString("D8")+":"+encoded);gm.MarkProgressDirty();Revision++;
  }
  public List<string> Dialogue(){var flags=new List<string>();if(gm)foreach(var f in gm.CollectedWorldIds)if(f.StartsWith(DialoguePrefix,StringComparison.Ordinal))flags.Add(f);flags.Sort(StringComparer.Ordinal);var lines=new List<string>();foreach(var f in flags)try{if(f.Length<=DialoguePrefix.Length+9)continue;lines.Add(Encoding.UTF8.GetString(Convert.FromBase64String(f.Substring(DialoguePrefix.Length+9))));}catch(FormatException){ }return lines;}
  public static readonly string[] Ids={"main","hunt","sled","braziers","lanterns","rods"};
  public static OWJournalQuest Quest(OpenWorldManager gm,string id){
   switch(id){
    case "hunt":return Make(id,"A Hunter's Promise","Verdant • Oakvale",gm.VerdantHuntObjective,gm.VerdantHuntTarget,gm.VerdantHuntStage,5);
    case "sled":return Make(id,"The Lost Supply Sled","Frozen Peaks",gm.SledObjective,gm.SledTarget,gm.SledStage,5);
    case "braziers":return Make(id,"Trial of the Sunspire","Burning Dunes",gm.BrazierObjective,gm.BrazierTarget,gm.BrazierStage,3);
    case "lanterns":return Make(id,"Restless Lanterns","Hollow Marches",gm.LanternObjective,gm.LanternTarget,gm.LanternStage,4);
    case "rods":return Make(id,"The Conductor","Stormveil",gm.RodObjective,gm.RodTarget,gm.RodStage,3);
    default:var q=Make("main",gm.QuestHeading,"Main story • Verdant",gm.QuestDetail,MainTarget(gm),(int)gm.QuestStage,5);q.Main=true;return q;
   }
  }
  static OWJournalQuest Make(string id,string title,string region,string objective,Vector3 target,int stage,int end)=>new OWJournalQuest{Id=id,Title=title,Region=region,Objective=objective,Target=target,Stage=stage,End=end};
  // The legacy QuestTarget automatically favours the hunt. A selected main story must resolve itself.
  static Vector3 MainTarget(OpenWorldManager gm){
   if(gm.QuestStage==OpenWorldManager.AdventureStage.RecoverSigils){Vector3 target=OpenWorldManager.SanctuaryPosition;float best=float.MaxValue;Vector3 p=gm.Hero.transform.position;foreach(var camp in gm.Camps)if(camp&&!camp.Captured){float d=(camp.transform.position-p).sqrMagnitude;if(d<best){best=d;target=camp.transform.position;}}foreach(var item in gm.Collectibles)if(item&&!item.Collected&&item.Kind==OWCollectibleKind.TempleKey){float d=(item.transform.position-p).sqrMagnitude;if(d<best){best=d;target=item.transform.position;}}return target;}
   if(gm.QuestStage==OpenWorldManager.AdventureStage.OpenGrove)return OpenWorldManager.GroveSealPosition;
   if(gm.QuestStage==OpenWorldManager.AdventureStage.DefeatGuardian)return gm.ActiveBoss?gm.ActiveBoss.transform.position:new Vector3(0,0,115);
   return OpenWorldManager.SanctuaryPosition;
  }
  void OnDestroy(){if(Instance==this)Instance=null;}
 }
}



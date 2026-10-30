using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace LostRealmsOW {
 public static class VerdantExperienceValidation {
  const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  const string Report="Docs/Performance/Codex_combat.txt";
  static OpenWorldSaveData snapshot;static Vector3 position;static bool blocked,dirty,focus;static float health;
  static OpenWorldEnemy dummy;
  public static bool Busy{get;private set;}
  static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,Private).SetValue(obj,value);
  public static void Check(bool ok,string text){if(!ok)throw new Exception(text);File.AppendAllText(Report,"PASS: "+text+"\n");}
  public static void Begin(){var gm=OpenWorldManager.I;snapshot=gm.CaptureAdventure();position=gm.Hero.transform.position;health=gm.Hero.Health;focus=OpenWorldHero.DebugAlwaysFocused;OpenWorldHero.DebugAlwaysFocused=true;blocked=(bool)typeof(OpenWorldManager).GetField("saveBlocked",Private).GetValue(gm);dirty=(bool)typeof(OpenWorldManager).GetField("saveDirty",Private).GetValue(gm);Set(gm,"saveBlocked",true);Directory.CreateDirectory("Docs/Performance");}
  public static void Combat(string label){if(!OpenWorldManager.I)throw new Exception("World unavailable; restart Play after compilation");Busy=true;OpenWorldManager.I.StartCoroutine(MeasureCombat(label));}
  static IEnumerator MeasureCombat(string label){
   var gm=OpenWorldManager.I;var h=gm.Hero;Vector3 site=gm.HuntGround(new Vector3(60,0,28));
   try{
    h.WarpTo(site);h.transform.rotation=Quaternion.identity;h.LockOnTarget=null;OpenWorldHero.DebugAction("god");
    dummy=OpenWorldEnemy.Spawn("IronGoblin","Timing sparring foe",site+Vector3.forward*2,gm.WorldRoot);dummy.MaxHealth=dummy.Health=100;gm.Enemies.Add(dummy);
    foreach(int weapon in new[]{0,2,5}){
     h.WarpTo(site);h.transform.rotation=Quaternion.identity;h.EquipWeapon(weapon);Set(h,"comboStep",0);Set(h,"comboResetAt",0f);Set(h,"actionUntil",0f);Set(h,"chainAt",0f);
     dummy.Controller.enabled=false;dummy.transform.position=site+Vector3.forward*2;dummy.Controller.enabled=true;dummy.Stun(15);dummy.Health=100;
     yield return new WaitForSeconds(.1f);
     float began=Time.time;h.PerformLightAttack();float deadline=Time.time+2;
     while(dummy.Health==100&&Time.time<deadline)yield return null;
     float first=Time.time-began;
     File.AppendAllText(Report,$"{label} weapon={weapon} first impact={first:F3}s damage={100-dummy.Health:F2} movement cancel={(float)typeof(OpenWorldHero).GetField("cancelAt",Private).GetValue(h)-began:F3}s chain={(float)typeof(OpenWorldHero).GetField("chainAt",Private).GetValue(h)-began:F3}s\n");
     Check(dummy.Health<100,"scheduled real weapon hit landed ("+weapon+")");
     yield return new WaitForSeconds(.9f);
    }
   }finally{if(dummy){gm.Enemies.Remove(dummy);UnityEngine.Object.Destroy(dummy.gameObject);}dummy=null;Busy=false;}
  }
  public static void Experience(){if(!OpenWorldManager.I)throw new Exception("World unavailable");Busy=true;OpenWorldManager.I.StartCoroutine(VerifyExperience());}
  static IEnumerator VerifyExperience(){
   var gm=OpenWorldManager.I;var h=gm.Hero;var journal=OpenWorldQuestJournal.Instance;
   try{
    OpenWorldHero.DebugAction("god");gm.CollectedWorldIds.RemoveWhere(f=>f.StartsWith("hunt-discovery:")||f.StartsWith("journal:"));
    gm.CollectedWorldIds.Add("verdant-hunt:accepted");
    h.WarpTo(OpenWorldHuntingDiscoveries.NoteAnchor);gm.CameraRig.SnapToTarget();yield return new WaitForSeconds(2);
    var sites=UnityEngine.Object.FindObjectsByType<OpenWorldDiscoverySite>();Check(sites.Length==3,"three deliberate discoveries placed");
    var note=sites.First(s=>s.Kind==0);h.WarpTo(note.transform.position);OpenWorldHuntingDiscoveries.Instance.Interact(note);yield return new WaitForSeconds(.25f);
    Check(gm.CollectedWorldIds.Contains(OpenWorldHuntingDiscoveries.NoteId)&&journal.Dialogue().Any(l=>l.Contains("Thornhide scattered")),"field note read and retained in journal");
    ScreenCapture.CaptureScreenshot("ClaudeBridge/shots/codexpass_note.png");
    var cache=sites.First(s=>s.Kind==1);h.WarpTo(cache.transform.position+Vector3.back);int gold=gm.Gold,gems=gm.Gems;
    OpenWorldHuntingDiscoveries.Instance.Interact(cache);OpenWorldHuntingDiscoveries.Instance.Interact(cache);
    Check(gm.Gold==gold+35&&gm.Gems==gems+2,"hidden cache pays exactly once");
    var camp=sites.First(s=>s.Kind==2);h.WarpTo(camp.transform.position+Vector3.back);OpenWorldHuntingDiscoveries.Instance.Interact(camp);yield return new WaitForSeconds(.3f);
    var raider=gm.Enemies.Find(e=>e&&e.DisplayName=="Briarfoot Supply Thief");Check(raider,"optional camp creates encounter only on investigation");
    gold=gm.Gold;gems=gm.Gems;raider.TakeHit(1000,h.transform.position,true);yield return new WaitForSeconds(.5f);
    Check(gm.CollectedWorldIds.Contains(OpenWorldHuntingDiscoveries.CampId)&&gm.Gold>=gold+40&&gm.Gems>=gems+1,"optional encounter clears and awards reward");
    int enemies=gm.Enemies.Count;OpenWorldHuntingDiscoveries.Instance.Interact(camp);Check(gm.Enemies.Count==enemies,"cleared optional encounter cannot restart");
    var kael=gm.NPCs.Find(n=>n&&n.NpcId=="kael");h.WarpTo(kael.transform.position+Vector3.back*2);kael.Converse(gm);yield return new WaitForSeconds(.25f);
    Check(journal.Dialogue().Any(l=>l.StartsWith("KAEL:")),"actual NPC dialogue captured");
    journal.Track("main");var main=OpenWorldQuestJournal.Quest(gm,journal.TrackedId);journal.Track("hunt");var hunt=OpenWorldQuestJournal.Quest(gm,journal.TrackedId);
    Check(main.Main&&hunt.Id=="hunt"&&(main.Target-hunt.Target).sqrMagnitude>1,"main/side tracking resolves distinct objectives");
    foreach(string id in OpenWorldQuestJournal.Ids)Check(!string.IsNullOrEmpty(OpenWorldQuestJournal.Quest(gm,id).Objective),"journal objective readable: "+id);
    journal.Toggle();yield return new WaitForSecondsRealtime(.3f);Check(journal.Open&&gm.IsModalOpen&&Time.timeScale==0,"journal pauses gameplay and owns modal input");
    Set(OpenWorldMobileHud.Instance,"journalSelected","hunt");typeof(OpenWorldMobileHud).GetMethod("FillJournal",Private).Invoke(OpenWorldMobileHud.Instance,new object[]{gm,journal});
    ScreenCapture.CaptureScreenshot("ClaudeBridge/shots/codexpass_journal_quests.png");yield return new WaitForSecondsRealtime(.3f);
    Set(OpenWorldMobileHud.Instance,"journalDialogue",true);typeof(OpenWorldMobileHud).GetMethod("FillJournal",Private).Invoke(OpenWorldMobileHud.Instance,new object[]{gm,journal});
    ScreenCapture.CaptureScreenshot("ClaudeBridge/shots/codexpass_journal_dialogue.png");yield return new WaitForSecondsRealtime(.3f);
    journal.Close();yield return null;Check(!gm.IsModalOpen&&Time.timeScale>0,"closing journal restores gameplay");
    var store=new OpenWorldSaveStore(Path.GetFullPath("Temp/CodexExperience/Save_"+DateTime.UtcNow.Ticks));store.Write(gm.CaptureAdventure());var saved=store.Load();
    Check(saved.collected.Contains("journal:track:hunt")&&saved.collected.Any(f=>f.StartsWith("journal:line:"))&&saved.collected.Contains(OpenWorldHuntingDiscoveries.CacheId),"disk round-trip preserves tracking, dialogue and discoveries");
    for(int i=0;i<35;i++)journal.Record("QA", "Bounded transcript entry "+i);Check(journal.Dialogue().Count==32,"dialogue history remains bounded at 32 entries");
    VerifyCombatRules();
   }finally{journal.Close();Busy=false;}
  }
  static object Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,Private).Invoke(obj,args);
  static void VerifyCombatRules(){
   var gm=OpenWorldManager.I;var h=gm.Hero;var p=gm.HuntGround(new Vector3(60,0,28));h.WarpTo(p);h.transform.rotation=Quaternion.identity;
   var enemy=OpenWorldEnemy.Spawn("IronGoblin","Cover fixture",p+Vector3.forward*2,gm.WorldRoot);enemy.MaxHealth=enemy.Health=50;gm.Enemies.Add(enemy);
   var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=p+new Vector3(0,1,1);wall.transform.localScale=new Vector3(3,3,.3f);Physics.SyncTransforms();
   try{
    Call(h,"StrikeNearbyEnemies",1f,3f,false,false,-1,0f,0f,false);Check(enemy.Health==50,"solid cover prevents weapon damage");
    wall.SetActive(false);Physics.SyncTransforms();Call(h,"StrikeNearbyEnemies",1f,3f,false,false,-1,0f,0f,false);Check(enemy.Health<50,"unobstructed melee connects");
    Set(enemy,"lightStaggerReadyAt",0f);enemy.TakeHit(.1f,h.transform.position,false);
    var stateField=typeof(OpenWorldEnemy).GetField("state",Private);stateField.SetValue(enemy,Enum.Parse(stateField.FieldType,"Windup"));Set(enemy,"stateTimer",.7f);
    enemy.TakeHit(.1f,h.transform.position,false);Check(enemy.Telegraphing,"successive light hits cannot permanently restart stagger");enemy.TakeHit(.1f,h.transform.position,true);Check(!enemy.Telegraphing,"heavy attack still breaks windup");
    gm.CameraRig.AddShake(99,99);Check((float)typeof(OpenWorldCamera).GetField("shakeIntensity",Private).GetValue(gm.CameraRig)<=.22f,"stacked camera shake is capped");
    Set(h,"actionUntil",Time.time+1);Set(h,"chainAt",Time.time+.75f);h.PerformLightAttack();Check((float)typeof(OpenWorldHero).GetField("bufferedAttackUntil",Private).GetValue(h)>Time.time+.75f,"early attack press survives long windup");
   }finally{gm.Enemies.Remove(enemy);UnityEngine.Object.Destroy(enemy.gameObject);UnityEngine.Object.Destroy(wall);h.WarpTo(p);}
  }
  public static void Spar(){if(!OpenWorldManager.I)throw new Exception("World unavailable");Busy=true;OpenWorldManager.I.StartCoroutine(LiveSpar());}
  static IEnumerator LiveSpar(){
   var gm=OpenWorldManager.I;var h=gm.Hero;var p=gm.HuntGround(new Vector3(60,0,28));h.WarpTo(p);h.Health=h.MaxHealth;Set(h,"invulnerableUntil",0f);h.Aether=h.MaxAether;h.Stamina=h.MaxStamina;
   var e=OpenWorldEnemy.Spawn("IronGoblin","Live sparring opponent",p+Vector3.forward*2.6f,gm.WorldRoot);e.MaxHealth=e.Health=60;e.AttackDamage=.6f;e.LeashRadius=22;gm.Enemies.Add(e);h.LockOnTarget=e;
   int warnings=0,parries=0,dashes=0;bool was=false,responded=false;float warnAt=0,nextAttack=Time.time+.8f,end=Time.time+22,lowest=h.Health;
   try{while(Time.time<end&&!h.Dead&&!e.Dead){
     if(e.Telegraphing&&!was){warnings++;warnAt=Time.time;responded=false;}was=e.Telegraphing;
     if(e.Telegraphing&&!responded&&Time.time-warnAt>.3f){responded=true;if(warnings%3==1){h.PerformParry();parries++;}else if(warnings%3==2){h.PerformDodge(new Vector2(0,-1));dashes++;}}
     if(!e.Telegraphing&&Time.time>nextAttack){h.PerformLightAttack();nextAttack=Time.time+1.15f;}
     lowest=Mathf.Min(lowest,h.Health);yield return null;
    }
    File.AppendAllText(Report,$"LIVE AI SPAR: 22s; warnings={warnings}, parry requests={parries}, dodge requests={dashes}, hero minimum HP={lowest:F2}/{h.MaxHealth:F2}, opponent HP={e.Health:F2}/{e.MaxHealth:F2}. Scripted real controllers, not human difficulty certification.\n");
    Check(warnings>=2&&!h.Dead,"live AI delivers readable repeated warnings while attack/parry/dodge loop remains playable");
    ScreenCapture.CaptureScreenshot("ClaudeBridge/shots/codexpass_live_combat.png");
   }finally{gm.Enemies.Remove(e);UnityEngine.Object.Destroy(e.gameObject);h.LockOnTarget=null;h.Health=h.MaxHealth;Busy=false;}
  }
  public static void Performance(string tier){if(!OpenWorldManager.I)throw new Exception("World unavailable");Busy=true;OpenWorldManager.I.StartCoroutine(Profile(tier));}
  static IEnumerator Profile(string tier){
   var gm=OpenWorldManager.I;var h=gm.Hero;var old=OpenWorldGraphics.Instance.Current;var probe=gm.gameObject.AddComponent<OpenWorldPerformanceProbe>();
   string file="Docs/Performance/Codex_profile_"+tier+".md";
   File.WriteAllText(file,"# Desktop Editor performance — "+tier+" — "+DateTime.Now+"\n\nUnity "+Application.unityVersion+"; "+SystemInfo.processorType+"; "+SystemInfo.graphicsDeviceName+"; "+Screen.width+"×"+Screen.height+". Play mode; frame cap retained. Includes Editor overhead. No Android build, device heat or GPU temperature measurement. Main-thread counter may include waits; NaN means unavailable. Allocations include Editor activity.\n\n| Scenario | Seconds | Frames | Mean ms | P50 | P95 | P99 | Worst | >50 ms | Unity used MiB start→end | Peak used | Peak reserved | Mono MiB start→end | Main thread P95 ms | GC alloc P95 KiB/frame | Gen0 collections |\n|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---|---:|---:|---:|\n");
   string counts="";
   try{
    OpenWorldGraphics.Instance.Apply(tier.StartsWith("high")?OpenWorldGraphics.Tier.High:OpenWorldGraphics.Tier.Balanced,false);OpenWorldHero.DebugAction("god");
    foreach(string scenario in tier.EndsWith("_herd")?new[]{"Wild horse herd nearby"}:new[]{"Oakvale settled","Hunt-route traversal","Village horse nearby"}){
     if(scenario.Contains("horse"))OpenWorldPopulation.DebugVisit("Wild Horse");else h.WarpTo(gm.HuntGround(scenario=="Oakvale settled"?new Vector3(44,0,42):new Vector3(53,0,32)));
     gm.CameraRig.Yaw=90;gm.CameraRig.Pitch=22;gm.CameraRig.SnapToTarget();yield return new WaitForSecondsRealtime(5);
     probe.Begin(scenario,30,file);float nextStrike=Time.time;bool outbound=true;float travel=0;Vector3 last=h.transform.position;
     while(probe.Busy){
      if(scenario=="Hunt-route traversal"){
       // Follow the route with the real touch movement path, rather than teleporting between samples.
       Vector3 target=outbound?new Vector3(108,0,46):new Vector3(53,0,32);Vector3 to=target-h.transform.position;to.y=0;if(to.magnitude<2)outbound=!outbound;
       Vector3 forward=Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up).normalized,right=Vector3.Cross(Vector3.up,forward);
       h.TouchMoveInput=new Vector2(Vector3.Dot(to.normalized,right),Vector3.Dot(to.normalized,forward))*.7f;
       if(Time.time>nextStrike){h.PerformLightAttack();nextStrike=Time.time+2.1f;}
      }travel+=Vector3.Distance(last,h.transform.position);last=h.transform.position;yield return null;
     }
     h.TouchMoveInput=Vector2.zero;
     counts+="\n- "+scenario+": enemies="+gm.Enemies.Count(e=>e&&!e.Dead)+", wildlife="+UnityEngine.Object.FindObjectsByType<OpenWorldWildlife>().Length+", NPCs="+gm.NPCs.Count(n=>n)+", travelled="+travel.ToString("F1")+" m, end="+h.transform.position+".";
    }
    File.AppendAllText(file,"\n"+counts+"\n");
   }finally{h.TouchMoveInput=Vector2.zero;UnityEngine.Object.Destroy(probe);OpenWorldGraphics.Instance.Apply(old,false);Busy=false;}
  }
  public static void AudioAndTargets(){
   var gm=OpenWorldManager.I;var audio=OpenWorldAudio.Instance;var voices=(AudioSource[])typeof(OpenWorldAudio).GetField("voices",Private).GetValue(audio);
   Check(voices.Length==8,"audio source budget remains eight voices");
   if(audio.VolumeTier!=2){
    foreach(var voice in voices)voice.Stop();for(int i=0;i<8;i++)OpenWorldAudio.Play("sfx_upgrade",.02f,1,0);
    Set(audio,"lastImpact",-10f);OpenWorldAudio.PlayCombatImpact(true);
    var clip=OpenWorldImportedContent.Sound("sfx_impact")??Resources.Load<AudioClip>("Audio/sfx_impact");
    Check(clip&&voices.Any(v=>v.isPlaying&&v.clip==clip),"hit impact can replace a saturated low-priority sound pool");
    voices[0].Stop();OpenWorldAudio.Play("sfx_upgrade",.02f,1,0);Check(Mathf.Abs(voices[0].pitch-1)<.001f,"normal sound playback resets pitch");
   }
   var journal=OpenWorldQuestJournal.Instance;journal.Track("main");var q=OpenWorldQuestJournal.Quest(gm,"main");Check(q.Main,"main tracking stays explicit with an active hunt");
   gm.ShowMapModal=true;var hud=OpenWorldMobileHud.Instance;hud.SendMessage("Update");typeof(OpenWorldMobileHud).GetMethod("UpdateQuestJournal",Private).Invoke(hud,new object[]{gm,journal});
   var pin=(RectTransform)typeof(OpenWorldMobileHud).GetField("targetPin",Private).GetValue(hud);var expected=(Vector2)typeof(OpenWorldMobileHud).GetMethod("WorldToMapCanvas",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{q.Target});Check(pin&&(pin.anchoredPosition-expected).sqrMagnitude<.01f,"atlas marker follows selected main objective instead of active hunt");gm.CloseMenus();
   File.AppendAllText(Report,"CHECK: atlas override executed for explicitly selected main story. No auditory quality/listening claim.\n");
  }
  public static void ViewDiscovery(string kind){
   var gm=OpenWorldManager.I;var site=UnityEngine.Object.FindObjectsByType<OpenWorldDiscoverySite>().First(s=>s.Kind==int.Parse(kind));
   if(site.Kind==1&&site.CacheVisual){gm.CollectedWorldIds.Remove(OpenWorldHuntingDiscoveries.CacheId);site.Refresh(gm);}
   gm.Hero.WarpTo(site.transform.position+Vector3.back*3);gm.CameraRig.enabled=false;var cam=Camera.main;var focus=site.transform.position+Vector3.up*.6f;cam.transform.position=focus+new Vector3(2.4f,1.6f,3.3f);cam.transform.LookAt(focus);cam.fieldOfView=48;
   File.AppendAllText(Report,"Discovery "+site.Kind+" actual="+site.transform.position+" grass="+OpenWorldWildlife.GrassAt(gm.ActiveTerrain,site.transform.position)+"\n");
  }
  public static void Cleanup(){if(snapshot==null)return;var gm=OpenWorldManager.I;gm.RestoreAdventure(snapshot);gm.Hero.WarpTo(position);gm.Hero.Health=health;gm.CameraRig.enabled=true;gm.CameraRig.SnapToTarget();Set(gm.Hero,"invulnerableUntil",0f);Set(gm,"saveBlocked",blocked);Set(gm,"saveDirty",dirty);OpenWorldHero.DebugAlwaysFocused=focus;snapshot=null;}
 }
}







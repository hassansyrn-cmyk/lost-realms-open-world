using UnityEngine;
using UnityEngine.UI;

namespace LostRealmsOW {
 // GLM Phase 2 quest hub: a physical quest board in the Heart of the Realms that opens a modal listing
 // every side quest (Codex's A Hunter's Promise and the four GLM region quests) with live status, plus
 // the tiny static coordinator the one-line manager/adventure/NPC hooks fan out through. The board UI
 // follows the established partial-of-OpenWorldMobileHud pattern (as OpenWorldClock.cs does) so no
 // existing HUD file is edited.
 public static class GlmQuests {
  // Read-only quest snapshot for Codex's journal (GLM brief "Update 22:58"): one entry per side quest
  // with id, title, region, stage bounds, status, current objective text and its world position.
  // Includes Codex's A Hunter's Promise read-only via its public state.
  public struct GlmQuestState {
   public string Id, Title, Region, Status, Objective;
   public int Stage, CompleteStage;
   public Vector3 ObjectivePos;
  }
  public static System.Collections.Generic.List<GlmQuestState> Snapshot(OpenWorldManager gm) {
   var list = new System.Collections.Generic.List<GlmQuestState>(5);
   list.Add(Make("glm-sled", "The Lost Supply Sled", "Frozen Peaks", 5, gm.SledStage, gm.SledObjective, gm.SledTarget));
   list.Add(Make("glm-brazier", "Trial of the Sunspire", "Burning Dunes", 3, gm.BrazierStage, gm.BrazierObjective, gm.BrazierTarget));
   list.Add(Make("glm-lantern", "Restless Lanterns", "Hollow Marches", 4, gm.LanternStage, gm.LanternObjective, gm.LanternTarget));
   list.Add(Make("glm-rod", "The Conductor", "Stormveil", 3, gm.RodStage, gm.RodObjective, gm.RodTarget));
   list.Add(Make("verdant-hunt", "A Hunter's Promise", "Verdant (Codex)", 5, gm.VerdantHuntStage, gm.VerdantHuntObjective, gm.VerdantHuntTarget));
   return list;
  }
  static GlmQuestState Make(string id, string title, string region, int complete, int stage, string objective, Vector3 pos) {
   return new GlmQuestState {
    Id = id, Title = title, Region = region, Stage = stage, CompleteStage = complete,
    Status = stage <= 0 ? "not-started" : stage >= complete ? "complete" : "active",
    Objective = stage <= 0 ? "Not started — find the quest giver" : objective,
    ObjectivePos = pos
   };
  }
  public static void Tick(OpenWorldManager gm) {
   gm.TickSled(); gm.TickBraziers(); gm.TickLanterns(); gm.TickRods();
   GlmMerchant.Tick(gm); // trader stall + shop modal tick (spawns once, then modal visibility only)
   OpenWorldWeaponShop.Tick(gm); // royal forge workshop + weapon shop modal tick
   GlmGathering.Tick(gm); // gathering nodes (spawn once; nodes self-update afterwards)
   GlmEliteHunts.Tick(gm); // wanted poster refresh + slain-elite despawn watcher
   if (!gm.GetComponent<GlmQuestBoardRuntime>()) gm.gameObject.AddComponent<GlmQuestBoardRuntime>();
  }
  public static bool Converse(OpenWorldManager gm, string npcId) {
   Debug.Log("[GlmQuest] converse dispatch: " + npcId + " stage(sled " + gm.SledStage + ", brazier " + gm.BrazierStage + ", lantern " + gm.LanternStage + ", rod " + gm.RodStage + ")");
   switch (npcId) {
    case "glm-q-sled": return gm.ConverseSled();
    case "glm-q-brazier": return gm.ConverseBrazier();
    case "glm-q-lantern": return gm.ConverseLantern();
    case "glm-q-rod": return gm.ConverseRod();
    default: return false;
   }
  }
  public static bool Interact(OpenWorldManager gm) {
   if (OpenWorldMobileHud.Instance && OpenWorldMobileHud.Instance.TryDiscover(gm)) return true;
   if (gm.InteractSledTracks() || gm.InteractSledSupplies() || gm.InteractLantern() || gm.InteractRod()) return true;
   return TryOpenBoard(gm);
  }
  public static void Defeat(OpenWorldManager gm, OpenWorldEnemy enemy) {
   gm.DefeatSledWolf(enemy);
   if (enemy) GlmEliteHunts.OnDefeat(gm, enemy.DisplayName); // region elite bounties
  }
  // Bridge helper (Phase 5): opens the credits modal for the visual check.
  public static void DebugCredits(string arg) {
   var gm = OpenWorldManager.I;
   if (!gm) return;
   gm.CloseMenus();
   gm.ShowCreditsModal = true;
   Debug.Log("[GlmCredits] ShowCreditsModal set via GlmQuests.DebugCredits");
  }
  // Bridge helper: closes every modal.
  public static void DebugClose(string arg) { if (OpenWorldManager.I) OpenWorldManager.I.CloseMenus(); }
  static bool TryOpenBoard(OpenWorldManager gm) {
   var board = Object.FindAnyObjectByType<GlmQuestBoardRuntime>();
   if (!board || !gm.Hero || Vector2.Distance(new Vector2(gm.Hero.transform.position.x, gm.Hero.transform.position.z), new Vector2(board.BoardPos.x, board.BoardPos.z)) > 4f) return false;
   gm.ShowQuestBoardModal = true;
   OpenWorldAudio.Play("sfx_upgrade", .5f);
   return true;
  }
 }
 public sealed partial class OpenWorldManager {
  public bool ShowQuestBoardModal;
  public bool IsBoardOpen => ShowQuestBoardModal;
  // One-line hooks called from Update / OnEnemyDefeated (see DEVELOPMENT_LOG claim 22:45).
  public void TickGlmQuests() { GlmQuests.Tick(this); }
  public void RecordGlmDefeat(OpenWorldEnemy enemy) { GlmQuests.Defeat(this, enemy); }
  public bool GlmQuestConverse(string npcId) { return GlmQuests.Converse(this, npcId); }
  public bool GlmQuestInteract() { return GlmQuests.Interact(this); }
 }
 public sealed class GlmQuestBoardRuntime : MonoBehaviour {
  OpenWorldManager gm;
  GameObject board;
  public Vector3 BoardPos => board ? board.transform.position : transform.position;
  void Awake() { gm = GetComponent<OpenWorldManager>(); }
  void Update() {
   if (!gm || !gm.Hero || !gm.ActiveTerrain) return;
   if (!board) {
    Vector3 anchor = new Vector3(-5.5f, 0f, 29f);
    gm.ReserveSpot(anchor, 1.8f);
    board = new GameObject("Hub quest board");board.transform.SetParent(gm.WorldRoot,false);board.transform.position=gm.HuntGround(anchor);
    OpenWorldNoticeBoard.Build(board.transform,"THE WARDENS' LEDGER","Work for willing hands\n\nOakvale • Frozen Peaks\nSunspire • Hollow Marches\nStormveil\n\nSpeak to a warden for details");
   }
   var show = Vector3.Distance(gm.Hero.transform.position, board.transform.position) < 14f;
   var promptTransform = board.transform.Find("Board prompt");
   if (promptTransform) promptTransform.gameObject.SetActive(show);
   if (show && Camera.main && promptTransform) promptTransform.rotation = Camera.main.transform.rotation;
   if (OpenWorldMobileHud.Instance) OpenWorldMobileHud.Instance.TickQuestBoard(gm);
  }
  void OnDestroy() { if (board) Destroy(board); }
 }
 public sealed partial class OpenWorldMobileHud {
  GameObject questBoardRoot;
  RectTransform questBoardRows;
  ScrollRect questBoardScroll;
  Text questBoardTitle;
  // The floating questText line for an active GLM quest (same slot Codex's hunt panel uses; the main
  // adventure questText returns when no side quest is active). Only the furthest-along GLM quest shows.
  internal void UpdateGlmQuestPanel(OpenWorldManager gm, string quest) {
   if (!objectiveTitle || gm.IsModalOpen || gm.IsBoardOpen || (titleRoot && titleRoot.activeSelf)) return;
   int best = 0; string questText = null; Vector3 target = Vector3.zero;
   Consider(gm.SledStage, gm.SledObjective, gm.SledTarget, ref best, ref questText, ref target);
   Consider(gm.BrazierStage, gm.BrazierObjective, gm.BrazierTarget, ref best, ref questText, ref target);
   Consider(gm.LanternStage, gm.LanternObjective, gm.LanternTarget, ref best, ref questText, ref target);
   Consider(gm.RodStage, gm.RodObjective, gm.RodTarget, ref best, ref questText, ref target);
   if (best <= 0 || questText == null) return;
   objectiveTitle.text = "SIDE QUEST";
   objective.text = questText;
   objectiveDist.text = gm.SaveUnavailable ? "Save unavailable • check storage" : Mathf.RoundToInt(Vector3.Distance(gm.Hero.transform.position, target)) + " m";
  }
  static void Consider(int stage, string text, Vector3 target, ref int best, ref string questText, ref Vector3 bestTarget) {
   if (stage > 0 && stage < 5 && stage > best) { best = stage; questText = text; bestTarget = target; }
  }
  internal void TickQuestBoard(OpenWorldManager gm) {
   if (questBoardRoot && questBoardRoot.activeSelf != gm.ShowQuestBoardModal) {
    questBoardRoot.SetActive(gm.ShowQuestBoardModal);
    if (gm.ShowQuestBoardModal) FillQuestBoard(gm);
   } else if (!questBoardRoot && gm.ShowQuestBoardModal) {
    BuildQuestBoard();
    questBoardRoot.SetActive(true);
    FillQuestBoard(gm);
   }
  }
  void BuildQuestBoard() {
   var root = Rect("Quest Board", safeRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   root.offsetMin = root.offsetMax = Vector2.zero;
   questBoardRoot = root.gameObject;
   root.gameObject.AddComponent<Image>().color = new Color(0f, 0.015f, 0.04f, 0.84f);
   var frame = Rect("Quest Board Frame", root, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(760, 640));
   theme.Round(frame.gameObject, new Color(0.05f, 0.075f, 0.13f, 1f), 22f, true);
   questBoardTitle = Label(frame, "QUEST BOARD", new Vector2(0, -44), new Vector2(700, 52), new Vector2(.5f, 1f), 30);
   questBoardTitle.font = theme.Heading ? theme.Heading : questBoardTitle.font;
   questBoardTitle.color = OpenWorldUITheme.Gold;
   var close = Rect("Quest Board Close", frame, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34, -34), new Vector2(46, 46));
   theme.Round(close.gameObject, new Color(0.1f, 0.14f, 0.22f, 0.95f), 23f, true);
   var closeLabel = Label(close, "X", Vector2.zero, new Vector2(46, 46), new Vector2(.5f, .5f), 20);
   closeLabel.color = OpenWorldUITheme.TextMain;
   var closeButton = close.gameObject.AddComponent<Button>();
   closeButton.transition = Selectable.Transition.ColorTint;
   closeButton.onClick.AddListener(() => { if (OpenWorldManager.I) OpenWorldManager.I.ShowQuestBoardModal = false; });
   var viewport = Rect("Quest Board Viewport", frame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
   viewport.offsetMin=new Vector2(26,30);viewport.offsetMax=new Vector2(-26,-95);
   viewport.gameObject.AddComponent<RectMask2D>();viewport.gameObject.AddComponent<Image>().color=new Color(0,0,0,.01f);
   questBoardRows=Rect("Quest Board Rows",viewport,new Vector2(0,1),Vector2.one,Vector2.zero,new Vector2(0,660));questBoardRows.pivot=new Vector2(.5f,1);
   Label(frame,"5 contracts • Swipe or scroll to browse",new Vector2(0,14),new Vector2(650,22),new Vector2(.5f,0),14).color=OpenWorldUITheme.TextDim;
   questBoardScroll=viewport.gameObject.AddComponent<ScrollRect>();questBoardScroll.viewport=viewport;questBoardScroll.content=questBoardRows;questBoardScroll.horizontal=false;questBoardScroll.movementType=ScrollRect.MovementType.Clamped;questBoardScroll.scrollSensitivity=34;
  }
  void FillQuestBoard(OpenWorldManager gm) {
   if (!questBoardRows) return;
   for (int i = questBoardRows.childCount - 1; i >= 0; i--) Destroy(questBoardRows.GetChild(i).gameObject);
   questBoardTitle.text = "QUEST BOARD";
   var quests = new (string title, string status, string reward)[]
   {
    ("A HUNTER'S PROMISE — Oakvale", HuntStatus(gm), "120 gold • 5 gems • Ranger Axe"),
    ("THE LOST SUPPLY SLED — Frozen Peaks", GlmStatus(gm.SledStage, 5, gm.SledObjective), "110 gold • 4 gems • Ranger Axe"),
    ("TRIAL OF THE SUNSPIRE — Burning Dunes", GlmStatus(gm.BrazierStage, 3, gm.BrazierObjective), "120 gold • 6 gems"),
    ("RESTLESS LANTERNS — Hollow Marches", GlmStatus(gm.LanternStage, 4, gm.LanternObjective), "140 gold • 5 gems • night only"),
    ("THE CONDUCTOR — Stormveil", GlmStatus(gm.RodStage, 3, gm.RodObjective), "120 gold • 5 gems • Reaper's Spear"),
   };
   float y = 0f;
   foreach (var q in quests) {
    var row = Rect("Row " + q.title, questBoardRows, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0, -y - 62f), new Vector2(694, 120));
    theme.Round(row.gameObject, new Color(0.09f, 0.12f, 0.2f, 0.92f), 14f);
    var title = Label(row, q.title, new Vector2(18, -12), new Vector2(650, 30), new Vector2(0, 1f), 21);
    title.color = OpenWorldUITheme.Gold; title.alignment = TextAnchor.UpperLeft;
    var status = Label(row, q.status, new Vector2(18, -47), new Vector2(650, 42), new Vector2(0, 1f), 19);
    status.color = OpenWorldUITheme.TextDim; status.alignment = TextAnchor.UpperLeft;
    var reward = Label(row, "Reward: " + q.reward, new Vector2(18, -94), new Vector2(650, 22), new Vector2(0, 1f), 16);
    reward.color = OpenWorldUITheme.TextMuted; reward.alignment = TextAnchor.UpperLeft;
    foreach(var label in new[]{title,status,reward}) {label.rectTransform.pivot=new Vector2(0,1);label.resizeTextForBestFit=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;label.raycastTarget=false;}
    y += 132f;
   }
   questBoardRows.sizeDelta=new Vector2(0,y);questBoardScroll.verticalNormalizedPosition=1f;
  }
  static string HuntStatus(OpenWorldManager gm) {
   if (gm.VerdantHuntStage <= 0) return "Not started — speak to Warden Kael in Oakvale";
   if (gm.VerdantHuntStage >= 5) return "COMPLETE — Oakvale's trail is safe again";
   return "Active — " + gm.VerdantHuntObjective;
  }
  static string GlmStatus(int stage, int complete, string questText) {
   if (stage <= 0) return "Not started — find the quest giver";
   if (stage >= complete) return "COMPLETE";
   return "Active — " + questText;
  }
 }
}

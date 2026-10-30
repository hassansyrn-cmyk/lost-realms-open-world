using System.Text;
using UnityEngine;
using UnityEngine.UI;
namespace LostRealmsOW {
 public sealed partial class OpenWorldMobileHud {
  Button journalButton,journalTrackButton;GameObject journalRoot;RectTransform journalChoices;
  Text journalTitle,journalBody,journalTrackLabel,journalInstruction;ScrollRect journalScroll;
  string journalSelected="main";bool journalDialogue;int journalShownRevision=-1;float nextJournalTracker;
  OWJournalQuest cachedTrackedQuest;string cachedTrackId;float nextTrackDistance;string cachedTrackDistance;
  internal void UpdateQuestJournal(OpenWorldManager gm,OpenWorldQuestJournal journal){
   if(!safeRoot||theme==null)return;
   if(!journalButton){journalButton=PillButton(safeRoot,"JOURNAL  [L]",new Vector2(-85,-168),new Vector2(146,42),new Vector2(1,1),15);journalButton.onClick.AddListener(journal.Toggle);}
   journalButton.gameObject.SetActive(!gm.IsModalOpen&&(!titleRoot||!titleRoot.activeSelf)&&!gm.Hero.Dead);
   if(journal.Open){
    if(!journalRoot)BuildJournal(journal);
    if(menuRoot)menuRoot.SetActive(false); // Reuse the established pause/input gate, with the journal as its view.
    journalRoot.SetActive(true);journalRoot.transform.SetAsLastSibling();
    if(journalShownRevision!=journal.Revision){journalShownRevision=journal.Revision;FillJournal(gm,journal);}
   }else if(journalRoot)journalRoot.SetActive(false);
   if(Time.unscaledTime>=nextJournalTracker||cachedTrackId!=journal.TrackedId){nextJournalTracker=Time.unscaledTime+.15f;cachedTrackId=journal.TrackedId;cachedTrackedQuest=OpenWorldQuestJournal.Quest(gm,cachedTrackId);}
   var q=cachedTrackedQuest;
   if(targetPin&&gm.ShowMapModal)targetPin.anchoredPosition=WorldToMapCanvas(q.Target);
   if(gm.IsModalOpen||(titleRoot&&titleRoot.activeSelf))return;
   if(objectiveTitle)objectiveTitle.text=gm.SaveUnavailable?"SAVE UNAVAILABLE":q.Title.ToUpperInvariant();
   if(objective)objective.text=gm.SaveUnavailable?"Progress is not being saved • check storage":q.Objective;
   Vector3 delta=q.Target-gm.Hero.transform.position;delta.y=0;
   if(Time.unscaledTime>=nextTrackDistance){nextTrackDistance=Time.unscaledTime+.2f;cachedTrackDistance=q.Complete?"COMPLETE":Mathf.RoundToInt(delta.magnitude)+" m • JOURNAL [L]";}
   if(objectiveDist)objectiveDist.text=cachedTrackDistance;
   if(minimapGoalDot){minimapGoalDot.gameObject.SetActive(!q.Complete);minimapGoalDot.anchoredPosition=minimapRaw?LocalBlip(q.Target-gm.Hero.transform.position,true):WorldToMapCanvas(q.Target)*.45f;}
   if(targetPin&&gm.ShowMapModal)targetPin.anchoredPosition=WorldToMapCanvas(q.Target);
  }
  void BuildJournal(OpenWorldQuestJournal journal){
   var root=Rect("Quest journal",safeRoot,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);root.offsetMin=root.offsetMax=Vector2.zero;journalRoot=root.gameObject;
   root.gameObject.AddComponent<Image>().color=new Color(.01f,.025f,.05f,.94f);
   var frame=Rect("Journal frame",root,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(1100,650));theme.Round(frame.gameObject,new Color(.025f,.055f,.105f,.98f),16);
   var title=JText(frame,"Heading","QUEST JOURNAL",new Vector2(-270,276),new Vector2(490,42),30);title.font=theme.Heading?theme.Heading:font;title.color=gold;
   var close=PillButton(frame,"CLOSE",new Vector2(455,276),new Vector2(135,42),Vector2.one*.5f,16);close.onClick.AddListener(journal.Close);
   var questTab=PillButton(frame,"QUESTS",new Vector2(-425,213),new Vector2(180,42),Vector2.one*.5f,17);
   questTab.onClick.AddListener(()=>{journalDialogue=false;FillJournal(OpenWorldManager.I,journal);});
   var dialogueTab=PillButton(frame,"DIALOGUE",new Vector2(-225,213),new Vector2(180,42),Vector2.one*.5f,17);
   dialogueTab.onClick.AddListener(()=>{journalDialogue=true;FillJournal(OpenWorldManager.I,journal);});
   journalInstruction=JText(frame,"Instruction","Choose a quest to follow. Conversations are kept here.",new Vector2(225,214),new Vector2(540,40),17);journalInstruction.color=OpenWorldUITheme.TextDim;
   journalChoices=Rect("Quest list",frame,Vector2.one*.5f,Vector2.one*.5f,new Vector2(-350,-40),new Vector2(330,440));
   journalTitle=JText(frame,"Quest title","",new Vector2(175,130),new Vector2(620,72),27);journalTitle.font=theme.Heading?theme.Heading:font;journalTitle.color=gold;
   var viewport=Rect("Journal viewport",frame,Vector2.one*.5f,Vector2.one*.5f,new Vector2(175,-65),new Vector2(620,300));viewport.gameObject.AddComponent<Image>().color=new Color(0,0,0,.08f);viewport.gameObject.AddComponent<RectMask2D>();
   var content=Rect("Journal text",viewport,new Vector2(0,1),new Vector2(1,1),Vector2.zero,new Vector2(0,300));content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;
   journalBody=content.gameObject.AddComponent<Text>();journalBody.font=theme.Body?theme.Body:font;journalBody.fontSize=22;journalBody.color=OpenWorldUITheme.TextMain;journalBody.alignment=TextAnchor.UpperLeft;journalBody.supportRichText=false;journalBody.horizontalOverflow=HorizontalWrapMode.Wrap;journalBody.verticalOverflow=VerticalWrapMode.Overflow;
   journalScroll=viewport.gameObject.AddComponent<ScrollRect>();journalScroll.viewport=viewport;journalScroll.content=content;journalScroll.horizontal=false;journalScroll.movementType=ScrollRect.MovementType.Clamped;
   journalTrackButton=PillButton(frame,"TRACK QUEST",new Vector2(175,-274),new Vector2(280,48),Vector2.one*.5f,18);journalTrackLabel=journalTrackButton.GetComponentInChildren<Text>();
   journalTrackButton.onClick.AddListener(()=>{journal.Track(journalSelected);FillJournal(OpenWorldManager.I,journal);});
   JText(frame,"Footer","Your journal is saved with this adventure.",new Vector2(-315,-287),new Vector2(410,32),16).color=OpenWorldUITheme.TextDim;
  }
  Text JText(Transform p,string name,string text,Vector2 pos,Vector2 size,int fontSize){var t=Label(p,text,pos,size,Vector2.one*.5f,fontSize);t.name=name;t.font=theme.Body?theme.Body:font;t.alignment=TextAnchor.MiddleLeft;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.supportRichText=false;return t;}
  void FillJournal(OpenWorldManager gm,OpenWorldQuestJournal journal){
   if(!journalChoices||!gm)return;
   foreach(Transform t in journalChoices){t.gameObject.SetActive(false);Destroy(t.gameObject);}
   journalScroll.viewport.anchoredPosition=new Vector2(journalDialogue?0:175,-65);journalScroll.viewport.sizeDelta=new Vector2(journalDialogue?1000:620,300);
   journalTitle.rectTransform.anchoredPosition=new Vector2(journalDialogue?0:175,130);journalTitle.rectTransform.sizeDelta=new Vector2(journalDialogue?1000:620,72);
   journalInstruction.text=journalDialogue?"Newest first • scroll for earlier conversations":"Choose a quest to follow. Conversations are kept here.";
   if(journalDialogue){
    journalTitle.text="CONVERSATIONS & FIELD NOTES";var lines=journal.Dialogue();var text=new StringBuilder();for(int i=lines.Count-1;i>=0;i--)text.Append(lines[i]).Append("\n\n");
    journalBody.text=lines.Count==0?"Speak with people in the world. Their words and the field notes you discover will appear here.":text.ToString();journalTrackButton.gameObject.SetActive(false);
   }else{
    for(int i=0;i<OpenWorldQuestJournal.Ids.Length;i++){
     string id=OpenWorldQuestJournal.Ids[i];var q=OpenWorldQuestJournal.Quest(gm,id);
     var row=PillButton(journalChoices,q.Title,new Vector2(0,183-i*72),new Vector2(320,64),Vector2.one*.5f,17);
     var label=row.GetComponentInChildren<Text>();label.text=(journal.TrackedId==id?"◆ ":"")+q.Title+"\n"+q.Status;label.supportRichText=false;
     row.onClick.AddListener(()=>{journalSelected=id;FillJournal(gm,journal);});
    }
    var selected=OpenWorldQuestJournal.Quest(gm,journalSelected);journalTitle.text=selected.Title.ToUpperInvariant();
    journalBody.text=selected.Region+"\n"+selected.Status+"\n\n"+selected.Objective+"\n\n"+(selected.Complete?"You can revisit this quest's conversations in Dialogue.":selected.Stage==0&&!selected.Main?"Follow the marker to meet the quest giver. Accept the quest through conversation.":"The map marker and on-screen objective follow your tracked quest.");
    journalTrackButton.gameObject.SetActive(true);journalTrackButton.interactable=journal.TrackedId!=journalSelected;
    if(journalTrackLabel)journalTrackLabel.text=journal.TrackedId==journalSelected?"TRACKED":"TRACK QUEST";
   }
   Canvas.ForceUpdateCanvases();journalScroll.content.sizeDelta=new Vector2(0,Mathf.Max(300,journalBody.preferredHeight+20));journalScroll.verticalNormalizedPosition=1;
  }
 }
}



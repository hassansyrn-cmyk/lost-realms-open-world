using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostRealmsOW {
 // A lightweight front end: the expensive realm and gameplay do not exist until a choice is made.
 public sealed class OpenWorldTitleScreen : MonoBehaviour {
  public static bool IsOpen { get; private set; }
  GameObject canvasObject, home, dialog, settings;
  RectTransform safe, artwork;
  CanvasGroup fade;
  Text message, quality, volume;
  Button continueButton, newButton;
  AudioSource music;
  Font heading, body;
  bool hasSave, busy, saveError;
  readonly RectTransform[] motes = new RectTransform[28];
  readonly Color gold = new Color(.94f,.81f,.53f);
  string SaveDirectory => Path.Combine(Application.persistentDataPath, "Adventure");
  public bool HasJourney => hasSave;
  public bool Busy => busy;
  void Awake() {
   IsOpen = true;
   heading = Resources.Load<Font>("UI/Fonts/Cinzel-Bold");
   body = Resources.Load<Font>("UI/Fonts/CrimsonText-Regular");
   if (!EventSystem.current) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
   canvasObject = new GameObject("Lost Realms • Title", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
   var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
   var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
   scaler.referenceResolution = new Vector2(1280,720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
   fade = canvasObject.AddComponent<CanvasGroup>();
   artwork = Rect("Floating realms", canvasObject.transform, Vector2.zero, Vector2.zero); Stretch(artwork);
   var art = artwork.gameObject.AddComponent<RawImage>(); art.texture = Resources.Load<Texture2D>("UI/Backgrounds/BG_MainMenu_Fullscreen_Landscape_Primary"); art.raycastTarget = false;
   var aspect = artwork.gameObject.AddComponent<AspectRatioFitter>(); aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; aspect.aspectRatio = 16f/9f;
   var shade = Panel("Twilight veil", canvasObject.transform, new Color(.015f,.035f,.07f,.32f)); Stretch(shade);
   // Gradient darkens only the menu side, keeping the distant islands luminous.
   var gradient = new Texture2D(64,1,TextureFormat.RGBA32,false); gradient.name = "Title gradient"; gradient.wrapMode = TextureWrapMode.Clamp;
   for(int x=0;x<64;x++) gradient.SetPixel(x,0,new Color(.015f,.035f,.07f,Mathf.SmoothStep(0,.92f,x/63f)));
   gradient.Apply(); var veil = Rect("Menu atmosphere",canvasObject.transform,Vector2.zero,Vector2.zero); Stretch(veil);
   var vi=veil.gameObject.AddComponent<RawImage>();vi.texture=gradient;vi.raycastTarget=false;
   for(int i=0;i<motes.Length;i++) {
    motes[i]=Panel("Drifting light",canvasObject.transform,new Color(1,.89f,.6f,.18f+(i%4)*.09f));
    motes[i].sizeDelta=Vector2.one*(i%3+2);motes[i].localRotation=Quaternion.Euler(0,0,45);
   }
   safe=Rect("Safe area",canvasObject.transform,Vector2.zero,Vector2.zero); Stretch(safe);
   home=Rect("Home",safe,new Vector2(340,0),new Vector2(480,620)).gameObject;
   Label(home.transform,"L O S T   R E A L M S",new Vector2(0,200),new Vector2(460,70),38,true);
   Label(home.transform,"O P E N   W O R L D",new Vector2(0,144),new Vector2(440,35),18,true);
   var line=Panel("Gold rule",home.transform,gold);line.sizeDelta=new Vector2(240,1);line.anchoredPosition=new Vector2(0,112);
   Label(home.transform,"Beyond the horizon, your story awaits.",new Vector2(0,77),new Vector2(440,42),23,false);
   continueButton=Button(home.transform,"CONTINUE JOURNEY",new Vector2(0,3),()=>Begin(false));
   newButton=Button(home.transform,"NEW JOURNEY",new Vector2(0,-67),RequestNew);
   Button(home.transform,"SETTINGS",new Vector2(0,-137),()=>{home.SetActive(false);settings.SetActive(true);});
   if (!Application.isMobilePlatform) Button(home.transform,"QUIT GAME",new Vector2(0,-207),Application.Quit);
   message=Label(safe,"",new Vector2(0,-305),new Vector2(1050,62),21,false);
   var version=Label(safe,"LOST REALMS  /  "+Application.version,new Vector2(-20,20),new Vector2(300,25),13,true);
   var vr=version.rectTransform;vr.anchorMin=vr.anchorMax=Vector2.zero;vr.pivot=Vector2.zero;vr.anchoredPosition=new Vector2(24,16);version.alignment=TextAnchor.MiddleLeft;
   dialog=Modal("Begin a new journey?","Your current adventure will be backed up.\nBegin anew with no weapons or tamed horses.");
   Button(dialog.transform,"BEGIN NEW JOURNEY",new Vector2(0,-65),()=>Begin(true));
   Button(dialog.transform,"GO BACK",new Vector2(0,-140),Back);
   settings=Modal("SETTINGS","Choose the experience that suits your device.");
   quality=Button(settings.transform,"",new Vector2(0,-25),()=>{OpenWorldGraphics.Instance.Cycle();RefreshSettings();}).GetComponentInChildren<Text>();
   volume=Button(settings.transform,"",new Vector2(0,-95),()=>{OpenWorldAudio.Instance.CycleVolume();RefreshSettings();}).GetComponentInChildren<Text>();
   Button(settings.transform,"BACK",new Vector2(0,-170),Back);RefreshSettings();
   InspectSave();
   music=gameObject.AddComponent<AudioSource>(); music.clip=Resources.Load<AudioClip>("Audio/verdant_realm_theme"); music.loop=true;music.volume=.26f;music.Play();
  }
  void InspectSave() {
   try {hasSave=new OpenWorldSaveStore(SaveDirectory).Load()!=null;message.text=hasSave?"Your adventure is waiting.":"A new legend begins with you.";}
   catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is NotSupportedException) {
    saveError=true;message.text="Your save could not be opened. Existing files are preserved.\n"+e.Message;
   }
   continueButton.gameObject.SetActive(hasSave);newButton.interactable=!saveError;
  }
  void RefreshSettings() {quality.text="GRAPHICS  ·  "+OpenWorldGraphics.Instance.Label;volume.text="SOUND  ·  "+OpenWorldAudio.Instance.VolumeLabel;}
  public void RequestNew() {if(busy||saveError)return;if(hasSave){home.SetActive(false);dialog.SetActive(true);}else Begin(true);}
  public void Back() {if(busy)return;dialog.SetActive(false);settings.SetActive(false);home.SetActive(true);}
  public void Begin(bool fresh) {if(busy||saveError||(!fresh&&!hasSave))return;busy=true;StartCoroutine(Enter(fresh));}
  IEnumerator Enter(bool fresh) {
   home.SetActive(false);dialog.SetActive(false);settings.SetActive(false);message.text="Opening the way to Verdant…";
   yield return null; yield return new WaitForEndOfFrame();
   if(fresh) {
    try { PrepareNewJourney(SaveDirectory); }
    catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is NotSupportedException) {
     message.text="Could not begin. Your previous adventure is preserved.\n"+e.Message;busy=false;Back();yield break;
    }
   }
   var gm=GetComponent<OpenWorldManager>();
   gm.CloseMenus();gm.BuildOpenWorld();
   // Keep the title over the first rendered world frame, then dissolve into gameplay.
   yield return null;
   for(float t=0;t<.65f;t+=Time.unscaledDeltaTime){fade.alpha=1-t/.65f;music.volume=.26f*fade.alpha;yield return null;}
   Destroy(this);
  }
  public static void PrepareNewJourney(string directory) {
   var store=new OpenWorldSaveStore(directory); var previous=store.Load();
   if(previous!=null) {
    string backup=Path.Combine(directory,"BeforeNewJourney-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"));Directory.CreateDirectory(backup);
    foreach(string file in Directory.GetFiles(directory,"adventure-*.json"))File.Copy(file,Path.Combine(backup,Path.GetFileName(file)));
   }
   store.Write(OpenWorldSaveData.NewJourney());
  }
  void Update() {
   Rect r=Screen.safeArea;safe.anchorMin=new Vector2(r.x/Screen.width,r.y/Screen.height);safe.anchorMax=new Vector2(r.xMax/Screen.width,r.yMax/Screen.height);
   float t=Time.unscaledTime;artwork.localScale=Vector3.one*(1.05f+.012f*Mathf.Sin(t*.08f));artwork.anchoredPosition=new Vector2(Mathf.Sin(t*.065f)*12,Mathf.Cos(t*.09f)*7);
   var bounds=((RectTransform)canvasObject.transform).rect;
   for(int i=0;i<motes.Length;i++)motes[i].anchoredPosition=new Vector2(Mathf.Repeat(i*173.7f+t*(3+i%4),bounds.width)-bounds.width/2,Mathf.Repeat(i*93.1f+t*(8+i%7),bounds.height)-bounds.height/2);
   if(Input.GetKeyDown(KeyCode.Escape))Back();
  }
  void OnDestroy(){IsOpen=false;if(canvasObject){foreach(var raw in canvasObject.GetComponentsInChildren<RawImage>())if(raw.texture&&raw.texture.name=="Title gradient")Destroy(raw.texture);Destroy(canvasObject);}if(music)Destroy(music);}
  GameObject Modal(string title,string copy) {
   var r=Panel(title,safe,new Color(.025f,.055f,.085f,.98f));r.sizeDelta=new Vector2(650,460);
   Label(r,title,new Vector2(0,160),new Vector2(600,55),30,true);Label(r,copy,new Vector2(0,85),new Vector2(570,80),24,false);
   r.gameObject.SetActive(false);return r.gameObject;
  }
  Button Button(Transform parent,string caption,Vector2 pos,UnityEngine.Events.UnityAction action) {
   var rim=Panel(caption,parent,new Color(.78f,.67f,.43f,.8f));rim.sizeDelta=new Vector2(380,56);rim.anchoredPosition=pos;
   var inner=Panel("Inset",rim,new Color(.025f,.055f,.085f,.92f));Stretch(inner);inner.offsetMin=Vector2.one;inner.offsetMax=-Vector2.one;
   var b=rim.gameObject.AddComponent<Button>();b.targetGraphic=inner.GetComponent<Image>();inner.GetComponent<Image>().raycastTarget=true;var colors=b.colors;colors.highlightedColor=new Color(.6f,.85f,1);colors.pressedColor=new Color(.35f,.6f,.8f);b.colors=colors;b.onClick.AddListener(action);
   Label(rim,caption,Vector2.zero,new Vector2(360,50),20,true);return b;
  }
  Text Label(Transform parent,string value,Vector2 pos,Vector2 size,int fontSize,bool title) {
   var r=Rect(value,parent,pos,size);var text=r.gameObject.AddComponent<Text>();text.font=title?heading:body;text.text=value;text.fontSize=fontSize;text.color=title?gold:new Color(.86f,.9f,.91f);text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;return text;
  }
  static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.sizeDelta=size;r.anchoredPosition=pos;return r;}
  static RectTransform Panel(string name,Transform parent,Color color){var r=Rect(name,parent,Vector2.zero,Vector2.zero);var i=r.gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=false;return r;}
  static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
 }
}


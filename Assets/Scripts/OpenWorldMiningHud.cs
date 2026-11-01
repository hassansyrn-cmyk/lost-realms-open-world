using UnityEngine;using UnityEngine.UI;
namespace LostRealmsOW {
 public sealed partial class OpenWorldMobileHud {
  Button mineControl;
  internal void UpdateMiningControl(){if(!safeRoot)return;
   if(!mineControl){mineControl=PillButton(safeRoot,"MINE",new Vector2(0,155),new Vector2(190,64),new Vector2(.5f,0),22);mineControl.gameObject.name="Mine ore";mineControl.onClick.AddListener(()=>{var gm=OpenWorldManager.I;var ore=GatherNode.NearestOre(gm);if(ore)ore.TryMine(gm);});}
   var manager=OpenWorldManager.I;mineControl.gameObject.SetActive((!titleRoot||!titleRoot.activeSelf)&&GatherNode.NearestOre(manager));
  }
 }
 public sealed class OpenWorldMiningHudDriver:MonoBehaviour {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install(){new GameObject("Ore mining HUD").AddComponent<OpenWorldMiningHudDriver>();}
  void LateUpdate(){if(OpenWorldMobileHud.Instance)OpenWorldMobileHud.Instance.UpdateMiningControl();}
 }
}

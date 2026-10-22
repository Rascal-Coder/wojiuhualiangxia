#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Inkomino
{
    public sealed class SafeLayoutQA:MonoBehaviour
    {
        [Serializable] public sealed class Result {public string status;public List<string> errors=new List<string>();public List<string> checks=new List<string>();public int screenshots;}
        Result result=new Result();string dir;InkominoApp app;
        void Check(bool ok,string name){result.checks.Add(name);if(!ok)result.errors.Add(name);}
        IEnumerator Start()
        {
            app=InkominoApp.Instance;app.enabled=false;app.SourceTutorialEnabled=false;dir=InkominoApp.QAOutput();Directory.CreateDirectory(dir);AudioListener.volume=0;
            Check(WeChatSafeLayout.ReservedTopPixels(null,812)==0,"unavailable native metrics preserve original HUD positions");
            var metrics=new WeChatSafeLayout.Metrics{ok=true,width=375,height=812,hasSafeArea=true,left=0,right=375,top=44,bottom=778,hasMenu=true,menuBottom=64};
            Check(Mathf.Abs(WeChatSafeLayout.ReservedTopPixels(metrics,2436)-72*3)<.1f,"CSS coordinates map once to 3x render pixels");
            foreach(var size in new[]{new Vector2Int(375,812),new Vector2Int(360,800),new Vector2Int(540,836)})
            {
                Screen.SetResolution(size.x,size.y,false);yield return new WaitForSecondsRealtime(.5f);
                metrics=new WeChatSafeLayout.Metrics{ok=true,width=size.x,height=size.y,hasSafeArea=true,left=0,right=size.x,top=44,bottom=size.y-34,hasMenu=true,menuBottom=76};
                WeChatSafeLayout.QaMetrics=metrics;app.ShowHome();yield return Settle();
                string id=size.x+"x"+size.y;
                CheckRect("home",id,metrics);CheckButton("game_assets_btnsettings button",id,metrics);CheckButton("Profile",id,metrics);
                yield return Capture("home-"+id+".png");
                var settings=app.Content.Find("game_assets_btnsettings button").GetComponent<Button>();settings.onClick.Invoke();yield return Settle();Check(!app.InputEnabled&&app.Overlays.GetComponentsInChildren<Button>().Length>0,"settings opens "+id);app.ClosePopup();
                app.StartLevel(23,true);yield return Settle();CheckRect("game",id,metrics);CheckButton("game_assets_btnpause button",id,metrics);
                Check(app.BoardView.CellSize>0,"board geometry remains valid "+id);yield return Capture("game-"+id+".png");
                app.Content.Find("game_assets_btnpause button").GetComponent<Button>().onClick.Invoke();yield return Settle();Check(!app.InputEnabled,"pause disables input "+id);yield return Capture("pause-"+id+".png");app.ClosePopup();
                app.ShowShop();yield return Settle();CheckRect("shop",id,metrics);CheckButton("game_assets_btnsettings button",id,metrics);yield return Capture("shop-"+id+".png");
            }
            WeChatSafeLayout.QaMetrics=null;app.ShowHome();yield return Settle();CheckRect("fallback","desktop",null);
            result.status=result.errors.Count==0?"PASS":"FAIL";File.WriteAllText(Path.Combine(dir,"safe-layout-validation.json"),JsonUtility.ToJson(result,true));Debug.Log("INKOMINO_SAFE_LAYOUT_QA "+result.status);Application.Quit(result.errors.Count==0?0:1);
        }
        IEnumerator Settle(){yield return new WaitForSecondsRealtime(.35f);yield return null;Canvas.ForceUpdateCanvases();yield return null;}
        void CheckRect(string screen,string id,WeChatSafeLayout.Metrics metrics)
        {
            var corners=new Vector3[4];app.Stage.GetWorldCorners(corners);
            Check(corners.All(p=>p.x>=-.5f&&p.x<=Screen.width+.5f&&p.y>=-.5f&&p.y<=Screen.height+.5f),screen+" original portrait stage stays in viewport "+id);
        }
        void CheckButton(string name,string id,WeChatSafeLayout.Metrics metrics)
        {
            var target=app.Content.Find(name) as RectTransform;Check(target!=null,name+" exists "+id);if(!target)return;
            var corners=new Vector3[4];target.GetWorldCorners(corners);float top=(metrics.menuBottom+WeChatSafeLayout.MenuGap)/metrics.height*Screen.height;
            Check(corners.All(p=>p.y<=Screen.height-top+.5f),name+" entirely below native overlay "+id);
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,target.TransformPoint(target.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==target.GetComponent<Button>(),name+" center receives pointer "+id);
        }
        IEnumerator Capture(string file)
        {
            yield return new WaitForEndOfFrame();var camera=FindObjectOfType<Camera>();var canvas=FindObjectOfType<Canvas>();var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;var target=new RenderTexture(Screen.width,Screen.height,24);target.Create();camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;Canvas.ForceUpdateCanvases();camera.Render();var active=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(dir,file),image.EncodeToPNG());RenderTexture.active=active;camera.targetTexture=null;canvas.renderMode=mode;canvas.worldCamera=oldCamera;Destroy(image);target.Release();Destroy(target);result.screenshots++;
        }
    }
}
#endif

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
    // Focused desktop rendering/interaction check for the game-club entry.
    // Selected by -inkominoGameClubQA; only the isolated QA profile is staged.
    public sealed class GameClubQA:MonoBehaviour
    {
        [Serializable] public sealed class Page
        {
            public string resolution,file;
            public float[] entry,stage;
            public int glyphVertices;
        }
        [Serializable] public sealed class Result
        {
            public string status;
            public List<string> checks=new List<string>(),errors=new List<string>();
            public List<Page> pages=new List<Page>();
            public int screenshots;
        }
        readonly Result result=new Result();
        InkominoApp app;string dir,id;bool reporting;

        void Check(bool ok,string name)
        {
            result.checks.Add(name);
            if(!ok){result.errors.Add(name);Debug.LogError("INKOMINO_GAME_CLUB_QA_CHECK "+name);}
        }
        void Awake(){Application.logMessageReceived+=Logged;}
        void OnDestroy(){Application.logMessageReceived-=Logged;}
        void Logged(string condition,string stack,LogType type)
        {
            if(reporting||condition.StartsWith("INKOMINO_GAME_CLUB_QA_CHECK",StringComparison.Ordinal))return;
            if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)
                result.errors.Add(type+": "+condition);
        }
        IEnumerator Start()
        {
            app=InkominoApp.Instance;dir=InkominoApp.QAOutput();Directory.CreateDirectory(dir);
            var routines=new Stack<IEnumerator>();routines.Push(Run());
            while(routines.Count>0)
            {
                bool moved=false;object current=null;
                try{moved=routines.Peek().MoveNext();if(moved)current=routines.Peek().Current;}
                catch(Exception error){result.errors.Add("Unhandled QA exception: "+error);break;}
                if(!moved){routines.Pop();continue;}
                var nested=current as IEnumerator;
                if(nested!=null){routines.Push(nested);continue;}
                yield return current;
            }
            WeChatSafeLayout.QaMetrics=null;
            result.status=result.errors.Count==0?"PASS":"FAIL";
            File.WriteAllText(Path.Combine(dir,"gameclub-validation.json"),JsonUtility.ToJson(result,true));
            reporting=true;
            Debug.Log("INKOMINO_GAME_CLUB_QA "+result.status+" checks="+result.checks.Count+" screenshots="+result.screenshots+" "+string.Join(";",result.errors));
            Application.Quit(result.errors.Count==0?0:1);
        }
        IEnumerator Run()
        {
            Check(app!=null,"application exists");if(!app)yield break;
            Check(app.IsQA,"isolated QA startup");if(!app.IsQA)yield break;
            string qaRoot=Path.GetFullPath(dir)+Path.DirectorySeparatorChar;
            Check(Path.GetFullPath(app.Store.Path).StartsWith(qaRoot,StringComparison.OrdinalIgnoreCase),"save path remains inside QA output");
            app.StopAllCoroutines();app.enabled=false;app.SourceTutorialEnabled=false;AudioListener.volume=0;
            foreach(var size in new[]{new Vector2Int(360,800),new Vector2Int(375,812),new Vector2Int(540,836)})
            {
                Screen.SetResolution(size.x,size.y,false);yield return new WaitForSecondsRealtime(.5f);yield return null;
                id=size.x+"x"+size.y;
                Check(Screen.width==size.x&&Screen.height==size.y,"requested render viewport "+id);
                WeChatSafeLayout.QaMetrics=new WeChatSafeLayout.Metrics{ok=true,width=size.x,height=size.y,hasSafeArea=true,left=0,right=size.x,top=44,bottom=size.y-34,hasMenu=true,menuBottom=76};
                Profile();app.ShowHome();yield return Settle();
                var entry=Find("Home game club entry");
                Check(entry!=null,"home game-club entry exists "+id);if(!entry)continue;
                // A hidden QA player has no OS keyboard focus. Stage the same
                // foreground event that a real user tap would already require.
                entry.SendMessage("OnApplicationFocus",true,SendMessageOptions.DontRequireReceiver);
                var entryBounds=ScreenRect(entry);var stageBounds=ScreenRect(app.Stage);
                Check(Inside(entryBounds,new Rect(0,0,Screen.width,Screen.height)),"entry and native-overlay target remain inside viewport "+id);
                Check(Inside(entryBounds,stageBounds),"entry and native-overlay target remain inside portrait stage "+id);
                float capsuleBottom=Screen.height*(1-WeChatSafeLayout.ReservedTopFraction());
                Check(entryBounds.yMax<=capsuleBottom+.6f,"entry stays below simulated WeChat capsule "+id);
                var divider=app.Content.GetComponentsInChildren<RectTransform>().Where(r=>r.name=="Panel"&&r.rect.width>=1070&&r.rect.height>0&&r.rect.height<=8).OrderByDescending(r=>ScreenRect(r).yMax).FirstOrDefault();
                Check(divider!=null,"wallet divider exists "+id);
                if(divider)Check(entryBounds.yMax<ScreenRect(divider).yMin,"entry stays below wallet divider "+id);
                foreach(string name in new[]{Zh.Play+" menu button","Source event 0","Source event 3","Source navigation"})
                {
                    var other=Find(name);Check(other!=null,"home comparison target exists "+name+" "+id);
                    if(other)Check(!entryBounds.Overlaps(ScreenRect(other)),"entry does not cover "+name+" "+id);
                }
                var label=entry.GetComponentsInChildren<Text>().FirstOrDefault(t=>t.text=="游戏圈");
                Check(label&&label.font==Skin.ChineseFont&&label.fontSize==52,"entry caption uses shared display font at 52 units "+id);
                int vertices=CheckGlyphs(label);
                Check(vertices>=12,"all three entry caption glyphs generate visible geometry "+id);
                Check(entry.GetComponentsInChildren<Image>().Any(i=>i.name=="entry_friends"&&i.sprite==MenuUI.AtlasSprite("entry_friends")),"entry reuses game friends sticker art "+id);
                var button=entry.GetComponent<Button>();Check(button&&button.interactable,"entry has an enabled game button "+id);
                if(button)
                {
                    var pointer=new PointerEventData(EventSystem.current){position=entryBounds.center};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                    Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"entry center receives pointer "+id);
                }
                string file="gameclub-home-"+id+".png";
                result.pages.Add(new Page{resolution=id,file=file,entry=Bounds(entryBounds),stage=Bounds(stageBounds),glyphVertices=vertices});
                yield return Capture(file);
                string before=JsonUtility.ToJson(app.Store.Data);
                if(button)
                {
                    button.onClick.Invoke();yield return Settle();
                    Check(app.IsHomeScreen&&app.IsModalOpen,"desktop click opens game-club information over home "+id);
                    var panel=app.Overlays.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name=="游戏圈 panel");
                    Check(panel!=null,"desktop hint uses the common information panel "+id);
                    var text=app.Overlays.GetComponentsInChildren<Text>().Where(t=>!string.IsNullOrWhiteSpace(t.text)).Select(t=>t.text).ToArray();
                    Check(text.Any(t=>t.Contains("微信")),"desktop hint explains opening in WeChat "+id);
                    Check(!text.Any(t=>t.Contains("http")||t.Contains("openlink")||t.Contains("配置")),"desktop hint contains player-facing copy "+id);
                    app.ClosePopup();yield return Settle();
                    Check(app.IsHomeScreen&&!app.IsModalOpen&&entry.gameObject.activeInHierarchy,"closing hint restores home entry "+id);
                    Check(before==JsonUtility.ToJson(app.Store.Data),"opening game-club hint does not change player progress "+id);
                }
                var oldEntry=entry;app.ShowShop();yield return Settle();CheckEntryGone(oldEntry,"shop");
                app.ShowHome();yield return Settle();oldEntry=Find("Home game club entry");app.ShowAchievements(false);yield return Settle();CheckEntryGone(oldEntry,"achievements");
                app.ShowHome();yield return Settle();oldEntry=Find("Home game club entry");app.StartLevel(23,true);yield return Settle();CheckEntryGone(oldEntry,"level");
                app.ShowHome();yield return Settle();Check(Find("Home game club entry")!=null,"returning home recreates one entry "+id);
                Check(app.Content.GetComponentsInChildren<RectTransform>().Count(r=>r.name=="Home game club entry")==1,"home has only one active game-club entry "+id);
            }
        }
        void Profile()
        {
            app.ClearPopups();long now=SaveStore.Now;
            app.Store.Data=new PlayerProfile{level=23,wins=22,coins=1000,hearts=5,hint=3,freeze=2,lens=1,avatar=2,drawIntroSeen=true,regenAt=now+900,dailyAt=now-86400+1800,shopDay=(now+8*3600)/86400};
            for(int i=0;i<22;i++)app.Store.Data.completed[i]=true;
            ClimbActivity.Refresh(app.Store.Data,now);
        }
        void CheckEntryGone(RectTransform old,string page)
        {
            Check(!old||!old.gameObject.activeInHierarchy,"old entry is inactive after opening "+page+" "+id);
            Check(Find("Home game club entry")==null,"game-club entry absent from "+page+" "+id);
        }
        RectTransform Find(string name){return app.Content.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name==name);}
        IEnumerator Settle(){yield return new WaitForSecondsRealtime(.55f);yield return null;Canvas.ForceUpdateCanvases();yield return null;}
        static float[] Bounds(Rect r){return new[]{r.xMin,r.yMin,r.width,r.height};}
        static bool Inside(Rect inner,Rect outer){return inner.xMin>=outer.xMin-.6f&&inner.xMax<=outer.xMax+.6f&&inner.yMin>=outer.yMin-.6f&&inner.yMax<=outer.yMax+.6f;}
        static Rect ScreenRect(RectTransform transform)
        {
            var corners=new Vector3[4];transform.GetWorldCorners(corners);var points=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(null,p)).ToArray();
            return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
        }
        static int CheckGlyphs(Text label)
        {
            if(!label)return 0;var generator=label.cachedTextGenerator;
            if(generator.vertexCount<12){generator.Invalidate();generator.Populate(label.text,label.GetGenerationSettings(label.rectTransform.rect.size));}
            int visible=0;var vertices=generator.verts;
            for(int i=0;i+3<Mathf.Min(generator.vertexCount,vertices.Count);i+=4)
            {
                float x0=float.PositiveInfinity,y0=x0,x1=float.NegativeInfinity,y1=x1;
                for(int k=0;k<4;k++){var p=vertices[i+k].position;x0=Mathf.Min(x0,p.x);x1=Mathf.Max(x1,p.x);y0=Mathf.Min(y0,p.y);y1=Mathf.Max(y1,p.y);}
                if(x1-x0>.01f&&y1-y0>.01f)visible+=4;
            }
            return visible;
        }
        IEnumerator Capture(string file)
        {
            yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
            var camera=FindObjectOfType<Camera>();var canvas=app.Content.GetComponentInParent<Canvas>().rootCanvas;
            Check(camera&&canvas,"render camera and canvas exist "+id);if(!camera||!canvas)yield break;
            var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;float distance=canvas.planeDistance;
            var oldTarget=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(Screen.width,Screen.height,24);Texture2D image=null;
            try
            {
                target.Create();camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
                float min=1,max=0;for(int x=1;x<8;x++)for(int y=1;y<10;y++){float light=image.GetPixel(image.width*x/8,image.height*y/10).grayscale;min=Mathf.Min(min,light);max=Mathf.Max(max,light);}
                Check(max-min>.1f,"actual rendered screenshot contains game pixels "+id);
                File.WriteAllBytes(Path.Combine(dir,file),image.EncodeToPNG());result.screenshots++;
            }
            finally
            {
                RenderTexture.active=active;camera.targetTexture=oldTarget;canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=distance;
                if(image)Destroy(image);target.Release();Destroy(target);Canvas.ForceUpdateCanvases();
            }
        }
    }
}
#endif

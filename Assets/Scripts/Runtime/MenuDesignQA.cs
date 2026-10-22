#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Inkomino
{
    // Run with -inkominoMenuQA -qaOutput <directory>. The app owns the isolated
    // QA profile; no normal player profile is loaded or changed by this suite.
    public sealed class MenuDesignQA:MonoBehaviour
    {
        [Serializable] public sealed class PageMetric
        {
            public string page,resolution;
            public int bodyLabels,buttons;
            public float minimumBodyPixels,headerGapPixels;
        }
        [Serializable] public sealed class Result
        {
            public string status,bodyFont;
            public List<string> checks=new List<string>(),errors=new List<string>();
            public List<PageMetric> pages=new List<PageMetric>();
            public int screenshots,textMeshChecks,forcedTextRegenerations;
        }
        readonly Result result=new Result();
        InkominoApp app;string dir,id;

        void Check(bool ok,string name)
        {
            result.checks.Add(name);
            if(!ok){result.errors.Add(name);Debug.LogError("INKOMINO_MENU_QA_CHECK "+name);}
        }
        IEnumerator Start()
        {
            app=InkominoApp.Instance;
            dir=InkominoApp.QAOutput();Directory.CreateDirectory(dir);
            // Drive nested routines explicitly so a render/assertion exception
            // still produces a useful JSON failure report and an exit status.
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
            File.WriteAllText(Path.Combine(dir,"menu-design-validation.json"),JsonUtility.ToJson(result,true));
            Debug.Log("INKOMINO_MENU_QA "+result.status+" checks="+result.checks.Count+" screenshots="+result.screenshots+" "+string.Join(";",result.errors));
            Application.Quit(result.errors.Count==0?0:1);
        }
        IEnumerator Run()
        {
            Check(app!=null,"application exists");if(!app)yield break;
            app.enabled=false;app.StopAllCoroutines();app.SourceTutorialEnabled=false;AudioListener.volume=0;
            var body=MenuUI.BodyFont;result.bodyFont=body?body.name:"missing";
            var resource=Resources.Load<Font>("Fonts/LiangxiaInkSerif-Semibold");
            Check(resource&&body==resource&&body!=Skin.ChineseFont,"dedicated readable body font loads instead of display-font fallback");
            foreach(var size in new[]{new Vector2Int(360,800),new Vector2Int(375,812),new Vector2Int(540,836)})
            {
                Screen.SetResolution(size.x,size.y,false);yield return new WaitForSecondsRealtime(.5f);
                yield return null;id=size.x+"x"+size.y;
                Check(Screen.width==size.x&&Screen.height==size.y,"requested render resolution "+id);
                WeChatSafeLayout.QaMetrics=new WeChatSafeLayout.Metrics{ok=true,width=size.x,height=size.y,hasSafeArea=true,left=0,right=size.x,top=44,bottom=size.y-34,hasMenu=true,menuBottom=76};

                Profile(9,8);app.ShowHome();yield return Page("01-home",false);CheckNavigation();CheckCapsule();
                CheckMenuText("01-home",app.Content,true);CheckButtonsAboveNavigation("01-home");
                Check(Find("Home ink scroll showcase")!=null&&Find("Home level directory")!=null,"home has a coherent puzzle showcase and level entry "+id);
                Profile(9,8);app.ShowShop();yield return Page("02-shop");yield return CheckExchange();
                Profile(1,0);app.ShowAchievements(false);yield return Page("03-achievements-zero");
                Profile(251,250);app.Store.Data.bestStreak=17;app.Store.Data.climb.summits=4;
                app.ShowAchievements(false);yield return Page("04-achievements-250");
                app.ShowAchievements(true);yield return Page("05-friends-desktop");
                Check(Find("Friends rank canvas")!=null,"friend surface exists in nested menu layout "+id);
                Check(app.Content.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("微信")),"desktop friends page explains WeChat availability "+id);

                Profile(40,39);app.ShowClimb();yield return Page("06-climb-locked");
                CheckClimbAction("41",false,"locked activity action");
                JoinClimb(3);app.ShowClimb();yield return Page("07-climb-active");
                CheckClimbAction("继续",true,"active activity action");
                JoinClimb(7);app.ShowClimb();yield return Page("08-climb-summit");
                CheckClimbAction("领取",true,"summit activity action");
                JoinClimb(2);ClimbActivity.Fail(app.Store.Data,SaveStore.Now);app.ShowClimb();
                yield return Page("09-climb-cooldown");
                Check(app.Content.GetComponentsInChildren<Button>().Any(b=>!b.interactable&&ButtonText(b).Contains("等待")),"cooldown has a disabled, explained action "+id);
                var rules=typeof(InkominoApp).GetMethod("ClimbInfo",BindingFlags.Instance|BindingFlags.NonPublic);
                Check(rules!=null,"activity rules entry exists "+id);
                if(rules!=null)
                {
                    rules.Invoke(app,null);yield return Settle();
                    Check(app.Overlays.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("模拟")),"rules disclose simulated opponents "+id);
                    Check(!app.Overlays.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("提现")),"rules omit redundant currency disclaimer "+id);
                    CheckMenuText("10-climb-rules",app.Overlays,false);
                    CheckPopupInStage();yield return Capture("10-climb-rules-"+id+".png");app.ClosePopup();
                }
                Profile(23,22);app.StartLevel(5,true);yield return Page("11-game-level5",false);CheckMenuText("11-game-level5",app.Content,true);
                Check(app.BoardView&&app.InputEnabled,"gameplay remains interactive after HUD restyle "+id);
                var boosters=app.Content.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("Source booster ")).ToArray();
                Check(boosters.Length==3,"all three gameplay booster actions remain present "+id);
                var stageRect=ScreenRect(app.Stage);
                Check(boosters.All(b=>{var r=ScreenRect((RectTransform)b.transform);return r.xMin>=stageRect.xMin&&r.xMax<=stageRect.xMax&&r.yMin>=stageRect.yMin;}),"gameplay boosters stay within portrait bounds "+id);
                app.StartLevel(23,true);yield return Page("12-game-level23",false);
                app.Settings(true);yield return Settle();Check(!app.InputEnabled,"pause blocks board input after restyle "+id);
                CheckMenuText("13-game-pause",app.Overlays,false);yield return Capture("13-game-pause-"+id+".png");app.ClearPopups();
                Profile(23,8);app.StartLevel(11,false);app.Board.Restore(app.Board.Level.solution);yield return new WaitForSecondsRealtime(5);
                Check(app.Finished&&!app.Playing,"solving a real level reaches the result page "+id);
                CheckMenuText("14-game-result",app.Content,true);yield return Capture("14-game-result-"+id+".png");
                var atlasImages=app.Content.GetComponentsInChildren<Image>().Where(i=>i.sprite&&i.sprite.texture.name=="Liangxia shared UI atlas").ToArray();
                Check(atlasImages.Length>0&&atlasImages.All(i=>i.sprite.texture.width==1024&&i.sprite.texture.height==1024),"result uses the shared 1024-square UI atlas "+id);
            }
        }
        void Profile(int level,int wins)
        {
            app.ClearPopups();long now=SaveStore.Now;
            app.Store.Data=new PlayerProfile{level=level,wins=wins,coins=1000,hearts=5,hint=3,freeze=2,lens=1,avatar=2,drawIntroSeen=true,regenAt=now+900,dailyAt=now-86400+1800};
            for(int i=0;i<wins&&i<500;i++)app.Store.Data.completed[i]=true;
            ClimbActivity.Refresh(app.Store.Data,now);
        }
        void JoinClimb(int steps)
        {
            Profile(41,40);long now=SaveStore.Now;
            Check(ClimbActivity.Join(app.Store,now),"activity sample can join at "+steps+" stages "+id);
            for(int i=0;i<steps;i++)ClimbActivity.Advance(app.Store.Data,app.Data.levels[40+i],app.Data,now);
        }
        IEnumerator Page(string page,bool menu=true)
        {
            yield return Settle();CheckStage(page);
            if(menu)
            {
                Check(Find("Menu page layout")!=null,page+" uses shared menu adaptation "+id);
                CheckMenuText(page,app.Content,true);CheckHeaderTitle(page);CheckButtonsAboveNavigation(page);CheckIslandLabels(page);
            }
            yield return Capture(page+"-"+id+".png");
        }
        IEnumerator Settle(){yield return new WaitForSecondsRealtime(.42f);yield return null;Canvas.ForceUpdateCanvases();yield return null;}
        RectTransform Find(string name){return app.Content.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name==name);}
        static string ButtonText(Button button){return string.Join(" ",button.GetComponentsInChildren<Text>().Select(t=>t.text));}
        static Rect ScreenRect(RectTransform transform)
        {
            var corners=new Vector3[4];transform.GetWorldCorners(corners);
            var points=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(null,p)).ToArray();
            return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
        }
        void CheckStage(string page)
        {
            var r=ScreenRect(app.Stage);
            Check(r.xMin>=-.6f&&r.xMax<=Screen.width+.6f&&r.yMin>=-.6f&&r.yMax<=Screen.height+.6f,page+" portrait stage stays within viewport "+id);
        }
        void CheckMenuText(string page,Transform root,bool require)
        {
            var labels=root.GetComponentsInChildren<Text>().Where(t=>t.name.StartsWith("Menu text",StringComparison.Ordinal)&&!string.IsNullOrWhiteSpace(t.text)).ToArray();
            var body=labels.Where(t=>t.font==MenuUI.BodyFont).ToArray();
            if(require)Check(body.Length>0,page+" has readable body labels "+id);
            Check(labels.All(t=>!t.resizeTextForBestFit),page+" menu text never silently best-fits "+id);
            Check(labels.All(t=>t.fontSize>=42),page+" menu typography follows 42-unit minimum "+id);
            Check(labels.All(t=>t.font==MenuUI.BodyFont||t.font==MenuUI.NumberFont||t.font==Skin.ChineseFont),page+" menu text uses the shared font pair "+id);
            float minimum=body.Length>0?body.Min(t=>t.fontSize*Mathf.Abs(t.rectTransform.lossyScale.y)):0;
            if(body.Length>0)Check(minimum>=11.8f,page+" rendered body text remains at least 12px "+id);
            result.pages.Add(new PageMetric{page=page,resolution=id,bodyLabels=body.Length,buttons=root.GetComponentsInChildren<Button>().Length,minimumBodyPixels=minimum});
        }
        void CheckHeaderTitle(string page)
        {
            var title=Find("Menu page title");
            Check(title!=null,page+" has a consistent page title "+id);if(!title)return;
            var divider=app.Content.GetComponentsInChildren<RectTransform>().Where(r=>r.name=="Panel"&&r.rect.width>=1070&&r.rect.height>0&&r.rect.height<=8).OrderByDescending(r=>ScreenRect(r).yMax).FirstOrDefault();
            Check(divider!=null,page+" wallet divider exists "+id);if(!divider)return;
            float gap=ScreenRect(divider).yMin-ScreenRect(title).yMax;
            Check(gap>=2,page+" page title stays below moving wallet divider "+id);
            if(result.pages.Count>0)result.pages[result.pages.Count-1].headerGapPixels=gap;
        }
        void CheckButtonsAboveNavigation(string page)
        {
            var nav=Find("Source navigation");if(!nav)return;
            var bounds=ScreenRect(nav);
            var offenders=app.Content.GetComponentsInChildren<Button>().Where(b=>!b.transform.IsChildOf(nav)).Where(b=>ScreenRect((RectTransform)b.transform).yMin<bounds.yMax-1).Select(b=>b.name).ToArray();
            Check(offenders.Length==0,page+" content buttons stay above bottom navigation "+id+(offenders.Length>0?": "+string.Join(",",offenders):""));
        }
        void CheckIslandLabels(string page)
        {
            var stage=ScreenRect(app.Stage);
            var labels=app.Content.GetComponentsInChildren<Text>().Where(t=>t.text=="已通过").ToArray();
            Check(labels.All(t=>{var r=ScreenRect(t.rectTransform);return r.xMin>=stage.xMin-.5f&&r.xMax<=stage.xMax+.5f;}),page+" completed island labels stay inside portrait stage "+id);
        }
        void CheckNavigation()
        {
            var nav=Find("Source navigation");Check(nav!=null,"home navigation exists "+id);if(!nav)return;
            foreach(string key in new[]{"shop","home","rank"})
            {
                var tab=nav.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name=="Tab "+key);
                Check(tab&&tab.GetComponentsInChildren<Text>().Any(t=>!string.IsNullOrWhiteSpace(t.text)),"navigation "+key+" keeps a visible text label "+id);
            }
        }
        void CheckCapsule()
        {
            var settings=Find("game_assets_btnsettings button");Check(settings!=null,"settings button exists "+id);if(!settings)return;
            float bottom=Screen.height*(1-WeChatSafeLayout.ReservedTopFraction());
            Check(ScreenRect(settings).yMax<=bottom+.5f,"settings avoids simulated WeChat capsule "+id);
        }
        void CheckClimbAction(string label,bool enabled,string name)
        {
            Check(app.Content.GetComponentsInChildren<Button>().Any(b=>b.interactable==enabled&&ButtonText(b).Contains(label)),name+" "+id);
        }
        void CheckPopupInStage()
        {
            var stage=ScreenRect(app.Stage);
            var panel=app.Overlays.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name=="活动规则 panel");
            Check(panel!=null,"activity rules has a panel "+id);if(!panel)return;
            var r=ScreenRect(panel);
            Check(r.xMin>=stage.xMin-.5f&&r.xMax<=stage.xMax+.5f&&r.yMin>=stage.yMin-.5f&&r.yMax<=stage.yMax+.5f,"activity rules panel stays within stage "+id);
        }
        IEnumerator CheckExchange()
        {
            var row=Find("Shop row 0");Check(row!=null,"shop has first item row "+id);if(!row)yield break;
            var button=row.GetComponentsInChildren<Button>().FirstOrDefault(b=>ButtonText(b).Contains("150"));
            Check(button!=null,"shop exposes 150-coin pen exchange "+id);if(!button)yield break;
            var pointer=new PointerEventData(EventSystem.current){position=ScreenRect((RectTransform)button.transform).center};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"shop exchange center receives pointer "+id);
            int beforeCoins=app.Store.Data.coins,beforeHints=app.Store.Data.hint;
            button.onClick.Invoke();yield return Settle();
            Check(app.Store.Data.coins==beforeCoins-150&&app.Store.Data.hint==beforeHints+3,"pen exchange deducts 150 coins and adds exactly three boosters "+id);
            app.ClearPopups();app.ShowShop();yield return Settle();
            Check(app.Store.Data.coins==beforeCoins-150&&app.Store.Data.hint==beforeHints+3&&Find("Shop row 0")!=null,"shop redraw preserves exchange balance and inventory "+id);
        }
        IEnumerator Capture(string file)
        {
            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();
            var camera=FindObjectOfType<Camera>();var canvas=app.Content.GetComponentInParent<Canvas>().rootCanvas;
            Check(camera!=null&&canvas!=null,"render camera and canvas exist "+file);if(!camera||!canvas)yield break;
            var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldDistance=canvas.planeDistance;
            var previousTarget=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(Screen.width,Screen.height,24);target.Create();
            camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
            Canvas.ForceUpdateCanvases();CheckTextMeshes(file);camera.Render();RenderTexture.active=target;
            var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
            float min=1,max=0;for(int x=1;x<8;x++)for(int y=1;y<10;y++){float light=image.GetPixel(image.width*x/8,image.height*y/10).grayscale;min=Mathf.Min(min,light);max=Mathf.Max(max,light);}
            Check(max-min>.1f,"actual rendered screenshot contains game pixels "+file);
            File.WriteAllBytes(Path.Combine(dir,file),image.EncodeToPNG());
            RenderTexture.active=active;camera.targetTexture=previousTarget;canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldDistance;
            Destroy(image);target.Release();Destroy(target);result.screenshots++;
        }
        void CheckTextMeshes(string file)
        {
            // A valid RectTransform/font size does not prove that Unity emitted
            // glyphs. A short Truncate box can erase an entire Noto line while
            // all structural checks pass. Inspect the generator used by Text.
            var labels=app.Stage.GetComponentsInChildren<Text>().Where(t=>
                !string.IsNullOrWhiteSpace(t.text)&&
                (t.name.StartsWith("Menu text",StringComparison.Ordinal)||t.name=="Menu page title"||t.name=="Shop rewarded CTA label")).ToArray();
            foreach(var label in labels)
            {
                var generator=label.cachedTextGenerator;
                if(generator.vertexCount<4)
                {
                    // Force generation with the label's real settings, including
                    // its actual rectangle and overflow policy. Never enlarge
                    // the box or substitute settings just to make QA succeed.
                    generator.Invalidate();
                    generator.Populate(label.text,label.GetGenerationSettings(label.rectTransform.rect.size));
                    result.forcedTextRegenerations++;
                }
                bool hasGeometry=false;
                var vertices=generator.verts;
                // The bundled Tuanjie uGUI Text consumes every generated vertex;
                // a single visible digit is exactly one four-vertex quad.
                for(int i=0;i+3<Mathf.Min(generator.vertexCount,vertices.Count);i+=4)
                {
                    float minX=float.PositiveInfinity,minY=float.PositiveInfinity,maxX=float.NegativeInfinity,maxY=float.NegativeInfinity;
                    for(int k=0;k<4;k++)
                    {
                        var p=vertices[i+k].position;
                        minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);
                    }
                    if(maxX-minX>.01f&&maxY-minY>.01f){hasGeometry=true;break;}
                }
                string preview=label.text.Replace('\n',' ').Replace('\r',' ');
                if(preview.Length>56)preview=preview.Substring(0,56)+"…";
                Check(generator.vertexCount>=4&&hasGeometry,"visible TextGenerator glyph mesh "+file+" / "+label.transform.parent.name+" / "+preview);
                result.textMeshChecks++;
            }
        }
    }
}
#endif

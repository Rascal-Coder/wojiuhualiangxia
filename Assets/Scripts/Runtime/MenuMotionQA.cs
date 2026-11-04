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
    // Real uGUI animation/effect checks. Timing checks run without PNG encoding;
    // a second pass records actual RenderTexture frames and their elapsed times.
    public sealed class MenuMotionQA:MonoBehaviour
    {
        [Serializable] public sealed class Frame
        {
            public string sequence,file;
            public float elapsed,entranceProgress,pressScale,progressTotal;
            public int activeMotions,particles;
        }
        [Serializable] public sealed class Entry
        {
            public string page;
            public int animatedChildren;
            public float completionSeconds;
            public bool inputReadyAtStart;
        }
        [Serializable] public sealed class RaycastEvidence
        {
            public string page,stage,buttonPath,canvasMode;
            public float elapsed,centerX,centerY,rectWidth,rectHeight,graphicAlpha,rendererAlpha,inheritedAlpha;
            public int frame,graphicDepth,registeredGraphics;
            public bool targetExists,active,enabled,interactable,raycastTarget,rendererCull,insideRect,graphicRaycast,topHitMatches;
            public List<string> hits=new List<string>(),groups=new List<string>();
        }
        [Serializable] public sealed class Result
        {
            public string status;
            public List<string> checks=new List<string>(),errors=new List<string>();
            public List<Entry> entries=new List<Entry>();
            public List<Frame> frames=new List<Frame>();
            public List<RaycastEvidence> raycasts=new List<RaycastEvidence>();
            public int homeLiveBaseline,homeActiveBaseline,homeLiveAfterSwitches,homeActiveAfterSwitches;
        }
        struct Pose
        {
            public RectTransform rect;
            public Vector2 position;
            public Vector3 scale;
            public Quaternion rotation;
        }
        sealed class BufferedFrame
        {
            public Frame record;
            public Texture2D image;
            public string path;
        }
        readonly Result result=new Result();
        InkominoApp app;string dir;

        void Check(bool ok,string name)
        {
            result.checks.Add(name);
            if(!ok){result.errors.Add(name);Debug.LogError("INKOMINO_MENU_MOTION_CHECK "+name);}
        }
        IEnumerator Start()
        {
            app=InkominoApp.Instance;dir=InkominoApp.QAOutput();Directory.CreateDirectory(dir);
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                bool moved=false;object current=null;
                try{moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}
                catch(Exception error){result.errors.Add("Unhandled QA exception: "+error);break;}
                if(!moved){stack.Pop();continue;}
                var nested=current as IEnumerator;
                if(nested!=null){stack.Push(nested);continue;}
                yield return current;
            }
            WeChatSafeLayout.QaMetrics=null;
            result.status=result.errors.Count==0?"PASS":"FAIL";
            File.WriteAllText(Path.Combine(dir,"menu-motion-validation.json"),JsonUtility.ToJson(result,true));
            Debug.Log("INKOMINO_MENU_MOTION_QA "+result.status+" checks="+result.checks.Count+" frames="+result.frames.Count+" "+string.Join(";",result.errors));
            Application.Quit(result.errors.Count==0?0:1);
        }
        IEnumerator Run()
        {
            Check(app!=null,"application exists");if(!app)yield break;
            app.enabled=false;app.StopAllCoroutines();app.SourceTutorialEnabled=false;AudioListener.volume=0;
            Screen.SetResolution(360,800,false);yield return new WaitForSecondsRealtime(.5f);yield return null;
            Check(Screen.width==360&&Screen.height==800,"motion capture uses real 360x800 rendering");
            WeChatSafeLayout.QaMetrics=new WeChatSafeLayout.Metrics{ok=true,width=360,height=800,hasSafeArea=true,left=0,right=360,top=44,bottom=766,hasMenu=true,menuBottom=76};
            Profile(false);app.ShowHome();yield return Pause(.45f);
            result.homeLiveBaseline=MenuMotion.LiveCount;result.homeActiveBaseline=MenuMotion.ActiveCount;
            Check(!MenuMotion.Reduced,"full menu motion is enabled in ordinary profile");
            Check(app.Content.GetComponentsInChildren<Button>().Where(b=>b.name=="Add"||b.name.StartsWith("Tab ")).All(b=>b.GetComponent<MenuPressFeedback>()),"wallet plus and dock tabs keep press feedback");
            CheckCounterConsistency("initial home");
            yield return TestAchievementCoverage();

            yield return TimeEntry("shop",app.ShowShop);
            yield return RecordEntry("01-shop-entry",app.ShowShop);
            yield return TestPressAndExchange();

            Profile(false);yield return TimeEntry("achievements",()=>app.ShowAchievements(false));
            yield return Pause(.12f);CheckProgress("normal achievements");
            yield return RecordEntry("02-achievements-entry",()=>app.ShowAchievements(false));
            CheckProgress("recorded achievements");

            ClimbProfile(false,3);yield return TimeEntry("climb",app.ShowClimb);
            yield return RecordEntry("03-climb-entry",app.ShowClimb);
            var sparkles=app.Content.GetComponentsInChildren<MenuGoldSparkles>();
            Check(sparkles.Length>0&&sparkles.Any(m=>m.PeakParticles>0),"ordinary climb actually emits decorative sparkle particles");
            Check(sparkles.All(m=>m.PeakParticles<=12&&m.ActiveParticles<=m.ParticleCapacity),"decorative sparkle bursts remain within their bounded particle capacity");
            var floats=app.Content.GetComponentsInChildren<MenuFloatMotion>();
            Check(floats.All(m=>!m.IsAnimating),"ordinary climb keeps artwork planted instead of bobbing");
            yield return TestDynamicPreference();
            yield return TestReduced();
            yield return TestPageLifetime();
        }
        void Profile(bool reduced)
        {
            app.ClearPopups();long now=SaveStore.Now;
            app.Store.Data=new PlayerProfile{level=251,wins=250,bestStreak=17,coins=1000,hearts=5,hint=3,freeze=2,lens=1,avatar=2,drawIntroSeen=true,regenAt=now+900,dailyAt=now-86400+1800,reducedMenuMotion=reduced};
            for(int i=0;i<250;i++)app.Store.Data.completed[i]=true;
            ClimbActivity.Refresh(app.Store.Data,now);app.Store.Data.climb.summits=4;
        }
        IEnumerator TestAchievementCoverage()
        {
            Profile(false);app.Store.Data.level=18;app.Store.Data.wins=17;
            app.ShowAchievements(false);
            // Force the real first-frame mesh while progress reveal scale is zero.
            // RenderTexture capture changes canvas mode and can hide this cached-mesh bug.
            Canvas.ForceUpdateCanvases();CheckRoundedCoverage("collapsed entry");
            yield return new WaitForEndOfFrame();
            SaveScreen("achievement-17-entry.png");
            yield return new WaitForSecondsRealtime(.65f);
            yield return new WaitForEndOfFrame();
            CheckRoundedCoverage("settled entry");SaveScreen("achievement-17-settled.png");
            Check(app.Content.GetComponentsInChildren<RectTransform>().Count(r=>r.name.StartsWith("Achievement milestone "))==5,"17 wins retains five achievement rows");
            app.ShowAchievements(true);yield return new WaitForSecondsRealtime(.5f);
            app.ShowAchievements(false);yield return new WaitForSecondsRealtime(.65f);
            yield return new WaitForEndOfFrame();
            CheckRoundedCoverage("return from friends");SaveScreen("achievement-17-return.png");
        }
        void SaveScreen(string file)
        {
            var image=ScreenCapture.CaptureScreenshotAsTexture();
            bool visible=image.GetPixels32().Any(c=>c.r>80&&c.g>60);
            if(visible)File.WriteAllBytes(Path.Combine(dir,file),image.EncodeToPNG());
            Destroy(image);
            // Hidden/minimized Windows players can return an empty backbuffer.
            // Mesh assertions above run before this explicit rendering fallback.
            if(!visible){result.checks.Add("empty hidden-window backbuffer; explicit render fallback "+file);FidelityCapture.Save(Path.Combine(dir,file),Screen.width,Screen.height);}
        }
        void CheckRoundedCoverage(string stage)
        {
            foreach(var graphic in app.Content.GetComponentsInChildren<RoundedGraphic>())
            {
                var mesh=graphic.canvasRenderer.GetMesh();if(!mesh||mesh.vertexCount==0)continue;
                var r=graphic.rectTransform.rect;var bounds=mesh.bounds;
                bool finite=!float.IsNaN(bounds.size.x)&&!float.IsInfinity(bounds.size.x)&&!float.IsNaN(bounds.size.y)&&!float.IsInfinity(bounds.size.y);
                Check(finite&&bounds.min.x>=r.xMin-32&&bounds.max.x<=r.xMax+32&&bounds.min.y>=r.yMin-32&&bounds.max.y<=r.yMax+32,"bounded rounded mesh "+stage+" "+HierarchyPath(graphic.transform));
            }
        }
        void ClimbProfile(bool reduced,int stages)
        {
            Profile(reduced);app.Store.Data.level=41;app.Store.Data.wins=40;
            long now=SaveStore.Now;ClimbActivity.Join(app.Store,now);
            for(int i=0;i<stages;i++)ClimbActivity.Advance(app.Store.Data,app.Data.levels[40+i],app.Data,now);
        }
        IEnumerator Pause(float seconds)
        {
            float start=Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup-start<seconds)yield return null;
            Canvas.ForceUpdateCanvases();
        }
        RectTransform Find(string name)
        {
            return app.Content.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name==name);
        }
        static string Label(Button b){return string.Join(" ",b.GetComponentsInChildren<Text>().Select(t=>t.text));}
        Button Primary()
        {
            var named=Find("Shop exchange hint");if(named)return named.GetComponent<Button>();
            var tab=Find("Achievements tab");if(tab)return tab.GetComponent<Button>();
            return app.Content.GetComponentsInChildren<Button>().LastOrDefault(b=>Label(b).Contains("继续"));
        }
        static Vector2 Center(RectTransform rect)
        {
            return RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        }
        bool CenterReceives(Button button)
        {
            if(!button||!EventSystem.current)return false;
            var pointer=new PointerEventData(EventSystem.current){position=Center((RectTransform)button.transform)};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            return hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button;
        }
        void DiagnoseRaycast(string page,string stage,Button button,float elapsed)
        {
            var evidence=new RaycastEvidence{page=page,stage=stage,elapsed=elapsed,frame=Time.frameCount,targetExists=button!=null,graphicDepth=-999};
            if(button)
            {
                var rect=(RectTransform)button.transform;var center=Center(rect);var graphic=button.targetGraphic;
                evidence.buttonPath=HierarchyPath(button.transform);evidence.active=button.gameObject.activeInHierarchy;evidence.enabled=button.enabled;evidence.interactable=button.IsInteractable();
                evidence.centerX=center.x;evidence.centerY=center.y;evidence.rectWidth=rect.rect.width;evidence.rectHeight=rect.rect.height;
                var canvas=graphic?graphic.canvas:button.GetComponentInParent<Canvas>();
                var camera=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
                evidence.canvasMode=canvas?canvas.renderMode.ToString():"missing";
                evidence.insideRect=RectTransformUtility.RectangleContainsScreenPoint(rect,center,camera);
                if(graphic)
                {
                    evidence.raycastTarget=graphic.raycastTarget;evidence.graphicDepth=graphic.depth;evidence.graphicAlpha=graphic.color.a;
                    evidence.rendererCull=graphic.canvasRenderer.cull;evidence.rendererAlpha=graphic.canvasRenderer.GetAlpha();evidence.inheritedAlpha=graphic.canvasRenderer.GetInheritedAlpha();
                    evidence.graphicRaycast=graphic.Raycast(center,camera);
                    evidence.registeredGraphics=canvas?GraphicRegistry.GetGraphicsForCanvas(canvas).Count:0;
                }
                foreach(var group in button.GetComponentsInParent<CanvasGroup>(true))
                    evidence.groups.Add(HierarchyPath(group.transform)+" alpha="+group.alpha+" blocks="+group.blocksRaycasts+" interactable="+group.interactable+" ignoreParent="+group.ignoreParentGroups);
                if(EventSystem.current)
                {
                    var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=center},hits);
                    evidence.topHitMatches=hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button;
                    foreach(var hit in hits.Take(8))evidence.hits.Add(HierarchyPath(hit.gameObject.transform)+" depth="+hit.depth+" sorting="+hit.sortingOrder);
                }
            }
            result.raycasts.Add(evidence);
            Debug.Log("INKOMINO_MENU_RAYCAST "+JsonUtility.ToJson(evidence));
        }
        static string HierarchyPath(Transform transform)
        {
            var names=new Stack<string>();for(var node=transform;node;node=node.parent)names.Push(node.name);return string.Join("/",names);
        }
        IEnumerator TimeEntry(string name,Action show)
        {
            float start=Time.realtimeSinceStartup;show();Canvas.ForceUpdateCanvases();
            var entrances=app.Content.GetComponentsInChildren<MenuPageEntrance>();
            var entry=new Entry{page=name,animatedChildren=entrances.Sum(m=>m.AnimatedChildren)};
            DiagnoseRaycast(name,"creation",Primary(),Time.realtimeSinceStartup-start);
            // GraphicRaycaster excludes native CanvasRenderer.depth == -1.
            // The diagnostic control proves that even a reduced/nonanimated
            // new button has no native depth until its first real render. Render
            // that initial frame without advancing time or waiting out entry;
            // then test the same raycast a player can perform on visible UI.
            var inputProbe=ReadFrame(name+"-input-probe",0,Time.unscaledTime);
            if(inputProbe!=null)Destroy(inputProbe.image);
            Canvas.ForceUpdateCanvases();entry.inputReadyAtStart=CenterReceives(Primary());
            DiagnoseRaycast(name,"first actual rendered frame",Primary(),Time.realtimeSinceStartup-start);
            Check(entrances.Length>0&&entry.animatedChildren>0,name+" has an actual page entrance");
            Check(entry.inputReadyAtStart,name+" main button receives input in the first actual rendered frame");
            Check(entrances.Any(m=>m.IsAnimating&&!m.Completed),name+" starts in an animated intermediate state");
            bool firstFrame=true;
            while(entrances.Any(m=>!m.Completed||m.IsAnimating)&&Time.realtimeSinceStartup-start<.4f)
            {
                yield return null;
                if(firstFrame){DiagnoseRaycast(name,"first following frame",Primary(),Time.realtimeSinceStartup-start);firstFrame=false;}
            }
            entry.completionSeconds=Time.realtimeSinceStartup-start;result.entries.Add(entry);
            Check(entrances.All(m=>m.Completed&&!m.IsAnimating)&&entry.completionSeconds<.4f,name+" entry converges in less than 400ms without recording overhead");
            Check(CenterReceives(Primary()),name+" main button remains clickable after entry");
            DiagnoseRaycast(name,"settled",Primary(),Time.realtimeSinceStartup-start);
        }
        IEnumerator RecordEntry(string sequence,Action show)
        {
            show();Canvas.ForceUpdateCanvases();float start=Time.unscaledTime;
            // Read back actual consecutive engine frames; defer expensive PNG
            // encoding until the entry has finished. Keep only one short page
            // sequence in memory at a time and record its real time stamps.
            var buffered=new List<BufferedFrame>();
            var first=ReadFrame(sequence,0,start);if(first!=null)buffered.Add(first);
            DiagnoseRaycast(sequence,"after first actual frame render",Primary(),Time.unscaledTime-start);
            int index=1;
            while(Time.unscaledTime-start<.5f)
            {
                yield return null;
                if(buffered.Count<30)
                {
                    var shot=ReadFrame(sequence,index++,start);if(shot!=null)buffered.Add(shot);
                }
            }
            if(buffered.Count==0||buffered[buffered.Count-1].record.elapsed<.48f)
            {
                var shot=ReadFrame(sequence,index,start);if(shot!=null)buffered.Add(shot);
            }
            Check(app.Content.GetComponentsInChildren<MenuPageEntrance>().All(m=>m.Completed&&!m.IsAnimating),sequence+" final recorded frame is settled");
            foreach(var shot in buffered)WriteFrame(shot);
            // Let the frame containing batch encoding finish before starting
            // another timing test; its delta must not fast-forward a new page.
            yield return null;
        }
        PointerEventData Pointer(Button button)
        {
            return new PointerEventData(EventSystem.current){pointerId=7,button=PointerEventData.InputButton.Left,position=Center((RectTransform)button.transform)};
        }
        IEnumerator TestPressAndExchange()
        {
            app.ShowShop();yield return Pause(.45f);
            var rect=Find("Shop exchange hint");Check(rect!=null,"shop pen exchange exists");if(!rect)yield break;
            var button=rect.GetComponent<Button>();var press=button.GetComponent<MenuPressFeedback>();
            Check(press!=null,"exchange button has press feedback");if(!press)yield break;
            Check(press.UsesVisualWrapper&&press.VisualTarget!=rect,"press targets an artwork wrapper rather than the hit rectangle");
            Check(CenterReceives(button),"exchange button center raycasts to the button");
            var pointer=Pointer(button);Vector3 hitScale=rect.localScale;Vector2 hitPosition=rect.anchoredPosition;
            int coins=app.Store.Data.coins,hints=app.Store.Data.hint;float start=Time.unscaledTime;
            SaveFrame("04-button-press",0,start);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);
            Check(press.IsPressed&&press.CurrentScale<1,"pointer down acknowledges press in the event frame");
            Check(Feel.LastKind==Feel.Kind.Tap,"pointer down plays the shared tap feel");
            SaveFrame("04-button-press",1,start);yield return Pause(.09f);SaveFrame("04-button-press",2,start);
            Check(press.CurrentScale<=.95f,"pointer down reaches the intended compressed visual");
            Check(Vector3.Distance(rect.localScale,hitScale)<.0001f&&Vector2.Distance(rect.anchoredPosition,hitPosition)<.001f,"press preserves the hit rectangle");
            Check(CenterReceives(button),"pressed button keeps the same clickable center");
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerUpHandler);
            yield return Pause(.17f);SaveFrame("04-button-press",3,start);
            Check(Restored(press),"pointer up restores press artwork");
            Check(app.Store.Data.coins==coins&&app.Store.Data.hint==hints,"pointer down and up do not invoke an extra exchange");

            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);yield return Pause(.09f);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerExitHandler);yield return Pause(.17f);
            Check(Restored(press),"pointer exit restores press artwork");
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);yield return Pause(.09f);
            ExecuteEvents.Execute(button.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.cancelHandler);
            yield return null;Check(Restored(press),"cancel restores press artwork without waiting for another tap");

            button.interactable=false;
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);yield return Pause(.1f);
            Check(Restored(press),"disabled button ignores press animation");
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerUpHandler);button.interactable=true;
            Check(app.Store.Data.coins==coins&&app.Store.Data.hint==hints,"exit cancel and disabled presses grant no boosters");

            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);yield return Pause(.09f);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerUpHandler);
            float clickTime=Time.unscaledTime;
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            yield return CaptureExchangeCelebration(clickTime);
            Check(app.Store.Data.coins==coins-150&&app.Store.Data.hint==hints+3,"one complete pointer click exchanges exactly once: minus 150 coins plus three pens");
            app.ClearPopups();app.ShowShop();yield return Pause(.4f);
            Check(app.Store.Data.coins==coins-150&&app.Store.Data.hint==hints+3,"exchange totals survive animated shop redraw");
        }
        IEnumerator CaptureExchangeCelebration(float start)
        {
            var buffered=new List<BufferedFrame>();float[] marks={.12f,.3f,.45f,.55f};
            for(int i=0;i<marks.Length;i++)
            {
                while(Time.unscaledTime-start<marks[i])yield return null;
                if(i==2)
                {
                    var art=app.Overlays.GetComponentsInChildren<Image>().FirstOrDefault(image=>image.name=="menu_pen");
                    Check(art&&art.sprite,"coin exchange confirmation displays the newly granted pen art");
                    var celebration=app.Overlays.GetComponentsInChildren<MenuGoldSparkles>();
                    Check(celebration.Length>0&&celebration.Any(m=>m.PeakParticles>0&&m.PeakParticles<=12),"coin exchange confirmation actually emits a bounded celebration burst");
                    Check(celebration.All(m=>!m.Loop&&!m.RequestedRepeat),"coin exchange celebration is a single burst rather than a repeating reward prompt");
                }
                var shot=ReadFrame("08-purchase-celebration",i,start);if(shot!=null)buffered.Add(shot);
            }
            foreach(var shot in buffered)WriteFrame(shot);
            yield return null;
        }
        static bool Restored(MenuPressFeedback press)
        {
            return press&&!press.IsPressed&&!press.IsAnimating&&Mathf.Abs(press.CurrentScale-1)<.001f&&
                (!press.VisualTarget||Vector3.Distance(press.VisualTarget.localScale,press.OriginalVisualScale)<.001f);
        }
        void CheckProgress(string context)
        {
            int[] milestones={10,50,100,250,500};float fullWidth=0;
            var targets=new List<MenuProgressReveal>();
            foreach(int milestone in milestones)
            {
                var row=Find("Achievement milestone "+milestone);
                var progress=row?row.GetComponentInChildren<MenuProgressReveal>():null;
                Check(progress!=null,context+" progress exists for "+milestone);if(!progress)continue;
                targets.Add(progress);fullWidth=Mathf.Max(fullWidth,progress.TargetWidth);
                Check(progress.Completed&&!progress.IsAnimating&&Mathf.Abs(progress.Progress01-1)<.001f,context+" reveal completes for "+milestone);
                Check(progress.Fill&&Mathf.Abs(progress.Fill.rect.width-progress.TargetWidth)<.1f&&Mathf.Abs(progress.Fill.localScale.x-1)<.001f,context+" actual fill reaches its exact width for "+milestone);
            }
            if(targets.Count==5)
            {
                for(int i=0;i<targets.Count;i++)
                    Check(Mathf.Abs(targets[i].TargetWidth-fullWidth*Mathf.Clamp01(250f/milestones[i]))<.1f,context+" 250 wins maps to correct milestone fraction "+milestones[i]);
            }
        }
        IEnumerator TestDynamicPreference()
        {
            // The summit contains an ordinary repeating halo, an existing float
            // and a one-shot treasure celebration. Toggle the preference on this
            // very page, without rebuilding any of those effects.
            ClimbProfile(false,7);app.ShowClimb();yield return Pause(.15f);
            var effects=app.Content.GetComponentsInChildren<MenuGoldSparkles>();
            var repeating=effects.Where(m=>m.RequestedRepeat).ToArray();
            var oneShot=effects.Where(m=>!m.RequestedRepeat).ToArray();
            var floats=app.Content.GetComponentsInChildren<MenuFloatMotion>();
            Check(repeating.Length>0&&repeating.Any(m=>m.PeakParticles>0),"dynamic preference test starts with existing repeating sparkle playback");
            Check(oneShot.Length>0&&oneShot.Any(m=>m.PeakParticles>0),"dynamic preference test starts with an existing one-shot celebration");
            app.Store.Data.reducedMenuMotion=true;yield return null;
            Check(effects.All(m=>m.Completed&&!m.IsAnimating&&m.ActiveParticles==0),"turning reduced motion on stops already-created particle effects on the next frame");
            Check(floats.All(m=>!m.IsAnimating),"turning reduced motion on leaves no idle artwork bobbing");
            Check(app.Content.GetComponentsInChildren<MenuPageEntrance>().All(m=>m.Completed&&!m.IsAnimating),"turning reduced motion on finishes an entry already in progress");
            Check(MenuMotion.ActiveCount==0,"dynamic reduced preference leaves no active menu playback");
            SaveFrame("09-live-motion-preference",0,Time.unscaledTime);

            app.Store.Data.reducedMenuMotion=false;yield return null;
            Check(repeating.All(m=>m.RequestedRepeat&&m.IsAnimating),"turning full motion back on resumes only explicitly requested loops");
            Check(oneShot.All(m=>m.Completed&&!m.IsAnimating&&m.ActiveParticles==0),"turning full motion back on does not restart one-shot celebrations");
            bool sawRepeatedBurst=false,sawOneShotReplay=false;float start=Time.unscaledTime;
            while(Time.unscaledTime-start<3.9f)
            {
                yield return null;
                sawRepeatedBurst|=repeating.Any(m=>m.ActiveParticles>0);
                sawOneShotReplay|=oneShot.Any(m=>m.IsAnimating||m.ActiveParticles>0);
            }
            Check(sawRepeatedBurst,"existing ambient sparkle loops emit again within their 3.7-second maximum interval");
            Check(!sawOneShotReplay,"existing one-shot rewards never replay during ambient-loop resumption");
            Check(floats.All(m=>!m.IsAnimating),"restoring full motion does not resume idle artwork bobbing");
            SaveFrame("09-live-motion-preference",1,start);
        }
        IEnumerator TestReduced()
        {
            Profile(true);Check(MenuMotion.Reduced,"profile preference activates reduced menu motion");
            yield return ReducedPage("05-reduced-shop",app.ShowShop);
            var buttonRect=Find("Shop exchange hint");var button=buttonRect?buttonRect.GetComponent<Button>():null;
            var press=button?button.GetComponent<MenuPressFeedback>():null;
            Check(press!=null,"reduced shop still has button interaction");
            if(press)
            {
                var pointer=Pointer(button);ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);yield return null;
                Check(Restored(press),"reduced preference keeps press artwork at its terminal pose");
                ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerUpHandler);
                Check(CenterReceives(button),"reduced preference never disables button input");
            }
            yield return ReducedPage("06-reduced-achievements",()=>app.ShowAchievements(false));CheckProgress("reduced achievements");
            ClimbProfile(true,3);yield return ReducedPage("07-reduced-climb",app.ShowClimb);
        }
        IEnumerator ReducedPage(string name,Action show)
        {
            show();Canvas.ForceUpdateCanvases();
            DiagnoseRaycast(name,"reduced creation control",Primary(),0);
            Check(app.Content.GetComponentsInChildren<MenuPageEntrance>().All(m=>m.Completed&&!m.IsAnimating),name+" entry is immediately at its final state");
            Check(app.Content.GetComponentsInChildren<MenuProgressReveal>().All(m=>m.Completed&&Mathf.Abs(m.Progress01-1)<.001f&&m.Fill&&Mathf.Abs(m.Fill.localScale.x-1)<.001f&&Mathf.Abs(m.Fill.rect.width-m.TargetWidth)<.1f),name+" progress is immediately at its actual final width and scale");
            Check(MenuMotion.ActiveCount==0,name+" starts no decorative animation or global playback");
            yield return null;yield return null;Canvas.ForceUpdateCanvases();
            var poses=Snapshot(app.Content);float start=Time.unscaledTime;SaveFrame(name,0,start);
            yield return Pause(.45f);SaveFrame(name,1,start);
            Check(Unchanged(poses),name+" has no drifting or looping transforms");
            Check(app.Content.GetComponentsInChildren<MenuGoldSparkles>().All(m=>m.ActiveParticles==0&&!m.IsAnimating),name+" creates no looping sparkle particles");
            Check(app.Content.GetComponentsInChildren<MenuFloatMotion>().All(m=>m.Completed&&!m.IsAnimating),name+" has no active float loops");
            Check(MenuMotion.ActiveCount==0,name+" remains at zero active menu playback");
        }
        static Pose[] Snapshot(Transform parent)
        {
            return parent.GetComponentsInChildren<RectTransform>().Select(r=>new Pose{rect=r,position=r.anchoredPosition,scale=r.localScale,rotation=r.localRotation}).ToArray();
        }
        static bool Unchanged(Pose[] poses)
        {
            return poses.All(p=>p.rect&&Vector2.Distance(p.rect.anchoredPosition,p.position)<.01f&&Vector3.Distance(p.rect.localScale,p.scale)<.0001f&&Quaternion.Angle(p.rect.localRotation,p.rotation)<.01f);
        }
        IEnumerator TestPageLifetime()
        {
            Profile(false);app.ShowHome();yield return Pause(.45f);
            // Refresh the baseline with the same profile used by the stress run.
            int live=MenuMotion.LiveCount,active=MenuMotion.ActiveCount;
            for(int i=0;i<20;i++)
            {
                switch(i%4){case 0:app.ShowShop();break;case 1:app.ShowAchievements(false);break;case 2:app.ShowClimb();break;default:app.ShowHome();break;}
                yield return null;
            }
            app.ShowHome();yield return Pause(.5f);
            result.homeLiveAfterSwitches=MenuMotion.LiveCount;result.homeActiveAfterSwitches=MenuMotion.ActiveCount;
            Check(MenuMotion.LiveCount==live&&MenuMotion.ActiveCount==active,"20 page switches return both motion counters to the same home baseline");
            Check(app.Content.GetComponentsInChildren<MenuPageEntrance>().Length==1&&app.Content.GetComponentsInChildren<MenuPageEntrance>().All(m=>m.Completed&&!m.IsAnimating)&&!app.Content.GetComponentsInChildren<MenuProgressReveal>().Any()&&!app.Content.GetComponentsInChildren<ClimbPageMotion>().Any(),"home retains exactly its settled entrance and no old progress or climb motion components");
            CheckCounterConsistency("home after 20 switches");
            Check(FindObjectsOfType<MenuMotionProbe>().All(m=>m.transform.IsChildOf(app.Stage)),"no menu motion ticker survives outside the game stage");
        }
        void CheckCounterConsistency(string name)
        {
            var probes=FindObjectsOfType<MenuMotionProbe>().Where(m=>m.isActiveAndEnabled).ToArray();
            Check(MenuMotion.LiveCount==probes.Length&&MenuMotion.ActiveCount==probes.Count(m=>m.IsAnimating),name+" static counters match actual live playback components");
        }
        void SaveFrame(string sequence,int index,float start)
        {
            var shot=ReadFrame(sequence,index,start);if(shot!=null)WriteFrame(shot);
        }
        BufferedFrame ReadFrame(string sequence,int index,float start)
        {
            Canvas.ForceUpdateCanvases();
            var camera=FindObjectOfType<Camera>();var canvas=app.Content.GetComponentInParent<Canvas>().rootCanvas;
            if(!camera||!canvas){Check(false,"render camera and canvas exist for "+sequence);return null;}
            string relative="frames/"+sequence+"/"+index.ToString("D3")+".png";
            string path=Path.Combine(dir,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));
            var frame=new Frame{sequence=sequence,file=relative,elapsed=Time.unscaledTime-start,activeMotions=MenuMotion.ActiveCount,particles=app.Stage.GetComponentsInChildren<MenuGoldSparkles>().Sum(m=>m.ActiveParticles)};
            var entry=app.Content.GetComponentsInChildren<MenuPageEntrance>();frame.entranceProgress=entry.Length>0?entry.Average(m=>m.Progress01):1;
            var press=app.Content.GetComponentsInChildren<MenuPressFeedback>().FirstOrDefault(m=>m.Button&&m.Button.name=="Shop exchange hint");frame.pressScale=press?press.CurrentScale:1;
            frame.progressTotal=app.Content.GetComponentsInChildren<MenuProgressReveal>().Sum(m=>m.Current);
            var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;float distance=canvas.planeDistance;
            var oldTarget=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(Screen.width,Screen.height,24);target.Create();
            camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
            RenderTexture.active=active;camera.targetTexture=oldTarget;canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=distance;
            target.Release();Destroy(target);
            return new BufferedFrame{record=frame,image=image,path=path};
        }
        void WriteFrame(BufferedFrame shot)
        {
            File.WriteAllBytes(shot.path,shot.image.EncodeToPNG());Destroy(shot.image);result.frames.Add(shot.record);
        }
    }
}
#endif

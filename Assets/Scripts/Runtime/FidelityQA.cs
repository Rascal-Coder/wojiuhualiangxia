#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    // Isolated runtime rendering audit, selected only by -inkominoFidelityQA.
    // No screenshots are synthesized: every PNG comes from FidelityCapture's
    // actual camera/uGUI RenderTexture path at1080x2400.
    public sealed class FidelityQA:MonoBehaviour
    {
        const float StepSeconds=1f/60f;
        static readonly int[] Samples={0,50,100,150,200,350,700};
        [Serializable] public sealed class Seed
        {public int level=23,coins=220,avatar=2,hearts=0,hint=0,freeze=3,lens=0,remainingLifeSeconds=867;}
        [Serializable] public sealed class Frame
        {
            public string sequence,file,sha256,trigger;
            public int requestedMs,sequenceFrameIndex,sourceStepsSinceEpoch,globalSourceSteps,pngWidth,pngHeight,desktopWidth,desktopHeight,sampledColors;
            public long bytes;public float elapsedManualMs,sourceClockSeconds,remainingSeconds;
            public float[] stageSize,popupScale,boardVisibleBounds;public float popupAlpha=-1,popupProgress=-1,boardCellSize;
            public bool visiblePixelRange,solved,finished,animationBusy;public int level,pieceCount,drawTraceCells,revealCells;public float drawProgress;
            public Effect[] effects;
        }
        [Serializable] public sealed class Effect
        {public string name;public float[] anchoredPosition,scale;public float angleZ,alpha=-1;public int children;}
        [Serializable] public sealed class Check
        {public string name,detail;public bool passed;}
        [Serializable] public sealed class Sequence
        {public string id,trigger,interpretation;public int[] requestedMs;public int sourceFps=60;public float sourceEpoch;}
        [Serializable] public sealed class Report
        {
            public string status="RUNNING",startedUtc,finishedUtc,unityVersion,platform,outputDirectory;
            public int captureWidth=1080,captureHeight=2400,sourceFps=60,screenshots;
            public bool actualRuntimeRender=true,manualAnimationClock=true,applicationRealtimeClockFrozen=true,engineExecuted;
            public Seed referenceSeed=new Seed();public List<Sequence> sequences=new List<Sequence>();
            public List<Frame> frames=new List<Frame>();public List<Check> checks=new List<Check>();public List<string> errors=new List<string>();
            public string[] limitations={
                "PASS means runtime capture and stated probes passed; it does not certify visual or animation identity with the original application.",
                "The original evidence contains only static Home/Pause frames. No original animation recording is available for a frame-by-frame timing comparison.",
                "App.Update and its initially running coroutines are frozen for this QA instance so realtime desktop timing does not alter the reference HUD. SourceClock animation time still advances by1/60second per yielded Unity frame.",
                "Player data is the isolated QA profile under QAOutput(), not a user save. Level23 uses review/resume solely to stage111seconds without consuming a life.",
                "Piece placement uses public board drawing methods; it is not an operating-system touch injection test. System.Random facet/color choices are not forcibly replaced by a new deterministic implementation.",
                "The full board is staged and settled before the real app.Win() entry for an isolated victory-effect timeline. This separates gold/sweep timing from last-piece pop and is not the natural end-to-end completion timeline.",
                "Booster sequences use real UseBooster entry points on separately reset level23 QA boards. Freeze is captured after activation without waiting for expiry because the QA intentionally freezes the app realtime countdown.",
                "PNG dimensions are the exact RenderTexture output, which may differ from the operating-system constrained desktop window dimensions."
            };
        }
        InkominoApp app;Report report;string dir;int steps;bool finalized,atEndOfFrame;
        void Awake(){Application.logMessageReceived+=Logged;}
        void OnDestroy(){Application.logMessageReceived-=Logged;}
        void Logged(string condition,string trace,LogType type)
        {
            if(report==null||finalized||type!=LogType.Exception&&type!=LogType.Error&&type!=LogType.Assert)return;
            report.errors.Add(type+": "+condition+(type==LogType.Exception?"\n"+trace:""));
        }
        void CheckIt(bool passed,string name,string detail="")
        {report.checks.Add(new Check{name=name,passed=passed,detail=detail});if(!passed)report.errors.Add(name+": "+detail);}
        IEnumerator Start()
        {
            dir=InkominoApp.QAOutput();Directory.CreateDirectory(dir);
            report=new Report{startedUtc=DateTime.UtcNow.ToString("o"),unityVersion=Application.unityVersion,platform=Application.platform.ToString(),outputDirectory=dir,engineExecuted=true};
            // Flatten nested iterators so an exception in a nested capture/stage
            // still writes a useful manifest and quits with a failure code.
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                object yielded=null;bool more=false;Exception failure=null;
                try{more=stack.Peek().MoveNext();if(more)yielded=stack.Peek().Current;}
                catch(Exception e){failure=e;}
                if(failure!=null){report.errors.Add(failure.ToString());Finish();yield break;}
                if(!more){stack.Pop();continue;}
                var nested=yielded as IEnumerator;if(nested!=null){stack.Push(nested);continue;}
                yield return yielded;
            }
            Finish();
        }
        IEnumerator Run()
        {
            app=InkominoApp.Instance;if(!app)throw new InvalidOperationException("InkominoApp.Instance is missing.");
            var oldQA=GetComponent<RuntimeQA>();CheckIt(oldQA==null,"Legacy RuntimeQA not attached","Only the fidelity runner may own this scenario.");
            if(oldQA){oldQA.StopAllCoroutines();oldQA.enabled=false;}
            CheckIt(app.IsQA,"QA save isolation",app.Store.Path);
            if(!app.IsQA)throw new InvalidOperationException("Refusing to stage profile outside IsQA.");
            app.StopAllCoroutines();app.enabled=false;app.SourceTutorialEnabled=false;
            SourceClock.Manual=true;SourceClock.ManualTime=0;SourceClock.ManualDelta=0;
            UnityEngine.Random.InitState(230220);Screen.SetResolution(1080,2400,false);
            app.Store.Data=new PlayerProfile{level=23,coins=220,avatar=2,hearts=0,hint=0,freeze=3,lens=0,regenAt=SaveStore.Now+867,sound=true,music=true,haptics=true};
            app.Store.Save();app.ShowHome();yield return LayoutReady();yield return Advance(42);
            CheckIt(!app.Playing&&app.Store.Data.level==23&&app.Store.Data.hearts==0,"Reference Home seed","Level23,220coins,avatar2,0hearts; life recovery staged14:27.");
            yield return Capture("static","01-home.png",0,SourceClock.Now,0,"ShowHome reference profile");

            app.Store.Data.resumeLevel=23;app.Store.Data.resumeTime=111;app.Store.Data.resumePieces=new PieceData[0];
            app.StartLevel(23,true,true);app.Remaining=111;app.SendMessage("TimerChanged",SendMessageOptions.DontRequireReceiver);
            yield return LayoutReady();
            CheckIt(app.BoardView&&app.Board!=null&&app.CurrentLevel==23,"Level23 staged");
            CheckIt(Mathf.Abs(app.Remaining-111)<.001f,"Reference remaining111s");
            yield return Capture("static","02-level23-empty.png",0,SourceClock.Now,0,"StartLevel(23,review:true,resume:true), remaining111");
            app.Settings(true);float pauseEpoch=SourceClock.Now;
            CheckIt(!app.InputEnabled,"Pause blocks input");
            yield return SequenceFrames("pause-open",pauseEpoch,"Settings(true)","SourcePopupMotion alpha/default timing and scale/spring, original motion specification port.");
            yield return Advance(30); //700ms→1200ms: spring's source settled branch.
            CheckIt(Mathf.Abs(app.Remaining-111)<.001f,"Paused timer remains111s");
            yield return Capture("static","03-pause.png",1200,pauseEpoch,72,"Paused settled frame matching original static evidence state");
            var popup=FindPopup();if(popup){var group=popup.GetComponent<CanvasGroup>();CheckIt(group&&Mathf.Abs(group.alpha-1)<.001f,"Popup final alpha1");CheckIt(Vector3.Distance(popup.transform.localScale,Vector3.one)<.001f,"Popup final scale1");}

            app.ClearPopups();app.StartLevel(1,true);yield return LayoutReady();
            var solution=app.Board.Level.solution;if(solution==null||solution.Length==0)throw new InvalidOperationException("Level1 has no exported solution for the placement probe.");
            var path=Walk(solution[0].cells,app.Board.Level.n);app.BoardView.BeginCell(path[0]);
            foreach(int cell in path.Skip(1))app.BoardView.MoveCell(cell);
            bool placed=app.BoardView.EndStroke();CheckIt(placed&&app.Board.Pieces.Count==1,"Public draw API placed one normal piece","No model bypass for the pop sequence.");
            float popEpoch=SourceClock.Now;
            yield return SequenceFrames("piece-pop",popEpoch,"BeginCell→MoveCell→EndStroke","Ordinary piece appearance, before any victory or pen-animation lock.");
            CheckIt(!app.Board.Solved,"Piece-pop probe is not a win");

            // Suppress only automatic Win dispatch while staging the solved model.
            // Then let every piece settle before calling the actual public Win.
            app.Finished=true;bool restored=app.Board.Restore(solution);yield return Advance(42);
            CheckIt(restored&&app.Board.Solved,"Staged full authored solution");app.Finished=false;
            app.Win();float victoryEpoch=SourceClock.Now;
            CheckIt(app.Finished,"Win entry completes level state");
            yield return SequenceFrames("victory",victoryEpoch,"app.Win() after settled full board","Gold outline300ms; sweep delay220ms and sweep900ms use SourceClock. Last-piece pop deliberately excluded.");
            yield return Advance(318); //700ms→6000ms, includes the independent result title/reward/button sequence.
            yield return Capture("static","04-victory-result.png",6000,victoryEpoch,360,"Independent LevelCompleteScreen after source title/reward/Continue settling");
            CheckIt(app.Finished&&app.Board.Solved,"Final victory state remains solved");

            yield return BoosterScenario(0,"pen",new[]{200,400,800,1900},"Flight, beginning ink, mid-ink, end/fade settled. Actual ink duration depends on exported target perimeter.");
            yield return BoosterScenario(1,"freeze",new[]{150,350,450,600},"Flight, overlay activation, overlay ramp, activated overlay. Expiry deliberately not advanced while App.Update is frozen.");
            yield return BoosterScenario(2,"lens",new[]{200,900,1700,3000},"Flight, board scan, target approach/arrival, final reveal. Actual route segment durations depend on board geometry.");
            app.StopAllCoroutines();app.SourceTutorialEnabled=true;app.Store.Data.drawIntroSeen=false;app.StartLevel(1,true);yield return LayoutReady();float tutorialEpoch=SourceClock.Now;yield return Advance(60);yield return Capture("tutorial","05-tutorial-demo.png",1000,tutorialEpoch,60,"Actual source tutorial demonstration");
            int wait=0;while(app.SourceTutorialPhase!="guide"&&wait++<420)yield return Advance(1);yield return Advance(40);
            CheckIt(app.SourceTutorialPhase=="guide"&&app.Board.Pieces.Count==1&&!app.AnimationBusy,"Tutorial demonstration transitions to guide");yield return Capture("tutorial","06-tutorial-guide.png",Mathf.RoundToInt((SourceClock.Now-tutorialEpoch)*1000),tutorialEpoch,wait+100,"Source guide with one protected demonstration piece");
            var tutorialAnswer=app.Board.Solve(60000);if(tutorialAnswer!=null&&tutorialAnswer.Count>0){var guidedPath=Walk(tutorialAnswer[0].cells,app.Board.Level.n);app.BoardView.BeginCell(guidedPath[0]);foreach(int c in guidedPath.Skip(1))app.BoardView.MoveCell(c);app.BoardView.EndStroke();yield return Advance(50);CheckIt(app.Board.Pieces.Count>=2,"Tutorial accepts guided shape");}
            yield return Capture("tutorial","07-tutorial-user.png",Mathf.RoundToInt((SourceClock.Now-tutorialEpoch)*1000),tutorialEpoch,wait+150,"Real accepted tutorial gesture");app.SourceTutorialEnabled=false;
            CheckIt(report.frames.Count==40,"Expected capture count","4static +21popup/pop/victory +12booster +3tutorial frames =40.");
        }
        IEnumerator LayoutReady()
        {SourceClock.ManualDelta=0;atEndOfFrame=false;Canvas.ForceUpdateCanvases();yield return null;Canvas.ForceUpdateCanvases();yield return null;}
        IEnumerator Advance(int frames)
        {
            // Finish the tick at render-frame end. Capture can immediately render
            // that same pose, without an extra zero-delta Unity update resetting
            // velocity-dependent lens rotation between samples.
            for(int i=0;i<frames;i++){atEndOfFrame=false;SourceClock.Step(StepSeconds);steps++;yield return new WaitForEndOfFrame();atEndOfFrame=true;SourceClock.ManualDelta=0;}
        }
        IEnumerator BoosterScenario(int index,string name,int[] samples,string interpretation)
        {
            // Retire the previous scenario's app-owned routines; the new genuine
            // UseBooster invocation starts its own normal SourceClock routines.
            app.StopAllCoroutines();app.Store.Data.hint=app.Store.Data.freeze=app.Store.Data.lens=3;
            app.StartLevel(23,true);app.Remaining=111;app.SendMessage("TimerChanged",SendMessageOptions.DontRequireReceiver);yield return LayoutReady();
            CheckIt(app.InputEnabled,"Booster "+name+" entry input enabled");
            if(!atEndOfFrame){yield return new WaitForEndOfFrame();atEndOfFrame=true;}
            app.UseBooster(index);float epoch=SourceClock.Now;
            int after=index==0?app.Store.Data.hint:index==1?app.Store.Data.freeze:app.Store.Data.lens;
            CheckIt(after==2,"Booster "+name+" inventory consumed exactly one");
            yield return SequenceFrames("booster-"+name,epoch,"UseBooster("+index+")",interpretation,samples);
            if(index==0)CheckIt(app.Board.Pieces.Count==1&&!app.AnimationBusy,"Pen completed one actual placement");
            if(index==1)CheckIt(ActiveEffects().Any(x=>x.name=="Source freeze edge"),"Freeze activated real overlay");
            if(index==2)CheckIt(app.BoardView.Reveal!=null&&app.BoardView.Reveal.Length>0&&!app.AnimationBusy,"Lens completed actual final reveal");
        }
        IEnumerator SequenceFrames(string id,float epoch,string trigger,string interpretation,int[] customSamples=null)
        {
            int[] samples=customSamples??Samples;
            report.sequences.Add(new Sequence{id=id,trigger=trigger,interpretation=interpretation,requestedMs=(int[])samples.Clone(),sourceEpoch=epoch});
            int frame=0;
            for(int i=0;i<samples.Length;i++)
            {
                int target=Mathf.RoundToInt(samples[i]*.06f);yield return Advance(target-frame);frame=target;
                yield return Capture(id,"frames/"+id+"/frame-"+samples[i].ToString("0000")+"ms.png",samples[i],epoch,frame,trigger,i);
            }
        }
        RectTransform[] ActiveEffects()
        {
            string[] names={"Source pen tip","Source freeze flight","Source freeze edge","Source lens center","Source lens arrival ring"};
            return app.Stage.GetComponentsInChildren<RectTransform>(false).Where(x=>names.Contains(x.name)||x.name.StartsWith("Source revealed ")).ToArray();
        }
        SourcePopupMotion FindPopup()
        {return app.Overlays.GetComponentsInChildren<SourcePopupMotion>(false).LastOrDefault();}
        IEnumerator Capture(string sequence,string file,int requestedMs,float epoch,int sequenceSteps,string trigger,int index=0)
        {
            SourceClock.ManualDelta=0;Canvas.ForceUpdateCanvases();if(!atEndOfFrame){yield return new WaitForEndOfFrame();atEndOfFrame=true;}
            string path=Path.Combine(dir,file.Replace('/',Path.DirectorySeparatorChar));FidelityCapture.Save(path,1080,2400);
            byte[] png=File.ReadAllBytes(path);var decoded=new Texture2D(2,2,TextureFormat.RGB24,false);
            bool loaded=decoded.LoadImage(png,false);var pixels=loaded?decoded.GetPixels32():new Color32[0];
            var colors=new HashSet<uint>();int min=255,max=0;
            for(int i=0;i<pixels.Length;i+=Mathf.Max(1,pixels.Length/4096))
            {var c=pixels[i];int l=(c.r+c.g+c.b)/3;min=Mathf.Min(min,l);max=Mathf.Max(max,l);colors.Add((uint)(c.r<<16|c.g<<8|c.b));}
            // The legitimate0ms popup frame has an85% black backdrop and a fully
            // transparent panel, so its useful luma span is only about30levels.
            var f=new Frame{sequence=sequence,file=file,requestedMs=requestedMs,sequenceFrameIndex=index,sourceStepsSinceEpoch=sequenceSteps,globalSourceSteps=steps,elapsedManualMs=(SourceClock.Now-epoch)*1000,sourceClockSeconds=SourceClock.Now,pngWidth=decoded.width,pngHeight=decoded.height,desktopWidth=Screen.width,desktopHeight=Screen.height,bytes=png.LongLength,sampledColors=colors.Count,visiblePixelRange=max-min>18&&colors.Count>16,remainingSeconds=app.Remaining,level=app.Playing?app.CurrentLevel:app.Store.Data.level,pieceCount=app.Board==null?0:app.Board.Pieces.Count,solved=app.Board!=null&&app.Board.Solved,finished=app.Finished,trigger=trigger,stageSize=new[]{app.Stage.rect.width,app.Stage.rect.height}};
            using(var sha=SHA256.Create())f.sha256=BitConverter.ToString(sha.ComputeHash(png)).Replace("-","").ToLowerInvariant();
            var popup=FindPopup();if(popup){var g=popup.GetComponent<CanvasGroup>();f.popupAlpha=g?g.alpha:-1;f.popupProgress=popup.Progress;var s=popup.transform.localScale;f.popupScale=new[]{s.x,s.y,s.z};}
            f.animationBusy=app.AnimationBusy;
            f.effects=ActiveEffects().Select(x=>{var group=x.GetComponent<CanvasGroup>();return new Effect{name=x.name,anchoredPosition=new[]{x.anchoredPosition.x,x.anchoredPosition.y},scale=new[]{x.localScale.x,x.localScale.y,x.localScale.z},angleZ=x.localEulerAngles.z,alpha=group?group.alpha:-1,children=x.childCount};}).ToArray();
            if(app.BoardView){Rect b=app.BoardView.VisibleBoundsLocal;f.boardVisibleBounds=new[]{b.x,b.y,b.width,b.height};f.boardCellSize=app.BoardView.CellSize;f.drawProgress=app.BoardView.DrawProgress;f.drawTraceCells=app.BoardView.DrawTrace==null?0:app.BoardView.DrawTrace.Length;f.revealCells=app.BoardView.Reveal==null?0:app.BoardView.Reveal.Length;}
            report.frames.Add(f);Destroy(decoded);
            CheckIt(loaded&&f.pngWidth==1080&&f.pngHeight==2400,"Exact1080x2400 "+file);
            CheckIt(f.visiblePixelRange,"Nonblank real render "+file,"Sampled colors="+f.sampledColors+", luma range="+(max-min));
            CheckIt(Mathf.Abs(f.elapsedManualMs-requestedMs)<(sequence=="tutorial"?.6f:.15f)||sequence=="static"&&requestedMs==0,"Manual timestamp "+file,"actual="+f.elapsedManualMs.ToString("F4")+"ms requested="+requestedMs);
            Write();
        }
        void Write()
        {report.screenshots=report.frames.Count;File.WriteAllText(Path.Combine(dir,"fidelity-validation.json"),JsonUtility.ToJson(report,true));}
        void Finish()
        {
            if(finalized||report==null)return;report.finishedUtc=DateTime.UtcNow.ToString("o");report.status=report.errors.Count==0?"PASS":"FAIL";finalized=true;
            try{Write();Debug.Log("INKOMINO_FIDELITY_QA "+report.status+" frames="+report.frames.Count+" manifest="+Path.Combine(dir,"fidelity-validation.json"));}
            catch(Exception e){Debug.LogError("Fidelity manifest write failed: "+e);}
            SourceClock.ManualDelta=0;Application.Quit(report.errors.Count==0?0:1);
        }
        static List<int> Walk(int[] cells,int n)
        {
            var set=new HashSet<int>(cells);var visited=new HashSet<int>();var path=new List<int>();Action<int> walk=null;
            walk=i=>{visited.Add(i);path.Add(i);foreach(int j in new[]{i-n,i+n,i-1,i+1})if(set.Contains(j)&&!visited.Contains(j)&&Math.Abs(j/n-i/n)+Math.Abs(j%n-i%n)==1){walk(j);path.Add(i);}};
            walk(cells[0]);return path;
        }
    }
}

#endif

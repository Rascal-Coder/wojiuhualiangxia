#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Inkomino
{
    // Opt-in desktop runner. The real startup uses an isolated, initially absent
    // qa-profile.json; no production profile or PlayerPrefs key is reset.
    public sealed class TutorialQA:MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name;public bool passed; }
        [Serializable] public sealed class Report
        {
            public string status="RUNNING",engine,method="Actual SourceStartup with isolated empty profile, real tutorial gestures and saved-profile reloads. Windows runtime; not WeChat device execution.";
            public List<Check> checks=new List<Check>();public List<string> errors=new List<string>();
        }
        Report report=new Report();InkominoApp app;string dir;
        void CheckIt(bool ok,string name){report.checks.Add(new Check{name=name,passed=ok});if(!ok)report.errors.Add(name);}
        void Logged(string message,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error)report.errors.Add(message);}
        IEnumerator Start()
        {
            app=InkominoApp.Instance;dir=InkominoApp.QAOutput();Directory.CreateDirectory(dir);report.engine=Application.unityVersion;
            Application.logMessageReceived+=Logged;AudioListener.volume=0;
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                object next=null;bool more=false;Exception error=null;
                try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){report.errors.Add(error.ToString());break;}
                if(!more){stack.Pop();continue;}
                var nested=next as IEnumerator;if(nested!=null)stack.Push(nested);else yield return next;
            }
            report.status=report.errors.Count==0?"PASS":"FAIL";
            File.WriteAllText(Path.Combine(dir,"tutorial-validation.json"),JsonUtility.ToJson(report,true));
            Debug.Log("INKOMINO_TUTORIAL_QA "+report.status+" checks="+report.checks.Count);
            Application.logMessageReceived-=Logged;Application.Quit(report.errors.Count==0?0:1);
        }
        IEnumerator Run()
        {
            CheckIt(app.IsQA,"isolated QA storage");if(!app.IsQA)yield break;
            CheckIt(!File.Exists(Path.Combine(dir,"qa-profile.json")),"profile absent at actual startup");
            float deadline=Time.realtimeSinceStartup+40;
            while(!app.BootReady&&Time.realtimeSinceStartup<deadline)yield return null;
            CheckIt(app.BootReady,"real loading completed");if(!app.BootReady)yield break;
            app.enabled=false;SourceClock.Manual=true;SourceClock.ManualTime=Time.unscaledTime;SourceClock.ManualDelta=0;
            CheckIt(app.Playing&&app.CurrentLevel==1&&!app.Review,"empty startup directly opens level one");
            CheckIt(app.SourceTutorialPhase=="demo"&&!app.Store.Data.drawIntroSeen,"fresh startup begins unseen demo");
            CheckIt(app.AnimationBusy&&!app.InputEnabled,"demo blocks player input");
            yield return Advance(65);yield return Capture("01-fresh-demo.png");
            int wait=0;while(app.Board.Pieces.Count==0&&wait++<240)yield return Advance(1);
            CheckIt(app.Board.Pieces.Count==1&&!app.Store.Data.drawIntroSeen,"demo places piece before seen flag");
            app.SaveBoard();app.Store.Save();
            var reloaded=new SaveStore(app.Store.Path);
            CheckIt(!reloaded.Data.drawIntroSeen&&reloaded.Data.resumeLevel==0&&(reloaded.Data.resumePieces==null||reloaded.Data.resumePieces.Length==0),"interrupted demo does not save temporary piece");
            app.ShowHome();app.Store.Data=reloaded.Data;app.ShowStartupDestination();
            CheckIt(app.SourceTutorialPhase=="demo"&&app.Board.Pieces.Count==0,"interrupted unseen demo starts clean on reentry");
            // Also exercise a stale half-demo saved by the earlier reconstruction.
            app.ShowHome();app.Store.Data.resumeLevel=1;app.Store.Data.resumeTime=12;
            app.Store.Data.resumePieces=new[]{app.Data.levels[0].solution[0]};app.Store.Save();
            app.StartLevel(1,false,true);
            CheckIt(app.Board.Pieces.Count==0&&app.SourceTutorialPhase=="demo","legacy partial demo cannot bypass tutorial");
            CheckIt(app.Store.Data.resumePieces==null&&app.Remaining==120,"stale demo snapshot cleared without carrying timer");
            wait=0;while(app.SourceTutorialPhase!="guide"&&wait++<400)yield return Advance(1);
            CheckIt(app.SourceTutorialPhase=="guide"&&app.Board.Pieces.Count==1,"automatic demo reaches guide");
            CheckIt(app.Store.Data.drawIntroSeen&&new SaveStore(app.Store.Path).Data.drawIntroSeen,"seen flag persists at original demo-to-guide boundary");
            CheckIt(!app.AnimationBusy&&app.InputEnabled,"guide enables player drawing");
            yield return Advance(40);yield return Capture("02-follow-hand.png");
            var guide=app.Board.Level.solution[1];var last=app.Board.Level.solution[2];
            CheckIt(!app.BoardView.StrokeAllowed(last.cells)&&app.BoardView.StrokeAllowed(guide.cells),"guide accepts only highlighted shape");
            Draw(guide.cells);yield return Advance(45);
            CheckIt(app.SourceTutorialPhase=="user"&&app.Board.Pieces.Count==2,"actual guided gesture advances to independent drawing");
            yield return Capture("03-draw-yourself.png");
            app.SaveBoard();reloaded=new SaveStore(app.Store.Path);
            CheckIt(reloaded.Data.drawIntroSeen&&reloaded.Data.resumePieces.Length==2,"player progress resumes after completed demo");
            app.ShowHome();app.Store.Data=reloaded.Data;app.ShowStartupDestination();
            CheckIt(app.Playing&&app.Board.Pieces.Count==2&&app.SourceTutorialPhase==null,"relaunch keeps player pieces without repeating seen demo");
            Draw(last.cells);yield return Advance(2);
            CheckIt(app.Board.Solved&&app.Finished&&app.Store.HasCompletedLevel,"first tutorial level can be completed");
            app.ShowHome();app.ShowStartupDestination();
            CheckIt(!app.Playing&&app.Store.Data.level==2,"completed player starts on home");
            app.Store.Data=new PlayerProfile{level=23,coins=1234,drawIntroSeen=true};app.ShowStartupDestination();
            CheckIt(!app.Playing&&app.Store.Data.level==23&&app.Store.Data.coins==1234,"returning progress preserved");
        }
        IEnumerator Advance(int frames){for(int i=0;i<frames;i++){SourceClock.Step(1f/60);yield return new WaitForEndOfFrame();SourceClock.ManualDelta=0;}}
        IEnumerator Capture(string file){yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();FidelityCapture.Save(Path.Combine(dir,file),1080,2400);CheckIt(new FileInfo(Path.Combine(dir,file)).Length>10000,"real rendered screenshot "+file);}
        void Draw(int[] cells)
        {
            var walk=(int[])typeof(InkominoApp).GetMethod("TutorialWalk",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{cells,app.Board.Level.n});
            app.BoardView.BeginCell(walk[0]);foreach(int i in walk.Skip(1))app.BoardView.MoveCell(i);app.BoardView.EndStroke();
        }
    }
}
#endif

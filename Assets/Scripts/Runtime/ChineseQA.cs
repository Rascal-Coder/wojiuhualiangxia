#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    // Explicit -inkominoChineseQA runner. All screenshots use the real camera,
    // canvas and RenderTexture path; this runner never synthesizes a UI image.
    public sealed partial class ChineseQA:MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name,detail;public bool passed; }
        [Serializable] public sealed class VisibleText
        { public string value,font;public int instances,fontSize;public string missingCharacters; }
        [Serializable] public sealed class Frame
        {
            public string file,sha256,screen;public int width,height,visibleTextInstances;
            public long bytes;public bool visiblePixels;public List<VisibleText> texts=new List<VisibleText>();
            public List<string> visibleImages=new List<string>();
        }
        [Serializable] public sealed class Report
        {
            public string status="RUNNING",startedUtc,finishedUtc,engine,outputDirectory;
            public bool actualRuntimeRender=true,isolatedQaProfile,defaultEngineSplashExcludedFromCanvasCapture=true;
            public int screenshots;public List<Frame> frames=new List<Frame>();
            public List<Check> checks=new List<Check>();public List<string> errors=new List<string>();
            public string method="真实启动加载流程与 uGUI RenderTexture 截图；仅扫描当前有效且透明度可见的 Text，按文字和字体去重。此检查不声称与原作逐像素一致。引擎默认 Splash 在脚本启动之前，由 PlayerSettings 保留，不能用本运行时 Canvas 截图验证。";
        }
        readonly Regex english=new Regex("[A-Za-z]{2,}",RegexOptions.CultureInvariant);
        InkominoApp app;Report report;string dir;bool finalized;
        void Awake(){Application.logMessageReceived+=Logged;}
        void OnDestroy(){Application.logMessageReceived-=Logged;}
        void Logged(string message,string trace,LogType type)
        {if(report!=null&&!finalized&&(type==LogType.Exception||type==LogType.Error||type==LogType.Assert))report.errors.Add(type+": "+message+(type==LogType.Exception?"\n"+trace:""));}
        void CheckIt(bool value,string name,string detail="")
        {report.checks.Add(new Check{name=name,passed=value,detail=detail});if(!value)report.errors.Add(name+": "+detail);}
        IEnumerator Start()
        {
            dir=InkominoApp.QAOutput();Directory.CreateDirectory(dir);
            report=new Report{startedUtc=DateTime.UtcNow.ToString("o"),engine=Application.unityVersion,outputDirectory=dir};
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                bool more=false;object next=null;Exception failure=null;
                try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){failure=e;}
                if(failure!=null){report.errors.Add(failure.ToString());Finish();yield break;}
                if(!more){stack.Pop();continue;}
                var nested=next as IEnumerator;if(nested!=null){stack.Push(nested);continue;}
                yield return next;
            }
            Finish();
        }
        IEnumerator Run()
        {
            app=InkominoApp.Instance;if(!app)throw new InvalidOperationException("Missing app instance.");
            report.isolatedQaProfile=app.IsQA;CheckIt(app.IsQA,"使用隔离测试存档");
            if(!app.IsQA)throw new InvalidOperationException("Refusing to change a non-QA profile.");
            float deadline=Time.realtimeSinceStartup+20;
            while(!app.LoadingView&&Time.realtimeSinceStartup<deadline)yield return null;
            CheckIt(app.LoadingView,"真实加载页已创建");if(!app.LoadingView)yield break;
            CheckIt(app.LoadingView.HoldForQA,"加载流程由 QA 暂停在真实加载页");
            yield return Capture("01-loading-1080x2400.png","启动加载",1080,2400);
            var firstLabel=app.LoadingView.Label.text;
            deadline=Time.realtimeSinceStartup+2;
            while(app.LoadingView&&app.LoadingView.Label.text==firstLabel&&Time.realtimeSinceStartup<deadline)yield return null;
            CheckIt(app.LoadingView&&app.LoadingView.Label.text!=firstLabel,"加载中文字后的圆点真实变化");
            yield return Capture("02-loading-540x836.png","启动加载小窗口",540,836);
            CheckIt(app.LoadingView.Logo&&app.LoadingView.Logo.gameObject.name=="Sticker game brand","加载页使用多巴胺贴纸标志");
            CheckIt(Application.productName==Zh.GameName&&Zh.GameName=="我就划两下","工程显示名称与游戏名一致",Application.productName);
            var brandTexts=app.LoadingView.Logo.GetComponentsInChildren<Text>();
            CheckIt(brandTexts.Any(t=>t.text==Zh.GameName&&t.font==Skin.ChineseFont),"标题使用真实圆趣字体并完整显示五字游戏名");
            var loadTexts=app.LoadingView.GetComponentsInChildren<Text>().Where(Visible).Select(t=>t.text).ToArray();
            CheckIt(loadTexts.Any(t=>t.StartsWith("加载中",StringComparison.Ordinal)),"加载页显示真实加载提示");

            CheckIt(loadTexts.All(t=>!t.Contains("%")&&!t.Contains("广告")),"加载页无伪造百分比或广告文案");
            if(Environment.GetCommandLineArgs().Contains("-brandingOnly"))yield break;
            app.LoadingView.HoldForQA=false;deadline=Time.realtimeSinceStartup+20;
            while(!app.BootReady&&Time.realtimeSinceStartup<deadline)yield return null;
            CheckIt(app.BootReady,"加载完成并淡出进入启动目标界面");if(!app.BootReady)yield break;
            CheckIt(app.Store!=null&&app.Data.levels.Length==500,"真实启动已加载存档及 500 关");
            if(Environment.GetCommandLineArgs().Contains("-monetizationOnly")){yield return RunMonetization();yield break;}
            app.StopAllCoroutines();app.enabled=false;app.SourceTutorialEnabled=false;
            SourceClock.Manual=true;SourceClock.ManualTime=Time.unscaledTime;SourceClock.ManualDelta=0;
            app.Store.Data=new PlayerProfile{level=23,coins=1200,avatar=2,hearts=5,hint=3,freeze=3,lens=3,regenAt=SaveStore.Now+900,sound=true,music=true,haptics=true};app.Store.Save();
            app.ShowHome();yield return Settle();yield return Capture("03-home.png","主页");
            app.Settings(false);yield return Settle();yield return Capture("04-settings.png","设置");
            app.ClearPopups();Invoke("LifePopup");yield return Settle();yield return Capture("05-lives-full.png","生命已满");
            app.ClearPopups();app.Store.Data.hearts=0;app.Store.Data.regenAt=SaveStore.Now+867;app.ShowHome();Invoke("LifePopup");yield return Settle();yield return Capture("06-lives-empty.png","恢复生命");
            app.ClearPopups();app.Store.Data.hearts=5;app.ShowShop();yield return Settle();yield return Capture("07-shop.png","金币兑换道具");
            app.ShowCollection();yield return Settle();yield return Capture("08-collection.png","成就");
            app.LevelLibrary(0);yield return Settle();yield return Capture("09-library-first.png","关卡目录第一页");
            app.ClearPopups();app.LevelLibrary(19);yield return Settle();yield return Capture("10-library-last.png","关卡目录末页");
            app.ShowHome();Invoke("Profile");yield return Settle();yield return Capture("11-profile.png","个人资料");
            app.ClearPopups();app.Store.Data.dailyAt=0;Invoke("DailyReward");yield return Settle();yield return Capture("12-daily-gift.png","免费每日礼物");
            app.ClearPopups();app.StartLevel(23,true);yield return Settle();yield return Capture("13-level23.png","第23关");
            app.Settings(true);yield return Settle();yield return Capture("14-pause.png","关卡暂停");
            app.ClearPopups();Invoke("BoosterShop",0);yield return Settle();yield return Capture("15-booster-shop.png","补充道具");
            app.ClearPopups();app.Settings(true);Invoke("LeavePrompt",true);yield return Settle();yield return Capture("16-restart.png","重新开始确认");
            CheckConfirmationClearance();
            yield return Capture("25-restart-small.png","重开确认小窗口",540,836);
            yield return Capture("26-restart-narrow.png","重开确认窄屏",360,800);
            app.ClearPopups();app.Settings(true);Invoke("LeavePrompt",false);yield return Settle();CheckConfirmationClearance();
            yield return Capture("27-exit.png","退出确认");
            yield return Capture("28-exit-small.png","退出确认小窗口",540,836);
            app.ClearPopups();app.Store.Data.firstContinueUsed=false;app.StartLevel(23,false);app.Remaining=0;app.Fail();yield return Settle();yield return Capture("17-failure-free.png","首次免费继续");
            var free=FindButton(Zh.Free);CheckIt(free,"免费继续按钮存在");if(free){free.onClick.Invoke();CheckIt(!app.Finished&&app.Remaining>=60&&app.Store.Data.firstContinueUsed,"免费继续确实增加60秒");}
            app.Remaining=0;app.Fail();yield return Settle();yield return Capture("18-failure-coins.png","金币继续挑战");
            var paid=FindButton(Zh.PlayOn);CheckIt(paid,"金币继续按钮存在");int beforeCoins=app.Store.Data.coins;
            if(paid){paid.onClick.Invoke();CheckIt(!app.Finished&&app.Remaining>=60&&app.Store.Data.coins==beforeCoins-120,"再次继续仅消耗120游戏金币");}
            app.ClearPopups();app.Store.Data.resumeLevel=23;app.Store.Data.resumeTime=98;app.Store.Data.resumePieces=new PieceData[0];app.ShowHome();Invoke("CoreContinuePrompt");yield return Settle();yield return Capture("19-continue.png","继续上次进度");
            app.ClearPopups();app.StartLevel(11,true);app.Finished=true;bool restored=app.Board.Restore(app.Board.Level.solution);CheckIt(restored&&app.Board.Solved,"完整导出解可还原胜利棋盘");yield return Advance(42);app.Finished=false;app.Win();yield return Advance(420);yield return Capture("20-result.png","通关奖励与弧形中文标题");
            app.StopAllCoroutines();app.SourceTutorialEnabled=true;app.Store.Data.drawIntroSeen=false;app.StartLevel(1,true);yield return Advance(60);yield return Capture("21-tutorial-demo.png","新手教学演示");
            int wait=0;while(app.SourceTutorialPhase!="guide"&&wait++<480)yield return Advance(1);
            yield return Advance(36);CheckIt(app.SourceTutorialPhase=="guide","新手教学进入手势引导");yield return Capture("22-tutorial-guide.png","新手教学轮到你");
            var answer=app.Board.Solve(60000);if(answer!=null&&answer.Count>0){var path=Walk(answer[0].cells,app.Board.Level.n);app.BoardView.BeginCell(path[0]);foreach(int cell in path.Skip(1))app.BoardView.MoveCell(cell);app.BoardView.EndStroke();yield return Advance(50);}
            yield return Capture("23-tutorial-user.png","新手教学独立尝试");
            app.StopAllCoroutines();app.SourceTutorialEnabled=false;app.StartLevel(23,true);yield return Settle();
            app.EnterLevelWithLoading(24,true);yield return Advance(70);
            CheckIt(app.TransitioningLevel&&!app.InputEnabled&&app.GameplayLoadingView,"进入关卡的过渡画面阻止玩法输入");
            yield return Capture("24-gameplay-loading.png","关卡加载过渡");
            yield return Advance(160);
            CheckIt(app.CurrentLevel==24&&!app.TransitioningLevel&&!app.GameplayLoadingView&&app.InputEnabled,"关卡加载过渡结束并进入第24关");
            TestRewardInterface();
            CheckIt(report.frames.Count==28,"预期28张真实截图",report.frames.Count.ToString());
        }
        void CheckConfirmationClearance()
        {
            var warning=app.Overlays.GetComponentsInChildren<Text>().Last(t=>t.gameObject.activeInHierarchy&&t.text==Zh.ProgressLost);
            var panel=(RectTransform)warning.transform.parent;var corners=new Vector3[4];warning.rectTransform.GetWorldCorners(corners);
            float textBottom=corners.Min(c=>panel.InverseTransformPoint(c).y);
            var actions=panel.GetComponentsInChildren<Button>().Where(b=>b.GetComponentsInChildren<Text>().Any(t=>t.text==Zh.Retry||t.text==Zh.Resume||t.text==Zh.Cancel||t.text==Zh.Exit)).ToArray();
            CheckIt(actions.Length==2,"确认弹窗保留两个操作按钮");
            foreach(var action in actions){((RectTransform)action.transform).GetWorldCorners(corners);float top=corners.Max(c=>panel.InverseTransformPoint(c).y),gap=(textBottom-top)/Skin.DP;CheckIt(gap>=23.9f,"提示文字与按钮完整分离",action.name+" gapDp="+gap.ToString("F2"));}
        }
        void Invoke(string method,object argument=null)
        {
            var info=typeof(InkominoApp).GetMethods(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).FirstOrDefault(m=>m.Name==method&&m.GetParameters().Length==(argument==null?0:1));
            if(info==null)throw new MissingMethodException(method);info.Invoke(app,argument==null?null:new[]{argument});
        }
        Button FindButton(string value)
        {return app.Overlays.GetComponentsInChildren<Button>().LastOrDefault(b=>b.gameObject.activeInHierarchy&&b.GetComponentsInChildren<Text>().Any(t=>t.text==value));}
        IEnumerator Settle(){yield return null;Canvas.ForceUpdateCanvases();yield return Advance(75);}
        IEnumerator Advance(int count)
        {for(int i=0;i<count;i++){SourceClock.Step(1f/60);yield return new WaitForEndOfFrame();SourceClock.ManualDelta=0;}}
        static bool Visible(Graphic graphic)
        {
            if(!graphic||!graphic.enabled||!graphic.gameObject.activeInHierarchy||graphic.color.a<=.02f)return false;
            float alpha=graphic.color.a;foreach(var group in graphic.GetComponentsInParent<CanvasGroup>()){alpha*=group.alpha;if(group.ignoreParentGroups)break;}
            return alpha>.02f&&graphic.rectTransform.rect.width>0&&graphic.rectTransform.rect.height>0;
        }
        void Scan(Frame frame,string phase)
        {
            bool record=phase=="截图前";
            foreach(var text in app.Stage.GetComponentsInChildren<Text>().Where(Visible))
            {
                string value=text.text;if(string.IsNullOrWhiteSpace(value))continue;if(record)frame.visibleTextInstances++;
                CheckText(!english.IsMatch(value),frame.screen+" "+phase+" 英文漏译: "+value);
                var missing=new string(value.Where(c=>c>=0x2e80&&c<=0xffef&&(text.font==null||!text.font.HasCharacter(c))).Distinct().ToArray());
                CheckText(missing.Length==0,frame.screen+" "+phase+" 缺字: "+value+" / "+missing);
                if(!record)continue;string font=text.font?text.font.name:"(null)";var entry=frame.texts.FirstOrDefault(x=>x.value==value&&x.font==font);
                if(entry==null)frame.texts.Add(new VisibleText{value=value,font=font,fontSize=text.fontSize,instances=1,missingCharacters=missing});else entry.instances++;
            }
            foreach(var image in app.Stage.GetComponentsInChildren<Image>().Where(Visible))
            {
                string source=image.gameObject.name+" | "+(image.sprite?image.sprite.name+" | "+image.sprite.texture.name:"");
                if(!frame.visibleImages.Contains(source))frame.visibleImages.Add(source);
                CheckText(source.IndexOf("noadsicon",StringComparison.OrdinalIgnoreCase)<0&&source.IndexOf("welcomepackicon",StringComparison.OrdinalIgnoreCase)<0&&source.IndexOf("zh_publisher_logo",StringComparison.OrdinalIgnoreCase)<0,frame.screen+" "+phase+" 出现已移除促销/发行商图: "+source);
            }
        }
        void CheckText(bool passed,string error){if(!passed&&!report.errors.Contains(error))report.errors.Add(error);}
        IEnumerator Capture(string file,string screen,int width=1080,int height=2400)
        {
            Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
            var frame=new Frame{file=file,screen=screen,width=width,height=height};Scan(frame,"截图前");
            string path=Path.Combine(dir,file);FidelityCapture.Save(path,width,height);byte[] png=File.ReadAllBytes(path);
            frame.bytes=png.Length;using(var sha=SHA256.Create())frame.sha256=BitConverter.ToString(sha.ComputeHash(png)).Replace("-","").ToLowerInvariant();
            var image=new Texture2D(2,2,TextureFormat.RGB24,false);image.LoadImage(png);CheckIt(image.width==width&&image.height==height,screen+" 截图尺寸",image.width+"x"+image.height);
            var colors=image.GetPixels32();frame.visiblePixels=colors.Any(c=>c.r>100&&c.g>70)&&colors.Where((c,i)=>i%113==0).Select(c=>(c.r/8)+","+(c.g/8)+","+(c.b/8)).Distinct().Count()>12;
            CheckIt(frame.visiblePixels,screen+" 真实截图具有可见色彩变化");Destroy(image);
            // Scan both sides of the real capture to catch late font invalidation.
            Scan(frame,"截图后");report.frames.Add(frame);report.screenshots=report.frames.Count;Write();
        }
        sealed class FakeProvider:IRewardedAdProvider
        {
            public RewardedOutcome result;public bool throws,hold,repeat;public Action<RewardedOutcome> pending;
            public bool IsAvailable(RewardPlacement placement){return true;}
            public void Show(RewardPlacement placement,Action<RewardedOutcome> closed){if(throws)throw new InvalidOperationException("QA provider failure");if(hold){pending=closed;return;}closed(result);if(repeat)closed(result);}
        }
        void TestRewardInterface()
        {
            int grants=0;RewardedOutcome outcome=RewardedOutcome.Completed;
            try
            {
                RewardedAds.Register(null);RewardedAds.ShowForReward(RewardPlacement.Pen,()=>grants++,v=>outcome=v);CheckIt(grants==0&&outcome==RewardedOutcome.Unavailable,"广告无提供方时不发奖");
                RewardedAds.Register(new FakeProvider{result=RewardedOutcome.Skipped});RewardedAds.ShowForReward(RewardPlacement.Pen,()=>grants++,v=>outcome=v);CheckIt(grants==0&&outcome==RewardedOutcome.Skipped,"跳过广告不发奖");
                RewardedAds.Register(new FakeProvider{result=RewardedOutcome.Completed,repeat=true});RewardedAds.ShowForReward(RewardPlacement.Pen,()=>grants++,v=>outcome=v);CheckIt(grants==1&&outcome==RewardedOutcome.Completed,"完成且重复回调只发一次奖");
                RewardedAds.Register(new FakeProvider{throws=true});RewardedAds.ShowForReward(RewardPlacement.Pen,()=>grants++,v=>outcome=v);CheckIt(grants==1&&outcome==RewardedOutcome.Failed&&!RewardedAds.IsShowing,"提供方异常不发奖并解除忙碌");
                var hold=new FakeProvider{hold=true};RewardedAds.Register(hold);CheckIt(app.InputEnabled,"广告接口并发测试前玩法可输入");RewardedAds.ShowForReward(RewardPlacement.Freeze,()=>grants++);
                CheckIt(RewardedAds.IsShowing&&!app.InputEnabled,"预留广告播放时冻结玩法");RewardedAds.ShowForReward(RewardPlacement.Lens,()=>grants++,v=>outcome=v);CheckIt(outcome==RewardedOutcome.Busy&&grants==1,"忙碌时拒绝并发且不发奖");
                hold.pending(RewardedOutcome.Skipped);CheckIt(!RewardedAds.IsShowing&&app.InputEnabled,"广告关闭恢复玩法");
            }
            finally{RewardedAds.Register(null);}
        }
        static List<int> Walk(int[] cells,int n)
        {
            var set=new HashSet<int>(cells);var seen=new HashSet<int>();var path=new List<int>();Action<int> dfs=null;
            dfs=i=>{seen.Add(i);path.Add(i);foreach(int j in new[]{i-n,i+n,i-1,i+1})if(set.Contains(j)&&!seen.Contains(j)&&Math.Abs(j/n-i/n)+Math.Abs(j%n-i%n)==1){dfs(j);path.Add(i);}};
            dfs(cells[0]);return path;
        }
        void Write(){File.WriteAllText(Path.Combine(dir,"chinese-validation.json"),JsonUtility.ToJson(report,true));}
        void Finish()
        {
            if(finalized)return;finalized=true;SourceClock.Manual=false;
            report.finishedUtc=DateTime.UtcNow.ToString("o");report.status=report.errors.Count==0?"PASS":"FAIL";Write();
            Debug.Log("INKOMINO_CHINESE_QA "+report.status+" screenshots="+report.screenshots+" errors="+report.errors.Count);Application.Quit(report.errors.Count==0?0:1);
        }
    }
}

#endif

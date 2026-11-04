#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.IO;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Inkomino
{
    public class RuntimeQA:MonoBehaviour
    {
        List<string> errors=new List<string>();int screenshots;string dir;
        void Check(bool condition,string reason){if(!condition){errors.Add(reason);Debug.LogError("QA "+reason);}}
        IEnumerator Capture(string name,float wait=1.1f){yield return new WaitForSecondsRealtime(wait);yield return null;yield return new WaitForEndOfFrame();var camera=UnityEngine.Object.FindObjectOfType<Camera>();var canvas=UnityEngine.Object.FindObjectOfType<Canvas>();var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;var oldTarget=camera.targetTexture;var target=new RenderTexture(Screen.width,Screen.height,24);target.Create();camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;Canvas.ForceUpdateCanvases();camera.Render();var active=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();var colors=image.GetPixels32();Check(colors.Any(c=>c.r>80&&c.g>60),"Visible rendered frame "+name);File.WriteAllBytes(Path.Combine(dir,name+".png"),image.EncodeToPNG());RenderTexture.active=active;camera.targetTexture=oldTarget;canvas.renderMode=mode;canvas.worldCamera=oldCamera;Destroy(image);target.Release();Destroy(target);screenshots++;yield return new WaitForSecondsRealtime(.1f);}
        IEnumerator Start(){dir=InkominoApp.QAOutput();Directory.CreateDirectory(dir);var app=InkominoApp.Instance;yield return new WaitForSecondsRealtime(1);app.Store.Data=new PlayerProfile{level=23,coins=220,avatar=2,hearts=5,regenAt=SaveStore.Now+900};app.ShowHome();yield return Capture("01-home");
            CloudSaveQA.Run(app,Check);
            yield return CheckRefinements(app);
            yield return CheckRewardAndTips(app);
            app.StartLevel(23,true);yield return null;Check(app.InputEnabled,"game input enabled");yield return Capture("02-level23-empty");CheckBoosterLayout(app);
            // Feed genuine uGUI pointer events through EventSystem, rather than only changing the model.
            var piece=app.Board.Level.solution[0];var view=app.BoardView;var rect=(RectTransform)view.transform;var cells=piece.cells;var path=Walk(cells,app.Board.Level.n);Vector2 screen(int cell){return view.CellCenterScreen(cell); }
            var evt=new PointerEventData(EventSystem.current){pointerId=-1,position=screen(path[0])};ExecuteEvents.Execute(view.gameObject,evt,ExecuteEvents.pointerDownHandler);foreach(int c in path.Skip(1)){evt.position=screen(c);ExecuteEvents.Execute(view.gameObject,evt,ExecuteEvents.dragHandler);}yield return Capture("16-drawing-trail",.02f);ExecuteEvents.Execute(view.gameObject,evt,ExecuteEvents.pointerUpHandler);yield return Capture("17-sticker-landing",.10f);yield return Capture("17b-sticker-press",.08f);Check(app.Board.Pieces.Count==1,"pointer gesture placed a piece");
            app.UseBooster(0);while(app.AnimationBusy)yield return null;yield return Capture("03-level23-pieces");var graphic=view.GetComponentsInChildren<BoardMesh>().First(x=>x.Layer==1);var mesh=graphic.canvasRenderer.GetMesh();File.WriteAllText(Path.Combine(dir,"mesh-diagnostic.txt"),"vertex="+mesh.vertexCount+" first="+(mesh.colors32.Length>0?mesh.colors32[0].ToString():"none")+" texture="+graphic.mainTexture.name+" shader="+graphic.material.shader.name);Check(app.Board.Pieces.Count>=2,"pen booster placed a piece");var saved=app.Board.Pieces.ToArray();Check(saved.Any(p=>p.locked)&&saved.All(p=>p.appearanceSaved),"piece lock and appearance saved");string fingerprint=string.Join(";",saved.Select(p=>p.locked+":"+p.colorIndex+":"+p.textureIndex+":"+p.textureOffsetX+":"+p.textureOffsetY));Check(app.Board.Restore(saved)&&fingerprint==string.Join(";",app.Board.Pieces.Select(p=>p.locked+":"+p.colorIndex+":"+p.textureIndex+":"+p.textureOffsetX+":"+p.textureOffsetY)),"restore preserves piece lock/color/facet/offset");int lens=app.Store.Data.lens;app.UseBooster(2);while(app.AnimationBusy)yield return null;Check(app.Store.Data.lens==lens-1&&app.BoardView.Reveal!=null,"lens reveals continuation");Check(app.BoardView.HintCells!=null&&app.BoardView.HintCells.SequenceEqual(app.BoardView.Reveal),"lens highlights entire continuation shape");yield return Capture("20-lens-whole-shape",.02f);Check(view.HintCells.All(cell=>app.Board.At(cell)==null),"lens hint only covers empty cells");var hintMesh=view.GetComponentsInChildren<BoardMesh>().First(x=>x.Layer==8).canvasRenderer.GetMesh();Check(hintMesh!=null&&hintMesh.vertexCount>0,"lens hint has visible rendered mesh");File.WriteAllText(Path.Combine(dir,"hint-diagnostic.txt"),"cells="+string.Join(",",view.HintCells)+" strength="+view.HintStrength+" vertices="+(hintMesh?hintMesh.vertexCount:0));app.UseBooster(1);yield return new WaitForSecondsRealtime(.5f);float before=app.Remaining;yield return new WaitForSecondsRealtime(.3f);Check(Mathf.Abs(before-app.Remaining)<.1f,"freeze pauses timer");yield return Capture("18-freeze-timer",.02f);
            app.Settings(true);before=app.Remaining;yield return new WaitForSecondsRealtime(.2f);Check(!app.InputEnabled&&Mathf.Abs(before-app.Remaining)<.1f,"pause blocks input and timer");yield return Capture("04-paused");app.ClosePopup();
            app.StartLevel(11,true);yield return null;app.Board.Restore(app.Board.Level.solution);Check(app.Board.Solved,"level 11 corrected reference solves");yield return new WaitForSecondsRealtime(2.6f);yield return Capture("05-win");
            app.StartLevel(15,true);yield return Capture("21-level15-preview");foreach(var placement in app.Board.Level.solution.Take(3)){app.Board.TryPlace(placement.cells,out string why);}yield return Capture("22-level15-stickers");
            Screen.SetResolution(360,800,false);yield return new WaitForSecondsRealtime(.5f);
            foreach(int levelId in new[]{17,20,21,22}){
                app.StartLevel(levelId,true);yield return Capture("barrier-level"+levelId);
                var barriers=app.BoardView.GetComponentInChildren<StickerBarrierGraphic>();
                int expected=app.Board.Level.walls.e.Count(w=>w>0)+app.Board.Level.walls.s.Count(w=>w>0);
                Check(barriers&&barriers.RenderedBarrierCount==expected,"all barriers use the sticker atlas on level "+levelId);
                Check(barriers&&!barriers.raycastTarget,"barrier art does not intercept drawing gestures");
            }
            Screen.SetResolution(540,960,false);yield return new WaitForSecondsRealtime(.5f);
            app.StartLevel(25,true);yield return null;app.Board.TryPlace(app.Board.Level.solution[0].cells,out string reason);yield return Capture("06-symbols");
            app.StartLevel(50,false);yield return null;app.Remaining=0;yield return new WaitForSecondsRealtime(.1f);Check(app.Finished,"timeout ends level");yield return Capture("07-timeout");CheckTimeout(app,false);int heartBefore=app.Store.Data.hearts;var freeButton=FindButton(app,Zh.Free);Check(freeButton!=null,"first failure has original FREE continuation");if(freeButton){freeButton.onClick.Invoke();Check(!app.Finished&&app.Remaining>59&&app.Store.Data.firstContinueUsed,"first free continuation adds60seconds");}app.Remaining=0;app.Fail();yield return new WaitForSecondsRealtime(1);yield return Capture("19-timeout-paid",.02f);CheckTimeout(app,true);var paid=FindButton(app,Zh.PlayOn);int coinsBefore=app.Store.Data.coins;Check(paid!=null,"repeat failure has local coin continuation");if(paid){paid.onClick.Invoke();Check(!app.Finished&&app.Remaining>59&&app.Store.Data.coins==coinsBefore-120,"second continuation spends120 and adds60seconds");}app.Remaining=0;app.Fail();yield return new WaitForSecondsRealtime(1);app.SourceFailureExit?.Invoke();yield return new WaitForSecondsRealtime(1);app.SourceFailureExit?.Invoke();Check(!app.Playing&&app.Store.Data.hearts==heartBefore-1,"confirmed give-up loses exactly one life");
            app.ShowShop();yield return Capture("08-shop");app.ShowCollection();yield return Capture("09-collection");app.LevelLibrary(19);yield return Capture("10-level-library");
            app.ShowHome();app.SendMessage("LifePopup",SendMessageOptions.RequireReceiver);yield return Capture("13-lives");app.ClosePopup();app.StartLevel(23,true);app.Settings(true);app.SendMessage("LeavePrompt",true,SendMessageOptions.RequireReceiver);yield return Capture("14-restart-confirm");app.ClearPopups();app.Store.Data.resumeLevel=23;app.Store.Data.resumeTime=98;app.Store.Data.resumePieces=new PieceData[0];app.ShowHome();app.SendMessage("PlayCurrent",SendMessageOptions.RequireReceiver);yield return Capture("15-continue-prompt");app.ClearPopups();
            app.ShowHome();Screen.SetResolution(960,720,false);yield return new WaitForSecondsRealtime(.6f);yield return Capture("11-landscape");CheckRect(app);Screen.SetResolution(540,960,false);yield return new WaitForSecondsRealtime(.6f);yield return Capture("12-short-phone");CheckRect(app);
            string json="{\"status\":\""+(errors.Count==0?"PASS":"FAIL")+"\",\"screenshots\":"+screenshots+",\"errors\":["+string.Join(",",errors.Select(x=>"\""+x+"\""))+"]}";File.WriteAllText(Path.Combine(dir,"runtime-validation.json"),json);Debug.Log("INKOMINO_RUNTIME_QA "+json);Application.Quit(errors.Count==0?0:1);}
        IEnumerator CheckRewardAndTips(InkominoApp app)
        {
            Screen.SetResolution(360,800,false);yield return new WaitForSecondsRealtime(.25f);
            app.StartLevel(11,true);yield return null;
            float began=Time.unscaledTime;app.Board.Restore(app.Board.Level.solution);
            SourceResultMotion result=null;
            while(Time.unscaledTime-began<1.1f){result=app.Content.GetComponentInChildren<SourceResultMotion>();if(result&&result.RewardAlpha.alpha>=.95f&&result.ButtonAlpha.blocksRaycasts)break;yield return null;}
            Check(result&&result.RewardAlpha.alpha>=.95f&&result.ButtonAlpha.blocksRaycasts,"solved board exposes reward and usable Continue within 1.1 seconds");
            yield return Capture("28-fast-result",.01f);
            for(int i=0;i<4;i++){
                var tip=SourceGameplayLoadingView.Create(app.Stage,i);yield return new WaitForSecondsRealtime(.4f);
                var icon=tip.Icon.GetComponent<Image>();Check(icon&&icon.sprite,"tip illustration exists "+i);
                if(icon&&icon.sprite){var sprite=icon.sprite;Check(Mathf.Max(sprite.rect.width,sprite.rect.height)>=tip.Icon.sizeDelta.x,"tip uses enough original pixels without upscaling "+i);Check(icon.preserveAspect&&tip.Icon.localScale.x<=1.001f,"tip preserves aspect and avoids overshoot "+i);}
                yield return Capture("29-tip-"+i,.01f);Destroy(tip.gameObject);yield return null;
            }
        }
        IEnumerator CheckRefinements(InkominoApp app)
        {
            app.Store.Data.hearts=3;app.Store.Data.regenAt=SaveStore.Now+899;app.ShowHome();yield return null;
            var homeLabels=app.Content.GetComponentsInChildren<Text>();
            var levelLabel=homeLabels.First(t=>t.name=="Home current level");
            var play=app.Content.GetComponentsInChildren<Button>().First(b=>b.GetComponentsInChildren<Text>().Any(t=>t.text==Zh.Play));
            var playText=play.GetComponentsInChildren<Text>().First(t=>t.text==Zh.Play);var slogan=homeLabels.First(t=>t.text=="随手划两下，快乐贴满格");
            Check(levelLabel.text=="第23关"&&levelLabel.transform.IsChildOf(play.transform)&&levelLabel.transform.position.x<playText.transform.position.x,"home level sits inside the left side of Play");
            Check(levelLabel.font==playText.font&&slogan.font==playText.font,"home level, Play and slogan share one font");
            Check(!app.Content.GetComponentsInChildren<Image>().Any(i=>i.sprite==MenuUI.BrandSprite("brand_logo")),"home has no title logo");
            var life=homeLabels.First(t=>t.name=="Wallet life count");
            Check(life.text=="3"&&life.transform.parent.name=="Wallet heart","life count is inside the heart");
            Check(homeLabels.Any(t=>t.text=="00:15")&&!homeLabels.Any(t=>t.text.Contains("查看关卡")||t.text==Zh.LevelLibrary),"home shows hh:mm recovery time and no directory entry");
            yield return Capture("34-home-layout",.5f);app.ShowCollection();yield return null;
            Check(!app.Content.GetComponentsInChildren<Text>().Any(t=>t.text==Zh.LevelLibrary),"achievement page has no directory entry");
            app.Store.Data.hearts=5;app.ShowHome();yield return null;
            var startup=SourceLoadingView.Create(app.Stage);yield return null;
            Check(startup.Logo.GetComponentInChildren<Image>().sprite==MenuUI.BrandSprite("brand_logo"),"startup uses new shared title logo");
            Check(startup.GetComponentsInChildren<Image>().Any(i=>i.sprite==MenuUI.BrandSprite("brand_startup")),"startup has its own journal artwork");
            string words="进度已保存在本机暂未备份到云端连接恢复后会自动重试微信登录暂未成功";
            MenuUI.BodyFont.RequestCharactersInTexture(words,48);
            Check(words.All(c=>MenuUI.BodyFont.HasCharacter(c)),"cloud status includes every Chinese glyph");
            yield return Capture("30-startup-brand",.25f);Destroy(startup.gameObject);yield return null;
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var sync=app.gameObject.AddComponent<CloudSaveSync>();
            typeof(CloudSaveSync).GetMethod("Offline",flags).Invoke(sync,null);
            var cloudField=typeof(InkominoApp).GetField("<CloudSave>k__BackingField",flags);cloudField.SetValue(app,sync);
            typeof(InkominoApp).GetMethod("ShowCloudSaveStatus",flags).Invoke(app,null);
            yield return Capture("33-cloud-status",.5f);app.ClearPopups();cloudField.SetValue(app,null);Destroy(sync);yield return null;
            app.LevelLibrary(0);yield return new WaitForSecondsRealtime(.9f);
            var directory=app.Overlays.GetComponentInChildren<LevelDirectoryView>();var panel=directory.transform;Vector3 scale=panel.localScale;int id=panel.GetInstanceID();
            for(int page=1;page<20;page++)directory.SetPage(page);
            Check(directory.Page==19&&panel.GetInstanceID()==id&&panel.localScale==scale,"directory paging preserves the modal and its settled scale");
            Check(directory.Cells[0].GetComponentInChildren<Text>().text=="476"&&!directory.transform.Find("Directory next").GetComponent<Button>().interactable,"last directory page renders correct range and disables next");
            directory.SetPage(-1);Check(directory.Page==0&&!directory.transform.Find("Directory previous").GetComponent<Button>().interactable,"first directory page disables previous");
            yield return Capture("31-directory-stable",.01f);app.ClearPopups();
            app.StartLevel(14,true);yield return null;
            var tee=app.Board.Level.solution.FirstOrDefault(p=>p.cells.Length==4&&p.cells.Any(c=>p.cells.Count(other=>Math.Abs(other/app.Board.Level.n-c/app.Board.Level.n)+Math.Abs(other%app.Board.Level.n-c%app.Board.Level.n)==1)==3));
            Check(tee!=null,"level 14 contains a valid T target for the backtracking regression");
            if(tee!=null){var teePath=Walk(tee.cells,app.Board.Level.n);app.BoardView.BeginCell(teePath[0]);foreach(int cell in teePath.Skip(1))app.BoardView.MoveCell(cell);Check(app.BoardView.EndStroke(),"T shape accepts backtracking across its middle cell");}
            app.StartLevel(23,true);yield return null;var view=app.BoardView;var cells=app.Board.Level.solution[0].cells;var path=Walk(cells,app.Board.Level.n);
            view.BeginCell(path[0]);float first=SourceClock.Now;
            bool sawSequentialReveal=false;
            foreach(int cell in path.Skip(1)){
                yield return new WaitForSecondsRealtime(.035f);int previousCount=view.Trail.Count;view.MoveCell(cell);
                if(view.Trail.Count>previousCount){float oldScale,oldAlpha,newScale,newAlpha;view.TrailAppearance(cells[0],out oldScale,out oldAlpha);view.TrailAppearance(view.Trail.Last(),out newScale,out newAlpha);sawSequentialReveal|=oldAlpha>newAlpha;}
            }
            Check(new HashSet<int>(view.Trail).SetEquals(cells),"drawing can backtrack to reach branched shapes");
            Check(sawSequentialReveal,"earlier cells are more developed than each newly entered cell during drawing");
            Canvas.ForceUpdateCanvases();var feedback=view.GetComponentsInChildren<BoardMesh>().First(m=>m.Layer==7).canvasRenderer.GetMesh();
            Check(!feedback||feedback.vertexCount==0,"drawing has no pointer line or decorative trail mesh");
            bool committed=view.EndStroke();Check(committed,"ordered stroke commits a real valid piece");
            if(committed){var placed=app.Board.Pieces[0];var style=view.Style(placed);Check(style.CellTimes!=null&&style.CellTimes.Length==cells.Length&&style.CellTimes[0]<style.CellTimes.Last()&&Mathf.Abs(style.CellTimes[0]-first)<.03f,"commit preserves actual first-entry times without replaying every cell");Check(view.CornerFold(placed,cells[0])>view.CellSize*.30f,"release restores the full visible sticker corner curl");}
            if(committed){
                bool manual=SourceClock.Manual;float clock=SourceClock.ManualTime;var placed=app.Board.Pieces[0];float landed=view.Style(placed).Born;
                try{SourceClock.Manual=true;SourceClock.ManualTime=landed+.25f;Check(view.CornerFold(placed,cells[0])>0,"corner curl retains the original unfolding duration");SourceClock.ManualTime=landed+.70f;Check(view.CornerFold(placed,cells[0])<=.01f,"sticker finishes unfolding cleanly");}
                finally{SourceClock.Manual=manual;SourceClock.ManualTime=clock;}
            }
            yield return Capture("32-ordered-piece",.02f);
        }
        void CheckBoosterLayout(InkominoApp app)
        {
            Rect Bounds(RectTransform r){var corners=new Vector3[4];r.GetWorldCorners(corners);return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);}
            var tray=app.Content.GetComponentsInChildren<RectTransform>().First(r=>r.name=="Sticker booster tray");
            var slots=tray.GetComponentsInChildren<RectTransform>().Where(r=>r.name.StartsWith("Source booster ")).OrderBy(r=>r.name).ToArray();
            Check(slots.Length==3,"three tool buttons rendered");
            Check(!tray.GetComponentsInChildren<Image>().Any(i=>i.name=="sticker_dock"),"booster tray has no oval backing");
            var clock=app.Content.GetComponentsInChildren<RectTransform>().First(r=>r.name=="Source timer");
            Check(!clock.GetComponentsInChildren<Image>().Any(i=>i.name=="Panel"),"timer has no progress underline");
            Check(app.Content.GetComponentsInChildren<StickerPreviewTile>().Length>0,"targets use separated puffy sticker cells");
            var round=slots[0].GetComponentInChildren<RoundedGraphic>();var roundedMesh=round.canvasRenderer.GetMesh();
            Check(roundedMesh&&roundedMesh.colors32.Any(c=>c.a==0)&&roundedMesh.colors32.Any(c=>c.a>0),"rounded cards have alpha coverage fringe");

            for(int i=0;i<slots.Length;i++){
                Check(Bounds(slots[i]).width>=44,"booster hit area meets 44px minimum");
                if(i>0)Check(Bounds(slots[i]).xMin-Bounds(slots[i-1]).xMax>=6,"tool columns remain separated");
                var evt=new PointerEventData(EventSystem.current){position=Bounds(slots[i]).center};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(evt,hits);
                Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==slots[i].GetComponent<Button>(),"tool button receives input through illustrated face");
            }
            Check(Bounds((RectTransform)app.BoardView.transform).yMin>Bounds(tray).yMax,"board does not overlap tool tray");
            for(int i=0;i<3;i++){var status=app.Content.GetComponentsInChildren<RectTransform>().First(r=>r.name=="Booster status "+i);var r=Bounds(status);var card=Bounds(slots[i]);Check(r.yMin>=card.yMin+4&&r.yMax<=card.yMax,"tool count stays inside its card");}

        }
        void CheckTimeout(InkominoApp app,bool paid)
        {
            var panel=app.Overlays.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name=="Sticker timeout panel");Check(panel!=null,"timeout uses compact shared modal");if(!panel)return;
            var price=panel.GetComponentsInChildren<Text>().FirstOrDefault(t=>t.name=="Continuation cost");
            if(paid){Check(price&&price.text=="120"&&price.color.a==1,"paid continuation shows correct visible price");var button=panel.GetComponentsInChildren<Button>().First(b=>b.name=="Timeout continue");Check(price&&price.transform.IsChildOf(button.transform),"price belongs to animated continuation button");}
            Check(panel.GetComponentsInChildren<Button>().Any(b=>b.name=="Timeout exit"&&b.interactable),"timeout provides explicit end-round action");
        }
        static Button FindButton(InkominoApp app,string text){return app.Overlays.GetComponentsInChildren<Button>().LastOrDefault(b=>b.gameObject.activeInHierarchy&&b.GetComponentsInChildren<Text>().Any(t=>t.text==text));}
        void CheckRect(InkominoApp app){var corners=new Vector3[4];app.Stage.GetWorldCorners(corners);Check(corners.All(p=>p.x>=-1&&p.y>=-1&&p.x<=Screen.width+1&&p.y<=Screen.height+1),"portrait stage stays within viewport "+Screen.width+"x"+Screen.height);}
        static List<int> Walk(int[] cells,int n){var set=new HashSet<int>(cells);var visited=new HashSet<int>();var path=new List<int>();Action<int> dfs=null;dfs=i=>{visited.Add(i);path.Add(i);foreach(int j in new[]{i-n,i+n,i-1,i+1})if(set.Contains(j)&&!visited.Contains(j)&&Math.Abs(j/n-i/n)+Math.Abs(j%n-i%n)==1){dfs(j);path.Add(i);}};dfs(cells[0]);return path;}
    }
}

#endif

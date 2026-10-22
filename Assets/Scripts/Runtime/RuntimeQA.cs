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
            app.StartLevel(23,true);yield return null;Check(app.InputEnabled,"game input enabled");yield return Capture("02-level23-empty");CheckBoosterLayout(app);
            // Feed genuine uGUI pointer events through EventSystem, rather than only changing the model.
            var piece=app.Board.Level.solution[0];var view=app.BoardView;var rect=(RectTransform)view.transform;var cells=piece.cells;var path=Walk(cells,app.Board.Level.n);Vector2 screen(int cell){return view.CellCenterScreen(cell); }
            var evt=new PointerEventData(EventSystem.current){pointerId=-1,position=screen(path[0])};ExecuteEvents.Execute(view.gameObject,evt,ExecuteEvents.pointerDownHandler);foreach(int c in path.Skip(1)){evt.position=screen(c);ExecuteEvents.Execute(view.gameObject,evt,ExecuteEvents.dragHandler);}yield return Capture("16-drawing-trail",.02f);ExecuteEvents.Execute(view.gameObject,evt,ExecuteEvents.pointerUpHandler);yield return Capture("17-sticker-landing",.10f);Check(app.Board.Pieces.Count==1,"pointer gesture placed a piece");
            app.UseBooster(0);while(app.AnimationBusy)yield return null;yield return Capture("03-level23-pieces");var graphic=view.GetComponentsInChildren<BoardMesh>().First(x=>x.Layer==1);var mesh=graphic.canvasRenderer.GetMesh();File.WriteAllText(Path.Combine(dir,"mesh-diagnostic.txt"),"vertex="+mesh.vertexCount+" first="+(mesh.colors32.Length>0?mesh.colors32[0].ToString():"none")+" texture="+graphic.mainTexture.name+" shader="+graphic.material.shader.name);Check(app.Board.Pieces.Count>=2,"pen booster placed a piece");var saved=app.Board.Pieces.ToArray();Check(saved.Any(p=>p.locked)&&saved.All(p=>p.appearanceSaved),"piece lock and appearance saved");string fingerprint=string.Join(";",saved.Select(p=>p.locked+":"+p.colorIndex+":"+p.textureIndex+":"+p.textureOffsetX+":"+p.textureOffsetY));Check(app.Board.Restore(saved)&&fingerprint==string.Join(";",app.Board.Pieces.Select(p=>p.locked+":"+p.colorIndex+":"+p.textureIndex+":"+p.textureOffsetX+":"+p.textureOffsetY)),"restore preserves piece lock/color/facet/offset");int lens=app.Store.Data.lens;app.UseBooster(2);while(app.AnimationBusy)yield return null;Check(app.Store.Data.lens==lens-1&&app.BoardView.Reveal!=null,"lens reveals continuation");app.UseBooster(1);yield return new WaitForSecondsRealtime(.5f);float before=app.Remaining;yield return new WaitForSecondsRealtime(.3f);Check(Mathf.Abs(before-app.Remaining)<.1f,"freeze pauses timer");yield return Capture("18-freeze-timer",.02f);
            app.Settings(true);before=app.Remaining;yield return new WaitForSecondsRealtime(.2f);Check(!app.InputEnabled&&Mathf.Abs(before-app.Remaining)<.1f,"pause blocks input and timer");yield return Capture("04-paused");app.ClosePopup();
            app.StartLevel(11,true);yield return null;app.Board.Restore(app.Board.Level.solution);Check(app.Board.Solved,"level 11 corrected reference solves");yield return new WaitForSecondsRealtime(2.6f);yield return Capture("05-win");
            app.StartLevel(25,true);yield return null;app.Board.TryPlace(app.Board.Level.solution[0].cells,out string reason);yield return Capture("06-symbols");
            app.StartLevel(50,false);yield return null;app.Remaining=0;yield return new WaitForSecondsRealtime(.1f);Check(app.Finished,"timeout ends level");yield return Capture("07-timeout");int heartBefore=app.Store.Data.hearts;var freeButton=FindButton(app,Zh.Free);Check(freeButton!=null,"first failure has original FREE continuation");if(freeButton){freeButton.onClick.Invoke();Check(!app.Finished&&app.Remaining>59&&app.Store.Data.firstContinueUsed,"first free continuation adds60seconds");}app.Remaining=0;app.Fail();yield return new WaitForSecondsRealtime(1);var paid=FindButton(app,Zh.PlayOn);int coinsBefore=app.Store.Data.coins;Check(paid!=null,"repeat failure has local coin continuation");if(paid){paid.onClick.Invoke();Check(!app.Finished&&app.Remaining>59&&app.Store.Data.coins==coinsBefore-120,"second continuation spends120 and adds60seconds");}app.Remaining=0;app.Fail();yield return new WaitForSecondsRealtime(1);app.SourceFailureExit?.Invoke();yield return new WaitForSecondsRealtime(1);app.SourceFailureExit?.Invoke();Check(!app.Playing&&app.Store.Data.hearts==heartBefore-1,"confirmed give-up loses exactly one life");
            app.ShowShop();yield return Capture("08-shop");app.ShowCollection();yield return Capture("09-collection");app.LevelLibrary(19);yield return Capture("10-level-library");
            app.ShowHome();app.SendMessage("LifePopup",SendMessageOptions.RequireReceiver);yield return Capture("13-lives");app.ClosePopup();app.StartLevel(23,true);app.Settings(true);app.SendMessage("LeavePrompt",true,SendMessageOptions.RequireReceiver);yield return Capture("14-restart-confirm");app.ClearPopups();app.Store.Data.resumeLevel=23;app.Store.Data.resumeTime=98;app.Store.Data.resumePieces=new PieceData[0];app.ShowHome();app.SendMessage("PlayCurrent",SendMessageOptions.RequireReceiver);yield return Capture("15-continue-prompt");app.ClearPopups();
            app.ShowHome();Screen.SetResolution(960,720,false);yield return new WaitForSecondsRealtime(.6f);yield return Capture("11-landscape");CheckRect(app);Screen.SetResolution(540,960,false);yield return new WaitForSecondsRealtime(.6f);yield return Capture("12-short-phone");CheckRect(app);
            string json="{\"status\":\""+(errors.Count==0?"PASS":"FAIL")+"\",\"screenshots\":"+screenshots+",\"errors\":["+string.Join(",",errors.Select(x=>"\""+x+"\""))+"]}";File.WriteAllText(Path.Combine(dir,"runtime-validation.json"),json);Debug.Log("INKOMINO_RUNTIME_QA "+json);Application.Quit(errors.Count==0?0:1);}
        void CheckBoosterLayout(InkominoApp app)
        {
            Rect Bounds(RectTransform r){var corners=new Vector3[4];r.GetWorldCorners(corners);return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);}
            var tray=app.Content.GetComponentsInChildren<RectTransform>().First(r=>r.name=="Sticker booster tray");
            var slots=tray.GetComponentsInChildren<RectTransform>().Where(r=>r.name.StartsWith("Source booster ")).OrderBy(r=>r.name).ToArray();
            Check(slots.Length==3,"three tool buttons rendered");
            for(int i=0;i<slots.Length;i++){
                Check(Bounds(slots[i]).width>=44,"booster hit area meets 44px minimum");
                if(i>0)Check(Bounds(slots[i]).xMin-Bounds(slots[i-1]).xMax>=6,"tool columns remain separated");
                var evt=new PointerEventData(EventSystem.current){position=Bounds(slots[i]).center};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(evt,hits);
                Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==slots[i].GetComponent<Button>(),"tool button receives input through illustrated face");
            }
            Check(Bounds((RectTransform)app.BoardView.transform).yMin>Bounds(tray).yMax,"board does not overlap tool tray");
        }
        static Button FindButton(InkominoApp app,string text){return app.Overlays.GetComponentsInChildren<Button>().LastOrDefault(b=>b.gameObject.activeInHierarchy&&b.GetComponentsInChildren<Text>().Any(t=>t.text==text));}
        void CheckRect(InkominoApp app){var corners=new Vector3[4];app.Stage.GetWorldCorners(corners);Check(corners.All(p=>p.x>=-1&&p.y>=-1&&p.x<=Screen.width+1&&p.y<=Screen.height+1),"portrait stage stays within viewport "+Screen.width+"x"+Screen.height);}
        static List<int> Walk(int[] cells,int n){var set=new HashSet<int>(cells);var visited=new HashSet<int>();var path=new List<int>();Action<int> dfs=null;dfs=i=>{visited.Add(i);path.Add(i);foreach(int j in new[]{i-n,i+n,i-1,i+1})if(set.Contains(j)&&!visited.Contains(j)&&Math.Abs(j/n-i/n)+Math.Abs(j%n-i%n)==1){dfs(j);path.Add(i);}};dfs(cells[0]);return path;}
    }
}

#endif

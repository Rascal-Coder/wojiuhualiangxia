#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    public sealed class SocialQA:MonoBehaviour
    {
        [Serializable] public sealed class Result{public string status;public List<string> checks=new List<string>(),errors=new List<string>();public int screenshots;}
        Result result=new Result();InkominoApp app;string dir;
        void Check(bool ok,string name){result.checks.Add(name);if(!ok)result.errors.Add(name);}
        IEnumerator Start()
        {
            app=InkominoApp.Instance;app.enabled=false;app.SourceTutorialEnabled=false;AudioListener.volume=0;dir=InkominoApp.QAOutput();Directory.CreateDirectory(dir);
            long now=SaveStore.Now;var store=app.Store;var data=app.Data;
            store.Data=new PlayerProfile{level=40,drawIntroSeen=true};ClimbActivity.Refresh(store.Data,now);
            Check(!ClimbActivity.Join(store,now),"level40 cannot join");app.ShowClimb();yield return Capture("01-locked.png");
            store.Data.level=41;Check(ClimbActivity.Join(store,now),"level41 free entry");var state=store.Data.climb;
            Check(state.finalBots>=3&&state.finalBots<=5,"first summit distribution has3to5 simulated survivors");
            Check(!ClimbActivity.Join(store,now),"cannot replace active attempt");
            ClimbActivity.Advance(store.Data,data.levels[39],data,now);Check(state.stage==0,"below41 cannot advance event");
            store.Complete(data.levels[40],data);Check(state.stage==1,"main completion advances one island");
            store.LoseHeart(10);Check(!state.isOut,"unrelated low-level failure does not eliminate");
            app.ShowClimb();yield return Capture("02-active.png");
            for(int i=1;i<7;i++)ClimbActivity.Advance(store.Data,data.levels[40+i],data,now);
            Check(ClimbActivity.AtTop(state)&&state.finalCount==state.finalBots+1&&state.summits==1,"seven clears reach seeded final survivors");
            Check(state.eliminated.Sum()==99-state.finalBots,"dropouts conserve simulated population");
            app.ShowClimb();yield return Capture("03-summit.png");
            int before=store.Data.coins,prize=ClimbActivity.Prize(state);int claimed=ClimbActivity.Claim(store,now);
            Check(claimed==prize&&store.Data.coins==before+prize&&prize%5==0,"rounded summit reward granted once");
            Check(ClimbActivity.Claim(store,now)==0&&!ClimbActivity.Join(store,now),"claim and summit re-entry cannot duplicate reward");
            var reload=new SaveStore(store.Path);Check(reload.Data.climb.claimed&&reload.Data.coins==store.Data.coins,"claim and coins survive reload together");
            long boundary=ClimbActivity.WindowStart(now)+86400;ClimbActivity.Refresh(store.Data,boundary);
            Check(!store.Data.climb.joined&&store.Data.climb.summits==1,"daily18h reset preserves lifetime summits");
            Check(ClimbActivity.WindowStart(boundary-1)==boundary-86400&&ClimbActivity.WindowStart(boundary)==boundary,"daily reset boundary is exact");
            ClimbActivity.Join(store,now);ClimbActivity.Fail(store.Data,ClimbActivity.WindowStart(now)+60);Check(store.Data.climb.isOut,"eligible abandon eliminates attempt");
            long cooldown=store.Data.climb.cooldownUntil;
            Check(!ClimbActivity.Join(store,cooldown-1)&&ClimbActivity.Join(store,cooldown),"30minute cooldown blocks until exact expiry");
            app.ShowClimb();yield return Capture("04-retry.png");
            store.Data.climbPendingFailure=41;store.Save();var recovery=new SaveStore(store.Path);
            Check(recovery.Data.climb.isOut&&recovery.Data.climbPendingFailure==0,"unfinished failure screen forfeits once after relaunch");int lives=recovery.Data.hearts;recovery=new SaveStore(store.Path);Check(recovery.Data.hearts==lives,"relaunch cannot deduct twice");
            store.Data=new PlayerProfile{level=41,drawIntroSeen=true,firstContinueUsed=false};ClimbActivity.Join(store,now);app.StartLevel(41);app.Remaining=0;app.CoreFail();
            Check(!store.Data.climb.isOut&&store.Data.climbPendingFailure==41,"timeout offers continue without immediate elimination");
            var free=app.Overlays.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.GetComponentsInChildren<Text>().Any(t=>t.text==Zh.Free));
            Check(free!=null,"free continue available");if(free)free.onClick.Invoke();
            Check(!store.Data.climb.isOut&&store.Data.climbPendingFailure==0&&!app.Finished,"continue clears pending forfeiture");
            app.ShowHome();var trophy=app.Content.Find("Source navigation/Tab rank").GetComponent<Button>();trophy.onClick.Invoke();Check(app.Content.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Achievement summary"),"bottom trophy opens achievement page");
            yield return Capture("05-achievements.png");app.ShowAchievements(true);yield return Capture("06-friends-desktop.png");
            Check(app.Content.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Friends rank canvas"),"friend tab has dedicated canvas");
            var surface=app.Content.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name=="Friends rank surface");
            Check(surface!=null&&surface.GetSiblingIndex()>0,"shared surface is drawn after paper background");
            if(surface){var proof=surface.gameObject.AddComponent<RawImage>();proof.texture=Texture2D.whiteTexture;proof.color=Color.cyan;proof.raycastTarget=false;yield return Capture("07-shared-surface-visibility.png",true);Destroy(proof);}
            app.ShowHome();app.ShowClimb();yield return null;app.ShowHome();yield return null;Check(!app.Content.GetComponentsInChildren<ClimbPageMotion>().Any(),"activity motion removed on navigation");
            result.status=result.errors.Count==0?"PASS":"FAIL";File.WriteAllText(Path.Combine(dir,"social-validation.json"),JsonUtility.ToJson(result,true));Debug.Log("INKOMINO_SOCIAL_QA "+result.status+" "+string.Join(";",result.errors));Application.Quit(result.errors.Count==0?0:1);
        }
        IEnumerator Capture(string name,bool surfaceProbe=false)
        {
            yield return new WaitForSecondsRealtime(.6f);yield return new WaitForEndOfFrame();
            var camera=FindObjectOfType<Camera>();var canvas=FindObjectOfType<Canvas>();var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;var target=new RenderTexture(Screen.width,Screen.height,24);target.Create();camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;Canvas.ForceUpdateCanvases();camera.Render();var active=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();if(surfaceProbe){var surface=app.Content.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name=="Friends rank surface");var point=RectTransformUtility.WorldToScreenPoint(camera,surface.TransformPoint(surface.rect.center+Vector2.up*surface.rect.height*.35f));var pixel=image.GetPixel(Mathf.Clamp(Mathf.RoundToInt(point.x),0,image.width-1),Mathf.Clamp(Mathf.RoundToInt(point.y),0,image.height-1));Check(pixel.r<.2f&&pixel.g>.8f&&pixel.b>.8f,"shared texture pixels remain visible above paper in actual rendered frame");}File.WriteAllBytes(Path.Combine(dir,name),image.EncodeToPNG());RenderTexture.active=active;camera.targetTexture=null;canvas.renderMode=mode;canvas.worldCamera=oldCamera;Destroy(image);target.Release();Destroy(target);result.screenshots++;
        }
    }
}
#endif

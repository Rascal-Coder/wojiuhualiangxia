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
            long now=SaveStore.Now;var store=app.Store;var data=app.Data;yield return VerifyHomeEventStatus();yield return VerifyAvatars();VerifyShop(now);yield return VerifyShopUI();
            store.Data=new PlayerProfile{level=40,drawIntroSeen=true};ClimbActivity.Refresh(store.Data,now);
            Check(!ClimbActivity.Join(store,now),"level40 cannot join");app.ShowHome();var lockedHome=app.Content.GetComponentsInChildren<RectTransform>().First(r=>r.name=="Home sticker showcase");app.ShowClimb();
            Check(lockedHome&&lockedHome.gameObject.activeInHierarchy&&!app.Content.GetComponentsInChildren<ClimbPageMotion>().Any(),"level40 cannot open collection page through a direct entry");app.ClearPopups();yield return Capture("01-locked.png");
            store.Data.level=41;Check(ClimbActivity.Join(store,now),"level41 free entry");var state=store.Data.climb;
            Check(state.finalBots==0&&ClimbActivity.AliveBots(state)==0,"solo collection has no simulated opponents");
            Check(!ClimbActivity.Join(store,now),"cannot replace active attempt");
            ClimbActivity.Advance(store.Data,data.levels[39],data,now);Check(state.stage==0,"below41 cannot advance event");
            store.Complete(data.levels[40],data);Check(state.stage==1,"main completion advances one island");
            store.LoseHeart(10);Check(!state.isOut,"unrelated low-level failure does not eliminate");
            app.ShowClimb();Check(app.Content.GetComponentsInChildren<ClimbPageMotion>().Any(),"level41 opens collection page normally");yield return Capture("02-active.png");
            for(int i=1;i<7;i++)ClimbActivity.Advance(store.Data,data.levels[40+i],data,now);
            Check(ClimbActivity.AtTop(state)&&state.finalCount==state.finalBots+1&&state.summits==1,"seven clears complete solo collection");
            Check(state.eliminated.Sum()==0&&ClimbActivity.Prize(state)==1000,"collection grants fixed advertised1000coins");
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
        IEnumerator VerifyHomeEventStatus()
        {
            Text Status(int index)=>app.Content.GetComponentsInChildren<RectTransform>().First(r=>r.name=="Source event "+index).GetComponentsInChildren<Text>().Last();
            app.Store.Data=new PlayerProfile{level=18,avatar=8,coins=5,drawIntroSeen=true,dailyAt=SaveStore.Now-3480};app.ShowHome();yield return null;
            Check(Status(3).text=="41关解锁","home collection keeps level41 gate");
            Check(Status(0).font==Status(3).font&&Status(0).fontSize==Status(3).fontSize&&Status(0).color==Status(3).color&&!Status(0).alignByGeometry&&!Status(3).alignByGeometry,"home event statuses share typography and fixed baseline");
            var home=app.Content.GetComponentsInChildren<RectTransform>().First(r=>r.name=="Home sticker showcase");app.Content.GetComponentsInChildren<Button>().First(b=>b.name=="Source event 3").onClick.Invoke();
            Check(home&&home.gameObject.activeInHierarchy&&!app.Content.GetComponentsInChildren<ClimbPageMotion>().Any()&&app.Overlays.GetComponentsInChildren<Text>().Any(t=>t.text=="第 41 关开启"),"locked home entry stays on home and explains level41 unlock");app.ClearPopups();
            yield return Capture("00-home-events-locked.png");
            app.Store.Data.level=41;ClimbActivity.Join(app.Store,SaveStore.Now);app.Store.Data.climb.stage=2;app.ShowHome();yield return null;
            Check(System.Text.RegularExpressions.Regex.IsMatch(Status(3).text,@"^\d{2}:\d{2}$"),"unlocked active collection shows hh:mm instead of progress");
            yield return Capture("00-home-events-countdown.png");
            app.Store.Data.climb.isOut=true;app.Store.Data.climb.cooldownUntil=SaveStore.Now+1800;app.SendMessage("UpdateHomeTimers");Check(Status(3).text=="00:30","retry countdown keeps hh:mm format");
            app.Store.Data.climb.isOut=false;app.Store.Data.climb.stage=7;app.Store.Data.dailyAt=0;app.SendMessage("UpdateHomeTimers");Check(Status(0).text==Zh.GiftReady&&Status(3).text==Status(0).text,"claimable collection uses same ready label as daily gift");
            app.Store.Data.climb.claimed=true;app.SendMessage("UpdateHomeTimers");Check(System.Text.RegularExpressions.Regex.IsMatch(Status(3).text,@"^\d{2}:\d{2}$"),"claimed collection shows next-round countdown");
        }
        IEnumerator VerifyAvatars()
        {
            var sprites=Enumerable.Range(1,MenuUI.AvatarCount).Select(MenuUI.AvatarSprite).ToArray();
            Check(sprites.Length==8&&sprites.All(s=>s)&&sprites.Distinct().Count()==8,"eight distinct character sprites load from atlas");
            Check(sprites.All(s=>s&&s.texture==sprites[0].texture&&s.rect.width==240&&s.rect.height==240),"avatars share one padded texture with correct slices");
            Check(MenuUI.AvatarSprite(16)==sprites[7]&&MenuUI.AvatarSprite(9)==sprites[0]&&MenuUI.AvatarSprite(0)==sprites[0]&&MenuUI.AvatarSprite(int.MinValue)==sprites[0],"legacy and invalid avatar IDs map safely");
            app.Store.Data=new PlayerProfile{level=23,coins=1000,avatar=16,drawIntroSeen=true};app.ShowHome();yield return null;
            var wallet=app.Content.GetComponentsInChildren<Image>().First(i=>i.transform.parent.name=="Avatar clip");
            Check(wallet.sprite==sprites[7],"legacy profile displays new avatar in wallet");
            app.Content.GetComponentsInChildren<Button>().First(b=>b.name=="Profile").onClick.Invoke();yield return null;
            app.Overlays.GetComponentsInChildren<Button>().First(b=>b.GetComponentsInChildren<Text>().Any(t=>t.text==Zh.ChangeAvatar)).onClick.Invoke();yield return null;
            var choices=app.Overlays.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("Avatar choice ")).ToArray();
            Check(choices.Length==8,"picker exposes all eight avatars without cycling");
            foreach(int id in new[]{1,2,3,4,5,6,7,8})
            {
                choices.First(b=>b.name=="Avatar choice "+id).onClick.Invoke();
                Check(app.Store.Data.avatar==id&&wallet.sprite==sprites[id-1],"select avatar "+id+" updates wallet immediately");
                Check(new SaveStore(app.Store.Path).Data.avatar==id,"avatar "+id+" survives local save reload");
            }
            yield return Capture("00-avatar-picker.png");app.ClearPopups();app.ShowHome();yield return Capture("00-avatar-home.png");
        }
        void VerifyShop(long now)
        {
            var path=Path.Combine(dir,"shop-policy-profile.json");var store=new SaveStore(path);store.Data=new PlayerProfile{coins=3000,hint=0,freeze=0,lens=0};
            Check(!store.BuyAt("unknown",150,now)&&!store.BuyAt("hint",-10,now)&&store.Data.coins==3000,"invalid item or price cannot deduct coins");
            for(int i=0;i<3;i++)Check(store.BuyAt("hint",150,now),"daily pack "+i+" grants three tools");
            int coins=store.Data.coins;Check(!store.BuyAt("hint",150,now)&&store.Data.coins==coins&&store.Data.hint==9,"fourth daily pack cannot spend or grant");
            var reload=new SaveStore(path);Check(reload.ShopRemaining("hint",now)==0&&reload.Data.hint==9,"daily quota survives relaunch");
            Check(reload.ShopRemaining("freeze",now)==3,"different tools have independent daily quota");
            long midnight=((now+8*3600)/86400+1)*86400-8*3600;Check(reload.ShopRemaining("hint",midnight-1)==0&&reload.ShopRemaining("hint",midnight)==3,"quota resets at China midnight exactly");
            reload.Data.freeze=30;coins=reload.Data.coins;Check(reload.BuyAt("freeze",120,midnight,3)&&reload.Data.freeze==33&&reload.Data.coins==coins-120,"buying can exceed thirty owned tools");
            reload.Data.lens=0;coins=reload.Data.coins;
            Check(!reload.BuyAt("lens",0,midnight,0)&&!reload.BuyAt("lens",200,midnight,4)&&!reload.BuyAt("lens",1,midnight,1)&&reload.Data.coins==coins&&reload.ShopRemaining("lens",midnight)==3,"invalid quantities or forged prices cannot spend coins or daily quota");
            for(int quantity=1;quantity<=3;quantity++)Check(reload.BuyAt("lens",50*quantity,midnight,quantity),"selected quantity "+quantity+" purchases successfully");
            Check(reload.Data.lens==6&&reload.Data.coins==coins-300&&reload.ShopRemaining("lens",midnight)==0,"each transaction consumes one daily purchase regardless of selected amount");
            coins=reload.Data.coins;Check(!reload.BuyAt("lens",50,midnight,1)&&reload.Data.coins==coins&&reload.Data.lens==6,"fourth transaction rejected even for one unit");
            reload.Data.coins=49;Check(!reload.BuyAt("hint",50,midnight,1)&&reload.Data.coins==49&&reload.ShopRemaining("hint",midnight)==3,"insufficient coins never consumes a purchase");
        }
        IEnumerator VerifyShopUI()
        {
            app.Store.Data=new PlayerProfile{level=41,coins=1000,hint=30,freeze=0,lens=0};app.ShowShop();yield return null;
            Button Button(string name)=>app.Content.GetComponentsInChildren<Button>().First(b=>b.name==name);
            Text Quantity()=>app.Content.GetComponentsInChildren<Text>().First(t=>t.name=="Shop quantity hint");
            var minus=Button("Shop minus hint");var plus=Button("Shop plus hint");var buy=Button("Shop exchange hint");
            Check(Quantity().text=="1"&&!minus.interactable&&plus.interactable&&buy.GetComponentInChildren<Text>().text=="50 金币","quantity defaults to one with correct unit price and disabled minus");
            Check(!app.Content.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("库存")||t.text.Contains("个装")),"shop hides stock and fixed pack labels");
            Check(!app.Content.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("今日还可购买")),"shop uses purchase stamps instead of repeated sentences");
            plus.onClick.Invoke();Check(Quantity().text=="2"&&buy.GetComponentInChildren<Text>().text=="100 金币","plus updates quantity and total price");plus.onClick.Invoke();
            Check(Quantity().text=="3"&&!plus.interactable&&buy.GetComponentInChildren<Text>().text=="150 金币","quantity stops at three");
            yield return Capture("00-shop-quantity.png");minus.onClick.Invoke();buy.onClick.Invoke();
            Check(app.Store.Data.hint==32&&app.Store.Data.coins==900&&app.Store.ShopRemaining("hint",SaveStore.Now)==2,"UI grants exactly the selected two tools above thirty owned and consumes one purchase");
            var quota=app.Content.GetComponentsInChildren<RectTransform>().First(t=>t.name=="Shop remaining hint");Check(quota.GetComponentsInChildren<Image>().Count(i=>i.color.a>.9f)==2,"one purchase fades exactly one of the three stamps");
            app.ClearPopups();app.Store.Data.coins=55;app.ShowShop();yield return null;
            buy=Button("Shop exchange hint");Check(buy.interactable,"one tool is affordable with 55 coins");Button("Shop plus hint").onClick.Invoke();Check(!buy.interactable,"higher quantity disables purchase when total is unaffordable");
            app.Store.Data.coins+=50;app.Store.Save();Check(buy.interactable,"coin reward refreshes affordability without reopening shop");
            app.Store.Data.coins=500;app.StartLevel(23);app.SendMessage("BoosterShop",0,SendMessageOptions.RequireReceiver);yield return null;
            var popupPlus=app.Overlays.GetComponentsInChildren<Button>().First(b=>b.name=="Shop plus hint");popupPlus.onClick.Invoke();
            var popupBuy=app.Overlays.GetComponentsInChildren<Button>().First(b=>b.name=="Shop exchange hint");
            Check(popupBuy.GetComponentInChildren<Text>().text=="100 金币"&&popupBuy.interactable,"in-level replenishment uses the same selected-quantity price");
            yield return Capture("00-shop-replenishment.png");popupBuy.onClick.Invoke();
            Check(app.Store.Data.hint==34&&app.Store.Data.coins==400&&app.Store.ShopRemaining("hint",SaveStore.Now)==1&&!app.IsModalOpen,"in-level purchase grants selected quantity and closes its modal");
        }
        IEnumerator Capture(string name,bool surfaceProbe=false)
        {
            yield return new WaitForSecondsRealtime(.6f);yield return new WaitForEndOfFrame();
            var camera=FindObjectOfType<Camera>();var canvas=FindObjectOfType<Canvas>();var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;var target=new RenderTexture(Screen.width,Screen.height,24);target.Create();camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;Canvas.ForceUpdateCanvases();camera.Render();var active=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();if(surfaceProbe){var surface=app.Content.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name=="Friends rank surface");var point=RectTransformUtility.WorldToScreenPoint(camera,surface.TransformPoint(surface.rect.center+Vector2.up*surface.rect.height*.35f));var pixel=image.GetPixel(Mathf.Clamp(Mathf.RoundToInt(point.x),0,image.width-1),Mathf.Clamp(Mathf.RoundToInt(point.y),0,image.height-1));Check(pixel.r<.2f&&pixel.g>.8f&&pixel.b>.8f,"shared texture pixels remain visible above paper in actual rendered frame");}File.WriteAllBytes(Path.Combine(dir,name),image.EncodeToPNG());RenderTexture.active=active;camera.targetTexture=null;canvas.renderMode=mode;canvas.worldCamera=oldCamera;Destroy(image);target.Release();Destroy(target);result.screenshots++;
        }
    }
}
#endif

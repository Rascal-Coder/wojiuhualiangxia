#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    public sealed partial class ChineseQA
    {
        Button AdButton(RewardPlacement placement){return app.Stage.GetComponentsInChildren<Button>().Last(b=>b.gameObject.activeInHierarchy&&b.name=="IAA "+placement);}
        sealed class FakeInterstitial:IInterstitialAdProvider
        {
            public int shows;public bool IsAvailable(){return true;}
            public void Show(Action<bool> done){shows++;done(true);done(true);}
        }
        IEnumerator RunMonetization()
        {
            app.enabled=false;app.SourceTutorialEnabled=false;AudioListener.volume=0;
            SourceClock.Manual=true;SourceClock.ManualTime=Time.unscaledTime;SourceClock.ManualDelta=0;
            app.Store.Data=new PlayerProfile{level=23,wins=12,coins=1000,hearts=1,hint=0,freeze=0,lens=0,regenAt=SaveStore.Now+900,dailyAt=SaveStore.Now-86400+5,firstContinueUsed=true,drawIntroSeen=true};
            app.Store.Save();app.ShowHome();yield return Settle();
            var label=app.Content.GetComponentsInChildren<RectTransform>().First(t=>t.name=="Source event 0").GetComponentsInChildren<Text>().Last();string first=label.text;
            yield return new WaitForSecondsRealtime(1.3f);CheckIt(label.text!=first,"首页礼物倒计时随真实秒数变化",first+" -> "+label.text);
            CheckIt(app.Content.GetComponentsInChildren<RectTransform>().First(t=>t.name=="Source event 3").GetComponentsInChildren<Text>().Any(t=>t.text=="41关解锁"),"未解锁活动显示41关解锁");
            yield return Capture("03-home-countdown.png","真实首页倒计时");
            app.Store.Data.dailyAt=SaveStore.Now-86401;app.Store.Save();CheckIt(label.text==Zh.GiftReady,"礼物到期立即显示可领取");
            int money=app.Store.Data.coins;Invoke("DailyReward");FindButton(Zh.Claim).onClick.Invoke();
            CheckIt(app.Store.Data.coins==money+100&&app.Store.DailyGiftSeconds(SaveStore.Now)>86390,"领取后进入真实24小时冷却");
            CheckIt(!app.Store.ClaimDailyGift(SaveStore.Now)&&app.Store.Data.coins==money+100,"同一礼物不能重复领取");
            var loaded=new SaveStore(app.Store.Path);CheckIt(loaded.Data.dailyAt==app.Store.Data.dailyAt,"礼物领取时刻持久化");

            RewardedAds.Register(null);app.ShowShop();yield return Settle();yield return Capture("04-shop-ad.png","金币补给视频入口");
            money=app.Store.Data.coins;AdButton(RewardPlacement.CoinSupply).onClick.Invoke();
            CheckIt(app.Store.Data.coins==money&&(app.Store.Data.adUsage==null||app.Store.Data.adUsage.Length==0),"未配置广告时不发金币也不扣奖励次数");
            yield return Settle();yield return Capture("05-ad-unavailable.png","未配置广告的真实提示");app.ClearPopups();
            RewardedAds.Register(new FakeProvider{result=RewardedOutcome.Completed,repeat=true});AdButton(RewardPlacement.CoinSupply).onClick.Invoke();
            CheckIt(app.Store.Data.coins==money+50,"完整观看且重复回调只获得一次50金币");money=app.Store.Data.coins;AdButton(RewardPlacement.CoinSupply).onClick.Invoke();
            CheckIt(app.Store.Data.coins==money,"广告冷却期间不重复发奖");app.ClearPopups();

            app.StartLevel(23);Invoke("BoosterShop",0);yield return Settle();yield return Capture("06-booster-ad.png","补道具视频与金币双选择");
            RewardedAds.Register(new FakeProvider{result=RewardedOutcome.Skipped});AdButton(RewardPlacement.Pen).onClick.Invoke();
            CheckIt(app.Store.Data.hint==0,"中途关闭视频不发放画笔");app.ClearPopups();Invoke("BoosterShop",0);
            RewardedAds.Register(new FakeProvider{result=RewardedOutcome.Completed,repeat=true});AdButton(RewardPlacement.Pen).onClick.Invoke();CheckIt(app.Store.Data.hint==1,"画笔奖励入口真实接入库存");
            for(int i=1;i<3;i++){app.ClearPopups();Invoke("BoosterShop",i);AdButton(i==1?RewardPlacement.Freeze:RewardPlacement.Lens).onClick.Invoke();}
            CheckIt(app.Store.Data.freeze==2&&app.Store.Data.lens==1,"冻结和放大镜各自绑定正确奖励");
            app.ShowHome();Invoke("LifePopup");yield return Settle();yield return Capture("07-life-ad.png","生命奖励与金币补满");yield return Capture("08-life-small.png","生命弹窗小屏",540,836);
            AdButton(RewardPlacement.Life).onClick.Invoke();CheckIt(app.Store.Data.hearts==2,"生命奖励只增加一颗并保留恢复时刻");app.ClearPopups();

            app.StartLevel(23);app.Remaining=0;app.CoreFail();yield return Settle();yield return Capture("09-continue-ad.png","失败后视频加时入口");yield return Capture("10-continue-small.png","加时入口小屏",540,836);
            money=app.Store.Data.coins;AdButton(RewardPlacement.ContinueLevel).onClick.Invoke();
            CheckIt(!app.Finished&&app.Playing&&Mathf.Abs(app.Remaining-60)<.01f&&app.Store.Data.coins==money,"完成视频恢复同一关并增加60秒且不扣金币");
            app.Store.Data.adUsage=new AdUsage[0];app.StartLevel(24);app.Remaining=0;app.CoreFail();var pending=new FakeProvider{hold=true};RewardedAds.Register(pending);AdButton(RewardPlacement.ContinueLevel).onClick.Invoke();
            CheckIt(RewardedAds.IsShowing,"异步广告锁定玩法");app.ShowHome();pending.pending(RewardedOutcome.Completed);
            CheckIt(!RewardedAds.IsShowing&&!app.Playing&&Mathf.Abs(app.Remaining)<.01f,"离开关卡后的延迟回调不能复活旧关卡");

            app.Store.Data.adUsage=new AdUsage[0];app.StartLevel(23);int beforeWin=app.Store.Data.coins;app.Win();int baseReward=app.Store.Data.coins-beforeWin;
            yield return Advance(420);CheckIt(baseReward>0&&app.Content.GetComponentsInChildren<Button>().Any(b=>b.name=="IAA DoubleCoins"),"首次通关结算显示视频翻倍");
            yield return Capture("11-result-double.png","金币翻倍与普通继续");
            money=app.Store.Data.coins;RewardedAds.Register(new FakeProvider{result=RewardedOutcome.Completed,repeat=true});var twice=AdButton(RewardPlacement.DoubleCoins);twice.onClick.Invoke();
            CheckIt(app.Store.Data.coins==money+baseReward&&!twice.interactable,"翻倍仅补差额并锁定按钮，重复回调不重复发奖");
            yield return Capture("12-result-claimed.png","翻倍领取完成");
            CheckPolicy();RewardedAds.Register(null);AdPlacements.RegisterInterstitial(null);
        }
        void CheckPolicy()
        {
            var store=new SaveStore(Path.Combine(dir,"policy-profile.json"));store.Data=new PlayerProfile{level=23,wins=12};long now=1000000000;string reason;
            for(int i=0;i<3;i++){CheckIt(AdPlacements.CanReward(store,RewardPlacement.CoinSupply,now+i*61,out reason),"金币补给额度内可请求 "+i);AdPlacements.Record(store,"CoinSupply",now+i*61);}
            CheckIt(!AdPlacements.CanReward(store,RewardPlacement.CoinSupply,now+1000,out reason)&&reason==Zh.AdDailyLimit,"每日三次金币补给达到上限");
            CheckIt(AdPlacements.CanReward(store,RewardPlacement.CoinSupply,now+86400,out reason),"跨日恢复额度");
            CheckIt(new SaveStore(store.Path).Data.adUsage.Single(x=>x.placement=="CoinSupply").count==3,"广告次数和时间持久化");
            bool enabled=AdPlacements.Settings.interstitial.enabled;var fake=new FakeInterstitial();int next=0;
            AdPlacements.Settings.interstitial.enabled=true;AdPlacements.RegisterInterstitial(fake);store.Data.lastRewardAdAt=0;
            AdPlacements.TryLevelEnd(store,23,false,()=>next++);CheckIt(fake.shows==1&&next==1,"插屏只在自然结束点触发且重复关闭回调只前进一次");
            AdPlacements.TryLevelEnd(store,23,false,()=>next++);CheckIt(fake.shows==1&&next==2,"插屏180秒冷却期间直接继续");
            store.Data.lastInterstitialAt=0;store.Data.lastRewardAdAt=SaveStore.Now;AdPlacements.TryLevelEnd(store,23,false,()=>next++);CheckIt(fake.shows==1&&next==3,"刚看激励视频后不叠加插屏");
            store.Data.lastRewardAdAt=0;AdPlacements.TryLevelEnd(store,1,false,()=>next++);CheckIt(fake.shows==1,"新手关不触发插屏");
            AdPlacements.Settings.interstitial.enabled=enabled;AdPlacements.RegisterInterstitial(null);
            CheckIt(!AdPlacements.Settings.interstitial.enabled&&AdPlacements.Settings.rewarded.All(s=>string.IsNullOrEmpty(s.adUnitId)),"交付保持广告位ID留空、插屏默认关闭");
        }
    }
}
#endif

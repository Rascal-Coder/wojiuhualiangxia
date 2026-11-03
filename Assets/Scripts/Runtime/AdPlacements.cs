using System;
using System.Linq;
using UnityEngine;

namespace Inkomino
{
    [Serializable] public sealed class AdUsage { public string placement;public long day,lastAt;public int count; }
    [Serializable] public sealed class RewardSlot { public string placement,adUnitId="";public bool enabled=true;public int amount=1,dailyLimit=5,cooldownSeconds=45; }
    [Serializable] public sealed class InterstitialSlot { public bool enabled=false;public string adUnitId="";public int minLevel=6,everyWins=3,cooldownSeconds=180,dailyLimit=6,rewardQuietSeconds=120; }
    [Serializable] public sealed class AdSettings { public int rewardMinProgressLevel=3,dayResetUtcOffsetMinutes=480,toolDailyCap=9;public RewardSlot[] rewarded;public InterstitialSlot interstitial; }
    public interface IInterstitialAdProvider { bool IsAvailable();void Show(Action<bool> closed); }
    public static class AdPlacements
    {
        static AdSettings settings;static IInterstitialAdProvider interstitial;
        public static bool InterstitialShowing{get;private set;}
        public static event Action<string,string> Signal;
        public static AdSettings Settings {get {if(settings==null){var data=Resources.Load<TextAsset>("Data/monetization");if(!data)throw new InvalidOperationException("Missing monetization configuration");settings=JsonUtility.FromJson<AdSettings>(data.text);}return settings;}}
        public static RewardSlot Slot(RewardPlacement placement){return Settings.rewarded.FirstOrDefault(s=>s.placement==placement.ToString());}
        static readonly RewardPlacement[] ToolPlacements={RewardPlacement.Pen,RewardPlacement.Freeze,RewardPlacement.Lens};
        static bool IsToolPlacement(RewardPlacement placement){return placement==RewardPlacement.Pen||placement==RewardPlacement.Freeze||placement==RewardPlacement.Lens;}
        static long Day(long now){return (now+Settings.dayResetUtcOffsetMinutes*60L)/86400;}
        static AdUsage Usage(SaveStore store,string placement){if(store.Data.adUsage==null)store.Data.adUsage=new AdUsage[0];return store.Data.adUsage.FirstOrDefault(x=>x!=null&&x.placement==placement);}
        static int ToolCountToday(SaveStore store,long now)
        {
            long day=Day(now);int used=0;
            foreach(var tool in ToolPlacements){var u=Usage(store,tool.ToString());if(u!=null&&u.day==day)used+=u.count;}
            return used;
        }
        public static bool CanReward(SaveStore store,RewardPlacement placement,long now,out string reason)
        {
            reason=null;var slot=Slot(placement);if(slot==null||!slot.enabled){reason=Zh.AdUnavailable;return false;}
            if(store.Data.level<Settings.rewardMinProgressLevel){reason=Zh.AdUnavailable;return false;}
            if(RewardedAds.IsShowing||InterstitialShowing){reason=Zh.AdBusy;return false;}
            if(Settings.toolDailyCap>0&&IsToolPlacement(placement)&&ToolCountToday(store,now)>=Settings.toolDailyCap){reason=Zh.AdDailyLimit;return false;}
            var u=Usage(store,placement.ToString());if(u==null)return true;
            if(u.day==Day(now)&&u.count>=slot.dailyLimit){reason=Zh.AdDailyLimit;return false;}
            if(now-u.lastAt<slot.cooldownSeconds){reason=Zh.AdCooldown;return false;}return true;
        }
        public static void Record(SaveStore store,string placement,long now)
        {
            var u=Usage(store,placement);if(u==null){u=new AdUsage{placement=placement};store.Data.adUsage=store.Data.adUsage.Concat(new[]{u}).ToArray();}
            if(u.day!=Day(now)){u.day=Day(now);u.count=0;}u.count++;u.lastAt=now;
            if(placement=="LevelEnd")store.Data.lastInterstitialAt=now;else store.Data.lastRewardAdAt=now;
            store.Save();Emit(placement,"completed");
        }
        public static void Emit(string placement,string outcome){Signal?.Invoke(placement,outcome);}
        public static void RegisterInterstitial(IInterstitialAdProvider provider){if(InterstitialShowing)throw new InvalidOperationException("Interstitial active");interstitial=provider;}
        public static void TryLevelEnd(SaveStore store,int level,bool review,Action next)
        {
            var s=Settings.interstitial;long now=SaveStore.Now;var u=Usage(store,"LevelEnd");
            bool due=s!=null&&s.enabled&&!review&&level>=s.minLevel&&store.Data.wins>0&&store.Data.wins%Math.Max(1,s.everyWins)==0&&now-store.Data.lastInterstitialAt>=s.cooldownSeconds&&now-store.Data.lastRewardAdAt>=s.rewardQuietSeconds&&(u==null||u.day!=Day(now)||u.count<s.dailyLimit);
            if(!due||interstitial==null||RewardedAds.IsShowing||InterstitialShowing){next();return;}
            bool delivered=false;Action<bool> finish=shown=>{if(delivered)return;delivered=true;InterstitialShowing=false;if(shown)Record(store,"LevelEnd",SaveStore.Now);next();};
            try{if(!interstitial.IsAvailable()){finish(false);return;}InterstitialShowing=true;Emit("LevelEnd","requested");interstitial.Show(finish);}catch{if(!delivered)finish(false);else throw;}
        }
    }
}

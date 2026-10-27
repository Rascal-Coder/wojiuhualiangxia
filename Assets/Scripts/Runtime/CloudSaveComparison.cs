using System;
using System.Linq;
using UnityEngine;

namespace Inkomino
{
    // Compare game records at one instant, without mutating either saved copy.
    // Device preferences and expired eligibility clocks are not game progress.
    public static class CloudSaveComparison
    {
        public static PlayerProfile Parse(string json)
        {
            if(string.IsNullOrEmpty(json))return null;
            try {var p=JsonUtility.FromJson<PlayerProfile>(json);return SaveStore.ValidCloudProfile(p)?p:null;}
            catch{return null;}
        }
        public static PlayerProfile Normalize(PlayerProfile source,long now)
        {
            if(source==null)return null;
            var p=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(source));
            p.sound=p.music=p.haptics=p.reducedMenuMotion=false;
            p.bestStreak=Math.Max(p.bestStreak,p.streak);
            if(p.hearts<5&&p.regenAt>0&&now>=p.regenAt){
                int recovered=(int)Math.Min(5-p.hearts,1+(now-p.regenAt)/900);
                p.hearts+=recovered;p.regenAt+=recovered*900L;
            }
            if(p.hearts>=5)p.regenAt=0;
            if(p.dailyAt>0&&now>=p.dailyAt+86400)p.dailyAt=0;
            long day=(now+8*3600)/86400;
            if(p.shopDay!=day||p.shopPurchases==null||p.shopPurchases.Length!=3)p.shopPurchases=new int[3];
            p.shopDay=day;
            ClimbActivity.Refresh(p,now);
            p.climb.finalBots=p.climb.finalCount=0;p.climb.eliminated=new int[0];
            if(!p.climb.isOut||now>=p.climb.cooldownUntil)p.climb.cooldownUntil=0;
            if(p.resumeLevel<=0||p.resumePieces==null){p.resumeLevel=0;p.resumeTime=0;p.resumePieces=null;}
            else if(p.resumePieces!=null)foreach(var piece in p.resumePieces)if(piece!=null){
                piece.appearanceSaved=false;piece.colorIndex=piece.textureIndex=0;
                piece.textureOffsetX=piece.textureOffsetY=0;
                if(piece.cells!=null)piece.cells=piece.cells.OrderBy(c=>c).ToArray();
            }
            if(p.resumeLevel>0)p.resumeTime=Mathf.Ceil(p.resumeTime);
            if(p.resumePieces!=null)p.resumePieces=p.resumePieces.OrderBy(piece=>piece==null?"":piece.shape+":"+string.Join(",",piece.cells??new int[0])+":"+piece.locked+":"+piece.tutLock,StringComparer.Ordinal).ToArray();
            var ads=AdPlacements.Settings;long adDay=(now+ads.dayResetUtcOffsetMinutes*60L)/86400;
            p.adUsage=(p.adUsage??new AdUsage[0]).Where(u=>u!=null).Select(u=>{
                var slot=(ads.rewarded??new RewardSlot[0]).FirstOrDefault(s=>s.placement==u.placement);
                bool known=slot!=null||u.placement=="LevelEnd";
                if(known){
                    int cooldown=slot!=null?slot.cooldownSeconds:ads.interstitial==null?0:ads.interstitial.cooldownSeconds;
                    if(u.day!=adDay){u.day=adDay;u.count=0;}
                    if(now-u.lastAt>=cooldown)u.lastAt=0;
                }
                return u;
            }).Where(u=>u.count!=0||u.lastAt!=0).OrderBy(u=>u.placement,StringComparer.Ordinal).ToArray();
            if(ads.interstitial!=null){
                if(now-p.lastRewardAdAt>=ads.interstitial.rewardQuietSeconds)p.lastRewardAdAt=0;
                if(now-p.lastInterstitialAt>=ads.interstitial.cooldownSeconds)p.lastInterstitialAt=0;
            }
            return p;
        }
        static string Key(PlayerProfile p,long now,bool includeAvatar)
        {
            var normalized=Normalize(p,now);if(normalized==null)return "";
            if(!includeAvatar)normalized.avatar=0;
            return JsonUtility.ToJson(normalized);
        }
        public static bool GameplayEqual(PlayerProfile a,PlayerProfile b,long now){return Key(a,now,false)==Key(b,now,false);}
        public static bool SyncEqual(PlayerProfile a,PlayerProfile b,long now){return Key(a,now,true)==Key(b,now,true);}
        public static void KeepDevicePreferences(PlayerProfile local,PlayerProfile restored)
        {
            restored.sound=local.sound;restored.music=local.music;
            restored.haptics=local.haptics;restored.reducedMenuMotion=local.reducedMenuMotion;
        }
    }
}

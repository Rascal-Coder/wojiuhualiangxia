using System;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    public partial class InkominoApp
    {
        int rewardedContinues;
        public static string RewardCountdown(long seconds){seconds=Math.Max(0,seconds);long minutes=seconds/60+(seconds%60>0?1:0);return string.Format("{0:00}:{1:00}",minutes/60,minutes%60);}
        bool RewardEntryEnabled(RewardPlacement placement){var slot=AdPlacements.Slot(placement);return slot!=null&&slot.enabled&&Store.Data.level>=AdPlacements.Settings.rewardMinProgressLevel;}
        string RewardLabel(RewardPlacement placement)
        {
            int amount=AdPlacements.Slot(placement).amount;
            return placement==RewardPlacement.ContinueLevel?"看视频 +"+amount+"秒":placement==RewardPlacement.Life?"看视频 +"+amount+"生命":placement==RewardPlacement.CoinSupply?"看视频 +"+amount+"金币":placement==RewardPlacement.DoubleCoins?(amount==2?Zh.AdDouble:"看视频 金币×"+amount):"看视频 领取"+amount+"个";
        }
        void UpdateHomeTimers()
        {
            if(Store==null||sourceEventLabels==null)return;
            if(sourceEventLabels[0]){long sec=Store.DailyGiftSeconds(SaveStore.Now);sourceEventLabels[0].text=sec==0?Zh.GiftReady:RewardCountdown(sec);}
            if(sourceEventLabels[3])sourceEventLabels[3].text=ClimbHomeLabel();
        }
        Button RewardButton(RewardPlacement placement,string text,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Action action,bool menu=true)
        {
            var button=menu?MenuUI.GameButton("",parent,anchor,pos,size,action,48,false):Skin.SourceButton("",parent,anchor,pos,size,action,"purple",42);button.name="IAA "+placement;
            float icon=size.y*.58f;
            Skin.Art("game_assets_rewardedvideo",button.transform,new Vector2(.5f,.5f),new Vector2(-size.x*.5f+icon*.85f,0),new Vector2(icon*1.45f,icon));
            var label=Skin.Text(text,button.transform,new Vector2(.5f,.5f),new Vector2(icon*.48f,0),new Vector2(size.x-icon*1.85f,size.y*.65f),Mathf.RoundToInt(Mathf.Min(53,size.y*.25f)),menu?MenuUI.Ink:Color.white);
            label.name="Rewarded CTA label";return button;
        }
        void RequestReward(RewardPlacement placement,Action grant,Func<bool> context=null)
        {
            string reason;if(!AdPlacements.CanReward(Store,placement,SaveStore.Now,out reason)){Info(Zh.AdTitle,reason);return;}
            int epoch=presentationEpoch;var store=Store;AdPlacements.Emit(placement.ToString(),"requested");
            RewardedAds.ShowForReward(placement,()=>{
                if(this==null||Store!=store||(context!=null&&!context())){AdPlacements.Emit(placement.ToString(),"stale_context");return;}
                grant();AdPlacements.Record(store,placement.ToString(),SaveStore.Now);
            },outcome=>{
                if(outcome==RewardedOutcome.Completed)return;AdPlacements.Emit(placement.ToString(),outcome.ToString());
                if(this==null||epoch!=presentationEpoch)return;
                Info(Zh.AdTitle,outcome==RewardedOutcome.Skipped?Zh.AdSkipped:outcome==RewardedOutcome.Busy?Zh.AdBusy:outcome==RewardedOutcome.Failed?Zh.AdFailed:Zh.AdUnavailable);
            });
        }
        void RewardBooster(int index,RectTransform panel)
        {
            RewardPlacement placement=index==0?RewardPlacement.Pen:index==1?RewardPlacement.Freeze:RewardPlacement.Lens;
            RequestReward(placement,()=>{int amount=AdPlacements.Slot(placement).amount;if(index==0)Store.Data.hint+=amount;else if(index==1)Store.Data.freeze+=amount;else Store.Data.lens+=amount;if(panel&&panel.gameObject.activeInHierarchy){ClosePopup();if(Playing)BoosterCounts();}});
        }
        void RewardLife(RectTransform panel)
        {
            RequestReward(RewardPlacement.Life,()=>{Store.Data.hearts=Mathf.Min(5,Store.Data.hearts+AdPlacements.Slot(RewardPlacement.Life).amount);if(panel&&panel.gameObject.activeInHierarchy){ClosePopup();CoreLivesPopup();}});
        }
        void RewardCoinSupply(){RequestReward(RewardPlacement.CoinSupply,()=>{Store.Data.coins+=AdPlacements.Slot(RewardPlacement.CoinSupply).amount;SoundBank.Play("coin");Feel.Success();});}
    }
}

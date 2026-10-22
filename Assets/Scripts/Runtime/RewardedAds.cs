using System;

namespace Inkomino
{
    public enum RewardPlacement { ContinueLevel, Pen, Freeze, Lens, Life, DoubleCoins, CoinSupply }
    public enum RewardedOutcome { Completed, Skipped, Unavailable, Failed, Busy }

    // A future WeChat SDK adapter registers here. No SDK, ad-unit identifiers,
    // payment API, simulated playback or automatic rewards ship in this build.
    public interface IRewardedAdProvider
    {
        bool IsAvailable(RewardPlacement placement);
        void Show(RewardPlacement placement,Action<RewardedOutcome> closed);
    }
    public static class RewardedAds
    {
        static IRewardedAdProvider provider;
        public static bool IsShowing { get; private set; }
        public static void Register(IRewardedAdProvider adapter){if(IsShowing)throw new InvalidOperationException("Cannot replace an active ad provider.");provider=adapter;}
        public static bool IsAvailable(RewardPlacement placement){return !IsShowing&&provider!=null&&provider.IsAvailable(placement);}
        public static void ShowForReward(RewardPlacement placement,Action grantReward,Action<RewardedOutcome> closed=null)
        {
            if(IsShowing){closed?.Invoke(RewardedOutcome.Busy);return;}
            if(!IsAvailable(placement)){closed?.Invoke(RewardedOutcome.Unavailable);return;}
            IsShowing=true;bool delivered=false;
            Action<RewardedOutcome> finish=result=>{
                if(delivered)return;delivered=true;IsShowing=false;
                try{if(result==RewardedOutcome.Completed)grantReward?.Invoke();}
                finally{closed?.Invoke(result);}
            };
            try{provider.Show(placement,finish);}catch{if(delivered)throw;finish(RewardedOutcome.Failed);}
        }
    }
}

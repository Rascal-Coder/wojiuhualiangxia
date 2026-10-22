using System;
using System.Linq;
using UnityEngine;

namespace Inkomino
{
    [Serializable] public sealed class ClimbSegment {public int[] finalDist;}
    [Serializable] public sealed class ClimbConfig
    {
        public bool enabled=true;public int unlockLevel=41,stages=7,roster=100,prizePool=1000,payoutRoundTo=5,retrySeconds=1800,resetHourUtc=10;
        public int[] stepWeights,segmentCycle;public ClimbSegment[] segments;
    }
    [Serializable] public sealed class ClimbState
    {
        public long windowStart,cooldownUntil;public bool joined,isOut,claimed;
        public int attempt,stage,startLevel,finalBots,finalCount,summits;
        public int[] eliminated=new int[7];
    }
    // Port of retained modules2091/2092. Opponents are locally seeded; this
    // activity does not share its simulated roster with the real friend board.
    public static class ClimbActivity
    {
        static ClimbConfig config;
        public static ClimbConfig Config {get{if(config==null)config=JsonUtility.FromJson<ClimbConfig>(Resources.Load<TextAsset>("Data/climb").text);return config;}}
        public static long WindowStart(long now){long shifted=now-Config.resetHourUtc*3600L;return (long)Math.Floor(shifted/86400d)*86400+Config.resetHourUtc*3600L;}
        public static bool Refresh(PlayerProfile profile,long now)
        {
            var state=profile.climb;long window=WindowStart(now);
            if(state==null||state.windowStart!=window){profile.climb=new ClimbState{windowStart=window,summits=state==null?0:Math.Max(0,state.summits)};return true;}
            if(state.eliminated==null||state.eliminated.Length!=Config.stages)state.eliminated=new int[Config.stages];
            return false;
        }
        public static bool Unlocked(PlayerProfile profile){return Config.enabled&&profile.level>=Config.unlockLevel;}
        public static int AliveBots(ClimbState state){return Math.Max(0,Config.roster-1-(state.eliminated==null?0:state.eliminated.Sum()));}
        public static int Alive(ClimbState state){return AliveBots(state)+(state.isOut?0:1);}
        public static bool AtTop(ClimbState state){return state.joined&&!state.isOut&&state.stage>=Config.stages;}
        public static int Prize(ClimbState state)
        {int count=state.finalCount>0?state.finalCount:Math.Max(1,Alive(state));double amount=Config.prizePool/(double)count;return Config.payoutRoundTo>0?Config.payoutRoundTo*(int)Math.Ceiling(amount/Config.payoutRoundTo):(int)Math.Ceiling(amount);}
        public static bool Join(SaveStore store,long now)
        {
            Refresh(store.Data,now);var state=store.Data.climb;
            if(!Unlocked(store.Data)||state.joined&&(!state.isOut||now<state.cooldownUntil))return false;
            int attempt=state.joined?state.attempt+1:1;
            state=new ClimbState{windowStart=state.windowStart,summits=state.summits,attempt=attempt,joined=true,eliminated=new int[Config.stages]};
            var rng=new Random32(AttemptSeed(state)^668265263u);
            int segment=Config.segmentCycle[Math.Max(0,state.summits)%Config.segmentCycle.Length];
            var weights=Config.segments[Mathf.Clamp(segment,0,Config.segments.Length-1)].finalDist;
            double choice=rng.Next()*weights.Sum();int selected=weights.Length-1;
            for(int i=0;i<weights.Length;i++){choice-=weights[i];if(choice<0){selected=i;break;}}
            state.finalBots=Mathf.Clamp(selected,0,Config.roster-1);store.Data.climb=state;store.Save();return true;
        }
        public static void Advance(PlayerProfile profile,LevelData level,GameData catalog,long now)
        {
            Refresh(profile,now);var state=profile.climb;
            if(!Unlocked(profile)||level.id<Config.unlockLevel||!state.joined||state.isOut||state.stage>=Config.stages)return;
            if(state.startLevel==0)state.startLevel=level.id;
            var weights=new double[Config.stages];
            for(int i=0;i<weights.Length;i++)
            {
                string difficulty=level.difficulty;
                if(catalog!=null)difficulty=catalog.levels[Mathf.Clamp(state.startLevel+i-1,0,catalog.levels.Length-1)].difficulty;
                int multiple=difficulty=="ULTRA_HARD"?8:difficulty=="EXTRA_HARD"?6:difficulty=="HARD"?4:2;
                weights[i]=Config.stepWeights[i]*multiple;
            }
            int bound=Math.Max(0,AliveBots(state)-state.finalBots);double remaining=weights.Skip(state.stage).Sum();
            int drop=remaining>0?(int)Math.Ceiling(weights[state.stage]/remaining*bound):(state.stage==Config.stages-1?bound:0);
            state.eliminated[state.stage]=Mathf.Clamp(drop,0,bound);state.stage++;
            if(state.stage>=Config.stages){state.finalCount=AliveBots(state)+1;state.summits++;}
        }
        public static void Fail(PlayerProfile profile,long now)
        {
            Refresh(profile,now);var state=profile.climb;
            if(!Unlocked(profile)||!state.joined||state.isOut||state.stage>=Config.stages)return;
            state.isOut=true;state.cooldownUntil=now+Config.retrySeconds;
        }
        public static int Claim(SaveStore store,long now)
        {
            Refresh(store.Data,now);var state=store.Data.climb;
            if(!AtTop(state)||state.claimed)return 0;
            int amount=Prize(state);state.claimed=true;store.Data.coins+=amount;store.Save();return amount;
        }
        public static uint AttemptSeed(ClimbState state){return unchecked(((uint)state.windowStart^2654435769u)+(uint)Math.Max(0,state.attempt)*1663821227u);}
        public struct Random32
        {
            uint seed;public Random32(uint value){seed=value;}
            public double Next(){unchecked{uint t=seed+=0x6D2B79F5u;t=(t^(t>>15))*(t|1);t^=t+((t^(t>>7))*(t|61));return (t^(t>>14))/4294967296d;}}
        }
    }
}

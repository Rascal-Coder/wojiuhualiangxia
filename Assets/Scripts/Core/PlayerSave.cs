using System;
using System.IO;
using UnityEngine;

namespace Inkomino
{
    [Serializable] public class PlayerProfile
    {
        public int schema=1,level=1,coins=100,hearts=5,hint=3,freeze=3,lens=3,wins=0,streak=0,avatar=1;
        public bool sound=true,music=true,haptics=true,drawIntroSeen=false,firstContinueUsed=false,reducedMenuMotion=false;
        public long regenAt,dailyAt,lastRewardAdAt,lastInterstitialAt;
        public int boosterIntroSeen,barrierIntroSeen;
        public long shopDay;public int[] shopPurchases=new int[3];
        public AdUsage[] adUsage=new AdUsage[0];public bool[] completed=new bool[500];
        public int resumeLevel;public float resumeTime;public PieceData[] resumePieces;
        public int bestStreak,climbPendingFailure;public ClimbState climb=new ClimbState();
    }
    public sealed class SaveStore
    {
        public PlayerProfile Data;public readonly string Path;
        public bool PersistenceAvailable{get;private set;}=true;
        public event Action Changed;
        // Original AppContent routes maxLevel==0 straight into the first board.
        // A profile can exist before any level has been completed.
        public bool HasCompletedLevel{get{return Data.level>1||Data.wins>0||Array.Exists(Data.completed,x=>x);}}
        public static long Now{get{return DateTimeOffset.UtcNow.ToUnixTimeSeconds();}}
        const string StorageKey="inkomino-profile-v1";
        public SaveStore(string path){Path=path;try{
#if WEIXINMINIGAME && !UNITY_EDITOR
            string json=WeChatProfileStorage.Read(StorageKey);
            if(string.IsNullOrEmpty(json))
            {
                // Preserve an earlier PlayerPrefs profile when one is available.
                json=PlayerPrefs.GetString(StorageKey,"");
                if(!string.IsNullOrEmpty(json))WeChatProfileStorage.Write(StorageKey,json);
            }
            Data=string.IsNullOrEmpty(json)?new PlayerProfile():JsonUtility.FromJson<PlayerProfile>(json);
#elif UNITY_WEBGL && !UNITY_EDITOR
            Data=JsonUtility.FromJson<PlayerProfile>(PlayerPrefs.GetString(StorageKey,""));
#else
            Data=File.Exists(Path)?JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(Path)):new PlayerProfile();
#endif
            }catch(Exception error){PersistenceAvailable=false;Data=new PlayerProfile();Debug.LogError("Profile read failed; existing save is protected: "+error.Message);}if(Data==null)Data=new PlayerProfile();if(Data.completed==null||Data.completed.Length!=500)Data.completed=new bool[500];Data.level=Mathf.Clamp(Data.level,1,500);Data.hearts=Mathf.Clamp(Data.hearts,0,5);Data.bestStreak=Math.Max(Data.bestStreak,Data.streak);ClimbActivity.Refresh(Data,Now);if(Data.regenAt<=0)Data.regenAt=Now+900;Recover();if(Data.climbPendingFailure>0){int failedLevel=Data.climbPendingFailure;Data.climbPendingFailure=0;LoseHeart(failedLevel);Data.resumeLevel=0;Data.resumePieces=null;Save();}}
        public bool Recover(){return RecoverAt(Now);}
        public long DailyGiftSeconds(long now){return Data.dailyAt<=0?0:Math.Max(0,Data.dailyAt+86400-now);}
        public bool ClaimDailyGift(long now){if(DailyGiftSeconds(now)>0)return false;Data.coins+=100;Data.freeze++;Data.dailyAt=now;Save();return true;}
        public bool RecoverAt(long now){if(Data.hearts>=5){Data.regenAt=now+900;return false;}if(now<Data.regenAt)return false;int amount=(int)Math.Min(5-Data.hearts,1+(now-Data.regenAt)/900);Data.hearts+=amount;Data.regenAt=Data.hearts>=5?now+900:Data.regenAt+amount*900;Save();return true;}
        public void LoseHeart(int levelId=0){Recover();if(levelId>=ClimbActivity.Config.unlockLevel)ClimbActivity.Fail(Data,Now);Data.climbPendingFailure=0;if(Data.hearts==5)Data.regenAt=Now+900;Data.hearts=Math.Max(0,Data.hearts-1);Data.streak=0;Save();}
        public int Complete(LevelData level,GameData catalog=null){int reward=0;if(!Data.completed[level.id-1]){int multi=level.difficulty=="ULTRA_HARD"?7:level.difficulty=="EXTRA_HARD"?5:level.difficulty=="HARD"?3:1;reward=5*multi;Data.coins+=reward;Data.completed[level.id-1]=true;Data.wins++;Data.streak++;Data.bestStreak=Math.Max(Data.bestStreak,Data.streak);}if(catalog!=null)ClimbActivity.Advance(Data,level,catalog,Now);if(level.id==Data.level)Data.level=Math.Min(500,Data.level+1);Data.resumeLevel=0;Data.resumePieces=null;Data.climbPendingFailure=0;Save();return reward;}
        public void Save(){if(!PersistenceAvailable)return;try{
#if WEIXINMINIGAME && !UNITY_EDITOR
            WeChatProfileStorage.Write(StorageKey,JsonUtility.ToJson(Data));
#elif UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.SetString(StorageKey,JsonUtility.ToJson(Data));PlayerPrefs.Save();
#else
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));string tmp=Path+".tmp";File.WriteAllText(tmp,JsonUtility.ToJson(Data,true));if(File.Exists(Path)){if(File.Exists(Path+".bak"))File.Delete(Path+".bak");File.Replace(tmp,Path,Path+".bak");}else File.Move(tmp,Path);
#endif
            }catch(Exception e){Debug.LogError("Save failed: "+e.Message);}Changed?.Invoke();}
        public const int ShopDailyLimit=3,ShopStockLimit=30;
        static int ShopIndex(string item){return item=="hint"?0:item=="freeze"?1:item=="lens"?2:-1;}
        public int ShopRemaining(string item,long now)
        {
            int index=ShopIndex(item);if(index<0)return 0;long day=(now+8*3600)/86400;
            if(Data.shopDay!=day||Data.shopPurchases==null||Data.shopPurchases.Length!=3){Data.shopDay=day;Data.shopPurchases=new int[3];}
            return Math.Max(0,ShopDailyLimit-Data.shopPurchases[index]);
        }
        public string ShopError(string item,int cost,long now)
        {
            int index=ShopIndex(item);if(index<0||cost!=(index==1?120:150))return "商品暂不可用";
            if(ShopRemaining(item,now)==0)return "今日已售罄";
            int owned=index==0?Data.hint:index==1?Data.freeze:Data.lens;
            if(owned>ShopStockLimit-3)return "背包已满";
            if(Data.coins<cost)return "金币不足";return "";
        }
        public bool Buy(string item,int cost){return BuyAt(item,cost,Now);}
        public bool BuyAt(string item,int cost,long now)
        {
            if(ShopError(item,cost,now).Length>0)return false;int index=ShopIndex(item);
            Data.coins-=cost;if(index==0)Data.hint+=3;else if(index==1)Data.freeze+=3;else Data.lens+=3;
            Data.shopPurchases[index]++;Save();return true;
        }

    }
}

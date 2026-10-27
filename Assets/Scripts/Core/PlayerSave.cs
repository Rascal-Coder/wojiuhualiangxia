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
        public bool LastWriteSucceeded{get;private set;}
        // This describes the record read at construction, before recovery or
        // startup saves. A saved first-level board is already a local profile.
        public bool HasLocalProfile{get;private set;}
        public event Action Changed;
        public event Action Persisted;
        // Original AppContent routes maxLevel==0 straight into the first board.
        // A profile can exist before any level has been completed.
        public bool HasCompletedLevel{get{return Data.level>1||Data.wins>0||Array.Exists(Data.completed,x=>x);}}
        public static long Now{get{return DateTimeOffset.UtcNow.ToUnixTimeSeconds();}}
        const string StorageKey="inkomino-profile-v1";
        [Serializable] sealed class StoredProfileHeader {public int schema,level;public bool[] completed;}
        public SaveStore(string path){Path=path;try{
            string json;
#if WEIXINMINIGAME && !UNITY_EDITOR
            json=WeChatProfileStorage.Read(StorageKey);
            if(string.IsNullOrWhiteSpace(json))
            {
                // Preserve an earlier PlayerPrefs profile when one is available.
                json=PlayerPrefs.GetString(StorageKey,"");
                if(!string.IsNullOrEmpty(json))WeChatProfileStorage.Write(StorageKey,json);
            }
#elif UNITY_WEBGL && !UNITY_EDITOR
            json=PlayerPrefs.GetString(StorageKey,"");
#else
            json=File.Exists(Path)?File.ReadAllText(Path):"";
#endif
            if(string.IsNullOrWhiteSpace(json))Data=new PlayerProfile();
            else{
                // Missing fields must not be supplied by PlayerProfile defaults
                // and mistaken for a real record (for example, an empty object).
                var header=JsonUtility.FromJson<StoredProfileHeader>(json);
                Data=JsonUtility.FromJson<PlayerProfile>(json);
                if(header==null||header.schema!=1||header.level<1||header.completed==null||header.completed.Length!=500||!ValidCloudProfile(Data))throw new InvalidDataException("本机存档格式无效");
                HasLocalProfile=true;
            }
            }catch(Exception error){PersistenceAvailable=false;Data=new PlayerProfile();Debug.LogError("Profile read failed; existing save is protected: "+error.Message);}if(Data==null)Data=new PlayerProfile();if(Data.completed==null||Data.completed.Length!=500)Data.completed=new bool[500];Data.level=Mathf.Clamp(Data.level,1,500);Data.hearts=Mathf.Clamp(Data.hearts,0,5);Data.bestStreak=Math.Max(Data.bestStreak,Data.streak);ClimbActivity.Refresh(Data,Now);if(Data.regenAt<=0)Data.regenAt=Now+900;Recover();if(Data.climbPendingFailure>0){int failedLevel=Data.climbPendingFailure;Data.climbPendingFailure=0;LoseHeart(failedLevel);Data.resumeLevel=0;Data.resumePieces=null;Save();}}
        public bool Recover(){return RecoverAt(Now);}
        public long DailyGiftSeconds(long now){return Data.dailyAt<=0?0:Math.Max(0,Data.dailyAt+86400-now);}
        public bool ClaimDailyGift(long now){if(DailyGiftSeconds(now)>0)return false;Data.coins+=100;Data.freeze++;Data.dailyAt=now;Save();return true;}
        public bool RecoverAt(long now){if(Data.hearts>=5)return false;if(now<Data.regenAt)return false;int amount=(int)Math.Min(5-Data.hearts,1+(now-Data.regenAt)/900);Data.hearts+=amount;Data.regenAt=Data.hearts>=5?now+900:Data.regenAt+amount*900;Save();return true;}
        public void LoseHeart(int levelId=0){Recover();if(levelId>=ClimbActivity.Config.unlockLevel)ClimbActivity.Fail(Data,Now);Data.climbPendingFailure=0;if(Data.hearts==5)Data.regenAt=Now+900;Data.hearts=Math.Max(0,Data.hearts-1);Data.streak=0;Save();}
        public int Complete(LevelData level,GameData catalog=null){int reward=0;if(!Data.completed[level.id-1]){int multi=level.difficulty=="ULTRA_HARD"?7:level.difficulty=="EXTRA_HARD"?5:level.difficulty=="HARD"?3:1;reward=5*multi;Data.coins+=reward;Data.completed[level.id-1]=true;Data.wins++;Data.streak++;Data.bestStreak=Math.Max(Data.bestStreak,Data.streak);}if(catalog!=null)ClimbActivity.Advance(Data,level,catalog,Now);if(level.id==Data.level)Data.level=Math.Min(500,Data.level+1);Data.resumeLevel=0;Data.resumePieces=null;Data.climbPendingFailure=0;Save();return reward;}
        public void Save(){LastWriteSucceeded=false;if(!PersistenceAvailable)return;try{
#if WEIXINMINIGAME && !UNITY_EDITOR
            WeChatProfileStorage.Write(StorageKey,JsonUtility.ToJson(Data));
#elif UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.SetString(StorageKey,JsonUtility.ToJson(Data));PlayerPrefs.Save();
#else
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));string tmp=Path+".tmp";File.WriteAllText(tmp,JsonUtility.ToJson(Data,true));if(File.Exists(Path)){if(File.Exists(Path+".bak"))File.Delete(Path+".bak");File.Replace(tmp,Path,Path+".bak");}else File.Move(tmp,Path);
#endif
            LastWriteSucceeded=true;Persisted?.Invoke();
            }catch(Exception e){Debug.LogError("Save failed: "+e.Message);}Changed?.Invoke();}
        public static bool ValidCloudProfile(PlayerProfile p)
        {return p!=null&&p.schema==1&&p.level>=1&&p.level<=500&&p.coins>=0&&p.hearts>=0&&p.hearts<=5&&p.hint>=0&&p.freeze>=0&&p.lens>=0&&p.completed!=null&&p.completed.Length==500&&p.wins>=0&&p.wins<=500;}
        public bool ApplyCloudProfile(PlayerProfile profile)
        {
            if(!PersistenceAvailable||!ValidCloudProfile(profile))return false;
            var previous=Data;
            try{
                Data=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));
                Data.bestStreak=Math.Max(Data.bestStreak,Data.streak);ClimbActivity.Refresh(Data,Now);
                if(Data.shopPurchases==null||Data.shopPurchases.Length!=3)Data.shopPurchases=new int[3];
                if(Data.regenAt<=0)Data.regenAt=Now+900;
                if(Data.climbPendingFailure>0){int failed=Data.climbPendingFailure;Data.climbPendingFailure=0;LoseHeart(failed);Data.resumeLevel=0;Data.resumePieces=null;}
                Recover();Save();if(!LastWriteSucceeded){Data=previous;return false;}return true;
            }catch{Data=previous;return false;}
        }
        public const int ShopDailyLimit=3,ShopQuantityLimit=3;
        static int ShopIndex(string item){return item=="hint"?0:item=="freeze"?1:item=="lens"?2:-1;}
        public static int ShopPrice(string item,int quantity){return ShopIndex(item)<0||quantity<1||quantity>ShopQuantityLimit?-1:(item=="freeze"?40:50)*quantity;}
        public int ShopRemaining(string item,long now)
        {
            int index=ShopIndex(item);if(index<0)return 0;long day=(now+8*3600)/86400;
            if(Data.shopDay!=day||Data.shopPurchases==null||Data.shopPurchases.Length!=3){Data.shopDay=day;Data.shopPurchases=new int[3];}
            return Math.Max(0,ShopDailyLimit-Data.shopPurchases[index]);
        }
        public string ShopError(string item,int cost,long now,int quantity=3)
        {
            int index=ShopIndex(item);if(index<0||quantity<1||quantity>ShopQuantityLimit||cost!=ShopPrice(item,quantity))return "商品暂不可用";
            if(ShopRemaining(item,now)==0)return "今日已售罄";
            int owned=index==0?Data.hint:index==1?Data.freeze:Data.lens;
            if(owned>int.MaxValue-quantity)return "商品暂不可用";
            if(Data.coins<cost)return "金币不足";return "";
        }
        public bool Buy(string item,int cost,int quantity=3){return BuyAt(item,cost,Now,quantity);}
        public bool BuyAt(string item,int cost,long now,int quantity=3)
        {
            if(ShopError(item,cost,now,quantity).Length>0)return false;int index=ShopIndex(item);
            Data.coins-=cost;if(index==0)Data.hint+=quantity;else if(index==1)Data.freeze+=quantity;else Data.lens+=quantity;
            Data.shopPurchases[index]++;Save();return true;
        }

    }
}

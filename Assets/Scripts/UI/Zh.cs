using System.Globalization;

namespace Inkomino
{
    // Simplified Chinese game copy. Identifiers describe meaning, not source English.
    public static class Zh
    {
        public const string GameName="我就划两下";
        public const string Ok="确定";
        public const string LevelLibrary="关卡目录";
        public const string LibraryDescription="自由挑战全部 500 关";
        public const string DailyGift="每日礼物";
        public const string DailyContents="100 金币 + 1 个冰冰饮";
        public const string Tomorrow="明天再来领取吧！";
        public const string Claim="领取";
        public const string GiftReady="可领取",EventClosed="暂未开放";
        public const string AdTitle="视频奖励",AdUnavailable="视频暂未就绪，请稍后再试",AdSkipped="完整观看视频后可领取奖励",AdFailed="视频加载失败，请稍后再试",AdBusy="视频正在播放，请稍候";
        public const string AdDailyLimit="今天的奖励次数已用完，明天再来",AdCooldown="请稍等片刻，再来领取奖励";
        public const string AdLife="看视频 +1生命",AdBooster="看视频 领取1个",AdContinue="看视频 +60秒",AdDouble="看视频 金币翻倍",AdDoubled="已翻倍",AdCoins="看视频 +50金币";
        public const string Profile="个人资料";
        public const string ChangeAvatar="更换头像";
        public const string Shop="灵感补给站";
        public const string MagicPen="涂鸦笔";
        public const string FreezeTime="冰冰饮";
        public const string Lens="灵感镜";
        public const string PurchaseReady="兑换成功";
        public const string PurchaseBody="已获得 3 个道具。";
        public const string MoreCoins="金币不足";
        public const string EarnCoins="完成关卡即可赚取金币。";
        public const string ShopFooter="完成拼图，收集金币";
        public const string MoreBoosters="补充道具";
        public const string Collection="我的成就";
        public const string ComingSoon="敬请期待";
        public const string Play="开始游戏";
        public const string LivesFull="已满";
        public const string Home="首页";
        public const string Rank="成就";
        public const string Paused="已暂停";
        public const string Settings="设置";
        public const string Resume="继续游戏";
        public const string Done="完成";
        public const string Version="版本 1.0.0（1）";
        public const string Lives="生命";
        public const string LivesFullTitle="生命已满！";
        public const string NextLife="距离恢复下一颗生命";
        public const string Refill="补满生命";
        public const string Restart="重新开始";
        public const string QuitTitle="退出关卡？";
        public const string Retry="重新开始";
        public const string Cancel="取消";
        public const string Exit="退出";
        public const string WelcomeBack="欢迎回来！";
        public const string Unfinished="你有尚未完成的关卡。\n要继续上次的进度吗？";
        public const string Quit="放弃";
        public const string Continue="继续";
        public const string TimeUp="时间到了！";
        public const string ExtraTime="+60秒";
        public const string Free="免费继续";
        public const string PlayOn="继续挑战";
        public const string ProgressLost="当前关卡进度将会丢失！";
        public const string WellDone="太棒了！";
        public const string DrawShapes="画出目标图形";
        public const string DrawBody="手指划过相邻的空格，画出上方的目标图形。";
        public const string YourTurn="轮到你了！";
        public const string FollowHand="跟着画笔提示，画出图形。";
        public const string TryYourself="自己试试吧！";
        public const string RemainingShapes="现在请独立画出剩下的图形。";
        public const string LastShape="现在请独立画出最后一个图形。";
        public const string RemoveForRoom="试着移除一块拼图，腾出空间";
        public const string PieceRemoved="已移除拼图";
        public const string DrawShape="画出图形";
        public const string SpaceFilled="这里已被占用";
        public const string ConnectSquares="请连接相邻的方格";
        public const string MatchTarget="请画出上方的目标图形";
        public const string NoCrossWall="图形不能跨越隔墙";
        public const string SameAtWall="等号两侧需要相同图形";
        public const string DifferentAtWall="此隔墙两侧需要不同图形";
        public const string EverySymbol="每个图形都要包含每种符号各一个";
        public const string NumberArea="图形格数需与格内数字一致";
        public static string Level(int value){return string.Format(CultureInfo.GetCultureInfo("zh-CN"),"第 {0} 关",value);}
        public static string Unlock(int value){return string.Format(CultureInfo.GetCultureInfo("zh-CN"),"第 {0} 关解锁",value);}
        public static string UnlockBadge(int value){return string.Format(CultureInfo.GetCultureInfo("zh-CN"),"{0}关",value);}
        public static string Price(int value){return string.Format(CultureInfo.GetCultureInfo("zh-CN"),"{0} 金币",value);}
        public static string BoosterPrice(int value){return string.Format(CultureInfo.GetCultureInfo("zh-CN"),"3个  {0} 金币",value);}
        public static string BoosterContents(int index){return string.Format(CultureInfo.GetCultureInfo("zh-CN"),"{0}\n3个",index==0?MagicPen:index==1?FreezeTime:Lens);}
        public static string ProfileBody(int level,int wins){return string.Format(CultureInfo.GetCultureInfo("zh-CN"),"拼图艺术家\n第 {0} 关\n已完成 {1} 幅拼图",level,wins);}
        public static string CollectionBody(int wins,int level,int streak){return string.Format(CultureInfo.GetCultureInfo("zh-CN"),"已完成 {0} / 500 幅拼图\n\n最高进度：第 {1} 关\n连续通关：{2} 关",wins,level,streak);}
    }
}

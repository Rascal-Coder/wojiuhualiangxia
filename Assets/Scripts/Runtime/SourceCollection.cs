using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    public partial class InkominoApp
    {
        public void ShowCollection(){ShowAchievements(false);}
        public void ShowAchievements(bool friends)
        {
            Playing=false;Review=false;screen="collection";ClearScreen("game_assets_shopbg",friends?"friends":"collection");Wallet();int first=Content.childCount;
            MenuUI.Title("成就与好友",null,Content);
            MenuUI.Tabs(Content,friends,()=>ShowAchievements(false),()=>ShowAchievements(true));
            if(friends)FriendsPage();else AchievementsPage();
            MenuUI.Adapt(Content,headerLayout,first,2085);Navigation(2);FocusFirst();
        }
        void AchievementsPage()
        {
            var summary=MenuUI.Paper(Content,new Vector2(.5f,1),new Vector2(0,-995),new Vector2(960,270),true);summary.name="Achievement summary";
            MenuUI.Art(StickerDesign.Achievement(Store.Data.wins),summary,new Vector2(0,.5f),new Vector2(130,0),Vector2.one*190);
            MenuUI.Label("已通关",summary,new Vector2(0,.5f),new Vector2(400,65),new Vector2(310,66),46,MenuUI.Muted);
            MenuUI.Label(Store.Data.wins+" / 500",summary,new Vector2(0,.5f),new Vector2(445,-25),new Vector2(400,105),76,MenuUI.Ink,TextAnchor.MiddleLeft,true);
            MenuUI.Label("最高连胜  "+Store.Data.bestStreak+"\n集章完成  "+Store.Data.climb.summits,summary,new Vector2(1,.5f),new Vector2(-165,0),new Vector2(290,150),42,MenuUI.Muted,TextAnchor.MiddleLeft);
            int[] milestones={10,50,100,250,500};string[] titles={"刚会划两下","有点东西","划线高手","脑力担当","全关通关"};
            Color[] ranks={new Color32(234,72,119,255),new Color32(102,69,170,255),new Color32(102,69,170,255),new Color32(102,69,170,255),new Color32(204,181,232,255)};
            for(int i=0;i<milestones.Length;i++)
            {
                bool reached=Store.Data.wins>=milestones[i];float y=1245+i*158;
                var row=MenuUI.Paper(Content,new Vector2(.5f,1),new Vector2(0,-y),new Vector2(960,146),reached);row.name="Achievement milestone "+milestones[i];
                var icon=MenuUI.Art(StickerDesign.Badges[i],row,new Vector2(0,.5f),new Vector2(85,0),Vector2.one*116);
                if(!icon)icon=Skin.Art("game_assets_nav_tabrank",row,new Vector2(0,.5f),new Vector2(85,0),Vector2.one*116);
                if(!reached)icon.color=new Color(1,1,1,.64f);
                else MenuMotion.Sparkle(icon.rectTransform,3,true);
                var rank=Skin.Rect("Achievement rank "+(i+1),row,new Vector2(0,.5f),new Vector2(125,-32),Vector2.one*50);
                Skin.RoundBox(rank,new Vector2(.5f,.5f),Vector2.zero,rank.sizeDelta,reached?ranks[i]:new Color32(244,232,255,255),25);
                MenuUI.Label((i+1).ToString(),rank,new Vector2(.5f,.5f),Vector2.zero,rank.sizeDelta,42,reached?Color.white:MenuUI.Muted,TextAnchor.MiddleCenter);
                MenuUI.Label(titles[i],row,new Vector2(0,.5f),new Vector2(380,27),new Vector2(400,65),50,MenuUI.Ink);
                MenuUI.Label(reached?"已达成":Mathf.Min(Store.Data.wins,milestones[i])+" / "+milestones[i],row,new Vector2(1,.5f),new Vector2(-180,27),new Vector2(290,65),42,reached?MenuUI.Green:MenuUI.Muted,TextAnchor.MiddleRight);
                MenuUI.Progress(row,new Vector2(0,.5f),new Vector2(545,-38),new Vector2(730,16),(float)Store.Data.wins/milestones[i]);
            }
            MenuUI.SecondaryButton(Zh.LevelLibrary,Content,new Vector2(.5f,1),new Vector2(0,-2020),new Vector2(960,130),()=>LevelLibrary(0),52);
        }
        void FriendsPage()
        {
            MenuUI.Label("已通关 "+Store.Data.wins+" 关",Content,new Vector2(.5f,1),new Vector2(-220,-880),new Vector2(490,72),46,MenuUI.Ink);
            MenuUI.Label("按通关数排名",Content,new Vector2(.5f,1),new Vector2(245,-880),new Vector2(460,72),42,MenuUI.Muted,TextAnchor.MiddleRight);
            var area=MenuUI.Paper(Content,new Vector2(.5f,1),new Vector2(0,-1325),new Vector2(960,790));area.name="Friends rank canvas";
            var surface=Skin.Fill("Friends rank surface",area);surface.offsetMin=Vector2.one*16;surface.offsetMax=Vector2.one*-16;
#if WEIXINMINIGAME && !UNITY_EDITOR
            var image=surface.gameObject.AddComponent<RawImage>();image.raycastTarget=false;surface.gameObject.AddComponent<FriendsRankView>();
            FriendsRanking.SubmitProgress(Store.Data.wins);
#else
            Skin.Art("game_assets_nav_tabrank",area,new Vector2(.5f,.5f),new Vector2(0,145),Vector2.one*170);
            MenuUI.Label("在微信中与好友比一比",area,new Vector2(.5f,.5f),new Vector2(0,-30),new Vector2(860,90),52,MenuUI.Ink,TextAnchor.MiddleCenter,true);
            MenuUI.Label("仅展示同玩好友的真实通关进度",area,new Vector2(.5f,.5f),new Vector2(0,-145),new Vector2(860,90),42,MenuUI.Muted,TextAnchor.MiddleCenter);
#endif
            MenuUI.SecondaryButton("上一页",Content,new Vector2(.5f,1),new Vector2(-325,-1815),new Vector2(310,150),()=>FriendsRanking.Send("friends:page",delta:-1),48);
            MenuUI.GameButton("刷新",Content,new Vector2(.5f,1),new Vector2(0,-1815),new Vector2(310,150),()=>{FriendsRanking.SubmitProgress(Store.Data.wins);FriendsRanking.Send("friends:refresh");},52);
            MenuUI.SecondaryButton("下一页",Content,new Vector2(.5f,1),new Vector2(325,-1815),new Vector2(310,150),()=>FriendsRanking.Send("friends:page",delta:1),48);
            MenuUI.SecondaryButton("排行权限设置",Content,new Vector2(.5f,1),new Vector2(0,-1975),new Vector2(650,130),FriendsRanking.OpenSettings,42);
        }
    }
}

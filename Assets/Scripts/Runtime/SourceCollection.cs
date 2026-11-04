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
            MenuUI.Adapt(Content,headerLayout,first,2032);Navigation(2);FocusFirst();
        }
        void AchievementsPage()
        {
            var summary=MenuUI.Paper(Content,new Vector2(.5f,1),new Vector2(0,-960),new Vector2(920,240),true);summary.name="Achievement summary";
            MenuUI.Plate(summary,new Vector2(0,.5f),new Vector2(118,0),Vector2.one*150,MenuUI.Lemon,46);
            MenuUI.Art(StickerDesign.Achievement(Store.Data.wins),summary,new Vector2(0,.5f),new Vector2(118,0),Vector2.one*132);
            MenuUI.Label("已通关",summary,new Vector2(0,.5f),new Vector2(360,52),new Vector2(280,52),44,MenuUI.Muted);
            MenuUI.Label(Store.Data.wins+" / 500",summary,new Vector2(0,.5f),new Vector2(390,-22),new Vector2(360,80),64,MenuUI.Ink,TextAnchor.MiddleLeft,true);
            MenuUI.Label("最高连胜  "+Store.Data.bestStreak+"\n集章完成  "+Store.Data.climb.summits,summary,new Vector2(1,.5f),new Vector2(-158,0),new Vector2(300,150),42,MenuUI.Muted,TextAnchor.MiddleLeft);
            int[] milestones={10,50,100,250,500};string[] titles={"刚会划两下","有点东西","划线高手","脑力担当","全关通关"};
            Color[] ranks={MenuUI.Coral,new Color32(214,120,64,255),new Color32(214,140,48,255),new Color32(184,96,48,255),new Color32(232,168,64,255)};
            for(int i=0;i<milestones.Length;i++)
            {
                bool reached=Store.Data.wins>=milestones[i];float y=1168+i*156;
                var row=MenuUI.Paper(Content,new Vector2(.5f,1),new Vector2(0,-y),new Vector2(920,144),reached);row.name="Achievement milestone "+milestones[i];
                Skin.RoundBox(row,new Vector2(0,.5f),new Vector2(12,0),new Vector2(12,100),reached?MenuUI.CoralChip:new Color32(228,211,241,255),6);
                MenuUI.Plate(row,new Vector2(0,.5f),new Vector2(92,0),Vector2.one*104,reached?MenuUI.Blush:MenuUI.LilacWash,32);
                var icon=MenuUI.Art(StickerDesign.Badges[i],row,new Vector2(0,.5f),new Vector2(92,0),Vector2.one*92);
                if(!icon)icon=Skin.Art("game_assets_nav_tabrank",row,new Vector2(0,.5f),new Vector2(92,0),Vector2.one*92);
                if(!reached)icon.color=new Color(1,1,1,.64f);
                else MenuMotion.Sparkle(icon.rectTransform,3,true);
                var rank=Skin.Rect("Achievement rank "+(i+1),row,new Vector2(0,.5f),new Vector2(148,-36),Vector2.one*52);
                Skin.RoundBox(rank,new Vector2(.5f,.5f),Vector2.zero,rank.sizeDelta,reached?ranks[i]:new Color32(244,232,255,255),26);
                MenuUI.Label((i+1).ToString(),rank,new Vector2(.5f,.5f),Vector2.zero,rank.sizeDelta,42,reached?Color.white:MenuUI.Muted,TextAnchor.MiddleCenter);
                MenuUI.Label(titles[i],row,new Vector2(0,.5f),new Vector2(360,24),new Vector2(360,56),46,MenuUI.Ink);
                MenuUI.Label(reached?"已达成":Mathf.Min(Store.Data.wins,milestones[i])+" / "+milestones[i],row,new Vector2(1,.5f),new Vector2(-176,32),new Vector2(290,66),42,reached?MenuUI.Green:MenuUI.Muted,TextAnchor.MiddleRight);
                MenuUI.Progress(row,new Vector2(0,.5f),new Vector2(520,-32),new Vector2(660,20),(float)Store.Data.wins/milestones[i]);
            }
        }
        void FriendsPage()
        {
            MenuUI.Label("已通关 "+Store.Data.wins+" 关",Content,new Vector2(.5f,1),new Vector2(-220,-880),new Vector2(490,72),46,MenuUI.Ink);
            MenuUI.Label("按通关数排名",Content,new Vector2(.5f,1),new Vector2(245,-880),new Vector2(460,72),42,MenuUI.Muted,TextAnchor.MiddleRight);
            var area=MenuUI.Paper(Content,new Vector2(.5f,1),new Vector2(0,-1325),new Vector2(960,790),true);area.name="Friends rank canvas";
            var surface=Skin.Fill("Friends rank surface",area);surface.offsetMin=Vector2.one*22;surface.offsetMax=Vector2.one*-22;
#if WEIXINMINIGAME && !UNITY_EDITOR
            var image=surface.gameObject.AddComponent<RawImage>();image.raycastTarget=false;surface.gameObject.AddComponent<FriendsRankView>();
            FriendsRanking.SubmitProgress(Store.Data.wins);
#else
            Skin.RoundBox(area,new Vector2(.5f,.5f),new Vector2(0,118),new Vector2(420,420),MenuUI.Blush,210);
            MenuUI.Art("stamp_hand",area,new Vector2(.5f,.5f),new Vector2(-286,248),Vector2.one*108);
            MenuUI.Art("stamp_shine",area,new Vector2(.5f,.5f),new Vector2(292,48),Vector2.one*96);
            MenuUI.Art("entry_friends",area,new Vector2(.5f,.5f),new Vector2(0,148),Vector2.one*280);
            MenuUI.Label("在微信中与好友比一比",area,new Vector2(.5f,.5f),new Vector2(0,-52),new Vector2(860,90),54,MenuUI.Ink,TextAnchor.MiddleCenter,true);
            MenuUI.Label("仅展示同玩好友的真实通关进度",area,new Vector2(.5f,.5f),new Vector2(0,-148),new Vector2(860,80),42,MenuUI.Muted,TextAnchor.MiddleCenter);
#endif
            MenuUI.SecondaryButton("上一页",Content,new Vector2(.5f,1),new Vector2(-325,-1818),new Vector2(318,156),()=>FriendsRanking.Send("friends:page",delta:-1),48);
            MenuUI.GameButton("刷新",Content,new Vector2(.5f,1),new Vector2(0,-1818),new Vector2(318,156),()=>{FriendsRanking.SubmitProgress(Store.Data.wins);FriendsRanking.Send("friends:refresh");},52);
            MenuUI.SecondaryButton("下一页",Content,new Vector2(.5f,1),new Vector2(325,-1818),new Vector2(318,156),()=>FriendsRanking.Send("friends:page",delta:1),48);
        }
    }
}

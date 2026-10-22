using System;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    public partial class InkominoApp
    {

        public void ShowClimb()
        {
            if(ClimbActivity.Refresh(Store.Data,SaveStore.Now))Store.Save();
            Playing=false;Review=false;screen="climb";var state=Store.Data.climb;
            bool preview=!state.joined;ClearScreen("game_assets_shopbg");Wallet();int first=Content.childCount;
            var motion=Skin.Fill("Climb motion lifetime",Content).gameObject.AddComponent<ClimbPageMotion>();
            MenuUI.Title("登顶寻宝","连过七关，一路划到宝箱",Content);
            ((RectTransform)Skin.IconButton("shell_src_ui_close",Content,new Vector2(1,1),new Vector2(-120,-550),Vector2.one*120,ShowHome).transform).sizeDelta=Vector2.one*150;
            ((RectTransform)Skin.IconButton("shell_src_ui_climb_infobtn",Content,new Vector2(0,1),new Vector2(120,-550),Vector2.one*120,ClimbInfo).transform).sizeDelta=Vector2.one*150;

            var clockCard=Skin.Rect("Climb countdown strip",Content,new Vector2(.5f,1),new Vector2(0,-735),new Vector2(960,90));
            clockCard.name="Climb countdown";
            var clock=MenuUI.Label("",clockCard,new Vector2(.5f,.5f),Vector2.zero,new Vector2(850,70),42,MenuUI.Muted,TextAnchor.MiddleCenter);
            var prize=MenuUI.Paper(Content,new Vector2(.5f,1),new Vector2(0,-885),new Vector2(960,200),true);
            prize.name="Climb prize";
            Skin.Art("shell_src_ui_coinxlarge",prize,new Vector2(0,.5f),new Vector2(112,0),Vector2.one*138);
            string prizeText=ClimbActivity.AtTop(state)?"登顶奖励  "+ClimbActivity.Prize(state)+" 金币":"共享奖池  1000 金币";
            MenuUI.Label(prizeText,prize,new Vector2(.5f,.5f),new Vector2(75,35),new Vector2(690,70),56,MenuUI.Ink,TextAnchor.MiddleLeft,true);
            string roster=preview?"免费参加 · 连胜七关分享奖励":"挑战进度  "+state.stage+" / 7 关";
            MenuUI.Label(roster,prize,new Vector2(.5f,.5f),new Vector2(75,-38),new Vector2(690,60),42,MenuUI.Muted);

            if(preview)
            {
                ClimbHalo(Content,new Vector2(.5f,1),new Vector2(340,-1090),new Vector2(220,190),false);
                var hero=MenuUI.Art("menu_climb_journey",Content,new Vector2(.5f,1),new Vector2(0,-1350),new Vector2(1020,680));
                if(hero)
                {
                    MenuMotion.Float(hero.rectTransform,6,1.2f);
                    var treasureGlint=Skin.Rect("Climb invitation treasure glint",hero.transform,new Vector2(.5f,.5f),new Vector2(340,200),new Vector2(145,145));
                    MenuMotion.Sparkle(treasureGlint,3,true);
                }
                var panel=Skin.Rect("Climb invitation copy",Content,new Vector2(.5f,1),new Vector2(0,-1800),new Vector2(960,280));
                panel.name="Climb invitation";
                MenuUI.Label("连续通关 7 关",panel,new Vector2(.5f,.5f),new Vector2(0,83),new Vector2(860,78),60,MenuUI.Ink,TextAnchor.MiddleCenter,true);
                MenuUI.Label("拼出七级阶梯，分享金币奖池",panel,new Vector2(.5f,.5f),new Vector2(0,1),new Vector2(860,68),48,MenuUI.Ink,TextAnchor.MiddleCenter);
                MenuUI.Label("挑战失利后，30 分钟可再次出发",panel,new Vector2(.5f,.5f),new Vector2(0,-82),new Vector2(860,62),42,MenuUI.Muted,TextAnchor.MiddleCenter);
            }
            else
            {
                var map=MenuUI.Paper(Content,new Vector2(.5f,1),new Vector2(0,-1470),new Vector2(960,940));
                map.name="Climb island route";
                // Connect the seven original islands before adding their art and
                // inset labels. No state label extends beyond the map card.
                for(int i=0;i<6;i++)
                {
                    Vector2 from=ClimbIslandPoint(i),to=ClimbIslandPoint(i+1),delta=to-from;
                    var trail=Skin.RoundBox(map,new Vector2(.5f,.5f),(from+to)/2,new Vector2(delta.magnitude,9),i<state.stage?MenuUI.Green:new Color32(216,199,160,255),4.5f);
                    trail.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
                }
                for(int i=0;i<7;i++)
                {
                    Vector2 position=ClimbIslandPoint(i);bool passed=i<state.stage,current=i==Mathf.Clamp(state.stage,0,6);
                    if(current)
                    {
                        var ring=ClimbHalo(map,new Vector2(.5f,.5f),position+new Vector2(0,30),new Vector2(385,120),true);
                        if(state.isOut)ring.GetComponent<ClimbHaloGraphic>().color=new Color32(150,130,100,75);
                        else MenuMotion.Sparkle(ring,2,true);
                    }
                    var island=MenuUI.Art("menu_climb_node",map,new Vector2(.5f,.5f),position,new Vector2(340,178));
                    MenuUI.Label((i+1).ToString(),island.transform,new Vector2(.5f,.5f),new Vector2(passed||current?-100:0,12),new Vector2(85,62),50,MenuUI.Ink,TextAnchor.MiddleCenter);
                    if(!passed&&!current)island.color=new Color(.91f,.87f,.77f,.8f);
                    if(i==6)
                    {
                        
                        var treasure=Skin.Rect("Climb summit treasure",island.transform,new Vector2(.5f,.5f),new Vector2(130,58),new Vector2(140,140));
                        ClimbHalo(treasure,new Vector2(.5f,.5f),Vector2.zero,new Vector2(180,140),false);
                        MenuUI.Art("menu_treasure",treasure,new Vector2(.5f,.5f),Vector2.zero,treasure.sizeDelta);
                        MenuMotion.Sparkle(treasure,4,true);
                        if(ClimbActivity.AtTop(state)&&!state.claimed)MenuMotion.Celebrate(treasure);
                    }
                    if(current)
                    {
                        var avatar=Skin.Art("shell_src_ui_avatars_avatar_"+Store.Data.avatar,island.transform,new Vector2(.5f,.5f),new Vector2(-190,20),Vector2.one*100);
                        MenuMotion.Float(avatar.rectTransform,state.isOut?3:7,1.2f);if(state.isOut)avatar.color=new Color(1,1,1,.45f);
                    }
                    // The original art already includes its numbered wooden
                    // plaque. Only completed/current steps need an extra status.
                    if(passed||current)
                    {
                        var sign=Skin.Rect("Climb stage status",island.transform,new Vector2(.5f,.5f),new Vector2(43,12),new Vector2(180,62));
                        Skin.RoundBox(sign,new Vector2(.5f,.5f),Vector2.zero,sign.sizeDelta,new Color32(255,249,229,250),19);
                        string caption=passed?"已通过":state.isOut?"待重试":"在这里";
                        MenuUI.Label(caption,sign,new Vector2(.5f,.5f),Vector2.zero,new Vector2(174,60),42,passed?MenuUI.Green:MenuUI.Ink,TextAnchor.MiddleCenter);
                    }
                }
            }

            var status=MenuUI.Label("",Content,new Vector2(.5f,1),new Vector2(0,-2010),new Vector2(960,105),48,MenuUI.Ink,TextAnchor.MiddleCenter);
            Button action=null;
            action=MenuUI.GameButton("",Content,new Vector2(.5f,1),new Vector2(0,-2170),new Vector2(820,190),()=>
            {
                var current=Store.Data.climb;long now=SaveStore.Now;
                if(ClimbActivity.AtTop(current))
                {
                    if(current.claimed){ShowHome();return;}
                    int amount=ClimbActivity.Claim(Store,now);if(amount>0){SoundBank.Play("climb_win");ShowClimb();Info("登顶奖励","已领取 "+amount+" 金币！");}return;
                }
                if(!current.joined||current.isOut){if(ClimbActivity.Join(Store,now)){SoundBank.Play("climb_join");ShowClimb();}return;}
                PlayCurrent();
            },64);
            var actionArt=action.transform.Find("Button artwork").GetComponent<CanvasGroup>();
            MenuUI.Label("金币仅用于游戏内，不可提现",Content,new Vector2(.5f,1),new Vector2(0,-2320),new Vector2(960,65),42,MenuUI.Muted,TextAnchor.MiddleCenter);
            Action tick=()=>
            {
                if(screen!="climb")return;
                if(ClimbActivity.Refresh(Store.Data,SaveStore.Now)){Store.Save();ShowClimb();return;}
                var current=Store.Data.climb;long remaining=current.windowStart+86400-SaveStore.Now;
                clock.text="本轮剩余 "+RewardCountdown(remaining)+" · 每天 18:00 更新";
                bool unlocked=ClimbActivity.Unlocked(Store.Data);bool cooling=current.isOut&&SaveStore.Now<current.cooldownUntil;
                string text=!unlocked?"第 41 关开启":ClimbActivity.AtTop(current)?(current.claimed?"返回首页":"领取登顶奖励"):cooling?"等待再次出发":current.isOut?"重新挑战":current.joined?"继续闯关":"开始挑战";
                foreach(var label in action.GetComponentsInChildren<Text>())label.text=text;
                action.interactable=unlocked&&!cooling;action.GetComponent<CanvasGroup>().alpha=1;actionArt.alpha=action.interactable?1:.55f;
                status.text=!unlocked?"第 41 关开启 · 当前第 "+Store.Data.level+" 关":ClimbActivity.AtTop(current)?(current.claimed?"奖励已领取，下一轮再来挑战":"成功登顶！领取你的金币奖励"):cooling?"休整中 · "+RewardCountdown(current.cooldownUntil-SaveStore.Now)+" 后可重试":current.isOut?"准备好了，再向山顶出发":"已通过 "+current.stage+" / 7 关";
                if(preview&&unlocked)status.text="准备好了，就划这七关";
            };
            motion.Tick=tick;tick();MenuUI.Adapt(Content,headerLayout,first,2350);FocusFirst();
        }
        static RectTransform ClimbHalo(Transform parent,Vector2 anchor,Vector2 position,Vector2 size,bool ring)
        {
            var rect=Skin.Rect(ring?"Climb current stage halo":"Climb treasure glow",parent,anchor,position,size);
            var glow=rect.gameObject.AddComponent<ClimbHaloGraphic>();glow.Ring=ring;glow.raycastTarget=false;
            glow.color=ring?new Color32(217,162,42,120):new Color32(255,190,45,55);return rect;
        }
        static Vector2 ClimbIslandPoint(int index){return new Vector2(index%2==0?-165:165,-345+index*120);}
        void ClimbInfo()
        {
            var panel=Popup("活动规则",960,1600);
            MenuUI.Label("怎么参加",panel,new Vector2(.5f,.5f),new Vector2(0,550),new Vector2(820,75),54,MenuUI.Ink,TextAnchor.MiddleLeft,true);
            MenuUI.Label("第 41 关开启，每轮免费参加。\n连续通关 7 次，依次完成七级拼图阶梯。",panel,new Vector2(.5f,.5f),new Vector2(0,420),new Vector2(820,160),48,MenuUI.Ink);
            MenuUI.Label("挑战与重试",panel,new Vector2(.5f,.5f),new Vector2(0,245),new Vector2(820,75),54,MenuUI.Ink,TextAnchor.MiddleLeft,true);
            MenuUI.Label("共 100 名角色，含 99 名模拟对手。\n放弃、退出或重开会结束本轮尝试。\n30 分钟后可重试；接受续关不淘汰。",panel,new Vector2(.5f,.5f),new Vector2(0,75),new Vector2(820,235),48,MenuUI.Ink);
            MenuUI.Label("登顶奖励",panel,new Vector2(.5f,.5f),new Vector2(0,-140),new Vector2(820,75),54,MenuUI.Ink,TextAnchor.MiddleLeft,true);
            MenuUI.Label("登顶者分享 1000 金币奖池。\n金币仅限本游戏使用，不可提现。\n每天 18:00 开启新一轮，请及时领奖。",panel,new Vector2(.5f,.5f),new Vector2(0,-315),new Vector2(820,235),48,MenuUI.Ink);
            Skin.SourceButton("知道了",panel,new Vector2(.5f,0),new Vector2(0,155),new Vector2(680,170),ClosePopup,"green",60);
        }
        string ClimbHomeLabel()
        {
            if(ClimbActivity.Refresh(Store.Data,SaveStore.Now))Store.Save();var state=Store.Data.climb;
            if(!ClimbActivity.Unlocked(Store.Data))return "41关解锁";
            if(ClimbActivity.AtTop(state))return state.claimed?"已领取":"领奖励";
            if(state.isOut&&SaveStore.Now<state.cooldownUntil)return RewardCountdown(state.cooldownUntil-SaveStore.Now);
            return state.joined&&!state.isOut?state.stage+"/7":"登顶寻宝";
        }
    }
    // A small static mesh keeps the highlight soft without a texture, material
    // or per-frame work. MenuMotion owns the optional, reduced-motion-aware glints.
    public sealed class ClimbHaloGraphic:MaskableGraphic
    {
        public bool Ring;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();const int count=40;var rect=rectTransform.rect;Vector2 center=rect.center,radius=rect.size*.5f;
            if(radius.x<=0||radius.y<=0)return;
            if(Ring)
            {
                Vector2 inner=new Vector2(Mathf.Max(0,radius.x-7),Mathf.Max(0,radius.y-7));
                for(int i=0;i<count;i++)
                {
                    float angle=i*Mathf.PI*2/count;Vector2 direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                    vh.AddVert(center+Vector2.Scale(direction,radius),color,Vector2.zero);vh.AddVert(center+Vector2.Scale(direction,inner),color,Vector2.zero);
                }
                for(int i=0;i<count;i++){int next=(i+1)%count;vh.AddTriangle(i*2,next*2,i*2+1);vh.AddTriangle(i*2+1,next*2,next*2+1);}
            }
            else
            {
                vh.AddVert(center,color,Vector2.zero);var transparent=color;transparent.a=0;
                for(int i=0;i<count;i++){float angle=i*Mathf.PI*2/count;vh.AddVert(center+new Vector2(Mathf.Cos(angle)*radius.x,Mathf.Sin(angle)*radius.y),transparent,Vector2.zero);}
                for(int i=0;i<count;i++)vh.AddTriangle(0,i+1,(i+1)%count+1);
            }
        }
    }
}

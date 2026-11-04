using System;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    public partial class InkominoApp
    {

        public void ShowClimb()
        {
            if(!ClimbActivity.Unlocked(Store.Data)){Info("快乐集章","第 "+ClimbActivity.Config.unlockLevel+" 关开启");return;}
            if(ClimbActivity.Refresh(Store.Data,SaveStore.Now))Store.Save();
            Playing=false;Review=false;screen="climb";var state=Store.Data.climb;
            bool preview=!state.joined;ClearScreen("game_assets_shopbg");Wallet();int first=Content.childCount;
            var motion=Skin.Fill("Climb motion lifetime",Content).gameObject.AddComponent<ClimbPageMotion>();
            MenuUI.Title("快乐集章",null,Content);
            ((RectTransform)Skin.IconButton("shell_src_ui_close",Content,new Vector2(1,1),new Vector2(-120,-528),Vector2.one*120,ShowHome).transform).sizeDelta=Vector2.one*150;
            ((RectTransform)Skin.IconButton("shell_src_ui_climb_infobtn",Content,new Vector2(0,1),new Vector2(120,-528),Vector2.one*120,ClimbInfo).transform).sizeDelta=Vector2.one*150;

            var clockCard=Skin.Rect("Climb countdown strip",Content,new Vector2(.5f,1),new Vector2(0,-708),new Vector2(920,78));
            clockCard.name="Climb countdown";
            Skin.RoundBox(clockCard,Vector2.one*.5f,Vector2.zero,clockCard.sizeDelta,MenuUI.LilacWash,39);
            var clock=MenuUI.Label("",clockCard,new Vector2(.5f,.5f),Vector2.zero,new Vector2(850,70),42,MenuUI.Muted,TextAnchor.MiddleCenter);
            var prize=MenuUI.Paper(Content,new Vector2(.5f,1),new Vector2(0,-888),new Vector2(960,248),true);prize.name="Climb prize";
            MenuUI.Plate(prize,new Vector2(1,.5f),new Vector2(-138,0),Vector2.one*196,MenuUI.Lemon,52);
            MenuUI.Label(ClimbActivity.AtTop(state)?"本轮收获":"集齐奖励",prize,new Vector2(0,.5f),new Vector2(318,64),new Vector2(500,66),48,MenuUI.Muted);
            MenuUI.Label((ClimbActivity.AtTop(state)?ClimbActivity.Prize(state):1000)+" 金币",prize,new Vector2(0,.5f),new Vector2(318,-16),new Vector2(500,96),78,MenuUI.Ink,TextAnchor.MiddleLeft,true);
            MenuUI.Label("集满 7 枚即可领取",prize,new Vector2(0,.5f),new Vector2(318,-88),new Vector2(500,54),42,MenuUI.Muted);
            var gift=MenuUI.Art("reward_coins",prize,new Vector2(1,.5f),new Vector2(-138,0),Vector2.one*176);MenuMotion.Float(gift.rectTransform,5,1.2f);
            var album=MenuUI.Paper(Content,new Vector2(.5f,1),new Vector2(0,-1490),new Vector2(960,880));album.name=preview?"Climb invitation":"Climb island route";
            Skin.RoundBox(album,new Vector2(.5f,1),new Vector2(0,-78),new Vector2(900,92),MenuUI.Blush,32);
            MenuUI.Label("我的快乐集章册",album,new Vector2(0,1),new Vector2(360,-78),new Vector2(600,75),56,MenuUI.Ink,TextAnchor.MiddleLeft,true);
            MenuUI.Label(state.stage+" / 7",album,new Vector2(1,1),new Vector2(-110,-78),new Vector2(170,75),48,MenuUI.Grape,TextAnchor.MiddleRight);
            Color32[] fills={new Color32(255,224,236,255),new Color32(224,247,237,255),new Color32(255,240,180,255),new Color32(235,221,255,255),new Color32(220,239,255,255),new Color32(255,226,205,255)};
            string[] names={"好心情","小确幸","有灵感","超顺手","会发光","真厉害"};
            string[] icons=StickerDesign.Stamps;
            for(int i=0;i<6;i++)
            {
                bool passed=i<state.stage,current=!preview&&i==state.stage;
                var cell=Skin.Rect("Stamp stage "+(i+1),album,new Vector2(.5f,1),new Vector2((i%3-1)*286,-268-i/3*236),new Vector2(268,214));
                Skin.RoundBox(cell,Vector2.one*.5f,new Vector2(6,-8),cell.sizeDelta,MenuUI.WarmShadow,38);
                Skin.RoundBox(cell,Vector2.one*.5f,Vector2.zero,cell.sizeDelta,current?MenuUI.Coral:Color.white,38);
                Skin.RoundBox(cell,Vector2.one*.5f,Vector2.zero,cell.sizeDelta-Vector2.one*(current?14:8),fills[i],32);
                var art=MenuUI.Art(icons[i],cell,Vector2.one*.5f,new Vector2(0,22),Vector2.one*124);if(!passed&&!current)art.color=new Color(1,1,1,.35f);
                if(current){cell.localScale=Vector3.one*1.04f;MenuMotion.Float(art.rectTransform,4,1.1f);MenuMotion.Sparkle(art.rectTransform,2,true);}
                MenuUI.Label(names[i],cell,Vector2.one*.5f,new Vector2(0,-70),new Vector2(240,62),42,MenuUI.Ink,TextAnchor.MiddleCenter);
                if(passed){var stamp=Skin.Rect("Climb stage status",cell,new Vector2(1,1),new Vector2(-32,-30),Vector2.one*68);Skin.RoundBox(stamp,Vector2.one*.5f,Vector2.zero,stamp.sizeDelta,MenuUI.Coral,34);MenuUI.Label("好",stamp,Vector2.one*.5f,Vector2.zero,stamp.sizeDelta,42,Color.white,TextAnchor.MiddleCenter,true);stamp.localRotation=Quaternion.Euler(0,0,-12);}
            }
            var final=Skin.Rect("Stamp final reward",album,new Vector2(.5f,1),new Vector2(0,-738),new Vector2(880,168));
            Skin.RoundBox(final,Vector2.one*.5f,new Vector2(0,-6),final.sizeDelta,new Color32(132,74,40,70),42);
            Skin.RoundBox(final,Vector2.one*.5f,Vector2.zero,final.sizeDelta,Color.white,42);
            Skin.RoundBox(final,Vector2.one*.5f,Vector2.zero,final.sizeDelta-Vector2.one*10,new Color32(255,236,164,255),36);
            MenuUI.Art(StickerDesign.Stamps[6],final,new Vector2(0,.5f),new Vector2(96,0),Vector2.one*148);
            MenuUI.Label(state.stage>=7?"快乐集齐，开礼物！":"惊喜派送",final,Vector2.one*.5f,new Vector2(78,24),new Vector2(620,70),54,MenuUI.Ink,TextAnchor.MiddleCenter,true);
            MenuUI.Label(state.stage>=7?"这份奖励属于你":"第 7 枚印章",final,Vector2.one*.5f,new Vector2(78,-42),new Vector2(620,58),42,MenuUI.Muted,TextAnchor.MiddleCenter);
            if(state.stage>=6&&!state.isOut)MenuMotion.Sparkle(final,3,true);
            if(ClimbActivity.AtTop(state)&&!state.claimed)MenuMotion.Celebrate(gift.rectTransform);

            var status=MenuUI.Label("",Content,new Vector2(.5f,1),new Vector2(0,-2010),new Vector2(960,105),48,MenuUI.Ink,TextAnchor.MiddleCenter);
            Button action=null;
            action=MenuUI.GameButton("",Content,new Vector2(.5f,1),new Vector2(0,-2164),new Vector2(760,168),()=>
            {
                var current=Store.Data.climb;long now=SaveStore.Now;
                if(ClimbActivity.AtTop(current))
                {
                    if(current.claimed){ShowHome();return;}
                    int amount=ClimbActivity.Claim(Store,now);if(amount>0){SoundBank.Play("climb_win");Feel.Celebrate();ShowClimb();Info("集章奖励","已领取 "+amount+" 金币！");}return;
                }
                if(!current.joined||current.isOut){if(ClimbActivity.Join(Store,now)){SoundBank.Play("climb_join");Feel.Success();ShowClimb();}return;}
                PlayCurrent();
            },56);
            var actionArt=action.transform.Find("Button artwork").GetComponent<CanvasGroup>();
            Action tick=()=>
            {
                if(screen!="climb")return;
                if(ClimbActivity.Refresh(Store.Data,SaveStore.Now)){Store.Save();ShowClimb();return;}
                var current=Store.Data.climb;long remaining=current.windowStart+86400-SaveStore.Now;
                clock.text="本轮剩余 "+RewardCountdown(remaining)+" · 每天 18:00 更新";
                bool unlocked=ClimbActivity.Unlocked(Store.Data);bool cooling=current.isOut&&SaveStore.Now<current.cooldownUntil;
                string text=!unlocked?"第 41 关开启":ClimbActivity.AtTop(current)?(current.claimed?"返回首页":"领取集章奖励"):cooling?"等待再次出发":current.isOut?"重新挑战":current.joined?"继续闯关":"开始挑战";
                foreach(var label in action.GetComponentsInChildren<Text>())label.text=text;
                action.interactable=unlocked&&!cooling;action.GetComponent<CanvasGroup>().alpha=1;actionArt.alpha=action.interactable?1:.55f;
                status.text=!unlocked?"第 41 关开启 · 当前第 "+Store.Data.level+" 关":ClimbActivity.AtTop(current)?(current.claimed?"奖励已领取，下一轮再来挑战":"七枚印章集齐！领取惊喜奖励"):cooling?"休整中 · "+RewardCountdown(current.cooldownUntil-SaveStore.Now)+" 后可重试":current.isOut?"整理好心情，再集一册":"已收集 "+current.stage+" / 7 枚印章";
                if(preview&&unlocked)status.text="准备好了，就划这七关";
            };
            motion.Tick=tick;tick();MenuUI.Adapt(Content,headerLayout,first,2350);FocusFirst();
        }
        static RectTransform ClimbHalo(Transform parent,Vector2 anchor,Vector2 position,Vector2 size,bool ring)
        {
            var rect=Skin.Rect(ring?"Climb current stage halo":"Climb treasure glow",parent,anchor,position,size);
            var glow=rect.gameObject.AddComponent<ClimbHaloGraphic>();glow.Ring=ring;glow.raycastTarget=false;
            glow.color=ring?new Color32(232,156,96,140):new Color32(255,196,120,50);return rect;
        }
        static Vector2 ClimbIslandPoint(int index){return new Vector2(index%2==0?-165:165,-345+index*120);}
        void ClimbInfo()
        {
            var panel=Popup("活动规则",960,1260);
            string[] titles={"连过 7 关，集齐印章","中断后 30 分钟可重试","集齐领取 1000 金币"};
            string[] details={"第 41 关开启 · 免费参加","续关可保留本轮印章","每天 18:00 更新，请及时领奖"};
            string[] icons={"entry_album","ink_time","reward_coins"};
            for(int i=0;i<3;i++){
                float y=265-i*240;
                MenuUI.Art(icons[i],panel,Vector2.one*.5f,new Vector2(-322,y-18),Vector2.one*112);
                MenuUI.Label(titles[i],panel,Vector2.one*.5f,new Vector2(75,y+18),new Vector2(660,78),48,MenuUI.Ink,TextAnchor.MiddleLeft,true);
                MenuUI.Label(details[i],panel,Vector2.one*.5f,new Vector2(75,y-63),new Vector2(660,66),42,MenuUI.Muted,TextAnchor.MiddleLeft);
            }
            MenuUI.GameButton("知道了",panel,new Vector2(.5f,0),new Vector2(0,130),new Vector2(700,170),ClosePopup,60);
        }
        string ClimbHomeLabel()
        {
            long now=SaveStore.Now;if(ClimbActivity.Refresh(Store.Data,now))Store.Save();var state=Store.Data.climb;
            if(!ClimbActivity.Unlocked(Store.Data))return "41关解锁";
            if(ClimbActivity.AtTop(state)&&!state.claimed)return Zh.GiftReady;
            if(state.isOut&&now<state.cooldownUntil)return RewardCountdown(state.cooldownUntil-now);
            // Home mirrors the daily-gift card: time until refresh, or a ready reward.
            // Stamp progress stays on the activity page.
            return RewardCountdown(ClimbActivity.WindowStart(now)+86400-now);
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

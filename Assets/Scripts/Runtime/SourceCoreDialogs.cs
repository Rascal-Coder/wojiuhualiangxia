using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    public partial class InkominoApp
    {
        public Action SourceFailureExit;
        const float LossLabelOffsetDp=80,LossLabelHeightDp=44;
        // Static source ports:2180 LivesPopup,1989 RestartConfirmPopup,
        // 1990 ExitConfirmPopup/LossSummary,2218 ContinuePromptPopup,2122 ContinueScreen.
        // Native text metrics and the full-screen failure flex allocation still
        // require a reference capture; no exact screenshot match is claimed here.
        void CoreLivesPopup()
        {
            Store.Recover();bool full=Store.Data.hearts>=5;float areaH=(full?182:224)*D;
            bool showAd=!full&&RewardEntryEnabled(RewardPlacement.Life);float adSpace=showAd?90:0;
            var panel=Popup(Zh.Lives,943.2f,82*D+areaH+(full?26:16+115+26+adSpace)*D);float width=panel.sizeDelta.x-36*D;
            var area=Skin.Rect("Source lives heart area",panel,new Vector2(.5f,1),new Vector2(0,-82*D-areaH/2),new Vector2(width,areaH));
            Skin.RoundBox(area,new Vector2(.5f,.5f),Vector2.zero,area.sizeDelta,new Color32(204,181,232,255),16*D);
            Skin.RoundBox(area,new Vector2(.5f,.5f),Vector2.zero,area.sizeDelta-Vector2.one*4*D,new Color32(244,232,255,255),14*D);
            var heart=Skin.Rect("Pulsing life",area,new Vector2(.5f,1),new Vector2(0,-79*D),new Vector2(120,110)*D);
            Skin.Art("shell_src_ui_heart",heart,new Vector2(.5f,.5f),Vector2.zero,Vector2.one*96*D);
            var count=Skin.Text(Store.Data.hearts.ToString(),heart,new Vector2(.5f,.5f),new Vector2(0,-2*D),Vector2.one*90*D,Mathf.RoundToInt(44*D),Color.white);
            var label=Skin.Text(full?Zh.LivesFullTitle:Zh.NextLife,area,new Vector2(.5f,1),new Vector2(0,-(full?158:151)*D),new Vector2(width-20*D,28*D),Mathf.RoundToInt((full?20:14)*D),full?MenuUI.Green:MenuUI.Muted);
            var clock=Skin.Rect("Next life countdown",area,new Vector2(.5f,1),new Vector2(0,-180*D),new Vector2(180,40)*D);Skin.RoundBox(clock,new Vector2(.5f,.5f),Vector2.zero,clock.sizeDelta,new Color32(204,181,232,255),18*D);
            var dot=Skin.Rect("Rotating clock dot",clock,new Vector2(0,.5f),new Vector2(22*D,0),Vector2.one*28*D);Skin.RoundBox(dot,new Vector2(.5f,.5f),Vector2.zero,dot.sizeDelta,new Color32(155,120,207,255),14*D);Skin.RoundBox(dot,new Vector2(.5f,.5f),Vector2.zero,dot.sizeDelta-Vector2.one*6*D,new Color32(155,120,207,255),11*D);
            // timerDotText is an em dash (fn12514 @03b5, literal47093).
            Skin.Text("—",dot,new Vector2(.5f,.5f),Vector2.zero,dot.sizeDelta,Mathf.RoundToInt(14*D),Color.white);
            var value=Skin.Text("",clock,new Vector2(.5f,.5f),new Vector2(16*D,0),new Vector2(130,30)*D,Mathf.RoundToInt(22*D),MenuUI.Ink);clock.gameObject.SetActive(!full);
            var refill=CoreDialogButton(Zh.Refill,panel,new Vector2(.5f,0),new Vector2(0,(26+57.5f+adSpace)*D),new Vector2(width,115*D),()=>{if(Store.Data.coins<450)return;Store.Data.coins-=450;Store.Data.hearts=5;Store.Data.regenAt=SaveStore.Now+900;Store.Save();ClosePopup();CoreLivesPopup();},"green",22,1);
            var price=Skin.Rect("Refill price450",refill.transform,new Vector2(.5f,.5f),new Vector2(0,-21*D),new Vector2(120,26)*D);Skin.Art("shell_src_ui_coin",price,new Vector2(.5f,.5f),new Vector2(-24*D,0),Vector2.one*22*D);Skin.Text("450",price,new Vector2(.5f,.5f),new Vector2(14*D,0),new Vector2(70,25)*D,Mathf.RoundToInt(16*D),Color.white);
            var refillLabel=refill.GetComponentInChildren<Text>();if(refillLabel)refillLabel.rectTransform.anchoredPosition+=Vector2.up*12*D;refill.interactable=Store.Data.coins>=450;refill.gameObject.SetActive(!full);
            var rewardAd=showAd?RewardButton(RewardPlacement.Life,RewardLabel(RewardPlacement.Life),panel,new Vector2(.5f,0),new Vector2(0,64*D),new Vector2(width,76*D),()=>RewardLife(panel)):null;
            StartCoroutine(CoreLivesMotion(panel,heart,dot,count,value,label,clock,refill.gameObject,rewardAd?rewardAd.gameObject:null));
        }
        IEnumerator CoreLivesMotion(RectTransform panel,RectTransform heart,RectTransform dot,Text count,Text value,Text label,RectTransform clock,GameObject refill,GameObject rewardAd)
        {
            float begin=SourceClock.Now;while(panel&&panel.gameObject.activeInHierarchy){float t=SourceClock.Now-begin,u=Mathf.Repeat(t,1);heart.localScale=Vector3.one*Mathf.Lerp(1,1.1f,u<.5f?SourceMotion.DefaultTiming(u/.5f):1-SourceMotion.DefaultTiming((u-.5f)/.5f));dot.localRotation=Quaternion.Euler(0,0,-360*SourceMotion.DefaultTiming(Mathf.Repeat(t,2)/2));count.text=Store.Data.hearts.ToString();long sec=Math.Max(0,Store.Data.regenAt-SaveStore.Now);value.text=RewardCountdown(sec);if(Store.Data.hearts>=5){label.text=Zh.LivesFullTitle;label.color=MenuUI.Green;clock.gameObject.SetActive(false);refill.SetActive(false);if(rewardAd)rewardAd.SetActive(false);}yield return null;}
        }
        void CoreLeavePrompt(bool retry)
        {
            bool fromSaved=!Playing&&Store.Data.resumeLevel>0;int saved=Store.Data.resumeLevel;
            const float summaryCenterDp=151,contentGapDp=24,bottomPaddingDp=26;
            float h=(retry?70:60)*1.15f*D;
            // Reserve the entire warning text rectangle before positioning the
            // bottom-anchored actions. The former340dp panel put the buttons
            // inside that text area (restart and exit have different heights).
            float panelHeight=(summaryCenterDp+LossLabelOffsetDp+LossLabelHeightDp/2+contentGapDp+bottomPaddingDp)*D+h;
            var panel=Popup(retry?Zh.Restart:Zh.QuitTitle,943.2f,panelHeight);float width=panel.sizeDelta.x-36*D;
            CoreLossSummary(panel,new Vector2(0,-151*D),width);
            float bw=(width-12*D)/2;
            Action cancel=()=>{ClosePopup();if(fromSaved)StartLevel(saved,false,true);};
            Action confirm=()=>{if(!Review){Store.LoseHeart(fromSaved?saved:CurrentLevel);Store.Data.resumeLevel=0;Store.Data.resumePieces=null;Store.Save();}if(retry)Retry();else ShowHome();};
            if(retry){CoreDialogButton(Zh.Retry,panel,new Vector2(.5f,0),new Vector2(-(bw+12*D)/2,26*D+h/2),new Vector2(bw,h),confirm,"red",22,1);CoreDialogButton(Zh.Resume,panel,new Vector2(.5f,0),new Vector2((bw+12*D)/2,26*D+h/2),new Vector2(bw,h),cancel,"green",22,1);}
            else{CoreDialogButton(Zh.Cancel,panel,new Vector2(.5f,0),new Vector2(-(bw+12*D)/2,26*D+h/2),new Vector2(bw,h),cancel,"green",20,0);CoreDialogButton(Zh.Exit,panel,new Vector2(.5f,0),new Vector2((bw+12*D)/2,26*D+h/2),new Vector2(bw,h),confirm,"red",20,0);}
        }
        void CoreContinuePrompt()
        {
            var panel=Popup(Zh.WelcomeBack,943.2f,310*D);HideCoreClose(panel);float width=panel.sizeDelta.x-76*D;
            // Module2218 declares white text and no inner-card background.
            // Preserve its declared white colour; the original device appearance
            // of this paper-skin variant remains to be captured.
            Skin.Text(Zh.Unfinished,panel,new Vector2(.5f,1),new Vector2(0,-(82+20+22)*D),new Vector2(width,66*D),Mathf.RoundToInt(16*D),MenuUI.Ink);
            float bw=(width-12*D)/2,h=70*1.15f*D;CoreDialogButton(Zh.Quit,panel,new Vector2(.5f,0),new Vector2(-(bw+12*D)/2,44*D+h/2),new Vector2(bw,h),()=>{ClosePopup();CoreLeavePrompt(false);},"red",22,1);
            CoreDialogButton(Zh.Continue,panel,new Vector2(.5f,0),new Vector2((bw+12*D)/2,44*D+h/2),new Vector2(bw,h),()=>StartLevel(Store.Data.resumeLevel>0?Store.Data.resumeLevel:Store.Data.level,false,true),"green",22,1);
        }
        public void CoreFail()
        {
            if(Finished)return;Finished=true;
            if(!Review&&CurrentLevel>=ClimbActivity.Config.unlockLevel&&Store.Data.climb.joined&&!Store.Data.climb.isOut&&!ClimbActivity.AtTop(Store.Data.climb)){Store.Data.climbPendingFailure=CurrentLevel;Store.Save();}
            AnimationBusy=false;if(BoardView)BoardView.Cancel();SoundBank.Play("fail");
            bool first=!Store.Data.firstContinueUsed&&continuations==0;int cost=90+Math.Min(continuations,2)*30;
            bool showAd=!first&&!Review&&rewardedContinues<1&&RewardEntryEnabled(RewardPlacement.ContinueLevel);
            var panel=Popup(Zh.TimeUp,1000,showAd?1320:1130);panel.name="Sticker timeout panel";var root=(RectTransform)panel.parent.parent;root.name="Source ContinueScreen";HideCoreClose(panel);
            float width=panel.sizeDelta.x;var title=panel.GetComponentInChildren<Text>();
            var offer=Skin.Rect("Time offer",panel,new Vector2(.5f,1),new Vector2(0,-347),new Vector2(width-100,310));
            MenuUI.Art("ink_time",offer,Vector2.one*.5f,new Vector2(-190,15),Vector2.one*218);
            MenuUI.Label(Zh.ExtraTime,offer,Vector2.one*.5f,new Vector2(120,38),new Vector2(370,120),96,MenuUI.Ink,TextAnchor.MiddleCenter);
            MenuUI.Label("再给灵感一点时间",offer,Vector2.one*.5f,new Vector2(120,-58),new Vector2(405,64),42,MenuUI.Muted,TextAnchor.MiddleCenter);
            var description=MenuUI.Label("继续完成这一张拼图",panel,new Vector2(.5f,1),new Vector2(0,-550),new Vector2(width-100,70),48,MenuUI.Ink,TextAnchor.MiddleCenter);
            var wallet=Skin.Rect("Continuation wallet",panel,new Vector2(.5f,1),new Vector2(0,-645),new Vector2(500,84));
            Skin.RoundBox(wallet,Vector2.one*.5f,Vector2.zero,wallet.sizeDelta,new Color32(255,243,209,255),42);
            Skin.Art("shell_src_ui_coin",wallet,Vector2.one*.5f,new Vector2(-135,0),Vector2.one*66);
            MenuUI.Label("现有金币 "+Store.Data.coins,wallet,Vector2.one*.5f,new Vector2(35,0),new Vector2(340,70),42,MenuUI.Ink,TextAnchor.MiddleCenter);
            var loss=Skin.Rect("Loss warning phase",panel,new Vector2(.5f,1),new Vector2(0,-347),new Vector2(width-100,310));
            Skin.Art("shell_src_ui_heartbroken",loss,Vector2.one*.5f,new Vector2(-180,0),Vector2.one*202);
            MenuUI.Label("本局进度将丢失",loss,Vector2.one*.5f,new Vector2(110,35),new Vector2(390,80),54,MenuUI.Ink,TextAnchor.MiddleCenter);
            MenuUI.Label(Review?"可以重新挑战":"消耗 1 点体力",loss,Vector2.one*.5f,new Vector2(110,-55),new Vector2(390,70),44,MenuUI.Muted,TextAnchor.MiddleCenter);loss.gameObject.SetActive(false);
            bool warning=false;Button exitButton=null;
            Action exit=()=>{if(!warning){warning=true;offer.gameObject.SetActive(false);loss.gameObject.SetActive(true);if(title)title.text="确定结束吗？";description.text="也可以续时，接着完成";foreach(var label in exitButton.GetComponentsInChildren<Text>())label.text="确认结束";}else{if(!Review){Store.LoseHeart(CurrentLevel);Store.Data.resumeLevel=0;Store.Data.resumePieces=null;Store.Save();}SourceFailureExit=null;ShowHome();}};
            SourceFailureExit=exit;
            Skin.IconButton("shell_src_ui_close",panel,new Vector2(1,1),new Vector2(-40,-55),Vector2.one*42*D,exit);
            Action play=()=>{if(!first&&Store.Data.coins<cost)return;if(first)Store.Data.firstContinueUsed=true;else Store.Data.coins-=cost;Store.Data.climbPendingFailure=0;Store.Save();Remaining=Mathf.Max(0,Remaining)+60;SourceFailureExit=null;continuations++;Finished=false;AnimationBusy=false;ClosePopup();TimerChanged();SaveBoard();};
            var playButton=MenuUI.GameButton(first?Zh.Free:Zh.PlayOn,panel,new Vector2(.5f,1),new Vector2(0,-807),new Vector2(width-120,180),play,60);playButton.name="Timeout continue";playButton.interactable=first||Store.Data.coins>=cost;
            var label=playButton.GetComponentInChildren<Text>();label.rectTransform.anchoredPosition=new Vector2(0,first?0:33);label.rectTransform.sizeDelta=new Vector2(width-170,75);
            if(!first)
            {
                // Keep both cost icon and number inside the button's animated visual parent.
                var face=label.transform.parent;Skin.Art("shell_src_ui_coin",face,Vector2.one*.5f,new Vector2(-54,-38),Vector2.one*49);
                var price=MenuUI.Label(cost.ToString(),face,Vector2.one*.5f,new Vector2(28,-38),new Vector2(120,58),44,MenuUI.Ink,TextAnchor.MiddleCenter);price.name="Continuation cost";
            }
            if(showAd)
            {
                int level=CurrentLevel,epoch=presentationEpoch;
                RewardButton(RewardPlacement.ContinueLevel,RewardLabel(RewardPlacement.ContinueLevel),panel,new Vector2(.5f,1),new Vector2(0,-1010),new Vector2(width-120,160),()=>RequestReward(RewardPlacement.ContinueLevel,()=>{Store.Data.climbPendingFailure=0;Remaining=Mathf.Max(0,Remaining)+AdPlacements.Slot(RewardPlacement.ContinueLevel).amount;SourceFailureExit=null;continuations++;rewardedContinues++;Finished=false;AnimationBusy=false;ClosePopup();TimerChanged();SaveBoard();},()=>CurrentLevel==level&&presentationEpoch==epoch&&Playing&&Finished&&root&&root.gameObject.activeInHierarchy));
            }
            exitButton=MenuUI.SecondaryButton("结束本局",panel,new Vector2(.5f,0),new Vector2(0,116),new Vector2(width-270,110),exit,46);exitButton.name="Timeout exit";
            if(!playButton.interactable)description.text="金币不足，可以选择其他方式";
        }
        void CoreLossSummary(Transform parent,Vector2 topCenter,float width,bool onPaper=true,bool warningFirst=false)
        {
            var heart=Skin.Rect("Loss of one life",parent,new Vector2(.5f,1),topCenter,new Vector2(104,88)*D);Skin.Art("shell_src_ui_heartbroken",heart,new Vector2(.5f,.5f),Vector2.zero,Vector2.one*84*D);
            var minus=Skin.Rect("Minus one",heart,new Vector2(1,0),new Vector2(8*D,-4*D),Vector2.one*34*D);Skin.RoundBox(minus,new Vector2(.5f,.5f),Vector2.zero,minus.sizeDelta,new Color32(234,72,119,255),17*D);Skin.RoundBox(minus,new Vector2(.5f,.5f),Vector2.zero,minus.sizeDelta-Vector2.one*4*D,new Color32(234,72,119,255),15*D);Skin.Text("-1",minus,new Vector2(.5f,.5f),new Vector2(0,-2*D),minus.sizeDelta,Mathf.RoundToInt(16*D),Color.white);
            Skin.Text(Zh.ProgressLost,parent,new Vector2(.5f,1),topCenter+(warningFirst?Vector2.up*86*D:Vector2.down*LossLabelOffsetDp*D),new Vector2(width-32*D,LossLabelHeightDp*D),Mathf.RoundToInt(19*D),MenuUI.Ink);
        }
        Button CoreDialogButton(string text,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Action action,string color,int fontDp,float topMarginDp)
        {
            var b=MenuUI.GameButton(text,parent,anchor,pos,size,action,Mathf.RoundToInt(fontDp*D),color!="red");
            if(color=="red")foreach(var label in b.GetComponentsInChildren<Text>())label.color=new Color32(122,39,82,255);
            return b;
        }
        static void HideCoreClose(RectTransform panel){foreach(Transform c in panel)if(c.name.StartsWith("shell_src_ui_close",StringComparison.Ordinal))c.gameObject.SetActive(false);}
        static void CoreFailureTitle(RectTransform parent,string value,float sizeDp)
        {
            MenuUI.Label(value,parent,new Vector2(.5f,.5f),Vector2.zero,parent.sizeDelta,84,MenuUI.Ink,TextAnchor.MiddleCenter,true);
        }

        static Text CoreArcLetter(Transform parent,string ch,int size,Color color){var t=Skin.Text(ch,parent,new Vector2(.5f,.5f),Vector2.zero,new Vector2(size*2,size*1.7f),size,color);t.horizontalOverflow=HorizontalWrapMode.Overflow;t.verticalOverflow=VerticalWrapMode.Overflow;return t;}
    }
}

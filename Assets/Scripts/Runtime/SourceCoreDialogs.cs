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
            Skin.RoundBox(area,new Vector2(.5f,.5f),Vector2.zero,area.sizeDelta,new Color32(220,196,151,255),16*D);
            Skin.RoundBox(area,new Vector2(.5f,.5f),Vector2.zero,area.sizeDelta-Vector2.one*4*D,new Color32(244,229,197,255),14*D);
            var heart=Skin.Rect("Pulsing life",area,new Vector2(.5f,1),new Vector2(0,-79*D),new Vector2(120,110)*D);
            Skin.Art("shell_src_ui_heart",heart,new Vector2(.5f,.5f),Vector2.zero,Vector2.one*96*D);
            var count=Skin.Text(Store.Data.hearts.ToString(),heart,new Vector2(.5f,.5f),new Vector2(0,-2*D),Vector2.one*90*D,Mathf.RoundToInt(44*D),Color.white);
            var label=Skin.Text(full?Zh.LivesFullTitle:Zh.NextLife,area,new Vector2(.5f,1),new Vector2(0,-(full?158:151)*D),new Vector2(width-20*D,28*D),Mathf.RoundToInt((full?20:14)*D),full?MenuUI.Green:MenuUI.Muted);
            var clock=Skin.Rect("Next life countdown",area,new Vector2(.5f,1),new Vector2(0,-180*D),new Vector2(180,40)*D);Skin.RoundBox(clock,new Vector2(.5f,.5f),Vector2.zero,clock.sizeDelta,new Color32(220,196,151,255),18*D);
            var dot=Skin.Rect("Rotating clock dot",clock,new Vector2(0,.5f),new Vector2(22*D,0),Vector2.one*28*D);Skin.RoundBox(dot,new Vector2(.5f,.5f),Vector2.zero,dot.sizeDelta,new Color32(102,204,255,255),14*D);Skin.RoundBox(dot,new Vector2(.5f,.5f),Vector2.zero,dot.sizeDelta-Vector2.one*6*D,new Color32(68,170,221,255),11*D);
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
            float begin=SourceClock.Now;while(panel&&panel.gameObject.activeInHierarchy){float t=SourceClock.Now-begin,u=Mathf.Repeat(t,1);heart.localScale=Vector3.one*Mathf.Lerp(1,1.1f,u<.5f?SourceMotion.DefaultTiming(u/.5f):1-SourceMotion.DefaultTiming((u-.5f)/.5f));dot.localRotation=Quaternion.Euler(0,0,-360*SourceMotion.DefaultTiming(Mathf.Repeat(t,2)/2));count.text=Store.Data.hearts.ToString();long sec=Math.Max(0,Store.Data.regenAt-SaveStore.Now);value.text=(sec/60).ToString("00")+":"+(sec%60).ToString("00");if(Store.Data.hearts>=5){label.text=Zh.LivesFullTitle;label.color=MenuUI.Green;clock.gameObject.SetActive(false);refill.SetActive(false);if(rewardAd)rewardAd.SetActive(false);}yield return null;}
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
            if(Finished)return;Finished=true;if(!Review&&CurrentLevel>=ClimbActivity.Config.unlockLevel&&Store.Data.climb.joined&&!Store.Data.climb.isOut&&!ClimbActivity.AtTop(Store.Data.climb)){Store.Data.climbPendingFailure=CurrentLevel;Store.Save();}AnimationBusy=false;if(BoardView)BoardView.Cancel();SoundBank.Play("fail");
            var black=Skin.Fill("Source ContinueScreen",Overlays);black.gameObject.AddComponent<Image>().color=MenuUI.Ink;Skin.Art("game_assets_bg",black,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1080,2400),false);popups.Push(black);
            var motion=Skin.Fill("Failure popup section",black);var group=motion.gameObject.AddComponent<CanvasGroup>();
            var coin=At(black,"Failure coin top slot",16*D,50*D,150*D,38*D);Skin.RoundBox(coin,new Vector2(.5f,.5f),Vector2.zero,coin.sizeDelta,new Color32(90,78,64,255),19*D);Skin.RoundBox(coin,new Vector2(.5f,.5f),Vector2.zero,coin.sizeDelta-Vector2.one*4*D,Skin.Paper,17*D);Skin.Art("shell_src_ui_coin",coin,new Vector2(0,.5f),new Vector2(19*D,0),Vector2.one*38*D);var coinText=Skin.Text(Store.Data.coins.ToString(),coin,new Vector2(.5f,.5f),new Vector2(10*D,-2*D),new Vector2(110,36)*D,Mathf.RoundToInt(22*D),Skin.Ink);
            // Source full-screen flex: content paddingTop208/bottom24, two
            // equal flexible sections separated by offer marginTop50dp.
            float contentH=2400-232*D,sectionH=(contentH-50*D)/2,centerY=208*D+sectionH/2;
            MenuUI.Paper(motion,new Vector2(.5f,1),new Vector2(0,-1250),new Vector2(960,1250),true);
            var title=At(motion,"Time's Up title",16*D,centerY-220*D,1080-32*D,88*D);CoreFailureTitle(title,Zh.TimeUp,56);
            var body=At(motion,"Failure phase content",16*D,centerY-112*D,1080-32*D,330*D);
            var phase=Skin.Rect("Offer/loss transition only",body,new Vector2(.5f,1),new Vector2(0,-85*D),new Vector2(body.sizeDelta.x,170*D));var bg=phase.gameObject.AddComponent<CanvasGroup>();
            var offer=Skin.Fill("Time offer",phase);var icon=Skin.Art("game_assets_extratime",offer,new Vector2(.5f,1),new Vector2(0,-55*D),Vector2.one*110*D);
            Skin.Text(Zh.ExtraTime,offer,new Vector2(.5f,1),new Vector2(0,-135*D),new Vector2(220,42)*D,Mathf.RoundToInt(29*D),MenuUI.Ink);
            var failed=Skin.Fill("Loss warning phase",phase);CoreLossSummary(failed,new Vector2(0,-110*D),body.sizeDelta.x,false,true);failed.gameObject.SetActive(false);
            bool warning=false;Action exit=()=>{if(!warning){warning=true;StartCoroutine(CoreFailurePhase(offer,failed,bg,epoch:presentationEpoch));}else{if(!Review){Store.LoseHeart(CurrentLevel);Store.Data.resumeLevel=0;Store.Data.resumePieces=null;Store.Save();}ShowHome();}};
            SourceFailureExit=exit;Skin.IconButton("shell_src_ui_close",black,new Vector2(1,1),new Vector2(-37*D,-101*D),Vector2.one*42*D,exit);
            bool first=!Store.Data.firstContinueUsed&&continuations==0;int cost=90+Math.Min(continuations,2)*30;
            var buttons=Skin.Rect("Failure continue buttons",body,new Vector2(.5f,1),new Vector2(0,-(170+57.5f)*D),new Vector2(Mathf.Min(943.2f,body.sizeDelta.x),115*D));var buttonsGroup=buttons.gameObject.AddComponent<CanvasGroup>();
            Action play=()=>{if(!first&&Store.Data.coins<cost)return;if(first)Store.Data.firstContinueUsed=true;else Store.Data.coins-=cost;Store.Data.climbPendingFailure=0;Store.Save();Remaining=Mathf.Max(0,Remaining)+60;SourceFailureExit=null;continuations++;Finished=false;AnimationBusy=false;ClosePopup();TimerChanged();SaveBoard();};
            var playButton=CoreDialogButton(first?Zh.Free:Zh.PlayOn,buttons,new Vector2(.5f,.5f),Vector2.zero,new Vector2(buttons.sizeDelta.x*(first?.8f:1),115*D),play,first?"blue":"green",first?30:20,0);playButton.interactable=first||Store.Data.coins>=cost;
            if(!first){var playLabel=playButton.GetComponentInChildren<Text>();if(playLabel)playLabel.rectTransform.anchoredPosition+=Vector2.up*14*D;}
            if(!first){Skin.Art("shell_src_ui_coin",buttons,new Vector2(.5f,.5f),new Vector2(-22*D,-24*D),Vector2.one*24*D);Skin.Text(cost.ToString(),buttons,new Vector2(.5f,.5f),new Vector2(20*D,-24*D),new Vector2(85,28)*D,Mathf.RoundToInt(24*D),Color.white);}
            if(!first&&!Review&&rewardedContinues<1&&RewardEntryEnabled(RewardPlacement.ContinueLevel))
            {
                int level=CurrentLevel,epoch=presentationEpoch;
                RewardButton(RewardPlacement.ContinueLevel,RewardLabel(RewardPlacement.ContinueLevel),buttons,new Vector2(.5f,.5f),new Vector2(0,-135*D),new Vector2(buttons.sizeDelta.x*.88f,80*D),()=>RequestReward(RewardPlacement.ContinueLevel,()=>{
                    Store.Data.climbPendingFailure=0;Remaining=Mathf.Max(0,Remaining)+AdPlacements.Slot(RewardPlacement.ContinueLevel).amount;SourceFailureExit=null;continuations++;rewardedContinues++;Finished=false;AnimationBusy=false;ClosePopup();TimerChanged();SaveBoard();
                },()=>CurrentLevel==level&&presentationEpoch==epoch&&Playing&&Finished&&black&&black.gameObject.activeInHierarchy));
            }
            StartCoroutine(CoreFailureMotion(black,motion,group,coin,buttons,buttonsGroup,icon.rectTransform,coinText));
        }
        IEnumerator CoreFailureMotion(RectTransform root,RectTransform panel,CanvasGroup group,RectTransform coin,RectTransform buttons,CanvasGroup buttonGroup,RectTransform icon,Text money)
        {
            int epoch=presentationEpoch;float start=SourceClock.Now;Vector2 coinPos=coin.anchoredPosition,iconPos=icon.anchoredPosition;var coinGroup=coin.gameObject.AddComponent<CanvasGroup>();
            while(root&&root.gameObject.activeInHierarchy&&epoch==presentationEpoch){float t=SourceClock.Now-start;panel.anchoredPosition=new Vector2(0,-SourceMotion.Spring(t,-2400,0,50,10));group.alpha=SourceMotion.DefaultTiming(t/.2f);coin.anchoredPosition=coinPos+Vector2.right*(t<.25f?-1080:SourceMotion.Spring(t-.25f,-1080,0,60,11));coinGroup.alpha=SourceMotion.DefaultTiming((t-.25f)/.3f);float b=t-.45f;buttons.localScale=Vector3.one*(b<0?0:SourceMotion.Spring(b,0,1,55,7));buttonGroup.alpha=SourceMotion.DefaultTiming(b/.25f);float bob=Mathf.Repeat(t,1.8f);icon.anchoredPosition=iconPos+Vector2.up*Mathf.Lerp(0,6*D,bob<.9f?SourceMotion.DefaultTiming(bob/.9f):1-SourceMotion.DefaultTiming((bob-.9f)/.9f));money.text=Store.Data.coins.ToString();yield return null;}
        }
        IEnumerator CoreFailurePhase(RectTransform offer,RectTransform failed,CanvasGroup group,int epoch)
        {
            Vector3 origin=group.transform.localPosition;float start=SourceClock.Now;while(offer&&epoch==presentationEpoch&&SourceClock.Now-start<.18f){float t=SourceMotion.DefaultTiming((SourceClock.Now-start)/.18f);group.alpha=1-t;group.transform.localPosition=origin+Vector3.left*220*D*t;yield return null;}if(!offer||epoch!=presentationEpoch)yield break;offer.gameObject.SetActive(false);failed.gameObject.SetActive(true);start=SourceClock.Now;while(failed&&epoch==presentationEpoch&&SourceClock.Now-start<1){float t=SourceClock.Now-start;group.alpha=SourceMotion.DefaultTiming(t/.22f);group.transform.localPosition=origin+Vector3.right*SourceMotion.Spring(t,220*D,0,70,10);yield return null;}if(group){group.alpha=1;group.transform.localPosition=origin;}
        }
        void CoreLossSummary(Transform parent,Vector2 topCenter,float width,bool onPaper=true,bool warningFirst=false)
        {
            var heart=Skin.Rect("Loss of one life",parent,new Vector2(.5f,1),topCenter,new Vector2(104,88)*D);Skin.Art("shell_src_ui_heartbroken",heart,new Vector2(.5f,.5f),Vector2.zero,Vector2.one*84*D);
            var minus=Skin.Rect("Minus one",heart,new Vector2(1,0),new Vector2(8*D,-4*D),Vector2.one*34*D);Skin.RoundBox(minus,new Vector2(.5f,.5f),Vector2.zero,minus.sizeDelta,new Color32(255,68,68,255),17*D);Skin.RoundBox(minus,new Vector2(.5f,.5f),Vector2.zero,minus.sizeDelta-Vector2.one*4*D,new Color32(204,34,34,255),15*D);Skin.Text("-1",minus,new Vector2(.5f,.5f),new Vector2(0,-2*D),minus.sizeDelta,Mathf.RoundToInt(16*D),Color.white);
            Skin.Text(Zh.ProgressLost,parent,new Vector2(.5f,1),topCenter+(warningFirst?Vector2.up*86*D:Vector2.down*LossLabelOffsetDp*D),new Vector2(width-32*D,LossLabelHeightDp*D),Mathf.RoundToInt(19*D),MenuUI.Ink);
        }
        Button CoreDialogButton(string text,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Action action,string color,int fontDp,float topMarginDp)
        {
            var b=MenuUI.GameButton(text,parent,anchor,pos,size,action,Mathf.RoundToInt(fontDp*D),color!="red");
            if(color=="red")foreach(var label in b.GetComponentsInChildren<Text>())label.color=new Color32(155,59,36,255);
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

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    public partial class InkominoApp
    {
        // Module 2119 is an independent screen. The caller waits for BoardView's win sweep first.
        public void ShowSourceResult()
        {
            Playing=false;Finished=true;screen="result";ClearScreen("game_assets_bg");
            
            var root=Skin.Fill("Source LevelCompleteScreen",Content);
            var motion=root.gameObject.AddComponent<SourceResultMotion>();
            float width=1080,dp=Skin.DP;
            var title=Skin.Rect("Arc title",root,new Vector2(.5f,1),new Vector2(0,-340),new Vector2(900,180));
            motion.BuildTitle(title,Zh.Cleared,78);
            MenuUI.Label(Zh.Level(CurrentLevel),root,new Vector2(.5f,1),new Vector2(0,-520),new Vector2(860,64),46,MenuUI.Muted,TextAnchor.MiddleCenter);

            float buttonH=204;
            var rewardRoot=MenuUI.Paper(root,new Vector2(.5f,1),new Vector2(0,-700),new Vector2(920,196));rewardRoot.name="Source reward";
            MenuUI.Plate(rewardRoot,new Vector2(0,.5f),new Vector2(118,0),Vector2.one*140,MenuUI.Lemon,36);
            var pile=Skin.Rect("Coin reward",rewardRoot,new Vector2(0,.5f),new Vector2(118,0),Vector2.one*116);
            MenuUI.Art("reward_coins",pile,new Vector2(.5f,.5f),Vector2.zero,Vector2.one*104);
            MenuUI.Label(Zh.CoinsGot,rewardRoot,new Vector2(0,.5f),new Vector2(490,34),new Vector2(540,48),44,MenuUI.Muted,TextAnchor.MiddleLeft,true);
            var amount=MenuUI.Label("+"+reward,rewardRoot,new Vector2(0,.5f),new Vector2(490,-32),new Vector2(540,76),72,MenuUI.Coral,TextAnchor.MiddleLeft);
            motion.Reward=rewardRoot;motion.RewardAlpha=rewardRoot.gameObject.AddComponent<CanvasGroup>();
            motion.Coin=pile;
            float used=Mathf.Max(0,Board!=null?Board.Level.time-Remaining:0);
            int steps=Board!=null?Board.Pieces.Count:0;
            int stars=Board==null||used<=0?3:used<=Board.Level.time*.45f?3:used<=Board.Level.time*.75f?2:1;
            var stats=Skin.Rect("Result stats",root,new Vector2(.5f,1),new Vector2(0,-900),new Vector2(920,136));
            string[] statTitles={Zh.TimeStat,Zh.StepsStat,Zh.StarsStat};
            string[] statValues={FormatTime(used),steps.ToString(),stars+"/3"};
            Color32[] statFaces={MenuUI.LilacWash,MenuUI.Sky,MenuUI.Lemon};
            for(int i=0;i<3;i++)
            {
                var cell=Skin.Rect("Result stat "+i,stats,Vector2.one*.5f,new Vector2((i-1)*300,0),new Vector2(276,128));
                MenuUI.Extrude(cell,Vector2.one*.5f,Vector2.zero,cell.sizeDelta,statFaces[i],MenuUI.WarmShadow,32);
                MenuUI.Label(statTitles[i],cell,Vector2.one*.5f,new Vector2(0,36),new Vector2(240,44),42,MenuUI.Muted,TextAnchor.MiddleCenter,true);
                MenuUI.Label(statValues[i],cell,Vector2.one*.5f,new Vector2(0,-22),new Vector2(240,64),52,MenuUI.Ink,TextAnchor.MiddleCenter);
            }

            var buttonRoot=Skin.Rect("Source Continue entrance",root,new Vector2(.5f,1),new Vector2(0,-1288),new Vector2(920,buttonH));
            int level=CurrentLevel;bool review=Review;
            var button=MenuUI.GameButton(Zh.NextLevel,buttonRoot,new Vector2(.5f,.5f),Vector2.zero,buttonRoot.sizeDelta,()=>{Feel.Success();AdPlacements.TryLevelEnd(Store,level,review,()=>{if(level<500){if(review)StartLevel(level+1,true);else EnterLevelWithLoading(level+1);}else ShowHome();});},72);
            foreach(var label in button.GetComponentsInChildren<SourceTrackedText>()){label.LetterSpacing=.5f*dp;var clip=label.transform.parent as RectTransform;if(clip)clip.anchoredPosition=Vector2.zero;}
            var opacity=button as SourceOpacityButton;if(opacity)opacity.ActiveOpacity=.85f;
            bool showDouble=!review&&reward>0&&RewardEntryEnabled(RewardPlacement.DoubleCoins);
            if(showDouble)
            {
                buttonRoot.anchoredPosition+=Vector2.down*80;bool doubled=false;int baseReward=reward,epoch=presentationEpoch;Button doubleButton=null;
                doubleButton=RewardButton(RewardPlacement.DoubleCoins,Zh.WatchAdAgain,buttonRoot,new Vector2(.5f,.5f),new Vector2(0,buttonH/2+118),new Vector2(790,168),()=>RequestReward(RewardPlacement.DoubleCoins,()=>{
                    doubled=true;int extra=baseReward*Mathf.Max(0,AdPlacements.Slot(RewardPlacement.DoubleCoins).amount-1);Store.Data.coins+=extra;amount.text="+"+(baseReward+extra);doubleButton.interactable=false;foreach(var label in doubleButton.GetComponentsInChildren<Text>())if(label.name=="Rewarded CTA label")label.text=Zh.AdDoubled;SoundBank.Play("coin");Feel.Success();
                },()=>!doubled&&presentationEpoch==epoch&&screen=="result"&&CurrentLevel==level),true);
            }
            MenuUI.SecondaryButton(Zh.PlayAgain,root,new Vector2(.5f,1),new Vector2(0,showDouble?-1648:-1504),new Vector2(640,104),Retry,44);
            MenuUI.SecondaryButton(Zh.BackHome,root,new Vector2(.5f,1),new Vector2(0,showDouble?-1788:-1648),new Vector2(560,96),ShowHome,44);
            motion.Button=buttonRoot;motion.ButtonAlpha=buttonRoot.gameObject.AddComponent<CanvasGroup>();motion.Width=width;
            motion.ConfettiParent=Skin.Fill("Source result confetti",root);motion.ConfettiParent.SetAsFirstSibling();
            if(!review&&CurrentLevel>=41&&Store.Data.climb.joined&&!Store.Data.climb.isOut)
            {
                var climb=Store.Data.climb;
                MenuUI.SecondaryButton(ClimbActivity.AtTop(climb)?"集章完成 · 领取奖励":"快乐集章 "+climb.stage+" / 7",root,new Vector2(.5f,0),new Vector2(0,210),new Vector2(720,110),ShowClimb,46);
            }
            foreach(var control in root.GetComponentsInChildren<Button>())MenuMotion.Press(control);
            motion.Begin();
        }
    }

    public sealed class SourceResultMotion:MonoBehaviour
    {
        sealed class Letter{public RectTransform Rect;public CanvasGroup Alpha;public float Delay;}
        readonly List<Letter> letters=new List<Letter>();
        public RectTransform Reward,Button,ConfettiParent,Coin;public CanvasGroup RewardAlpha,ButtonAlpha;public float Width;
        float begin,titleDone;Vector2 buttonPosition;bool confettiStarted;
        public float Elapsed{get{return SourceClock.Now-begin;}}
        public float RewardStart{get{return titleDone;}}
        public static float TextWidth(string text,int fontSize)
        {
            var font=Skin.FontFor(text);font.RequestCharactersInTexture(text,fontSize,FontStyle.Normal);float width=0;
            foreach(char c in text){CharacterInfo info;if(font.GetCharacterInfo(c,out info,fontSize,FontStyle.Normal))width+=info.advance;else width+=fontSize*.45f;}return width;
        }
        public void BuildTitle(RectTransform title,string text,float fontSize)
        {
            float dp=Skin.DP;int fs=Mathf.RoundToInt(fontSize);float total=TextWidth(text,fs),x=-total/2;
            // Reward feedback overlaps the title instead of waiting for a spring
            // to reach an imperceptible rest threshold.
            float stagger=.035f;
            var row=Skin.Rect("Arc title row",title,new Vector2(.5f,.5f),Vector2.zero,new Vector2(total,100*dp));row.localScale=Vector3.one*Mathf.Min(1,title.sizeDelta.x/Mathf.Max(1,total));
            for(int i=0;i<text.Length;i++)
            {
                string ch=text[i]==' '?"\u00A0":text[i].ToString();float advance=TextWidth(text[i].ToString(),fs),u=text.Length>1?(i-(text.Length-1)/2f)/((text.Length-1)/2f):0;
                var r=Skin.Rect("Title letter "+i,row,new Vector2(.5f,.5f),new Vector2(x+advance/2,0),new Vector2(advance,100*dp));r.localRotation=Quaternion.identity;x+=advance;
                TitleGlyph(ch,r,fs,InkTheme.Ink);
                letters.Add(new Letter{Rect=r,Alpha=r.gameObject.AddComponent<CanvasGroup>(),Delay=i*stagger});
            }
            titleDone=.100f;
        }
        static Text TitleGlyph(string ch,RectTransform parent,int size,Color color)
        {var t=Skin.Text(ch,parent,new Vector2(.5f,.5f),Vector2.zero,new Vector2(size*2,size*1.7f),size,color);t.horizontalOverflow=HorizontalWrapMode.Overflow;t.verticalOverflow=VerticalWrapMode.Overflow;return t;}
        public void Begin(){begin=SourceClock.Now;buttonPosition=Button.anchoredPosition;Apply(0);}
        void Update(){Apply(Elapsed);}
        void Apply(float t)
        {
            foreach(var letter in letters){float age=t-letter.Delay;letter.Alpha.alpha=age<=0?0:SourceMotion.DefaultTiming(age/.160f);letter.Rect.localScale=Vector3.one*(age<0?.2f:age>2?1:SourceMotion.Spring(age,.2f,1,180,6));}
            float rewardAge=t-titleDone;RewardAlpha.alpha=SourceMotion.OutCubic(rewardAge/.160f);Reward.localScale=Vector3.one*(MenuMotion.Reduced?1:rewardAge>1.05f?1:SourceMotion.Spring(rewardAge,.55f,1,240,8));
            if(Coin)Coin.localScale=Vector3.one*(MenuMotion.Reduced||rewardAge<0?1:rewardAge>1.15f?1:SourceMotion.Spring(rewardAge,.22f,1,320,9));
            float buttonAge=t-.160f;ButtonAlpha.alpha=SourceMotion.OutCubic(buttonAge/.140f);Button.anchoredPosition=buttonPosition+Vector2.down*(MenuMotion.Reduced?0:36*(1-SourceMotion.OutCubic(buttonAge/.180f)));Button.localScale=Vector3.one*(MenuMotion.Reduced?1:buttonAge>1.05f?1:SourceMotion.Spring(Mathf.Max(0,buttonAge),.7f,1,230,8));
            ButtonAlpha.interactable=ButtonAlpha.blocksRaycasts=buttonAge>=.160f;
            if(!confettiStarted&&rewardAge>=0){confettiStarted=true;var confetti=ConfettiParent.gameObject.AddComponent<SourceResultConfetti>();confetti.Begin();MenuMotion.Celebrate(Reward);}
        }
    }

    public sealed class SourceResultConfetti:MonoBehaviour
    {
        sealed class Bit{public RectTransform Rect;public CanvasGroup Alpha;public float Delay,Rise,RiseDuration,FallDuration,Spin,X;}
        readonly List<Bit> bits=new List<Bit>();float begin;
        public void Begin()
        {
            begin=SourceClock.Now;string[] colors={"#FF638B","#75DABA","#FFE36B","#B6A0F5","#FFA8D2","#8DCFFC","#6645AA","#D6EA81"};var random=new System.Random();Func<float> rand=()=> (float)random.NextDouble();float dp=Skin.DP;
            for(int i=0;i<36;i++){float size=(8+rand()*9)*dp;Color color;ColorUtility.TryParseHtmlString(colors[i%colors.Length],out color);var g=Skin.RoundBox(transform,new Vector2(0,0),Vector2.zero,new Vector2(size,size*(.55f+rand()*.9f)),color,3*dp);var r=g.rectTransform;var bit=new Bit{Rect=r,Alpha=r.gameObject.AddComponent<CanvasGroup>(),Delay=rand()*.140f,Rise=(380+rand()*420)*dp,RiseDuration=(320+rand()*180)/1000,FallDuration=(800+rand()*500)/1000,Spin=540+rand()*720,X=(.06f+rand()*.88f)*1080};bits.Add(bit);}Apply();
        }
        void Update(){Apply();}
        void Apply()
        {
            float dp=Skin.DP;foreach(var bit in bits){float t=SourceClock.Now-begin-bit.Delay,phase=bit.RiseDuration+bit.FallDuration,y=t<=phase?bit.Rise*SourceMotion.OutQuad(t/bit.RiseDuration):Mathf.Lerp(bit.Rise,-800*dp,Mathf.Pow(Mathf.Clamp01((t-phase)/bit.FallDuration),2));bit.Rect.anchoredPosition=new Vector2(bit.X,y);bit.Rect.localRotation=Quaternion.Euler(0,0,-bit.Spin*SourceMotion.DefaultTiming(t/phase));bit.Alpha.alpha=t<0?0:t<phase?SourceMotion.DefaultTiming(t/.080f):1-SourceMotion.DefaultTiming((t-phase-bit.FallDuration+.500f)/.500f);}
        }
    }
}

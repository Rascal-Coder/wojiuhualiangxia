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
            var title=Skin.Rect("Arc title",root,new Vector2(.5f,1),new Vector2(0,-420),new Vector2(920,240));
            motion.BuildTitle(title,Zh.Cleared,128);
            var seal=MenuUI.Art("ink_stamp",root,new Vector2(.5f,1),new Vector2(360,-400),new Vector2(150,150));MenuMotion.Celebrate(seal.rectTransform);
            MenuUI.Label(Zh.Level(CurrentLevel),root,new Vector2(.5f,1),new Vector2(0,-620),new Vector2(900,70),54,MenuUI.Muted,TextAnchor.MiddleCenter);

            // Root title margins: top44, height100, bottom4; root gap8.
            // The remaining flex centerZone centres reward(200 + vertical margins8), gap8,
            // and the auto-width button(104 + top8 + bottom40).
            float buttonH=188;
            var rewardRoot=MenuUI.Paper(root,new Vector2(.5f,1),new Vector2(0,-980),new Vector2(860,520));rewardRoot.name="Source reward";
            MenuUI.Label(Zh.CoinsGot,rewardRoot,new Vector2(.5f,1),new Vector2(0,-58),new Vector2(700,70),48,MenuUI.Muted,TextAnchor.MiddleCenter,true);
            var pile=Skin.Rect("Coin reward",rewardRoot,new Vector2(.5f,.5f),new Vector2(0,36),Vector2.one*280);
            MenuUI.Art("reward_coins",pile,new Vector2(.5f,.5f),Vector2.zero,Vector2.one*280);
            var amount=MenuUI.Label("+"+reward,rewardRoot,new Vector2(.5f,.5f),new Vector2(0,-168),new Vector2(700,90),78,MenuUI.Coral,TextAnchor.MiddleCenter);
            motion.Reward=rewardRoot;motion.RewardAlpha=rewardRoot.gameObject.AddComponent<CanvasGroup>();
            motion.Coin=pile;
            float used=Mathf.Max(0,Board!=null?Board.Level.time-Remaining:0);
            int steps=Board!=null?Board.Pieces.Count:0;
            int stars=Board==null||used<=0?3:used<=Board.Level.time*.45f?3:used<=Board.Level.time*.75f?2:1;
            var stats=Skin.Rect("Result stats",root,new Vector2(.5f,1),new Vector2(0,-1320),new Vector2(920,180));
            string[] statTitles={Zh.TimeStat,Zh.StepsStat,Zh.StarsStat};
            string[] statValues={FormatTime(used),steps.ToString(),stars+"/3"};
            Color32[] statFaces={MenuUI.LilacWash,MenuUI.Sky,MenuUI.Lemon};
            for(int i=0;i<3;i++)
            {
                var cell=Skin.Rect("Result stat "+i,stats,Vector2.one*.5f,new Vector2((i-1)*300,0),new Vector2(260,168));
                MenuUI.Extrude(cell,Vector2.one*.5f,Vector2.zero,cell.sizeDelta,statFaces[i],new Color32(145,110,196,70),48);
                MenuUI.Label(statTitles[i],cell,Vector2.one*.5f,new Vector2(0,42),new Vector2(220,48),42,MenuUI.Muted,TextAnchor.MiddleCenter,true);
                MenuUI.Label(statValues[i],cell,Vector2.one*.5f,new Vector2(0,-22),new Vector2(220,70),52,MenuUI.Ink,TextAnchor.MiddleCenter);
            }

            // Source no-ad branch: module2120 AutoWidthGreenButton, activeOpacity=.85.
            var buttonRoot=Skin.Rect("Source Continue entrance",root,new Vector2(.5f,1),new Vector2(0,-1680),new Vector2(800,buttonH));
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
            MenuUI.SecondaryButton(Zh.PlayAgain,root,new Vector2(.5f,1),new Vector2(0,showDouble?-1980:-1900),new Vector2(720,140),Retry,50);
            MenuUI.SecondaryButton(Zh.BackHome,root,new Vector2(.5f,0),new Vector2(0,70),new Vector2(720,120),ShowHome,48);
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
            float rewardAge=t-titleDone;RewardAlpha.alpha=SourceMotion.OutCubic(rewardAge/.160f);Reward.localScale=Vector3.one*(MenuMotion.Reduced?1:rewardAge>1.1f?1:SourceMotion.Spring(rewardAge,.62f,1,170,7));
            if(Coin)Coin.localScale=Vector3.one*(MenuMotion.Reduced||rewardAge<0?1:rewardAge>1.2f?1:SourceMotion.Spring(rewardAge,.4f,1,200,7));
            float buttonAge=t-.180f;ButtonAlpha.alpha=SourceMotion.OutCubic(buttonAge/.160f);Button.anchoredPosition=buttonPosition+Vector2.down*(MenuMotion.Reduced?0:48*(1-SourceMotion.OutCubic(buttonAge/.200f)));
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

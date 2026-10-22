using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Inkomino
{
    public partial class InkominoApp
    {
        public bool TransitioningLevel { get; private set; }
        public SourceGameplayLoadingView GameplayLoadingView { get; private set; }

        // Interactive navigation uses this entry point; direct StartLevel remains
        // available to the existing deterministic board and grid QA runners.
        public void EnterLevelWithLoading(int id,bool review=false,bool resume=false)
        {
            if(!BootReady||TransitioningLevel)return;
            SaveBoard();
            TransitioningLevel=true;
            StartCoroutine(EnterSourceLevel(id,review,resume));
        }

        IEnumerator EnterSourceLevel(int id,bool review,bool resume)
        {
            bool previousPlaying=Playing,previousBusy=AnimationBusy,entered=false;
            Playing=false;AnimationBusy=true;
            if(BoardView)BoardView.Cancel();
            ClearPopups();
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
            try
            {
                GameplayLoadingView=SourceGameplayLoadingView.Create(Stage);
                while(GameplayLoadingView&&GameplayLoadingView.Elapsed<SourceGameplayLoadingView.DisplaySeconds)
                {
                    GameplayLoadingView.transform.SetAsLastSibling();
                    yield return null;
                }
                if(GameplayLoadingView)yield return GameplayLoadingView.FadeOut();
                if(GameplayLoadingView)
                {
                    GameplayLoadingView.gameObject.SetActive(false);
                    Destroy(GameplayLoadingView.gameObject);
                    GameplayLoadingView=null;
                }
                StartLevel(id,review,resume);
                entered=true;
            }
            finally
            {
                if(GameplayLoadingView)
                {
                    GameplayLoadingView.gameObject.SetActive(false);
                    Destroy(GameplayLoadingView.gameObject);
                    GameplayLoadingView=null;
                }
                TransitioningLevel=false;
                // StartLevel may have begun the first-level tutorial. Preserve
                // its busy flag; only restore the old state on cancellation.
                if(!entered){Playing=previousPlaying;AnimationBusy=previousBusy;}
            }
        }
    }

    // Original module2175 / fn12497, separate from startup LoadingScreen.
    public sealed class SourceGameplayLoadingView:MonoBehaviour
    {
        public const float DisplaySeconds=2.6f,ExitSeconds=.25f;
        public float Elapsed { get { return Mathf.Max(0,SourceClock.Now-begin); } }
        public int TipIndex { get; private set; }
        public string TipTitle { get; private set; }
        public Text Description { get; private set; }
        public Text Label { get; private set; }
        public RectTransform Icon { get; private set; }
        public RectTransform Title { get; private set; }
        public CanvasGroup Group { get; private set; }
        CanvasGroup iconAlpha,titleAlpha,labelAlpha;
        float begin;
        bool exiting;

        // The source profile has five entries. Its hammer hint is omitted here
        // because this port has no start-of-level hammer. Extra time describes
        // the implemented +60-second continuation, rather than a new booster.
        static readonly string[] titles={"画笔","时间冻结","放大镜","额外时间"};
        static readonly string[] descriptions={
            "自动为你画出一个正确形状，并将它固定。",
            "暂停倒计时10秒，安心规划下一步。",
            "显示一个正确形状的位置，帮助你继续拼图。",
            "时间不足时，续关可以增加60秒。"
        };
        static readonly string[] icons={"game_assets_pen","game_assets_freeze","game_assets_lens","game_assets_extratime"};

        public static SourceGameplayLoadingView Create(RectTransform stage,int tipIndex=-1)
        {
            var root=Skin.Fill("Source gameplay loading",stage);
            var view=root.gameObject.AddComponent<SourceGameplayLoadingView>();
            view.begin=SourceClock.Now;
            view.TipIndex=tipIndex<0?Random.Range(0,titles.Length):Mathf.Clamp(tipIndex,0,titles.Length-1);
            view.TipTitle=titles[view.TipIndex];
            var blocker=root.gameObject.AddComponent<Image>();
            blocker.color=new Color32(29,78,216,255);blocker.raycastTarget=true;
            view.Group=root.gameObject.AddComponent<CanvasGroup>();view.Group.blocksRaycasts=true;view.Group.interactable=false;
            var clip=Skin.Fill("Source gameplay artwork viewport",root);clip.gameObject.AddComponent<RectMask2D>();
            var background=Skin.Art("shell_src_ui_gameplaysplash",clip,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1080,2400),false);
            if(background.sprite)
            {
                float cover=Mathf.Max(1080/background.sprite.rect.width,2400/background.sprite.rect.height);
                background.rectTransform.sizeDelta=background.sprite.rect.size*cover;
            }

            float dp=Skin.DP;
            int fs=Mathf.Min(80,Mathf.Max(44,Mathf.FloorToInt(640f/view.TipTitle.Length)));
            float arcHeight=Mathf.Round(fs*1.625f)*dp,descriptionHeight=88*dp,margin=32*dp;
            view.Title=Skin.Rect("Source splash title and description",root,new Vector2(.5f,1),
                new Vector2(0,-240-(arcHeight+margin+descriptionHeight)/2),new Vector2(1080-48*dp,arcHeight+margin+descriptionHeight));
            view.titleAlpha=view.Title.gameObject.AddComponent<CanvasGroup>();
            var arc=Skin.Rect("Source curved splash title",view.Title,new Vector2(.5f,1),new Vector2(0,-arcHeight/2),new Vector2(1080-32*dp,arcHeight));
            BuildArcTitle(arc,view.TipTitle,fs,Mathf.Round(fs*.35f));
            view.Description=Skin.Text(descriptions[view.TipIndex],view.Title,new Vector2(.5f,1),
                new Vector2(0,-arcHeight-margin-descriptionHeight/2),new Vector2(1080-48*dp,descriptionHeight),Mathf.RoundToInt(18*dp),Color.white,dp);
            view.Description.font=Skin.ChineseFont;
            SourceBlurShadow.Attach(view.Description,new Color(0,0,0,.55f),new Vector2(0,-2*dp),4*dp);

            view.Icon=Skin.Rect("Source splash icon spring",root,new Vector2(.5f,.5f),Vector2.zero,Vector2.one*220*dp);
            view.iconAlpha=view.Icon.gameObject.AddComponent<CanvasGroup>();
            Skin.Art(icons[view.TipIndex],view.Icon,new Vector2(.5f,.5f),Vector2.zero,view.Icon.sizeDelta);
            var loading=Skin.Rect("Source gameplay loading label",root,new Vector2(.5f,0),new Vector2(0,288+18*dp),new Vector2(940,36*dp));
            view.labelAlpha=loading.gameObject.AddComponent<CanvasGroup>();
            view.Label=Skin.Text("加载中……",loading,new Vector2(.5f,.5f),Vector2.zero,loading.sizeDelta,Mathf.RoundToInt(24*dp),Color.white,3*dp);
            view.Label.font=Skin.ChineseFont;
            SourceBlurShadow.Attach(view.Label,new Color(0,0,0,.6f),new Vector2(0,-2*dp),5*dp);
            view.ApplyAtElapsed(0);
            return view;
        }

        static void BuildArcTitle(RectTransform root,string text,int fontSizeDp,float depthDp)
        {
            float dp=Skin.DP;int fontSize=Mathf.RoundToInt(fontSizeDp*dp);
            float total=SourceResultMotion.TextWidth(text,fontSize),x=-total/2;
            var row=Skin.Rect("Source splash arc glyph row",root,new Vector2(.5f,.5f),Vector2.zero,new Vector2(total,root.sizeDelta.y));
            row.localScale=Vector3.one*Mathf.Min(1,(root.sizeDelta.x-32*dp)/Mathf.Max(1,total));
            for(int i=0;i<text.Length;i++)
            {
                string ch=text[i].ToString();float advance=SourceResultMotion.TextWidth(ch,fontSize);
                float u=text.Length>1?(i-(text.Length-1)/2f)/((text.Length-1)/2f):0;
                var letter=Skin.Rect("Source splash title glyph "+i,row,new Vector2(.5f,.5f),new Vector2(x+advance/2,(1-u*u)*depthDp*dp),new Vector2(advance,root.sizeDelta.y));
                letter.localRotation=Quaternion.Euler(0,0,-u*18);x+=advance;
                for(int j=0;j<16;j++)
                {
                    float angle=j*Mathf.PI/8;
                    var stroke=ArcGlyph(ch,letter,fontSize,new Color32(217,119,6,255));
                    stroke.rectTransform.localScale=Vector3.one*1.12f;
                    stroke.rectTransform.anchoredPosition=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*8*dp;
                }
                var fill=ArcGlyph(ch,letter,fontSize,new Color32(255,248,231,255));
                SourceBlurShadow.Attach(fill,new Color(0,0,0,.3f),new Vector2(0,-2*dp),3*dp);
            }
        }

        static Text ArcGlyph(string text,RectTransform parent,int size,Color color)
        {
            var t=Skin.Text(text,parent,new Vector2(.5f,.5f),Vector2.zero,new Vector2(size*2,size*1.7f),size,color);
            t.font=Skin.ChineseFont;t.resizeTextForBestFit=false;
            t.horizontalOverflow=HorizontalWrapMode.Overflow;t.verticalOverflow=VerticalWrapMode.Overflow;
            return t;
        }

        void Update(){if(!exiting)ApplyAtElapsed(Elapsed);}
        public void ApplyAtElapsed(float elapsed)
        {
            Group.alpha=SourceMotion.DefaultTiming(elapsed/.2f);
            Icon.localScale=Vector3.one*SourceMotion.Spring(elapsed,.5f,1,80,6);
            iconAlpha.alpha=SourceMotion.DefaultTiming(elapsed/.25f);
            float titleAge=elapsed-.15f;
            Title.localScale=Vector3.one*(titleAge<=0?0:SourceMotion.Spring(titleAge,0,1,100,6));
            titleAlpha.alpha=SourceMotion.DefaultTiming(titleAge/.2f);
            float pulse=Mathf.Repeat(elapsed,1.4f);
            float blink=pulse<.7f?Mathf.Lerp(1,.4f,SourceMotion.DefaultTiming(pulse/.7f)):Mathf.Lerp(.4f,1,SourceMotion.DefaultTiming((pulse-.7f)/.7f));
            labelAlpha.alpha=SourceMotion.DefaultTiming((elapsed-.35f)/.25f)*blink;
        }

        public IEnumerator FadeOut()
        {
            exiting=true;float started=SourceClock.Now;float startScale=Icon.localScale.x;
            float startTitleAlpha=titleAlpha.alpha,startLabelAlpha=labelAlpha.alpha;
            while(SourceClock.Now-started<ExitSeconds)
            {
                float t=SourceClock.Now-started;
                Group.alpha=1-SourceMotion.DefaultTiming(t/ExitSeconds);
                Icon.localScale=Vector3.one*Mathf.Lerp(startScale,1.15f,SourceMotion.DefaultTiming(t/ExitSeconds));
                titleAlpha.alpha=startTitleAlpha*(1-SourceMotion.DefaultTiming(t/.2f));
                labelAlpha.alpha=startLabelAlpha*(1-SourceMotion.DefaultTiming(t/.2f));
                yield return null;
            }
            Group.alpha=0;
        }
    }
}

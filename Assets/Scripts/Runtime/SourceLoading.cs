using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    public partial class InkominoApp
    {
        public bool BootReady { get; private set; }
        public SourceLoadingView LoadingView { get; private set; }

        void InitializeGameData()
        {
            Data=JsonUtility.FromJson<GameData>(Resources.Load<TextAsset>("Data/levels").text);Data.Normalize();Shapes=new ShapeLibrary(Data.shapes);
            string saveRoot=IsQA?QAOutput():Application.persistentDataPath;
            string savePath=System.IO.Path.Combine(saveRoot,IsQA?"qa-profile.json":"profile-v1.json");
#if UNITY_EDITOR || UNITY_STANDALONE
            if(!IsQA&&!System.IO.File.Exists(savePath))
            {
                // Product/window title is now Chinese. Preserve a desktop save
                // from the previous product directory when upgrading this build.
                var parent=System.IO.Directory.GetParent(saveRoot);
                foreach(string previousName in new[]{"墨彩拼图","Inkomino"})
                {
                    string previous=parent==null?"":System.IO.Path.Combine(parent.FullName,previousName,"profile-v1.json");
                    if(!System.IO.File.Exists(previous))continue;
                    System.IO.Directory.CreateDirectory(saveRoot);System.IO.File.Copy(previous,savePath,false);break;
                }
            }
#endif
            Store=new SaveStore(savePath);
            SoundBank.Init(gameObject,Store.Data.sound,Store.Data.music);Store.Changed+=UpdateWallet;
        }

        IEnumerator SourceStartup()
        {
            var startupWatch=System.Diagnostics.Stopwatch.StartNew();
            LoadingView=SourceLoadingView.Create(Stage);
#if UNITY_EDITOR || UNITY_STANDALONE
            LoadingView.HoldForQA=Environment.GetCommandLineArgs().Contains("-inkominoChineseQA");
#endif
            // Present the original loading artwork before parsing the level data
            // or warming the menu/board textures. The label remains animated while
            // Unity's resource requests complete; no synthetic percent is shown.
            yield return null;
#if WEIXINMINIGAME && !UNITY_EDITOR
            // Keep WX runtime linked and wait for its local storage/input bridge
            // before accessing the profile. No ad or payment SDK is initialized.
            bool ready=false;WeChatWASM.WX.InitSDK(code=>ready=true);float deadline=Time.unscaledTime+30;
            while(!ready&&Time.unscaledTime<deadline)yield return null;
            if(!ready){LoadingView.StatusMessage="初始化超时，请重新进入";yield break;}
            WeChatSafeLayout.Enable();
#endif
            InitializeGameData();
            if(!Store.PersistenceAvailable){LoadingView.StatusMessage="存档读取失败，请重新进入";yield break;}
            // Valid returning profiles start cloud login in the background;
            // only the first local installation waits for an initial restore.
            yield return InitializeCloudSave();
            long dataReadyMs=startupWatch.ElapsedMilliseconds;int warmed=0;
            foreach(string key in SourceLoadingView.StartupResources(Store.HasCompletedLevel))
            {
#if WEIXINMINIGAME && !UNITY_EDITOR
                SourceArt.Texture(key);yield return null;
#else
                var request=Resources.LoadAsync<Texture2D>("Art/"+key);
                while(!request.isDone&&LoadingView.Elapsed<SourceLoadingView.MaximumSeconds)yield return null;
#endif
                warmed++;
                if(LoadingView.Elapsed>=SourceLoadingView.MaximumSeconds)break;
            }
            while(LoadingView.Elapsed<SourceLoadingView.MinimumSeconds||LoadingView.HoldForQA)yield return null;
            long warmReadyMs=startupWatch.ElapsedMilliseconds;
            cloudRefreshAfterStartup=false;
            ShowStartupDestination();long screenReadyMs=startupWatch.ElapsedMilliseconds;LoadingView.transform.SetAsLastSibling();
            yield return LoadingView.FadeOut();
            LoadingView.gameObject.SetActive(false);Destroy(LoadingView.gameObject);LoadingView=null;BootReady=true;CompleteCloudSaveStartup();StartCoroutine(Clock());
#if WEIXINMINIGAME && !UNITY_EDITOR
            FriendsRanking.SubmitProgress(Store.Data.wins);
            WeChatWASM.WX.ReportGameStart();
            Debug.Log("INKOMINO_STARTUP ready_ms="+startupWatch.ElapsedMilliseconds+" data_ready_ms="+dataReadyMs+" warm_ready_ms="+warmReadyMs+" screen_ready_ms="+screenReadyMs+" warmed_images="+warmed+" max_decode_ms="+SourceArt.MaximumDecodeMilliseconds+" max_decode_image="+SourceArt.SlowestDecodeKey+" cloud_waited="+ShouldAwaitInitialCloud+" storage=wx.setStorageSync");
#endif
        }

        internal void ShowStartupDestination()
        {
            // AppContent effect22 / Hermes fn15274: no completed progress opens
            // level one automatically. Keep returning players' normal home flow.
            if(!Store.HasCompletedLevel)
                StartLevel(1,false,Store.Data.drawIntroSeen&&Store.Data.resumeLevel==1&&Store.Data.resumePieces!=null);
            else ShowHome();
        }
    }

    // LoadingScreen module1711/fid11424. The game-specific config supplies the paper art,
    // logo center47%, cream backdrop and brown label. Its JSX does not render
    // progressTrack/Fill/Text (those are unused generic styles in that module).
    public sealed class SourceLoadingView:MonoBehaviour
    {
        public const float MinimumSeconds=1.2f,MaximumSeconds=10f,FadeSeconds=.3f;
        public bool HoldForQA;
        public string StatusMessage;
        public float Elapsed { get { return Mathf.Max(0,Time.unscaledTime-begin); } }
        public Text Label { get; private set; }
        public RectTransform Logo { get; private set; }
        public CanvasGroup Group { get; private set; }
        RectTransform progressFill;
        float begin;
        public static readonly string[] WarmResources={
            "game_assets_homebg","game_assets_bg","game_assets_shopbg","game_assets_paper","game_assets_wood",
            "game_assets_facets","game_assets_facetsb","game_assets_facetsc","game_assets_levelsignmosaic",
            "game_assets_btnpause","game_assets_btnsettings","game_assets_pen","game_assets_freeze","game_assets_lens",
            "game_assets_event_frame","game_assets_event_ribboncap","game_assets_event_ribbonmid",
            "game_assets_btnframecap","game_assets_btnframemid",
            "game_assets_nav_barc","game_assets_nav_bart","game_assets_nav_barl","game_assets_nav_bartl",
            "game_assets_nav_tabactive","game_assets_nav_tabhome","game_assets_nav_tabshop","game_assets_nav_tabrank",
            "shell_src_ui_heart","shell_src_ui_coin","shell_src_ui_greenbase","shell_src_ui_plus","shell_src_ui_close",
            "shell_src_ui_dailyrewardcardicon","shell_src_ui_avatars_avatar_1","shell_src_ui_avatars_avatar_2"
        };
        public static string[] StartupResources(bool home)
        {
            // Decode only the initial screen's images. Shop, other avatars and
            // board art are loaded on demand instead of blocking the home screen.
            return home?new[]{"game_assets_homebg"}:new[]{"game_assets_bg","game_assets_paper","game_assets_wood","game_assets_facets","shell_src_ui_tutorialhand"};
        }
        public static SourceLoadingView Create(RectTransform stage)
        {
            var root=Skin.Fill("Source startup loading",stage);var view=root.gameObject.AddComponent<SourceLoadingView>();view.begin=Time.unscaledTime;
            var backdrop=root.gameObject.AddComponent<Image>();backdrop.color=InkTheme.Paper;backdrop.raycastTarget=true;
            view.Group=root.gameObject.AddComponent<CanvasGroup>();view.Group.blocksRaycasts=true;
            var clip=Skin.Fill("Original loading artwork viewport",root);clip.gameObject.AddComponent<RectMask2D>();
            float cover=Mathf.Max(1080f/1024,2400f/2207),drawWidth=1024*cover,drawHeight=2207*cover;
            Skin.Art("source_loading_background",clip,new Vector2(.5f,.5f),Vector2.zero,new Vector2(drawWidth,drawHeight),false);
            var dots=Skin.Fill("Sticker journal dots",clip).gameObject.AddComponent<StickerBackdrop>();dots.raycastTarget=false;
            float dp=Skin.DP,windowW=1080/dp,windowH=2400/dp;
            view.Logo=InkTheme.Brand(root,new Vector2(.5f,1),new Vector2(0,-760),new Vector2(960,340));
            var illustration=Skin.Rect("Startup journal illustration",root,new Vector2(.5f,1),new Vector2(0,-1340),new Vector2(800,820));
            var art=illustration.gameObject.AddComponent<Image>();art.sprite=MenuUI.BrandSprite("brand_startup");art.preserveAspect=true;art.raycastTarget=false;
            view.Label=MenuUI.Label("正在准备拼图",root,new Vector2(.5f,1),new Vector2(0,-1900),new Vector2(920,120),48,InkTheme.Muted,TextAnchor.MiddleCenter);
            var track=Skin.Rect("Source startup progress",root,new Vector2(.5f,1),new Vector2(0,-2040),new Vector2(520,28));
            Skin.RoundBox(track,Vector2.one*.5f,Vector2.zero,track.sizeDelta,new Color32(255,236,220,255),14);
            var fill=Skin.RoundBox(track,new Vector2(0,.5f),new Vector2(8,0),new Vector2(28,18),MenuUI.Coral,9);
            fill.name="Source startup progress fill";fill.rectTransform.pivot=new Vector2(0,.5f);
            view.progressFill=fill.rectTransform;

            // The publisher overlay is omitted at the user's request. The
            // engine's default startup splash remains in PlayerSettings.
            return view;
        }
        void Update()
        {
            if(Label)Label.text=string.IsNullOrEmpty(StatusMessage)?"正在准备拼图"+new string('·',Mathf.FloorToInt(Elapsed/.4f)%4):StatusMessage;
            if(progressFill)progressFill.sizeDelta=new Vector2(Mathf.Lerp(28f,504f,SourceMotion.OutCubic(Mathf.Clamp01(Elapsed/MinimumSeconds))),18f);
        }
        public IEnumerator FadeOut()
        {
            float started=Time.unscaledTime;
            while(Time.unscaledTime-started<FadeSeconds){Group.alpha=1-SourceMotion.DefaultTiming((Time.unscaledTime-started)/FadeSeconds);yield return null;}
            Group.alpha=0;
        }
    }
}

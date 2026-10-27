using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Inkomino
{
    public partial class InkominoApp:MonoBehaviour
    {
        public static InkominoApp Instance;public GameData Data;public ShapeLibrary Shapes;public SaveStore Store;public BoardState Board;public PuzzleBoardView BoardView;
        public RectTransform Stage,Content,Overlays;public float Remaining;public bool Playing,Review,Finished;public int CurrentLevel;
        readonly Stack<RectTransform> popups=new Stack<RectTransform>();Text coins,hearts,lifeTimer,timer,message;Image timeFill;Transform targets;float frozen,lastTick=-1;bool suppress;int reward,continuations;string screen="home";public bool IsQA;
        public bool IsModalOpen{get{return popups.Count>0;}}
        public bool IsHomeScreen{get{return screen=="home";}}
        public bool InputEnabled{get{return BootReady&&!TransitioningLevel&&Playing&&!Finished&&!AnimationBusy&&!RewardedAds.IsShowing&&!AdPlacements.InterstitialShowing&&popups.Count==0;}}
        void Awake(){Instance=this;Application.targetFrameRate=60;Application.runInBackground=true;Screen.sleepTimeout=SleepTimeout.NeverSleep;
#if UNITY_EDITOR || UNITY_STANDALONE
            var args=Environment.GetCommandLineArgs();bool chineseQA=args.Contains("-inkominoChineseQA"),audioQA=args.Contains("-inkominoAudioQA"),tutorialQA=args.Contains("-inkominoTutorialQA"),safeAreaQA=args.Contains("-inkominoSafeAreaQA"),socialQA=args.Contains("-inkominoSocialQA"),menuQA=args.Contains("-inkominoMenuQA"),motionQA=args.Contains("-inkominoMenuMotionQA"),gameClubQA=args.Contains("-inkominoGameClubQA");IsQA=args.Contains("-inkominoQA")||args.Contains("-inkominoFidelityQA")||args.Contains("-inkominoGridQA")||chineseQA||audioQA||tutorialQA||safeAreaQA||socialQA||menuQA||motionQA||gameClubQA;
            BuildCanvas();
            if(!IsQA||chineseQA||tutorialQA){StartCoroutine(SourceStartup());if(chineseQA)gameObject.AddComponent<ChineseQA>();if(tutorialQA)gameObject.AddComponent<TutorialQA>();return;}
            InitializeGameData();BootReady=true;ShowHome();StartCoroutine(Clock());if(gameClubQA)gameObject.AddComponent<GameClubQA>();else if(motionQA)gameObject.AddComponent<MenuMotionQA>();else if(menuQA)gameObject.AddComponent<MenuDesignQA>();else if(socialQA)gameObject.AddComponent<SocialQA>();else if(safeAreaQA)gameObject.AddComponent<SafeLayoutQA>();else if(audioQA)gameObject.AddComponent<AudioQA>();else if(args.Contains("-inkominoGridQA"))gameObject.AddComponent<GridQA>();else if(args.Contains("-inkominoFidelityQA"))gameObject.AddComponent<FidelityQA>();else gameObject.AddComponent<RuntimeQA>();
#else
            IsQA=false;BuildCanvas();StartCoroutine(SourceStartup());
#endif
        }
        public static string QAOutput(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-qaOutput");return i>=0&&i+1<args.Length?args[i+1]:Path.Combine(Application.persistentDataPath,"QA");}
        void BuildCanvas(){var go=new GameObject("Inkomino Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;var scale=go.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1080,2400);scale.matchWidthOrHeight=1;
            var backdrop=Skin.Fill("Letterbox",go.transform);backdrop.gameObject.AddComponent<Image>().color=new Color32(239,225,255,255);
            var safe=Skin.Fill("Safe area",go.transform);safe.gameObject.AddComponent<SafeArea>();Stage=Skin.Rect("Portrait Stage",safe,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1080,2400));Stage.gameObject.AddComponent<PortraitFit>();
            Content=Skin.Fill("Screens",Stage);Overlays=Skin.Fill("Screen stack",Stage);var es=new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));DontDestroyOnLoad(es);}
        
        
        
        
        
        
        
        void PlayCurrent(){Store.Recover();if(Store.Data.hearts==0){LifePopup();return;}if(Store.Data.resumeLevel==Store.Data.level&&Store.Data.resumePieces!=null)CoreContinuePrompt();else EnterLevelWithLoading(Store.Data.level);}
        void BoardChanged(){if(suppress)return;RefreshTargets();SaveBoard();if(Board.Solved&&!Finished)Win();}
        public void SaveBoard(){if(Board==null||Review||Finished||!Playing)return;
            // A demo placement is not player progress. An interrupted demo must
            // restart rather than restoring a half-demo that bypasses the intro.
            if(CurrentLevel==1&&!Store.Data.drawIntroSeen)return;
            Store.Data.resumeLevel=CurrentLevel;Store.Data.resumeTime=Remaining;Store.Data.resumePieces=Board.Pieces.ToArray();Store.Save();}
        
        
        
        void SetMessage(string value){if(message)message.text=value;}
        public void Win(){if(Finished)return;Finished=true;reward=Review?0:Store.Complete(Board.Level,Data);if(!Review)FriendsRanking.SubmitProgress(Store.Data.wins);SoundBank.Play("win");BoardView.PlayVictory();StartCoroutine(WinPopup());}
        
        public void Fail(){CoreFail();}
        void Retry(){if(!Review&&Store.Data.hearts==0){LifePopup();return;}EnterLevelWithLoading(CurrentLevel,Review);}
        public static string FormatTime(float seconds){int t=Mathf.CeilToInt(Mathf.Max(0,seconds));return (t/60)+":"+(t%60).ToString("00");}
        void TimerChanged(){if(timer)timer.text=FormatTime(Remaining);if(timer)timer.color=frozen>0?new Color32(28,130,122,255):Skin.Ink;if(timeFill)timeFill.rectTransform.sizeDelta=new Vector2(timeTrackWidth*Mathf.Clamp01(Remaining/Board.Level.time),5*D);}
        void Update(){if(!BootReady||TransitioningLevel)return;if(IsQA&&Input.GetKeyDown(KeyCode.F2))LevelLibrary((CurrentLevel>0?CurrentLevel-1:Store.Data.level-1)/25);if(Input.GetKeyDown(KeyCode.Escape)){if(SourceFailureExit!=null&&Playing&&Finished)SourceFailureExit();else if(popups.Count>0)ClosePopup();else if(Playing&&!Finished)Settings(true);else ShowHome();}if(InputEnabled){if(frozen>0)frozen=Mathf.Max(0,frozen-Time.unscaledDeltaTime);else Remaining=Mathf.Max(0,Remaining-Time.unscaledDeltaTime);int tick=Mathf.CeilToInt(Remaining);if(tick!=lastTick){lastTick=tick;TimerChanged();}if(Remaining<=0)Fail();}}
        IEnumerator Clock(){while(true){yield return new WaitForSecondsRealtime(1);Store.Recover();UpdateWallet();}}
        void OnApplicationPause(bool pause){SoundBank.SetSuspended(pause);if(pause){SaveBoard();if(CloudSave)CloudSave.Flush();if(InputEnabled)Settings(true);}else if(Store!=null){Store.Recover();UpdateWallet();if(CloudSave)CloudSave.Foreground();}}
        void OnApplicationQuit(){SaveBoard();Store?.Save();}
        
        public void ClosePopup(){if(popups.Count==0)return;var p=popups.Pop();p.gameObject.SetActive(false);Destroy(p.gameObject);if(homeGameClubEntry)homeGameClubEntry.RefreshVisibility();FocusFirst();if(Playing&&Finished&&popups.Count==0)ShowHome();}
        public void ClearPopups(){while(popups.Count>0){var p=popups.Pop();p.gameObject.SetActive(false);Destroy(p.gameObject);}if(homeGameClubEntry)homeGameClubEntry.RefreshVisibility();}
        void FocusFirst(){StartCoroutine(FocusLater());}IEnumerator FocusLater(){yield return null;var root=popups.Count>0?popups.Peek():Content;var first=root.GetComponentsInChildren<Button>().FirstOrDefault(x=>x.interactable);if(first&&EventSystem.current)EventSystem.current.SetSelectedGameObject(first.gameObject);}
        
        void LeavePrompt(bool retry){CoreLeavePrompt(retry);}
        void Info(string title,string text){var panel=Popup(title,900,850);MenuUI.Label(text,panel,new Vector2(.5f,.5f),new Vector2(0,30),new Vector2(740,350),48,MenuUI.Ink,TextAnchor.MiddleCenter);Skin.Button(Zh.Ok,panel,new Vector2(.5f,0),new Vector2(0,155),new Vector2(600,170),ClosePopup,"green",65);}
        
        public void LevelLibrary(int page){if(!IsQA)return;var panel=Popup(Zh.LevelLibrary,1000,1700);panel.gameObject.AddComponent<LevelDirectoryView>().Init(this,page);}
        void LifePopup(){CoreLivesPopup();}
        void DailyReward(){var panel=Popup(Zh.DailyGift,900,1050);Skin.Art("shell_src_ui_chestgoldopen",panel,new Vector2(.5f,.5f),new Vector2(0,200),new Vector2(340,300));bool ready=Store.DailyGiftSeconds(SaveStore.Now)==0;Skin.Text(ready?Zh.DailyContents:Zh.Tomorrow,panel,new Vector2(.5f,.5f),new Vector2(0,-40),new Vector2(780,130),47);Skin.Button(ready?Zh.Claim:Zh.Ok,panel,new Vector2(.5f,0),new Vector2(0,170),new Vector2(640,190),()=>{if(ready&&Store.ClaimDailyGift(SaveStore.Now))SoundBank.Play("coin");ClosePopup();},"green",70);}
        Image profileAvatar;
        void Profile(){var panel=Popup(Zh.Profile,900,1100);profileAvatar=Skin.Art("shell_src_ui_avatars_avatar_"+Store.Data.avatar,panel,new Vector2(.5f,.5f),new Vector2(0,200),Vector2.one*260);Skin.Text(Zh.ProfileBody(Store.Data.level,Store.Data.wins),panel,new Vector2(.5f,.5f),new Vector2(0,-60),new Vector2(750,290),49);Skin.Button(Zh.ChangeAvatar,panel,new Vector2(.5f,0),new Vector2(0,160),new Vector2(680,170),()=>{ClosePopup();AvatarPicker();},"blue",50);}
        void AvatarPicker()
        {
            var panel=Popup(Zh.ChangeAvatar,900,900);
            var borders=new RoundedGraphic[MenuUI.AvatarCount];
            var faces=new RoundedGraphic[MenuUI.AvatarCount];
            void Refresh(){for(int j=0;j<borders.Length;j++){bool selected=MenuUI.AvatarId(Store.Data.avatar)==j+1;borders[j].color=selected?MenuUI.Ink:InkTheme.Line;faces[j].color=selected?new Color32(211,248,231,255):new Color32(249,241,255,255);}}
            for(int i=0;i<MenuUI.AvatarCount;i++)
            {
                int id=i+1;
                var tile=Skin.Rect("Avatar choice "+id,panel,Vector2.one*.5f,new Vector2(-300+(i%4)*200,150-(i/4)*210),Vector2.one*182);
                borders[i]=Skin.RoundBox(tile,Vector2.one*.5f,Vector2.zero,tile.sizeDelta,InkTheme.Line,32);
                faces[i]=Skin.RoundBox(tile,Vector2.one*.5f,Vector2.zero,tile.sizeDelta-Vector2.one*10,Color.white,28);
                Skin.Art("shell_src_ui_avatars_avatar_"+id,tile,Vector2.one*.5f,Vector2.zero,Vector2.one*168);
                Skin.Hit(tile,()=>{Store.Data.avatar=id;Store.Save();UpdateWallet();Refresh();},.85f);
            }
            Refresh();Skin.Button(Zh.Ok,panel,new Vector2(.5f,0),new Vector2(0,125),new Vector2(600,160),ClosePopup,"green",60);
        }
        void BoosterShop(int index){string[] ids={"hint","freeze","lens"};var placement=index==0?RewardPlacement.Pen:index==1?RewardPlacement.Freeze:RewardPlacement.Lens;bool showAd=RewardEntryEnabled(placement);var panel=Popup(Zh.MoreBoosters,900,showAd?1160:920);Skin.Art("game_assets_"+(index==0?"pen":ids[index]),panel,new Vector2(.5f,.5f),new Vector2(0,showAd?190:150),Vector2.one*230);ShopPurchaseControls(panel,ids[index],Vector2.one*.5f,new Vector2(0,showAd?-110:-180),quantity=>{ClosePopup();BoosterCounts();});if(showAd)RewardButton(placement,RewardLabel(placement),panel,new Vector2(.5f,0),new Vector2(0,150),new Vector2(720,180),()=>RewardBooster(index,panel));}

    }
}

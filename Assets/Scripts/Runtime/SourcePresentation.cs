using System;
using System.IO;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    public partial class InkominoApp
    {
        const float D=2.625f;int presentationEpoch;float timeTrackWidth=280;RectTransform effects;Button homePlayButton;Text[] sourceEventLabels=new Text[4];
        Image walletAvatar;public bool AnimationBusy;public bool SourceTutorialEnabled=true;WeChatHeaderLayout headerLayout;GameClubEntry homeGameClubEntry;
        public static RectTransform At(Transform parent,string name,float x,float y,float w,float h){return Skin.Rect(name,parent,new Vector2(0,1),new Vector2(x+w/2,-y-h/2),new Vector2(w,h));}
        void ClearScreen(string bg,string pattern=null){presentationEpoch++;AnimationBusy=false;SourceFailureExit=null;ClearPopups();if(effects){effects.gameObject.SetActive(false);Destroy(effects.gameObject);effects=null;}foreach(Transform child in Content){child.gameObject.SetActive(false);Destroy(child.gameObject);}BoardView=null;homePlayButton=null;coins=hearts=lifeTimer=timer=message=null;targets=null;
            headerLayout=null;walletAvatar=null;homeGameClubEntry=null;
            Array.Clear(sourceEventLabels,0,sourceEventLabels.Length);var mask=Skin.Fill("Source background viewport",Content);var rectMask=mask.gameObject.AddComponent<RectMask2D>();var art=Skin.Art(bg,mask,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1080,2400),false);var sprite=art.sprite;if(sprite){float k=Mathf.Max(1080/sprite.rect.width,2400/sprite.rect.height);art.rectTransform.sizeDelta=sprite.rect.size*k;}var dots=Skin.Fill("Sticker journal dots",mask).gameObject.AddComponent<StickerBackdrop>();dots.raycastTarget=false;StickerDesign.Backdrop(mask,pattern??screen);}
        RectTransform Effects{get{if(!effects)effects=Skin.Fill("Source effects",Stage);effects.SetAsLastSibling();Overlays.SetAsLastSibling();return effects;}}
        public void ShowHome()
        {
            Playing=false;Review=false;screen="home";ClearScreen("game_assets_homebg");Wallet();int first=Content.childCount;
            var hero=Skin.Rect("Home sticker showcase",Content,new Vector2(.5f,1),new Vector2(0,-1076),new Vector2(900,820));
            MenuUI.Paper(hero,Vector2.one*.5f,Vector2.zero,hero.sizeDelta,true);
            var painting=MenuUI.Art("ink_home_hero",hero,Vector2.one*.5f,new Vector2(0,8),new Vector2(820,740));
            if(painting)MenuMotion.Float(painting.rectTransform,4,.8f);
            HomeGameClub();

            MenuUI.Label("随手划两下，快乐贴满格",Content,new Vector2(.5f,1),new Vector2(0,-1548),new Vector2(900,80),52,MenuUI.Muted,TextAnchor.MiddleCenter,true);
            homePlayButton=MenuUI.GameButton(Zh.Play,Content,new Vector2(.5f,1),new Vector2(0,-1740),new Vector2(980,252),PlayCurrent,84);
            var playLabel=homePlayButton.GetComponentInChildren<Text>();playLabel.rectTransform.anchoredPosition=new Vector2(118,8);playLabel.rectTransform.sizeDelta=new Vector2(560,200);
            Skin.RoundBox(playLabel.transform.parent,Vector2.one*.5f,new Vector2(-318,8),new Vector2(208,92),MenuUI.Lemon,46);
            var level=MenuUI.Label("Lv "+Store.Data.level,playLabel.transform.parent,Vector2.one*.5f,new Vector2(-318,8),new Vector2(230,110),62,MenuUI.Ink,TextAnchor.MiddleCenter,true);level.name="Home current level";
            HomeEvent(0,"每日礼物","shell_src_ui_dailyrewardcardicon",-248,DailyReward);
            HomeEvent(3,"快乐集章","entry_album",248,ShowClimb);
            UpdateHomeTimers();MenuUI.Adapt(Content,headerLayout,first,2100);Navigation(1);
        }
        void HomeGameClub()
        {
            var card=MenuUI.Paper(Content,new Vector2(.5f,1),new Vector2(350,-575),new Vector2(330,150));card.name="Home game club entry";
            MenuUI.Art("entry_friends",card,Vector2.one*.5f,new Vector2(-108,0),Vector2.one*100);
            MenuUI.Label("游戏圈",card,Vector2.one*.5f,new Vector2(46,0),new Vector2(205,110),52,MenuUI.Ink,TextAnchor.MiddleCenter,true);
            var entry=card.gameObject.AddComponent<GameClubEntry>();homeGameClubEntry=entry;
            Skin.Hit(card,entry.Open,.8f);
            entry.Initialize(this,value=>{if(this&&IsHomeScreen&&!IsModalOpen)Info("游戏圈",value);});
        }
        void HomeEvent(int index,string title,string icon,float x,Action action)
        {
            var card=MenuUI.Paper(Content,new Vector2(.5f,1),new Vector2(x,-1990),new Vector2(472,216));card.name="Source event "+index;
            MenuUI.Plate(card,new Vector2(0,.5f),new Vector2(92,0),Vector2.one*148,index==3?MenuUI.LilacWash:MenuUI.MintWash,42);
            if(index==3)MenuUI.Art("entry_album",card,new Vector2(0,.5f),new Vector2(92,0),Vector2.one*128);else Skin.Art(icon,card,new Vector2(0,.5f),new Vector2(92,0),Vector2.one*128);
            MenuUI.Label(title,card,new Vector2(0,.5f),new Vector2(314,40),new Vector2(270,64),50,MenuUI.Ink,TextAnchor.MiddleLeft,true);
            sourceEventLabels[index]=MenuUI.Label("",card,new Vector2(0,.5f),new Vector2(314,-40),new Vector2(270,64),42,MenuUI.Muted);
            sourceEventLabels[index].font=MenuUI.BodyFont;sourceEventLabels[index].alignByGeometry=false;
            Skin.Hit(card,action,.8f);
        }
        void Wallet(){
            int first=Content.childCount;
            var strip=At(Content,"Wallet strip",188,164,708,108);Skin.RoundBox(strip,Vector2.one*.5f,new Vector2(0,-6),strip.sizeDelta,new Color32(145,110,196,40),54);Skin.RoundBox(strip,Vector2.one*.5f,Vector2.zero,strip.sizeDelta,Color.white,54);Skin.RoundBox(strip,Vector2.one*.5f,Vector2.zero,strip.sizeDelta-new Vector2(10,10),MenuUI.Cream,48);
            var avatar=At(Content,"Profile",26.25f,150,49*D,49*D);Skin.RoundBox(avatar,new Vector2(.5f,.5f),Vector2.zero,avatar.sizeDelta,new Color32(102,69,170,255),11*D);
            var clip=Skin.Rect("Avatar clip",avatar,new Vector2(.5f,.5f),Vector2.zero,Vector2.one*44*D);var round=clip.gameObject.AddComponent<RoundedGraphic>();round.color=Color.white;round.Radius=9*D;round.raycastTarget=false;var mask=clip.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;walletAvatar=Skin.Art("shell_src_ui_avatars_avatar_"+Store.Data.avatar,clip,new Vector2(.5f,.5f),Vector2.zero,clip.sizeDelta,false);Skin.Hit(avatar,Profile,.7f);
            var hp=At(Content,"Lives pill",204,169,382,36*D);Pill(hp,hp.sizeDelta,2);
            lifeTimer=MenuUI.Label("",hp,Vector2.one*.5f,new Vector2(48,0),new Vector2(205,78),46,MenuUI.Ink,TextAnchor.MiddleCenter);Skin.Hit(hp,LifePopup,.8f);
            var heart=Skin.Art("shell_src_ui_heart",Content,new Vector2(0,1),new Vector2(242,-216),Vector2.one*128);heart.name="Wallet heart";
            hearts=MenuUI.Label("",heart.transform,Vector2.one*.5f,new Vector2(0,-4),new Vector2(62,75),48,Color.white,TextAnchor.MiddleCenter);hearts.name="Wallet life count";
            var heartOutline=hearts.gameObject.AddComponent<Outline>();heartOutline.effectColor=MenuUI.Ink;heartOutline.effectDistance=new Vector2(1,-1);
            Plus(new Vector2(573,-216),LifePopup);
            var cp=At(Content,"Coin pill",638,169,258,36*D);Pill(cp,cp.sizeDelta,2);
            coins=MenuUI.Label("",cp,Vector2.one*.5f,new Vector2(14,0),new Vector2(166,75),50,MenuUI.Ink,TextAnchor.MiddleCenter);Skin.Hit(cp,ShowShop,.8f);
            Skin.Art("shell_src_ui_coin",Content,new Vector2(0,1),new Vector2(670,-216),Vector2.one*108);Plus(new Vector2(891,-216),ShowShop);
            var settings=Skin.IconButton("game_assets_btnsettings",Content,new Vector2(0,1),new Vector2(993.5f,-216.5f),Vector2.one*46*D,()=>Settings(false));
            Skin.ColorBox(Content,new Vector2(0,1),new Vector2(540,-334.5f),new Vector2(1080,D),new Color32(204,181,232,150));headerLayout=settings.gameObject.AddComponent<WeChatHeaderLayout>();headerLayout.TrackChildren(Content,first);UpdateWallet();}
        void Plus(Vector2 pos,Action onPress){var r=Skin.Rect("Add",Content,new Vector2(0,1),pos,Vector2.one*68);Skin.Art("shell_src_ui_plus",r,Vector2.one*.5f,Vector2.zero,r.sizeDelta);MenuMotion.Press(Skin.Hit(r,onPress,.8f));}
        void Pill(Transform parent,Vector2 size,float strokeDp=1){Skin.RoundBox(parent,Vector2.one*.5f,new Vector2(0,-3),size,new Color32(145,110,196,36),size.y/2);var image=MenuUI.Art("ink_paper",parent,Vector2.one*.5f,Vector2.zero,size);image.preserveAspect=false;image.type=Image.Type.Sliced;}
        void UpdateWallet(){if(walletAvatar)walletAvatar.sprite=MenuUI.AvatarSprite(Store.Data.avatar);if(profileAvatar)profileAvatar.sprite=MenuUI.AvatarSprite(Store.Data.avatar);if(coins)coins.text=Store.Data.coins.ToString();if(hearts)hearts.text=Store.Data.hearts.ToString();if(lifeTimer){long sec=Math.Max(0,Store.Data.regenAt-SaveStore.Now);lifeTimer.text=Store.Data.hearts>=5?Zh.LivesFull:RewardCountdown(sec);}UpdateHomeTimers();}
        void SourceEvent(string art,int index,float x,float y,string text,Action action,bool locked=false){var r=At(Content,"Source event "+index,x,y,86*D,84*D);Skin.Art("game_assets_event_frame",r,new Vector2(.5f,.5f),Vector2.zero,r.sizeDelta,false);float icon=index==2?Mathf.Round(86*.8301f*.8f)*D:Mathf.Round(Mathf.Round(86*.8301f))*D;var img=Skin.Art(art,r,new Vector2(.5f,.5f),new Vector2(0,4*D),Vector2.one*icon);if(locked)img.color=new Color(1,1,1,.35f);
            if(locked){Skin.RoundBox(r,new Vector2(.5f,.5f),new Vector2(0,4*D),Vector2.one*71*D,new Color(7/255f,11/255f,20/255f,.55f),35.5f*D);Skin.Art("shell_src_ui_lock",r,new Vector2(1,0),new Vector2(-14*D,26*D),Vector2.one*24*D);}
            var ribbon=Skin.Rect("Source event ribbon",r,new Vector2(.5f,0),new Vector2(0,11.5f*D),new Vector2(84*D,30*D));Skin.HSlice(ribbon,"game_assets_event_ribboncap","game_assets_event_ribbonmid",ribbon.sizeDelta);sourceEventLabels[index]=Skin.Text(text,ribbon,new Vector2(.5f,.5f),new Vector2(0,-4*D),new Vector2(79*D,24*D),Mathf.RoundToInt(14*D),Color.white);Skin.Hit(r,action,.7f);if(index==0&&headerLayout)headerLayout.Track(r);}
        void EventBadge(string art,string text,Vector2 pos,Action action){SourceEvent(art,0,pos.x,pos.y,text,action);}
        void Navigation(int active)
        {
            var nav=At(Content,"Source navigation",0,2130,1080,270);
            var dock=MenuUI.Art("sticker_dock",nav,Vector2.one*.5f,Vector2.zero,new Vector2(1050,260));dock.preserveAspect=false;dock.type=Image.Type.Sliced;dock.pixelsPerUnitMultiplier=dock.sprite.rect.height/260f;
            string[] ids={"shop","home","rank"};Action[] actions={ShowShop,ShowHome,ShowCollection};
            for(int i=0;i<3;i++)
            {
                var tab=At(nav,"Tab "+ids[i],i*360+16,13,328,232);bool selected=i==active;
                if(selected){Skin.RoundBox(tab,new Vector2(.5f,.5f),new Vector2(0,-2),new Vector2(304,222),new Color32(214,72,118,80),56);Skin.RoundBox(tab,new Vector2(.5f,.5f),new Vector2(0,6),new Vector2(300,218),MenuUI.CoralChip,54);}
                var icon=Skin.Art("game_assets_nav_tab"+ids[i],tab,new Vector2(.5f,1),new Vector2(0,-78),Vector2.one*(selected?142:108));icon.color=new Color(1,1,1,selected?1:.58f);
                string label=i==0?"商店":i==1?Zh.Home:Zh.Rank;
                MenuUI.Label(label,tab,new Vector2(.5f,1),new Vector2(0,-184),new Vector2(285,68),selected?52:48,selected?MenuUI.Grape:MenuUI.Muted,TextAnchor.MiddleCenter,true);
                MenuMotion.Press(Skin.Hit(tab,actions[i],.8f));
            }
        }
        public void StartLevel(int id,bool review=false,bool resume=false){CurrentLevel=Mathf.Clamp(id,1,500);Review=review;Playing=true;Finished=false;screen="game";ClearScreen("game_assets_bg");var level=Data.levels[CurrentLevel-1];Board=new BoardState(level,Shapes);Remaining=level.time;frozen=0;continuations=0;rewardedContinues=0;lastTick=-1;
            int firstHud=Content.childCount;
            var clock=At(Content,"Source timer",396,139,288,124);MenuUI.Paper(clock,new Vector2(.5f,.5f),Vector2.zero,clock.sizeDelta);Skin.RoundBox(clock,new Vector2(0,.5f),new Vector2(16,0),new Vector2(10,64),MenuUI.Coral,5);timer=MenuUI.Label("",clock,new Vector2(.5f,.5f),new Vector2(8,0),new Vector2(250,105),76,MenuUI.Ink,TextAnchor.MiddleCenter);timeFill=null;
            var lp=At(Content,"Source level",33,151,241,101);MenuUI.Paper(lp,new Vector2(.5f,.5f),Vector2.zero,lp.sizeDelta);Skin.RoundBox(lp,new Vector2(0,.5f),new Vector2(14,0),new Vector2(10,52),MenuUI.Grape,5);MenuUI.Label(Zh.Level(CurrentLevel),lp,new Vector2(.5f,.5f),new Vector2(6,0),new Vector2(210,90),46,MenuUI.Ink,TextAnchor.MiddleCenter,true);Skin.IconButton("game_assets_btnpause",Content,new Vector2(0,1),new Vector2(991,-207),Vector2.one*42*D,()=>Settings(true));
            var rules=MenuUI.SecondaryButton("规则",Content,new Vector2(0,1),new Vector2(819,-207),new Vector2(160,100),ShowGameHelp,42);rules.name="Game rules entry";
            targets=At(Content,"Source targets",31.5f,273,1017,225);((RectTransform)targets).pivot=new Vector2(0,1);((RectTransform)targets).anchoredPosition=new Vector2(31.5f,-273);
            headerLayout=clock.gameObject.AddComponent<WeChatHeaderLayout>();headerLayout.TrackChildren(Content,firstHud);
            var boardArea=At(Content,"Source board area",38,540,1004,1395);headerLayout.SetBoard(boardArea);BoardView=boardArea.gameObject.AddComponent<PuzzleBoardView>();BoardView.TutorialReservedDp=id==1?SourceTutorialReserveDp:0;BoardView.CanInput=()=>InputEnabled;BoardView.Message=SetMessage;BoardView.Init(Board);SourceWoodFrame.Build((RectTransform)boardArea.parent,BoardView);if(id<=10){var idle=boardArea.gameObject.AddComponent<SourceIdleHint>();idle.Init(this,BoardView);}
            message=Skin.Text("",Content,new Vector2(0,1),new Vector2(540,-1990),new Vector2(960,65),Mathf.RoundToInt(14*D),new Color32(234,72,119,255));
            BuildBoosterTray();
            bool restartIntro=id==1&&SourceTutorialEnabled&&!Store.Data.drawIntroSeen;
            if(restartIntro&&!review&&Store.Data.resumeLevel==1){Store.Data.resumeLevel=0;Store.Data.resumePieces=null;Store.Data.resumeTime=0;Store.Save();}
            Board.Changed+=BoardChanged;if(resume&&!restartIntro&&Store.Data.resumeLevel==id&&Store.Data.resumePieces!=null){suppress=true;Board.Restore(Store.Data.resumePieces);Remaining=Mathf.Max(1,Store.Data.resumeTime);suppress=false;}
            RefreshTargets();BoosterCounts();TimerChanged();if(id==1&&Board.Pieces.Count==0&&SourceTutorialEnabled&&!Store.Data.drawIntroSeen)Tutorial();else StartContextGuide();FocusFirst();}
        void RefreshTargets(){if(targets!=null&&!targets.GetComponent<SourceTargetsStrip>())SourceHud.BuildTargets((RectTransform)targets,Board,D);}
        // Three equal columns, with independent icon, name and status rows.
        // The entire tray is anchored to the safe stage bottom; every hit area is 98dp wide.
        void BuildBoosterTray()
        {
            var tray=Skin.Rect("Sticker booster tray",Content,new Vector2(.5f,0),new Vector2(0,218),new Vector2(1010,392));
            // Layout-only container. Each booster has its own die-cut card.
            string[] icons={"pen","freeze","lens"};Color[] colors={new Color32(255,228,238,255),new Color32(221,248,237,255),new Color32(239,228,255,255)};
            for(int i=0;i<3;i++)
            {
                int k=i;var slot=Skin.Rect("Source booster "+i,tray,new Vector2(.5f,.5f),new Vector2((i-1)*322,8),new Vector2(292,334));
                Skin.RoundBox(slot,Vector2.one*.5f,new Vector2(8,-14),new Vector2(286,326),new Color32(145,110,196,70),50);
                Skin.RoundBox(slot,Vector2.one*.5f,Vector2.zero,new Vector2(286,326),Color.white,50);
                Skin.RoundBox(slot,Vector2.one*.5f,new Vector2(0,4),new Vector2(268,306),colors[i],42);
                var icon=MenuUI.Art("menu_"+icons[i],slot,Vector2.one*.5f,new Vector2(0,65),Vector2.one*156);
                bool locked=CurrentLevel<(i==0?3:i==1?7:10);icon.color=new Color(1,1,1,locked?.64f:1);
                MenuUI.Label(i==0?Zh.MagicPen:i==1?Zh.FreezeTime:Zh.Lens,slot,Vector2.one*.5f,new Vector2(0,-43),new Vector2(250,62),44,MenuUI.Ink,TextAnchor.MiddleCenter,true);
                MenuMotion.Press(Skin.Hit(slot,()=>UseBooster(k),.8f));
            }
        }
        void BoosterCounts()
        {
            var old=Content.Find("Booster counts");if(old){old.gameObject.SetActive(false);Destroy(old.gameObject);}
            var layer=Skin.Fill("Booster counts",Content);int[] counts={Store.Data.hint,Store.Data.freeze,Store.Data.lens};int[] unlock={3,7,10};
            for(int i=0;i<3;i++)
            {
                var status=Skin.Rect("Booster status "+i,layer,new Vector2(.5f,0),new Vector2((i-1)*322,113),new Vector2(244,58));
                bool locked=CurrentLevel<unlock[i];
                Skin.RoundBox(status,Vector2.one*.5f,Vector2.zero,new Vector2(210,54),new Color32(255,255,255,235),27);
                if(locked){Skin.Art("shell_src_ui_lock",status,Vector2.one*.5f,new Vector2(-77,0),Vector2.one*44);MenuUI.Label(Zh.UnlockBadge(unlock[i]),status,Vector2.one*.5f,new Vector2(24,0),new Vector2(188,58),42,MenuUI.Muted,TextAnchor.MiddleCenter);}
                else {MenuUI.Label(counts[i]>0?"× "+counts[i]:"补充",status,Vector2.one*.5f,Vector2.zero,new Vector2(240,58),42,MenuUI.Ink,TextAnchor.MiddleCenter);}
            }
        }
        public RectTransform Popup(string title,float width=920,float height=1200){if(BoardView)BoardView.Cancel();var shade=Skin.Fill(title+" overlay",Overlays);shade.gameObject.AddComponent<Image>().color=new Color(46/255f,29/255f,67/255f,.55f);shade.gameObject.AddComponent<CanvasGroup>();var motion=Skin.Rect("Source popup motion",shade,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1080,2400));float w=Mathf.Min(width,1080*.99f-48*D);var panel=Skin.Rect(title+" panel",motion,new Vector2(.5f,.5f),new Vector2(0,-70.875f),new Vector2(w,height));MenuUI.Paper(panel,new Vector2(.5f,.5f),Vector2.zero,panel.sizeDelta,true);MenuUI.Label(title,panel,new Vector2(.5f,1),new Vector2(0,-88),new Vector2(w-175,118),72,MenuUI.Ink,TextAnchor.MiddleCenter,true);Skin.RoundBox(panel,new Vector2(.5f,1),new Vector2(0,-168),new Vector2(w-140,6),MenuUI.CoralChip,3);
            Skin.IconButton("shell_src_ui_close",panel,new Vector2(1,1),new Vector2(-40,-55),Vector2.one*42*D,ClosePopup);popups.Push(shade);if(homeGameClubEntry)homeGameClubEntry.RefreshVisibility();SourceMotion.Popup(motion);FocusFirst();return panel;}
        public void Settings(bool pause){bool hasCloud=!pause&&CloudSave&&CloudSave.Configured;var panel=Popup(pause?Zh.Paused:Zh.Settings,1080*.99f-48*D,pause?1288:hasCloud?1360:1166);float width=panel.sizeDelta.x;var card=At(panel,"Source settings card",18*D,88*D,width-36*D,116*D);Skin.RoundBox(card,new Vector2(.5f,.5f),new Vector2(0,-8),card.sizeDelta,new Color32(145,110,196,55),18*D);Skin.RoundBox(card,new Vector2(.5f,.5f),Vector2.zero,card.sizeDelta,Color.white,18*D);Skin.RoundBox(card,new Vector2(.5f,.5f),Vector2.zero,card.sizeDelta-Vector2.one*8,new Color32(255,236,244,255),16*D);
            string[] keys={"soundicon","musicicon","hapticsicon"};for(int i=0;i<3;i++){int k=i;bool on=i==0?Store.Data.sound:i==1?Store.Data.music:Store.Data.haptics;var button=Skin.Rect(keys[i],card,new Vector2(.5f,1),new Vector2((i-1)*80*D,-50*D),Vector2.one*64*D);var outline=Skin.RoundBox(button,new Vector2(.5f,.5f),Vector2.zero,button.sizeDelta,on?new Color32(155,120,207,255):new Color32(204,181,232,255),14*D);var face=Skin.RoundBox(button,new Vector2(.5f,.5f),Vector2.zero,button.sizeDelta-Vector2.one*4*D,on?new Color32(225,248,237,255):new Color32(239,233,245,255),12*D);var icon=Skin.Art("shell_src_ui_"+keys[i],button,new Vector2(.5f,.5f),Vector2.zero,Vector2.one*44*D);icon.color=new Color(1,1,1,on?1:.4f);var slash=Skin.RoundBox(button,new Vector2(.5f,.5f),Vector2.zero,new Vector2(40*D,3*D),new Color32(234,72,119,255),2*D);slash.rectTransform.localRotation=Quaternion.Euler(0,0,45);slash.gameObject.SetActive(!on);Skin.Hit(button,()=>{if(k==0)Store.Data.sound=!Store.Data.sound;else if(k==1)Store.Data.music=!Store.Data.music;else Store.Data.haptics=!Store.Data.haptics;SoundBank.Settings(Store.Data.sound,Store.Data.music);Store.Save();bool enabled=k==0?Store.Data.sound:k==1?Store.Data.music:Store.Data.haptics;outline.color=enabled?new Color32(155,120,207,255):new Color32(204,181,232,255);face.color=enabled?new Color32(225,248,237,255):new Color32(239,233,245,255);icon.color=new Color(1,1,1,enabled?1:.4f);slash.gameObject.SetActive(!enabled);if(k==2&&enabled)Feel.ConfirmHaptics();},.7f);}
            if(pause){MenuUI.SecondaryButton("重新开始",panel,new Vector2(.5f,1),new Vector2(-204,-672),new Vector2(376,172),()=>LeavePrompt(true),50);MenuUI.SecondaryButton("返回首页",panel,new Vector2(.5f,1),new Vector2(204,-672),new Vector2(376,172),()=>LeavePrompt(false),50);MenuUI.GameButton(Zh.Resume,panel,new Vector2(.5f,1),new Vector2(0,-368*D),new Vector2((width-36*D)*.76f,86*1.15f*D),ClosePopup,Mathf.RoundToInt(32*D));}
            else {
                Button motionButton=null;
                motionButton=MenuUI.SecondaryButton(Store.Data.reducedMenuMotion?"菜单动效：精简":"菜单动效：完整",panel,new Vector2(.5f,1),new Vector2(0,-630),new Vector2(width-120,140),()=>{
                    Store.Data.reducedMenuMotion=!Store.Data.reducedMenuMotion;Store.Save();
                    foreach(var label in motionButton.GetComponentsInChildren<Text>())label.text=Store.Data.reducedMenuMotion?"菜单动效：精简":"菜单动效：完整";
                },48);motionButton.name="Menu motion preference";MenuMotion.Press(motionButton);
                if(hasCloud)MenuUI.SecondaryButton("云存档",panel,new Vector2(.5f,1),new Vector2(0,-820),new Vector2(width-120,130),ShowCloudSaveStatus,48);
                MenuUI.GameButton(Zh.Done,panel,new Vector2(.5f,1),new Vector2(0,-(hasCloud?420:343)*D),new Vector2((width-36*D)*.7f,80*1.15f*D),ClosePopup,Mathf.RoundToInt(30*D));
            }
            Skin.Text(Zh.Version,panel,new Vector2(.5f,1),new Vector2(0,-(pause?458:hasCloud?480:410.3f)*D),new Vector2(700,21*D),Mathf.RoundToInt(12*D),new Color32(102,69,170,255));}
        IEnumerator WinPopup(){int epoch=presentationEpoch;while(BoardView&&BoardView.VictoryTimeRemaining>0)yield return null;if(epoch!=presentationEpoch||!Playing||!Finished)yield break;ShowSourceResult();}

    }
}

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
        public bool AnimationBusy;public bool SourceTutorialEnabled=true;WeChatHeaderLayout headerLayout;
        public static RectTransform At(Transform parent,string name,float x,float y,float w,float h){return Skin.Rect(name,parent,new Vector2(0,1),new Vector2(x+w/2,-y-h/2),new Vector2(w,h));}
        void ClearScreen(string bg){presentationEpoch++;AnimationBusy=false;SourceFailureExit=null;ClearPopups();if(effects){effects.gameObject.SetActive(false);Destroy(effects.gameObject);effects=null;}foreach(Transform child in Content){child.gameObject.SetActive(false);Destroy(child.gameObject);}BoardView=null;homePlayButton=null;coins=hearts=lifeTimer=timer=message=null;targets=null;
            headerLayout=null;
            Array.Clear(sourceEventLabels,0,sourceEventLabels.Length);var mask=Skin.Fill("Source background viewport",Content);var rectMask=mask.gameObject.AddComponent<RectMask2D>();var art=Skin.Art(bg,mask,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1080,2400),false);var sprite=art.sprite;if(sprite){float k=Mathf.Max(1080/sprite.rect.width,2400/sprite.rect.height);art.rectTransform.sizeDelta=sprite.rect.size*k;}}
        RectTransform Effects{get{if(!effects)effects=Skin.Fill("Source effects",Stage);effects.SetAsLastSibling();Overlays.SetAsLastSibling();return effects;}}
        public void ShowHome()
        {
            Playing=false;Review=false;screen="home";ClearScreen("game_assets_homebg");Wallet();int first=Content.childCount;
            Skin.Art("zh_loading_logo",Content,new Vector2(.5f,1),new Vector2(0,-620),new Vector2(740,290));
            var hero=MenuUI.Paper(Content,new Vector2(.5f,1),new Vector2(0,-1120),new Vector2(690,660),true);hero.name="Home puzzle showcase";
            hero.localRotation=Quaternion.Euler(0,0,-5);
            Skin.RoundBox(hero,new Vector2(.5f,.5f),Vector2.zero,new Vector2(604,574),new Color32(142,99,53,255),18);
            Skin.RoundBox(hero,new Vector2(.5f,.5f),new Vector2(0,4),new Vector2(582,548),new Color32(243,228,190,255),12);
            // Four interlocking tetrominoes communicate the puzzle at a glance.
            int[,] pieces={{0,0,1,1},{0,0,2,1},{3,3,2,1},{3,3,2,2}};
            Color32[] colors={new Color32(71,181,138,255),new Color32(173,117,213,255),new Color32(238,172,65,255),new Color32(74,167,211,255)};
            for(int group=0;group<4;group++)for(int layer=0;layer<2;layer++)
            {
                var baseColor=(Color)colors[group];Color c=layer==0?baseColor*.62f:baseColor;c.a=1;
                for(int y=0;y<4;y++)for(int x=0;x<4;x++)if(pieces[y,x]==group)
                {
                    var pos=new Vector2((x-1.5f)*134,(1.5f-y)*126+(layer==0?-6:0));
                    Skin.RoundBox(hero,new Vector2(.5f,.5f),pos,new Vector2(128,120),c,12);
                    if(x<3&&pieces[y,x+1]==group)Skin.ColorBox(hero,new Vector2(.5f,.5f),pos+new Vector2(67,0),new Vector2(38,120),c);
                    if(y<3&&pieces[y+1,x]==group)Skin.ColorBox(hero,new Vector2(.5f,.5f),pos-new Vector2(0,63),new Vector2(128,38),c);
                    if(x<3&&y<3&&pieces[y,x+1]==group&&pieces[y+1,x]==group&&pieces[y+1,x+1]==group)Skin.ColorBox(hero,new Vector2(.5f,.5f),pos+new Vector2(67,-63),new Vector2(38,38),c);
                    if(layer==1&&(y==0||pieces[y-1,x]!=group))Skin.RoundBox(hero,new Vector2(.5f,.5f),pos+new Vector2(0,48),new Vector2(98,5),new Color(1,1,1,.36f),2.5f);
                }
            }
            var pen=MenuUI.Art("menu_pen",Content,new Vector2(.5f,1),new Vector2(330,-1240),new Vector2(290,390));
            if(pen)MenuMotion.Float(pen.rectTransform,5,1.1f);
            MenuUI.Label("划出形状，刚好拼满",Content,new Vector2(.5f,1),new Vector2(0,-1500),new Vector2(900,72),48,MenuUI.Muted,TextAnchor.MiddleCenter);
            var level=MenuUI.SecondaryButton(Zh.Level(Store.Data.level)+"  ·  查看关卡",Content,new Vector2(.5f,1),new Vector2(0,-1605),new Vector2(620,112),()=>LevelLibrary((Store.Data.level-1)/25),46);level.name="Home level directory";
            homePlayButton=MenuUI.GameButton(Zh.Play,Content,new Vector2(.5f,1),new Vector2(0,-1790),new Vector2(820,220),PlayCurrent,86);
            HomeEvent(0,"每日礼物","shell_src_ui_dailyrewardcardicon",-247,DailyReward);
            HomeEvent(3,"登顶寻宝","menu_treasure",247,ShowClimb);
            UpdateHomeTimers();MenuUI.Adapt(Content,headerLayout,first,2100);Navigation(1);
        }
        void HomeEvent(int index,string title,string icon,float x,Action action)
        {
            var card=MenuUI.Paper(Content,new Vector2(.5f,1),new Vector2(x,-2000),new Vector2(466,190));card.name="Source event "+index;
            if(index==3)MenuUI.Art("menu_treasure",card,new Vector2(0,.5f),new Vector2(82,0),Vector2.one*140);else Skin.Art(icon,card,new Vector2(0,.5f),new Vector2(82,0),Vector2.one*140);
            MenuUI.Label(title,card,new Vector2(0,.5f),new Vector2(300,36),new Vector2(270,65),48,MenuUI.Ink,TextAnchor.MiddleLeft,true);
            sourceEventLabels[index]=MenuUI.Label("",card,new Vector2(0,.5f),new Vector2(300,-38),new Vector2(270,65),42,MenuUI.Muted);
            Skin.Hit(card,action,.8f);
        }
        void Wallet(){
            int first=Content.childCount;
            var avatar=At(Content,"Profile",26.25f,150,49*D,49*D);Skin.RoundBox(avatar,new Vector2(.5f,.5f),Vector2.zero,avatar.sizeDelta,new Color32(42,51,80,255),11*D);
            var clip=Skin.Rect("Avatar clip",avatar,new Vector2(.5f,.5f),Vector2.zero,Vector2.one*44*D);var round=clip.gameObject.AddComponent<RoundedGraphic>();round.color=Color.white;round.Radius=9*D;round.raycastTarget=false;var mask=clip.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;Skin.Art("shell_src_ui_avatars_avatar_"+Store.Data.avatar,clip,new Vector2(.5f,.5f),Vector2.zero,clip.sizeDelta,false);Skin.Hit(avatar,Profile,.7f);
            var hp=At(Content,"Lives pill",242.1f,169,343.8f,36*D);Pill(hp,hp.sizeDelta,2);lifeTimer=Skin.Text("",hp,new Vector2(.5f,.5f),new Vector2(21,2),new Vector2(286,75),Mathf.RoundToInt(20*D));Skin.Hit(hp,LifePopup,.8f);
            var heart=Skin.Art("shell_src_ui_heart",Content,new Vector2(0,1),new Vector2(269,-216.25f),new Vector2(38*D,38*D));hearts=Skin.Text("",heart.transform,new Vector2(.5f,.5f),new Vector2(0,4),Vector2.one*70,Mathf.RoundToInt(14*D),Color.white);Plus(new Vector2(298.5f,-242.5f),LifePopup);
            var cp=At(Content,"Coin pill",643,169,243,36*D);Pill(cp,cp.sizeDelta,2);coins=Skin.Text("",cp,new Vector2(.5f,.5f),new Vector2(21,2),new Vector2(180,75),Mathf.RoundToInt(20*D));Skin.Hit(cp,ShowShop,.8f);Skin.Art("shell_src_ui_coin",Content,new Vector2(0,1),new Vector2(679,-216.25f),new Vector2(38*D,38*D));Plus(new Vector2(709.5f,-242.5f),ShowShop);
            var settings=Skin.IconButton("game_assets_btnsettings",Content,new Vector2(0,1),new Vector2(993.5f,-216.5f),Vector2.one*46*D,()=>Settings(false));
            Skin.ColorBox(Content,new Vector2(0,1),new Vector2(540,-334.5f),new Vector2(1080,D),new Color32(183,144,83,150));headerLayout=settings.gameObject.AddComponent<WeChatHeaderLayout>();headerLayout.TrackChildren(Content,first);UpdateWallet();}
        void Plus(Vector2 pos,Action onPress){var r=Skin.Rect("Add",Content,new Vector2(0,1),pos,Vector2.one*19*D);Skin.Art("shell_src_ui_greenbase",r,new Vector2(.5f,.5f),Vector2.zero,r.sizeDelta);Skin.Art("shell_src_ui_plus",r,new Vector2(.5f,.5f),new Vector2(0,1),Vector2.one*11*D);Skin.Hit(r,onPress,.8f);}
        void Pill(Transform parent,Vector2 size,float strokeDp=1){float radius=size.y<=100?size.y/2:10*D;Skin.RoundBox(parent,new Vector2(.5f,.5f),Vector2.zero,size,new Color32(90,78,64,255),radius);Skin.RoundBox(parent,new Vector2(.5f,.5f),Vector2.zero,size-Vector2.one*(strokeDp*2*D),Skin.Paper,Mathf.Max(0,radius-strokeDp*D));}
        void UpdateWallet(){if(coins)coins.text=Store.Data.coins.ToString();if(hearts)hearts.text=Store.Data.hearts.ToString();if(lifeTimer){long sec=Math.Max(0,Store.Data.regenAt-SaveStore.Now);lifeTimer.text=Store.Data.hearts>=5?Zh.LivesFull:TimeSpan.FromSeconds(sec).ToString(@"hh\:mm\:ss");}UpdateHomeTimers();}
        void SourceEvent(string art,int index,float x,float y,string text,Action action,bool locked=false){var r=At(Content,"Source event "+index,x,y,86*D,84*D);Skin.Art("game_assets_event_frame",r,new Vector2(.5f,.5f),Vector2.zero,r.sizeDelta,false);float icon=index==2?Mathf.Round(86*.8301f*.8f)*D:Mathf.Round(Mathf.Round(86*.8301f))*D;var img=Skin.Art(art,r,new Vector2(.5f,.5f),new Vector2(0,4*D),Vector2.one*icon);if(locked)img.color=new Color(1,1,1,.35f);
            if(locked){Skin.RoundBox(r,new Vector2(.5f,.5f),new Vector2(0,4*D),Vector2.one*71*D,new Color(7/255f,11/255f,20/255f,.55f),35.5f*D);Skin.Art("shell_src_ui_lock",r,new Vector2(1,0),new Vector2(-14*D,26*D),Vector2.one*24*D);}
            var ribbon=Skin.Rect("Source event ribbon",r,new Vector2(.5f,0),new Vector2(0,11.5f*D),new Vector2(84*D,30*D));Skin.HSlice(ribbon,"game_assets_event_ribboncap","game_assets_event_ribbonmid",ribbon.sizeDelta);sourceEventLabels[index]=Skin.Text(text,ribbon,new Vector2(.5f,.5f),new Vector2(0,-4*D),new Vector2(79*D,24*D),Mathf.RoundToInt(14*D),Color.white);Skin.Hit(r,action,.7f);if(index==0&&headerLayout)headerLayout.Track(r);}
        void EventBadge(string art,string text,Vector2 pos,Action action){SourceEvent(art,0,pos.x,pos.y,text,action);}
        void Navigation(int active)
        {
            var nav=At(Content,"Source navigation",0,2130,1080,270);
            Skin.RoundBox(nav,new Vector2(.5f,.5f),Vector2.zero,nav.sizeDelta,new Color32(169,120,52,255),40);
            Skin.RoundBox(nav,new Vector2(.5f,.5f),new Vector2(0,-5),nav.sizeDelta-new Vector2(12,16),new Color32(255,241,204,255),35);
            string[] ids={"shop","home","rank"};Action[] actions={ShowShop,ShowHome,ShowCollection};
            for(int i=0;i<3;i++)
            {
                var tab=At(nav,"Tab "+ids[i],i*360+16,13,328,232);bool selected=i==active;
                if(selected){Skin.RoundBox(tab,new Vector2(.5f,.5f),Vector2.zero,tab.sizeDelta,new Color32(83,44,124,255),30);Skin.RoundBox(tab,new Vector2(.5f,.5f),new Vector2(0,5),tab.sizeDelta-new Vector2(8,16),new Color32(130,77,175,255),27);}
                Skin.Art("game_assets_nav_tab"+ids[i],tab,new Vector2(.5f,1),new Vector2(0,-80),Vector2.one*126);
                string label=i==0?"商店":i==1?Zh.Home:Zh.Rank;
                MenuUI.Label(label,tab,new Vector2(.5f,1),new Vector2(0,-181),new Vector2(285,65),48,selected?Color.white:MenuUI.Ink,TextAnchor.MiddleCenter,true);
                MenuMotion.Press(Skin.Hit(tab,actions[i],.8f));
            }
        }
        public void StartLevel(int id,bool review=false,bool resume=false){CurrentLevel=Mathf.Clamp(id,1,500);Review=review;Playing=true;Finished=false;screen="game";ClearScreen("game_assets_bg");var level=Data.levels[CurrentLevel-1];Board=new BoardState(level,Shapes);Remaining=level.time;frozen=0;continuations=0;rewardedContinues=0;lastTick=-1;
            int firstHud=Content.childCount;
            var clock=At(Content,"Source timer",396,139,288,124);MenuUI.Paper(clock,new Vector2(.5f,.5f),Vector2.zero,clock.sizeDelta);timer=MenuUI.Label("",clock,new Vector2(.5f,.5f),Vector2.zero,new Vector2(270,105),76,MenuUI.Ink,TextAnchor.MiddleCenter);timeTrackWidth=266;timeFill=Skin.ColorBox(clock,new Vector2(.5f,0),new Vector2(0,8),new Vector2(timeTrackWidth,5*D),new Color32(46,125,79,255));
            var lp=At(Content,"Source level",33,151,241,101);MenuUI.Paper(lp,new Vector2(.5f,.5f),Vector2.zero,lp.sizeDelta);MenuUI.Label(Zh.Level(CurrentLevel),lp,new Vector2(.5f,.5f),Vector2.zero,new Vector2(230,90),46,MenuUI.Ink,TextAnchor.MiddleCenter,true);Skin.IconButton("game_assets_btnpause",Content,new Vector2(0,1),new Vector2(991,-207),Vector2.one*42*D,()=>Settings(true));
            targets=At(Content,"Source targets",31.5f,273,1017,225);((RectTransform)targets).pivot=new Vector2(0,1);((RectTransform)targets).anchoredPosition=new Vector2(31.5f,-273);
            headerLayout=clock.gameObject.AddComponent<WeChatHeaderLayout>();headerLayout.TrackChildren(Content,firstHud);
            var boardArea=At(Content,"Source board area",0,520,1080,1518);headerLayout.SetBoard(boardArea);BoardView=boardArea.gameObject.AddComponent<PuzzleBoardView>();BoardView.TutorialReservedDp=id==1?SourceTutorialReserveDp:0;BoardView.CanInput=()=>InputEnabled;BoardView.Message=SetMessage;BoardView.Init(Board);SourceWoodFrame.Build((RectTransform)boardArea.parent,BoardView);if(id<=10){var idle=boardArea.gameObject.AddComponent<SourceIdleHint>();idle.Init(this,BoardView);}
            message=Skin.Text("",Content,new Vector2(0,1),new Vector2(540,-2000),new Vector2(960,65),Mathf.RoundToInt(14*D),new Color32(122,62,6,255));
            string[] icons={"pen","freeze","lens"};for(int i=0;i<3;i++){int k=i;var slot=Skin.Rect("Source booster "+i,Content,new Vector2(0,1),new Vector2(289.5f+i*252,-2197.5f),Vector2.one*84*D);MenuUI.Paper(slot,new Vector2(.5f,.5f),Vector2.zero,slot.sizeDelta);MenuUI.Art("menu_"+icons[i],slot,new Vector2(.5f,.5f),new Vector2(0,25),Vector2.one*155);MenuUI.Label(i==0?Zh.MagicPen:i==1?Zh.FreezeTime:Zh.Lens,slot,new Vector2(.5f,.5f),new Vector2(0,-75),new Vector2(205,58),42,MenuUI.Ink,TextAnchor.MiddleCenter);Skin.Hit(slot,()=>UseBooster(k),.6f);if(CurrentLevel<(i==0?3:i==1?7:10))slot.GetComponent<CanvasGroup>().alpha=.8f;}
            bool restartIntro=id==1&&SourceTutorialEnabled&&!Store.Data.drawIntroSeen;
            if(restartIntro&&!review&&Store.Data.resumeLevel==1){Store.Data.resumeLevel=0;Store.Data.resumePieces=null;Store.Data.resumeTime=0;Store.Save();}
            Board.Changed+=BoardChanged;if(resume&&!restartIntro&&Store.Data.resumeLevel==id&&Store.Data.resumePieces!=null){suppress=true;Board.Restore(Store.Data.resumePieces);Remaining=Mathf.Max(1,Store.Data.resumeTime);suppress=false;}
            RefreshTargets();BoosterCounts();TimerChanged();if(id==1&&Board.Pieces.Count==0&&SourceTutorialEnabled&&!Store.Data.drawIntroSeen)Tutorial();FocusFirst();}
        void RefreshTargets(){if(targets!=null&&!targets.GetComponent<SourceTargetsStrip>())SourceHud.BuildTargets((RectTransform)targets,Board,D);}
        void BoosterCounts(){var old=Content.Find("Booster counts");if(old){old.gameObject.SetActive(false);Destroy(old.gameObject);}var layer=Skin.Fill("Booster counts",Content);int[] counts={Store.Data.hint,Store.Data.freeze,Store.Data.lens};int[] unlock={3,7,10};for(int i=0;i<3;i++){if(CurrentLevel<unlock[i]){Skin.Art("shell_src_ui_lock",layer,new Vector2(0,1),new Vector2(381.375f+i*252,-2105.875f),Vector2.one*22*D);var pill=Skin.Rect("Unlock level",layer,new Vector2(0,1),new Vector2(289.5f+i*252,-2329),new Vector2(48,20)*D);Skin.RoundBox(pill,new Vector2(.5f,.5f),Vector2.zero,pill.sizeDelta,new Color32(251,245,230,255),10*D);Skin.Text(Zh.UnlockBadge(unlock[i]),pill,new Vector2(.5f,.5f),Vector2.zero,pill.sizeDelta,Mathf.RoundToInt(10*D));continue;}
                var r=Skin.Rect("Source count",layer,new Vector2(0,1),new Vector2(370.875f+i*252,-2135f),Vector2.one*30*D);if(counts[i]>0){Skin.HSlice(r,"game_assets_panelbadgecap","game_assets_panelbadgemid",r.sizeDelta,.495798319f);MenuUI.Label(counts[i].ToString(),r,new Vector2(.5f,.5f),Vector2.zero,r.sizeDelta,42,Color.white,TextAnchor.MiddleCenter);}else {Skin.Art("shell_src_ui_greenbase",r,new Vector2(.5f,.5f),Vector2.zero,Vector2.one*26*D);Skin.Art("shell_src_ui_plus",r,new Vector2(.5f,.5f),Vector2.zero,Vector2.one*15*D);}}}
        public RectTransform Popup(string title,float width=920,float height=1200){if(BoardView)BoardView.Cancel();var shade=Skin.Fill(title+" overlay",Overlays);shade.gameObject.AddComponent<Image>().color=new Color(30/255f,20/255f,12/255f,.70f);var motion=Skin.Rect("Source popup motion",shade,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1080,2400));float w=Mathf.Min(width,1080*.99f-48*D);var panel=Skin.Rect(title+" panel",motion,new Vector2(.5f,.5f),new Vector2(0,-70.875f),new Vector2(w,height));MenuUI.Paper(panel,new Vector2(.5f,.5f),Vector2.zero,panel.sizeDelta,true);MenuUI.Label(title,panel,new Vector2(.5f,1),new Vector2(0,-72),new Vector2(w-175,110),72,MenuUI.Ink,TextAnchor.MiddleCenter,true);Skin.RoundBox(panel,new Vector2(.5f,1),new Vector2(0,-150),new Vector2(w-100,4),new Color32(203,164,89,255),2);
            Skin.IconButton("shell_src_ui_close",panel,new Vector2(1,1),new Vector2(-40,-55),Vector2.one*42*D,ClosePopup);popups.Push(shade);SourceMotion.Popup(motion);FocusFirst();return panel;}
        public void Settings(bool pause){var panel=Popup(pause?Zh.Paused:Zh.Settings,1080*.99f-48*D,1166);float width=panel.sizeDelta.x;var card=At(panel,"Source settings card",18*D,82*D,width-36*D,104*D);Skin.RoundBox(card,new Vector2(.5f,.5f),Vector2.zero,card.sizeDelta,new Color32(220,196,151,255),16*D);Skin.RoundBox(card,new Vector2(.5f,.5f),Vector2.zero,card.sizeDelta-Vector2.one*4*D,new Color32(244,229,197,255),14*D);
            string[] keys={"soundicon","musicicon","hapticsicon"};for(int i=0;i<3;i++){int k=i;bool on=i==0?Store.Data.sound:i==1?Store.Data.music:Store.Data.haptics;var button=Skin.Rect(keys[i],card,new Vector2(.5f,1),new Vector2((i-1)*80*D,-50*D),Vector2.one*64*D);var outline=Skin.RoundBox(button,new Vector2(.5f,.5f),Vector2.zero,button.sizeDelta,on?new Color32(162,120,198,255):new Color32(185,169,143,255),14*D);var face=Skin.RoundBox(button,new Vector2(.5f,.5f),Vector2.zero,button.sizeDelta-Vector2.one*4*D,on?new Color32(114,66,160,255):new Color32(155,137,110,255),12*D);var icon=Skin.Art("shell_src_ui_"+keys[i],button,new Vector2(.5f,.5f),Vector2.zero,Vector2.one*32*D);icon.color=new Color(1,1,1,on?1:.4f);var slash=Skin.RoundBox(button,new Vector2(.5f,.5f),Vector2.zero,new Vector2(40*D,3*D),new Color32(255,68,68,255),2*D);slash.rectTransform.localRotation=Quaternion.Euler(0,0,45);slash.gameObject.SetActive(!on);Skin.Hit(button,()=>{if(k==0)Store.Data.sound=!Store.Data.sound;else if(k==1)Store.Data.music=!Store.Data.music;else Store.Data.haptics=!Store.Data.haptics;SoundBank.Settings(Store.Data.sound,Store.Data.music);Store.Save();bool enabled=k==0?Store.Data.sound:k==1?Store.Data.music:Store.Data.haptics;outline.color=enabled?new Color32(162,120,198,255):new Color32(185,169,143,255);face.color=enabled?new Color32(114,66,160,255):new Color32(155,137,110,255);icon.color=new Color(1,1,1,enabled?1:.4f);slash.gameObject.SetActive(!enabled);},.7f);}
            if(pause){MenuUI.SecondaryButton("重新开始",panel,new Vector2(.5f,1),new Vector2(-198,-640),new Vector2(360,160),()=>LeavePrompt(true),48);MenuUI.SecondaryButton("返回首页",panel,new Vector2(.5f,1),new Vector2(198,-640),new Vector2(360,160),()=>LeavePrompt(false),48);MenuUI.GameButton(Zh.Resume,panel,new Vector2(.5f,1),new Vector2(0,-343*D),new Vector2((width-36*D)*.7f,80*1.15f*D),ClosePopup,Mathf.RoundToInt(30*D));}
            else {
                Button motionButton=null;
                motionButton=MenuUI.SecondaryButton(Store.Data.reducedMenuMotion?"菜单动效：精简":"菜单动效：完整",panel,new Vector2(.5f,1),new Vector2(0,-630),new Vector2(width-120,140),()=>{
                    Store.Data.reducedMenuMotion=!Store.Data.reducedMenuMotion;Store.Save();
                    foreach(var label in motionButton.GetComponentsInChildren<Text>())label.text=Store.Data.reducedMenuMotion?"菜单动效：精简":"菜单动效：完整";
                },48);motionButton.name="Menu motion preference";MenuMotion.Press(motionButton);
                MenuUI.GameButton(Zh.Done,panel,new Vector2(.5f,1),new Vector2(0,-343*D),new Vector2((width-36*D)*.7f,80*1.15f*D),ClosePopup,Mathf.RoundToInt(30*D));
            }
            Skin.Text(Zh.Version,panel,new Vector2(.5f,1),new Vector2(0,-410.3f*D),new Vector2(700,21*D),Mathf.RoundToInt(12*D),new Color32(85,85,136,255));}
        IEnumerator WinPopup(){int epoch=presentationEpoch;while(BoardView&&BoardView.VictoryTimeRemaining>0)yield return null;if(epoch!=presentationEpoch||!Playing||!Finished)yield break;ShowSourceResult();}

    }
}

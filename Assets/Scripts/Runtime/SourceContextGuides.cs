using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Inkomino
{
    public partial class InkominoApp
    {
        static readonly int[] BoosterUnlockLevels={3,7,10};
        static readonly string[] GuideToolNames={"涂鸦笔","冰冰饮","灵感镜"};
        static readonly string[] GuideToolArt={"menu_pen","menu_freeze","menu_lens"};
        static readonly string[] BarrierNames={"普通封条","同形封条","异形封条"};
        static readonly string[] BarrierDescriptions={"两边贴纸的形状不限","两边贴纸的形状必须相同","两边贴纸的形状必须不同"};
        static readonly string[] BarrierArt={"plain","same","different"};
        static bool ContainsBarrier(LevelData level,int kind){return level.walls!=null&&(level.walls.e.Contains(kind)||level.walls.s.Contains(kind));}
        void StartContextGuide()
        {
            if(Review||!SourceTutorialEnabled)return;
            for(int i=0;i<3;i++)if(CurrentLevel==BoosterUnlockLevels[i]&&(Store.Data.boosterIntroSeen&(1<<i))==0){ShowBoosterGuide(i,true);return;}
            for(int kind=1;kind<=3;kind++)if((Store.Data.barrierIntroSeen&(1<<(kind-1)))==0&&ContainsBarrier(Board.Level,kind)&&!Data.levels.Any(l=>l.id<CurrentLevel&&ContainsBarrier(l,kind))){ShowBarrierGuide(kind);return;}
        }
        public void ShowBoosterGuide(int index,bool first=false)
        {
            if(index<0||index>2)return;
            var panel=Popup((first?"解锁":"")+GuideToolNames[index],920,1050);panel.name="Booster guide "+index;
            MenuUI.Art(GuideToolArt[index],panel,Vector2.one*.5f,new Vector2(0,180),Vector2.one*240);
            string[] descriptions={"自动完成一块正确图形\n生成的贴纸不能移除","暂停倒计时 10 秒\n趁这段时间继续划线","显示接下来两块图形的位置\n照着提示划出形状"};
            MenuUI.Label(descriptions[index],panel,Vector2.one*.5f,new Vector2(0,-70),new Vector2(760,180),48,MenuUI.Ink,TextAnchor.MiddleCenter);
            MenuUI.Label(CurrentLevel>=BoosterUnlockLevels[index]?"点下方道具卡使用":"第 "+BoosterUnlockLevels[index]+" 关解锁",panel,Vector2.one*.5f,new Vector2(0,-220),new Vector2(720,65),42,MenuUI.Muted,TextAnchor.MiddleCenter);
            GuideConfirm(panel,()=>{if(first){Store.Data.boosterIntroSeen|=1<<index;Store.Save();}ClosePopup();});
        }
        void ShowBarrierGuide(int kind)
        {
            var panel=Popup("认识封条",920,1050);panel.name="Barrier guide "+kind;
            MenuUI.Label("划线不能跨过任何封条",panel,Vector2.one*.5f,new Vector2(0,260),new Vector2(760,80),48,MenuUI.Ink,TextAnchor.MiddleCenter);
            RuleRow(panel,kind,40,true);
            MenuUI.Label("点右上方“规则”可随时查看",panel,Vector2.one*.5f,new Vector2(0,-220),new Vector2(760,65),42,MenuUI.Muted,TextAnchor.MiddleCenter);
            GuideConfirm(panel,()=>{Store.Data.barrierIntroSeen|=1<<(kind-1);Store.Save();ClosePopup();});
        }
        void GuideConfirm(RectTransform panel,Action confirm){var button=MenuUI.GameButton("知道了",panel,new Vector2(.5f,0),new Vector2(0,125),new Vector2(670,150),confirm,58);button.name="Guide confirm";}
        public void ShowGameHelp()
        {
            if(!Playing||Finished)return;
            var panel=Popup("玩法说明",920,1500);panel.name="Game rules help";
            MenuUI.Label("划线不能跨过任何封条",panel,Vector2.one*.5f,new Vector2(0,535),new Vector2(760,70),48,MenuUI.Ink,TextAnchor.MiddleCenter);
            for(int kind=1;kind<=3;kind++)RuleRow(panel,kind,330-(kind-1)*270,false);
            for(int i=0;i<3;i++){
                int index=i;var item=Skin.Rect("Guide tool entry "+i,panel,Vector2.one*.5f,new Vector2((i-1)*245,-465),new Vector2(225,160));
                MenuUI.Art(GuideToolArt[i],item,Vector2.one*.5f,new Vector2(0,25),Vector2.one*100);
                MenuUI.Label(GuideToolNames[i],item,Vector2.one*.5f,new Vector2(0,-56),new Vector2(220,60),42,MenuUI.Ink,TextAnchor.MiddleCenter,true);
                Skin.Hit(item,()=>ShowBoosterGuide(index),.8f);
            }
            GuideConfirm(panel,ClosePopup);
        }
        void RuleRow(RectTransform parent,int kind,float y,bool large)
        {
            var row=MenuUI.Paper(parent,Vector2.one*.5f,new Vector2(0,y),new Vector2(820,large?315:245));row.name="Rule explanation "+kind;
            RuleBadge(row,kind,new Vector2(-300,20),120);
            MenuUI.Label(BarrierNames[kind-1],row,Vector2.one*.5f,new Vector2(65,large?105:75),new Vector2(570,65),52,MenuUI.Ink,TextAnchor.MiddleLeft,true);
            MenuUI.Label(BarrierDescriptions[kind-1],row,Vector2.one*.5f,new Vector2(65,large?32:10),new Vector2(570,68),42,MenuUI.Ink,TextAnchor.MiddleLeft);
            float bottom=large?-75:-67;
            MiniRuleShape(row,new Vector2(-75,bottom),false,StickerDesign.Lilac);
            MiniRuleShape(row,new Vector2(185,bottom),kind!=2,kind==2?StickerDesign.Mint:StickerDesign.Pink);
            var seal=MenuUI.Art("barrier_strip_"+BarrierArt[kind-1],row,Vector2.one*.5f,new Vector2(50,bottom),new Vector2(110,22));seal.rectTransform.localRotation=Quaternion.Euler(0,0,90);
            RuleBadge(row,kind,new Vector2(50,bottom),66);
        }
        void RuleBadge(Transform parent,int kind,Vector2 position,float size)
        {
            var root=Skin.Rect("Rule badge "+kind,parent,Vector2.one*.5f,position,Vector2.one*size);
            MenuUI.Art("barrier_badge_"+BarrierArt[kind-1],root,Vector2.one*.5f,Vector2.zero,root.sizeDelta);
            Skin.RoundBox(root,Vector2.one*.5f,Vector2.zero,Vector2.one*size*.69f,StickerDesign.Background,size*.345f);
            var glyph=Skin.Fill("Rule symbol",root).gameObject.AddComponent<StickerRuleGlyph>();glyph.Kind=kind;glyph.raycastTarget=false;
        }
        void MiniRuleShape(Transform parent,Vector2 position,bool line,Color tint)
        {
            var root=Skin.Rect("Rule shape example",parent,Vector2.one*.5f,position,new Vector2(100,75));
            var cells=line?new[]{new Vector2(-32,0),Vector2.zero,new Vector2(32,0)}:new[]{new Vector2(-16,16),new Vector2(-16,-16),new Vector2(16,-16)};
            foreach(var cell in cells){var tile=Skin.Rect("Example tile",root,Vector2.one*.5f,cell,Vector2.one*29).gameObject.AddComponent<StickerPreviewTile>();tile.color=tint;tile.raycastTarget=false;}
        }
    }
}

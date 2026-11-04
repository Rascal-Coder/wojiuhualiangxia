using System;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    public partial class InkominoApp
    {
        // All shop amounts remain in-game coin exchanges. Video rewards are
        // a separate, explicitly labelled action underneath the item shelf.
        public void ShowShop()
        {
            Playing=false;
            screen="shop";
            ClearScreen("game_assets_shopbg");
            Wallet();
            int firstContentChild=Content.childCount;
            var band=Skin.Rect("Shop header band",Content,new Vector2(.5f,1),new Vector2(0,-630),new Vector2(980,200));
            MenuUI.Extrude(band,Vector2.one*.5f,Vector2.zero,band.sizeDelta,new Color32(255,244,228,255),MenuUI.WarmShadow,40);
            var title=MenuUI.Label("灵感补给站",Content,new Vector2(.5f,1),new Vector2(0,-600),new Vector2(900,100),MenuUI.TitleSize,MenuUI.Ink,TextAnchor.MiddleCenter,true);title.name="Menu page title";
            MenuUI.Label("每种每日 3 次 · 0 点刷新",Content,new Vector2(.5f,1),new Vector2(0,-684),new Vector2(860,52),MenuUI.Small,MenuUI.Muted,TextAnchor.MiddleCenter);

            string[] icons={"pen","freeze","lens"};
            string[] items={"hint","freeze","lens"};
            string[] titles={Zh.MagicPen,Zh.FreezeTime,Zh.Lens};
            string[] descriptions={"自动完成一块图形","暂停倒计时 10 秒","查看两块图形的提示"};
            Color32[] plates={MenuUI.PenWash,MenuUI.IceWash,MenuUI.LensWash};
            Vector2 top=new Vector2(0,1);

            var supply=Skin.Rect("Shop coin supply",Content,new Vector2(.5f,1),new Vector2(0,-900),new Vector2(960,268));
            MenuUI.SoftDepth(supply,supply.sizeDelta,40,MenuUI.WarmShadow);
            MenuUI.Extrude(supply,Vector2.one*.5f,Vector2.zero,supply.sizeDelta,MenuUI.MintWash,MenuUI.MintDepth,40);
            if(RewardEntryEnabled(RewardPlacement.CoinSupply))
            {
                Skin.Art("shell_src_ui_coin",supply,top,new Vector2(87,-71),Vector2.one*78);
                MenuUI.Label("金币补给",supply,top,new Vector2(300,-71),new Vector2(340,70),52,MenuUI.Ink,TextAnchor.MiddleLeft,true);
                int amount=AdPlacements.Slot(RewardPlacement.CoinSupply).amount;
                MenuUI.Label("完整观看可领\n+"+amount+" 金币",supply,top,new Vector2(300,-167),new Vector2(340,104),42,MenuUI.Muted);
                var video=RewardButton(RewardPlacement.CoinSupply,"看视频领取",supply,top,new Vector2(715,-130),new Vector2(430,150),RewardCoinSupply,true);
                foreach(var oldLabel in video.GetComponentsInChildren<Text>())
                {
                    if(oldLabel.name!="Rewarded CTA label")continue;
                    oldLabel.gameObject.SetActive(false);
                    Destroy(oldLabel.gameObject);
                }
                var videoLabel=MenuUI.Label("看视频领取",video.transform,new Vector2(.5f,.5f),new Vector2(42,0),new Vector2(269,98),48,MenuUI.Ink,TextAnchor.MiddleCenter);
                videoLabel.name="Shop rewarded CTA label";
            }
            else
            {
                Skin.Art("shell_src_ui_coin",supply,top,new Vector2(112,-105),Vector2.one*130);
                MenuUI.Label("通关攒金币",supply,top,new Vector2(580,-66),new Vector2(710,72),54,MenuUI.Ink,TextAnchor.MiddleLeft,true);
                MenuUI.Label("完成拼图可获得金币奖励",supply,top,new Vector2(580,-140),new Vector2(710,66),48,MenuUI.Muted);
            }

            for(int i=0;i<icons.Length;i++)
            {
                int index=i;
                float rowCenter=1220+i*312;
                var row=Skin.Rect("Shop row "+i,Content,new Vector2(.5f,1),new Vector2(0,-rowCenter),new Vector2(980,292));
                MenuUI.SoftDepth(row,row.sizeDelta,36,MenuUI.WarmShadow);
                MenuUI.Extrude(row,Vector2.one*.5f,Vector2.zero,row.sizeDelta,MenuUI.Cream,MenuUI.WarmShadow,36);
                MenuUI.Plate(row,top,new Vector2(128,-146),Vector2.one*176,plates[i],40);
                var tool=MenuUI.Art("menu_"+icons[i],row,top,new Vector2(128,-146),Vector2.one*148);
                if(tool){MenuMotion.Float(tool.rectTransform,4,1.1f);if(i==0)MenuMotion.Sparkle(tool.rectTransform,3,true);}
                MenuUI.Label(titles[i],row,top,new Vector2(430,-78),new Vector2(340,56),48,MenuUI.Ink,TextAnchor.MiddleLeft,true);
                MenuUI.Label(descriptions[i],row,top,new Vector2(430,-142),new Vector2(340,48),42,MenuUI.Muted);
                ShopPurchaseControls(row,items[i],top,new Vector2(812,-176),quantity=>
                {
                        ShowShop();
                        Info(Zh.PurchaseReady,"已获得 "+quantity+" 个"+titles[index]+"。");
                        var confirmation=popups.Peek().Find("Source popup motion/"+Zh.PurchaseReady+" panel") as RectTransform;
                        if(confirmation){var granted=MenuUI.Art("menu_"+icons[index],confirmation,new Vector2(.5f,.5f),new Vector2(0,220),Vector2.one*170);if(granted)MenuMotion.Celebrate(granted.rectTransform);}
                },new Vector2(860,-40));
            }

            MenuUI.Adapt(Content,headerLayout,firstContentChild,2024);
            Navigation(0);
            FocusFirst();
        }

        void ShopPurchaseControls(Transform parent,string item,Vector2 anchor,Vector2 position,Action<int> purchased,Vector2? quotaPosition=null)
        {
            var group=Skin.Rect("Shop purchase "+item,parent,anchor,position,new Vector2(300,216));
            var quota=Skin.Rect("Shop remaining "+item,parent,anchor,quotaPosition??position+new Vector2(0,156),new Vector2(148,36));
            var stamps=new Image[SaveStore.ShopDailyLimit];
            var pips=new RoundedGraphic[SaveStore.ShopDailyLimit];
            for(int i=0;i<stamps.Length;i++)
            {
                var slot=Skin.Rect("Purchase stamp slot "+i,quota,Vector2.one*.5f,new Vector2((i-1)*40,0),Vector2.one*28);
                pips[i]=Skin.RoundBox(slot,Vector2.one*.5f,Vector2.zero,slot.sizeDelta,MenuUI.Coral,14);pips[i].name="Purchase pip";
                var mark=slot.gameObject.AddComponent<Image>();mark.raycastTarget=false;mark.enabled=false;mark.color=Color.white;mark.name="Purchase stamp "+i;stamps[i]=mark;
            }
            var amount=MenuUI.Label("1",group,Vector2.one*.5f,new Vector2(0,50),new Vector2(64,64),46,MenuUI.Ink,TextAnchor.MiddleCenter);amount.name="Shop quantity "+item;
            int quantity=1;Button minus=null,plus=null,buy=null;
            void Refresh(){int left=Store.ShopRemaining(item,SaveStore.Now);for(int i=0;i<stamps.Length;i++){bool on=i<left;if(stamps[i])stamps[i].color=on?Color.white:new Color(.65f,.60f,.72f,.24f);if(pips[i])pips[i].color=on?MenuUI.Coral:new Color(.78f,.74f,.82f,.35f);}amount.text=quantity.ToString();minus.interactable=quantity>1&&left>0;plus.interactable=quantity<SaveStore.ShopQuantityLimit&&left>0;int price=SaveStore.ShopPrice(item,quantity);buy.GetComponentInChildren<Text>().text=left==0?"今日售罄":price+" 金币";buy.interactable=Store.ShopError(item,price,SaveStore.Now,quantity).Length==0;}
            minus=MenuUI.SecondaryButton("-",group,Vector2.one*.5f,new Vector2(-86,50),new Vector2(96,92),()=>{quantity=Mathf.Max(1,quantity-1);Refresh();},48);minus.name="Shop minus "+item;
            plus=MenuUI.SecondaryButton("+",group,Vector2.one*.5f,new Vector2(86,50),new Vector2(96,92),()=>{quantity=Mathf.Min(SaveStore.ShopQuantityLimit,quantity+1);Refresh();},48);plus.name="Shop plus "+item;
            buy=MenuUI.GameButton("",group,Vector2.one*.5f,new Vector2(0,-52),new Vector2(248,100),()=>{int price=SaveStore.ShopPrice(item,quantity);if(Store.Buy(item,price,quantity)){SoundBank.Play("coin");Feel.Success();purchased(quantity);}else{Refresh();Info("暂时无法补给",Store.ShopError(item,price,SaveStore.Now,quantity));}},46);buy.name="Shop exchange "+item;
            Refresh();
            group.gameObject.AddComponent<ShopPurchaseRefresh>().Init(Store,Refresh);
        }
    }
    // Refresh affordability after rewards/cloud changes without rebuilding the cards.
    public sealed class ShopPurchaseRefresh:MonoBehaviour
    {
        SaveStore store;Action refresh;long day;
        public void Init(SaveStore value,Action update){store=value;refresh=update;day=(SaveStore.Now+8*3600)/86400;store.Changed+=Changed;InvokeRepeating(nameof(CheckDay),1,1);}
        void Changed(){if(isActiveAndEnabled)refresh?.Invoke();}
        void CheckDay(){long current=(SaveStore.Now+8*3600)/86400;if(current!=day){day=current;Changed();}}
        void OnDisable(){if(store!=null)store.Changed-=Changed;CancelInvoke();}
    }
}

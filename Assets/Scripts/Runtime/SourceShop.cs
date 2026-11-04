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
            MenuUI.Title("灵感补给站","每种每日 3 次 · 0 点刷新",Content);

            string[] icons={"pen","freeze","lens"};
            string[] items={"hint","freeze","lens"};
            string[] titles={Zh.MagicPen,Zh.FreezeTime,Zh.Lens};
            string[] descriptions={"自动完成一块图形","暂停倒计时 10 秒","查看两块图形的提示"};
            Color32[] plates={MenuUI.PenWash,MenuUI.IceWash,MenuUI.LensWash};
            Vector2 top=new Vector2(0,1);

            for(int i=0;i<icons.Length;i++)
            {
                int index=i;
                var row=MenuUI.Paper(Content,new Vector2(.5f,1),new Vector2(0,-(1176+i*372)),new Vector2(960,356));
                row.name="Shop row "+i;

                MenuUI.Plate(row,top,new Vector2(148,-178),Vector2.one*236,plates[i],56);
                var tool=MenuUI.Art("menu_"+icons[i],row,top,new Vector2(148,-176),Vector2.one*198);
                if(tool){MenuMotion.Float(tool.rectTransform,4,1.1f);if(i==0)MenuMotion.Sparkle(tool.rectTransform,3,true);}
                MenuUI.Label(titles[i],row,top,new Vector2(528,-56),new Vector2(450,76),60,MenuUI.Ink,TextAnchor.MiddleLeft,true);

                MenuUI.Label(descriptions[i],row,top,new Vector2(612,-128),new Vector2(600,58),46,MenuUI.Muted);
                ShopPurchaseControls(row,items[i],top,new Vector2(612,-280),quantity=>
                {
                        ShowShop();
                        Info(Zh.PurchaseReady,"已获得 "+quantity+" 个"+titles[index]+"。");
                        var confirmation=popups.Peek().Find("Source popup motion/"+Zh.PurchaseReady+" panel") as RectTransform;
                        if(confirmation){var granted=MenuUI.Art("menu_"+icons[index],confirmation,new Vector2(.5f,.5f),new Vector2(0,220),Vector2.one*170);if(granted)MenuMotion.Celebrate(granted.rectTransform);}
                },new Vector2(800,-58));
            }

            var supply=MenuUI.Paper(Content,new Vector2(.5f,1),new Vector2(0,-848),new Vector2(960,268),true);
            supply.name="Shop coin supply";
            if(RewardEntryEnabled(RewardPlacement.CoinSupply))
            {
                Skin.Art("shell_src_ui_coin",supply,top,new Vector2(87,-71),Vector2.one*78);
                MenuUI.Label("金币补给",supply,top,new Vector2(310,-71),new Vector2(350,70),54,MenuUI.Ink,TextAnchor.MiddleLeft,true);
                int amount=AdPlacements.Slot(RewardPlacement.CoinSupply).amount;
                MenuUI.Label("完整观看可领\n+"+amount+" 金币",supply,top,new Vector2(310,-167),new Vector2(350,104),42,MenuUI.Muted);
                var video=RewardButton(RewardPlacement.CoinSupply,"看视频领取",supply,top,new Vector2(715,-130),new Vector2(430,150),RewardCoinSupply,true);
                // Keep the shared ad button behaviour and icon, but give this
                // compact shop action the same explicit typography as the page.
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

            MenuUI.Adapt(Content,headerLayout,firstContentChild,2080);
            Navigation(0);
            FocusFirst();
        }

        void ShopPurchaseControls(Transform parent,string item,Vector2 anchor,Vector2 position,Action<int> purchased,Vector2? quotaPosition=null)
        {
            var group=Skin.Rect("Shop purchase "+item,parent,anchor,position,new Vector2(660,126));
            var quota=Skin.Rect("Shop remaining "+item,parent,anchor,quotaPosition??position+new Vector2(0,110),new Vector2(230,80));
            var stamps=new Image[SaveStore.ShopDailyLimit];
            for(int i=0;i<stamps.Length;i++)
            {
                var slot=Skin.Rect("Purchase stamp slot "+i,quota,Vector2.one*.5f,new Vector2((i-1)*78,0),Vector2.one*88);
                MenuUI.Extrude(slot,Vector2.one*.5f,Vector2.zero,slot.sizeDelta,MenuUI.Cream,MenuUI.CoralChip,44);
                stamps[i]=MenuUI.Art("ink_shop",slot,Vector2.one*.5f,Vector2.zero,Vector2.one*64);if(stamps[i])stamps[i].name="Purchase stamp "+i;
            }
            var amount=MenuUI.Label("1",group,Vector2.one*.5f,new Vector2(-151,0),new Vector2(110,100),52,MenuUI.Ink,TextAnchor.MiddleCenter);amount.name="Shop quantity "+item;
            int quantity=1;Button minus=null,plus=null,buy=null;
            void Refresh(){int left=Store.ShopRemaining(item,SaveStore.Now);for(int i=0;i<stamps.Length;i++)if(stamps[i])stamps[i].color=i<left?Color.white:new Color(.65f,.60f,.72f,.24f);amount.text=quantity.ToString();minus.interactable=quantity>1&&left>0;plus.interactable=quantity<SaveStore.ShopQuantityLimit&&left>0;int price=SaveStore.ShopPrice(item,quantity);buy.GetComponentInChildren<Text>().text=left==0?"今日售罄":price+" 金币";buy.interactable=Store.ShopError(item,price,SaveStore.Now,quantity).Length==0;}
            minus=MenuUI.SecondaryButton("-",group,Vector2.one*.5f,new Vector2(-275,0),new Vector2(136,126),()=>{quantity=Mathf.Max(1,quantity-1);Refresh();},60);minus.name="Shop minus "+item;
            plus=MenuUI.SecondaryButton("+",group,Vector2.one*.5f,new Vector2(-27,0),new Vector2(136,126),()=>{quantity=Mathf.Min(SaveStore.ShopQuantityLimit,quantity+1);Refresh();},60);plus.name="Shop plus "+item;
            buy=MenuUI.GameButton("",group,Vector2.one*.5f,new Vector2(204,0),new Vector2(288,132),()=>{int price=SaveStore.ShopPrice(item,quantity);if(Store.Buy(item,price,quantity)){SoundBank.Play("coin");Feel.Success();purchased(quantity);}else{Refresh();Info("暂时无法补给",Store.ShopError(item,price,SaveStore.Now,quantity));}},48);buy.name="Shop exchange "+item;
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

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
            MenuUI.Title("道具商店","攒点金币，给灵感加个油",Content);

            string[] icons={"pen","freeze","lens"};
            string[] items={"hint","freeze","lens"};
            string[] titles={Zh.MagicPen,Zh.FreezeTime,Zh.Lens};
            string[] descriptions={"自动完成一块图形","暂停倒计时 10 秒","查看两块图形的提示"};
            int[] prices={150,120,150};
            int[] counts={Store.Data.hint,Store.Data.freeze,Store.Data.lens};
            Vector2 top=new Vector2(0,1);

            for(int i=0;i<icons.Length;i++)
            {
                int index=i;
                var row=MenuUI.Paper(Content,new Vector2(.5f,1),new Vector2(0,-(1200+i*355)),new Vector2(960,330));
                row.name="Shop row "+i;

                // Jewel-and-gold booster art shares one silhouette scale;
                // the paper card gives every item one text baseline.
                Skin.RoundBox(row,top,new Vector2(150,-165),Vector2.one*238,new Color32(242,228,198,255),48);
                var tool=MenuUI.Art("menu_"+icons[i],row,top,new Vector2(150,-164),Vector2.one*224);
                if(tool){MenuMotion.Float(tool.rectTransform,4,1.1f);if(i==0)MenuMotion.Sparkle(tool.rectTransform,3,true);}
                MenuUI.Label(titles[i],row,top,new Vector2(515,-58),new Vector2(430,72),56,MenuUI.Ink,TextAnchor.MiddleLeft,true);

                var pack=Skin.Rect("Three boosters per pack",row,top,new Vector2(840,-58),new Vector2(170,66));
                Skin.RoundBox(pack,new Vector2(.5f,.5f),Vector2.zero,pack.sizeDelta,new Color32(238,223,185,255),30);
                MenuUI.Label("3 个装",pack,new Vector2(.5f,.5f),Vector2.zero,pack.sizeDelta,42,MenuUI.Muted,TextAnchor.MiddleCenter);

                MenuUI.Label(descriptions[i],row,top,new Vector2(600,-134),new Vector2(600,60),48,MenuUI.Muted);
                MenuUI.Label("已有 "+ShopOwnedCount(counts[i])+" 个",row,top,new Vector2(405,-250),new Vector2(210,65),42,MenuUI.Muted);

                var buy=MenuUI.GameButton(prices[i]+" 金币",row,top,new Vector2(745,-250),new Vector2(360,130),()=>
                {
                    if(Store.Buy(items[index],prices[index]))
                    {
                        SoundBank.Play("coin");
                        // Recreate the wallet and inventory before the success
                        // overlay, so closing it never reveals stale quantities.
                        ShowShop();
                        Info(Zh.PurchaseReady,"已获得 3 个"+titles[index]+"。");
                        var confirmation=popups.Peek().Find("Source popup motion/"+Zh.PurchaseReady+" panel") as RectTransform;
                        if(confirmation){var granted=MenuUI.Art("menu_"+icons[index],confirmation,new Vector2(.5f,.5f),new Vector2(0,220),Vector2.one*170);if(granted)MenuMotion.Celebrate(granted.rectTransform);}
                    }
                    else Info(Zh.MoreCoins,Zh.EarnCoins);
                },52);
                buy.name="Shop exchange "+items[i];
            }

            var supply=MenuUI.Paper(Content,new Vector2(.5f,1),new Vector2(0,-855),new Vector2(960,260),true);
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

        static string ShopOwnedCount(int count)
        {
            if(count>=100000000)return (count/100000000).ToString()+"亿";
            if(count>=10000)return (count/10000).ToString()+"万";
            return Mathf.Max(0,count).ToString();
        }
    }
}

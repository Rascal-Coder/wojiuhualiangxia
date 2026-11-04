using UnityEngine;
namespace Inkomino
{
    // Shared casual palette. Screen chrome is built in MenuUI; this only names icons.
    public static class StickerDesign
    {
        public static readonly Color32 Background=new Color32(255,214,186,255),Ink=new Color32(98,52,34,255),Muted=new Color32(158,102,74,255);
        public static readonly Color32 Pink=new Color32(255,78,116,255),Mint=new Color32(117,218,186,255),Lilac=new Color32(255,186,140,255),Yellow=new Color32(255,214,96,255);
        public static readonly string[] Badges={"badge_seed","badge_star","badge_shield","badge_laurel","badge_crown"};
        public static readonly string[] Stamps={"stamp_sun","stamp_luck","stamp_bulb","stamp_hand","stamp_shine","stamp_rocket","stamp_surprise"};
        public static string Achievement(int wins){return Badges[wins>=500?4:wins>=250?3:wins>=100?2:wins>=50?1:0];}
        public static void Backdrop(Transform parent,string page)
        {
            // Warm peach ground. Stamp scatter read as a sticker collage.
            if(parent==null||string.IsNullOrEmpty(page))return;
        }
    }
}

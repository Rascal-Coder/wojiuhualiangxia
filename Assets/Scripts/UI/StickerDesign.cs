using UnityEngine;
namespace Inkomino
{
    // Shared art direction: additions must derive from the same chrome-sheet reference.
    public static class StickerDesign
    {
        public static readonly Color32 Background=new Color32(255,249,238,255),Ink=new Color32(65,47,91,255),Muted=new Color32(126,105,147,255);
        public static readonly Color32 Pink=new Color32(255,99,139,255),Mint=new Color32(117,218,186,255),Lilac=new Color32(169,128,239,255),Yellow=new Color32(255,227,107,255);
        public static readonly string[] Badges={"badge_seed","badge_star","badge_shield","badge_laurel","badge_crown"};
        public static readonly string[] Stamps={"stamp_sun","stamp_luck","stamp_bulb","stamp_hand","stamp_shine","stamp_rocket","stamp_surprise"};
        public static string Achievement(int wins){return Badges[wins>=500?4:wins>=250?3:wins>=100?2:wins>=50?1:0];}
        public static void Backdrop(Transform parent,string page)
        {
            if(page=="game")return;
            // Each destination has its own motifs and composition; only art treatment is shared.
            if(page=="shop"){
                Motif(parent,"reward_coins",105,470,178,-18);Motif(parent,"stamp_surprise",960,605,196,12);
                Motif(parent,"reward_coins",1025,1110,135,24);Motif(parent,"stamp_surprise",50,1420,164,-13);
                Motif(parent,"reward_coins",1020,1830,165,-15);Motif(parent,"stamp_surprise",180,2175,190,8);
            }else if(page=="collection"){
                Motif(parent,"badge_laurel",120,470,200,-13);Motif(parent,"badge_star",975,680,143,18);
                Motif(parent,"badge_star",32,1080,126,-16);Motif(parent,"badge_crown",1040,1550,204,14);
                Motif(parent,"badge_laurel",50,1950,176,-17);Motif(parent,"badge_star",900,2180,125,12);
            }else if(page=="friends"){
                Motif(parent,"entry_friends",115,510,190,-12);Motif(parent,"stamp_hand",965,700,154,16);
                Motif(parent,"stamp_hand",38,1190,126,-16);Motif(parent,"entry_friends",1040,1610,188,12);
                Motif(parent,"entry_friends",150,2160,172,-9);Motif(parent,"stamp_hand",960,2110,136,15);
            }else if(page=="climb"){
                Motif(parent,"entry_album",110,500,205,-18);Motif(parent,"stamp_luck",970,760,144,15);
                Motif(parent,"stamp_sun",35,1130,150,-12);Motif(parent,"stamp_rocket",1040,1470,179,20);
                Motif(parent,"stamp_shine",42,1875,140,-14);Motif(parent,"entry_album",925,2160,210,13);
            }else{
                Motif(parent,"stamp_sun",105,460,177,-12);Motif(parent,"stamp_shine",945,550,130,18);
                Motif(parent,"stamp_shine",45,960,126,-16);Motif(parent,"stamp_sun",1030,1350,165,15);
                Motif(parent,"stamp_shine",84,1610,150,-14);Motif(parent,"stamp_shine",975,1930,118,12);
                Motif(parent,"stamp_sun",200,2180,145,-15);
            }
        }
        static void Motif(Transform parent,string key,float x,float y,float size,float angle)
        {
            var art=MenuUI.Art(key,parent,new Vector2(0,1),new Vector2(x,-y),Vector2.one*size);
            art.gameObject.name="Sticker background motif "+key;
            art.color=new Color(1,1,1,.115f);art.raycastTarget=false;
            art.rectTransform.localRotation=Quaternion.Euler(0,0,angle);
            MenuMotion.Float(art.rectTransform,8,.45f+(x%70)/280);
        }
    }
}

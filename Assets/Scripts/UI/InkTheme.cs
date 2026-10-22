using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    // One theme boundary for legacy artwork references and new menu components.
    // Does not change resource IDs used by sound, saves, rewards or gameplay.
    public static class InkTheme
    {
        public static readonly Color32 Ink=new Color32(41,61,53,255),Muted=new Color32(101,115,104,255),Paper=new Color32(247,245,236,255),Line=new Color32(190,200,181,255),Jade=new Color32(60,102,87,255),Cinnabar=new Color32(173,76,62,255);
        static Texture2D paper,grain,wash;
        static Sprite paperSprite;
        static Texture2D Noise(string name,Color32 basis,int variation)
        {
            var t=new Texture2D(128,128,TextureFormat.RGBA32,false);t.name=name;
            var pixels=new Color32[128*128];var random=new System.Random(2431);
            for(int i=0;i<pixels.Length;i++){int n=random.Next(-variation,variation+1);if(name=="Ink pigment grain")n+=(int)((Mathf.PerlinNoise((i%128)/23f,(i/128)/23f)-.5f)*90);pixels[i]=new Color32((byte)Mathf.Clamp(basis.r+n,0,255),(byte)Mathf.Clamp(basis.g+n,0,255),(byte)Mathf.Clamp(basis.b+n,0,255),255);}
            t.SetPixels32(pixels);t.Apply(false,true);t.wrapMode=TextureWrapMode.Repeat;t.filterMode=FilterMode.Bilinear;return t;
        }
        public static Texture2D Texture(string key)
        {
            if(key=="game_assets_homebg"||key=="game_assets_bg"||key=="game_assets_shopbg"||key=="game_assets_paper"||key=="source_loading_background"||key=="shell_src_ui_gameplaysplash")
            {if(!paper)paper=Noise("Ink xuan paper",Paper,1);return paper;}
            if(key=="game_assets_wood"){if(!grain)grain=Noise("Ink board wash",new Color32(166,182,157,255),9);return grain;}
            if(key.StartsWith("game_assets_facets")){if(!wash)wash=Noise("Ink pigment grain",new Color32(232,238,225,255),15);return wash;}
            return null;
        }
        public static Sprite Sprite(string key)
        {
            var texture=Texture(key);
            if(texture&&texture==paper){if(!paperSprite)paperSprite=UnityEngine.Sprite.Create(paper,new Rect(0,0,128,128),Vector2.one*.5f,100);return paperSprite;}
            string icon=null;
            if(key.Contains("avatars_avatar_")){int value;int.TryParse(key.Substring(key.LastIndexOf('_')+1),out value);icon=new[]{"ink_stamp","menu_pen","menu_lens","menu_achievement_medallion"}[value%4];}
            else if(key=="shell_src_ui_tutorialhand")icon="menu_pen";
            else if(key=="shell_src_ui_defaultparticle")icon="ink_bloom";
            else if(key.Contains("nav_tabhome"))icon="ink_stamp";
            else if(key.Contains("nav_tabshop"))icon="menu_pen";
            else if(key.Contains("nav_tabrank"))icon="menu_achievement_medallion";
            else if(key.Contains("settings"))icon="ink_settings";
            else if(key.Contains("pause"))icon="ink_pause";
            else if(key.Contains("heartbroken"))icon="ink_heartbroken";
            else if(key.Contains("heart"))icon="ink_heart";
            else if(key.Contains("coin"))icon="ink_coin";
            else if(key.Contains("close"))icon="ink_close";
            else if(key.Contains("infobtn"))icon="ink_info";
            else if(key.Contains("plus"))icon="ink_plus";
            else if(key.Contains("greenbase"))icon="ink_seal";
            else if(key.Contains("lock"))icon="ink_lock";
            else if(key.Contains("dailyreward")||key.Contains("chest"))icon="menu_treasure";
            else if(key.Contains("soundicon"))icon="ink_sound";
            else if(key.Contains("musicicon"))icon="ink_music";
            else if(key.Contains("hapticsicon"))icon="ink_haptics";
            else if(key.Contains("rewardedvideo"))icon="ink_video";
            else if(key=="game_assets_pen")icon="menu_pen";
            else if(key=="game_assets_lens")icon="menu_lens";
            else if(key=="game_assets_freeze"||key=="shell_src_ui_freeze")icon="menu_freeze";
            else if(key.Contains("extratime"))icon="ink_time";
            else if(key.Contains("panelbadge"))icon="ink_seal";
            return icon==null?null:MenuUI.AtlasSprite(icon);
        }
        public static RectTransform Brand(Transform parent,Vector2 anchor,Vector2 position,Vector2 size)
        {
            var r=Skin.Rect("Ink game brand",parent,anchor,position,size);
            MenuUI.Label("我就划两下",r,Vector2.one*.5f,new Vector2(-30,15),size-new Vector2(125,0),118,Ink,TextAnchor.MiddleCenter,true);
            var seal=Skin.Rect("Ink brand seal",r,new Vector2(1,.5f),new Vector2(-55,-12),new Vector2(72,112));
            Skin.RoundBox(seal,Vector2.one*.5f,Vector2.zero,seal.sizeDelta,Cinnabar,4);
            MenuUI.Label("益\n趣",seal,Vector2.one*.5f,Vector2.zero,new Vector2(65,112),42,Paper,TextAnchor.MiddleCenter,true);
            return r;
        }
    }
}

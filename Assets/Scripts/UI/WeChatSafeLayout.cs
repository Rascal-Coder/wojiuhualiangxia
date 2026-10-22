using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Inkomino
{
    // WeChat's capsule is an overlay, not part of Screen.safeArea. Native
    // metrics stay in CSS/window coordinates until converted to render pixels.
    public static class WeChatSafeLayout
    {
        [Serializable] public sealed class Metrics
        {
            public bool ok,hasSafeArea,hasMenu;
            public float width,height,left,top,right,bottom,menuBottom;
        }
        public const float MenuGap=8;
        public static bool Ready {get;private set;}
#if WEIXINMINIGAME && !UNITY_EDITOR
        static float nextRead;
        static Metrics cached;
        [DllImport("__Internal")] static extern string InkominoReadLayoutMetrics();
#endif
#if UNITY_EDITOR || UNITY_STANDALONE
        public static Metrics QaMetrics;
#endif
        public static void Enable(){Ready=true;Invalidate();}
        public static void Invalidate(){
#if WEIXINMINIGAME && !UNITY_EDITOR
            nextRead=0;
#endif
        }
        static Metrics Read()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if(QaMetrics!=null)return QaMetrics;
#endif
#if WEIXINMINIGAME && !UNITY_EDITOR
            if(Ready&&Time.unscaledTime>=nextRead)
            {
                nextRead=Time.unscaledTime+1;
                try{cached=JsonUtility.FromJson<Metrics>(InkominoReadLayoutMetrics());}
                catch(Exception){cached=null;}
            }
            if(Ready)return cached;
#endif
            return null;
        }
        public static float ReservedTopFraction()
        {return ReservedTopPixels(Read(),1);}
        public static float ReservedTopPixels(Metrics metrics,int renderHeight)
        {
            if(metrics==null||!metrics.ok||metrics.width<=0||metrics.height<=0||renderHeight<1)return 0;
            float top=metrics.hasSafeArea?metrics.top:0;
            if(metrics.hasMenu&&metrics.menuBottom>0&&metrics.menuBottom<metrics.height*.35f)top=Mathf.Max(top,metrics.menuBottom+MenuGap);
            return Mathf.Clamp(top,0,metrics.height*.35f)/metrics.height*renderHeight;
        }
    }

}

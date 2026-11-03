using UnityEngine;

namespace Inkomino
{
    // Commercial press/haptic/audio feedback. Never owns layout, hit boxes,
    // rewards or save clocks. Haptics respect the existing settings flag.
    public static class Feel
    {
        public enum Kind { Tap, Soft, Success, Reject, Celebrate, Warn }

        const float TapGap=.045f,SoftGap=.08f;
        static float nextTap,nextSoft;
        public static Kind LastKind { get; private set; }
        public static int PulseCount { get; private set; }

        public static bool HapticsEnabled
        {
            get
            {
                var app=InkominoApp.Instance;
                return app&&app.Store!=null&&app.Store.Data!=null&&app.Store.Data.haptics;
            }
        }

        public static void Tap()
        {
            if(Time.unscaledTime<nextTap)return;
            nextTap=Time.unscaledTime+TapGap;
            Play(Kind.Tap,true);
        }
        public static void Soft()
        {
            if(Time.unscaledTime<nextSoft)return;
            nextSoft=Time.unscaledTime+SoftGap;
            Play(Kind.Soft,false);
        }
        public static void Success(){Play(Kind.Success,false);}
        public static void Reject(){Play(Kind.Reject,false);}
        public static void Celebrate(){Play(Kind.Celebrate,false);}
        public static void Warn(){Play(Kind.Warn,false);}
        // Settings just turned haptics on; confirm even if a tap already fired.
        public static void ConfirmHaptics(){Vibrate("medium",true);LastKind=Kind.Success;PulseCount++;}

        static void Play(Kind kind,bool click)
        {
            LastKind=kind;PulseCount++;
            if(click)SoundBank.Play("click");
            switch(kind)
            {
                case Kind.Celebrate:
                case Kind.Warn:Vibrate("heavy",true);break;
                case Kind.Success:Vibrate("medium",false);break;
                case Kind.Reject:Vibrate("medium",false);break;
                default:Vibrate("light",false);break;
            }
        }

        static void Vibrate(string type,bool heavy)
        {
            if(!HapticsEnabled)return;
#if WEIXINMINIGAME && !UNITY_EDITOR
            try
            {
                if(heavy)WeChatWASM.WX.VibrateLong(new WeChatWASM.VibrateLongOption());
                else WeChatWASM.WX.VibrateShort(new WeChatWASM.VibrateShortOption{type=type});
            }
            catch(System.Exception error){Debug.LogWarning("触感反馈暂不可用："+error.Message);}
#elif (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if(heavy&&Application.isMobilePlatform)Handheld.Vibrate();
#else
            _=type;_=heavy;
#endif
        }
    }
}

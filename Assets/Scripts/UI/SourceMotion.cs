using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    // Mirrors RN's monotonic animation clock. Manual mode is used only by the frame-audit runner.
    public static class SourceClock
    {
        public static bool Manual; public static float ManualTime,ManualDelta;
        public static float Now { get { return Manual?ManualTime:Time.unscaledTime; } }
        public static float Delta { get { return Manual?ManualDelta:Time.unscaledDeltaTime; } }
        public static void Step(float dt){ManualDelta=dt;ManualTime+=dt;}
        public static IEnumerator Wait(float seconds){float start=Now;while(Now-start<seconds)yield return null;}
    }
    public static class SourceMotion
    {
        public static float OutCubic(float t){t=Mathf.Clamp01(t);return 1-Mathf.Pow(1-t,3);}
        public static float OutQuad(float t){t=Mathf.Clamp01(t);return 1-(1-t)*(1-t);}
        public static float InOutQuad(float t){t=Mathf.Clamp01(t);return t<.5f?2*t*t:1-Mathf.Pow(-2*t+2,2)/2;}
        public static float Bezier(float x,float x1,float y1,float x2,float y2){float lo=0,hi=1,t=x;for(int i=0;i<18;i++){float u=1-t;float bx=3*u*u*t*x1+3*u*t*t*x2+t*t*t;if(bx<x)lo=t;else hi=t;t=(lo+hi)*.5f;}float v=1-t;return 3*v*v*t*y1+3*v*t*t*y2+t*t*t;}
        // Easing.inOut(Easing.ease), Animated.timing's source default.
        public static float DefaultTiming(float t){t=Mathf.Clamp01(t);return t<.5f?Bezier(t*2,.42f,0,1,1)/2:1-Bezier((1-t)*2,.42f,0,1,1)/2;}
        public static float Spring(float elapsed,float from,float to,float tension=60,float friction=8){
            float k=(tension-30)*3.62f+194,d=(friction-8)*3+25,z=d/2,w2=k-z*z;
            if(w2>0){float w=Mathf.Sqrt(w2);return to+(from-to)*Mathf.Exp(-z*elapsed)*(Mathf.Cos(w*elapsed)+z/w*Mathf.Sin(w*elapsed));}
            if(Mathf.Abs(w2)<.0001f)return to+(from-to)*(1+z*elapsed)*Mathf.Exp(-z*elapsed);
            float root=Mathf.Sqrt(-w2),r1=-z+root,r2=-z-root,c1=-(from-to)*r2/(r1-r2),c2=from-to-c1;return to+c1*Mathf.Exp(r1*elapsed)+c2*Mathf.Exp(r2*elapsed);
        }
        public static IEnumerator Tween(float duration,Action<float> apply,Func<float,float> ease=null){float begin=SourceClock.Now;apply(0);while(SourceClock.Now-begin<duration){float t=Mathf.Clamp01((SourceClock.Now-begin)/duration);apply(ease==null?t:ease(t));yield return null;}apply(1);}
        public static SourcePopupMotion Popup(RectTransform panel){return panel.gameObject.AddComponent<SourcePopupMotion>();}
        static readonly System.Collections.Generic.List<RectTransform> dismissing=new System.Collections.Generic.List<RectTransform>();
        public static bool HasDismissing { get { return dismissing.Count>0; } }
        public static void Dismiss(RectTransform overlay)
        {
            if(!overlay)return;
            if(!dismissing.Contains(overlay))dismissing.Add(overlay);
            var motion=overlay.GetComponent<SourcePopupDismiss>();
            if(!motion)motion=overlay.gameObject.AddComponent<SourcePopupDismiss>();
            motion.Play();
        }
        public static void CancelDismissals()
        {
            for(int i=dismissing.Count-1;i>=0;i--)if(dismissing[i])UnityEngine.Object.Destroy(dismissing[i].gameObject);
            dismissing.Clear();
        }
        internal static void ForgetDismiss(RectTransform overlay)
        {
            if(overlay)dismissing.Remove(overlay);
        }
    }
    public class SourcePopupMotion:MonoBehaviour
    {
        CanvasGroup group;float begin;public float Progress { get{return Mathf.Clamp01((SourceClock.Now-begin)/.2f);} }
        void Awake(){group=gameObject.AddComponent<CanvasGroup>();begin=SourceClock.Now;if(MenuMotion.Reduced){group.alpha=1;transform.localScale=Vector3.one;enabled=false;return;}group.alpha=0;transform.localScale=Vector3.one*.94f;}
        void Update(){if(MenuMotion.Reduced){group.alpha=1;transform.localScale=Vector3.one;enabled=false;return;}float t=Mathf.Max(0,SourceClock.Now-begin);group.alpha=SourceMotion.DefaultTiming(t/.16f);float u=Mathf.Clamp01(t/.22f);float s=u<.65f?Mathf.Lerp(.94f,1.04f,SourceMotion.OutCubic(u/.65f)):Mathf.Lerp(1.04f,1f,SourceMotion.OutCubic((u-.65f)/.35f));transform.localScale=Vector3.one*(u>=1f?1f:s);if(t>.24f){transform.localScale=Vector3.one;group.alpha=1;enabled=false;}}
    }
    [UnityEngine.Scripting.Preserve]
    public sealed class SourcePopupDismiss:MonoBehaviour
    {
        CanvasGroup group;Transform motion;float begin;bool playing;
        public const float Duration=.16f;
        public void Play()
        {
            group=GetComponent<CanvasGroup>();if(!group)group=gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts=false;group.interactable=false;
            motion=transform.Find("Source popup motion");
            begin=SourceClock.Now;playing=true;enabled=true;
            if(MenuMotion.Reduced){Finish();return;}
        }
        void Update()
        {
            if(!playing)return;
            if(MenuMotion.Reduced){Finish();return;}
            float u=Mathf.Clamp01((SourceClock.Now-begin)/Duration);
            float t=SourceMotion.DefaultTiming(u);
            if(group)group.alpha=1-t;
            if(motion)motion.localScale=Vector3.one*Mathf.Lerp(1,.88f,t);
            if(u>=1)Finish();
        }
        void Finish()
        {
            playing=false;SourceMotion.ForgetDismiss(transform as RectTransform);
            if(gameObject)Destroy(gameObject);
        }
        void OnDisable(){if(playing){playing=false;SourceMotion.ForgetDismiss(transform as RectTransform);}}
    }
    public static class FidelityCapture
    {
        // Render the real uGUI canvas at the source phone resolution, independent of the desktop's window limits.
        public static void Save(string path,int width=1080,int height=2400){
            var camera=UnityEngine.Object.FindObjectOfType<Camera>();var canvas=UnityEngine.Object.FindObjectOfType<Canvas>();var scaler=canvas.GetComponent<CanvasScaler>();
            var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;float scale=canvas.scaleFactor;bool scaleEnabled=scaler.enabled;var app=InkominoApp.Instance;Vector3 stageScale=app.Stage.localScale;
            var target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;scaler.enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;canvas.scaleFactor=1;Canvas.ForceUpdateCanvases();
            app.Stage.sizeDelta=new Vector2(1080,2400);app.Stage.localScale=Vector3.one*Mathf.Min(width/1080f,height/2400f);foreach(var graphic in app.Stage.GetComponentsInChildren<Graphic>())graphic.SetVerticesDirty();Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));System.IO.File.WriteAllBytes(path,image.EncodeToPNG());
            RenderTexture.active=oldActive;camera.targetTexture=oldTarget;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.scaleFactor=scale;scaler.enabled=scaleEnabled;app.Stage.localScale=stageScale;UnityEngine.Object.Destroy(image);target.Release();UnityEngine.Object.Destroy(target);foreach(var graphic in app.Stage.GetComponentsInChildren<Graphic>())graphic.SetVerticesDirty();Canvas.ForceUpdateCanvases();
        }
    }
}

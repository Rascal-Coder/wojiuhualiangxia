using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Inkomino
{
    // Menu-only motion. No input is delayed and no animation owns a page's
    // layout root. ActiveCount counts playback; LiveCount counts enabled probes.
    public static class MenuMotion
    {
        static int activeCount,liveCount;
        public static int ActiveCount { get { return activeCount; } }
        public static int LiveCount { get { return liveCount; } }
        public static bool Reduced
        {
            get
            {
                var app=InkominoApp.Instance;
                return app&&app.Store!=null&&app.Store.Data!=null&&app.Store.Data.reducedMenuMotion;
            }
        }
        internal static void PlaybackChanged(int delta){activeCount=Mathf.Max(0,activeCount+delta);}
        internal static void LifetimeChanged(int delta){liveCount=Mathf.Max(0,liveCount+delta);}
        internal static float EaseOut(float value){value=Mathf.Clamp01(value);float rest=1-value;return 1-rest*rest*rest;}

        public static void EnterPage(RectTransform page)
        {
            if(!page)return;
            // Adapt may be called before or after this entry point. Resolve its
            // dedicated root without ever animating that root itself.
            if(!page.GetComponent<MenuPageLayout>())
                for(int i=0;i<page.childCount;i++)
                {
                    var child=page.GetChild(i) as RectTransform;
                    if(child&&child.GetComponent<MenuPageLayout>()){page=child;break;}
                }
            var motion=page.GetComponent<MenuPageEntrance>();
            if(!motion)motion=page.gameObject.AddComponent<MenuPageEntrance>();
            motion.Play(page);
        }
        public static void Press(Button button)
        {
            if(!button)return;
            var motion=button.GetComponent<MenuPressFeedback>();
            if(!motion)motion=button.gameObject.AddComponent<MenuPressFeedback>();
            motion.Bind(button);
        }
        public static void RevealProgress(RectTransform fill,float targetWidth,float duration=.35f)
        {
            if(!fill)return;
            var motion=fill.GetComponent<MenuProgressReveal>();
            if(!motion)motion=fill.gameObject.AddComponent<MenuProgressReveal>();
            motion.Play(fill,targetWidth,duration);
        }
        public static void Sparkle(RectTransform target,int count=6,bool loop=false)
        {
            if(!target)return;
            SparkleLayer(target).Play(Mathf.Clamp(count,1,12),loop,false);
        }
        public static void Celebrate(RectTransform target)
        {
            if(!target)return;
            SparkleLayer(target).Play(10,false,true);
        }
        static MenuGoldSparkles SparkleLayer(RectTransform target)
        {
            var child=target.Find("Menu gold sparkle layer") as RectTransform;
            if(!child)child=Skin.Fill("Menu gold sparkle layer",target);
            var motion=child.GetComponent<MenuGoldSparkles>();
            if(!motion)motion=child.gameObject.AddComponent<MenuGoldSparkles>();
            return motion;
        }
        public static void Float(RectTransform visual,float amplitude=5,float speed=1.2f)
        {
            if(!visual)return;
            var motion=visual.GetComponent<MenuFloatMotion>();
            if(!motion)motion=visual.gameObject.AddComponent<MenuFloatMotion>();
            motion.Play(visual,amplitude,speed);
        }
    }

    // Counters represent component lifetimes and actual playback separately.
    // Disabled/destroyed pages synchronously unregister and restore their state.
    public abstract class MenuMotionProbe:MonoBehaviour
    {
        bool registered;
        public bool IsAnimating { get;private set; }
        public bool Completed { get;protected set; }
        protected virtual void OnEnable()
        {
            if(!registered){registered=true;MenuMotion.LifetimeChanged(1);}
        }
        protected void Playback(bool value)
        {
            value=value&&isActiveAndEnabled;
            if(IsAnimating==value)return;
            IsAnimating=value;MenuMotion.PlaybackChanged(value?1:-1);
        }
        protected virtual void OnDisable()
        {
            Playback(false);
            if(registered){registered=false;MenuMotion.LifetimeChanged(-1);}
        }
    }

    [UnityEngine.Scripting.Preserve]
    public sealed class MenuPageEntrance:MenuMotionProbe
    {
        struct Item
        {
            public RectTransform rect;
            public CanvasGroup group;
            public Vector2 position;
            public float alpha,delay,initialAlpha;
        }
        Item[] items;
        float elapsed;
        public int AnimatedChildren { get { return items==null?0:items.Length; } }
        public float Progress01 { get;private set; }
        public const float Duration=.22f,MaximumStagger=.08f;
        public void Play(RectTransform page)
        {
            Restore();
            if(!page){Completed=true;return;}
            int count=0;
            for(int i=0;i<page.childCount;i++)if(Suitable(page.GetChild(i) as RectTransform))count++;
            items=new Item[count];int next=0;
            for(int i=0;i<page.childCount;i++)
            {
                var rect=page.GetChild(i) as RectTransform;if(!Suitable(rect))continue;
                var group=rect.GetComponent<CanvasGroup>();if(!group)group=rect.gameObject.AddComponent<CanvasGroup>();
                // Visible controls remain visibly actionable throughout entry.
                // Preserve CanvasGroup input flags and its original alpha.
                bool textOnly=rect.GetComponent<Text>()!=null&&rect.childCount==0;
                items[next]=new Item{rect=rect,group=group,position=rect.anchoredPosition,alpha=group.alpha,
                    delay=count>1?MaximumStagger*next/(count-1):0,initialAlpha=textOnly?0:.18f};
                next++;
            }
            elapsed=0;Progress01=0;Completed=false;enabled=true;
            if(MenuMotion.Reduced||!gameObject.activeInHierarchy||count==0){Finish();return;}
            Apply();Playback(true);
        }
        static bool Suitable(RectTransform rect)
        {
            if(!rect||!rect.gameObject.activeSelf||rect.GetComponent<MenuPageLayout>())return false;
            string name=rect.name;
            if(name=="menu_climb_journey"||name=="Friends rank canvas"||name=="Source navigation"||
                name=="Source background viewport"||name=="Climb motion lifetime"||name=="Menu gold sparkle layer")return false;
            if(rect.GetComponentInChildren<FriendsRankView>(true))return false;
            // An empty motion lifetime node must not receive a CanvasGroup.
            return rect.GetComponentInChildren<Graphic>(true)!=null;
        }
        void Update()
        {
            if(!IsAnimating)return;
            if(MenuMotion.Reduced){Finish();return;}
            elapsed+=Time.unscaledDeltaTime;Progress01=Mathf.Clamp01(elapsed/(Duration+MaximumStagger));
            Apply();if(elapsed>=Duration+MaximumStagger)Finish();
        }
        void Apply()
        {
            if(items==null)return;
            for(int i=0;i<items.Length;i++)
            {
                var item=items[i];if(!item.rect)continue;
                float t=MenuMotion.EaseOut((elapsed-item.delay)/Duration);
                item.rect.anchoredPosition=item.position+Vector2.down*(16*(1-t));
                if(item.group)item.group.alpha=item.alpha*Mathf.Lerp(item.initialAlpha,1,t);
            }
        }
        void Restore()
        {
            if(items==null)return;
            for(int i=0;i<items.Length;i++)
            {
                var item=items[i];if(item.rect)item.rect.anchoredPosition=item.position;
                if(item.group)item.group.alpha=item.alpha;
            }
        }
        void Finish(){Restore();Progress01=1;Completed=true;Playback(false);}
        protected override void OnDisable(){Finish();base.OnDisable();}
    }

    [UnityEngine.Scripting.Preserve]
    public sealed class MenuPressFeedback:MenuMotionProbe,IPointerDownHandler,IPointerUpHandler,IPointerExitHandler,ICancelHandler
    {
        Button button;
        RectTransform visual;
        Vector3 originalScale=Vector3.one;
        int pointerId=int.MinValue;
        float from=1,to=1,current=1,elapsed,duration;
        bool attempted;
        public Button Button { get { return button; } }
        public RectTransform VisualTarget { get { return visual; } }
        public Vector3 OriginalVisualScale { get { return originalScale; } }
        public bool UsesVisualWrapper { get;private set; }
        public bool UnsupportedVisuals { get;private set; }
        public bool IsPressed { get;private set; }
        public float CurrentScale { get { return current; } }
        public void Bind(Button value)
        {
            Restore();button=value;Completed=true;enabled=true;
            // Resolve on the first frame, after callers finish adding labels.
        }
        bool EnsureVisual()
        {
            if(visual)return true;if(attempted||!button)return false;attempted=true;
            var root=button.transform as RectTransform;if(!root)return false;
            var existing=root.Find("Menu press visuals") as RectTransform;
            if(existing){visual=existing;originalScale=visual.localScale;UsesVisualWrapper=true;return true;}
            var graphic=root.GetComponent<Graphic>();
            // Never scale the hit rectangle as a fallback. Root graphics or a
            // layout controller cannot safely be relocated by this helper.
            if((graphic&&graphic.color.a>.001f)||root.GetComponent<LayoutGroup>()||
                root.GetComponent<ContentSizeFitter>()||root.GetComponent<AspectRatioFitter>())
            {UnsupportedVisuals=true;return false;}
            var selectables=root.GetComponentsInChildren<Selectable>(true);
            if(selectables.Length>1){UnsupportedVisuals=true;return false;}
            int childCount=root.childCount;if(childCount==0){UnsupportedVisuals=true;return false;}
            visual=Skin.Fill("Menu press visuals",root);
            // Equal-sized stretch root preserves the children's anchors, clips,
            // SourceOpacityButton artwork groups, and existing sibling ordering.
            for(int i=0;i<childCount;i++)root.GetChild(0).SetParent(visual,false);
            originalScale=visual.localScale;UsesVisualWrapper=true;return true;
        }
        public void OnPointerDown(PointerEventData eventData)
        {
            if(eventData==null||eventData.button!=PointerEventData.InputButton.Left||!Ready()||IsPressed)return;
            if(MenuMotion.Reduced){Restore();return;}
            if(!EnsureVisual())return;
            pointerId=eventData.pointerId;IsPressed=true;Tween(.97f,.08f);
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if(eventData==null||eventData.pointerId!=pointerId)return;
            Release();
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if(eventData==null||eventData.pointerId!=pointerId)return;
            Release();
        }
        public void OnCancel(BaseEventData eventData){Restore();}
        bool Ready(){return isActiveAndEnabled&&button&&button.IsActive()&&button.IsInteractable();}
        void Release()
        {
            pointerId=int.MinValue;IsPressed=false;
            if(!Ready()||MenuMotion.Reduced){Restore();return;}
            Tween(1,.14f);
        }
        void Tween(float value,float seconds)
        {
            to=value;elapsed=0;duration=seconds;Completed=false;Playback(true);
            // Acknowledge the pointer in the event frame without waiting for an
            // animation tick; the remainder of the press settles over 80ms.
            if(value<current){current=Mathf.Lerp(current,value,.28f);Apply();}
            from=current;
        }
        void Update()
        {
            if(!button){Restore();return;}
            if(!attempted)EnsureVisual();
            if(!Ready()||MenuMotion.Reduced){if(IsPressed||IsAnimating||current!=1)Restore();return;}
            if(!IsAnimating)return;
            elapsed+=Time.unscaledDeltaTime;float t=MenuMotion.EaseOut(elapsed/Mathf.Max(.001f,duration));
            current=Mathf.Lerp(from,to,t);Apply();
            if(elapsed>=duration){current=to;Apply();Completed=true;Playback(false);}
        }
        void Apply(){if(visual)visual.localScale=originalScale*current;}
        void Restore()
        {
            pointerId=int.MinValue;IsPressed=false;current=from=to=1;
            if(visual)visual.localScale=originalScale;
            Completed=true;Playback(false);
        }
        void OnApplicationFocus(bool focused){if(!focused)Restore();}
        void OnApplicationPause(bool paused){if(paused)Restore();}
        protected override void OnDisable(){Restore();base.OnDisable();}
    }

    [UnityEngine.Scripting.Preserve]
    public sealed class MenuProgressReveal:MenuMotionProbe
    {
        RectTransform fill;
        float elapsed,duration;
        Vector3 scale=Vector3.one;
        public float TargetWidth { get;private set; }
        public float Progress01 { get;private set; }
        public float Current { get { return TargetWidth*Progress01; } }
        public RectTransform Fill { get { return fill; } }
        public void Play(RectTransform target,float width,float seconds)
        {
            Finish();fill=target;
            if(!fill){Completed=true;return;}
            TargetWidth=float.IsNaN(width)||float.IsInfinity(width)?0:Mathf.Max(0,width);
            duration=float.IsNaN(seconds)||float.IsInfinity(seconds)?.35f:Mathf.Max(0,seconds);
            // Commit the layout width once; all following frames change scale.
            float shift=fill.rect.width*fill.pivot.x;
            var pivot=fill.pivot;pivot.x=0;fill.pivot=pivot;
            fill.anchoredPosition+=Vector2.left*shift;
            fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,TargetWidth);
            scale=fill.localScale;scale.x=1;
            elapsed=0;Progress01=0;Completed=false;enabled=true;
            if(MenuMotion.Reduced||duration<=0||TargetWidth<=0||!gameObject.activeInHierarchy){Finish();return;}
            Apply();Playback(true);
        }
        void Update()
        {
            if(!IsAnimating)return;
            if(!fill||MenuMotion.Reduced){Finish();return;}
            elapsed+=Time.unscaledDeltaTime;Progress01=MenuMotion.EaseOut(elapsed/duration);Apply();
            if(elapsed>=duration)Finish();
        }
        void Apply(){if(fill)fill.localScale=new Vector3(Progress01,scale.y,scale.z);}
        void Finish(){Progress01=1;Completed=true;if(fill)fill.localScale=new Vector3(1,scale.y,scale.z);Playback(false);}
        protected override void OnDisable(){Finish();base.OnDisable();}
    }

    [UnityEngine.Scripting.Preserve]
    public sealed class MenuFloatMotion:MenuMotionProbe
    {
        RectTransform visual;
        Vector2 origin;
        float elapsed,amplitude,speed;
        bool requested;
        public RectTransform VisualTarget { get { return visual; } }
        public Vector2 OriginalPosition { get { return origin; } }
        public float Amplitude { get { return amplitude; } }
        public void Play(RectTransform value,float amount,float rate)
        {
            Restore();visual=value;if(!visual){Completed=true;return;}
            origin=visual.anchoredPosition;amplitude=Mathf.Clamp(amount,0,12);speed=Mathf.Clamp(rate,.1f,3);
            elapsed=0;requested=true;enabled=true;Completed=MenuMotion.Reduced;
            Playback(!MenuMotion.Reduced&&amplitude>0);
        }
        void Update()
        {
            if(!visual||!requested)return;
            if(MenuMotion.Reduced){visual.anchoredPosition=origin;elapsed=0;Completed=true;Playback(false);return;}
            if(amplitude<=0)return;
            Playback(true);Completed=false;elapsed+=Time.unscaledDeltaTime;
            // Rate is radians/second: the default is a calm 5.2-second cycle.
            visual.anchoredPosition=origin+Vector2.up*(Mathf.Sin(elapsed*speed)*amplitude);
        }
        void Restore(){if(visual)visual.anchoredPosition=origin;requested=false;Completed=true;Playback(false);}
        protected override void OnDisable(){Restore();base.OnDisable();}
    }

    [UnityEngine.Scripting.Preserve]
    public sealed class MenuGoldSparkles:MenuMotionProbe
    {
        struct Particle
        {
            public Vector2 origin,velocity;
            public float age,delay,life,size,rotation,spin;
        }
        readonly Particle[] particles=new Particle[12];
        MenuSparkleGraphic graphic;
        RectTransform rect;
        int count;
        uint randomState=915841u;
        float wait,glowTime;
        bool loop,celebration,pendingBurst,requestedRepeat,suspendedForReduced;
        public int ActiveParticles { get;private set; }
        public int PeakParticles { get;private set; }
        public bool Loop { get { return loop; } }
        public bool RequestedRepeat { get { return requestedRepeat; } }
        public int ParticleCapacity { get { return particles.Length; } }
        public void Play(int amount,bool repeat,bool celebrate)
        {
            StopImmediate();rect=transform as RectTransform;
            graphic=GetComponent<MenuSparkleGraphic>();if(!graphic)graphic=gameObject.AddComponent<MenuSparkleGraphic>();
            graphic.Owner=this;graphic.raycastTarget=false;
            count=Mathf.Clamp(amount,1,12);requestedRepeat=repeat&&!celebrate;loop=requestedRepeat;celebration=celebrate;PeakParticles=0;
            pendingBurst=true;wait=0;glowTime=0;Completed=false;enabled=true;
            if(!gameObject.activeInHierarchy){StopImmediate();return;}
            if(MenuMotion.Reduced){SuspendForReduced();return;}
            Playback(true);
        }
        float Random01()
        {
            randomState^=randomState<<13;randomState^=randomState>>17;randomState^=randomState<<5;
            return (randomState&0x00ffffff)/16777215f;
        }
        void Burst()
        {
            pendingBurst=false;glowTime=0;
            var size=rect.rect.size;
            float rx=Mathf.Max(14,size.x*.4f),ry=Mathf.Max(12,size.y*.32f);
            for(int i=0;i<count;i++)
            {
                float angle=(i+(Random01()*.5f))*Mathf.PI*2/count;
                var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                float radius=celebration?.34f:.65f+Random01()*.35f;
                particles[i]=new Particle{origin=new Vector2(direction.x*rx,direction.y*ry)*radius,
                    velocity=direction*(celebration?24:8)+Vector2.up*(9+Random01()*9),age=0,delay=i*.018f,
                    life=.54f+Random01()*.18f,size=4+Random01()*(celebration?7:5),rotation=Random01()*Mathf.PI,
                    spin=(Random01()-.5f)*1.8f};
            }
            ActiveParticles=count;PeakParticles=Mathf.Max(PeakParticles,count);graphic.SetVerticesDirty();
        }
        void Update()
        {
            if(!rect){StopImmediate();return;}
            if(MenuMotion.Reduced)
            {
                if(IsAnimating||pendingBurst||ActiveParticles>0||(requestedRepeat&&!suspendedForReduced))SuspendForReduced();
                return;
            }
            if(!IsAnimating)
            {
                if(!requestedRepeat||!suspendedForReduced)return;
                // A preference change resumes only an explicitly requested
                // loop. Wait for the normal low-frequency interval instead of
                // bursting immediately as the settings overlay is closed.
                suspendedForReduced=false;loop=true;pendingBurst=false;
                wait=2.1f+Random01()*1.6f;Completed=false;Playback(true);
            }
            float dt=Time.unscaledDeltaTime;
            if(pendingBurst)
            {
                // A freshly created stretch RectTransform may not have its
                // parent's final dimensions until layout has run once.
                if(rect.rect.width<=0||rect.rect.height<=0)
                {
                    wait+=dt;if(wait>=.3f)StopImmediate();return;
                }
                wait=0;
                Burst();return;
            }
            if(ActiveParticles==0)
            {
                if(!loop){Completed=true;Playback(false);return;}
                wait-=dt;if(wait<=0)pendingBurst=true;return;
            }
            int active=0;glowTime+=dt;
            for(int i=0;i<count;i++)
            {
                var particle=particles[i];particle.age+=dt;particles[i]=particle;
                if(particle.age<=particle.delay+particle.life)active++;
            }
            ActiveParticles=active;graphic.SetVerticesDirty();
            if(active==0)
            {
                wait=2.1f+Random01()*1.6f;
                if(!loop){Completed=true;Playback(false);}
            }
        }
        internal void Populate(VertexHelper vh)
        {
            vh.Clear();if(!rect||MenuMotion.Reduced||ActiveParticles==0)return;
            if(celebration&&glowTime<.65f)AddGlow(vh,glowTime/.65f);
            for(int i=0;i<count;i++)
            {
                var p=particles[i];float age=p.age-p.delay;if(age<0||age>=p.life)continue;
                float t=age/p.life;
                float alpha=Mathf.Clamp01(age/.09f)*(1-MenuMotion.EaseOut(Mathf.Max(0,(t-.45f)/.55f)));
                float size=p.size*(.65f+Mathf.Sin(t*Mathf.PI)*.55f);
                AddStar(vh,p.origin+p.velocity*age,size,p.rotation+p.spin*age,alpha);
            }
        }
        void AddGlow(VertexHelper vh,float t)
        {
            int start=vh.currentVertCount;
            float radius=Mathf.Max(20,Mathf.Min(rect.rect.width,rect.rect.height)*.45f)*Mathf.Lerp(.65f,1.15f,t);
            byte alpha=(byte)(Mathf.Sin(t*Mathf.PI)*32);
            vh.AddVert(Vector3.zero,new Color32(102,135,116,alpha),Vector2.zero);
            for(int i=0;i<=20;i++)
            {
                float angle=i*Mathf.PI*2/20;
                vh.AddVert(new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,0),new Color32(145,169,146,0),Vector2.zero);
            }
            for(int i=0;i<20;i++)vh.AddTriangle(start,start+i+1,start+i+2);
        }
        static void AddStar(VertexHelper vh,Vector2 center,float radius,float rotation,float alpha)
        {
            int start=vh.currentVertCount;byte a=(byte)(Mathf.Clamp01(alpha)*255);
            vh.AddVert(center,new Color32(173,76,62,a),Vector2.zero);
            for(int i=0;i<8;i++)
            {
                float angle=rotation+i*Mathf.PI/4,length=radius*(i%2==0?1:.72f);
                vh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*length,new Color32(101,132,112,a),Vector2.zero);
            }
            for(int i=0;i<8;i++)vh.AddTriangle(start,start+i+1,start+(i+1)%8+1);
        }
        void StopImmediate()
        {
            requestedRepeat=false;suspendedForReduced=false;
            ActiveParticles=0;loop=false;pendingBurst=false;Completed=true;Playback(false);
            if(graphic)graphic.SetVerticesDirty();
        }
        void SuspendForReduced()
        {
            // Preserve the requested amount/count, but discard the visible
            // burst. A one-shot celebration never becomes a resumable loop.
            suspendedForReduced=requestedRepeat;
            ActiveParticles=0;loop=false;pendingBurst=false;Completed=true;Playback(false);
            if(graphic)graphic.SetVerticesDirty();
        }
        protected override void OnDisable(){StopImmediate();base.OnDisable();}
    }

    [UnityEngine.Scripting.Preserve]
    public sealed class MenuSparkleGraphic:MaskableGraphic
    {
        public MenuGoldSparkles Owner;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if(Owner)Owner.Populate(vh);else vh.Clear();
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    // Screen-space coverage fringe: works with overlay and camera canvases.
    public static class StickerMesh
    {
        public static float Pixel(RectTransform rt)
        {
            var canvas=rt.GetComponentInParent<Canvas>();
            Camera camera=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            var a=RectTransformUtility.WorldToScreenPoint(camera,rt.TransformPoint(Vector3.zero));
            var b=RectTransformUtility.WorldToScreenPoint(camera,rt.TransformPoint(Vector3.right*100));
            var c=RectTransformUtility.WorldToScreenPoint(camera,rt.TransformPoint(Vector3.up*100));
            // A progress reveal collapses X to zero before its first mesh build.
            // Use the non-collapsed axis so cached AA vertices cannot grow across
            // the page when X expands again. Bound fully collapsed transforms too.
            float pixels=Mathf.Max(Vector2.Distance(a,b),Vector2.Distance(a,c));
            return 100/Mathf.Max(6.25f,pixels);
        }
        public static List<Vector2> Rounded(Rect r,float radius)
        {
            float rad=Mathf.Clamp(radius,0,Mathf.Min(r.width,r.height)*.5f);var p=new List<Vector2>();
            for(int c=0;c<4;c++){
                var center=new Vector2(c==0||c==3?r.xMax-rad:r.xMin+rad,c<2?r.yMax-rad:r.yMin+rad);
                for(int k=0;k<=16;k++){float a=(c*90+k*90f/16)*Mathf.Deg2Rad;p.Add(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*rad);}
            }
            return p;
        }
        public static void Solid(VertexHelper vh,IList<Vector2> points,Color color,float aa,Func<Vector2,Vector2> uv=null)
        {
            if(points.Count<3)return;int start=vh.currentVertCount;var p=new List<Vector2>();
            foreach(var v in points)if(p.Count==0||(v-p[p.Count-1]).sqrMagnitude>.00001f)p.Add(v);
            if(p.Count>1&&(p[0]-p[p.Count-1]).sqrMagnitude<.00001f)p.RemoveAt(p.Count-1);
            if(p.Count<3)return;
            foreach(var v in p)vh.AddVert(v,color,uv==null?Vector2.zero:uv(v));
            for(int k=1;k+1<p.Count;k++)vh.AddTriangle(start,start+k,start+k+1);
            float area=0;for(int i=0;i<p.Count;i++)area+=p[i].x*p[(i+1)%p.Count].y-p[(i+1)%p.Count].x*p[i].y;
            Color clear=color;clear.a=0;
            for(int i=0;i<p.Count;i++){
                var a=(p[i]-p[(i+p.Count-1)%p.Count]).normalized;var b=(p[(i+1)%p.Count]-p[i]).normalized;
                var na=new Vector2(a.y,-a.x)*Mathf.Sign(area);var nb=new Vector2(b.y,-b.x)*Mathf.Sign(area);
                var normal=(na+nb).normalized;var v=p[i]+normal*(aa/Mathf.Max(.5f,Vector2.Dot(normal,nb)));
                vh.AddVert(v,clear,uv==null?Vector2.zero:uv(p[i]));
            }
            for(int i=0;i<p.Count;i++){int j=(i+1)%p.Count;vh.AddTriangle(start+i,start+j,start+p.Count+j);vh.AddTriangle(start+i,start+p.Count+j,start+p.Count+i);}
        }
        public static void Outline(VertexHelper vh,IList<Vector2> points,float width,Color color,float aa)
        {
            var p=new List<Vector2>();
            foreach(var v in points)if(p.Count==0||(v-p[p.Count-1]).sqrMagnitude>.00001f)p.Add(v);
            if(p.Count>1&&(p[0]-p[p.Count-1]).sqrMagnitude<.00001f)p.RemoveAt(p.Count-1);
            if(p.Count<3)return;
            int start=vh.currentVertCount;Color clear=color;clear.a=0;
            for(int i=0;i<p.Count;i++){
                var a=(p[i]-p[(i+p.Count-1)%p.Count]).normalized;var b=(p[(i+1)%p.Count]-p[i]).normalized;
                var na=new Vector2(-a.y,a.x);var nb=new Vector2(-b.y,b.x);var normal=(na+nb).normalized;
                if(normal.sqrMagnitude<.01f)normal=nb;
                normal/=Mathf.Max(.35f,Vector2.Dot(normal,nb));
                vh.AddVert(p[i]-normal*(width*.5f+aa),clear,Vector2.zero);
                vh.AddVert(p[i]-normal*width*.5f,color,Vector2.zero);
                vh.AddVert(p[i]+normal*width*.5f,color,Vector2.zero);
                vh.AddVert(p[i]+normal*(width*.5f+aa),clear,Vector2.zero);
            }
            for(int i=0;i<p.Count;i++)for(int k=0;k<3;k++){
                int a=start+i*4+k,b=start+((i+1)%p.Count)*4+k;
                vh.AddTriangle(a,b,b+1);vh.AddTriangle(a,b+1,a+1);
            }
        }
        public static void Band(VertexHelper vh,IList<Vector2> a,IList<Vector2> b,Color color,float aa)
        {
            int start=vh.currentVertCount;Color clear=color;clear.a=0;
            for(int i=0;i<a.Count;i++){
                var d=(a[i]-b[i]).normalized;
                vh.AddVert(a[i]+d*aa,clear,Vector2.zero);vh.AddVert(a[i],color,Vector2.zero);
                vh.AddVert(b[i],color,Vector2.zero);vh.AddVert(b[i]-d*aa,clear,Vector2.zero);
            }
            for(int i=0;i<a.Count;i++)for(int k=0;k<3;k++){int x=start+i*4+k,y=start+((i+1)%a.Count)*4+k;vh.AddTriangle(x,y,y+1);vh.AddTriangle(x,y+1,x+1);}
        }
    }
}

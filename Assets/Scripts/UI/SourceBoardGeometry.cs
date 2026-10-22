using System;
using System.Collections.Generic;
using UnityEngine;

namespace Inkomino
{
    public static class SourceBoardGeometry
    {
        public static List<Vector2> CellPolygon(Rect r,float radius,bool topLeft,bool topRight,bool bottomRight,bool bottomLeft)
        {
            var p=new List<Vector2>();float rad=Mathf.Min(radius,Mathf.Min(r.width,r.height)/2);bool[] rounded={topLeft,topRight,bottomRight,bottomLeft};
            Vector2[] corners={new Vector2(r.xMin,r.yMax),new Vector2(r.xMax,r.yMax),new Vector2(r.xMax,r.yMin),new Vector2(r.xMin,r.yMin)};
            Vector2[] centers={corners[0]+new Vector2(rad,-rad),corners[1]+new Vector2(-rad,-rad),corners[2]+new Vector2(-rad,rad),corners[3]+new Vector2(rad,rad)};
            for(int c=0;c<4;c++){if(!rounded[c]||rad<=0){p.Add(corners[c]);continue;}for(int k=0;k<=8;k++){float a=(180-c*90-k*90f/8)*Mathf.Deg2Rad;p.Add(centers[c]+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*rad);}}return p;
        }
        // Ear clipping handles concave SVG symbols without painting across notches.
        public static List<int> Triangulate(IList<Vector2> p)
        {
            var result=new List<int>();var order=new List<int>();float area=0;for(int i=0;i<p.Count;i++){order.Add(i);Vector2 a=p[i],b=p[(i+1)%p.Count];area+=a.x*b.y-b.x*a.y;}float sign=area>=0?1:-1;int guard=p.Count*p.Count;
            while(order.Count>2&&guard-->0){bool found=false;for(int j=0;j<order.Count;j++){int ia=order[(j+order.Count-1)%order.Count],ib=order[j],ic=order[(j+1)%order.Count];Vector2 a=p[ia],b=p[ib],c=p[ic];if(Cross(b-a,c-b)*sign<=.00001f)continue;bool inside=false;foreach(int k in order)if(k!=ia&&k!=ib&&k!=ic&&Cross(b-a,p[k]-a)*sign>=-.00001f&&Cross(c-b,p[k]-b)*sign>=-.00001f&&Cross(a-c,p[k]-c)*sign>=-.00001f){inside=true;break;}if(inside)continue;result.Add(ia);result.Add(ib);result.Add(ic);order.RemoveAt(j);found=true;break;}if(!found)break;}return result;
        }
        static float Cross(Vector2 a,Vector2 b){return a.x*b.y-a.y*b.x;}
        sealed class Edge { public Vector2Int A,B;public int D;public bool Used; }
        // Original traceLoops right/straight/left priority at shared grid vertices.
        public static List<List<Vector2>> Trace(int n,IEnumerable<int> source)
        {
            var cells=new HashSet<int>(source);var edges=new List<Edge>();var outgoing=new Dictionary<Vector2Int,List<Edge>>();
            Action<int,int,int,int,int> add=(x,y,xx,yy,d)=>{var e=new Edge{A=new Vector2Int(x,y),B=new Vector2Int(xx,yy),D=d};edges.Add(e);List<Edge> list;if(!outgoing.TryGetValue(e.A,out list))outgoing[e.A]=list=new List<Edge>();list.Add(e);};
            var sorted=new List<int>(cells);sorted.Sort();
            foreach(int i in sorted){int r=i/n,c=i%n;if(r==0||!cells.Contains(i-n))add(c,r,c+1,r,0);if(c==n-1||!cells.Contains(i+1))add(c+1,r,c+1,r+1,1);if(r==n-1||!cells.Contains(i+n))add(c+1,r+1,c,r+1,2);if(c==0||!cells.Contains(i-1))add(c,r+1,c,r,3);}
            var loops=new List<List<Vector2>>();
            foreach(var first in edges)
            {
                if(first.Used)continue;var walked=new List<Edge>();Edge current=first;
                while(current!=null&&!current.Used)
                {
                    current.Used=true;walked.Add(current);List<Edge> list;Edge next=null;
                    if(outgoing.TryGetValue(current.B,out list))foreach(int d in new[]{(current.D+1)%4,current.D,(current.D+3)%4})
                    {foreach(var e in list)if(!e.Used&&e.D==d){next=e;break;}if(next!=null)break;}
                    current=next;
                }
                var loop=new List<Vector2>();for(int i=0;i<walked.Count;i++)if(walked[(i+walked.Count-1)%walked.Count].D!=walked[i].D)loop.Add(walked[i].A);
                if(loop.Count>=4)loops.Add(loop);
            }
            return loops;
        }
        public static List<List<Vector2>> Paths(int n,IEnumerable<int> cells,float cell,Vector2 topLeft,float inset,float radius)
        {
            var paths=new List<List<Vector2>>();
            foreach(var grid in Trace(n,cells))
            {
                var points=new List<Vector2>();
                for(int i=0;i<grid.Count;i++)
                {
                    Vector2 a=grid[(i+grid.Count-1)%grid.Count],p=grid[i],b=grid[(i+1)%grid.Count];
                    Vector2 incoming=new Vector2(p.x-a.x,a.y-p.y).normalized,outgoing=new Vector2(b.x-p.x,p.y-b.y).normalized;
                    Vector2 na=new Vector2(incoming.y,-incoming.x),nb=new Vector2(outgoing.y,-outgoing.x);
                    Vector2 v=topLeft+new Vector2(p.x*cell,-p.y*cell)+(na+nb)*inset;
                    float radiusHere=incoming.x*outgoing.y-incoming.y*outgoing.x<0?Mathf.Max(0,radius-inset):0;
                    if(radiusHere<.5f){points.Add(v);continue;}
                    Vector2 start=v-incoming*radiusHere,center=start+outgoing*radiusHere,radial=start-center;
                    // Samples of the source circular SVG quarter-arc; concave corners stay sharp.
                    for(int k=0;k<=8;k++){float angle=-Mathf.PI*.5f*k/8;points.Add(center+new Vector2(radial.x*Mathf.Cos(angle)-radial.y*Mathf.Sin(angle),radial.x*Mathf.Sin(angle)+radial.y*Mathf.Cos(angle)));}
                }
                paths.Add(points);
            }
            return paths;
        }
        public static float Length(IList<Vector2> p,bool closed=true){float length=0;for(int i=1;i<p.Count;i++)length+=Vector2.Distance(p[i-1],p[i]);if(closed&&p.Count>1)length+=Vector2.Distance(p[p.Count-1],p[0]);return length;}
        public static List<Vector2> Prefix(IList<Vector2> p,float progress)
        {
            var result=new List<Vector2>();if(p.Count==0||progress<=0)return result;result.Add(p[0]);float remaining=Length(p)*Mathf.Clamp01(progress);
            for(int i=0;i<p.Count&&remaining>0;i++){Vector2 a=p[i],b=p[(i+1)%p.Count];float length=Vector2.Distance(a,b);if(length<.0001f)continue;if(remaining<length){result.Add(Vector2.Lerp(a,b,remaining/length));break;}result.Add(b);remaining-=length;}return result;
        }
        public static List<Vector2> ClipRect(IList<Vector2> input,Rect r)
        {
            var p=new List<Vector2>(input);for(int edge=0;edge<4&&p.Count>0;edge++)
            {var clip=new List<Vector2>();Vector2 previous=p[p.Count-1];bool prev=Inside(previous,edge,r);foreach(Vector2 current in p){bool inside=Inside(current,edge,r);if(inside!=prev)clip.Add(Intersection(previous,current,edge,r));if(inside)clip.Add(current);previous=current;prev=inside;}p=clip;}return p;
        }
        public static List<Vector2> ClipConvex(IList<Vector2> input,IList<Vector2> boundary)
        {
            var p=new List<Vector2>(input);float area=0;for(int i=0;i<boundary.Count;i++)area+=Cross(boundary[i],boundary[(i+1)%boundary.Count]);float sign=area>=0?1:-1;
            for(int i=0;i<boundary.Count&&p.Count>0;i++){var clip=new List<Vector2>();Vector2 a=boundary[i],edge=boundary[(i+1)%boundary.Count]-a,previous=p[p.Count-1];bool prev=Cross(edge,previous-a)*sign>=-.001f;foreach(Vector2 current in p){bool inside=Cross(edge,current-a)*sign>=-.001f;if(inside!=prev){Vector2 d=current-previous;float denominator=Cross(edge,d);float t=Mathf.Abs(denominator)<.000001f?0:Cross(edge,a-previous)/denominator;clip.Add(previous+d*t);}if(inside)clip.Add(current);previous=current;prev=inside;}p=clip;}return p;
        }
        static bool Inside(Vector2 p,int e,Rect r){return e==0?p.x>=r.xMin:e==1?p.x<=r.xMax:e==2?p.y>=r.yMin:p.y<=r.yMax;}
        static Vector2 Intersection(Vector2 a,Vector2 b,int e,Rect r)
        {if(e<2){float x=e==0?r.xMin:r.xMax;float t=Mathf.Abs(b.x-a.x)<.000001f?0:(x-a.x)/(b.x-a.x);return new Vector2(x,Mathf.LerpUnclamped(a.y,b.y,t));}float y=e==2?r.yMin:r.yMax;float u=Mathf.Abs(b.y-a.y)<.000001f?0:(y-a.y)/(b.y-a.y);return new Vector2(Mathf.LerpUnclamped(a.x,b.x,u),y);}
    }
}

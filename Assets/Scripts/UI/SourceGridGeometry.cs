using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    public static class SourceGridGeometry
    {
        // Diagnostic switch permits a real A/B capture of the former thin-quad
        // rasterization and the fix, with identical board/layout/dash parameters.
        public static bool CoverageAntialiasing=true;
        public static void Draw(VertexHelper vh,RectTransform target,PuzzleBoardView view)
        {
            var state=view.State;int n=state.Level.n;float cs=view.CellSize,dp=view.PixelDensity;
            float dash=Mathf.Clamp(Mathf.Floor(cs/dp*.12f+.5f),5,9)*dp,gap=Mathf.Clamp(Mathf.Floor(cs/dp*.05f+.5f),2,4)*dp;
            Vector2 origin=view.BoardOriginLocal;Color ink=new Color32(94,85,75,130);
            Vector2 Map(Vector2 p){return target.InverseTransformPoint(view.transform.TransformPoint(p));}
            for(int i=0;i<n*n;i++)if(state.IsActive(i))foreach(int j in state.Neighbors(i))
            {
                if(j<=i||!state.IsActive(j)||state.Wall(i,j)!=0)continue;
                Rect cell=new Rect(origin.x+i%n*cs,origin.y-(i/n+1)*cs,cs,cs);
                Vector2 a=j==i+1?new Vector2(cell.xMax,cell.yMin):new Vector2(cell.xMin,cell.yMin),b=j==i+1?new Vector2(cell.xMax,cell.yMax):new Vector2(cell.xMax,cell.yMin),d=(b-a).normalized;
                int count=Mathf.FloorToInt((cs-6*dp+gap)/(dash+gap));float offset=(cs-(count*dash+Mathf.Max(0,count-1)*gap))/2;
                for(int k=0;k<count;k++)Line(vh,target,Map(a+d*(offset+k*(dash+gap))),Map(a+d*(offset+k*(dash+gap)+dash)),.9f*dp,ink);
            }
        }
        static void Line(VertexHelper vh,RectTransform target,Vector2 a,Vector2 b,float width,Color ink)
        {
            var canvas=target.GetComponentInParent<Canvas>();Camera camera=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            Vector2 pa=RectTransformUtility.WorldToScreenPoint(camera,target.TransformPoint(a)),pb=RectTransformUtility.WorldToScreenPoint(camera,target.TransformPoint(b));
            Vector2 normal=new Vector2(-(b-a).y,(b-a).x).normalized;
            float physicalWidth=Vector2.Distance(pa,RectTransformUtility.WorldToScreenPoint(camera,target.TransformPoint(a+normal*width)));
            if(!CoverageAntialiasing||physicalWidth>=1.25f||physicalWidth<.0001f){var m=normal*width/2;Quad(vh,a-m,a+m,b+m,b-m,ink);return;}
            // A subpixel solid quad can miss every pixel centre. Distribute its
            // exact perpendicular coverage over pixel-aligned one-pixel bands.
            // This keeps both geometric width and integrated opacity: no global
            // thickening or invented extra cell boundaries.
            bool vertical=Mathf.Abs(pb.y-pa.y)>Mathf.Abs(pb.x-pa.x);float center=vertical?(pa.x+pb.x)/2:(pa.y+pb.y)/2;
            float start=center-physicalWidth/2,end=center+physicalWidth/2;
            Vector2 Local(Vector2 p){Vector2 v;RectTransformUtility.ScreenPointToLocalPointInRectangle(target,p,camera,out v);return v;}
            for(int pixel=Mathf.FloorToInt(start);pixel<Mathf.CeilToInt(end);pixel++)
            {
                float coverage=Mathf.Max(0,Mathf.Min(end,pixel+1)-Mathf.Max(start,pixel));if(coverage<=0)continue;
                Color color=ink;color.a*=coverage;
                if(vertical){float lo=Mathf.Min(pa.y,pb.y),hi=Mathf.Max(pa.y,pb.y);Quad(vh,Local(new Vector2(pixel,lo)),Local(new Vector2(pixel,hi)),Local(new Vector2(pixel+1,hi)),Local(new Vector2(pixel+1,lo)),color);}
                else {float lo=Mathf.Min(pa.x,pb.x),hi=Mathf.Max(pa.x,pb.x);Quad(vh,Local(new Vector2(lo,pixel)),Local(new Vector2(lo,pixel+1)),Local(new Vector2(hi,pixel+1)),Local(new Vector2(hi,pixel)),color);}
            }
        }
        static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {int k=vh.currentVertCount;vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);vh.AddVert(c,color,Vector2.zero);vh.AddVert(d,color,Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);}
    }
}

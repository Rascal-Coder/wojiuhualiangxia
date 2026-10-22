#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Inkomino
{
    // Image-based regression: examine every eligible internal boundary in the
    // real renderer, rather than checking that a line mesh was merely emitted.
    public sealed class GridQA:MonoBehaviour
    {
        [Serializable] public class Edge {public int a,b,markedPixels,samples;public float visibleFraction;public bool missing;}
        [Serializable] public class Frame {public string file;public int level,width,height,internalEdges,missingEdges;public bool coverageAA;public float physicalLineWidth;public List<Edge> edges=new List<Edge>();}
        [Serializable] public class Report {public string status="RUNNING",engine;public int scannedLevels,scanInternalEdges,scanMissingEdges;public List<Frame> frames=new List<Frame>();public List<Frame> scanFrames=new List<Frame>();public List<string> errors=new List<string>();public string method="Real RenderTexture capture with MSAA disabled to match the overlay UI raster path; on every legal internal cell boundary, scan the middle70% for ink darkening relative to adjacent paper. All500 levels are rendered at540x836; passing scan images are transient, while failing images and edge details are retained. This checks visibility, not original-app pixel identity.";}
        InkominoApp app;Report report;string dir;
        IEnumerator Start()
        {
            dir=InkominoApp.QAOutput();Directory.CreateDirectory(dir);app=InkominoApp.Instance;report=new Report{engine=Application.unityVersion};app.StopAllCoroutines();app.enabled=false;app.SourceTutorialEnabled=false;QualitySettings.antiAliasing=0;FindObjectOfType<Camera>().allowMSAA=false;SourceClock.Manual=true;SourceClock.ManualTime=0;
            Application.logMessageReceived+=OnLog;
            app.Store.Data=new PlayerProfile{level=5,hearts=5,drawIntroSeen=true};
            app.StartLevel(5,true);yield return null;yield return null;
            SourceGridGeometry.CoverageAntialiasing=false;yield return Capture("level05-before-540x836.png",540,836,false);
            SourceGridGeometry.CoverageAntialiasing=true;yield return Capture("level05-after-540x836.png",540,836,true);
            foreach(var size in new[]{new Vector2Int(540,1200),new Vector2Int(360,800),new Vector2Int(1080,2400)})yield return Capture("level05-after-"+size.x+"x"+size.y+".png",size.x,size.y,true);
            foreach(int level in new[]{1,11,23,47,500}){app.StartLevel(level,true);yield return null;yield return Capture("level"+level.ToString("000")+"-540x836.png",540,836,true);}
            for(int level=1;level<=500;level++){app.StartLevel(level,true);yield return null;yield return Capture("scan-current.png",540,836,true,true);if(level%25==0){Write();Debug.Log("INKOMINO_GRID_SCAN levels="+level+" missing="+report.scanMissingEdges);}}
            if(report.scannedLevels!=500)report.errors.Add("Expected500 scanned levels, got"+report.scannedLevels);
            if(report.frames[0].missingEdges==0)report.errors.Add("Original missing-line issue was not reproduced by the baseline at540x836.");
            report.status=report.errors.Count==0?"PASS":"FAIL";Write();Debug.Log("INKOMINO_GRID_QA "+report.status+" images="+report.frames.Count);Application.Quit(report.errors.Count==0?0:1);
        }
        IEnumerator Capture(string name,int width,int height,bool requireComplete,bool scan=false)
        {
            yield return new WaitForEndOfFrame();string path=Path.Combine(dir,name);FidelityCapture.Save(path,width,height);
            var image=new Texture2D(2,2,TextureFormat.RGB24,false);image.LoadImage(File.ReadAllBytes(path));Color32[] pixels=image.GetPixels32();
            var frame=new Frame{file=name,level=app.CurrentLevel,width=width,height=height,coverageAA=SourceGridGeometry.CoverageAntialiasing,physicalLineWidth=.9f*app.BoardView.PixelDensity*Mathf.Min(width/1080f,height/2400f)};
            float scale=Mathf.Min(width/1080f,height/2400f);var view=app.BoardView;int n=app.Board.Level.n;
            Vector2 Project(Vector2 local){Vector2 stage=app.Stage.InverseTransformPoint(view.transform.TransformPoint(local));return new Vector2(width/2f,height/2f)+stage*scale;}
            float Luma(Vector2 p){int x=Mathf.Clamp(Mathf.FloorToInt(p.x),0,width-1),y=Mathf.Clamp(Mathf.FloorToInt(p.y),0,height-1);var c=pixels[y*width+x];return c.r*.2126f+c.g*.7152f+c.b*.0722f;}
            for(int i=0;i<n*n;i++)if(app.Board.IsActive(i))foreach(int j in app.Board.Neighbors(i))
            {
                if(j<=i||!app.Board.IsActive(j)||app.Board.Wall(i,j)!=0)continue;
                Vector2 center=(view.CellCenterLocal(i)+view.CellCenterLocal(j))/2;bool vertical=j==i+1;Vector2 along=vertical?Vector2.up:Vector2.right;
                Vector2 a=Project(center-along*view.CellSize*.35f),b=Project(center+along*view.CellSize*.35f),direction=(b-a).normalized,normal=new Vector2(-direction.y,direction.x);
                var edge=new Edge{a=i,b=j};int steps=Mathf.Max(1,Mathf.FloorToInt(Vector2.Distance(a,b)));
                for(int k=0;k<=steps;k++){Vector2 p=Vector2.Lerp(a,b,k/(float)steps);float ink=Mathf.Min(Luma(p-normal),Luma(p),Luma(p+normal));float paper=(Luma(p-normal*3)+Luma(p+normal*3)+Luma(p-normal*4)+Luma(p+normal*4))/4;if(paper-ink>12)edge.markedPixels++;edge.samples++;}
                edge.visibleFraction=edge.markedPixels/(float)edge.samples;edge.missing=edge.visibleFraction<.10f;frame.edges.Add(edge);
            }
            frame.internalEdges=frame.edges.Count;frame.missingEdges=frame.edges.Count(x=>x.missing);Destroy(image);
            if(scan){report.scannedLevels++;report.scanInternalEdges+=frame.internalEdges;report.scanMissingEdges+=frame.missingEdges;if(frame.missingEdges>0){frame.file="scan-failed-level"+frame.level.ToString("000")+".png";File.Copy(path,Path.Combine(dir,frame.file),true);}else{frame.file="";frame.edges.Clear();}report.scanFrames.Add(frame);File.Delete(path);}else report.frames.Add(frame);
            if(requireComplete&&frame.missingEdges>0)report.errors.Add("Level"+frame.level+" "+name+": "+frame.missingEdges+" of"+frame.internalEdges+" boundaries have no readable dashed line.");if(!scan)Write();
        }
        void Write(){File.WriteAllText(Path.Combine(dir,"grid-validation.json"),JsonUtility.ToJson(report,true));}
        void OnLog(string message,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)report.errors.Add("Level"+app.CurrentLevel+" renderer: "+message);}
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}

#endif

using System;
using System.Collections.Generic;
using System.Linq;

namespace Inkomino
{
    [Serializable] public struct Cell { public int r,c; public Cell(int row,int col){r=row;c=col;} }
    [Serializable] public class ShapeData { public string id,color; public Cell[] cells; }
    [Serializable] public class WallData { public int[] e,s; }
    [Serializable] public class PieceData { public string shape; public int[] cells; public bool locked,tutLock,appearanceSaved;public int colorIndex,textureIndex;public float textureOffsetX,textureOffsetY; public PieceData(){} public PieceData(string s,IEnumerable<int> c){shape=s;cells=c.OrderBy(x=>x).ToArray();} }
    [Serializable] public class LevelData { public int id,n,time,seed; public bool authored; public string name,difficulty,design; public string[] mask,targets; public int[] blocked,icons,nums; public WallData walls; public PieceData[] solution; }
    [Serializable] public class GameData { public LevelData[] levels; public ShapeData[] shapes;
        public void Normalize(){foreach(var l in levels){int length=l.n*l.n;if(l.blocked==null||l.blocked.Length!=length){l.blocked=new int[length];if(l.mask!=null&&l.mask.Length==l.n)for(int i=0;i<length;i++)l.blocked[i]="Xx1#".IndexOf(l.mask[i/l.n][i%l.n])>=0?0:1;}if(l.walls!=null&&(l.walls.e==null||l.walls.s==null||l.walls.e.Length!=length||l.walls.s.Length!=length))l.walls=null;if(l.icons==null||l.icons.Length!=length)l.icons=null;if(l.nums==null||l.nums.Length!=length)l.nums=null;}}
    }
    public sealed class ShapeLibrary
    {
        public readonly Dictionary<string,ShapeData> Shapes;
        readonly Dictionary<string,List<Cell[]>> rotations=new Dictionary<string,List<Cell[]>>();
        public ShapeLibrary(IEnumerable<ShapeData> shapes){Shapes=shapes.ToDictionary(x=>x.id);foreach(var s in Shapes.Values)rotations[s.id]=Orientations(s.cells);}
        public List<Cell[]> GetOrientations(string id){return rotations[id];}
        public static string Key(IEnumerable<Cell> cells){var a=cells.ToArray();if(a.Length==0)return "";int r=a.Min(x=>x.r),c=a.Min(x=>x.c);return string.Join(";",a.Select(x=>new Cell(x.r-r,x.c-c)).OrderBy(x=>x.r).ThenBy(x=>x.c).Select(x=>x.r+","+x.c));}
        public string Match(LevelData l,IEnumerable<int> cells){string k=Key(cells.Select(i=>new Cell(i/l.n,i%l.n)));foreach(string id in l.targets)if(rotations[id].Any(o=>Key(o)==k))return id;return null;}
        public static List<Cell[]> Orientations(Cell[] cells){var list=new List<Cell[]>();var keys=new HashSet<string>();for(int mirror=0;mirror<2;mirror++){var a=cells.Select(x=>new Cell(x.r,mirror==0?x.c:-x.c)).ToArray();for(int turn=0;turn<4;turn++){int r=a.Min(x=>x.r),c=a.Min(x=>x.c);var norm=a.Select(x=>new Cell(x.r-r,x.c-c)).OrderBy(x=>x.r).ThenBy(x=>x.c).ToArray();if(keys.Add(Key(norm)))list.Add(norm);a=a.Select(x=>new Cell(x.c,-x.r)).ToArray();}}return list;}
    }
    public sealed class BoardState
    {
        public readonly LevelData Level; public readonly ShapeLibrary Library;
        public readonly List<PieceData> Pieces=new List<PieceData>();
        public event Action Changed;
        public int ActiveCount{get{return Level.blocked.Count(x=>x==0);}}
        public int Filled{get{return Pieces.Sum(x=>x.cells.Length);}}
        public bool Solved{get{return Filled==ActiveCount&&Level.targets.All(t=>Pieces.Any(p=>p.shape==t));}}
        public BoardState(LevelData level,ShapeLibrary library){Level=level;Library=library;}
        public PieceData At(int i){return Pieces.FirstOrDefault(p=>Array.IndexOf(p.cells,i)>=0);}
        public bool IsActive(int i){return i>=0&&i<Level.n*Level.n&&Level.blocked[i]==0;}
        public int Wall(int a,int b){if(Level.walls==null||Level.walls.e==null||Level.walls.s==null)return 0;int n=Level.n;if(a/n==b/n&&Math.Abs(a-b)==1)return Level.walls.e[Math.Min(a,b)];if(a%n==b%n&&Math.Abs(a-b)==n)return Level.walls.s[Math.Min(a,b)];return 0;}
        public IEnumerable<int> Neighbors(int i){int n=Level.n;if(i/n>0)yield return i-n;if(i/n<n-1)yield return i+n;if(i%n>0)yield return i-1;if(i%n<n-1)yield return i+1;}
        public bool CanPlace(IEnumerable<int> selected,out string shape,out string reason){
            var a=selected.ToArray();shape=null;reason="";var set=new HashSet<int>(a);
            if(a.Length==0){reason=Zh.DrawShape;return false;}
            if(set.Count!=a.Length||a.Any(i=>!IsActive(i)||At(i)!=null)){reason=Zh.SpaceFilled;return false;}
            var visit=new HashSet<int>();var q=new Queue<int>();q.Enqueue(a[0]);visit.Add(a[0]);while(q.Count>0){int i=q.Dequeue();foreach(int j in Neighbors(i))if(set.Contains(j)&&visit.Add(j))q.Enqueue(j);}
            if(visit.Count!=a.Length){reason=Zh.ConnectSquares;return false;}
            shape=Library.Match(Level,a);if(shape==null){reason=Zh.MatchTarget;return false;}
            foreach(int i in a)foreach(int j in Neighbors(i)){int w=Wall(i,j);if(w==0)continue;if(set.Contains(j)){reason=Zh.NoCrossWall;return false;}var other=At(j);if(other!=null&&((w==2&&other.shape!=shape)||(w==3&&other.shape==shape))){reason=w==2?Zh.SameAtWall:Zh.DifferentAtWall;return false;}}
            if(Level.icons!=null){var kinds=Level.icons.Where(x=>x>0).Distinct();foreach(int k in kinds)if(a.Count(i=>Level.icons[i]==k)!=1){reason=Zh.EverySymbol;return false;}}
            if(Level.nums!=null&&a.Any(i=>Level.nums[i]>0&&Level.nums[i]!=a.Length)){reason=Zh.NumberArea;return false;}
            return true;
        }
        public bool TryPlace(IEnumerable<int> cells,out string reason){string shape;var a=cells.ToArray();if(!CanPlace(a,out shape,out reason))return false;Pieces.Add(new PieceData(shape,a));Changed?.Invoke();return true;}
        public void Remove(PieceData p){if(p!=null&&Pieces.Remove(p))Changed?.Invoke();}
        public void Clear(){Pieces.Clear();Changed?.Invoke();}
        public void Notify(){Changed?.Invoke();}
        public bool Restore(IEnumerable<PieceData> pieces){var saved=pieces.ToArray();Pieces.Clear();foreach(var p in saved){string s,r;if(!CanPlace(p.cells,out s,out r)||s!=p.shape){Pieces.Clear();Changed?.Invoke();return false;}Pieces.Add(new PieceData(p.shape,p.cells){locked=p.locked,tutLock=p.tutLock,appearanceSaved=p.appearanceSaved,colorIndex=p.colorIndex,textureIndex=p.textureIndex,textureOffsetX=p.textureOffsetX,textureOffsetY=p.textureOffsetY});}Changed?.Invoke();return true;}
        public List<PieceData> Solve(int budget=6000){
            var b=new BoardState(Level,Library);b.Pieces.AddRange(Pieces.Select(p=>new PieceData(p.shape,p.cells)));
            // Prefer the exported, verified continuation; fall back to constraint search after alternative player placements.
            if(Level.solution!=null){var remaining=Level.solution.Where(p=>!b.Pieces.Any(x=>x.shape==p.shape&&x.cells.SequenceEqual(p.cells))).ToArray();bool ok=true;foreach(var p in remaining){string reason;if(!b.TryPlace(p.cells,out reason)){ok=false;break;}}if(ok&&b.Solved)return remaining.ToList();}
            b.Pieces.Clear();b.Pieces.AddRange(Pieces.Select(p=>new PieceData(p.shape,p.cells)));int keep=b.Pieces.Count,n=Level.n,nodes=0;
            Func<bool> search=null;search=()=>{if(++nodes>budget)return false;int first=-1;for(int i=0;i<n*n;i++)if(b.IsActive(i)&&b.At(i)==null){first=i;break;}if(first<0)return b.Solved;
                // Missing targets first reduces end states that tile the board but omit a target.
                foreach(string id in Level.targets.OrderBy(t=>b.Pieces.Any(p=>p.shape==t)?1:0))foreach(var o in Library.GetOrientations(id))foreach(var anchor in o){int rr=first/n-anchor.r,cc=first%n-anchor.c;var cells=new int[o.Length];bool bounds=true;for(int k=0;k<o.Length;k++){int r=rr+o[k].r,c=cc+o[k].c;if(r<0||c<0||r>=n||c>=n){bounds=false;break;}cells[k]=r*n+c;}if(!bounds)continue;string shape,reason;if(!b.CanPlace(cells,out shape,out reason)||shape!=id)continue;b.Pieces.Add(new PieceData(id,cells));if(search())return true;b.Pieces.RemoveAt(b.Pieces.Count-1);if(nodes>budget)return false;}return false;};
            return search()?b.Pieces.Skip(keep).ToList():null;
        }
    }
}

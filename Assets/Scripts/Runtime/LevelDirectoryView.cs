using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    // Paging changes only labels and callbacks. The modal and its motion stay alive.
    public sealed class LevelDirectoryView:MonoBehaviour
    {
        public int Page{get;private set;}
        public readonly Button[] Cells=new Button[25];
        Text pageLabel;Button previous,next;InkominoApp app;
        public void Init(InkominoApp owner,int page)
        {
            app=owner;var panel=(RectTransform)transform;
            MenuUI.Label(Zh.LibraryDescription,panel,new Vector2(.5f,1),new Vector2(0,-250),new Vector2(900,90),38,MenuUI.Muted,TextAnchor.MiddleCenter);
            for(int i=0;i<Cells.Length;i++){
                int slot=i;
                Cells[i]=MenuUI.SecondaryButton("",panel,new Vector2(.5f,.5f),new Vector2((i%5-2)*174,390-(i/5)*190),new Vector2(164,164),()=>app.StartLevel(Page*25+slot+1,true),48);
                Cells[i].name="Directory level "+i;
            }
            previous=MenuUI.SecondaryButton("‹",panel,new Vector2(.5f,0),new Vector2(-290,155),new Vector2(145,150),()=>SetPage(Page-1),64);previous.name="Directory previous";
            pageLabel=MenuUI.Label("",panel,new Vector2(.5f,0),new Vector2(0,155),new Vector2(350,100),50,MenuUI.Ink,TextAnchor.MiddleCenter);
            next=MenuUI.SecondaryButton("›",panel,new Vector2(.5f,0),new Vector2(290,155),new Vector2(145,150),()=>SetPage(Page+1),64);next.name="Directory next";
            SetPage(page);
        }
        public void SetPage(int page)
        {
            Page=Mathf.Clamp(page,0,19);
            for(int i=0;i<Cells.Length;i++){
                int id=Page*25+i+1;var label=Cells[i].GetComponentInChildren<Text>();label.font=MenuUI.NumberFont;label.text=id.ToString();
                label.color=app.Store.Data.completed[id-1]?MenuUI.Green:MenuUI.Ink;
            }
            pageLabel.text=(Page+1)+" / 20";previous.interactable=Page>0;next.interactable=Page<19;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Inkomino
{
    public sealed class ClimbPageMotion:MonoBehaviour
    {
        sealed class Item {public RectTransform rect;public Vector2 position;public float amount,phase;}
        readonly List<Item> items=new List<Item>();public Action Tick;float next;
        public void Track(RectTransform rect,float amount,float phase){items.Add(new Item{rect=rect,position=rect.anchoredPosition,amount=amount,phase=phase});}
        void Update(){float now=Time.unscaledTime;foreach(var item in items)if(item.rect)item.rect.anchoredPosition=item.position+Vector2.up*(Mathf.Sin(now*1.5f+item.phase)*item.amount);if(now>=next){next=now+1;Tick?.Invoke();}}
    }
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Inkomino
{
    public partial class InkominoApp
    {
        public CloudSaveSync CloudSave{get;private set;}
        bool cloudRefreshAfterStartup;
        internal bool ShouldAwaitInitialCloud{get{return Store==null||!Store.HasLocalProfile;}}
        internal IEnumerator InitializeCloudSaveTask(IEnumerator initialization,Action<IEnumerator> startBackground=null)
        {
            // Returning players can use their durable local record immediately.
            // A new installation waits once so an existing cloud record can be
            // restored before the first board starts.
            if(ShouldAwaitInitialCloud)yield return initialization;
            else if(startBackground!=null)startBackground(initialization);
            else StartCoroutine(initialization);
        }
        void CloudSaveRemoteApplied()
        {
            if(!BootReady){cloudRefreshAfterStartup=true;return;}
            if(IsHomeScreen&&!Playing&&!TransitioningLevel&&!IsModalOpen){ShowHome();FriendsRanking.SubmitProgress(Store.Data.wins);}
            else UpdateWallet();
        }
        void CompleteCloudSaveStartup()
        {
            if(!BootReady||!cloudRefreshAfterStartup)return;
            cloudRefreshAfterStartup=false;
            // The data may arrive after the first home view was built but
            // before its loading cover finishes fading out.
            if(IsHomeScreen&&!Playing&&!TransitioningLevel&&!IsModalOpen)ShowHome();
            else UpdateWallet();
        }
        IEnumerator InitializeCloudSave()
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            CloudSave=gameObject.AddComponent<CloudSaveSync>();
            CloudSave.RemoteApplied+=CloudSaveRemoteApplied;
            yield return InitializeCloudSaveTask(CloudSave.Initialize(this));
#else
            yield break;
#endif
        }
        string CloudSaveStatusDetail()
        {return string.IsNullOrEmpty(CloudSave.Detail)?"自动使用最新的游戏记录":CloudSave.Detail;}
        void ShowCloudSaveStatus()
        {
            if(!CloudSave)return;
            var panel=Popup("云存档",920,880);
            var label=MenuUI.Label(CloudSave.Status,panel,new Vector2(.5f,1),new Vector2(0,-330),new Vector2(800,100),48,MenuUI.Ink,TextAnchor.MiddleCenter);
            var detail=MenuUI.Label(CloudSaveStatusDetail(),panel,new Vector2(.5f,1),new Vector2(0,-490),new Vector2(800,180),42,MenuUI.Muted,TextAnchor.MiddleCenter);
            var retry=MenuUI.GameButton("立即同步",panel,new Vector2(.5f,0),new Vector2(0,160),new Vector2(680,160),()=>CloudSave.Retry(),52);
            retry.interactable=!CloudSave.Busy;
            StartCoroutine(CloudStatusLabel(panel,label,detail,retry));
        }
        IEnumerator CloudStatusLabel(RectTransform panel,Text label,Text detail,Button retry)
        {
            while(CloudSave&&panel&&panel.gameObject.activeInHierarchy&&label&&detail){
                label.text=CloudSave.Status;detail.text=CloudSaveStatusDetail();if(retry)retry.interactable=!CloudSave.Busy;
                yield return new WaitForSecondsRealtime(.25f);
            }
        }
    }
}

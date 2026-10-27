using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Inkomino
{
    [Serializable] public sealed class CloudSaveConfig
    { public bool enabled;public string endpoint="",environmentId=""; }
    [Serializable] public sealed class CloudSaveLocalState
    {
        public string identity="",baseProfile="",operationId="",pendingProfile="";
        public long revision,localSavedAt,baseSavedAt,pendingSavedAt,clockOffset;public bool dirty;
    }
    [Serializable] public sealed class CloudSaveReply
    {
        public bool ok,exists;public string code,message,token,identity,lastOperationId,updatedAt;
        public long revision,savedAt,serverTime;public PlayerProfile profile;
    }
    [Serializable] sealed class CloudSaveRequest
    { public string action,code,token,operationId;public long revision,savedAt;public PlayerProfile profile; }

    // One writer per device. A pending operation is durable before sending;
    // retries reuse it, and the server compares the expected cloud revision.
    public sealed class CloudSaveSync:MonoBehaviour
    {
        const string MetaKey="inkomino-cloud-state-v1",BackupKey="inkomino-cloud-backup-v1",RemoteBackupKey="inkomino-cloud-remote-backup-v1";
        InkominoApp app;CloudSaveConfig config;CloudSaveLocalState state;
        string token,authenticatedIdentity,observedProfile;bool ready,busy,suppress,metadataAvailable=true,started,reconnect;
        bool networkKnown,networkConnected=true;
#if WEIXINMINIGAME && !UNITY_EDITOR
        Action<WeChatWASM.OnNetworkStatusChangeListenerResult> networkListener;
#endif
        float nextAttempt,firstDirty,deferredAt;CloudSaveReply deferred;
        public string Status{get;private set;}="尚未连接";
        public string LastSynced{get;private set;}="";
        public string Detail{get;private set;}="";
        string failureCode="";
        public bool Configured{get{return config!=null&&config.enabled&&!string.IsNullOrEmpty(config.endpoint);}}
        public bool Busy{get{return busy;}}
        public bool HasDeferred{get{return deferred!=null;}}
        public long LocalSavedAt{get{return state==null?0:state.localSavedAt;}}
        public event Action RemoteApplied;
        public IEnumerator Initialize(InkominoApp owner)
        {
            app=owner;var asset=Resources.Load<TextAsset>("Settings/cloud-save");
            config=asset?JsonUtility.FromJson<CloudSaveConfig>(asset.text):new CloudSaveConfig();
            if(!Configured||app.IsQA)yield break;
            Uri uri;
            if(!Uri.TryCreate(config.endpoint,UriKind.Absolute,out uri)||uri.Scheme!="https") {Status="云存档地址尚未配置";yield break;}
            try {var json=Read(MetaKey);state=string.IsNullOrEmpty(json)?new CloudSaveLocalState():JsonUtility.FromJson<CloudSaveLocalState>(json);if(state==null)throw new Exception();}
            catch {metadataAvailable=false;Status="云存档记录读取失败，本机进度已保留";yield break;}
            observedProfile=Snapshot();app.Store.Persisted+=LocalChanged;started=true;
            WatchNetwork();
            yield return Connect();
        }
        void OnDestroy()
        {
            if(app!=null&&app.Store!=null)app.Store.Persisted-=LocalChanged;
#if WEIXINMINIGAME && !UNITY_EDITOR
            if(networkListener!=null)WeChatWASM.WX.OffNetworkStatusChange(networkListener);
#endif
        }
        void WatchNetwork()
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            networkListener=result=>{if(this)NetworkChanged(result.isConnected);};
            WeChatWASM.WX.OnNetworkStatusChange(networkListener);
            WeChatWASM.WX.GetNetworkType(new WeChatWASM.GetNetworkTypeOption{
                success=result=>{if(this&&!networkKnown)NetworkChanged(result.networkType!="none");}
            });
#endif
        }
        public void NetworkChanged(bool connected)
        {
            bool known=networkKnown,wasConnected=networkConnected;
            networkKnown=true;networkConnected=connected;
            if(!started||!metadataAvailable)return;
            if(!connected){ready=false;Status="进度已保存在本机";return;}
            // Ignore duplicate online notifications. An initial online result
            // need not restart the login that is already in flight.
            if((known&&wasConnected)||(!known&&busy))return;
            Retry();
        }
        string Snapshot(){return JsonUtility.ToJson(app.Store.Data);}
        void LocalChanged()
        {
            if(suppress||!started||!metadataAvailable)return;
            long now=SaveStore.Now;var baseline=CloudSaveComparison.Parse(state.baseProfile);
            var previous=CloudSaveComparison.Parse(observedProfile)??baseline;
            // Changing an avatar or device preferences must not make older
            // gameplay win over a newer record from another device.
            if(previous==null||!CloudSaveComparison.GameplayEqual(app.Store.Data,previous,now))state.localSavedAt=ClockNow;
            observedProfile=Snapshot();bool wasDirty=state.dirty;
            state.dirty=!string.IsNullOrEmpty(state.pendingProfile)||baseline==null||!CloudSaveComparison.SyncEqual(app.Store.Data,baseline,now);
            if(state.dirty&&!wasDirty)firstDirty=Time.unscaledTime;
            Persist();if(state.dirty)nextAttempt=Mathf.Min(Time.unscaledTime+2,firstDirty+12);
            if(deferred==null)Status=state.dirty?"等待同步":ready?"已同步":Status;
        }
        bool Persist()
        {
            try{Write(MetaKey,JsonUtility.ToJson(state));return true;}
            catch{metadataAvailable=false;ready=false;Status="本机进度已保存，云同步暂不可用";return false;}
        }
        void Update()
        {
            if(!started||!metadataAvailable||busy||(networkKnown&&!networkConnected)||Time.unscaledTime<nextAttempt)return;
            if(deferred!=null){TryApplyDeferred();return;}
            if(!ready||reconnect)StartCoroutine(Connect());else if(state.dirty||!string.IsNullOrEmpty(state.pendingProfile))StartCoroutine(Upload());
        }
        public void Flush(){if(started)nextAttempt=0;}
        public void Retry(){if(!started)return;reconnect=true;nextAttempt=0;}
        public void Foreground(){Retry();}
        IEnumerator Connect()
        {
            if(busy||!metadataAvailable||(networkKnown&&!networkConnected))yield break;
            busy=true;ready=false;reconnect=false;Status="正在连接云存档";Detail="";failureCode="";
            string code="";
#if WEIXINMINIGAME && !UNITY_EDITOR
            bool done=false;
            WeChatWASM.WX.Login(new WeChatWASM.LoginOption{success=result=>{code=result.code;done=true;},fail=result=>{done=true;}});
            float until=Time.unscaledTime+8;while(!done&&Time.unscaledTime<until&&(!networkKnown||networkConnected))yield return null;
#endif
            if(string.IsNullOrEmpty(code)||(networkKnown&&!networkConnected)){failureCode="WX_LOGIN";Offline();busy=false;yield break;}
            CloudSaveReply reply=null;yield return Send(new CloudSaveRequest{action="login",code=code},value=>reply=value);
            if(reply==null||!reply.ok||string.IsNullOrEmpty(reply.token)||string.IsNullOrEmpty(reply.identity)){Offline();busy=false;yield break;}
            token=reply.token;authenticatedIdentity=reply.identity;ready=true;Detail="";
            Reconcile(reply);busy=false;
            if(ready&&deferred==null&&metadataAvailable&&(state.dirty||!string.IsNullOrEmpty(state.pendingProfile)))nextAttempt=0;
        }
        static string Json(PlayerProfile p){return p==null?"":JsonUtility.ToJson(p);}
        static bool Fresh(PlayerProfile p)
        {
            return CloudSaveComparison.GameplayEqual(p,new PlayerProfile(),SaveStore.Now);
        }
        void Reconcile(CloudSaveReply reply)
        {
            CalibrateClock(reply);ReconcileLatest(reply);
        }
        void ReconcileLatest(CloudSaveReply reply)
        {
            if(reply.exists&&!SaveStore.ValidCloudProfile(reply.profile)){ready=false;Status="云端存档格式异常，本机进度已保留";nextAttempt=Time.unscaledTime+60;return;}
            deferred=null;long now=SaveStore.Now;
            string cloud=Json(reply.profile);bool sameOwner=state.identity==reply.identity;
            // A verified account switch restores that account, never uploads
            // the previous account's wallet, even when its visible level matches.
            if(!sameOwner&&!string.IsNullOrEmpty(state.identity)){RestoreWhenSafe(reply);return;}
            if(sameOwner&&reply.exists&&reply.revision<state.revision){ready=false;Status="云端记录版本异常，本机进度已保留";nextAttempt=Time.unscaledTime+60;return;}
            if(!reply.exists){
                state.identity=reply.identity;state.revision=0;state.baseProfile="";state.baseSavedAt=0;ClearPending();state.dirty=true;if(Persist())Status="等待首次备份";return;
            }
            var baseline=CloudSaveComparison.Parse(state.baseProfile);
            if(sameOwner&&state.operationId==reply.lastOperationId&&!string.IsNullOrEmpty(state.pendingProfile)
                &&CloudSaveComparison.SyncEqual(CloudSaveComparison.Parse(state.pendingProfile),reply.profile,now)){
                Acknowledge(reply,cloud);return;
            }
            if(CloudSaveComparison.GameplayEqual(app.Store.Data,reply.profile,now)){
                // Appearance changes never change the gameplay clock. Preserve an
                // independently edited local avatar, otherwise follow the cloud.
                AdoptUneditedCloudAvatar(reply,baseline,sameOwner);
                Acknowledge(reply,cloud);return;
            }
            if(!app.BootReady&&Fresh(app.Store.Data)){RestoreWhenSafe(reply);return;}
            if(sameOwner&&baseline!=null){
                if(CloudSaveComparison.GameplayEqual(app.Store.Data,baseline,now)){RestoreWhenSafe(reply);return;}
                if(CloudSaveComparison.GameplayEqual(reply.profile,baseline,now)){
                    AdoptUneditedCloudAvatar(reply,baseline,true);
                    KeepLatestLocal(reply,cloud,false);return;
                }
            }
            long clock=ClockNow;
            long localTime=BoundedTime(state.localSavedAt,clock),remoteTime=RemoteTime(reply,clock);
            // Equal or unknown clocks have one stable winner, preventing devices
            // from repeatedly replacing each other. Never combine wallet maxima.
            if(localTime>0&&remoteTime>0&&localTime>remoteTime)KeepLatestLocal(reply,cloud,true);
            else RestoreWhenSafe(reply);
        }
        static long BoundedTime(long value,long now){return value<=0?0:Math.Min(value,now);}
        long ClockNow{get{return Math.Max(0,SaveStore.Now+state.clockOffset);}}
        void CalibrateClock(CloudSaveReply reply){if(reply.serverTime>0)state.clockOffset=reply.serverTime-SaveStore.Now;}
        static long RemoteTime(CloudSaveReply reply,long now)
        {
            if(reply.savedAt>0)return BoundedTime(reply.savedAt,now);
            DateTimeOffset timestamp;
            return DateTimeOffset.TryParse(reply.updatedAt,out timestamp)?BoundedTime(timestamp.ToUnixTimeSeconds(),now):0;
        }
        void ClearPending(){state.operationId="";state.pendingProfile="";state.pendingSavedAt=0;}
        void KeepLatestLocal(CloudSaveReply reply,string cloud,bool backup)
        {
            if(backup&&!Backup(reply)){ready=false;nextAttempt=Time.unscaledTime+30;return;}
            state.revision=reply.revision;state.baseProfile=cloud;state.baseSavedAt=RemoteTime(reply,ClockNow);ClearPending();
            state.identity=reply.identity;state.dirty=true;if(Persist()){Status="等待同步";nextAttempt=0;}
        }
        void AdoptUneditedCloudAvatar(CloudSaveReply reply,PlayerProfile baseline,bool sameOwner)
        {
            bool locallyEdited=sameOwner&&baseline!=null&&app.Store.Data.avatar!=baseline.avatar;
            if(locallyEdited||app.Store.Data.avatar==reply.profile.avatar)return;
            suppress=true;try{app.Store.Data.avatar=reply.profile.avatar;app.Store.Save();}finally{suppress=false;}
            observedProfile=Snapshot();
        }
        bool UnsafeToRestore{get{return app.Playing||app.TransitioningLevel||app.AnimationBusy||app.IsModalOpen||(app.BootReady&&!app.IsHomeScreen);}}
        void RestoreWhenSafe(CloudSaveReply reply)
        {
            if(UnsafeToRestore){deferred=reply;deferredAt=Time.unscaledTime;Status="等待同步";nextAttempt=0;return;}
            ApplyRemote(reply);
        }
        void TryApplyDeferred()
        {
            if(deferred==null||busy||UnsafeToRestore)return;
            // Do not apply an old background snapshot after a long play session.
            if(reconnect||Time.unscaledTime-deferredAt>30){deferred=null;ready=false;nextAttempt=0;return;}
            var latest=deferred;deferred=null;ReconcileLatest(latest);
        }
        void Acknowledge(CloudSaveReply reply,string snapshot)
        {
            state.identity=string.IsNullOrEmpty(reply.identity)?state.identity:reply.identity;
            state.revision=reply.revision;state.baseProfile=snapshot;state.baseSavedAt=RemoteTime(reply,ClockNow);ClearPending();
            state.dirty=!CloudSaveComparison.SyncEqual(app.Store.Data,CloudSaveComparison.Parse(snapshot),SaveStore.Now);
            if(!state.dirty)state.localSavedAt=state.baseSavedAt;
            if(!Persist())return;LastSynced=reply.updatedAt;Status=state.dirty?"等待同步":"已同步";
        }
        bool Backup(CloudSaveReply reply)
        {try{Write(BackupKey,Snapshot());if(reply!=null&&reply.exists)Write(RemoteBackupKey,Json(reply.profile));return true;}catch{Status="备份失败，本机进度已保留";return false;}}
        bool ApplyRemote(CloudSaveReply reply,bool preserveEditedAvatar=true,bool notify=true)
        {
            if(!Backup(reply)){ready=false;nextAttempt=Time.unscaledTime+30;return false;}
            var restored=reply.exists?CloudSaveComparison.Parse(Json(reply.profile)):new PlayerProfile();
            CloudSaveComparison.KeepDevicePreferences(app.Store.Data,restored);
            var baseline=CloudSaveComparison.Parse(state.baseProfile);
            if(preserveEditedAvatar&&state.identity==reply.identity&&baseline!=null&&app.Store.Data.avatar!=baseline.avatar)restored.avatar=app.Store.Data.avatar;
            suppress=true;bool applied=false;
            try {applied=app.Store.ApplyCloudProfile(restored);}finally{suppress=false;}
            if(!applied){ready=false;Status="同步暂不可用，进度已保存";nextAttempt=Time.unscaledTime+30;return false;}
            observedProfile=Snapshot();
            state.localSavedAt=reply.exists?RemoteTime(reply,ClockNow):0;
            Acknowledge(reply,reply.exists?Json(reply.profile):"");SoundBank.Settings(app.Store.Data.sound,app.Store.Data.music);
            if(notify)RemoteApplied?.Invoke();
            return true;
        }
        IEnumerator Upload()
        {
            if(busy||!ready||deferred!=null||!metadataAvailable||(networkKnown&&!networkConnected)||state.identity!=authenticatedIdentity)yield break;
            busy=true;Status="正在保存云存档";
            if(string.IsNullOrEmpty(state.pendingProfile)){
                state.pendingProfile=Snapshot();state.pendingSavedAt=state.localSavedAt;state.operationId=Guid.NewGuid().ToString();if(!Persist()){busy=false;yield break;}
            }
            string sent=state.pendingProfile;
            var request=new CloudSaveRequest{action="save",token=token,revision=state.revision,operationId=state.operationId,savedAt=state.pendingSavedAt,profile=JsonUtility.FromJson<PlayerProfile>(sent)};
            CloudSaveReply reply=null;yield return Send(request,value=>reply=value);
            HandleUploadReply(reply,sent);
            busy=false;
        }
        void HandleUploadReply(CloudSaveReply reply,string sent)
        {
            if(reply!=null&&reply.ok){CalibrateClock(reply);Acknowledge(reply,sent);nextAttempt=reconnect?0:Time.unscaledTime+2;}
            else if(reply!=null&&reply.code=="REVISION_CONFLICT"&&(!reply.exists||SaveStore.ValidCloudProfile(reply.profile))){
                // Save replies carry no identity; bind them to this verified login.
                if(string.IsNullOrEmpty(authenticatedIdentity)){Offline();return;}
                reply.identity=authenticatedIdentity;Reconcile(reply);
            }
            else if(reply!=null&&reply.code=="INVALID_PROFILE"){ready=false;nextAttempt=float.PositiveInfinity;Status="本机进度已保留，云端校验未通过";}
            else Offline();
        }
        void Offline(){ready=false;Status="进度已保存在本机";Detail=networkKnown&&!networkConnected?"暂未备份到云端\n连接恢复后会自动重试":failureCode=="LOGIN_FAILED"||failureCode=="WX_LOGIN"?"微信登录暂未成功\n稍后会自动重试备份":failureCode=="NOT_CONFIGURED"?"云存档暂不可用\n本机进度可以继续游玩":"暂未备份到云端\n连接恢复后会自动重试";nextAttempt=reconnect&&(!networkKnown||networkConnected)?0:Time.unscaledTime+30;Debug.LogWarning("INKOMINO_CLOUD_SYNC deferred reason="+failureCode);}
        IEnumerator Send(CloudSaveRequest data,Action<CloudSaveReply> completed)
        {
            CloudSaveReply reply=null;
            using(var request=new UnityWebRequest(config.endpoint,"POST")){
                request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(data)));
                request.downloadHandler=new DownloadHandlerBuffer();request.SetRequestHeader("Content-Type","application/json");request.timeout=10;
                yield return request.SendWebRequest();
                if(request.responseCode>=200&&request.responseCode<=599){try{reply=JsonUtility.FromJson<CloudSaveReply>(request.downloadHandler.text);}catch{}}
                failureCode=reply!=null&&!string.IsNullOrEmpty(reply.code)?reply.code:request.result==UnityWebRequest.Result.Success?"INVALID_RESPONSE":"HTTP_"+request.responseCode;
                if(reply==null||!reply.ok)Debug.LogWarning("INKOMINO_CLOUD_REQUEST action="+data.action+" reason="+failureCode+" transport="+request.result+" detail="+request.error);
            }
            completed(reply);
        }
        string Read(string key)
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            return WeChatProfileStorage.Read(key);
#else
            string path=app.Store.Path+"."+key+".json";return File.Exists(path)?File.ReadAllText(path):"";
#endif
        }
        void Write(string key,string value)
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            WeChatProfileStorage.Write(key,value);
#else
            string path=app.Store.Path+"."+key+".json",temp=path+".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(temp,value);
            if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
#endif
        }
    }
}

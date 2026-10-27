#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Inkomino
{
    // Real automatic reconciliation and durable writes, with isolated profiles.
    // No credentials, remote environment, HTTP calls or currency max merge.
    public static class CloudSaveQA
    {
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        const string MetaSuffix=".inkomino-cloud-state-v1.json";
        const string BackupSuffix=".inkomino-cloud-backup-v1.json";
        static void Set(object target,string name,object value){target.GetType().GetField(name,Flags).SetValue(target,value);}
        static T Get<T>(object target,string name){return (T)target.GetType().GetField(name,Flags).GetValue(target);}
        static object Call(object target,string name,params object[] args){return target.GetType().GetMethod(name,Flags).Invoke(target,args);}
        static string Json(PlayerProfile value){return JsonUtility.ToJson(value);}
        static PlayerProfile Copy(PlayerProfile value){return JsonUtility.FromJson<PlayerProfile>(Json(value));}
        static PlayerProfile Profile(int level,int coins)
        {
            var p=new PlayerProfile{level=level,wins=level-1,coins=coins,drawIntroSeen=level>1,regenAt=SaveStore.Now+900};
            for(int i=0;i<level-1;i++)p.completed[i]=true;
            ClimbActivity.Refresh(p,SaveStore.Now);return p;
        }
        static CloudSaveReply Reply(PlayerProfile profile,long revision=5,string identity="account-a",long savedAt=0)
        {return new CloudSaveReply{ok=true,exists=profile!=null,identity=identity,revision=revision,profile=profile,savedAt=savedAt,updatedAt=""};}
        static CloudSaveLocalState DurableState(InkominoApp app)
        {return JsonUtility.FromJson<CloudSaveLocalState>(File.ReadAllText(app.Store.Path+MetaSuffix));}
        static bool Wallet(PlayerProfile p,int coins,int hint,int freeze=3,int lens=3)
        {return p.coins==coins&&p.hint==hint&&p.freeze==freeze&&p.lens==lens;}
        public static void Run(InkominoApp app,Action<bool,string> check)
        {
            var original=app.Store.Data;var originalBoard=app.Board;
            string originalScreen=(string)app.GetType().GetField("screen",Flags).GetValue(app);
            bool boot=app.BootReady,playing=app.Playing,transitioning=app.TransitioningLevel,animationBusy=app.AnimationBusy,sound=SoundBank.Sound,music=SoundBank.Music;
            CloudSaveSync sync=null;CloudSaveLocalState state=null;
            void Reset(PlayerProfile local,PlayerProfile baseline=null,bool dirty=false,long revision=4,string identity="account-a",long savedAt=0,long baseSavedAt=0)
            {
                if(sync)UnityEngine.Object.DestroyImmediate(sync);
                app.Store.Data=Copy(local);app.Playing=false;app.AnimationBusy=false;Set(app,"screen","home");
                Set(app,"<BootReady>k__BackingField",true);Set(app,"<TransitioningLevel>k__BackingField",false);
                sync=app.gameObject.AddComponent<CloudSaveSync>();Set(sync,"app",app);
                state=new CloudSaveLocalState{identity=identity,revision=revision,baseProfile=baseline==null?"":Json(baseline),dirty=dirty,localSavedAt=savedAt,baseSavedAt=baseSavedAt};
                Set(sync,"state",state);Set(sync,"authenticatedIdentity",identity);app.Store.Save();
            }
            void Reconcile(CloudSaveReply value){Call(sync,"Reconcile",value);}
            try{
                var bad=Profile(2,-10);app.Store.Data=Profile(1,100);
                check(!app.Store.ApplyCloudProfile(bad)&&app.Store.Data.coins==100,"invalid remote profile preserves local data");

                long instant=SaveStore.Now;
                Reset(Profile(1,100),identity:"");Set(app,"<BootReady>k__BackingField",false);
                var remote=Profile(8,75);Reconcile(Reply(remote,4,savedAt:instant-120));
                check(app.Store.Data.level==8&&app.Store.Data.coins==75&&state.revision==4&&!sync.HasDeferred,"fresh device automatically restores a complete cloud profile, including a lower coin balance");
                remote.coins=900;check(app.Store.Data.coins==75,"remote profile is deep-copied before applying");

                var same=Profile(23,320);var clockOnly=Copy(same);clockOnly.regenAt+=137;
                Reset(same,same,true);Reconcile(Reply(clockOnly));
                check(!state.dirty&&state.revision==5&&app.Store.Data.level==23&&app.Store.Data.coins==320,"same 23-level / 320-coin full-heart save ignores recovery-clock drift");
                Reset(same,same,false,2);Reconcile(Reply(Copy(same),19));
                check(!state.dirty&&state.revision==19,"identical content with a newer server revision acknowledges silently");
                Reset(same,same,true);
                Call(sync,"HandleUploadReply",new CloudSaveReply{ok=false,exists=true,code="REVISION_CONFLICT",revision=9,profile=Copy(clockOnly)},Json(same));
                check(!state.dirty&&state.revision==9&&state.identity=="account-a","same-content HTTP 409 reply without identity uses the authenticated owner and acknowledges silently");

                Reset(same,same,false);Set(sync,"started",true);long localSaved=sync.LocalSavedAt;
                app.Store.Data.regenAt+=900;Call(sync,"LocalChanged");
                check(!state.dirty&&sync.LocalSavedAt==localSaved,"full-heart clock drift does not mark gameplay dirty or change its save time");
                app.Store.Data.sound=false;app.Store.Data.haptics=false;Call(sync,"LocalChanged");
                check(!state.dirty&&sync.LocalSavedAt==localSaved,"device-preference persistence does not mark gameplay dirty or change its save time");
                app.Store.Data.avatar=2;Call(sync,"LocalChanged");
                check(state.dirty&&sync.LocalSavedAt==localSaved,"avatar changes synchronize without pretending that gameplay was edited more recently");
                app.Store.Data.coins-=50;app.Store.Data.hint++;app.Store.Save();Call(sync,"LocalChanged");
                check(state.dirty&&sync.LocalSavedAt>0,"real local purchases mark gameplay dirty and record their original edit time");

                Reset(same,same,false);var calibratedReply=Reply(Copy(same),savedAt:instant-3600);
                calibratedReply.serverTime=SaveStore.Now-3600;Reconcile(calibratedReply);Set(sync,"started",true);
                long calibratedOffset=state.clockOffset;app.Store.Data.coins-=50;app.Store.Data.hint++;app.Store.Save();
                long editBegan=SaveStore.Now;Call(sync,"LocalChanged");long editEnded=SaveStore.Now;
                check(calibratedOffset<0&&sync.LocalSavedAt>=editBegan+calibratedOffset&&sync.LocalSavedAt<=editEnded+calibratedOffset&&sync.LocalSavedAt<editEnded-3500,"server-calibrated gameplay edits use the estimated server clock instead of the device's future clock");

                var advanced=Profile(24,325);Reset(same,same,true);Reconcile(Reply(advanced,savedAt:instant-60));
                check(app.Store.Data.level==24&&app.Store.Data.coins==325&&!state.dirty,"stale dirty flags with unchanged gameplay cannot block an automatic remote restore");

                var spent=Copy(same);spent.coins=220;spent.hint=5;
                Reset(spent,same,true,savedAt:instant-7200);Reconcile(Reply(Copy(same),8,savedAt:instant));
                check(state.revision==8&&state.dirty&&Wallet(app.Store.Data,220,5)&&sync.LocalSavedAt==instant-7200,"local-only purchases safely rebase while preserving their original offline edit time");
                var durable=DurableState(app);
                check(durable.revision==8&&durable.dirty&&durable.localSavedAt==instant-7200&&CloudSaveComparison.GameplayEqual(CloudSaveComparison.Parse(durable.baseProfile),same,instant),"safe rebase and original edit time are durable before upload");

                var remoteAvatar=Copy(same);remoteAvatar.avatar=2;
                Reset(spent,same,true);Reconcile(Reply(remoteAvatar,8));
                check(state.revision==8&&state.dirty&&app.Store.Data.avatar==2&&Wallet(app.Store.Data,220,5),"remote-only avatar changes are adopted while local-only purchases remain intact");
                var localAvatar=Copy(spent);localAvatar.avatar=3;
                Reset(localAvatar,same,true);Reconcile(Reply(Copy(remoteAvatar),8));
                check(state.revision==8&&state.dirty&&app.Store.Data.avatar==3&&Wallet(app.Store.Data,220,5),"independently edited local avatars survive rebasing alongside purchases");
                Reset(spent,same,true,9);string baselineBeforeOldReply=state.baseProfile;
                Reconcile(Reply(advanced,8,savedAt:instant));
                check(state.revision==9&&state.baseProfile==baselineBeforeOldReply&&state.dirty&&app.Store.Data.level==23&&Wallet(app.Store.Data,220,5),"older server revisions cannot roll back the baseline or overwrite gameplay");

                var otherSpend=Copy(same);otherSpend.coins=280;otherSpend.freeze=4;
                Reset(spent,same,true,savedAt:instant-30);Reconcile(Reply(Copy(otherSpend),savedAt:instant-60));
                check(state.revision==5&&state.dirty&&Wallet(app.Store.Data,220,5),"newer local gameplay automatically wins as one complete wallet without a currency or item max merge");
                check(File.Exists(app.Store.Path+BackupSuffix),"automatic divergent-save resolution keeps a durable backup before proceeding");

                Reset(spent,same,true,savedAt:instant-60);Reconcile(Reply(Copy(otherSpend),savedAt:instant-30));
                check(state.revision==5&&!state.dirty&&Wallet(app.Store.Data,280,3,4),"newer cloud gameplay automatically restores one complete wallet without duplicating rewards");
                var backup=CloudSaveComparison.Parse(File.ReadAllText(app.Store.Path+BackupSuffix));
                check(backup!=null&&Wallet(backup,220,5),"automatic cloud replacement preserves the previous local wallet in backup");
                Reset(spent,same,true,savedAt:instant-60);Reconcile(Reply(Copy(otherSpend),savedAt:instant-60));
                check(!state.dirty&&Wallet(app.Store.Data,280,3,4),"equal gameplay edit times deterministically use the authoritative cloud version");
                Reset(spent,same,true);Reconcile(Reply(Copy(otherSpend)));
                check(!state.dirty&&Wallet(app.Store.Data,280,3,4),"unknown edit times deterministically use the cloud version");
                Reset(spent,same,true,savedAt:instant-30);Reconcile(Reply(Copy(otherSpend)));
                check(!state.dirty&&Wallet(app.Store.Data,280,3,4),"a missing cloud edit time cannot justify silently discarding the authoritative cloud version");
                Reset(spent,same,true,savedAt:instant-90);
                var legacyReply=Reply(Copy(otherSpend));legacyReply.updatedAt=DateTimeOffset.FromUnixTimeSeconds(instant-60).ToString("O");Reconcile(legacyReply);
                check(!state.dirty&&Wallet(app.Store.Data,280,3,4),"older servers use updatedAt as a backward-compatible cloud edit-time fallback");
                Reset(spent,same,true,savedAt:instant+365L*86400);
                Reconcile(Reply(Copy(otherSpend),savedAt:instant+30L*86400));
                check(!state.dirty&&Wallet(app.Store.Data,280,3,4),"future device and cloud clocks are bounded to now before comparing latest progress");

                var preferences=Copy(same);preferences.sound=false;preferences.music=false;preferences.haptics=false;preferences.reducedMenuMotion=true;
                Reset(preferences,same,true);Reconcile(Reply(advanced,savedAt:instant-60));
                check(app.Store.Data.level==24&&!app.Store.Data.sound&&!app.Store.Data.music&&!app.Store.Data.haptics&&app.Store.Data.reducedMenuMotion,"automatic cloud restore preserves device sound, music, haptics and motion preferences");
                Reset(preferences,same,true);Reconcile(Reply(Copy(same)));
                check(!state.dirty&&!app.Store.Data.sound,"preference-only differences do not create a synchronization loop");

                Reset(spent,same,true,savedAt:instant);
                state.pendingProfile=Json(spent);state.pendingSavedAt=instant;state.operationId="old-account-operation";
                Reconcile(Reply(Profile(2,105),1,"account-b",instant-3600));
                check(state.identity=="account-b"&&state.revision==1&&app.Store.Data.level==2&&app.Store.Data.coins==105&&string.IsNullOrEmpty(state.pendingProfile)&&string.IsNullOrEmpty(state.operationId),"changing WeChat accounts automatically restores the target account and discards old-account pending uploads");
                backup=CloudSaveComparison.Parse(File.ReadAllText(app.Store.Path+BackupSuffix));
                check(backup!=null&&Wallet(backup,220,5),"account switching preserves old-account progress in backup");
                Reset(spent,same,true,savedAt:instant);state.pendingProfile=Json(spent);state.pendingSavedAt=instant;state.operationId="old-account-operation";
                Reconcile(Reply(null,0,"account-b"));
                check(state.identity=="account-b"&&state.revision==0&&app.Store.Data.level==1&&Wallet(app.Store.Data,100,3)&&app.Store.Data.wins==0&&string.IsNullOrEmpty(state.pendingProfile)&&string.IsNullOrEmpty(state.operationId),"switching to an empty account creates fresh progress and cannot upload the previous account's wallet");

                var committed=Profile(23,270);var newer=Copy(committed);newer.coins=220;newer.hint++;
                Reset(newer,same,true,savedAt:instant-30,baseSavedAt:instant-600);
                state.pendingProfile=Json(committed);state.pendingSavedAt=instant-120;state.operationId="operation-a";Call(sync,"Persist");
                var retry=Reply(Copy(committed),6,savedAt:instant-120);retry.lastOperationId="operation-a";Reconcile(retry);
                check(state.revision==6&&state.dirty&&string.IsNullOrEmpty(state.pendingProfile)&&string.IsNullOrEmpty(state.operationId)&&Wallet(app.Store.Data,220,4)&&sync.LocalSavedAt==instant-30,"committed retry acknowledgement preserves newer unsent local purchases and their edit time");
                durable=DurableState(app);
                check(durable.revision==6&&durable.dirty&&durable.pendingSavedAt==0&&durable.localSavedAt==instant-30&&CloudSaveComparison.GameplayEqual(CloudSaveComparison.Parse(durable.baseProfile),committed,instant),"retry acknowledgement durably retires only the committed operation");
                var reloaded=new SaveStore(app.Store.Path);
                check(reloaded.PersistenceAvailable&&Wallet(reloaded.Data,220,4),"newer unsent local purchases remain readable and durable after acknowledging a retry");

                Reset(spent,same,true,savedAt:instant-7200);Set(sync,"ready",true);
                var upload=(IEnumerator)Call(sync,"Upload");bool yielded=upload.MoveNext();
                check(yielded&&!string.IsNullOrEmpty(state.pendingProfile)&&state.pendingSavedAt==instant-7200,"upload freezes the original offline gameplay edit time before its first network yield");
                check(DurableState(app).pendingSavedAt==instant-7200,"pending request edit time is durable before sending");
                if(upload is IDisposable)((IDisposable)upload).Dispose();
                state.localSavedAt=instant-30;app.Store.Data.coins=170;app.Store.Data.hint=6;app.Store.Save();
                Call(sync,"HandleUploadReply",Reply(Copy(spent),9,savedAt:instant-7200),state.pendingProfile);
                check(state.revision==9&&state.dirty&&sync.LocalSavedAt==instant-30&&Wallet(app.Store.Data,170,6),"successful upload acknowledgement does not erase later local edits or refresh their clock");

                Reset(spent,same,true,savedAt:instant-7200);Set(sync,"started",true);
                state.pendingProfile=Json(spent);state.pendingSavedAt=instant-7200;state.operationId="network-pending";Call(sync,"Persist");
                Set(sync,"nextAttempt",Time.unscaledTime+60);sync.NetworkChanged(false);
                check(Get<bool>(sync,"networkKnown")&&!Get<bool>(sync,"networkConnected")&&state.dirty&&state.operationId=="network-pending"&&state.pendingSavedAt==instant-7200,"disconnecting preserves dirty progress and the durable pending operation");
                Set(sync,"nextAttempt",0f);Call(sync,"Update");
                check(!sync.Busy&&state.dirty&&state.operationId=="network-pending","known-offline update cannot start login or discard pending progress");
                Set(sync,"ready",true);
                var offlineConnect=(IEnumerator)Call(sync,"Connect");var offlineUpload=(IEnumerator)Call(sync,"Upload");
                check(!offlineConnect.MoveNext()&&!offlineUpload.MoveNext()&&!sync.Busy,"known-offline connect and upload stop before their first network yield");
                sync.NetworkChanged(true);
                check(Get<bool>(sync,"networkConnected")&&Get<bool>(sync,"reconnect")&&Get<float>(sync,"nextAttempt")==0&&state.operationId=="network-pending"&&state.pendingSavedAt==instant-7200,"reconnecting immediately clears retry backoff and queues the original pending upload");
                durable=DurableState(app);
                check(durable.dirty&&durable.operationId=="network-pending"&&durable.pendingSavedAt==instant-7200,"network callbacks leave the original pending request durable");
                Set(sync,"reconnect",false);float handledAttempt=Time.unscaledTime+27;Set(sync,"nextAttempt",handledAttempt);sync.NetworkChanged(true);
                check(!Get<bool>(sync,"reconnect")&&Get<float>(sync,"nextAttempt")==handledAttempt,"duplicate online notifications do not queue a second reconnect or reset its schedule");

                Reset(spent,same,true,savedAt:instant-7200);Set(sync,"started",true);
                state.pendingProfile=Json(spent);state.pendingSavedAt=instant-7200;state.operationId="busy-network-pending";Call(sync,"Persist");
                sync.NetworkChanged(false);Set(sync,"busy",true);Set(sync,"nextAttempt",Time.unscaledTime+60);sync.NetworkChanged(true);
                check(sync.Busy&&Get<bool>(sync,"reconnect")&&Get<float>(sync,"nextAttempt")==0&&state.operationId=="busy-network-pending","recovery during an in-flight request queues immediate reconnect without clearing that request");
                Set(sync,"failureCode","HTTP_0");Call(sync,"Offline");
                check(Get<bool>(sync,"reconnect")&&Get<float>(sync,"nextAttempt")==0&&state.operationId=="busy-network-pending"&&state.pendingSavedAt==instant-7200,"the previous request failing after recovery cannot restore its old retry delay or erase pending progress");
                Call(sync,"HandleUploadReply",Reply(Copy(spent),9,savedAt:instant-7200),state.pendingProfile);
                check(Get<bool>(sync,"reconnect")&&Get<float>(sync,"nextAttempt")==0&&state.revision==9&&string.IsNullOrEmpty(state.pendingProfile),"the previous request succeeding after recovery acknowledges once while keeping immediate reconnect queued");

                Reset(spent,same,true);Set(sync,"started",true);Set(sync,"busy",true);
                float initialAttempt=Time.unscaledTime+60;Set(sync,"nextAttempt",initialAttempt);sync.NetworkChanged(true);
                check(Get<bool>(sync,"networkKnown")&&Get<bool>(sync,"networkConnected")&&!Get<bool>(sync,"reconnect")&&Get<float>(sync,"nextAttempt")==initialAttempt,"initial online detection does not restart a login already in flight");

                Reset(same,same,false);app.Playing=true;
                var board=new BoardState(new LevelData{id=23,n=1,blocked=new int[1],targets=new[]{"one"}},new ShapeLibrary(new[]{new ShapeData{id="one",cells=new[]{new Cell(0,0)}}}));
                board.Pieces.Add(new PieceData("one",new[]{0}));app.Board=board;
                Reconcile(Reply(advanced,savedAt:instant-60));
                check(sync.HasDeferred&&state.revision==4&&app.Store.Data.level==23&&ReferenceEquals(app.Board,board)&&board.Pieces.Count==1,"cloud progress arriving during a level waits without replacing the active board or wallet");
                Call(sync,"TryApplyDeferred");check(sync.HasDeferred&&app.Store.Data.level==23,"deferred restore cannot be forced during active gameplay");
                app.Playing=false;Call(sync,"TryApplyDeferred");
                check(!sync.HasDeferred&&state.revision==5&&app.Store.Data.level==24,"returning home applies a still-safe deferred cloud restore");

                Reset(same,same,false,savedAt:instant-300);app.Playing=true;Reconcile(Reply(advanced,savedAt:instant-150));
                app.Store.Data.coins=220;app.Store.Data.hint=5;app.Store.Save();Set(sync,"started",true);Call(sync,"LocalChanged");
                long duringPlaySaved=sync.LocalSavedAt;app.Playing=false;Call(sync,"TryApplyDeferred");
                check(!sync.HasDeferred&&state.revision==5&&state.dirty&&app.Store.Data.level==23&&Wallet(app.Store.Data,220,5)&&sync.LocalSavedAt==duringPlaySaved,"new gameplay edits made during a deferred restore are re-compared and automatically win by their later edit time");

                Reset(same,same,false,savedAt:instant-300);app.Playing=true;
                var timedDeferredReply=Reply(Copy(advanced),savedAt:instant-10);timedDeferredReply.serverTime=instant-10;Reconcile(timedDeferredReply);
                check(sync.HasDeferred&&app.Store.Data.level==23,"a reply carrying server time waits safely during active gameplay");
                app.Store.Data.coins=220;app.Store.Data.hint=5;app.Store.Save();state.dirty=true;
                state.localSavedAt=instant-5;state.clockOffset=0;app.Playing=false;Call(sync,"TryApplyDeferred");
                check(!sync.HasDeferred&&state.revision==5&&state.dirty&&app.Store.Data.level==23&&Wallet(app.Store.Data,220,5)&&sync.LocalSavedAt==instant-5&&state.clockOffset==0,"rechecking a deferred reply cannot reset the clock to its old server time or discard a newer purchase");

                Reset(Profile(1,100),identity:"");Set(app,"<BootReady>k__BackingField",false);app.Playing=true;
                Reconcile(Reply(Copy(advanced),savedAt:instant-60));
                check(sync.HasDeferred&&app.Store.Data.level==1&&app.Store.Data.coins==100,"late cloud arrival cannot replace the first active board while startup is still fading out");
                Reset(Profile(1,100),identity:"");Set(app,"<BootReady>k__BackingField",false);Set(app,"<TransitioningLevel>k__BackingField",true);
                Reconcile(Reply(Copy(advanced),savedAt:instant-60));
                check(sync.HasDeferred&&app.Store.Data.level==1,"late cloud arrival waits for an active startup level transition even before BootReady");
                Reset(Profile(1,100),identity:"");Set(app,"<BootReady>k__BackingField",false);app.AnimationBusy=true;
                Reconcile(Reply(Copy(advanced),savedAt:instant-60));
                check(sync.HasDeferred&&app.Store.Data.level==1,"startup animation also prevents asynchronous cloud replacement before BootReady");

                Reset(same,same,false);Set(app,"<TransitioningLevel>k__BackingField",true);Reconcile(Reply(advanced));
                Call(sync,"TryApplyDeferred");check(sync.HasDeferred&&app.Store.Data.level==23,"cloud restore waits while the next level is loading");
                Set(app,"<TransitioningLevel>k__BackingField",false);Call(sync,"TryApplyDeferred");
                check(!sync.HasDeferred&&app.Store.Data.level==24,"cloud restore resumes safely after a transition");
                Reset(same,same,false);app.AnimationBusy=true;Reconcile(Reply(advanced));
                check(sync.HasDeferred&&app.Store.Data.level==23,"cloud restore waits for active animations");
                app.AnimationBusy=false;Call(sync,"TryApplyDeferred");check(!sync.HasDeferred&&app.Store.Data.level==24,"cloud restore resumes after animation");
                Reset(same,same,false);Set(app,"screen","shop");Reconcile(Reply(advanced));
                check(sync.HasDeferred&&app.Store.Data.level==23,"background cloud arrival does not interrupt browsing another page");
                Set(app,"screen","home");Call(sync,"TryApplyDeferred");
                check(!sync.HasDeferred&&app.Store.Data.level==24,"returning home applies the deferred record without any local/cloud choice");

                Reset(spent,same,true,savedAt:instant-90);string protectedJson=Json(app.Store.Data);string protectedBase=state.baseProfile;
                using(var lockedBackup=new FileStream(app.Store.Path+BackupSuffix,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
                    Reconcile(Reply(Copy(otherSpend),savedAt:instant-30));
                    check(Json(app.Store.Data)==protectedJson&&state.revision==4&&state.baseProfile==protectedBase,"backup write failure protects local gameplay and its baseline from automatic replacement");
                }
                Reset(spent,same,true,savedAt:instant);
                using(var lockedBackup=new FileStream(app.Store.Path+BackupSuffix,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
                    Reconcile(Reply(null,0,"account-b"));
                    check(state.identity=="account-a"&&app.Store.Data.level==23&&Wallet(app.Store.Data,220,5),"failed account-switch backup cannot erase the old account or upload it as the new account");
                }

                var storeBeforeProbe=app.Store;
                string probePath=Path.Combine(Path.GetDirectoryName(app.Store.Path),"startup-cloud-profile-"+Guid.NewGuid().ToString("N")+".json");
                try{
                    app.Store=null;check(app.ShouldAwaitInitialCloud,"startup with no store waits for its first cloud lookup");
                    var noLocal=new SaveStore(probePath);app.Store=noLocal;
                    check(!noLocal.HasLocalProfile&&app.ShouldAwaitInitialCloud,"an absent local profile does not bypass the initial cloud lookup");
                    IEnumerator delayedInitialization(){yield return null;}
                    int backgroundStarts=0;IEnumerator backgroundTask=null;
                    Action<IEnumerator> captureBackground=value=>{backgroundStarts++;backgroundTask=value;};
                    var delayed=delayedInitialization();var startupTask=app.InitializeCloudSaveTask(delayed,captureBackground);
                    check(startupTask.MoveNext()&&ReferenceEquals(startupTask.Current,delayed)&&backgroundStarts==0,"first launch awaits the actual cloud initialization task without also scheduling a duplicate background task");
                    noLocal.Data=Profile(1,100);noLocal.Save();var validFirstLevel=new SaveStore(probePath);app.Store=validFirstLevel;
                    check(validFirstLevel.HasLocalProfile&&!validFirstLevel.HasCompletedLevel&&!app.ShouldAwaitInitialCloud,"a valid saved first-level player takes the local startup path even before completing any level");
                    var warmStartup=app.InitializeCloudSaveTask(delayed,captureBackground);
                    check(!warmStartup.MoveNext()&&backgroundStarts==1&&ReferenceEquals(backgroundTask,delayed),"valid local startup finishes immediately while queuing the same cloud task in the background");
                    check(!warmStartup.MoveNext()&&backgroundStarts==1,"revisiting the completed startup iterator cannot queue the cloud task twice");
                    var invalidProfile=Profile(1,100);invalidProfile.coins=-1;File.WriteAllText(probePath,Json(invalidProfile));
                    SaveStore invalidLocal;bool logging=Debug.unityLogger.logEnabled;
                    try{Debug.unityLogger.logEnabled=false;invalidLocal=new SaveStore(probePath);}finally{Debug.unityLogger.logEnabled=logging;}
                    app.Store=invalidLocal;
                    check(!invalidLocal.HasLocalProfile&&app.ShouldAwaitInitialCloud,"semantically invalid local JSON is not treated as usable saved progress");
                    File.WriteAllText(probePath,"{not-valid-json");
                    try{Debug.unityLogger.logEnabled=false;app.Store=new SaveStore(probePath);}finally{Debug.unityLogger.logEnabled=logging;}
                    check(!app.Store.HasLocalProfile&&app.ShouldAwaitInitialCloud,"malformed local JSON cannot skip the initial cloud lookup");
                }finally{
                    app.Store=storeBeforeProbe;
                    foreach(string path in new[]{probePath,probePath+".bak",probePath+".tmp"})if(File.Exists(path))File.Delete(path);
                }

                var lowHearts=Copy(same);lowHearts.hearts=3;lowHearts.regenAt=SaveStore.Now+120;
                var shiftedHeart=Copy(lowHearts);shiftedHeart.regenAt+=150;
                check(!CloudSaveComparison.GameplayEqual(lowHearts,shiftedHeart,SaveStore.Now),"a recovery-clock difference below full hearts remains meaningful gameplay");
                var resumed=Copy(same);resumed.resumeLevel=23;resumed.resumeTime=123;resumed.resumePieces=new[]{new PieceData("one",new[]{0})};
                var boardChanged=Copy(resumed);boardChanged.resumePieces[0].cells=new[]{1};
                check(!CloudSaveComparison.GameplayEqual(resumed,boardChanged,SaveStore.Now),"different unfinished placements remain meaningful despite identical level and coins");
                var expired=Copy(same);var ads=AdPlacements.Settings;
                expired.dailyAt=instant-90000;expired.shopDay=(instant+8*3600)/86400-1;expired.shopPurchases=new[]{3,2,1};
                expired.climb.windowStart=ClimbActivity.WindowStart(instant)-86400;expired.climb.joined=true;expired.climb.stage=7;expired.climb.claimed=true;
                expired.climb.finalBots=47;expired.climb.finalCount=48;expired.climb.eliminated=new[]{1,2,3};
                expired.adUsage=new[]{new AdUsage{placement=ads.rewarded[0].placement,day=(instant+ads.dayResetUtcOffsetMinutes*60L)/86400-1,lastAt=instant-90000,count=5}};
                expired.lastRewardAdAt=expired.lastInterstitialAt=instant-90000;string beforeNormalization=Json(expired);
                check(CloudSaveComparison.GameplayEqual(expired,same,instant),"expired eligibility clocks normalize to the same current progress");
                check(Json(expired)==beforeNormalization,"semantic comparison never mutates a stored profile");
                var boardOrder=Copy(resumed);boardOrder.resumeTime=123.001f;boardOrder.resumePieces=new[]{new PieceData("one",new[]{0}){appearanceSaved=true,colorIndex=2,textureIndex=3},new PieceData("one",new[]{1})};
                var reordered=Copy(boardOrder);reordered.resumeTime=123.009f;reordered.resumePieces=new[]{reordered.resumePieces[1],reordered.resumePieces[0]};
                reordered.resumePieces[1].colorIndex=7;reordered.resumePieces[1].textureIndex=9;reordered.resumePieces[1].textureOffsetX=.25f;
                check(CloudSaveComparison.GameplayEqual(boardOrder,reordered,instant),"identical unfinished layouts ignore array order, textures and subsecond drift");
                var purchasedToday=Copy(same);purchasedToday.shopDay=(instant+8*3600)/86400;purchasedToday.shopPurchases[0]=1;
                check(!CloudSaveComparison.GameplayEqual(same,purchasedToday,instant),"current-day purchase usage remains meaningful");
                var claimable=Profile(41,1320);claimable.climb.joined=true;claimable.climb.stage=7;var claimed=Copy(claimable);claimed.climb.claimed=true;
                check(!CloudSaveComparison.GameplayEqual(claimable,claimed,instant),"current-window reward claim remains meaningful even when level and coins match");
            }catch(Exception error){check(false,"cloud reconciliation runtime error: "+error.GetBaseException().Message);}
            finally{
                if(sync)UnityEngine.Object.DestroyImmediate(sync);
                Set(app,"<BootReady>k__BackingField",boot);app.Playing=playing;app.Board=originalBoard;
                Set(app,"<TransitioningLevel>k__BackingField",transitioning);app.AnimationBusy=animationBusy;
                Set(app,"screen",originalScreen);app.Store.Data=original;app.Store.Save();SoundBank.Settings(sound,music);
            }
        }
    }
}
#endif

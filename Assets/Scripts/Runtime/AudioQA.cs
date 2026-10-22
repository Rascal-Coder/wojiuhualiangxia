#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Inkomino
{
    public sealed class AudioQA : MonoBehaviour
    {
        [Serializable] public sealed class Playback { public string key; public float gain; }
        [Serializable] public sealed class Result { public string status; public int assets, effects, checks; public string[] musicBranches, errors; public Playback[] playback; public string profile; }
        readonly List<string> errors=new List<string>();
        readonly List<Playback> playback=new List<Playback>();
        readonly HashSet<string> branches=new HashSet<string>();
        int checks,assets,effects; string dir,profile;
        void Check(bool ok,string reason){checks++;if(!ok){errors.Add(reason);Debug.LogError("AUDIO_QA "+reason);}}
        void Heard(string key,float gain){playback.Add(new Playback{key=key,gain=gain});}
        bool HeardSince(int start,string key){return playback.Skip(start).Any(p=>p.key==key);}
        string Exact(string key){return SoundBank.Entries.Any(e=>e.key==key)?key:"shell:"+key;}
        IEnumerator Start()
        {
            // Exercise the engine's playback controls without broadcasting the
            // exhaustive effect sweep through the user's desktop speakers.
            AudioListener.volume=0;
            dir=InkominoApp.QAOutput();Directory.CreateDirectory(dir);profile=Path.Combine(dir,"audio-qa-profile.json");
            SoundBank.Played+=Heard;
            var run=Run();bool running=true;
            while(running)
            {
                object wait=null;
                try{running=run.MoveNext();if(running)wait=run.Current;}
                catch(Exception ex){errors.Add(ex.ToString());Debug.LogException(ex);running=false;}
                if(running)yield return wait;
            }
            SoundBank.Settings(false,false);SoundBank.Played-=Heard;
            var result=new Result{status=errors.Count==0?"PASS":"FAIL",assets=assets,effects=effects,checks=checks,musicBranches=branches.OrderBy(x=>x).ToArray(),errors=errors.ToArray(),playback=playback.ToArray(),profile=profile};
            string json=JsonUtility.ToJson(result,true);File.WriteAllText(Path.Combine(dir,"runtime-audio-validation.json"),json);Debug.Log("INKOMINO_AUDIO_QA "+result.status+" checks="+checks+" assets="+assets+" effects="+effects);Application.Quit(errors.Count==0?0:1);
        }
        IEnumerator Run()
        {
            yield return null;
            var app=InkominoApp.Instance;Check(app!=null&&app.IsQA,"dedicated QA startup");
            app.Store=new SaveStore(profile);app.Store.Data=new PlayerProfile{level=20,coins=1000,hearts=5,hint=10,lens=10,freeze=10,drawIntroSeen=true,regenAt=SaveStore.Now+900};
            var entries=SoundBank.Entries.ToArray();Check(entries.Length==83,"catalog contains all 83 original MP3 assets");
            foreach(var e in entries){var clip=Resources.Load<AudioClip>(e.resource);Check(clip&&clip.length>0&&clip.channels>0,"load and decode "+e.key);assets++;}
            SoundBank.Settings(false,false);var owner=new GameObject("Audio QA isolated owner");SoundBank.Init(owner,true,false);
            foreach(var entry in entries.Where(e=>!e.music))
            {
                int n=playback.Count;SoundBank.Play(entry.key);Check(playback.Count==n+1,"one playback event "+entry.key);
                if(playback.Count>n){var p=playback[n];Check(p.key==entry.key&&Mathf.Abs(p.gain-entry.volume)<.0001f,"exact key and original gain "+entry.key);}
                effects++;
            }
            Check(effects==79,"79 original nonmusic effects exercised");
            int before=playback.Count;SoundBank.Settings(false,false);SoundBank.Play("pipe_place");Check(playback.Count==before,"sound mute suppresses playback");Check(owner.GetComponents<AudioSource>().All(s=>!s.isPlaying),"mute stops every source");
            SoundBank.Settings(true,false);SoundBank.SetSuspended(true);before=playback.Count;SoundBank.Play("pipe_place");Check(playback.Count==before,"suspension suppresses playback");Check(owner.GetComponents<AudioSource>().All(s=>!s.isPlaying),"suspension stops every source");SoundBank.SetSuspended(false);
            var groups=new Dictionary<string,int>{{"pump_hit",2},{"flow_step",3},{"coin_tick",3},{"card_deal",2},{"card_flip",2},{"climb_pop",3},{"climb_fall",2}};
            foreach(var group in groups)for(int i=0;i<group.Value+1;i++){int n=playback.Count;string expected=Exact(group.Key+"_"+(i%group.Value+1));SoundBank.Play(group.Key);Check(playback.Count==n+1&&playback[n].key==expected,"variant sequence "+group.Key+" step "+i);}
            SoundBank.Settings(false,false);Destroy(owner);yield return null;
            var state=UnityEngine.Random.state;
            for(int seed=0;seed<64&&branches.Count<3;seed++)
            {
                UnityEngine.Random.InitState(seed);owner=new GameObject("BGM QA seed "+seed);SoundBank.Init(owner,true,true);branches.Add(SoundBank.CurrentMusic);
                var source=SoundBank.MusicSource;var clip=source.clip;string key=SoundBank.CurrentMusic;Check(clip&&source.loop&&Mathf.Abs(source.volume-.3f)<.0001f,"BGM loops at original 0.3 gain seed "+seed);Check(SoundBank.MusicTracks.Contains(key),"BGM comes from original three tracks");
                yield return new WaitForSecondsRealtime(.15f);float time=source.time;SoundBank.Settings(true,false);Check(source.clip==clip&&SoundBank.CurrentMusic==key&&!source.isPlaying,"music disabled preserves selected clip");
                yield return new WaitForSecondsRealtime(.1f);Check(Mathf.Abs(source.time-time)<.12f,"paused music time stays fixed");SoundBank.Settings(true,true);Check(source.clip==clip&&SoundBank.CurrentMusic==key&&Mathf.Abs(source.time-time)<.15f,"resume preserves clip and playback position");
                SoundBank.SetSuspended(true);time=source.time;yield return new WaitForSecondsRealtime(.1f);Check(!source.isPlaying&&Mathf.Abs(source.time-time)<.12f,"background suspension pauses music");SoundBank.SetSuspended(false);Check(source.clip==clip&&SoundBank.CurrentMusic==key,"foreground restores same music");
                SoundBank.Settings(false,false);Destroy(owner);yield return null;
            }
            UnityEngine.Random.state=state;Check(branches.Count==3,"fixed seeds reach all three BGM choices");SoundBank.Init(app.gameObject,true,true);
            app.StartLevel(12,true);yield return null;Check(app.InputEnabled,"gameplay input ready");
            var cells=app.Board.Level.solution[0].cells;var view=app.BoardView;before=playback.Count;view.BeginCell(cells[0]);view.Trail.Clear();view.Trail.AddRange(cells);bool placed=view.EndStroke();Check(placed&&app.Board.Pieces.Count==1&&HeardSince(before,"pipe_place"),"real board placement emits pipe_place");
            before=playback.Count;view.BeginCell(cells[0]);view.EndStroke();Check(app.Board.Pieces.Count==0&&HeardSince(before,"card_pick"),"real board erase emits card_pick");
            before=playback.Count;view.BeginCell(cells[0]);view.EndStroke();Check(HeardSince(before,"invalid_drop"),"invalid single cell emits invalid_drop");
            before=playback.Count;int hints=app.Store.Data.hint;app.UseBooster(0);float deadline=Time.realtimeSinceStartup+8;
            while(app.AnimationBusy&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(!app.AnimationBusy&&app.Store.Data.hint==hints-1&&app.Board.Pieces.Count>0&&HeardSince(before,"hint_sparkle"),"real pen booster emits hint_sparkle");
            before=playback.Count;int lenses=app.Store.Data.lens;app.UseBooster(2);deadline=Time.realtimeSinceStartup+8;
            while(app.AnimationBusy&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(!app.AnimationBusy&&app.Store.Data.lens==lenses-1&&view.Reveal!=null&&HeardSince(before,"hint_sparkle"),"real lens booster emits hint_sparkle");
            before=playback.Count;app.Fail();Check(app.Finished&&HeardSince(before,"shell:fail"),"real failure uses shell generic fail");
            app.ClearPopups();app.StartLevel(12,true);yield return null;before=playback.Count;app.Win();Check(app.Finished&&HeardSince(before,"shell:win"),"real victory uses shell generic win");
            app.Playing=false;yield return new WaitForSecondsRealtime(.2f);
        }
    }
}
#endif

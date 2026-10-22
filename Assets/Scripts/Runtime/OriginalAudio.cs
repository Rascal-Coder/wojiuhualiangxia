using System;
using System.Collections.Generic;
using UnityEngine;

namespace Inkomino
{
    [Serializable] public sealed class OriginalAudioEntry {public string key,resource;public float volume=1;public bool music;}
    [Serializable] public sealed class OriginalAudioCatalog {public OriginalAudioEntry[] entries;}
    public static class SoundBank
    {
        public static bool Sound{get;private set;}=true;
        public static bool Music{get;private set;}=true;
        public static readonly string[] MusicTracks={"music:calmpaper","music:calmdusk","music:calmdaylight"};
        public static event Action<string,float> Played;
        static readonly Dictionary<string,OriginalAudioEntry> catalog=new Dictionary<string,OriginalAudioEntry>();
        static readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        static readonly Dictionary<string,AudioSource> voices=new Dictionary<string,AudioSource>();
        static readonly Dictionary<string,int> cycles=new Dictionary<string,int>();
        static readonly Dictionary<string,int> variants=new Dictionary<string,int>{{"card_deal",2},{"pump_hit",2},{"flow_step",3},{"coin_tick",3},{"card_flip",2},{"climb_pop",3},{"climb_fall",2}};
        static GameObject host;static AudioSource music;static bool suspended,musicStarted;
        public static string CurrentMusic{get;private set;}
        public static IReadOnlyCollection<OriginalAudioEntry> Entries {get{LoadCatalog();return catalog.Values;}}
        public static AudioSource MusicSource{get{return music;}}
        static void LoadCatalog()
        {
            if(catalog.Count>0)return;
            var data=Resources.Load<TextAsset>("Data/original-audio");
            if(!data)throw new InvalidOperationException("Missing original audio catalog.");
            foreach(var entry in JsonUtility.FromJson<OriginalAudioCatalog>(data.text).entries)catalog.Add(entry.key,entry);
        }
        public static AudioClip Clip(string key)
        {
            LoadCatalog();OriginalAudioEntry entry;if(!catalog.TryGetValue(key,out entry))return null;
            AudioClip clip;if(!clips.TryGetValue(key,out clip)){clip=Resources.Load<AudioClip>(entry.resource);if(clip)clips.Add(key,clip);}
            return clip;
        }
        public static void Init(GameObject owner,bool sound=true,bool on=true)
        {
            LoadCatalog();if(music){music.Stop();UnityEngine.Object.Destroy(music);}foreach(var voice in voices.Values)if(voice){voice.Stop();UnityEngine.Object.Destroy(voice);}
            host=owner;voices.Clear();cycles.Clear();suspended=false;musicStarted=false;Sound=sound;Music=on;
            music=owner.AddComponent<AudioSource>();music.playOnAwake=false;music.loop=true;music.volume=.3f;music.spatialBlend=0;
            // module1996 pickMusicAsset: uniform random once; loaded music then
            // resumes without choosing again. All three native backends loop.
            CurrentMusic=MusicTracks[UnityEngine.Random.Range(0,MusicTracks.Length)];music.clip=Clip(CurrentMusic);
            ApplyMusic();
        }
        static void ApplyMusic()
        {
            if(!music||!music.clip)return;
            if(!Music||suspended){if(musicStarted)music.Pause();return;}
            if(!musicStarted){music.Play();musicStarted=true;Played?.Invoke(CurrentMusic,music.volume);}else music.UnPause();
        }
        public static void Settings(bool sound,bool on)
        {
            Sound=sound;Music=on;if(!sound)foreach(var voice in voices.Values)if(voice)voice.Stop();ApplyMusic();
        }
        public static void SetSuspended(bool value)
        {
            suspended=value;if(value)foreach(var voice in voices.Values)if(voice)voice.Stop();ApplyMusic();
        }
        public static string Resolve(string token)
        {
            LoadCatalog();switch(token){case "click":token="ui_tap";break;case "place":token="pipe_place";break;case "win":token="shell:win";break;case "fail":token="shell:fail";break;case "freeze":token="freeze_cast";break;case "hint":token="hint_sparkle";break;case "invalid":token="invalid_drop";break;}
            int count;if(variants.TryGetValue(token,out count)){int index;cycles.TryGetValue(token,out index);cycles[token]=(index+1)%count;token+="_"+(index+1);}
            if(catalog.ContainsKey(token))return token;
            string shell="shell:"+token;return catalog.ContainsKey(shell)?shell:null;
        }
        public static void Play(string token,float volume=-1)
        {
            if(!Sound||suspended||!host)return;string key=Resolve(token);
            if(key==null){Debug.LogError("Unknown original sound event: "+token);return;}
            var entry=catalog[key];if(entry.music)return;var clip=Clip(key);if(!clip){Debug.LogError("Missing original sound: "+key);return;}
            AudioSource voice;
            if(!voices.TryGetValue(key,out voice)||!voice){voice=host.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;voices[key]=voice;}
            voice.Stop();voice.clip=clip;voice.volume=Mathf.Clamp01(volume<0?entry.volume:volume);voice.loop=false;voice.Play();Played?.Invoke(key,voice.volume);
        }
    }
}

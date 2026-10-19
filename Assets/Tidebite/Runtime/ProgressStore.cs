using System;
using System.IO;
using Tidebite.Core;
using UnityEngine;

namespace Tidebite
{
    public sealed class ProgressStore
    {
        public string Path{get;}
        public ProgressStore(string path){Path=System.IO.Path.GetFullPath(path);}
        public PlayerProgress Load()=>Read(Path)??Read(Path+".bak")??new PlayerProgress();
        private static PlayerProgress Read(string path)
        {
            try
            {
                if(!File.Exists(path))return null;
                var data=JsonUtility.FromJson<PlayerProgress>(File.ReadAllText(path));
                if(data==null||data.Version!=1)return null;data.Sanitize();return data;
            }
            catch(Exception e)when(e is IOException||e is ArgumentException||e is UnauthorizedAccessException)
            {Debug.LogWarning("TIDEBITE PROGRESS_READ_RECOVERY "+e.GetType().Name);return null;}
        }
        public bool Save(PlayerProgress data)
        {
            try
            {
                data.Sanitize();Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                File.WriteAllText(Path+".tmp",JsonUtility.ToJson(data,true));
                // Keep a known-good previous save rather than backing up corrupt input.
                if(Read(Path)!=null)File.Copy(Path,Path+".bak",true);
                if(File.Exists(Path))
                {
                    try{File.Replace(Path+".tmp",Path,null);}
                    catch(PlatformNotSupportedException){File.Copy(Path+".tmp",Path,true);File.Delete(Path+".tmp");}
                }
                else File.Move(Path+".tmp",Path);
                return true;
            }
            catch(Exception e)when(e is IOException||e is UnauthorizedAccessException)
            {Debug.LogWarning("TIDEBITE PROGRESS_SAVE_FAILED "+e.GetType().Name);return false;}
        }
        public void AppendRun(RunRecord record)
        {
            try{Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));File.AppendAllText(Path+".runs.jsonl",JsonUtility.ToJson(record)+"\n");}
            catch(Exception e)when(e is IOException||e is UnauthorizedAccessException){Debug.LogWarning("TIDEBITE RUN_LOG_FAILED "+e.GetType().Name);}
        }
    }
    [Serializable]
    public sealed class RunRecord
    {
        public string endedUtc,mode,reason;
        public float seconds,firstFood=-1,firstEat=-1,firstEvolution=-1;
        public int score,level,foods,eats,dodges;
    }
}

using System;
using System.IO;
using System.Collections.Generic;
using Tidebite.Core;
using UnityEngine;

namespace Tidebite
{
    // Persist before sending. A retry after a crash keeps the original round ID and payload.
    public sealed class RoundOutbox
    {
        [Serializable] private sealed class Saved {public OnlineRoundReport[] reports;}
        private readonly Queue<OnlineRoundReport> reports=new Queue<OnlineRoundReport>();
        private readonly string path;
        public int Count=>reports.Count;
        public OnlineRoundReport Peek()=>reports.Peek();
        public RoundOutbox(string file)
        {
            path=Path.GetFullPath(file);
            var saved=Read(path)??Read(path+".bak");
            if(saved?.reports!=null)foreach(var report in saved.reports)if(report!=null&&report.participants!=null&&report.participants.Length<=256&&reports.Count<64)reports.Enqueue(report);
        }
        private static Saved Read(string file)
        {
            try{return File.Exists(file)&&new FileInfo(file).Length<=4194304?JsonUtility.FromJson<Saved>(File.ReadAllText(file)):null;}
            catch(Exception e)when(e is IOException||e is ArgumentException||e is UnauthorizedAccessException){return null;}
        }
        public bool Enqueue(OnlineRoundReport report)
        {if(reports.Count>=64)return false;reports.Enqueue(report);return Save();}
        public void Acknowledge(){reports.Dequeue();Save();}
        private bool Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path+".tmp",JsonUtility.ToJson(new Saved{reports=reports.ToArray()}));
                if(Read(path)!=null)File.Copy(path,path+".bak",true);
                if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);
                return true;
            }
            catch(Exception e)when(e is IOException||e is UnauthorizedAccessException||e is PlatformNotSupportedException)
            {Debug.LogError("TIDEBITE OUTBOX_SAVE_FAILED "+e.GetType().Name);return false;}
        }
    }
}

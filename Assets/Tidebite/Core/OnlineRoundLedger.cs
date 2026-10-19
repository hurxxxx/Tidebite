#nullable disable
using System;
using System.Collections.Generic;

namespace Tidebite.Core
{
    // Receives authority state only; retains disconnected humans until their round is reported.
    public sealed class OnlineRoundLedger
    {
        public event Action<OnlineRoundReport> Completed;
        private readonly Dictionary<string,OnlineParticipant> players=new Dictionary<string,OnlineParticipant>();
        private readonly string instance;
        private int tracked=-1,reported=-1,duration;
        public OnlineRoundLedger(string serverInstance){instance=serverInstance;}
        public void Begin(World world)
        {
            int id=world.Round.Id;if(id==tracked)return;
            if(tracked>=0&&reported!=tracked)Report();
            tracked=id;duration=(int)Math.Ceiling(world.Config.RoundDuration);players.Clear();
        }
        public void Capture(World world,string admission,int slot,bool complete)
        {
            if(string.IsNullOrEmpty(admission)||reported==tracked||slot<0||slot>=world.Fish.Length)return;
            var f=world.Fish[slot];if(!f.Occupied||f.IsBot||f.WaitingForRound)return;
            if(!players.TryGetValue(admission,out var record))players[admission]=record=new OnlineParticipant{admissionId=admission};
            record.score=f.RoundScore;record.kills=f.RoundKills;record.assists=f.RoundAssists;record.level=GrowthCurve.Level(f.Mass,world.Config);
            record.rank=Math.Max(1,world.Rank(slot));record.completed=complete;
        }
        public void Finish(World world){if(world.Round.Phase==RoundPhase.Results&&reported!=tracked)Report();}
        private void Report()
        {
            reported=tracked;if(players.Count==0)return;
            var records=new List<OnlineParticipant>(players.Values);records.Sort((a,b)=>string.CompareOrdinal(a.admissionId,b.admissionId));
            Completed?.Invoke(new OnlineRoundReport{roundId=instance+":"+tracked,endedAt=DateTimeOffset.UtcNow.ToUnixTimeSeconds(),durationSeconds=duration,participants=records.ToArray()});
        }
    }
}

using System;
using FishNet.Serializing;
using Tidebite.Core;
using UnityEngine;

namespace Tidebite
{
    internal static class WireValidation
    {
        // Exercises FishNet's generated serializers in a Development player, without extra clients.
        public static void Run()
        {
            var fish=new FishWire[12];
            for(int i=0;i<fish.Length;i++)fish[i]=FishWire.From(new FishState {
                Id=52+i,Occupied=true,Alive=true,IsBot=true,Generation=3,Mass=4.5f,
                Health=37.25f,Species=1,HurtProtection=.6f,RegenDelay=5,Position=new V2(65,-60),
                DodgeRemaining=.1f,Combo=4,ComboRemaining=7,RivalId=63,RivalGeneration=9,RivalRemaining=42,Hunter=2,Armor=1,Feeder=2,WardCooldown=8,RoundScore=3456,RoundKills=12,WaitingForRound=true
            });
            var masks=new ulong[GameConfig.FoodCapacity/64];masks[0]=1;masks[4]=1UL<<3;masks[15]=1UL<<63;
            var sent=new SnapshotMessage{Tick=71,Ack=8,Visible=(1UL<<63)|(1UL<<32)|1,Count=12,Fish=fish,FoodMask=masks,Round=new RoundInfo{Id=7,KingId=63,WinnerId=-1,Elapsed=191.5f,Duration=240,Phase=RoundPhase.Finale,LocalRank=3}};
            var writer=WriterPool.Retrieve();
            try
            {
                writer.Write(sent);int bytes=writer.GetArraySegment().Count;
                var reader=ReaderPool.Retrieve(writer.GetArraySegment(),null);
                try
                {
                    var received=reader.Read<SnapshotMessage>();
                    Require(received.Tick==sent.Tick&&received.Visible==sent.Visible&&received.Fish.Length==12,"Snapshot identity/64-bit visibility");
                    var state=received.Fish[11].State();
                    Require(state.Id==63&&state.Species==1&&Mathf.Abs(state.Health-37.25f)<.001f&&state.Alive,"Health/species at slot 63");
                    Require(Mathf.Abs(state.HurtProtection-.6f)<.002f&&Mathf.Abs(state.RegenDelay-5)<.002f,"Hit protection/recovery timers");
                    Require(state.Combo==4&&state.RivalId==63&&state.RivalGeneration==9&&Mathf.Abs(state.DodgeRemaining-.1f)<.002f&&Mathf.Abs(state.ComboRemaining-7)<.002f&&Mathf.Abs(state.RivalRemaining-42)<.002f,"Dodge, combo and rival state");
                    Require(state.Hunter==2&&state.Armor==1&&state.Feeder==2&&Mathf.Abs(state.WardCooldown-8)<.002f,"Evolution ranks/guard cooldown");
                    Require(state.RoundScore==3456&&state.RoundKills==12&&state.WaitingForRound,"Round score/kills/waiting state");
                    Require(received.Round.Id==7&&received.Round.KingId==63&&received.Round.LocalRank==3&&received.Round.Phase==RoundPhase.Finale&&Mathf.Abs(received.Round.Elapsed-191.5f)<.001f,"Round clock/phase/rank");
                    Require(received.FoodMask.Length==16&&received.FoodMask[4]==8&&received.FoodMask[15]==1UL<<63,"Food 259 and 1023");
                    Require(bytes<1100,"Snapshot payload exceeds MTU budget");
                    Debug.Log($"TIDEBITE WIRE_VALIDATED protocol={NetworkSession.Protocol} bytes={bytes} fish=12 maxSlot=63 food=1024 health=37.25 species=1");
                }
                finally{reader.Store();}
            }
            finally{writer.Store();}
        }
        private static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException("Wire validation: "+label);}
    }
}

using System;
using System.IO;
using Tidebite.Core;
using UnityEngine;

namespace Tidebite
{
    // One Development process, real loopback UDP: no extra macOS apps or windows.
    public sealed class NetworkPlayValidation:MonoBehaviour
    {
        private NetworkSession server,first,late,reconnected;
        private GameConfig config;
        private float started,stageAt,printed;
        private int stage,initialRound,oldSnapshots;
        private int combatFinisher,expectedAssistScore;
        private bool finished;
        private string resultPath;
        private readonly Report report=new Report();
        private const string Endpoint="127.0.0.1:27887";
        private void Start()
        {
            started=Time.realtimeSinceStartup;
            string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--result-file");
            resultPath=index>=0&&index+1<args.Length?args[index+1]:Path.Combine(Application.temporaryCachePath,"tidebite-network-check.json");
            config=new GameConfig{Mode=GameMode.Arena,RoundDuration=8,MaxFish=8,Population=6,FoodCount=128,OceanFeatures=false};
            server=NetworkSession.Start(config,Endpoint,true,validationPeers:true);
        }
        private void Update()
        {
            if(finished)return;
            try
            {
                float dt=Time.unscaledDeltaTime,now=Time.realtimeSinceStartup;
                server?.Advance(dt);first?.Advance(dt);late?.Advance(dt);reconnected?.Advance(dt);
                if(now-started>40)throw new Exception("Timed out at stage "+stage);
                if(now-printed>5){printed=now;Debug.Log($"TIDEBITE NETWORK_CHECK stage={stage} seconds={now-started:F1}");}
                switch(stage)
                {
                    case 0:
                        if(!server.Ready)return;
                        first=NetworkSession.Start(config.Copy(),Endpoint,false,1,2,true);MoveTo(1);break;
                    case 1:
                        if(!first.Ready||first.ReceivedSnapshots<2)return;
                        int id=first.LocalId;var f=server.Fish[id];
                        Require(f.Species==1&&f.Skin==2,"Appearance selection not replicated");report.appearance=true;
                        server.Fish[id].Mass=3;server.Fish[id].RoundScore=500;
                        first.ChooseEvolution(Adaptation.Hunter);MoveTo(2);break;
                    case 2:
                        if(server.Fish[first.LocalId].Hunter!=1||first.Fish[first.LocalId].Hunter!=1)return;
                        report.evolution=true;first.ChooseEvolution(Adaptation.Feeder);first.ChooseEvolution((Adaptation)255);MoveTo(3);break;
                    case 3:
                        if(now-stageAt<.25f||server.Round.Elapsed<2.5f)return;
                        Require(server.Fish[first.LocalId].Feeder==0,"Unearned evolution accepted");report.overspendRejected=true;
                        late=NetworkSession.Start(config.Copy(),Endpoint,false,0,3,true);MoveTo(4);break;
                    case 4:
                        if(!late.Ready||late.ReceivedSnapshots<2)return;
                        Require(late.Fish[late.LocalId].WaitingForRound&&!late.Fish[late.LocalId].Alive,"Late join did not wait");
                        report.lateJoinWaited=true;late.Respawn();initialRound=server.Round.Id;MoveTo(5);break;
                    case 5:
                        if(first.Round.Phase!=RoundPhase.Results||late.Round.Phase!=RoundPhase.Results)return;
                        Require(first.Round.WinnerId==first.LocalId&&first.Round.LocalRank==1,"Round result/rank mismatch");
                        Require(server.Fish[late.LocalId].WaitingForRound,"Early respawn bypassed late-join wait");report.results=true;MoveTo(6);break;
                    case 6:
                        if(server.Round.Id<=initialRound||first.Round.Id<=initialRound||late.Round.Id<=initialRound)return;
                        Require(first.Fish[first.LocalId].Alive&&late.Fish[late.LocalId].Alive&&!late.Fish[late.LocalId].WaitingForRound,"Next round failed to release waiting player");
                        Require(first.Fish[first.LocalId].RoundScore==0&&first.Fish[first.LocalId].Hunter==0&&first.Fish[first.LocalId].Skin==2,"Round reset or cosmetic preservation failed");
                        report.roundReset=true;
                        for(int i=0;i<server.Fish.Length;i++)if(server.Fish[i].Occupied)
                        {
                            server.Fish[i].Position=server.Fish[i].PreviousPosition=new V2(40+i,40);server.Fish[i].Velocity=default;
                            if(server.Fish[i].IsBot)combatFinisher=i;
                        }
                        for(int i=0;i<server.Food.Length;i++)server.Food[i].RespawnRemaining=100000;
                        Place(first.LocalId,1,default);Place(late.LocalId,8,new V2(0,1.3f));
                        first.Event+=e=>{if(e.Kind==WorldEventKind.Assist&&e.Actor==first.LocalId)report.assistEvent=true;};
                        MoveTo(60);break;
                    case 60:
                        if(first.Fish[first.LocalId].Mass!=1||first.Fish[late.LocalId].Mass!=8||V2.Distance(first.Fish[first.LocalId].Position,default)>.1f)return;
                        first.Submit(new FishCommand(default,true));MoveTo(61);break;
                    case 61:
                        if(server.Fish[late.LocalId].Health>=100)return;
                        Require(server.Fish[late.LocalId].Alive&&server.Fish[first.LocalId].Mass==1,"Small attack should damage without instant growth");report.smallDamage=true;
                        expectedAssistScore=(int)(800*.7f*(100-server.Fish[late.LocalId].Health)/100);
                        Place(combatFinisher,20,new V2(5,0));
                        server.Fish[late.LocalId].Position=server.Fish[late.LocalId].PreviousPosition=new V2(5,1.3f);
                        // Inject ordinary authority input through Step, so effects use the production broadcast path.
                        server.Fish[combatFinisher].IsBot=false;
                        server.ServerWorld.SetInput(combatFinisher,new FishCommand(default,true));
                        MoveTo(62);break;
                    case 62:
                        if(server.Fish[late.LocalId].Alive)
                        {server.ServerWorld.SetInput(combatFinisher,new FishCommand(default,true));return;}
                        server.Fish[combatFinisher].IsBot=true;
                        if(first.Fish[first.LocalId].Assists!=1||!report.assistEvent)return;
                        var contributor=first.Fish[first.LocalId];
                        Require(contributor.Score==expectedAssistScore&&contributor.RoundScore==expectedAssistScore&&contributor.Mass>1&&contributor.RoundAssists==1,"Contribution score/growth snapshot mismatch");
                        report.contribution=true;oldSnapshots=first.ReceivedSnapshots;first.Dispose();first=null;MoveTo(7);break;
                    case 7:
                        if(server.ServerWorld.Humans!=1)return;
                        reconnected=NetworkSession.Start(config.Copy(),Endpoint,false,1,1,true);MoveTo(8);break;
                    case 8:
                        if(!reconnected.Ready||reconnected.ReceivedSnapshots<4)return;
                        Require(server.ServerWorld.Humans==2&&reconnected.Fish[reconnected.LocalId].Skin==1,"Reconnect state failed");
                        report.reconnect=true;report.snapshots=oldSnapshots+late.ReceivedSnapshots+reconnected.ReceivedSnapshots;Finish(true,null);break;
                }
            }
            catch(Exception e){Finish(false,e.Message);}
        }
        private void Place(int id,float mass,V2 position)
        {
            server.Fish[id].Position=server.Fish[id].PreviousPosition=position;server.Fish[id].Velocity=default;
            server.Fish[id].Heading=0;server.Fish[id].Mass=mass;server.Fish[id].Health=100;server.Fish[id].Protection=0;
            server.Fish[id].HurtProtection=0;server.Fish[id].BiteCooldown=0;server.Fish[id].BiteRemaining=0;
        }
        private void MoveTo(int next){stage=next;stageAt=Time.realtimeSinceStartup;}
        private void Require(bool condition,string message){report.checks++;if(!condition)throw new Exception(message);}
        private void Finish(bool pass,string error)
        {
            if(finished)return;finished=true;report.passed=pass;report.error=error;report.seconds=Time.realtimeSinceStartup-started;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath)));File.WriteAllText(resultPath,JsonUtility.ToJson(report,true));
            Debug.Log("TIDEBITE NETWORK_CHECK_"+(pass?"PASS":"FAIL")+" "+JsonUtility.ToJson(report));
            Close();StartCoroutine(Quit(pass?0:3));
        }
        private System.Collections.IEnumerator Quit(int code){yield return null;Application.Quit(code);}
        private void Close(){first?.Dispose();late?.Dispose();reconnected?.Dispose();server?.Dispose();}
        private void OnDestroy(){Close();}
        [Serializable]private sealed class Report
        {public bool passed,appearance,evolution,overspendRejected,lateJoinWaited,results,roundReset,reconnect,smallDamage,contribution,assistEvent;public int checks,snapshots;public float seconds;public string error;}
    }
}

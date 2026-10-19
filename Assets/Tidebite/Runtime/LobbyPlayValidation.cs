using System;
using System.Collections;
using System.IO;
using Tidebite.Core;
using UnityEngine;

namespace Tidebite
{
    // One headless Development process exercises real HTTP allocation and real UDP admissions.
    public sealed class LobbyPlayValidation:MonoBehaviour
    {
        private NetworkSession server,first,second,probe,reconnected;
        private GameConfig config;
        private GameApp app;
        private string url,endpoint,resultPath;
        private float started;
        private bool finished;
        private readonly Report report=new Report();
        private void Start()
        {
            started=Time.realtimeSinceStartup;url=OnlineService.ClientUrl();app=GetComponent<GameApp>();
            string[] args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--result-file"),p=Array.IndexOf(args,"--check-port");
            resultPath=i>=0?args[i+1]:Path.GetFullPath("TestResults/lobby-network.json");endpoint="127.0.0.1:"+(p>=0?args[p+1]:"27888");
            string room=Environment.GetEnvironmentVariable("TIDEBITE_ROOM_ID"),key=Environment.GetEnvironmentVariable("TIDEBITE_ROOM_KEY");
            config=new GameConfig{Mode=GameMode.Arena,RoundDuration=30,MaxFish=8,Population=6,FoodCount=128,OceanFeatures=false};
            server=NetworkSession.Start(config,endpoint,true,validationPeers:true,admissionGate:new AdmissionGate(room,key));
            gameObject.AddComponent<ServerDirectoryAgent>().Begin(server,url,room,key);StartCoroutine(Run());
        }
        private void Update()
        {
            if(finished)return;
            try
            {float dt=Time.unscaledDeltaTime;server?.Advance(dt);first?.Advance(dt);second?.Advance(dt);probe?.Advance(dt);}
            catch(Exception e){Finish(false,e.GetType().Name);}
            if(Time.realtimeSinceStartup-started>40)Finish(false,"Lobby validation timed out");
        }
        private IEnumerator Wait(Func<bool> ready)
        {
            float deadline=Time.realtimeSinceStartup+6;
            while(!finished&&!ready()&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!finished&&!ready())Finish(false,"Expected network state did not arrive");
        }
        private IEnumerator Reserve(Action<MatchReply> receive)
        {
            float deadline=Time.realtimeSinceStartup+10;
            while(!finished&&Time.realtimeSinceStartup<deadline)
            {
                using(var request=OnlineService.Post(url+"/v1/matches",new MatchRequest{protocol=NetworkSession.Protocol}))
                {
                    yield return request.SendWebRequest();MatchReply reply=null;
                    try{reply=JsonUtility.FromJson<MatchReply>(request.downloadHandler.text);}catch(ArgumentException){}
                    if(reply?.status=="assigned"){receive(reply);yield break;}
                }
                yield return new WaitForSecondsRealtime(.25f);
            }
            if(!finished)Finish(false,"Central assignment unavailable");
        }
        private IEnumerator Rejected(string ticket)
        {
            int before=server.RejectedAdmissions,humans=server.ServerWorld.Humans;
            probe=NetworkSession.Start(config.Copy(),endpoint,false,validationPeers:true,admissionTicket:ticket);
            yield return Wait(()=>server.RejectedAdmissions>before);
            if(!finished)Check(!probe.Ready&&server.ServerWorld.Humans==humans,"Invalid admission added a player");
            probe.Dispose();probe=null;yield return null;
        }
        private IEnumerator Run()
        {
            yield return Wait(()=>server.Ready);if(finished)yield break;
            yield return Rejected(null);if(finished)yield break;report.missingRejected=true;
            yield return Rejected("forged.ticket");if(finished)yield break;report.forgeryRejected=true;
            MatchReply one=null;yield return Reserve(reply=>one=reply);if(finished)yield break;
            if(!Check(one.endpoint==endpoint&&one.roomId==Environment.GetEnvironmentVariable("TIDEBITE_ROOM_ID"),"Assignment routed to wrong room"))yield break;
            first=NetworkSession.Start(config.Copy(),one.endpoint,false,1,2,true,one.ticket);
            yield return Wait(()=>first.Ready&&first.ReceivedSnapshots>=3);if(finished)yield break;
            report.assigned=true;
            yield return Rejected(one.ticket);if(finished)yield break;report.replayRejected=true;
            MatchReply two=null;yield return Reserve(reply=>two=reply);if(finished)yield break;
            second=NetworkSession.Start(config.Copy(),two.endpoint,false,0,1,true,two.ticket);
            yield return Wait(()=>second.Ready&&second.ReceivedSnapshots>=3);if(finished)yield break;
            if(!Check(server.ServerWorld.Humans==2&&first.LocalId!=second.LocalId,"Two clients failed to share authority"))yield break;
            report.twoPlayers=true;
            V2 origin=server.Fish[first.LocalId].Position;first.Submit(new FishCommand(new V2(0,1)));
            float until=Time.realtimeSinceStartup+1;while(!finished&&Time.realtimeSinceStartup<until)yield return null;
            if(finished||!Check(V2.Distance(origin,server.Fish[first.LocalId].Position)>.5f,"Client input did not move server actor"))yield break;
            report.authority=true;report.snapshots=first.ReceivedSnapshots;first.Dispose();first=null;
            yield return Wait(()=>server.ServerWorld.Humans==1);if(finished)yield break;
            app.Online();app.CancelOnline();yield return null;
            if(!Check(!app.Matching&&app.Session==null&&server.ServerWorld.Humans==1,"Cancelled menu request created a connection"))yield break;
            report.cancel=true;app.Online();
            yield return Wait(()=>app.Session is NetworkSession n&&n.Ready&&n.ReceivedSnapshots>=3);if(finished)yield break;
            reconnected=(NetworkSession)app.Session;report.gameAppFlow=true;
            if(!Check(server.ServerWorld.Humans==2,"Fresh-ticket reconnect failed"))yield break;
            report.reconnect=true;report.snapshots+=second.ReceivedSnapshots+reconnected.ReceivedSnapshots;Finish(true,null);
        }
        private bool Check(bool condition,string message)
        {report.checks++;if(!condition)Finish(false,message);return condition;}
        private void Finish(bool passed,string error)
        {
            if(finished)return;finished=true;report.passed=passed;report.error=error;report.protocol=NetworkSession.Protocol;report.seconds=Time.realtimeSinceStartup-started;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath)));File.WriteAllText(resultPath,JsonUtility.ToJson(report,true));
            Debug.Log("TIDEBITE LOBBY_CHECK_"+(passed?"PASS":"FAIL")+" "+JsonUtility.ToJson(report));
            Close();StartCoroutine(Quit(passed?0:3));
        }
        private IEnumerator Quit(int code){yield return null;Application.Quit(code);}
        private void Close(){probe?.Dispose();first?.Dispose();second?.Dispose();reconnected?.Dispose();server?.Dispose();}
        private void OnDestroy()=>Close();
        [Serializable]private sealed class Report
        {public bool passed,assigned,missingRejected,forgeryRejected,replayRejected,twoPlayers,authority,reconnect,cancel,gameAppFlow;public int checks,snapshots,protocol;public float seconds;public string error;}
    }
}

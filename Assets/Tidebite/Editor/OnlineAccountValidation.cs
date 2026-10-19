using System;
using System.IO;
using System.Linq;
using Tidebite.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tidebite.Editor
{
    public static class OnlineAccountValidation
    {
        [MenuItem("Tidebite/Validate online account and outbox")]
        public static void Run()
        {
            int checks=0;
            Action<bool,string> check=(value,message)=>{checks++;if(!value)throw new Exception("Online account: "+message);};
            string folder=Path.GetFullPath("TestResults/online-account/"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);string path=Path.Combine(folder,"outbox.json");
            var report=new OnlineRoundReport{roundId="instance:1",endedAt=DateTimeOffset.UtcNow.ToUnixTimeSeconds(),durationSeconds=240,participants=new[]{new OnlineParticipant{admissionId="abc",score=800,kills=3,assists=2,level=8,rank=1,completed=true}}};
            var outbox=new RoundOutbox(path);check(outbox.Count==0,"new queue");check(outbox.Enqueue(report),"persist before sending");
            var recovered=new RoundOutbox(path);check(recovered.Count==1&&recovered.Peek().roundId==report.roundId&&recovered.Peek().participants[0].score==800,"result survives process restart");
            check(JsonUtility.ToJson(recovered.Peek())==JsonUtility.ToJson(report),"retry retains exact payload");
            recovered.Acknowledge();check(new RoundOutbox(path).Count==0,"acknowledgement persists");
            recovered.Enqueue(report);recovered.Enqueue(report);File.WriteAllText(path,"{broken");check(new RoundOutbox(path).Count==1,"corrupt primary recovers backup");
            check(OnlineService.ValidateUrl("https://game.example/",true)=="https://game.example","production HTTPS");
            bool rejected=false;try{OnlineService.ValidateUrl("http://game.example",true);}catch(ArgumentException){rejected=true;}check(rejected,"release rejects HTTP");
            rejected=false;try{OnlineService.ValidateUrl("https://user:secret@game.example",true);}catch(ArgumentException){rejected=true;}check(rejected,"reject URL credentials");
            var root=new GameObject("Account UI regression");
            try
            {
                var app=root.AddComponent<GameApp>();var view=root.AddComponent<OceanView>();view.Build(new GameConfig{MaxFish=2,Population=2,FoodCount=1});
                var ui=root.AddComponent<GameUI>();ui.Build(app,root.AddComponent<TouchControls>(),view);
                var inputs=root.GetComponentsInChildren<InputField>(true);
                check(inputs.Length==6,"signup profile and security inputs");
                check(inputs.Count(i=>i.contentType==InputField.ContentType.Password)==3,"all passwords masked");
                var buttons=root.GetComponentsInChildren<Button>(true);
                check(buttons.Any(b=>b.name=="Solo")&&buttons.Any(b=>b.name=="Online"),"solo and multiplayer remain available");
                check(buttons.Any(b=>b.name=="Register")&&buttons.Any(b=>b.name=="Login")&&buttons.Any(b=>b.name=="Delete account"),"membership controls available");
                var rows=root.GetComponentsInChildren<Text>(true).First(t=>t.name=="Records and leaders");check(!rows.supportRichText,"public nicknames render as plain text");
                check(root.GetComponentsInChildren<ScrollRect>(true).Any(s=>s.vertical&&!s.horizontal),"records and top 50 can scroll on mobile");
                check(!buttons.First(b=>b.name=="Register").gameObject.activeInHierarchy,"account dialog starts closed");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            File.WriteAllText("TestResults/online-account.json","{\"passed\":true,\"checks\":"+checks+",\"serversStarted\":false}");
            Debug.Log("TIDEBITE ONLINE_ACCOUNT_VALIDATED checks="+checks+" serversStarted=false");
        }
    }
}

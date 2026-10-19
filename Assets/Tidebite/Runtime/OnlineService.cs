using System;
using System.Collections;
using System.Text;
using Tidebite.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Tidebite
{
    public static class OnlineService
    {
        [Serializable] private sealed class Settings { public string matchmakerUrl; }
        public static string ClientUrl()
        {
            string[] args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--matchmaker");
            string url=i>=0&&i+1<args.Length?args[i+1]:null;
            var settings=Resources.Load<TextAsset>("OnlineSettings");
            if(string.IsNullOrWhiteSpace(url)&&settings!=null)url=JsonUtility.FromJson<Settings>(settings.text)?.matchmakerUrl;
            if(string.IsNullOrWhiteSpace(url)&&Debug.isDebugBuild)url="http://127.0.0.1:8080";
            return ValidateUrl(url,!Debug.isDebugBuild);
        }
        public static string ValidateUrl(string url,bool requireHttps)
        {
            if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"&&(requireHttps||uri.Scheme!="http")||
                !string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("온라인 서비스가 아직 설정되지 않았습니다.");
            return url.TrimEnd('/');
        }
        public static UnityWebRequest Post(string url,object body,string key=null)
        {
            var request=new UnityWebRequest(url,"POST");request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
            request.downloadHandler=new DownloadHandlerBuffer();request.timeout=8;
            request.SetRequestHeader("Content-Type","application/json");
            if(key!=null)request.SetRequestHeader("Authorization","Bearer "+key);
            return request;
        }
        public static IEnumerator Release(string url,string id,string ticket)
        {
            using(var request=Post(url+"/v1/matches/cancel",new CancelMatchRequest{reservationId=id,ticket=ticket}))
                yield return request.SendWebRequest();
        }
    }

    public sealed class ServerDirectoryAgent:MonoBehaviour
    {
        private NetworkSession observed;
        private RoundOutbox pending;
        public void Begin(NetworkSession server,string url,string room,string key)
        {
            string path=Environment.GetEnvironmentVariable("TIDEBITE_RESULT_OUTBOX");
            pending=new RoundOutbox(string.IsNullOrEmpty(path)?System.IO.Path.Combine(Application.persistentDataPath,"online-outbox-"+room+".json"):path);
            observed=server;server.RoundCompleted+=Enqueue;StartCoroutine(Publish(server,OnlineService.ValidateUrl(url,false),room,key));
        }
        private void Enqueue(OnlineRoundReport report)
        {if(!pending.Enqueue(report))Debug.LogError("TIDEBITE RESULT_NOT_PERSISTED");}
        private void OnDestroy(){if(observed!=null)observed.RoundCompleted-=Enqueue;}
        private IEnumerator Publish(NetworkSession server,string url,string room,string key)
        {
            bool warned=false;
            while(server!=null)
            {
                if(server.Ready)
                {
                    var round=server.ServerWorld.Round;
                    var heartbeat=new RoomHeartbeat{protocol=NetworkSession.Protocol,mode="arena",humans=server.ServerWorld.Humans,capacity=server.Settings.MaxFish,
                        accepting=server.ServerWorld.Humans==0||round.Phase==RoundPhase.Growth&&round.Elapsed<=Math.Min(10,round.Duration*.25f),admissions=server.ActiveAdmissions()};
                    using(var request=OnlineService.Post(url+"/v1/rooms/"+room+"/heartbeat",heartbeat,key))
                    {
                        yield return request.SendWebRequest();
                        if(request.result!=UnityWebRequest.Result.Success&&!warned){Debug.LogWarning("TIDEBITE DIRECTORY_UNAVAILABLE code="+request.responseCode);warned=true;}
                        if(request.result==UnityWebRequest.Result.Success)
                        {
                            server.AcknowledgeAdmissions(heartbeat.admissions);
                            if(warned){Debug.Log("TIDEBITE DIRECTORY_RECOVERED");warned=false;}
                        }
                    }
                    // Retries reuse the exact round ID and payload; the database deduplicates them.
                    if(pending.Count>0)using(var request=OnlineService.Post(url+"/v1/rooms/"+room+"/results",pending.Peek(),key))
                    {
                        yield return request.SendWebRequest();
                        if(request.result==UnityWebRequest.Result.Success){pending.Acknowledge();Debug.Log("TIDEBITE ROUND_RECORDED");}
                        else if(request.responseCode==400||request.responseCode==409)
                        {pending.Acknowledge();Debug.LogError("TIDEBITE RESULT_REJECTED code="+request.responseCode);}
                    }
                }
                yield return new WaitForSecondsRealtime(2);
            }
        }
    }
}

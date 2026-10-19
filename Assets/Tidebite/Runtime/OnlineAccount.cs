using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Tidebite
{
    [Serializable] public sealed class OnlineProfile
    {public string id,displayName;public int avatar,games,wins,bestScore,kills,assists;public long createdAt;}
    [Serializable] public sealed class OnlineRecord
    {public long id,playedAt;public int score,kills,assists,level,rank;public bool completed;}
    [Serializable] public sealed class OnlineLeader
    {public string id,displayName;public int rank,avatar,bestScore,wins,games;}
    public sealed class OnlineAccount:MonoBehaviour
    {
        [Serializable] private sealed class Credentials {public string login,password,displayName;}
        [Serializable] private sealed class AuthReply {public string token;public long expiresAt;public OnlineProfile profile;}
        [Serializable] private sealed class ErrorReply {public string message;}
        [Serializable] private sealed class RecordReply {public OnlineRecord[] records;public long nextBefore;}
        [Serializable] private sealed class LeaderReply {public OnlineLeader[] entries;}
        [Serializable] private sealed class Edit {public string displayName;public int avatar;}
        [Serializable] private sealed class Password {public string password,newPassword;}
        public string Token{get;private set;}
        public OnlineProfile Profile{get;private set;}
        public OnlineRecord[] Records{get;private set;}=Array.Empty<OnlineRecord>();
        public OnlineLeader[] Leaders{get;private set;}=Array.Empty<OnlineLeader>();
        public bool Busy{get;private set;}
        public bool SignedIn=>!string.IsNullOrEmpty(Token)&&DateTimeOffset.UtcNow.ToUnixTimeSeconds()<expires;
        public string Message{get;private set;}="함께 플레이 기록은 계정에 저장됩니다.";
        private long expires;
        private string url;
        private UnityWebRequest active;
        public void Authenticate(bool register,string login,string password,string nickname)
        {if(!Busy)StartCoroutine(SignIn(register,new Credentials{login=login,password=password,displayName=nickname}));}
        private bool Configure()
        {try{url=OnlineService.ClientUrl();return true;}catch(ArgumentException e){Message=e.Message;return false;}}
        private IEnumerator SignIn(bool register,Credentials credentials)
        {
            if(!Configure())yield break;Busy=true;Message=register?"계정을 만드는 중…":"로그인 중…";
            using(var request=OnlineService.Post(url+"/v1/auth/"+(register?"register":"login"),credentials))
            {
                active=request;yield return request.SendWebRequest();active=null;credentials.password=null;
                if(Success(request))
                {
                    var reply=Read<AuthReply>(request.downloadHandler.text);
                    if(reply!=null&&reply.profile!=null&&!string.IsNullOrEmpty(reply.token))
                    {Records=Array.Empty<OnlineRecord>();Token=reply.token;expires=reply.expiresAt;Profile=reply.profile;Message="반가워요, "+Profile.displayName+"!";}
                    else Message="서버의 로그인 응답을 확인할 수 없습니다.";
                }
            }
            Busy=false;if(SignedIn)Refresh();
        }
        public void Forget(){Token=null;Profile=null;Records=Array.Empty<OnlineRecord>();}
        private bool Success(UnityWebRequest request)
        {
            if(request.result==UnityWebRequest.Result.Success)return true;
            if(request.responseCode==401)Forget();
            ErrorReply error=null;try{error=JsonUtility.FromJson<ErrorReply>(request.downloadHandler.text);}catch(ArgumentException){}
            Message=error?.message??"서버에 연결할 수 없습니다. 잠시 후 다시 시도해 주세요.";return false;
        }
        public void Refresh(string period="week") {if(!Busy&&Configure())StartCoroutine(Load(period));}
        private IEnumerator Load(string period)
        {
            Busy=true;
            if(SignedIn)
            {
                using(var r=Get("/v1/profile")){active=r;yield return r.SendWebRequest();active=null;if(Success(r))Profile=Read<OnlineProfile>(r.downloadHandler.text);}
                if(SignedIn)using(var r=Get("/v1/records?limit=20")){active=r;yield return r.SendWebRequest();active=null;if(Success(r))Records=Read<RecordReply>(r.downloadHandler.text)?.records??Array.Empty<OnlineRecord>();}
            }
            using(var r=Get("/v1/leaderboard?period="+(period=="all"?"all":"week"))){active=r;yield return r.SendWebRequest();active=null;if(Success(r))Leaders=Read<LeaderReply>(r.downloadHandler.text)?.entries??Array.Empty<OnlineLeader>();}
            Busy=false;
        }
        private UnityWebRequest Get(string path)
        {var r=UnityWebRequest.Get(url+path);r.timeout=8;if(SignedIn)r.SetRequestHeader("Authorization","Bearer "+Token);return r;}
        public void EditProfile(string name,int avatar)
        {if(!Busy&&SignedIn&&Configure())StartCoroutine(Mutate("/v1/profile",new Edit{displayName=name,avatar=avatar},"PUT",false));}
        public void Logout(){if(!Busy&&SignedIn&&Configure())StartCoroutine(Mutate("/v1/auth/logout",new object(),"POST",true));}
        public void ChangePassword(string current,string next)
        {if(!Busy&&SignedIn&&Configure())StartCoroutine(Mutate("/v1/auth/password",new Password{password=current,newPassword=next},"POST",true));}
        public void DeleteAccount(string current)
        {if(!Busy&&SignedIn&&Configure())StartCoroutine(Mutate("/v1/account/delete",new Password{password=current},"POST",true));}
        private IEnumerator Mutate(string path,object body,string method,bool clear)
        {
            Busy=true;
            using(var r=OnlineService.Post(url+path,body,Token))
            {
                r.method=method;active=r;yield return r.SendWebRequest();active=null;
                if(Success(r))
                {
                    if(clear){Token=null;Profile=null;Records=Array.Empty<OnlineRecord>();Message=path.EndsWith("delete")?"계정과 온라인 기록을 삭제했습니다.":path.EndsWith("password")?"비밀번호를 변경했습니다. 다시 로그인해 주세요.":"로그아웃했습니다.";}
                    else{Profile=Read<OnlineProfile>(r.downloadHandler.text);Message="프로필을 저장했습니다.";}
                }
            }
            Busy=false;
        }
        private T Read<T>(string json) where T:class
        {try{return JsonUtility.FromJson<T>(json);}catch(ArgumentException){Message="서버 응답을 확인할 수 없습니다.";return null;}}
        private void OnDestroy(){active?.Abort();active?.Dispose();Token=null;}
    }
}

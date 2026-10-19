using System;
using Tidebite.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

namespace Tidebite
{
    public sealed class GameApp : MonoBehaviour
    {
        public GameConfig Config { get; } = new GameConfig{Mode=GameMode.Arena};
        public IGameSession Session { get; private set; }
        public OnlineAccount Account{get;private set;}
        public bool Menu { get; private set; } = true;
        public bool Paused { get; private set; }
        public bool Matching { get; private set; }
        public string OnlineStatus { get; private set; }="온라인 바다 자동 배정 · 빈자리는 봇과 함께";
        private Coroutine matchRoutine;
        private UnityWebRequest matchRequest;
        private string directoryUrl,reservationId,reservationTicket;
        private int connectionAttempt;
        private OceanView ocean;
        private GameUI ui;
        private TouchControls controls;
        private bool headless,autoPilot,quitting,transitioning,autoEvolution=true;
        private float runUntil,elapsed,autoAction,printAt;
        private string resultFile;
        public PlayerProgress Progress{get;private set;}=new PlayerProgress();
        private ProgressStore progressStore;
        private bool progressDirty,lifeRecorded;
        private int lifeGeneration=-1,lastCompletedRound=-1,lastLifeScore,lastLifeLevel=1,lifeFoods,lifeEats,lifeDodges;
        private float lifeSeconds,firstFood=-1,firstEat=-1,firstEvolution=-1,saveTimer,discoverTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot(){if(FindAnyObjectByType<GameApp>()==null)new GameObject("Tidebite").AddComponent<GameApp>();}
        private void Awake()
        {
            Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Application.runInBackground=true;
            Screen.sleepTimeout=SleepTimeout.NeverSleep;
            Account=gameObject.AddComponent<OnlineAccount>();
            var args=Environment.GetCommandLineArgs();
            if(Has(args,"--endless"))Config.Mode=GameMode.Endless;
            if(float.TryParse(Arg(args,"--round-seconds","240"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float roundSeconds)&&roundSeconds>=1)Config.RoundDuration=roundSeconds;
            resultFile=Arg(args,"--result-file",null);
            headless=Has(args,"-batchmode");autoPilot=Has(args,"--autoplay");
            autoEvolution=!Has(args,"--hold-evolution");
            float.TryParse(Arg(args,"--duration","0"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out runUntil);
            if(Debug.isDebugBuild&&Has(args,"--network-check")){headless=true;gameObject.AddComponent<NetworkPlayValidation>();return;}
            if(Debug.isDebugBuild&&Has(args,"--lobby-check")){headless=true;gameObject.AddComponent<LobbyPlayValidation>();return;}
            if(Has(args,"--server"))
            {
                headless=true;Menu=false;Application.targetFrameRate=30;
                string directory=Environment.GetEnvironmentVariable("TIDEBITE_DIRECTORY_URL"),room=Environment.GetEnvironmentVariable("TIDEBITE_ROOM_ID"),key=Environment.GetEnvironmentVariable("TIDEBITE_ROOM_KEY");
                try
                {
                    var gate=string.IsNullOrEmpty(directory)?null:new AdmissionGate(room,key);
                    if(gate!=null)Config.Mode=GameMode.Arena;
                    Session=NetworkSession.Start(Config,Arg(args,"--port","7777").Insert(0,"127.0.0.1:"),true,admissionGate:gate);
                    if(gate!=null)gameObject.AddComponent<ServerDirectoryAgent>().Begin((NetworkSession)Session,directory,room,key);
                }
                catch(Exception){Debug.LogError("TIDEBITE SERVER_CONFIGURATION_INVALID");Application.Quit(3);}
                return;
            }
            if(!headless)
            {
                string profilePath=Arg(args,"--profile-path",autoPilot?System.IO.Path.Combine(Application.temporaryCachePath,"tidebite-playtest-progress.json"):System.IO.Path.Combine(Application.persistentDataPath,"progress-v1.json"));
                progressStore=new ProgressStore(profilePath);Progress=progressStore.Load();
                gameObject.AddComponent<FramePacing>();
                ocean=new GameObject("Ocean").AddComponent<OceanView>();ocean.Build(Config);
                controls=gameObject.AddComponent<TouchControls>();ui=gameObject.AddComponent<GameUI>();ui.Build(this,controls,ocean);
            }
            string connect=Arg(args,"--connect",null);
            if(connect!=null)
            {
                // Headless deployment tests obtain a real central ticket without exposing it in argv.
                if(headless&&Has(args,"--ticket-from-env"))ConnectAllocated(connect,Environment.GetEnvironmentVariable("TIDEBITE_TEST_TICKET"));
                else Connect(connect);
            }
            else if(Has(args,"--solo"))Solo();else{Home();if(Has(args,"--online"))Online();}
        }
        // FishNet registers generated serializers in AfterSceneLoad callbacks too.
        // Start runs after all of those callbacks, including our Boot/Awake pair.
        private void Start(){if(Debug.isDebugBuild)WireValidation.Run();}
        public void Solo(){CancelOnline();ChangeScene(()=>Switch(new SoloSession(Config,false,Progress.SelectedFamily,Progress.SelectedSkin),false));}
        public void Arena(){if(!Menu&&Session?.Round.Phase==RoundPhase.Results)RecordRetry("round");Config.Mode=GameMode.Arena;Solo();}
        public void Endless(){Config.Mode=GameMode.Endless;Solo();}
        public void Home(){CancelOnline();ChangeScene(()=>Switch(new SoloSession(Config,true),true));}
        public void Online()
        {
            if(Matching)return;
            if(!Account.SignedIn){OnlineStatus="로그인하고 함께 헤엄치세요. 혼자하기는 바로 시작할 수 있어요.";ui?.ShowAccount();return;}
            try{directoryUrl=OnlineService.ClientUrl();}
            catch(ArgumentException e){OnlineStatus=e.Message;ui?.Notify(e.Message);return;}
            Config.Mode=GameMode.Arena;Matching=true;OnlineStatus="함께 헤엄칠 바다를 찾는 중…";
            matchRoutine=StartCoroutine(FindMatch());
        }
        private System.Collections.IEnumerator FindMatch()
        {
            using(var request=OnlineService.Post(directoryUrl+"/v1/matches",new MatchRequest{protocol=NetworkSession.Protocol},Account.Token))
            {
                matchRequest=request;yield return request.SendWebRequest();matchRequest=null;
                MatchReply reply=null;
                try{reply=JsonUtility.FromJson<MatchReply>(request.downloadHandler.text);}catch(ArgumentException){}
                Matching=false;matchRoutine=null;
                if(request.result!=UnityWebRequest.Result.Success||reply==null||reply.status!="assigned"||string.IsNullOrEmpty(reply.ticket))
                {
                    OnlineStatus=reply?.message??"온라인 바다에 연결할 수 없습니다. 잠시 후 다시 시도해 주세요.";
                    if(request.responseCode==401){Account.Forget();ui?.ShowAccount();}
                    ui?.Notify(OnlineStatus);yield break;
                }
                reservationId=reply.reservationId;reservationTicket=reply.ticket;
                OnlineStatus="바다를 찾았습니다. 입장하는 중…";
                ConnectAllocated(reply.endpoint,reply.ticket);
            }
        }
        public void CancelOnline()
        {
            connectionAttempt++;
            if(matchRoutine!=null){StopCoroutine(matchRoutine);matchRoutine=null;}
            if(matchRequest!=null){matchRequest.Abort();matchRequest.Dispose();matchRequest=null;}
            if(Matching)OnlineStatus="연결을 취소했습니다. 준비되면 다시 참가하세요.";
            Matching=false;
            if(!string.IsNullOrEmpty(reservationId)&&!quitting)
                StartCoroutine(OnlineService.Release(directoryUrl,reservationId,reservationTicket));
            reservationId=reservationTicket=null;
        }
        private void ChangeScene(Action change)
        {
            if(transitioning) return;
            if(Session==null || headless) { change(); return; }
            StartCoroutine(Transition(change));
        }
        private System.Collections.IEnumerator Transition(Action change)
        {
            transitioning=true; controls?.SetActive(false);
            for(float t=0;t<.14f;t+=Time.unscaledDeltaTime) { ui.SetTransition(t/.14f); yield return null; }
            ui.SetTransition(1); change(); yield return null;
            for(float t=0;t<.22f;t+=Time.unscaledDeltaTime) { ui.SetTransition(1-t/.22f); yield return null; }
            ui.SetTransition(0); transitioning=false; controls?.SetActive(!Menu&&!Paused);
        }
        public void Connect(string address)
        {CancelOnline();ConnectAllocated(address,null);}
        private void ConnectAllocated(string address,string ticket)
        {
            try
            {
                NetworkSession.ParseEndpoint(address,out _,out _);
                Session?.Dispose();Session=null; // Destroy old manager before the next frame creates a replacement.
                StartCoroutine(ConnectNextFrame(address,ticket,++connectionAttempt));
            }
            catch(Exception e){ui?.Notify(e.Message);}
        }
        private System.Collections.IEnumerator ConnectNextFrame(string address,string ticket,int attempt)
        {
            yield return null;if(attempt!=connectionAttempt)yield break;
            var args=Environment.GetCommandLineArgs();
            var network=NetworkSession.Start(Config,address,false,Progress.SelectedFamily,Progress.SelectedSkin,
                validationPeers:Debug.isDebugBuild&&Has(args,"--lobby-check"),admissionTicket:ticket);
            if(long.TryParse(Arg(args,"--latency-ms","0"),out var latency)&&latency>0)
                network.SimulateNetwork(latency,.03);
            Switch(network,false);
        }
        private void Switch(IGameSession session,bool menu)
        {
            if(Session!=null&&!Menu)FinishLife("leave");SaveProgress();
            Session?.Dispose();Session=session;Menu=menu;Paused=false;
            lifeGeneration=-1;lastCompletedRound=-1;lifeRecorded=false;
            Session.Event+=OnWorldEvent;
            ocean?.ResetPresentation();controls?.SetActive(!menu);ui?.SetMode(menu);elapsed=0;
        }
        public void Respawn()=>ChangeScene(()=>{
            if(Session!=null&&Session.LocalId>=0){var f=Session.Fish[Session.LocalId];if(!f.Alive&&!f.WaitingForRound&&f.RespawnRemaining<=0)RecordRetry("respawn");}
            Session?.Respawn();ocean?.ResetPresentation();});
        public void ChooseEvolution(Adaptation choice)=>Session?.ChooseEvolution(choice);
        public void SelectFamily(byte family)
        {if(Progress.SelectFamily(family)){progressDirty=true;SaveProgress();ui?.Notify("출발 어종 선택 완료");}else ui?.Notify("누적 포식 3마리로 산호 계열을 해금하세요");}
        public void EquipSkin(byte skin)
        {if(Progress.EquipSkin(skin)){progressDirty=true;SaveProgress();ui?.Notify("외형 적용 완료");}else ui?.Notify("도전 과제를 완료해 조개를 모으세요");}
        private void OnWorldEvent(WorldEvent e)
        {
            ocean?.React(e,Session.LocalId);ui?.React(e);
            if(Menu||progressStore==null||e.Actor!=Session.LocalId)return;
            var self=Session.Fish[Session.LocalId];if(!self.Occupied||self.WaitingForRound)return;EnsureLife(self.Generation);
            if(e.Kind==WorldEventKind.Food){Progress.TotalFood++;lifeFoods++;if(firstFood<0)firstFood=lifeSeconds;progressDirty=true;}
            if(e.Kind==WorldEventKind.Eat){Progress.TotalEats++;lifeEats++;if(firstEat<0)firstEat=lifeSeconds;progressDirty=true;}
            if(e.Kind==WorldEventKind.Dodge){Progress.TotalDodges++;lifeDodges++;progressDirty=true;}
            if(e.Kind==WorldEventKind.Escape){Progress.TotalEscapes++;progressDirty=true;}
            if(e.Kind==WorldEventKind.CleanEat){Progress.TotalCleanEats++;progressDirty=true;}
            if(e.Kind==WorldEventKind.Assist){Progress.TotalAssists++;progressDirty=true;}
        }
        public void TogglePause()
        {
            if(Menu)return;Paused=!Paused;controls?.SetActive(!Paused);ui?.ShowPause(Paused,Session.IsOnline);
        }
        public void ToggleSound(){if(ocean!=null){ocean.Sound=!ocean.Sound;ui.Notify(ocean.Sound?"소리 켜짐":"소리 꺼짐");}}
        private void Update()
        {
            float dt=Time.unscaledDeltaTime;elapsed+=dt;
            saveTimer+=dt;if(saveTimer>=20&&progressDirty)SaveProgress();
            if(Session==null)return;
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true)TogglePause();
            if(!Menu&&!Paused&&Keyboard.current!=null)
            {
                if(Keyboard.current.digit1Key.wasPressedThisFrame)ChooseEvolution(Adaptation.Hunter);
                if(Keyboard.current.digit2Key.wasPressedThisFrame)ChooseEvolution(Adaptation.Armor);
                if(Keyboard.current.digit3Key.wasPressedThisFrame)ChooseEvolution(Adaptation.Feeder);
            }
            var command=controls!=null?controls.Read():default;
            if(autoPilot && !Menu)
            {
                autoAction+=dt;command=new FishCommand(new V2(Mathf.Sin(elapsed*.57f),Mathf.Cos(elapsed*.43f)),autoAction>.7f,autoAction>2.8f);
                if(autoAction>2.8f)autoAction=0;
                if(Session.LocalId>=0 && !Session.Fish[Session.LocalId].Alive)Session.Respawn();
                if(autoEvolution&&Session.LocalId>=0&&EvolutionRules.Available(Session.Fish[Session.LocalId],Session.Settings)>0)
                {
                    var f=Session.Fish[Session.LocalId];for(int a=1;a<=3;a++)if(EvolutionRules.Rank(f,(Adaptation)a)<3){ChooseEvolution((Adaptation)a);break;}
                }
            }
            Session.Submit(Paused||Session.Round.Phase==RoundPhase.Results?default:command);
            long timing=Debug.isDebugBuild?System.Diagnostics.Stopwatch.GetTimestamp():0;
            if(!Paused||Session.IsOnline)Session.Advance(dt);
            if(Debug.isDebugBuild)FramePacing.RecordSimulation(timing);
            if(runUntil>0)
            {
                if(elapsed-printAt>5)
                {
                    printAt=elapsed;var n=Session as NetworkSession;
                    Debug.Log($"TIDEBITE METRICS seconds={elapsed:F1} ready={Session.Ready} snapshots={n?.ReceivedSnapshots} corrections={n?.Reconciliations}");
                }
                if(elapsed>=runUntil && !quitting)
                {
                    quitting=true;var n=Session as NetworkSession;
                    bool pass=Session.Ready && (n==null||n.IsServer||n.ReceivedSnapshots>10);
                    Debug.Log("TIDEBITE SMOKE_"+(pass?"PASS":"FAIL"));
                    if(resultFile!=null)System.IO.File.WriteAllText(resultFile,JsonUtility.ToJson(new SmokeResult{
                        passed=pass,ready=Session.Ready,snapshots=n?.ReceivedSnapshots??0,corrections=n?.Reconciliations??0,seconds=elapsed,slot=Session.LocalId,
                        completedRounds=Progress.TotalRounds,maxLevel=Progress.BestLevel,foods=Progress.TotalFood,eats=Progress.TotalEats}));
                    StartCoroutine(QuitNextFrame(pass?0:2));
                }
            }
        }
        private void LateUpdate()
        {
            if(headless || Session==null) return;
            float dt=Time.unscaledDeltaTime;
            // Simulation first, then rendered fish, camera and finally labels from the same pose.
            ocean.Display(Session,Paused&&!Session.IsOnline?0:dt,Menu);
            ObserveProgress(dt);
            ui.Refresh(Session,dt);
        }
        private void ObserveProgress(float dt)
        {
            if(Menu||progressStore==null||!Session.Ready||Session.LocalId<0)return;
            var me=Session.Fish[Session.LocalId];if(me.WaitingForRound)return;
            EnsureLife(me.Generation);
            var match=Session.Round;
            if(me.Alive&&!Paused&&match.Phase!=RoundPhase.Results)lifeSeconds+=dt;
            lastLifeScore=me.Score;lastLifeLevel=GrowthCurve.Level(me.Mass,Session.Settings);
            if(lastLifeScore>Progress.BestLifeScore||lastLifeLevel>Progress.BestLevel)progressDirty=true;
            Progress.Observe(lastLifeScore,lastLifeLevel);
            if(firstEvolution<0&&lastLifeLevel>=6)firstEvolution=lifeSeconds;
            discoverTimer+=dt;
            if(discoverTimer>.2f)
            {
                discoverTimer=0;ushort previous=Progress.Discovered;
                for(int i=0;i<Session.Fish.Length;i++)if(ocean.TryGetLabelPosition(i,out _))
                    Progress.Discover(FishEvolution.Index(GrowthCurve.Form(Session.Fish[i].Mass,Session.Settings),Session.Fish[i].Species));
                if(previous!=Progress.Discovered)progressDirty=true;
            }
            uint rewards=Progress.ClaimRewards();
            if(rewards!=0){progressDirty=true;int shells=0;for(int i=0;i<PlayerProgress.Goals.Length;i++)if((rewards&(1u<<i))!=0)shells+=PlayerProgress.Rewards[i];ui.Notify($"항해 도전 달성!  조개 +{shells}");SaveProgress();}
            if(!me.Alive)FinishLife("death");
            if(match.Phase==RoundPhase.Results&&match.Id!=lastCompletedRound)
            {
                lastCompletedRound=match.Id;Progress.RecordRound(me.RoundScore,match.LocalRank);FinishLife("round");
                Debug.Log($"TIDEBITE ROUND_COMPLETED id={match.Id} rank={match.LocalRank} score={me.RoundScore} winner={match.WinnerId}");
                Progress.ClaimRewards();progressDirty=true;SaveProgress();
            }
        }
        private void EnsureLife(int generation)
        {
            if(lifeGeneration==generation)return;
            FinishLife("respawn");lifeGeneration=generation;lifeRecorded=false;
            lifeSeconds=0;firstFood=firstEat=firstEvolution=-1;lifeFoods=lifeEats=lifeDodges=0;lastLifeScore=0;lastLifeLevel=1;
        }
        private void RecordRetry(string reason)
        {
            if(progressStore==null)return;Progress.TotalManualRetries++;progressDirty=true;
            progressStore.AppendRun(new RunRecord{endedUtc=DateTime.UtcNow.ToString("O"),mode=Session.Settings.Mode.ToString(),reason="manual-"+reason});SaveProgress();
        }
        private void FinishLife(string reason)
        {
            if(progressStore==null||lifeGeneration<0||lifeRecorded)return;
            lifeRecorded=true;Progress.RecordLife(lastLifeScore,lastLifeLevel);progressDirty=true;
            progressStore.AppendRun(new RunRecord{endedUtc=DateTime.UtcNow.ToString("O"),mode=Session.Settings.Mode.ToString(),reason=reason,
                seconds=lifeSeconds,firstFood=firstFood,firstEat=firstEat,firstEvolution=firstEvolution,score=lastLifeScore,level=lastLifeLevel,foods=lifeFoods,eats=lifeEats,dodges=lifeDodges});
            SaveProgress();
        }
        private void SaveProgress()
        {if(progressStore!=null&&progressDirty&&progressStore.Save(Progress)){progressDirty=false;saveTimer=0;}}
        private void OnApplicationPause(bool paused){if(paused){SaveProgress();controls?.ResetInput();if(Session!=null&&!Session.IsOnline&&!Menu&&!Paused)TogglePause();}}
        private void OnApplicationQuit(){quitting=true;CancelOnline();FinishLife("quit");SaveProgress();}
        private void OnDestroy(){CancelOnline();SaveProgress();Session?.Dispose();}
        [Serializable]private struct SmokeResult{public bool passed,ready;public int snapshots,corrections,slot,completedRounds,maxLevel,foods,eats;public float seconds;}
        private System.Collections.IEnumerator QuitNextFrame(int code){yield return null;Application.Quit(code);}
        private static bool Has(string[] args,string flag)=>Array.IndexOf(args,flag)>=0;
        private static string Arg(string[] args,string flag,string fallback){int i=Array.IndexOf(args,flag);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
    }
}

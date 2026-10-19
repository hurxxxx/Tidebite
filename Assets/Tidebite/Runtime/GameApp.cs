using System;
using Tidebite.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Tidebite
{
    public sealed class GameApp : MonoBehaviour
    {
        public GameConfig Config { get; } = new GameConfig();
        public IGameSession Session { get; private set; }
        public bool Menu { get; private set; } = true;
        public bool Paused { get; private set; }
        private OceanView ocean;
        private GameUI ui;
        private TouchControls controls;
        private bool headless,autoPilot,quitting,transitioning;
        private float runUntil,elapsed,autoAction,printAt;
        private string resultFile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot(){if(FindAnyObjectByType<GameApp>()==null)new GameObject("Tidebite").AddComponent<GameApp>();}
        private void Awake()
        {
            Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Application.runInBackground=true;
            Screen.sleepTimeout=SleepTimeout.NeverSleep;
            var args=Environment.GetCommandLineArgs();
            resultFile=Arg(args,"--result-file",null);
            headless=Has(args,"-batchmode");autoPilot=Has(args,"--autoplay");
            float.TryParse(Arg(args,"--duration","0"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out runUntil);
            if(Has(args,"--server"))
            {
                headless=true;Menu=false;Application.targetFrameRate=30;
                Session=NetworkSession.Start(Config,Arg(args,"--port","7777").Insert(0,"127.0.0.1:"),true);return;
            }
            if(!headless)
            {
                gameObject.AddComponent<FramePacing>();
                ocean=new GameObject("Ocean").AddComponent<OceanView>();ocean.Build(Config);
                controls=gameObject.AddComponent<TouchControls>();ui=gameObject.AddComponent<GameUI>();ui.Build(this,controls,ocean);
            }
            string connect=Arg(args,"--connect",null);
            if(connect!=null)Connect(connect);else if(Has(args,"--solo"))Solo();else Home();
        }
        // FishNet registers generated serializers in AfterSceneLoad callbacks too.
        // Start runs after all of those callbacks, including our Boot/Awake pair.
        private void Start(){if(Debug.isDebugBuild)WireValidation.Run();}
        public void Solo()=>ChangeScene(()=>Switch(new SoloSession(Config),false));
        public void Home()=>ChangeScene(()=>Switch(new SoloSession(Config,true),true));
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
        {
            try
            {
                NetworkSession.ParseEndpoint(address,out _,out _);
                Session?.Dispose();Session=null; // Destroy old manager before the next frame creates a replacement.
                StartCoroutine(ConnectNextFrame(address));
            }
            catch(Exception e){ui?.Notify(e.Message);}
        }
        private System.Collections.IEnumerator ConnectNextFrame(string address)
        {
            yield return null;var network=NetworkSession.Start(Config,address,false);
            var args=Environment.GetCommandLineArgs();
            if(long.TryParse(Arg(args,"--latency-ms","0"),out var latency)&&latency>0)
                network.SimulateNetwork(latency,.03);
            Switch(network,false);
        }
        private void Switch(IGameSession session,bool menu)
        {
            Session?.Dispose();Session=session;Menu=menu;Paused=false;
            Session.Event+=e=>{ocean?.React(e,Session.LocalId);ui?.React(e);};
            ocean?.ResetPresentation();controls?.SetActive(!menu);ui?.SetMode(menu);elapsed=0;
        }
        public void Respawn()=>ChangeScene(()=>{Session?.Respawn();ocean?.ResetPresentation();});
        public void TogglePause()
        {
            if(Menu)return;Paused=!Paused;controls?.SetActive(!Paused);ui?.ShowPause(Paused,Session.IsOnline);
        }
        public void ToggleSound(){if(ocean!=null){ocean.Sound=!ocean.Sound;ui.Notify(ocean.Sound?"소리 켜짐":"소리 꺼짐");}}
        private void Update()
        {
            float dt=Time.unscaledDeltaTime;elapsed+=dt;
            if(Session==null)return;
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true)TogglePause();
            var command=controls!=null?controls.Read():default;
            if(autoPilot && !Menu)
            {
                autoAction+=dt;command=new FishCommand(new V2(Mathf.Sin(elapsed*.57f),Mathf.Cos(elapsed*.43f)),autoAction>.7f,autoAction>2.8f);
                if(autoAction>2.8f)autoAction=0;
                if(Session.LocalId>=0 && !Session.Fish[Session.LocalId].Alive)Session.Respawn();
            }
            Session.Submit(Paused?default:command);
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
                        passed=pass,ready=Session.Ready,snapshots=n?.ReceivedSnapshots??0,corrections=n?.Reconciliations??0,seconds=elapsed,slot=Session.LocalId}));
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
            ui.Refresh(Session,dt);
        }
        private void OnApplicationPause(bool paused){if(paused){controls?.ResetInput();if(Session!=null&&!Session.IsOnline&&!Menu&&!Paused)TogglePause();}}
        private void OnDestroy(){Session?.Dispose();}
        [Serializable]private struct SmokeResult{public bool passed,ready;public int snapshots,corrections,slot;public float seconds;}
        private System.Collections.IEnumerator QuitNextFrame(int code){yield return null;Application.Quit(code);}
        private static bool Has(string[] args,string flag)=>Array.IndexOf(args,flag)>=0;
        private static string Arg(string[] args,string flag,string fallback){int i=Array.IndexOf(args,flag);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
    }
}

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tidebite
{
    public sealed class FramePacing : MonoBehaviour
    {
        private readonly float[] samples=new float[2048];
        private int count,width,height;
        private float elapsed,warmup=2;
        private GameApp app;
        private string sampleMode;
        private static long simulationTicks;
        private static int simulationSamples;
        public static void RecordSimulation(long started)
        {simulationTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-started;simulationSamples++;}

        private void Awake()
        {
            app=GetComponent<GameApp>();
            // Desktop software caps can jitter against the display's presentation clock.
            QualitySettings.vSyncCount=Application.isMobilePlatform?0:1;
            Application.targetFrameRate=Application.isMobilePlatform?60:-1;
        }

        private void LateUpdate()
        {
            if(width!=Screen.width || height!=Screen.height)
            {
                width=Screen.width; height=Screen.height;
                if(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
                {
                    float budget=Application.isMobilePlatform?2073600f:3686400f;
                    pipeline.renderScale=Mathf.Clamp(Mathf.Sqrt(budget/Mathf.Max(1,(float)width*height)),.5f,1);
                    pipeline.msaaSampleCount=1;
                    Debug.Log($"TIDEBITE RENDER size={width}x{height} scale={pipeline.renderScale:F2} vsync={QualitySettings.vSyncCount} refresh={Screen.currentResolution.refreshRateRatio.value:F1}");
                }
            }
            if(!Debug.isDebugBuild) return;
            string mode=app.Menu?"home":app.Paused?"paused":app.Session!=null&&app.Session.Round.Phase==Tidebite.Core.RoundPhase.Results?"results":app.Session!=null&&app.Session.LocalId>=0&&!app.Session.Fish[app.Session.LocalId].Alive?"dead":"play";
            if(mode!=sampleMode){sampleMode=mode;count=0;elapsed=0;warmup=1;simulationTicks=0;simulationSamples=0;}
            if(!Application.isFocused) { count=0; elapsed=0; warmup=1; return; }
            if(warmup>0) { warmup-=Time.unscaledDeltaTime; return; }
            float dt=Time.unscaledDeltaTime;
            if(count<samples.Length) samples[count++]=dt*1000;
            elapsed+=dt;
            if(elapsed<10 || count==0) return;
            float total=0; int slow=0;
            for(int i=0;i<count;i++) { total+=samples[i]; if(samples[i]>25) slow++; }
            Array.Sort(samples,0,count);
            double simulationMs=simulationTicks*1000.0/System.Diagnostics.Stopwatch.Frequency/Math.Max(1,simulationSamples);
            Debug.Log($"TIDEBITE FRAME_PACING mode={sampleMode} frames={count} fps={count*1000/total:F1} medianMs={samples[count/2]:F2} p95Ms={samples[(int)((count-1)*.95f)]:F2} maxMs={samples[count-1]:F2} over25ms={slow} simulationMs={simulationMs:F3}");
            simulationTicks=0;simulationSamples=0;
            count=0; elapsed=0;
        }
    }
}

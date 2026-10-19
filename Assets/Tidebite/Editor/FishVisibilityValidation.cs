using System;
using System.IO;
using Tidebite.Core;
using UnityEditor;
using UnityEngine;

namespace Tidebite.Editor
{
    public static class FishVisibilityValidation
    {
        [MenuItem("Tidebite/Validate fish visibility")]
        public static void Run()
        {
            var root=new GameObject("Fish visibility regression");
            var result=new Result();
            try
            {
                var view=root.AddComponent<OceanView>();var session=new SampleSession();
                view.Build(session.Settings);session.Place(0,default,32,Math2.Pi*.5f);
                float[] aspects={4f/3,16f/9,20f/9,32f/9};
                foreach(float aspect in aspects)
                {
                    view.Camera.aspect=aspect;view.Display(session,0,false);
                    float halfWidth=view.Camera.orthographicSize*aspect,halfHeight=view.Camera.orthographicSize;
                    for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                    {
                        session.Place(1,new V2(x*(halfWidth-1),y*(halfHeight-1)),1,Math2.Pi*.5f);
                        view.Display(session,1f/60,false);result.viewportCases++;
                        Check(view.TryGetLabelPosition(1,out _),"An on-screen fish was hidden",ref result);
                    }
                }
                session.Place(1,new V2(2,0),1,Math2.Pi*.5f);view.Display(session,1f/60,false);
                Check(view.TryGetLabelPosition(1,out _),"Visible fixture has no label anchor",ref result);
                session.Place(1,new V2(100,100),1,Math2.Pi*.5f);view.Display(session,1f/60,false);
                Check(!view.TryGetLabelPosition(1,out _),"Hidden fish retained its old label anchor",ref result);
                session.PresentationOffset=new V2(-98,-100);view.Display(session,1f/60,false);
                Check(view.TryGetLabelPosition(1,out _),"Culling used simulation pose instead of rendered pose",ref result);
                session.PresentationOffset=default;session.Place(1,new V2(2,0),1,Math2.Pi*.5f);
                session.Fish[1].Alive=false;view.Display(session,1f/60,false);
                Check(!view.TryGetLabelPosition(1,out _),"Dead fish retained a label anchor",ref result);
                session.Fish[1].Alive=true;session.Fish[1].Occupied=false;view.Display(session,1f/60,false);
                Check(!view.TryGetLabelPosition(1,out _),"Vacant slot retained a label anchor",ref result);
                Check(!view.TryGetLabelPosition(-1,out _)&&!view.TryGetLabelPosition(2,out _),"Invalid label index accepted",ref result);

                var fish=root.transform.Find("Fish 1").GetComponent<FishVisual>();
                var rig=fish.transform.Find("Side-profile fish rig");
                float[] steps={1f/15,1f/30,1f/60,1f/120};result.minimumBodyThickness=1;
                foreach(float dt in steps)
                {
                    fish.ResetPose();
                    for(int frame=0;frame<120;frame++)
                    {
                        session.Place(1,new V2(2,0),1,(frame/15%2==0?1:-1)*Math2.Pi*.5f);
                        view.Display(session,dt,false);result.turnFrames++;
                        float thickness=Mathf.Abs(rig.localScale.z);
                        result.minimumBodyThickness=Mathf.Min(result.minimumBodyThickness,thickness);
                        Check(thickness>=.8f,"Direction change collapsed the fish body",ref result);
                    }
                }
                result.passed=result.failures==0;
                Directory.CreateDirectory("TestResults");File.WriteAllText("TestResults/fish-visibility.json",JsonUtility.ToJson(result,true));
                Debug.Log("TIDEBITE FISH_VISIBILITY "+JsonUtility.ToJson(result));
                if(!result.passed)throw new Exception("Fish visibility regression failed");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        private static void Check(bool success,string message,ref Result result)
        {result.checks++;if(success)return;result.failures++;Debug.LogError(message);}
        [Serializable]
        private sealed class Result
        {public bool passed;public int viewportCases,turnFrames,checks,failures;public float minimumBodyThickness;}
        private sealed class SampleSession:IGameSession
        {
            public GameConfig Settings{get;}=new GameConfig{MaxFish=2,Population=2,FoodCount=1};
            public FishState[] Fish{get;}=new FishState[2];public FoodState[] Food{get;}=new FoodState[1];
            public V2 PresentationOffset;
            public int LocalId=>0;public bool IsOnline=>false;public bool Ready=>true;public string Status=>"Visibility regression";
            public event Action<WorldEvent> Event{add{} remove{}}
            public void Place(int id,V2 position,float mass,float heading)
            {Fish[id]=new FishState{Id=id,Generation=1,Occupied=true,Alive=true,Health=100,Mass=mass,Position=position,Heading=heading};}
            public FishState RenderFish(int id){var f=Fish[id];if(id==1)f.Position+=PresentationOffset;return f;}
            public void Submit(FishCommand command){}public void Advance(float elapsed){}public void Respawn(){}public void Dispose(){}
        }
    }
}

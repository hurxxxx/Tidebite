using System;
using System.Collections.Generic;
using System.IO;
using Tidebite.Core;
using UnityEditor;
using UnityEngine;

namespace Tidebite.Editor
{
    // Checks the real presentation, renderer bounds and shader displacement together.
    // Run with -batchmode -nographics -executeMethod Tidebite.Editor.HabitatValidation.Run.
    public static class HabitatValidation
    {
        [MenuItem("Tidebite/Validate habitat visibility")]
        public static void Run()
        {
            var root=new GameObject("Habitat visibility regression");
            var result=new Result();
            try
            {
                var view=root.AddComponent<OceanView>();
                var session=new SampleSession();view.Build(session.Settings);
                session.Place(0,0,9.25f);view.Display(session,0,false);
                var props=new List<Renderer>();Transform floor=null;
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if(renderer.name=="Painted lagoon")floor=renderer.transform;
                    if(renderer.name=="profile-reef"||renderer.name.StartsWith("habitat-"))props.Add(renderer);
                }
                result.props=props.Count;
                if(floor==null||props.Count!=374)throw new Exception("Incomplete habitat fixture");
                var orders=new HashSet<int>();
                foreach(var prop in props)
                {
                    if(prop.transform.position.y<=floor.position.y+.1f)result.floorDepthConflicts++;
                    if(!orders.Add(prop.sortingOrder))result.sortTies++;
                }
                var block=new MaterialPropertyBlock();
                float[] aspects={4f/3,16f/9,20f/9},zooms={9.25f,8.6f+Mathf.Sqrt(session.Settings.MaxMass)*.65f};
                foreach(float aspect in aspects)foreach(float zoom in zooms)
                for(int y=-72;y<=72;y+=12)for(int x=-72;x<=72;x+=12)
                {
                    session.Place(x,y,zoom);view.ResetPresentation();view.Camera.aspect=aspect;
                    view.Display(session,0,false);result.cameraSamples++;
                    var planes=GeometryUtility.CalculateFrustumPlanes(view.Camera);
                    var centre=view.Camera.transform.position;
                    foreach(var prop in props)
                    {
                        prop.GetPropertyBlock(block);var habitat=block.GetVector("_Habitat");
                        var mesh=prop.GetComponent<MeshFilter>().sharedMesh;
                        var vertices=mesh.vertices;var uv=mesh.uv;
                        var rendered=new Bounds();bool first=true,outside=false;
                        for(int k=0;k<vertices.Length;k++)
                        {
                            // Include the full sway envelope, not a single animation phase.
                            for(int sign=-1;sign<=1;sign+=2)
                            {
                                var vertex=vertices[k];vertex.x+=sign*habitat.z*uv[k].y*uv[k].y;
                                var world=prop.transform.TransformPoint(vertex)+new Vector3(centre.x,0,centre.z)*habitat.y;
                                if(first){rendered=new Bounds(world,Vector3.zero);first=false;}else rendered.Encapsulate(world);
                                if(prop.bounds.SqrDistance(world)>.000001f)outside=true;
                            }
                        }
                        if(outside)result.boundsMismatches++;
                        if(GeometryUtility.TestPlanesAABB(planes,rendered)&&!GeometryUtility.TestPlanesAABB(planes,prop.bounds))result.falseCullCandidates++;
                    }
                }
                result.passed=result.floorDepthConflicts==0&&result.sortTies==0&&result.boundsMismatches==0&&result.falseCullCandidates==0;
                Directory.CreateDirectory("TestResults");
                File.WriteAllText("TestResults/habitat-visibility.json",JsonUtility.ToJson(result,true));
                Debug.Log("TIDEBITE HABITAT_VISIBILITY "+JsonUtility.ToJson(result));
                if(!result.passed)throw new Exception("Habitat visibility regression failed; see TestResults/habitat-visibility.json");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }

        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public int props,cameraSamples,floorDepthConflicts,sortTies,boundsMismatches,falseCullCandidates;
        }
        private sealed class SampleSession:IGameSession
        {
            public GameConfig Settings{get;}=new GameConfig{MaxFish=1,Population=1,FoodCount=1};
            public FishState[] Fish{get;}=new FishState[1];
            public FoodState[] Food{get;}=new FoodState[1];
            public int LocalId=>0;
            public bool IsOnline=>false;
            public bool Ready=>true;
            public int Tick=>0;
            public string Status=>"Habitat regression";
            public event Action<WorldEvent> Event{add{} remove{}}
            public void Place(float x,float y,float zoom)
            {
                float radius=(zoom-8.6f)/.65f;
                Fish[0]=new FishState{Id=0,Generation=1,Occupied=true,Alive=true,Health=100,Mass=radius*radius,Position=new V2(x,y)};
            }
            public FishState RenderFish(int id)=>Fish[id];
            public void Submit(FishCommand command){}
            public void Advance(float elapsed){}
            public void Respawn(){}
            public void ChooseEvolution(Adaptation choice){}
            public void Dispose(){}
        }
    }
}

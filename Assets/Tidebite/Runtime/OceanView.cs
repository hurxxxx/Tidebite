using Tidebite.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Tidebite
{
    public sealed class OceanView : MonoBehaviour
    {
        public Camera Camera { get; private set; }
        private FishVisual[] fish;
        private Vector3[] foodVertices;
        private static readonly Vector3[] Corners={new Vector3(-1,0,-1),new Vector3(-1,0,1),new Vector3(1,0,1),new Vector3(1,0,-1)};
        private readonly Transform[] bubbles=new Transform[64];
        private readonly Vector3[] bubbleVelocity=new Vector3[64];
        private readonly float[] bubbleLife=new float[64];
        private readonly Transform[] motes=new Transform[18];
        private readonly Transform[] swallowed=new Transform[12];
        private readonly Renderer[] swallowedRenderers=new Renderer[12];
        private readonly Vector3[] swallowStart=new Vector3[12],swallowScale=new Vector3[12];
        private readonly float[] swallowLife=new float[12];
        private readonly int[] swallowActor=new int[12];
        private MaterialPropertyBlock swallowProperties;
        private Mesh foodMesh;
        private GameConfig config;
        private Vector3 follow;
        private Vector3 followVelocity;
        private float zoom=10,visualTime;
        private bool resetView=true;
        private int followedGeneration=-1;
        private int bubbleIndex,swallowIndex;
        private AudioSource audioSource;
        private AudioClip biteSound,eatSound,dashSound,foodSound,deathSound,hitSound;
        private Material waterMaterial;
        private readonly Transform[] foreground=new Transform[4];
        private readonly List<HabitatPose> habitats=new List<HabitatPose>(337);
        private struct HabitatPose { public Transform Transform; public Vector3 Origin; public float Parallax; }
        public bool Sound=true,Haptics=true;

        public void Build(GameConfig settings)
        {
            config=settings;
            swallowProperties=new MaterialPropertyBlock();
            Camera=new GameObject("Ocean Camera").AddComponent<Camera>(); Camera.transform.SetParent(transform);
            Camera.orthographic=true; Camera.orthographicSize=10; Camera.nearClipPlane=.1f; Camera.farClipPlane=160;
            Camera.backgroundColor=new Color(.025f,.14f,.20f); Camera.clearFlags=CameraClearFlags.SolidColor;
            Camera.cullingMask=~(1<<FishPreviewAtlas.Layer);
            Camera.transform.rotation=Quaternion.Euler(90,0,0); Camera.gameObject.AddComponent<AudioListener>();
            var ghost=SpriteArt.Painted(FishEvolution.Assets[0]);
            var actions=new[]{SpriteArt.Action(0),SpriteArt.Action(1),SpriteArt.Action(2),SpriteArt.Action(3)};
            var shade=SpriteArt.Effect(new Color(.005f,.025f,.05f,.26f),0);
            var aura=SpriteArt.Effect(new Color(.48f,.95f,1,.35f),1);
            var water=waterMaterial=new Material(Shader.Find("Tidebite/ReefWater"));
            water.SetTexture("_MainTex",Resources.Load<Texture2D>("Art/profile-abyss-backdrop"));
            // The opaque floor must be behind every transparent habitat plane.
            // Coplanar props flicker with depth precision; negative-y props were hidden entirely.
            SpriteArt.Part("Painted lagoon",transform,SpriteArt.Quad,water,new Vector3(0,-2,0),
                Vector3.one*(config.WorldHalfSize*2+48),0);
            fish=new FishVisual[config.MaxFish];
            for(int i=0;i<fish.Length;i++)
            {
                fish[i]=new GameObject("Fish "+i).AddComponent<FishVisual>(); fish[i].transform.SetParent(transform);
                fish[i].Build(i,shade,aura,actions);
            }
            for(int i=0;i<swallowed.Length;i++)
            {
                swallowed[i]=SpriteArt.Part("Swallowed prey",transform,SpriteArt.Quad,ghost,Vector3.zero,Vector3.one,13);
                swallowedRenderers[i]=swallowed[i].GetComponent<Renderer>();swallowed[i].gameObject.SetActive(false);
            }
            BuildReef();
            foodMesh=new Mesh{name="Painted pearls, one batched mesh"}; foodMesh.MarkDynamic();
            foodVertices=new Vector3[config.FoodCount*4]; var uv=new Vector2[foodVertices.Length];
            var triangles=new int[config.FoodCount*6]; int[] quad={0,1,2,0,2,3};
            for(int i=0;i<config.FoodCount;i++)
            {
                for(int k=0;k<6;k++) triangles[i*6+k]=i*4+quad[k];
                for(int k=0;k<4;k++) uv[i*4+k]=new Vector2((Corners[k].x+1)*.5f,(Corners[k].z+1)*.5f);
            }
            foodMesh.vertices=foodVertices; foodMesh.uv=uv; foodMesh.triangles=triangles;
            foodMesh.bounds=new Bounds(Vector3.zero,new Vector3(config.WorldHalfSize*2+2,5,config.WorldHalfSize*2+2));
            SpriteArt.Part("Golden pearls",transform,foodMesh,SpriteArt.Painted("golden-pearl"),Vector3.zero,Vector3.one,3);
            var bubbleMaterial=SpriteArt.Effect(new Color(.7f,1,1,.75f),.8f);
            for(int i=0;i<bubbles.Length;i++)
            {
                bubbles[i]=SpriteArt.Part("Splash bubble",transform,SpriteArt.Quad,bubbleMaterial,Vector3.zero,Vector3.one*.1f,15);
                bubbles[i].gameObject.SetActive(false);
            }
            var moteMaterial=SpriteArt.Effect(new Color(.65f,1,.93f,.20f),0);
            for(int i=0;i<motes.Length;i++)
                motes[i]=SpriteArt.Part("Drifting plankton",transform,SpriteArt.Quad,moteMaterial,Vector3.zero,Vector3.one*.12f,4);
            audioSource=gameObject.AddComponent<AudioSource>(); audioSource.spatialBlend=0; audioSource.volume=.35f;
            biteSound=Tone("Bite",180,65,.10f); eatSound=Tone("Eat",330,750,.17f); dashSound=Tone("Dash",400,80,.14f);
            foodSound=Tone("Pearl",700,1000,.045f); deathSound=Tone("Death",240,45,.35f);hitSound=ImpactSound();
        }

        private void BuildReef()
        {
            string[] assets={"profile-reef","habitat-kelp","habitat-coral","habitat-wreck","habitat-vent","habitat-arch","habitat-sponge"};
            var materials=new Material[assets.Length];for(int i=0;i<assets.Length;i++)materials[i]=SpriteArt.Painted(assets[i]);
            var seed=new Rng(9134);var properties=new MaterialPropertyBlock();
            // Composed habitats with open swimming lanes instead of evenly tiled islands.
            for(int h=0;h<9;h++)
            {
                var centre=new Vector3((h%3-1)*46+seed.Range(-7,7),0,(h/3-1)*43+seed.Range(-6,6));
                int landmark=h%3==0?5:h%3==1?3:4;
                AddProp(landmark,centre+new Vector3(2,-.3f,5),seed.Range(14,21),.70f,.13f,.64f,-800+h);
                for(int k=0;k<20;k++)
                {
                    float a=seed.Range(0,Mathf.PI*2),r=seed.Range(k<5?3:8,k<5?11:20);
                    var p=centre+new Vector3(Mathf.Sin(a)*r,-.15f,Mathf.Cos(a)*r*.62f);
                    int kind=h/3==2?(k%3==0?6:1):h%3==2?(k%3==0?6:2):h/3==0?(k%3==0?0:4):k%3;
                    AddProp(kind,p,seed.Range(k<5?4.8f:1.5f,k<5?8.5f:4),k<5?.34f:.18f,k<5?.04f:.015f,k<5?.72f:.64f,-600+h*20+k);
                }
            }
            for(int i=0;i<100;i++)
                AddProp(i%3==0?5:i%3==1?0:4,new Vector3(seed.Range(-80,80),-.4f,seed.Range(-80,80)),seed.Range(7,18),.88f,.20f,.20f,-1000+i);
            for(int i=0;i<48;i++)
            {
                float a=i*Mathf.PI*2/48;var p=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));
                p=p/Mathf.Max(Mathf.Abs(p.x),Mathf.Abs(p.z))*(config.WorldHalfSize+2);
                AddProp(i%2==0?1:2,p,seed.Range(9,16),.24f,.02f,.84f,-300+i);
            }
            for(int i=0;i<foreground.Length;i++)foreground[i]=AddProp(i%2==0?1:2,Vector3.zero,12,.18f,0,.75f,26+i);

            Transform AddProp(int kind,Vector3 p,float size,float fog,float parallax,float opacity,int order)
            {
                var prop=SpriteArt.Part(assets[kind],transform,SpriteArt.Quad,materials[kind],p,
                    new Vector3(size*(seed.Unit()>.5f?-1:1),1,size*seed.Range(.83f,1.14f)),order);
                properties.Clear();properties.SetColor("_Tint",new Color(.75f,.90f,.95f,opacity));
                // Apply parallax to the Transform so Unity culls the rendered position,
                // rather than an offscreen, unshifted mesh. Local sway fits the padded quad bounds.
                properties.SetVector("_Habitat",new Vector4(fog,0,kind==1?.018f:0,seed.Range(0,6.28f)));
                if(parallax>0)habitats.Add(new HabitatPose{Transform=prop,Origin=p,Parallax=parallax});
                prop.GetComponent<Renderer>().SetPropertyBlock(properties);return prop;
            }
        }

        public void Display(IGameSession session,float dt,bool menu)
        {
            config=session.Settings;
            int local=session.LocalId; var states=session.Fish;
            visualTime+=dt;
            Vector3 target=Vector3.zero;
            if(local>=0&&states[local].Occupied)
            {
                var pose=session.RenderFish(local);
                target=new Vector3(pose.Position.X,0,pose.Position.Y);
                if(followedGeneration!=pose.Generation) { followedGeneration=pose.Generation; resetView=true; }
            }
            if(resetView) { follow=target; followVelocity=Vector3.zero; }
            else if(dt>0) follow=Vector3.SmoothDamp(follow,target,ref followVelocity,.12f,100,dt);
            float desired=menu?11:8.6f+(local>=0?Mathf.Sqrt(Mathf.Max(1,states[local].Mass))*.65f:0);
            if(resetView) zoom=desired;
            zoom=Mathf.Lerp(zoom,desired,1-Mathf.Exp(-dt*2)); Camera.orthographicSize=zoom;
            Camera.transform.position=follow-Camera.transform.forward*40;
            Shader.SetGlobalVector("_OceanView",new Vector4(follow.x,follow.z,zoom*Camera.aspect,zoom));
            for(int i=0;i<habitats.Count;i++)
            {
                var habitat=habitats[i];habitat.Transform.localPosition=habitat.Origin+follow*habitat.Parallax;
            }
            for(int i=0;i<foreground.Length;i++)
            {
                float x=(i%2==0?-1:1)*(zoom*Camera.aspect+2.5f);
                float z=i<2?-zoom-2.4f:zoom+4;
                foreground[i].position=follow+new Vector3(x,1,z);
            }
            for(int i=0;i<fish.Length;i++)
            {
                var pose=session.RenderFish(i);
                // Use the same interpolated pose for rendering and culling. A fixed
                // distance omitted visible fish on wide screens / at larger zooms.
                float margin=pose.Radius*4+.5f;
                bool visible=pose.Occupied&&pose.Alive&&Mathf.Abs(pose.Position.X-follow.x)<=zoom*Camera.aspect+margin&&Mathf.Abs(pose.Position.Y-follow.z)<=zoom+margin;
                if(visible) fish[i].Display(pose,i==local,dt,visualTime,config); else fish[i].gameObject.SetActive(false);
            }
            waterMaterial.SetVector("_ViewCenter",new Vector4(follow.x,follow.z,config.WorldHalfSize,0));
            resetView=false;
            for(int i=0;i<session.Food.Length;i++)
            {
                var f=session.Food[i];
                var centre=new Vector3(f.Position.X,.35f,f.Position.Y);
                float radius=f.Active?.29f*(1+Mathf.Sin(visualTime*2.2f+i*2.4f)*.035f):0;
                for(int k=0;k<4;k++) foodVertices[i*4+k]=centre+Corners[k]*radius;
            }
            foodMesh.SetVertices(foodVertices);
            for(int i=0;i<bubbles.Length;i++) if(bubbleLife[i]>0)
            {
                bubbleLife[i]-=dt; bubbles[i].position+=bubbleVelocity[i]*dt;
                bubbles[i].localScale=Vector3.one*Mathf.Max(0,bubbleLife[i])*.38f;
                if(bubbleLife[i]<=0) bubbles[i].gameObject.SetActive(false);
            }
            for(int i=0;i<swallowed.Length;i++)if(swallowLife[i]>0)
            {
                swallowLife[i]=Mathf.Max(0,swallowLife[i]-dt);float t=1-swallowLife[i]/.24f;
                Vector3 mouth=fish[swallowActor[i]].MouthPosition;
                swallowed[i].position=Vector3.Lerp(swallowStart[i],mouth,t*t);
                swallowed[i].localScale=swallowScale[i]*Mathf.Lerp(1,.08f,t*t);
                swallowProperties.Clear();swallowProperties.SetColor("_Tint",new Color(1,1,1,1-t*t));
                swallowedRenderers[i].SetPropertyBlock(swallowProperties);
                if(swallowLife[i]<=0)swallowed[i].gameObject.SetActive(false);
            }
            for(int i=0;i<motes.Length;i++)
            {
                float x=Mathf.Repeat(i*7.71f+visualTime*.12f,38)-19;
                float z=Mathf.Repeat(i*3.17f+visualTime*.18f,24)-12;
                motes[i].position=follow+new Vector3(x,1,z);
                motes[i].localScale=Vector3.one*(.08f+.06f*Mathf.Sin(visualTime*.7f+i));
            }
        }

        public void React(WorldEvent e,int local)
        {
            bool mine=e.Actor==local;
            if(e.Actor>=0&&e.Actor<fish.Length&&(e.Kind==WorldEventKind.Eat||e.Kind==WorldEventKind.Bite)) fish[e.Actor].Pulse(e.Kind==WorldEventKind.Eat);
            if(e.Kind==WorldEventKind.Eat&&e.Target>=0&&e.Target<fish.Length&&e.Actor>=0&&e.Actor<fish.Length&&fish[e.Target].gameObject.activeSelf)
            {
                int i=swallowIndex++%swallowed.Length;var prey=fish[e.Target].transform;
                swallowStart[i]=prey.position+Vector3.up*.04f;swallowScale[i]=fish[e.Target].GhostScale;
                swallowActor[i]=e.Actor;swallowLife[i]=.24f;
                swallowed[i].position=swallowStart[i];swallowed[i].rotation=fish[e.Target].GhostRotation;swallowed[i].localScale=swallowScale[i];
                swallowedRenderers[i].sharedMaterial=fish[e.Target].CurrentMaterial;swallowed[i].gameObject.SetActive(true);
            }
            if(e.Kind==WorldEventKind.Damage&&e.Target>=0&&e.Target<fish.Length)
            {fish[e.Target].Hurt();if(e.Actor>=0&&e.Actor<fish.Length)fish[e.Actor].Contact();}
            if(e.Kind==WorldEventKind.Dodge&&e.Actor>=0&&e.Actor<fish.Length)fish[e.Actor].Pulse(true);
            if(e.Kind==WorldEventKind.Eat||e.Kind==WorldEventKind.Dash||e.Kind==WorldEventKind.Food||e.Kind==WorldEventKind.Damage)
            {
                int n=e.Kind==WorldEventKind.Eat?12:e.Kind==WorldEventKind.Dash?7:2;
                for(int k=0;k<n;k++)
                {
                    int i=bubbleIndex++%bubbles.Length; float a=k*Mathf.PI*2/n;
                    bubbles[i].position=new Vector3(e.Position.X,.7f,e.Position.Y);
                    bubbleVelocity[i]=new Vector3(Mathf.Sin(a)*1.6f,0,Mathf.Cos(a)*1.6f);
                    bubbleLife[i]=.5f+k*.025f; bubbles[i].gameObject.SetActive(true);
                }
            }
            if(!mine&&e.Target!=local) return;
            AudioClip clip=e.Kind==WorldEventKind.Damage?hitSound:e.Kind==WorldEventKind.Dodge?dashSound:e.Kind==WorldEventKind.Bite?biteSound:e.Kind==WorldEventKind.Eat?eatSound:e.Kind==WorldEventKind.Dash?dashSound:e.Kind==WorldEventKind.Food?foodSound:e.Kind==WorldEventKind.Death?deathSound:null;
            if(Sound&&clip!=null) audioSource.PlayOneShot(clip,e.Kind==WorldEventKind.Food?.25f:1);
            if(e.Kind==WorldEventKind.Eat)
            {
#if UNITY_IOS || UNITY_ANDROID
                if(Haptics) Handheld.Vibrate();
#endif
            }
        }

        public void ResetPresentation()
        {
            resetView=true; followedGeneration=-1;
            if(fish!=null) foreach(var visual in fish) visual.ResetPose();
            for(int i=0;i<swallowed.Length;i++){swallowLife[i]=0;swallowed[i]?.gameObject.SetActive(false);}
            for(int i=0;i<bubbles.Length;i++){bubbleLife[i]=0;bubbles[i]?.gameObject.SetActive(false);}
        }
        public bool TryGetLabelPosition(int id,out Vector3 position)
        {
            position=default;
            if(fish==null||id<0||id>=fish.Length||!fish[id].gameObject.activeInHierarchy)return false;
            var visual=fish[id].transform;
            position=visual.position+Vector3.forward*(visual.localScale.x*.65f+.35f);return true;
        }

        private static AudioClip ImpactSound()
        {
            const int rate=22050;var data=new float[(int)(rate*.12f)];var noise=new Rng(1843);
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate;
                data[i]=(Mathf.Sin(2*Mathf.PI*(140*t-210*t*t))*.55f*Mathf.Exp(-t*32)+(noise.Unit()*2-1)*.30f*Mathf.Exp(-t*80))*Mathf.Min(1,t*2500);
            }
            var clip=AudioClip.Create("Layered bite contact",data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        private static AudioClip Tone(string name,float from,float to,float duration)
        {
            const int rate=22050; var samples=new float[(int)(rate*duration)]; float phase=0;
            for(int i=0;i<samples.Length;i++){float t=i/(float)samples.Length; phase+=Mathf.Lerp(from,to,t)*Mathf.PI*2/rate; samples[i]=Mathf.Sin(phase)*Mathf.Sin(t*Mathf.PI)*.45f;}
            var clip=AudioClip.Create(name,samples.Length,1,rate,false); clip.SetData(samples,0); return clip;
        }
    }
}

using Tidebite.Core;
using UnityEngine;

namespace Tidebite
{
    public sealed class FishVisual : MonoBehaviour
    {
        private ProfileFishRig rig,previousRig;
        private Transform shadow,halo,slash,impact,evolution,wake;
        private Renderer haloRenderer,slashRenderer,impactRenderer,evolutionRenderer,wakeRenderer;
        private MaterialPropertyBlock properties;
        private float hurtPulse,eatPulse,bitePulse,swimPhase,turnLean,turnVelocity,lastHeading,swimSpeed,dashStretch,formFade,evolvePulse,bellyFacing=1,contactHold;
        private int generation,form=-1;
        private bool started,facingRight=true;
        private Color fishTint;
        private static readonly int Tint=Shader.PropertyToID("_Tint"),FX=Shader.PropertyToID("_FX");
        public Material CurrentMaterial=>rig.WholeMaterial;
        public Vector3 MouthPosition=>rig.MouthPosition;
        public Quaternion GhostRotation=>rig.transform.rotation;
        public Vector3 GhostScale=>Vector3.Scale(transform.localScale,Vector3.Scale(rig.transform.localScale,new Vector3(ProfileFishRig.Width,1,ProfileFishRig.Width)));
        public static readonly Color[] Palette={new Color(.98f,.70f,.27f),new Color(.34f,.85f,.78f),new Color(.94f,.43f,.42f),new Color(.58f,.60f,.92f),new Color(.36f,.70f,.93f),new Color(.97f,.61f,.76f)};
        public void Build(int id,Material shade,Material aura,Material[] action)
        {
            properties=new MaterialPropertyBlock();swimPhase=id*2.399f;fishTint=id%2==0?new Color(.91f,.98f,1):Color.white;
            previousRig=new GameObject("Previous profile form").AddComponent<ProfileFishRig>();previousRig.transform.SetParent(transform,false);previousRig.Build(0);previousRig.gameObject.SetActive(false);
            rig=new GameObject("Side-profile fish rig").AddComponent<ProfileFishRig>();rig.transform.SetParent(transform,false);rig.Build(0);
            shadow=SpriteArt.Part("Soft shadow",transform,SpriteArt.Quad,shade,new Vector3(.06f,-.39f,-.08f),new Vector3(.9f,1,1.2f),1);
            halo=SpriteArt.Part("Spawn shield",transform,SpriteArt.Quad,aura,new Vector3(0,-.10f,0),Vector3.one*2.1f,8);haloRenderer=halo.GetComponent<Renderer>();
            slash=SpriteArt.Part("Bite water crescent",transform,SpriteArt.Quad,action[0],new Vector3(0,.12f,.65f),new Vector3(2.2f,1,2.2f),24);slashRenderer=slash.GetComponent<Renderer>();
            impact=SpriteArt.Part("Consumption shockwave",transform,SpriteArt.Quad,action[1],new Vector3(0,.14f,.75f),Vector3.one,25);impactRenderer=impact.GetComponent<Renderer>();
            evolution=SpriteArt.Part("Evolution ripple",transform,SpriteArt.Quad,action[3],new Vector3(0,.10f,0),Vector3.one,9);evolutionRenderer=evolution.GetComponent<Renderer>();
            wake=SpriteArt.Part("Dash water trail",transform,SpriteArt.Quad,action[2],new Vector3(0,-.03f,-.70f),new Vector3(1.1f,1,2.5f),7);wakeRenderer=wake.GetComponent<Renderer>();
        }
        public void Pulse(bool eat){if(eat)eatPulse=1;else bitePulse=1;}
        public void Hurt(){hurtPulse=1;contactHold=.055f;}
        public void Contact(){contactHold=.045f;}
        public void ResetPose(){hurtPulse=contactHold=0;started=false;eatPulse=bitePulse=evolvePulse=0;}
        public void Display(FishState state,bool local,float dt,float time,GameConfig settings)
        {
            gameObject.SetActive(state.Occupied&&state.Alive);
            if(!state.Occupied||!state.Alive){started=false;return;}
            int next=FishEvolution.Index(GrowthCurve.Form(state.Mass,settings),state.Species);
            if(!started||generation!=state.Generation)
            {
                started=true;generation=state.Generation;form=next;rig.SetForm(form);
                transform.localScale=Vector3.one*(state.Radius/.48f);formFade=evolvePulse=eatPulse=bitePulse=0;
                previousRig.gameObject.SetActive(false);lastHeading=state.Heading;turnLean=turnVelocity=dashStretch=0;swimSpeed=state.Velocity.Magnitude;facingRight=state.Forward.X>=0;bellyFacing=facingRight?1:-1;
            }
            else if(form!=next)
            {
                previousRig.SetForm(form);previousRig.gameObject.SetActive(true);rig.SetForm(next);form=next;formFade=evolvePulse=1;
            }
            transform.position=new Vector3(state.Position.X,.6f,state.Position.Y);transform.rotation=Quaternion.Euler(0,state.Heading*Mathf.Rad2Deg,0);
            transform.localScale=Vector3.Lerp(transform.localScale,Vector3.one*(state.Radius/.48f),1-Mathf.Exp(-dt*9));
            float speed=state.Velocity.Magnitude;swimSpeed=Mathf.Lerp(swimSpeed,speed,1-Mathf.Exp(-dt*8));contactHold=Mathf.Max(0,contactHold-dt);
            if(contactHold<=0)swimPhase+=dt*(3.3f+Mathf.Min(swimSpeed,9)*1.15f);
            float turn=dt>0?Mathf.Clamp(Math2.DeltaAngle(lastHeading,state.Heading)/Mathf.Max(dt,.001f)/5,-1,1):turnLean;
            if(dt>0)turnLean=Mathf.SmoothDamp(turnLean,turn,ref turnVelocity,.11f,20,dt);
            lastHeading=state.Heading;if(state.Forward.X>.16f)facingRight=true;else if(state.Forward.X<-.16f)facingRight=false;
            bellyFacing=Mathf.MoveTowards(bellyFacing,facingRight?1:-1,dt*9);
            dashStretch=Mathf.Lerp(dashStretch,state.DashRemaining>0?1:0,1-Mathf.Exp(-dt*18));
            eatPulse=Mathf.MoveTowards(eatPulse,0,dt*3.5f);bitePulse=Mathf.MoveTowards(bitePulse,0,dt/.30f);formFade=Mathf.MoveTowards(formFade,0,dt/.32f);evolvePulse=Mathf.MoveTowards(evolvePulse,0,dt/.75f);
            hurtPulse=Mathf.MoveTowards(hurtPulse,0,dt*5);
            float growth=GrowthCurve.FormProgress(state.Mass,settings),pulse=Mathf.Sin(eatPulse*Mathf.PI)*.08f,action=1-bitePulse;
            float gape=bitePulse>0?FishJaw.Gape(action):0;
            float strike=bitePulse<=0?0:action<.16f?-action/.16f*.04f:Mathf.Sin(Mathf.InverseLerp(.16f,1,action)*Mathf.PI)*.12f;
            rig.transform.localRotation=Quaternion.Euler(0,-90-turnLean*3,0);
            // Smooth a shallow bank, not the signed scale through zero: interpolating
            // +1 to -1 made the entire body disappear briefly while its tag remained.
            float bank=(bellyFacing>=0?1:-1)*Mathf.Lerp(.82f,1,Mathf.Abs(bellyFacing));
            rig.transform.localScale=new Vector3(Mathf.Lerp(.97f,1.04f,growth)+strike+dashStretch*.12f,1,bank*(1+pulse-dashStretch*.05f));
            rig.transform.localPosition=new Vector3(0,.015f,strike*.65f);rig.SetOrder(local?16:10);
            float dive=Mathf.Cos(state.Heading)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,.95f,Mathf.Abs(state.Forward.Y)));
            rig.Animate(swimPhase,.55f+Mathf.Min(swimSpeed,7)*.08f,gape,Mathf.Max(eatPulse,hurtPulse),Color.Lerp(local?Color.white:fishTint,new Color(1,.40f,.35f),hurtPulse*.7f),1-formFade,turnLean,dive);
            if(formFade>0)
            {
                previousRig.transform.localScale=rig.transform.localScale;previousRig.transform.localRotation=rig.transform.localRotation;previousRig.transform.localPosition=rig.transform.localPosition;
                previousRig.SetOrder(local?15:9);previousRig.Animate(swimPhase,.6f,gape,eatPulse,fishTint,formFade,turnLean,dive);
            }
            else previousRig.gameObject.SetActive(false);
            float slashAge=Mathf.InverseLerp(.14f,.84f,action);
            SetEffect(slash,slashRenderer,bitePulse>0&&action>.14f&&action<.84f,slashAge,new Color(.74f,.96f,1,.85f));
            impact.localScale=Vector3.one*Mathf.Lerp(.5f,2.5f,1-eatPulse);SetEffect(impact,impactRenderer,eatPulse>0,1-eatPulse,new Color(1,.86f,.53f,.9f));
            evolution.localScale=Vector3.one*Mathf.Lerp(1.2f,3.2f,1-evolvePulse);SetEffect(evolution,evolutionRenderer,evolvePulse>0,1-evolvePulse,new Color(.45f,1,.91f,.72f));
            SetEffect(wake,wakeRenderer,dashStretch>.03f,Mathf.Repeat(time*2,1),new Color(.65f,.94f,1,dashStretch*.62f));
            bool ward=state.Armor>0&&state.WardCooldown<=0;
            halo.gameObject.SetActive(state.Protection>0||ward);
            if(state.Protection>0||ward)
            {
                halo.localScale=Vector3.one*(2.05f+Mathf.Sin(time*3)*.07f);properties.Clear();properties.SetColor(Tint,ward?new Color(1,.78f,.34f,.22f):new Color(.48f,.95f,1,.28f+Mathf.Sin(time*6)*.10f));haloRenderer.SetPropertyBlock(properties);
            }
            shadow.localScale=new Vector3(.9f+pulse,1,1.2f);
        }
        private void SetEffect(Transform part,Renderer renderer,bool active,float age,Color color)
        {
            part.gameObject.SetActive(active);if(!active)return;properties.Clear();properties.SetColor(Tint,color);properties.SetVector(FX,new Vector4(age,0,0,0));renderer.SetPropertyBlock(properties);
        }
    }
}

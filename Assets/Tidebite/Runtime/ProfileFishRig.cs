using Tidebite.Core;
using UnityEngine;

namespace Tidebite
{
    // The mandible uses the SAME sprite pixels as the body. Only a hinge rotates; no pasted-on mouth.
    public sealed class ProfileFishRig : MonoBehaviour
    {
        public const float Width=2.1f;
        private static Mesh grid;
        private static Material[] bodies,jaws,whole;
        private static Mesh[] jawMeshes,upperTeethMeshes,lowerTeethMeshes;
        private static Material mouthMaterial;
        // Individual lip seams are registered to each generated side-profile drawing.
        private static readonly Vector4[] Lips={
            new Vector4(.752f,.447f,.968f,.508f),new Vector4(.730f,.400f,.965f,.482f),
            new Vector4(.719f,.434f,.971f,.523f),new Vector4(.710f,.459f,.985f,.531f),
            new Vector4(.750f,.444f,.961f,.450f),new Vector4(.764f,.375f,.984f,.477f),
            new Vector4(.826f,.425f,.970f,.487f),new Vector4(.877f,.382f,.982f,.424f),
            new Vector4(.812f,.400f,.978f,.466f),new Vector4(.807f,.461f,.984f,.489f),
            new Vector4(.766f,.428f,.900f,.443f),new Vector4(.800f,.440f,.984f,.461f)
        };
        private static readonly float[] Curves={.004f,.018f,0,0,.024f,.010f,0,.006f,.018f,.006f,.006f,.002f};
        private Transform upper,mandible,upperTeeth,lowerTeeth;
        private MeshRenderer upperRenderer,jawRenderer,upperTeethRenderer,lowerTeethRenderer;
        private MaterialPropertyBlock properties;
        private int form=-1;
        public Material WholeMaterial=>whole[form];
        public Vector3 MouthPosition=>transform.TransformPoint(Point(Lips[form].z,Lips[form].w));

        private static Vector3 Point(float u,float v)=>new Vector3((u-.5f)*Width,0,(v-.5f)*Width);
        private static float Seam(int f,float u)
        {
            var lip=Lips[f];float t=Mathf.Clamp01((u-lip.x)/(lip.z-lip.x));
            return Mathf.Lerp(lip.y,lip.w,t)+Curves[f]*4*t*(1-t);
        }
        private static void EnsureAssets()
        {
            if(grid!=null)return;
            grid=SpriteArt.Grid(Width,Width,32,24);
            // Tail rotation, turn follow-through and dive curvature extend beyond
            // the unanimated quad. Keep edge-of-screen culling conservative.
            grid.bounds=new Bounds(Vector3.zero,new Vector3(Width+1.4f,.1f,Width+1.8f));
            int count=FishEvolution.Assets.Length;bodies=new Material[count];jaws=new Material[count];whole=new Material[count];
            jawMeshes=new Mesh[count];upperTeethMeshes=new Mesh[count];lowerTeethMeshes=new Mesh[count];
            mouthMaterial=new Material(Shader.Find("Tidebite/ProfileMouth"));
            for(int f=0;f<count;f++)
            {
                var texture=FishEvolution.Texture(f);
                if(texture==null)throw new System.InvalidOperationException("Missing profile fish: "+FishEvolution.Assets[f]);
                for(int part=0;part<3;part++)
                {
                    var material=new Material(Shader.Find("Tidebite/ProfileFish")){name=FishEvolution.Assets[f]+" part "+part,enableInstancing=true};
                    material.SetTexture("_MainTex",texture);material.SetVector("_Lip",Lips[f]);material.SetFloat("_Curve",Curves[f]);material.SetFloat("_Part",part);
                    if(part==0)bodies[f]=material;else if(part==1)jaws[f]=material;else whole[f]=material;
                }
                var pivot=Point(Lips[f].x,Lips[f].y);var mesh=Object.Instantiate(grid);mesh.name="Mandible at lip hinge "+f;
                var vertices=mesh.vertices;for(int v=0;v<vertices.Length;v++)vertices[v]-=pivot;
                mesh.vertices=vertices;mesh.RecalculateBounds();jawMeshes[f]=mesh;
                upperTeethMeshes[f]=Teeth(f,false,Vector3.zero);lowerTeethMeshes[f]=Teeth(f,true,pivot);
            }
        }
        private static Mesh Teeth(int f,bool lower,Vector3 offset)
        {
            int stage=f%GrowthCurve.FormCount;int count=6+stage;var vertices=new Vector3[count*3];var colors=new Color[vertices.Length];var indices=new int[vertices.Length];
            var lip=Lips[f];float gap=(lip.z-lip.x)*.78f/count;
            for(int i=0;i<count;i++)
            {
                float u=Mathf.Lerp(lip.x+(lip.z-lip.x)*.16f,lip.z-.017f,(i+.5f)/count);
                float v=Seam(f,u)+(lower?-.002f:.002f),length=(.012f+stage*.0022f)*(1-.25f*i/count);
                vertices[i*3]=Point(u-gap*.36f,v)-offset;
                vertices[i*3+1]=Point(u+gap*.36f,v)-offset;
                vertices[i*3+2]=Point(u+.002f,v+(lower?length:-length))-offset;
                colors[i*3]=new Color(.76f,.57f,.58f);colors[i*3+1]=new Color(.98f,.89f,.70f);colors[i*3+2]=new Color(1,.99f,.89f);
                indices[i*3]=i*3;indices[i*3+1]=i*3+1;indices[i*3+2]=i*3+2;
            }
            var mesh=new Mesh{name="Anatomical teeth "+f};mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=indices;mesh.RecalculateBounds();return mesh;
        }
        public void Build(int initialForm)
        {
            EnsureAssets();properties=new MaterialPropertyBlock();
            upper=SpriteArt.Part("Upper head and body",transform,grid,bodies[0],Vector3.zero,Vector3.one,11);upperRenderer=upper.GetComponent<MeshRenderer>();
            mandible=SpriteArt.Part("Hinged lower jaw",transform,jawMeshes[0],jaws[0],Vector3.zero,Vector3.one,12);jawRenderer=mandible.GetComponent<MeshRenderer>();
            upperTeeth=SpriteArt.Part("Upper teeth",transform,upperTeethMeshes[0],mouthMaterial,new Vector3(0,.01f,0),Vector3.one,13);upperTeethRenderer=upperTeeth.GetComponent<MeshRenderer>();
            lowerTeeth=SpriteArt.Part("Lower teeth",mandible,lowerTeethMeshes[0],mouthMaterial,new Vector3(0,.01f,0),Vector3.one,13);lowerTeethRenderer=lowerTeeth.GetComponent<MeshRenderer>();
            SetForm(initialForm);Animate(0,.5f,0,0,Color.white,1);
        }
        public void SetForm(int next)
        {
            if(next==form)return;form=next;var lip=Lips[form];
            upperRenderer.sharedMaterial=bodies[form];jawRenderer.sharedMaterial=jaws[form];
            mandible.GetComponent<MeshFilter>().sharedMesh=jawMeshes[form];mandible.localPosition=Point(lip.x,lip.y);
            upperTeeth.GetComponent<MeshFilter>().sharedMesh=upperTeethMeshes[form];lowerTeeth.GetComponent<MeshFilter>().sharedMesh=lowerTeethMeshes[form];
        }
        public void SetOrder(int order)
        {
            upperRenderer.sortingOrder=order+1;jawRenderer.sortingOrder=order+2;
            upperTeethRenderer.sortingOrder=lowerTeethRenderer.sortingOrder=order+3;
        }
        public void Animate(float phase,float amplitude,float gape,float impact,Color tint,float alpha,float bend=0,float dive=0)
        {
            properties.Clear();tint.a=alpha;properties.SetColor("_Tint",tint);properties.SetVector("_Swim",new Vector4(phase,amplitude,bend,dive));
            properties.SetVector("_Expression",new Vector4(gape,impact,0,0));upperRenderer.SetPropertyBlock(properties);jawRenderer.SetPropertyBlock(properties);
            mandible.localRotation=Quaternion.Euler(0,gape*FishJaw.MaxAngle*Mathf.Rad2Deg,0);
            // The opening is genuinely empty: world water / UI alpha show between the jaws.
            bool open=gape>.01f;upperTeeth.gameObject.SetActive(open);lowerTeeth.gameObject.SetActive(open);
            if(!open)return;
            properties.Clear();properties.SetColor("_Tint",new Color(1,1,1,alpha));
            upperTeethRenderer.SetPropertyBlock(properties);lowerTeethRenderer.SetPropertyBlock(properties);
        }
    }
}

using Tidebite.Core;
using UnityEngine;

namespace Tidebite
{
    // One offscreen camera renders all twelve gameplay rigs for the menu and codex.
    public sealed class FishPreviewAtlas : MonoBehaviour
    {
        public static FishPreviewAtlas Instance{get;private set;}
        public RenderTexture Texture{get;private set;}
        public const int Layer=30;
        private Camera previewCamera;
        private ProfileFishRig[] rigs;
        private readonly float[] bites=new float[12];
        private float phase,autoTimer;
        private bool visible=true;
        public static FishPreviewAtlas Ensure()
        {
            if(Instance!=null)return Instance;
            Instance=new GameObject("Fish rig preview atlas").AddComponent<FishPreviewAtlas>();Instance.Build();return Instance;
        }
        private void Build()
        {
            Texture=new RenderTexture(1536,2048,16,RenderTextureFormat.ARGB32){name="Shared fish previews",antiAliasing=1,useMipMap=false,filterMode=FilterMode.Bilinear};Texture.Create();
            previewCamera=new GameObject("Fish preview camera").AddComponent<Camera>();previewCamera.transform.SetParent(transform,false);
            previewCamera.transform.position=new Vector3(0,1004,0);previewCamera.transform.rotation=Quaternion.Euler(90,0,0);
            previewCamera.orthographic=true;previewCamera.orthographicSize=4.4f;previewCamera.nearClipPlane=.1f;previewCamera.farClipPlane=8;
            previewCamera.backgroundColor=Color.clear;previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.cullingMask=1<<Layer;
            previewCamera.allowHDR=false;previewCamera.allowMSAA=false;previewCamera.targetTexture=Texture;previewCamera.depth=-10;
            rigs=new ProfileFishRig[12];
            for(int i=0;i<rigs.Length;i++)
            {
                rigs[i]=new GameObject("Profile preview "+i).AddComponent<ProfileFishRig>();rigs[i].transform.SetParent(transform,false);
                rigs[i].transform.position=new Vector3((i%3-1)*2.2f,1000,(1.5f-i/3)*2.2f);rigs[i].Build(i);
                foreach(var child in rigs[i].GetComponentsInChildren<Transform>(true))child.gameObject.layer=Layer;
            }
        }
        public Rect UV(int form)=>new Rect((form%3)/3f,(3-form/3)/4f,1/3f,.25f);
        public void Attack(int form){bites[form]=1;}
        public void Show(bool show){visible=show;if(!show)previewCamera.enabled=false;}
        private void Update()
        {
            if(!visible)return;float dt=Time.unscaledDeltaTime;phase+=dt*4.5f;autoTimer+=dt;
            if(autoTimer>4){autoTimer=0;Attack(4);}
            for(int i=0;i<rigs.Length;i++)
            {
                bites[i]=Mathf.Max(0,bites[i]-dt);float gape=bites[i]>0?FishJaw.Gape(1-bites[i]):0;
                rigs[i].Animate(phase+i*.47f,.62f,gape,0,Color.white,1);
            }
            previewCamera.enabled=Time.frameCount%2==0;
        }
        private void OnDestroy(){if(Texture!=null){Texture.Release();Destroy(Texture);}if(Instance==this)Instance=null;}
    }
}

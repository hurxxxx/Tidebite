Shader "Tidebite/ProfileFish"
{
    Properties
    {
        _MainTex("Profile fish",2D)="white"{}
        _Tint("Tint",Color)=(1,1,1,1)
        _Swim("Swim",Vector)=(0,0,0,0)
        _Expression("Bite / impact",Vector)=(0,0,0,0)
        _Lip("Hinge UV / tip UV",Vector)=(.75,.44,.96,.46)
        _Curve("Lip curvature",Float)=0
        _Part("Body / mandible / full",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Lip;float _Curve,_Part;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(Fish)
                UNITY_DEFINE_INSTANCED_PROP(float4,_Tint)
                UNITY_DEFINE_INSTANCED_PROP(float4,_Swim)
                UNITY_DEFINE_INSTANCED_PROP(float4,_Expression)
            UNITY_INSTANCING_BUFFER_END(Fish)
            struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            V vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);
                float4 swim=UNITY_ACCESS_INSTANCED_PROP(Fish,_Swim);
                if(_Part<.5)
                {
                    float tail=1-smoothstep(.22,.36,i.uv.x);
                    float angle=sin(swim.x)*.22*swim.y;
                    float2 pivot=float2(-.441,0),p=i.positionOS.xz-pivot;
                    float2 turned=float2(cos(angle)*p.x-sin(angle)*p.y,sin(angle)*p.x+cos(angle)*p.y);
                    i.positionOS.xz=lerp(i.positionOS.xz,turned+pivot,tail);
                    float rear=saturate((.73-i.uv.x)/.70);
                    i.positionOS.z+=sin(swim.x-rear*3.8)*.025*swim.y*rear*rear;
                    float fin=smoothstep(.37,.46,i.uv.x)*(1-smoothstep(.61,.68,i.uv.x))*(1-smoothstep(.36,.46,i.uv.y));
                    i.positionOS.z+=sin(swim.x+.8)*.028*swim.y*fin;
                }
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float t=saturate((i.uv.x-_Lip.x)/max(.001,_Lip.z-_Lip.x));
                float seam=lerp(_Lip.y,_Lip.w,t)+_Curve*4*t*(1-t);
                float mandible=step(_Lip.x,i.uv.x)*step(i.uv.y,seam);
                if(_Part<.5)clip(.5-mandible);
                else if(_Part<1.5)clip(mandible-.5);
                half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*UNITY_ACCESS_INSTANCED_PROP(Fish,_Tint);
                float pulse=UNITY_ACCESS_INSTANCED_PROP(Fish,_Expression).y;
                color.rgb=lerp(color.rgb,half3(1,.87,.63),pulse*.16);
                clip(color.a-.005);return color;
            }
            ENDHLSL
        }
    }
}

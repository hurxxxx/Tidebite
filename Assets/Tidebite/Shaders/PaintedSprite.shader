Shader "Tidebite/PaintedSprite"
{
    Properties
    {
        _MainTex("Painted sprite",2D)="white"{}
        _Tint("Tint",Color)=(1,1,1,1)
        _Swim("Swim phase / amplitude",Vector)=(0,0,0,0)
        _Expression("Bite / impact",Vector)=(0,0,0,0)
        _Habitat("Fog / reserved / sway / phase",Vector)=(0,0,0,0)
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
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(Sprite)
                UNITY_DEFINE_INSTANCED_PROP(float4,_Swim)
                UNITY_DEFINE_INSTANCED_PROP(float4,_Tint)
                UNITY_DEFINE_INSTANCED_PROP(float4,_Expression)
                UNITY_DEFINE_INSTANCED_PROP(float4,_Habitat)
            UNITY_INSTANCING_BUFFER_END(Sprite)
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i,o);
                float4 swim=UNITY_ACCESS_INSTANCED_PROP(Sprite,_Swim);
                // Three articulated regions: tail hinge, left flipper, right flipper.
                float tail=1-smoothstep(.28,.36,i.uv.y);
                float angle=sin(swim.x-1.2)*.48*swim.y+swim.z*.12;
                float2 pivot=float2(0,-.36);
                float2 p=i.positionOS.xz-pivot;
                float2 turned=float2(cos(angle)*p.x-sin(angle)*p.y,sin(angle)*p.x+cos(angle)*p.y);
                i.positionOS.xz=lerp(i.positionOS.xz,turned+pivot,tail);
                float side=sign(i.uv.x-.5);
                float fin=smoothstep(.09,.18,abs(i.uv.x-.5))*smoothstep(.32,.43,i.uv.y)*(1-smoothstep(.57,.66,i.uv.y));
                pivot=float2(side*.22,-.02); p=i.positionOS.xz-pivot;
                angle=side*sin(swim.x+.8)*.34*swim.y;
                turned=float2(cos(angle)*p.x-sin(angle)*p.y,sin(angle)*p.x+cos(angle)*p.y);
                i.positionOS.xz=lerp(i.positionOS.xz,turned+pivot,fin);
                // A travelling spine wave and turn follow-through keep the head stable.
                float spine=saturate((.82-i.uv.y)/.72);
                i.positionOS.x+=(sin(swim.x-spine*4.2)*.045*swim.y+swim.z*.10)*spine*spine;
                float4 expression=UNITY_ACCESS_INSTANCED_PROP(Sprite,_Expression);
                float jaw=smoothstep(.77,.91,i.uv.y);
                i.positionOS.x*=1+jaw*expression.x*.16;
                i.positionOS.z+=jaw*expression.x*.045;
                float4 habitat=UNITY_ACCESS_INSTANCED_PROP(Sprite,_Habitat);
                i.positionOS.x+=sin(_Time.y*.7+habitat.w+i.uv.y*1.7)*habitat.z*i.uv.y*i.uv.y;
                float3 world=TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(world); o.uv=i.uv;
                return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*UNITY_ACCESS_INSTANCED_PROP(Sprite,_Tint);
                float4 expression=UNITY_ACCESS_INSTANCED_PROP(Sprite,_Expression);
                color.rgb=lerp(color.rgb,half3(1,.85,.52),expression.y*.24);
                float fog=UNITY_ACCESS_INSTANCED_PROP(Sprite,_Habitat).x;
                float luminance=dot(color.rgb,half3(.2126,.7152,.0722));
                color.rgb=lerp(color.rgb,lerp(luminance.xxx,half3(.035,.17,.23),.65),fog);
                clip(color.a-.005); return color;
            }
            ENDHLSL
        }
    }
}

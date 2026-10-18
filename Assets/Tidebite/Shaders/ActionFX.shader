Shader "Tidebite/ActionFX"
{
    Properties
    {
        _Tint("Color",Color)=(1,1,1,1)
        _FX("Age",Vector)=(0,0,0,0)
        _Mode("Crescent / impact / wake / evolution",Float)=0
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
            CBUFFER_START(UnityPerMaterial)
                float _Mode;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(Action)
                UNITY_DEFINE_INSTANCED_PROP(float4,_Tint)
                UNITY_DEFINE_INSTANCED_PROP(float4,_FX)
            UNITY_INSTANCING_BUFFER_END(Action)
            struct A { float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float age=saturate(UNITY_ACCESS_INSTANCED_PROP(Action,_FX).x);
                float4 tint=UNITY_ACCESS_INSTANCED_PROP(Action,_Tint);
                float2 p=(i.uv-.5)*2;
                float radius=length(p),angle=atan2(p.x,p.y),alpha=0;
                float fade=(1-age)*(1-age);
                if(_Mode<.5)
                {
                    float wave=.52+age*.35;
                    float arc=1-smoothstep(.72,1.22,abs(angle));
                    alpha=(1-smoothstep(.025,.10,abs(radius-wave)))*arc*fade;
                    alpha+=.35*(1-smoothstep(.018,.05,abs(radius-wave+.16)))*arc*fade;
                }
                else if(_Mode<1.5)
                {
                    float ring=1-smoothstep(.025,.10,abs(radius-.70));
                    float rays=pow(saturate(cos(angle*7)),14)*smoothstep(.15,.42,radius)*(1-smoothstep(.48,.92,radius));
                    alpha=(ring+rays*.8)*fade;
                }
                else if(_Mode<2.5)
                {
                    float along=i.uv.y;
                    float flank=.18+(1-along)*.52;
                    float stream=1-smoothstep(.012,.07,abs(abs(p.x)-flank));
                    float ripple=.55+.45*sin(along*30-age*15);
                    alpha=stream*sin(along*3.14159)*ripple;
                    alpha+=pow(saturate(1-abs(p.x)*2.5),3)*sin(along*3.14159)*.15;
                }
                else
                {
                    float ring=1-smoothstep(.014,.04,abs(radius-.78));
                    float broken=.55+.45*pow(saturate(sin(angle*6+age*4)),4);
                    alpha=ring*broken*fade;
                }
                return half4(tint.rgb,saturate(alpha)*tint.a);
            }
            ENDHLSL
        }
    }
}

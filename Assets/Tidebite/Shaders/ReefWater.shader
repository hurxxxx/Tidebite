Shader "Tidebite/ReefWater"
{
    Properties { _MainTex("Painted lagoon floor",2D)="white"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
            V vert(A i) { V o; o.world=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world); return o; }
            half4 frag(V i):SV_Target
            {
                float2 p=i.world.xz;
                float2 uv=p/float2(64,24)+.5+float2(sin(p.y*.2+_Time.y*.17),cos(p.x*.19+_Time.y*.14))*.001;
                half3 floor=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).rgb;
                half3 color=floor*half3(.65,.82,.88)+half3(.006,.018,.027);
                float tide=sin(p.x*.18+p.y*.14+_Time.y*.15)*.5+.5;
                color*=.91+tide*.09;
                float boundary=smoothstep(31.5,35,max(abs(p.x),abs(p.y)));
                color=lerp(color,half3(.025,.11,.14),boundary*.65);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}

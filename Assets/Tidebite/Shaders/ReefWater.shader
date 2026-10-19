Shader "Tidebite/ReefWater"
{
    Properties { _MainTex("Painted lagoon floor",2D)="white"{} _ViewCenter("View center / world half size",Vector)=(0,0,72,0) }
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
            float4 _OceanView;
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST,_ViewCenter;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
            V vert(A i) { V o; o.world=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world); return o; }
            half4 frag(V i):SV_Target
            {
                float2 p=i.world.xz;
                float2 uv=(p-_ViewCenter.xy*.92)/float2(64,24)+.5+float2(sin(p.y*.2+_Time.y*.17),cos(p.x*.19+_Time.y*.14))*.001;
                half3 floor=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).rgb;
                float2 view=(p-_OceanView.xy)/max(float2(1,1),_OceanView.zw);
                half3 water=lerp(half3(.018,.075,.14),half3(.035,.31,.37),saturate(view.y*.44+.50));
                // Keep distant painted detail atmospheric and the playable water calm.
                half3 color=lerp(water,floor*half3(.48,.65,.72),.34);
                float tide=sin(p.x*.18+p.y*.14+_Time.y*.15)*.5+.5;
                color*=.91+tide*.09;
                float shafts=pow(saturate(sin((view.x+view.y*.24)*10+_Time.y*.045)),18)*saturate(view.y*.5+.55);
                color+=half3(.055,.12,.12)*shafts*.13;
                float edge=smoothstep(.48,1.6,dot(view*float2(.82,.65),view*float2(.82,.65)));
                color*=1-edge*.24;
                float boundary=smoothstep(_ViewCenter.z-3,_ViewCenter.z+1,max(abs(p.x),abs(p.y)));
                color=lerp(color,half3(.025,.11,.14),boundary*.65);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}

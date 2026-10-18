Shader "Tidebite/WaterFX"
{
    Properties { _Tint("Color",Color)=(1,1,1,1) _Ring("Ring amount",Range(0,1))=0 }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint; float _Ring;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            V vert(A i) { V o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; return o; }
            half4 frag(V i):SV_Target
            {
                float d=length(i.uv-.5)*2;
                float soft=pow(saturate(1-d*d),2);
                float ring=smoothstep(.66,.80,d)*(1-smoothstep(.83,1,d));
                return half4(_Tint.rgb,_Tint.a*lerp(soft,ring,_Ring));
            }
            ENDHLSL
        }
    }
}

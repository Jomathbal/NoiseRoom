Shader "Custom/DomeCrossfade"
{
    Properties
    {
        _TexA ("Bild A", 2D) = "white" {}
        _TexB ("Bild B", 2D) = "black" {}
        _Blend ("Überblendung (0 = A, 1 = B)", Range(0, 1)) = 0

        [HDR] _Tint ("Tint / Helligkeit", Color) = (1, 1, 1, 1)

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (1 = Front für Innenansicht)", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "DomeCrossfade"
            Tags { "LightMode"="UniversalForward" }

            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_TexA);
            SAMPLER(sampler_TexA);
            TEXTURE2D(_TexB);
            SAMPLER(sampler_TexB);

            CBUFFER_START(UnityPerMaterial)
                float4 _TexA_ST;
                float4 _TexB_ST;
                float _Blend;
                half4 _Tint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uvA : TEXCOORD0;
                float2 uvB : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uvA = IN.uv * _TexA_ST.xy + _TexA_ST.zw;
                OUT.uvB = IN.uv * _TexB_ST.xy + _TexB_ST.zw;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 colA = SAMPLE_TEXTURE2D(_TexA, sampler_TexA, IN.uvA);
                half4 colB = SAMPLE_TEXTURE2D(_TexB, sampler_TexB, IN.uvB);
                return lerp(colA, colB, _Blend) * _Tint;
            }
            ENDHLSL
        }
    }
}

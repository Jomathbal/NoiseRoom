Shader "Custom/DomeCrossfade"
{
    Properties
    {
        _TexA ("Bild A", 2D) = "white" {}
        _TexB ("Bild B", 2D) = "black" {}
        _Blend ("Überblendung (0 = A, 1 = B)", Range(0, 1)) = 0

        [HDR] _Tint ("Tint / Helligkeit", Color) = (1, 1, 1, 1)

        _NoiseAmount ("Noise Amount (0 = clean, 1 = voll Noise)", Range(0, 1)) = 0
        _NoiseColor ("Noise Color", Color) = (0, 0, 0, 1)

        _FlickerSpeed ("Flicker Speed", Float) = 1.0
        // 0 = kein Flackern (Noise-Muster steht still)
        // >0 = pro Frame neu gewürfelt, höher = "wilderes" Flackern

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

            // Größe eines Noise-"Korns" in Bildschirm-Pixeln (fest, nicht im Inspector einstellbar)
            static const float NOISE_PIXEL_SIZE = 1.0;

            CBUFFER_START(UnityPerMaterial)
                float4 _TexA_ST;
                float4 _TexB_ST;
                float _Blend;
                half4 _Tint;
                float _NoiseAmount;
                half4 _NoiseColor;
                float _FlickerSpeed;
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

            // 2D-Hash: Pixelkoordinate + Seed (Zeit) -> pseudo-zufälliger Wert in [0,1)
            // (identisch zu RaytraceNoiseSurface, damit das Korn gleich aussieht)
            float hash12(float2 p, float seed)
            {
                float3 p3 = frac(p.xyx * 0.1031 + seed);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 colA = SAMPLE_TEXTURE2D(_TexA, sampler_TexA, IN.uvA);
                half4 colB = SAMPLE_TEXTURE2D(_TexB, sampler_TexB, IN.uvB);
                half4 col = lerp(colA, colB, _Blend) * _Tint;

                // Hash aus Bildschirm-Pixelkoordinate (+ Zeit für Flackern pro Frame)
                float2 pixelCoord = floor(IN.positionHCS.xy / NOISE_PIXEL_SIZE);
                float rnd = hash12(pixelCoord, _Time.y * _FlickerSpeed);

                // Hard Dropout: Pixel ist entweder Bildfarbe oder komplett Noise-Farbe
                float lit = step(rnd, 1.0 - _NoiseAmount);

                return half4(lerp(_NoiseColor.rgb, col.rgb, lit), 1.0);
            }
            ENDHLSL
        }
    }
}

Shader "Custom/EyesPictureNoiseFade"
{
    Properties
    {
        _BaseMap ("Augen-Foto", 2D) = "white" {}
        [HDR] _BaseColor ("Tint", Color) = (1,1,1,1)
        // HDR-Werte > 1 kippen über den Bloom-Threshold -> das Foto "verstrahlt" (wie GlowingEye).

        _FadeWidth ("Rand-Fade Breite (0..1)", Range(0.001, 1.0)) = 0.4
        // Breite des Übergangs vom Oval-Rand Richtung Mitte (radial normiert).
        // 1 = Fade reicht vom Oval-Rand bis zur Bildmitte.

        _FadeExponent ("Fade Kurve (Exponent)", Range(0.25, 8.0)) = 1.0
        // >1 = Foto bleibt länger dicht und zerfällt erst nah am Rand
        // <1 = Noise frisst sich weiter Richtung Mitte

        _Visibility ("Gesamt-Sichtbarkeit", Range(0.0, 1.0)) = 1.0
        // Für Kondensations-Animationen per Script: 0 = komplett aufgelöst, 1 = voll da.

        _FlickerSpeed ("Flicker Speed", Float) = 1.0
        // 0 = kein Flackern (Noise-Muster steht still)
        // >0 = pro Frame neu gewürfelt, höher = "wilderes" Flackern
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" }
        LOD 100

        Pass
        {
            Name "EyesPictureNoiseFade"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION; // im Fragment-Shader: Pixelkoordinaten (Screen Space)
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // Größe eines Noise-"Korns" in Bildschirm-Pixeln (fest, nicht im Inspector einstellbar)
            static const float NOISE_PIXEL_SIZE = 1.0;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _FadeWidth;
                float  _FadeExponent;
                float  _Visibility;
                float  _FlickerSpeed;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
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
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                // Ovale Maske: radialer Abstand zur Bildmitte, normiert auf die dem Quad
                // einbeschriebene Ellipse (0 = Mitte, 1 = Oval-Rand, Ecken > 1)
                float2 uv = IN.uv;
                float r = length(uv * 2.0 - 1.0);

                // Sichtbarkeit: 1 = Pixel überlebt sicher, 0 = Pixel wird sicher verworfen
                float visibility = saturate((1.0 - r) / _FadeWidth);
                visibility = pow(visibility, _FadeExponent);
                visibility *= _Visibility;
                visibility *= tex.a; // Alpha des Fotos (falls vorhanden) mitnehmen

                // Hash aus Bildschirm-Pixelkoordinate (+ Zeit für Flackern pro Frame)
                float2 pixelCoord = floor(IN.positionHCS.xy / NOISE_PIXEL_SIZE);
                float rnd = hash12(pixelCoord, _Time.y * _FlickerSpeed);

                // Hard Dropout: Pixel ist entweder voll da oder komplett weg ->
                // das Foto "kondensiert" aus denselben Partikeln wie der Kopf
                clip(visibility - rnd);

                return half4(tex.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}

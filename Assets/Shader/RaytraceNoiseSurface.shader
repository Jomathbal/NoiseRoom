Shader "Custom/RaytraceNoiseSurface"
{
    Properties
    {
        _BaseColor ("Color 1 (Licht-zugewandte Seite)", Color) = (1,1,1,1)
        _MidColor1 ("Color 2", Color) = (0.66,0.66,0.66,1)
        _MidColor2 ("Color 3", Color) = (0.33,0.33,0.33,1)
        _ShadowColor ("Color 4 (Noise/Schatten)", Color) = (0,0,0,1)

        _NoiseCurveTex ("Noise Falloff Curve (U: 0=0°, 1=180° / Wert: 0=clean, 1=voll Noise)", 2D) = "white" {}
        // Wird von NoiseFalloffCurveBaker.cs aus einer Unity AnimationCurve gebacken.
        // X-Achse der Kurve im Inspector = normalisierter Winkel (0=0°, 1=180°)
        // Y-Achse = Noise-Anteil (0=komplett clean, 1=komplett schwarz/voll Noise)

        _FlickerSpeed ("Flicker Speed", Float) = 1.0
        // 0 = kein Flackern (Noise-Muster steht still)
        // >0 = pro Frame neu gewürfelt, höher = "wilderes" Flackern
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS   : SV_POSITION; // im Fragment-Shader: Pixelkoordinaten (Screen Space)
                float3 normalWS      : TEXCOORD0;
            };

            TEXTURE2D(_NoiseCurveTex);
            SAMPLER(sampler_NoiseCurveTex);

            // Größe eines Noise-"Korns" in Bildschirm-Pixeln (fest, nicht im Inspector einstellbar)
            static const float NOISE_PIXEL_SIZE = 1.0;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _MidColor1;
                float4 _MidColor2;
                float4 _ShadowColor;
                float  _FlickerSpeed;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            // 2D-Hash: Pixelkoordinate + Seed (Zeit) -> pseudo-zufälliger Wert in [0,1)
            float hash12(float2 p, float seed)
            {
                float3 p3 = frac(p.xyx * 0.1031 + seed);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                Light mainLight = GetMainLight();
                float3 N = normalize(IN.normalWS);
                float3 L = normalize(mainLight.direction);

                float NdotL = dot(N, L);
                float angle = acos(clamp(NdotL, -1.0, 1.0)); // 0 = Richtung Licht, PI = weg vom Licht

                // Winkel (0..PI) normalisieren und Noise-Wahrscheinlichkeit aus der
                // im Inspector gezeichneten Kurve lesen (per Script gebackene Textur)
                float t = saturate(angle / PI);
                float noiseAmount = SAMPLE_TEXTURE2D(_NoiseCurveTex, sampler_NoiseCurveTex, float2(t, 0.5)).r;

                // Hash aus Bildschirm-Pixelkoordinate (+ Zeit für Flackern pro Frame)
                float2 pixelCoord = floor(IN.positionHCS.xy / NOISE_PIXEL_SIZE);
                float rnd = hash12(pixelCoord, _Time.y * _FlickerSpeed);

                // Position im 4-Farben-Verlauf: noiseAmount 0..1 -> 3 Übergänge (Color 1..4).
                // band = zwischen welchen zwei Nachbarfarben, f = Fortschritt im Übergang.
                float pos  = noiseAmount * 3.0;
                float band = min(floor(pos), 2.0);
                float f    = pos - band;

                half3 c0, c1;
                if (band < 0.5)      { c0 = _BaseColor.rgb; c1 = _MidColor1.rgb; }
                else if (band < 1.5) { c0 = _MidColor1.rgb; c1 = _MidColor2.rgb; }
                else                 { c0 = _MidColor2.rgb; c1 = _ShadowColor.rgb; }

                // Noisy Dithering statt weichem Verlauf: jeder Pixel bekommt gewürfelt
                // exakt eine der beiden Nachbarfarben, Wahrscheinlichkeit = f.
                half3 color = lerp(c0, c1, step(rnd, f));
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}

Shader "Custom/RaytraceNoiseSurface"
{
    Properties
    {
        _BaseColor ("Base Color (Licht-zugewandte Seite)", Color) = (1,1,1,1)
        _ShadowColor ("Noise/Schatten Color", Color) = (0,0,0,1)

        _NoiseCurveTex ("Noise Falloff Curve (U: 0=0°, 1=180° / Wert: 0=clean, 1=voll Noise)", 2D) = "white" {}
        // Wird von NoiseFalloffCurveBaker.cs aus einer Unity AnimationCurve gebacken.
        // X-Achse der Kurve im Inspector = normalisierter Winkel (0=0°, 1=180°)
        // Y-Achse = Noise-Anteil (0=komplett clean, 1=komplett schwarz/voll Noise)

        _NoiseScale ("Noise Spatial Scale", Float) = 20.0
        // Wie "fein" das Rauschmuster auf der Oberfläche ist.
        // Höher = feinkörniger/dichter, niedriger = grobkörniger/geklumpter.

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
                float4 positionHCS   : SV_POSITION;
                float3 normalWS      : TEXCOORD0;
                float3 positionOS    : TEXCOORD1; // Objektraum-Position, für stabilen Hash (klebt an Oberfläche)
            };

            TEXTURE2D(_NoiseCurveTex);
            SAMPLER(sampler_NoiseCurveTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowColor;
                float  _NoiseScale;
                float  _FlickerSpeed;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionOS  = IN.positionOS.xyz;
                return OUT;
            }

            // Einfache 3D-Hash-Funktion: Position + Seed (Zeit) -> pseudo-zufälliger Wert in [0,1)
            float hash13(float3 p, float seed)
            {
                p = frac(p * 0.3183099 + seed);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
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

                // Hash aus Objektraum-Position (+ Zeit für Flackern pro Frame)
                float rnd = hash13(IN.positionOS * _NoiseScale, _Time.y * _FlickerSpeed);

                // Hard Dropout: Pixel ist entweder normale Farbe oder komplett "Schatten"-Farbe
                float lit = step(rnd, 1.0 - noiseAmount);

                half3 color = lerp(_ShadowColor.rgb, _BaseColor.rgb, lit);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}

Shader "Custom/RaytraceNoiseSurfaceReflective"
{
    // Kopie von RaytraceNoiseSurface, erweitert um planare Spiegelung:
    // "lit" Pixel zeigen statt _BaseColor eine Mischung aus _BaseColor und der
    // von PlanarReflectionCamera.cs gerenderten Spiegelung. Die Noise-Pixel
    // bleiben unverändert _ShadowColor, damit der Dropout-Look erhalten bleibt.

    Properties
    {
        _BaseColor ("Base Color (Licht-zugewandte Seite)", Color) = (1,1,1,1)
        _ShadowColor ("Noise/Schatten Color", Color) = (0,0,0,1)

        _NoiseCurveTex ("Noise Falloff Curve (U: 0=0°, 1=180° / Wert: 0=clean, 1=voll Noise)", 2D) = "white" {}
        // Wird von NoiseFalloffCurveBaker.cs aus einer Unity AnimationCurve gebacken.

        _FlickerSpeed ("Flicker Speed", Float) = 1.0

        _ReflectionStrength ("Reflection Strength (0=matt, 1=voller Spiegel)", Range(0,1)) = 0.6
        // _PlanarReflectionTex ist bewusst KEINE Property: sie wird von
        // PlanarReflectionCamera.cs global gesetzt (Shader.SetGlobalTexture)
        // und würde sonst vom leeren Material-Slot überschrieben.
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
                float4 positionHCS : SV_POSITION; // im Fragment-Shader: Pixelkoordinaten (Screen Space)
                float3 normalWS    : TEXCOORD0;
                float4 screenPos   : TEXCOORD1;   // homogene Screen-UVs fuer die Spiegel-Textur
            };

            TEXTURE2D(_NoiseCurveTex);
            SAMPLER(sampler_NoiseCurveTex);

            // Global von PlanarReflectionCamera.cs gesetzt
            TEXTURE2D(_PlanarReflectionTex);
            SAMPLER(sampler_PlanarReflectionTex);

            // Größe eines Noise-"Korns" in Bildschirm-Pixeln (fest, nicht im Inspector einstellbar)
            static const float NOISE_PIXEL_SIZE = 1.0;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowColor;
                float  _FlickerSpeed;
                float  _ReflectionStrength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vpi.positionCS;
                OUT.screenPos   = vpi.positionNDC;
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

                float t = saturate(angle / PI);
                float noiseAmount = SAMPLE_TEXTURE2D(_NoiseCurveTex, sampler_NoiseCurveTex, float2(t, 0.5)).r;

                float2 pixelCoord = floor(IN.positionHCS.xy / NOISE_PIXEL_SIZE);
                float rnd = hash12(pixelCoord, _Time.y * _FlickerSpeed);

                // Hard Dropout: Pixel ist entweder normale Farbe oder komplett "Schatten"-Farbe
                float lit = step(rnd, 1.0 - noiseAmount);

                // Spiegelung: die Spiegelkamera rendert in X geflippt
                // (Projektion mit -1 in X, siehe PlanarReflectionCamera.cs),
                // deshalb hier 1-u beim Sampling.
                float2 reflUV = IN.screenPos.xy / IN.screenPos.w;
                reflUV.x = 1.0 - reflUV.x;
                half3 reflection = SAMPLE_TEXTURE2D(_PlanarReflectionTex, sampler_PlanarReflectionTex, reflUV).rgb;

                half3 litColor = lerp(_BaseColor.rgb, reflection, _ReflectionStrength);
                half3 color = lerp(_ShadowColor.rgb, litColor, lit);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}

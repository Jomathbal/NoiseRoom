Shader "Custom/SuctionLightRays"
{
    Properties
    {
        [HDR] _ColorBottom ("Strahl-Farbe unten", Color) = (1.4, 1.25, 1.0, 1)
        [HDR] _ColorTop ("Strahl-Farbe oben", Color) = (0.45, 0.42, 0.6, 1)
        [HDR] _RingColor ("Ring-Farbe (Boden)", Color) = (2.5, 2.2, 1.7, 1)

        _Intensity ("Gesamt-Intensität", Range(0, 10)) = 1.5

        _StreakCount ("Strahlen-Dichte (um den Umfang)", Float) = 32
        // Basis-Frequenz des Noise um den Umfang. Höher = mehr, schmalere Strahlen.

        _RayCutoff ("Strahlen-Schwelle", Range(0, 0.9)) = 0.45
        // Noise unterhalb dieser Schwelle wird schwarz -> je höher, desto sparsamer/kontrastiger die Strahlen.

        _HazeAmount ("Dunst zwischen den Strahlen", Range(0, 1)) = 0.35

        _NoiseStretch ("Vertikale Noise-Streckung", Float) = 2.5
        // Klein halten! Kleine Werte = lange vertikale Schlieren, große Werte = wolkig.

        _RiseSpeed ("Sog-Geschwindigkeit (nach unten)", Float) = 0.6

        _RayHeightMin ("Strahl-Höhe min", Range(0, 1)) = 0.45
        _RayHeightMax ("Strahl-Höhe max", Range(0, 1)) = 1.0
        _TipSpeed ("Wandern der Strahl-Spitzen", Float) = 0.15

        _HeightFalloff ("Helligkeits-Falloff nach oben", Range(0.1, 6)) = 1.6

        _RingHeight ("Ring-Höhe (Anteil der Zylinderhöhe)", Range(0.001, 0.5)) = 0.08
        _RingSharpness ("Ring-Schärfe", Range(0.5, 8)) = 2.5

        _EdgeAmount ("Silhouetten-Verstärkung", Range(0, 1)) = 0.35
        // Fakes Volumen: an der Silhouette (streifender Blick) wird der Effekt dichter.
        _EdgePower ("Silhouetten-Power", Range(0.5, 8)) = 2.0

        _EdgeFade ("Rand-Weichheit", Range(0.01, 1)) = 0.35
        // Blendet den Effekt zur Silhouette hin weich aus, statt hart an der Mesh-Kante zu enden.

        _GrainAmount ("Film-Korn", Range(0, 1)) = 0.25
        _GrainSpeed ("Korn-Flackern", Float) = 8.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "SuctionLight"
            Tags { "LightMode"="UniversalForward" }

            Blend One One   // additiv: Licht addiert sich auf den Hintergrund
            ZWrite Off
            Cull Off        // Innen- und Außenseite rendern -> wirkt dichter/volumetrischer

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorBottom;
                float4 _ColorTop;
                float4 _RingColor;
                float  _Intensity;
                float  _StreakCount;
                float  _RayCutoff;
                float  _HazeAmount;
                float  _NoiseStretch;
                float  _RiseSpeed;
                float  _RayHeightMin;
                float  _RayHeightMax;
                float  _TipSpeed;
                float  _HeightFalloff;
                float  _RingHeight;
                float  _RingSharpness;
                float  _EdgeAmount;
                float  _EdgePower;
                float  _EdgeFade;
                float  _GrainAmount;
                float  _GrainSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS  : TEXCOORD0;
                float3 normalOS    : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 viewDirWS   : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.positionOS  = IN.positionOS.xyz;
                OUT.normalOS    = IN.normalOS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS   = GetWorldSpaceViewDir(positionWS);
                return OUT;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            // 2D-Hash wie in RaytraceNoiseSurface: Pixelkoordinate + Seed -> [0,1)
            float hash12(float2 p, float seed)
            {
                float3 p3 = frac(p.xyx * 0.1031 + seed);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Value-Noise, periodisch in x mit Periode 'per' -> keine Naht am Zylinder-Umfang
            float pnoise(float2 p, float per)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float x0 = i.x - per * floor(i.x / per);
                float x1 = (i.x + 1.0) - per * floor((i.x + 1.0) / per);

                float a = hash21(float2(x0, i.y));
                float b = hash21(float2(x1, i.y));
                float c = hash21(float2(x0, i.y + 1.0));
                float d = hash21(float2(x1, i.y + 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p, float per)
            {
                float v = 0.0;
                float amp = 0.55;
                [unroll]
                for (int k = 0; k < 3; k++)
                {
                    v += amp * pnoise(p, per);
                    p *= 2.0;
                    per *= 2.0;
                    amp *= 0.5;
                }
                return v;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Deckel des Zylinders ausblenden, nur der Mantel leuchtet
                clip(0.5 - abs(IN.normalOS.y));

                // Unity-Zylinder: y in Objekt-Space von -1 (unten) bis +1 (oben)
                float h = saturate(IN.positionOS.y * 0.5 + 0.5);

                float per = max(round(_StreakCount), 2.0);
                float x = (atan2(IN.positionOS.x, IN.positionOS.z) / (2.0 * PI) + 0.5) * per;

                // Nach unten gesaugte, vertikal gestreckte Schlieren
                float2 p = float2(x, h * _NoiseStretch + _Time.y * _RiseSpeed);
                float n = fbm(p, per);

                float streak = smoothstep(_RayCutoff, 1.0, n);
                streak = streak * streak; // zusätzlich schärfen
                float rays = streak + n * _HazeAmount;

                // Strahl-Spitzen: pro Winkel unterschiedlich hoch, wandert langsam
                float tip = pnoise(float2(x, 7.3 + _Time.y * _TipSpeed), per);
                float rayLen = lerp(_RayHeightMin, _RayHeightMax, tip);
                float tipMask = 1.0 - smoothstep(rayLen * 0.6, rayLen, h);

                float heightFall = pow(saturate(1.0 - h), _HeightFalloff);
                rays *= tipMask * heightFall;

                half3 col = lerp(_ColorBottom.rgb, _ColorTop.rgb, h) * rays;

                // Heller Ring am Boden, mit leicht ungleichmäßigen Hotspots
                float ringVar = 0.7 + 0.6 * pnoise(float2(x, 3.7 + _Time.y * 0.1), per);
                float ring = pow(saturate(1.0 - h / _RingHeight), _RingSharpness) * ringVar;
                col += _RingColor.rgb * ring;

                // Silhouette dichter machen (Fake-Volumen)
                float ndotv = abs(dot(normalize(IN.normalWS), normalize(IN.viewDirWS)));
                float fres = pow(1.0 - saturate(ndotv), _EdgePower);
                col *= lerp(1.0, fres, _EdgeAmount);

                // Weicher Rand: zur Silhouette hin auf 0 ausblenden statt harter Mesh-Kante.
                // Quadriert, damit die Kurve am Rand flach ausläuft statt sichtbar anzusetzen.
                float rim = smoothstep(0.0, _EdgeFade, ndotv);
                col *= rim * rim;

                // Film-Korn wie im Rest des Projekts
                float grain = hash12(floor(IN.positionHCS.xy), _Time.y * _GrainSpeed);
                col *= 1.0 - _GrainAmount * grain;

                return half4(col * _Intensity, 1.0);
            }
            ENDHLSL
        }
    }
}

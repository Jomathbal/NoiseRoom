using System;
using UnityEngine;

// Zentrale Steuerung für Base Color und Schatten-Farbe mehrerer Materialien
// (Dach, Donut, Human), die alle den RaytraceNoiseSurface-Shader benutzen.
//
// Auf ein leeres "LightController"-GameObject legen und die Materialien
// im Inspector in die Liste ziehen. Änderungen an den beiden Farben werden
// dank [ExecuteAlways] + OnValidate sofort auf alle Materialien übernommen,
// auch im Edit Mode ohne Play.
//
// "Invert Colors" pro Eintrag tauscht Base- und Schatten-Farbe für dieses
// Material (z.B. Human: schwarzer Körper mit farbigem Noise statt umgekehrt).
[ExecuteAlways]
public class LightController : MonoBehaviour
{
    [Serializable]
    public class MaterialEntry
    {
        public Material material;

        [Tooltip("Tauscht Base- und Schatten-Farbe für dieses Material (z.B. Human)")]
        public bool invertColors;
    }

    [Tooltip("Alle Materialien, die zentral eingefärbt werden sollen (Dach, Donut, Human, ...)")]
    public MaterialEntry[] materials;

    [Tooltip("Licht-zugewandte Seite (_BaseColor)")]
    public Color baseColor = new Color(1f, 0.28045592f, 0f, 1f);

    [Tooltip("Noise/Schatten-Farbe (_ShadowColor)")]
    public Color shadowColor = Color.black;

    [Header("Base Color Rotation")]
    [Tooltip("Liste an Farben, durch die für die Base Color rotiert wird. Leer = keine Rotation, baseColor bleibt wie eingestellt.")]
    public Color[] baseColorCycle;

    [Tooltip("Dauer der Überblendung von einer Farbe zur nächsten in Sekunden.")]
    public float cycleInterval = 1f;

    [Tooltip("Rotation an/aus (läuft nur im Play Mode).")]
    public bool cycleEnabled = true;

    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    static readonly int ShadowColorID = Shader.PropertyToID("_ShadowColor");

    int cycleIndex;
    float cycleTimer;

    void OnEnable()
    {
        cycleTimer = 0f;
        cycleIndex = 0;

        // Im Play Mode direkt mit der ersten Farbe der Liste starten,
        // statt erst nach dem ersten Intervall umzuspringen.
        if (Application.isPlaying && cycleEnabled && baseColorCycle != null && baseColorCycle.Length > 0)
            baseColor = baseColorCycle[0];

        Apply();
    }

    void Update()
    {
        // Zeitbasierte Rotation nur im Play Mode; im Edit Mode läuft Update
        // durch [ExecuteAlways] nur sporadisch und würde unregelmäßig springen.
        if (!Application.isPlaying || !cycleEnabled || baseColorCycle == null || baseColorCycle.Length == 0)
            return;

        cycleTimer += Time.deltaTime;
        if (cycleTimer >= cycleInterval)
        {
            cycleTimer -= cycleInterval;
            cycleIndex = (cycleIndex + 1) % baseColorCycle.Length;
        }

        // Konstante Rotation: über das ganze Intervall von der aktuellen
        // zur nächsten Farbe blenden, ohne Haltephase.
        float t = Mathf.Clamp01(cycleTimer / cycleInterval);

        Color current = baseColorCycle[cycleIndex];
        Color next = baseColorCycle[(cycleIndex + 1) % baseColorCycle.Length];
        baseColor = LerpHSV(current, next, t);

        Apply();
    }

    void OnValidate()
    {
        Apply();
    }

    // Blendet über den HSV-Farbraum statt linear in RGB: Der Farbton wandert
    // über den kürzesten Weg auf dem Farbkreis, dadurch bleibt der Übergang
    // durchgehend satt statt durch ein Grau in der Mitte zu laufen.
    static Color LerpHSV(Color a, Color b, float t)
    {
        Color.RGBToHSV(a, out float h1, out float s1, out float v1);
        Color.RGBToHSV(b, out float h2, out float s2, out float v2);

        // Bei unbuntem Start/Ziel (Sättigung 0, z.B. Schwarz/Weiß/Grau) ist der
        // Farbton undefiniert – dann den Ton der anderen Farbe übernehmen,
        // statt sinnlos von Rot (Hue 0) loszudrehen.
        if (s1 <= 0f) h1 = h2;
        if (s2 <= 0f) h2 = h1;

        float h = Mathf.Repeat(h1 + Mathf.DeltaAngle(h1 * 360f, h2 * 360f) / 360f * t, 1f);

        Color result = Color.HSVToRGB(h, Mathf.Lerp(s1, s2, t), Mathf.Lerp(v1, v2, t));
        result.a = Mathf.Lerp(a.a, b.a, t);
        return result;
    }

    // Kann auch zur Laufzeit von anderen Scripts aufgerufen werden,
    // nachdem baseColor/shadowColor per Code geändert wurden.
    public void Apply()
    {
        if (materials == null)
            return;

        foreach (var entry in materials)
        {
            if (entry == null || entry.material == null)
                continue;

            entry.material.SetColor(BaseColorID, entry.invertColors ? shadowColor : baseColor);
            entry.material.SetColor(ShadowColorID, entry.invertColors ? baseColor : shadowColor);
        }
    }

    // Setzt beide Farben per Code und wendet sie sofort an.
    public void SetColors(Color newBaseColor, Color newShadowColor)
    {
        baseColor = newBaseColor;
        shadowColor = newShadowColor;
        Apply();
    }
}

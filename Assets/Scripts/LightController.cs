using System;
using UnityEngine;

// Zentrale Steuerung für die vier Farben des RaytraceNoiseSurface-Shaders
// auf mehreren Materialien (Dach, Donut, Human).
//
// Auf ein leeres "LightController"-GameObject legen und die Materialien
// im Inspector in die Liste ziehen. Änderungen an den Farben werden
// dank [ExecuteAlways] + OnValidate sofort auf alle Materialien übernommen,
// auch im Edit Mode ohne Play.
//
// "Invert Colors" pro Eintrag spiegelt den Verlauf für dieses Material:
// Farbe 1 tauscht mit 4, Farbe 2 mit 3 (z.B. Human: schwarzer Körper
// mit farbigem Noise statt umgekehrt).
[ExecuteAlways]
public class LightController : MonoBehaviour
{
    [Serializable]
    public class MaterialEntry
    {
        public Material material;

        [Tooltip("Spiegelt den Verlauf für dieses Material: Farbe 1 tauscht mit 4, Farbe 2 mit 3 (z.B. Human)")]
        public bool invertColors;

        [Tooltip("Dauer der Überblendung von einer Palette zur nächsten in Sekunden.")]
        public float cycleInterval = 1f;

        [Tooltip("Zeitversatz in Sekunden, um Materialien gegeneinander zu verschieben.")]
        public float cycleOffset;
    }

    // Eine komplette 4-Farben-Palette für den Shader-Verlauf.
    [Serializable]
    public class Palette
    {
        [Tooltip("Color 1 (_BaseColor, Licht-zugewandte Seite)")]
        public Color color1 = Color.white;

        [Tooltip("Color 2 (_MidColor1)")]
        public Color color2 = new Color(0.66f, 0.66f, 0.66f, 1f);

        [Tooltip("Color 3 (_MidColor2)")]
        public Color color3 = new Color(0.33f, 0.33f, 0.33f, 1f);

        [Tooltip("Color 4 (_ShadowColor, Noise/Schatten)")]
        public Color color4 = Color.black;
    }

    [Tooltip("Alle Materialien, die zentral eingefärbt werden sollen (Dach, Donut, Human, ...)")]
    public MaterialEntry[] materials;

    [Tooltip("Die vier Verlaufsfarben, solange keine Paletten-Rotation läuft.")]
    public Palette colors = new Palette();

    [Header("Palette Rotation")]
    [Tooltip("Paletten, durch die rotiert wird. Leer = keine Rotation, die Farben oben bleiben wie eingestellt.")]
    public Palette[] paletteCycle;

    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    static readonly int MidColor1ID = Shader.PropertyToID("_MidColor1");
    static readonly int MidColor2ID = Shader.PropertyToID("_MidColor2");
    static readonly int ShadowColorID = Shader.PropertyToID("_ShadowColor");

    void OnEnable()
    {
        Apply();
    }

    void Update()
    {
        // Zeitbasierte Rotation nur im Play Mode; im Edit Mode läuft Update
        // durch [ExecuteAlways] nur sporadisch und würde unregelmäßig springen.
        if (!Application.isPlaying || paletteCycle == null || paletteCycle.Length == 0 || materials == null)
            return;

        foreach (var entry in materials)
        {
            if (entry == null || entry.material == null)
                continue;

            // Position im Paletten-Kreis aus Zeit, Offset und Intervall des Eintrags:
            // ganzzahliger Teil = aktuelle Palette, Nachkommateil = Blendfortschritt.
            float interval = Mathf.Max(entry.cycleInterval, 0.0001f);
            float pos = (Time.time + entry.cycleOffset) / interval;

            int index = (int)Mathf.Repeat(Mathf.Floor(pos), paletteCycle.Length);
            float t = Mathf.Repeat(pos, 1f);

            Palette current = paletteCycle[index];
            Palette next = paletteCycle[(index + 1) % paletteCycle.Length];

            ApplyToMaterial(entry,
                LerpHSV(current.color1, next.color1, t),
                LerpHSV(current.color2, next.color2, t),
                LerpHSV(current.color3, next.color3, t),
                LerpHSV(current.color4, next.color4, t));
        }
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

    static void ApplyToMaterial(MaterialEntry entry, Color c1, Color c2, Color c3, Color c4)
    {
        if (entry.invertColors)
        {
            (c1, c4) = (c4, c1);
            (c2, c3) = (c3, c2);
        }

        entry.material.SetColor(BaseColorID, c1);
        entry.material.SetColor(MidColor1ID, c2);
        entry.material.SetColor(MidColor2ID, c3);
        entry.material.SetColor(ShadowColorID, c4);
    }

    // Kann auch zur Laufzeit von anderen Scripts aufgerufen werden,
    // nachdem die Farben in "colors" per Code geändert wurden.
    public void Apply()
    {
        if (materials == null || colors == null)
            return;

        foreach (var entry in materials)
        {
            if (entry == null || entry.material == null)
                continue;

            ApplyToMaterial(entry, colors.color1, colors.color2, colors.color3, colors.color4);
        }
    }

    // Setzt alle vier Farben per Code und wendet sie sofort an.
    public void SetColors(Color color1, Color color2, Color color3, Color color4)
    {
        colors.color1 = color1;
        colors.color2 = color2;
        colors.color3 = color3;
        colors.color4 = color4;
        Apply();
    }
}

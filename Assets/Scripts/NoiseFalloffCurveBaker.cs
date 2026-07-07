using UnityEngine;

// Backt eine im Inspector editierbare AnimationCurve in eine 1D-Textur
// und übergibt sie an das Material der RaytraceNoiseSurface-Shader-Instanz.
//
// X-Achse der Kurve: normalisierter Winkel zum Licht (0 = 0°, 1 = 180°)
// Y-Achse der Kurve: Noise-Anteil (0 = komplett clean, 1 = komplett schwarz/voll Noise)
//
// [ExecuteAlways] sorgt dafür, dass die Kurve auch im Edit Mode (ohne Play)
// sofort im Material aktualisiert wird, wenn du sie im Inspector änderst.
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class NoiseFalloffCurveBaker : MonoBehaviour
{
    [Tooltip("X = normalisierter Winkel (0=0°, 1=180°), Y = Noise-Anteil (0=clean, 1=voll Noise/schwarz)")]
    public AnimationCurve noiseCurve = new AnimationCurve(
        new Keyframe(0.00f, 0.00f),   // 0°   -> clean
        new Keyframe(0.25f, 0.25f),   // 45°  -> noisy
        new Keyframe(0.50f, 0.60f),   // 90°  -> sehr noisy
        new Keyframe(0.75f, 0.85f),   // 135° -> extrem noisy
        new Keyframe(1.00f, 1.00f)    // 180° -> schwarz
    );

    [Tooltip("Auflösung der gebackenen Kurven-Textur. 256 reicht für glatte Übergänge locker aus.")]
    [Range(8, 1024)]
    public int resolution = 256;

    Renderer _renderer;
    Texture2D _curveTexture;

    static readonly int NoiseCurveTexID = Shader.PropertyToID("_NoiseCurveTex");

    void OnEnable()
    {
        Bake();
    }

    void OnValidate()
    {
        // Wird im Editor bei jeder Änderung im Inspector aufgerufen (Curve, Resolution, ...)
        Bake();
    }

    void Bake()
    {
        if (_renderer == null)
            _renderer = GetComponent<Renderer>();

        if (_renderer == null || noiseCurve == null)
            return;

        if (_curveTexture == null || _curveTexture.width != resolution)
        {
            if (_curveTexture != null)
                DestroyImmediate(_curveTexture);

            _curveTexture = new Texture2D(resolution, 1, TextureFormat.RFloat, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "BakedNoiseFalloffCurve"
            };
        }

        for (int i = 0; i < resolution; i++)
        {
            float t = i / (float)(resolution - 1);
            float value = Mathf.Clamp01(noiseCurve.Evaluate(t));
            _curveTexture.SetPixel(i, 0, new Color(value, value, value, value));
        }
        _curveTexture.Apply(false, false);

        // sharedMaterial bewusst statt .material, damit im Edit Mode keine
        // Material-Instanz erzeugt und das Original-Asset direkt aktualisiert wird.
        var mat = _renderer.sharedMaterial;
        if (mat != null)
            mat.SetTexture(NoiseCurveTexID, _curveTexture);
    }
}
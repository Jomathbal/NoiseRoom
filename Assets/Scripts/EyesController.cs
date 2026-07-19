using UnityEngine;

// Zentrale Steuerung für ALLE Augenpaare. Liegt einmal in der Szene auf dem
// EyesController-GO – das Interval hier ändern, und alle EyeController
// übernehmen es sofort (auch im Play Mode).
public class EyesController : MonoBehaviour
{
    [Tooltip("Anzeigedauer pro Bildpaar in Sekunden – gilt zentral für alle Augenpaare.")]
    [Min(0.02f)]
    public float interval = 0.1f;

    const float FallbackInterval = 0.1f;

    static EyesController instance;

    void Awake()
    {
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static float Interval
    {
        get
        {
            if (instance == null)
                instance = FindAnyObjectByType<EyesController>();
            return instance != null ? instance.interval : FallbackInterval;
        }
    }
}

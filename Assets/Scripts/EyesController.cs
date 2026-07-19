using UnityEngine;

// Zentrale Steuerung für ALLE Augenpaare. Liegt einmal in der Szene auf dem
// EyesController-GO – das Interval hier ändern, und alle EyeController
// übernehmen es sofort (auch im Play Mode).
public class EyesController : MonoBehaviour
{
    [Tooltip("Kamera, deren XZ-Abstand zu diesem GO das Interval steuert. Leer = Camera.main.")]
    public Camera mainCamera;

    [Header("Abstand → Interval Mapping")]
    [Tooltip("Ab diesem XZ-Abstand (und darunter) gilt minInterval.")]
    [Min(0f)]
    public float minDistance = 1f;

    [Tooltip("Ab diesem XZ-Abstand (und darüber) gilt maxInterval.")]
    [Min(0f)]
    public float maxDistance = 10f;

    [Tooltip("Interval bei minimalem Abstand (Kamera nah).")]
    [Min(0.02f)]
    public float minInterval = 0.05f;

    [Tooltip("Interval bei maximalem Abstand (Kamera weit weg).")]
    [Min(0.02f)]
    public float maxInterval = 0.5f;

    [Tooltip("Aktueller XZ-Abstand zur Kamera – wird zur Laufzeit berechnet.")]
    public float distance;

    [Tooltip("Aktuelles Interval – wird zur Laufzeit aus dem Kamera-Abstand berechnet.")]
    [Min(0.02f)]
    public float interval = 0.1f;

    const float FallbackInterval = 0.1f;

    static EyesController instance;

    void Awake()
    {
        instance = this;
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
                return;
        }

        Vector3 camPos = mainCamera.transform.position;
        Vector3 selfPos = transform.position;
        float dx = camPos.x - selfPos.x;
        float dz = camPos.z - selfPos.z;
        distance = Mathf.Sqrt(dx * dx + dz * dz);

        float t = Mathf.InverseLerp(minDistance, maxDistance, distance);
        interval = Mathf.Lerp(minInterval, maxInterval, t);
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

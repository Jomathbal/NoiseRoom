using UnityEngine;

public class EyeController : MonoBehaviour
{
    [Tooltip("Anzeigedauer pro Bild in Sekunden. Wird auf alle EyeCycler in den Kind-Objekten (z.B. Right Eyes und Left Eyes) übertragen.")]
    public float interval = 1f;

    void Awake()
    {
        ApplyInterval();
    }

    public void ApplyInterval()
    {
        foreach (var cycler in GetComponentsInChildren<EyeCycler>(true))
        {
            if (cycler.interval == interval)
                continue;

            cycler.interval = interval;
#if UNITY_EDITOR
            // Damit der übertragene Wert auch mit der Szene gespeichert wird.
            UnityEditor.EditorUtility.SetDirty(cycler);
#endif
        }
    }

#if UNITY_EDITOR
    // Überträgt Änderungen aus dem Inspector sofort auf die Kinder.
    void OnValidate()
    {
        if (gameObject.scene.IsValid())
            ApplyInterval();
    }
#endif
}

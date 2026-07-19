using UnityEngine;

/// <summary>
/// Berechnet einmal pro Frame einen gemeinsamen Morph-Faktor t aus der Kameradistanz
/// und gibt ihn an alle HumanPlacer weiter: t = 1 → Kegel, t = 0 → Zylinder.
/// </summary>
public class HumanConeController : MonoBehaviour
{
    [SerializeField] private Camera mainCam;
    [SerializeField] private HumanPlacer[] placers;
    [SerializeField, Min(0f)] private float nearDistance = 2f;
    [SerializeField, Min(0f)] private float farDistance = 20f;
    [SerializeField, Min(0f)] private float lerpSpeed = 3f;
    // Min-Radius bei t = 0, gilt für alle Ringe gemeinsam
    [SerializeField, Min(0f)] private float cylinderRadius = 15f;

    public float CylinderRadius => cylinderRadius;

    [Header("Debug (nur Anzeige)")]
    [SerializeField] private float currentDistance;

    private float t = 1f;

    private void Awake()
    {
        if (placers == null || placers.Length == 0)
            placers = GetComponentsInChildren<HumanPlacer>();
    }

    private void Update()
    {
        if (mainCam == null)
            return;

        Vector3 delta = mainCam.transform.position - transform.position;
        delta.y = 0f;
        currentDistance = delta.magnitude;
        float raw = Mathf.InverseLerp(nearDistance, farDistance, currentDistance);
        // Framerate-unabhängiges Lerp Richtung Zielwert
        t = Mathf.Lerp(t, raw, 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime));

        foreach (HumanPlacer placer in placers)
        {
            if (placer != null)
                placer.SetMorph(t, cylinderRadius);
        }
    }
}

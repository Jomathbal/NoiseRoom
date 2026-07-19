using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class HumanPlacer : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField, Min(0)] private int count = 10;
    [SerializeField, Min(0f)] private float radius = 5f;
    [SerializeField] private bool faceCenter = false;

    [Header("Camera Tracking")]
    [SerializeField] private Camera mainCam;
    [SerializeField, Min(0f)] private float padding = 2f;
    [SerializeField, Min(0f)] private float minRadius = 1f;
    [SerializeField, Min(0f)] private float maxRadius = 20f;
    [SerializeField, Min(0f)] private float radiusLerpSpeed = 3f;

    private void Start()
    {
        if (Application.isPlaying)
            Rebuild();
    }

    private void Update()
    {
        if (mainCam == null)
            return;

        Vector3 delta = mainCam.transform.position - transform.position;
        delta.y = 0f;
        float targetRadius = Mathf.Clamp(delta.magnitude + padding, minRadius, maxRadius);
        // Framerate-unabhängiges Lerp Richtung Zielradius
        float newRadius = Mathf.Lerp(radius, targetRadius, 1f - Mathf.Exp(-radiusLerpSpeed * Time.deltaTime));

        if (!Mathf.Approximately(newRadius, radius))
        {
            radius = newRadius;
            Reposition();
        }
    }

    private void Reposition()
    {
        int childCount = transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            float angle = i * Mathf.PI * 2f / childCount;
            Vector3 localPos = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Transform child = transform.GetChild(i);
            child.localPosition = localPos;

            if (faceCenter)
                child.localRotation = Quaternion.LookRotation(-localPos.normalized, Vector3.up);
        }
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        // Instantiate/DestroyImmediate darf nicht direkt in OnValidate laufen
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            Rebuild();
        };
#endif
    }

    private void Rebuild()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        if (prefab == null || count <= 0)
            return;

        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Vector3 localPos = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

            GameObject instance = null;
#if UNITY_EDITOR
            // Funktioniert nur mit Prefab-Assets; bei Szenenobjekten kommt null zurück
            if (!Application.isPlaying)
                instance = PrefabUtility.InstantiatePrefab(prefab, transform) as GameObject;
#endif
            if (instance == null)
                instance = Instantiate(prefab, transform);
            if (instance == null)
                continue;
            instance.transform.localPosition = localPos;

            if (faceCenter)
                instance.transform.localRotation = Quaternion.LookRotation(-localPos.normalized, Vector3.up);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        const int segments = 64;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments;
            float a1 = (i + 1) * Mathf.PI * 2f / segments;
            Gizmos.DrawLine(
                new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius,
                new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius);
        }
    }
}

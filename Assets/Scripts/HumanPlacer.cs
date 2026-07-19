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

    private void Start()
    {
        if (Application.isPlaying)
            Rebuild();
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

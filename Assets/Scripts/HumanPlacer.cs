using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class HumanPlacer : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField, Min(0)] private int count = 10;
    [SerializeField, Min(0f)] private float humanScale = 1f;
    [SerializeField] private bool faceCenter = false;

    [Header("Morph Radien")]
    [SerializeField, Min(0f)] private float coneRadius = 5f; // Radius bei t = 1 (Kegelform, pro Ring unterschiedlich)

    // Aktuell wirksamer Radius; wird zur Laufzeit vom HumanConeController über SetMorph gesetzt
    private float radius;

    private void Start()
    {
        if (Application.isPlaying)
        {
            radius = coneRadius;
            Rebuild();
        }
    }

    /// <summary>Setzt den Morph-Faktor: t = 1 → Kegelradius, t = 0 → Zylinderradius (kommt vom Controller).</summary>
    public void SetMorph(float t, float cylinderRadius)
    {
        float newRadius = Mathf.Lerp(cylinderRadius, coneRadius, Mathf.Clamp01(t));
        if (Mathf.Approximately(newRadius, radius))
            return;

        radius = newRadius;
        Reposition();
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
            if (!Application.isPlaying)
                radius = coneRadius; // Editor-Vorschau zeigt die Kegelform (t = 1)
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
            instance.transform.localScale = prefab.transform.localScale * humanScale;

            if (faceCenter)
                instance.transform.localRotation = Quaternion.LookRotation(-localPos.normalized, Vector3.up);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        DrawCircle(coneRadius, Color.cyan);

        HumanConeController controller = GetComponentInParent<HumanConeController>();
        if (controller != null)
            DrawCircle(controller.CylinderRadius, Color.gray);
    }

    private static void DrawCircle(float r, Color color)
    {
        Gizmos.color = color;
        const int segments = 64;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments;
            float a1 = (i + 1) * Mathf.PI * 2f / segments;
            Gizmos.DrawLine(
                new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r,
                new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r);
        }
    }
}

using System.Collections;
using System.Linq;
using UnityEngine;

/// Überblendet vorab geladene Bilder als Base Albedo auf dem Dome.
/// Erwartet ein Material mit dem Shader "Custom/DomeCrossfade" (_TexA, _TexB, _Blend).
public class DomeImageFader : MonoBehaviour
{
    [SerializeField] private Renderer domeRenderer;
    [SerializeField, HideInInspector] private string imageFolder; // Projektpfad, z.B. "Assets/Images/sky"
    [SerializeField] private Texture2D[] images; // wird aus imageFolder befüllt
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private int startIndex = 0;

    private static readonly int TexA = Shader.PropertyToID("_TexA");
    private static readonly int TexB = Shader.PropertyToID("_TexB");
    private static readonly int Blend = Shader.PropertyToID("_Blend");

    private Material mat;
    private bool showingA = true;
    private int currentIndex = -1;
    private int pendingIndex = -1; // Wechsel, der während eines laufenden Fades angefragt wurde
    private Coroutine running;

    public int CurrentIndex => currentIndex;

    void Awake()
    {
        // Instanz, damit das Material-Asset nicht verändert wird
        mat = domeRenderer.material;

        if (images != null && images.Length > 0)
        {
            currentIndex = Mathf.Clamp(startIndex, 0, images.Length - 1);
            mat.SetTexture(TexA, images[currentIndex]);
        }
        mat.SetFloat(Blend, 0f);
    }

    public void ShowNext() => ShowImage((currentIndex + 1) % images.Length);

    public void ShowPrevious() => ShowImage((currentIndex - 1 + images.Length) % images.Length);

    /// Zeigt das Bild mit dem angegebenen Texturnamen (ohne Dateiendung). Liefert false, wenn es nicht existiert.
    public bool ShowImage(string imageName)
    {
        if (images == null) return false;

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null &&
                string.Equals(images[i].name, imageName, System.StringComparison.OrdinalIgnoreCase))
            {
                ShowImage(i);
                return true;
            }
        }
        return false;
    }

    public void ShowImage(int index)
    {
        if (images == null || index < 0 || index >= images.Length) return;
        if (index == currentIndex && running == null) return;

        if (running != null)
        {
            // Laufenden Fade nicht unterbrechen (würde sichtbar springen),
            // sondern den Wechsel danach ausführen
            pendingIndex = index;
            return;
        }

        running = StartCoroutine(Fade(index));
    }

    private IEnumerator Fade(int index)
    {
        // Neues Bild in den gerade unsichtbaren Slot legen
        mat.SetTexture(showingA ? TexB : TexA, images[index]);

        float from = showingA ? 0f : 1f;
        float to = 1f - from;

        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            mat.SetFloat(Blend, Mathf.SmoothStep(from, to, t / fadeDuration));
            yield return null;
        }
        mat.SetFloat(Blend, to);

        showingA = !showingA;
        currentIndex = index;
        running = null;

        if (pendingIndex >= 0)
        {
            int next = pendingIndex;
            pendingIndex = -1;
            ShowImage(next);
        }
    }

#if UNITY_EDITOR
    /// Befüllt das images-Array mit allen Texturen aus imageFolder (alphabetisch sortiert).
    public void ReloadImagesFromFolder()
    {
        if (string.IsNullOrEmpty(imageFolder) || !UnityEditor.AssetDatabase.IsValidFolder(imageFolder))
        {
            Debug.LogWarning($"DomeImageFader: \"{imageFolder}\" ist kein gültiger Ordner im Projekt.", this);
            return;
        }

        images = UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { imageFolder })
            .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, System.StringComparer.OrdinalIgnoreCase)
            .Select(UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>)
            .Where(tex => tex != null)
            .ToArray();

        UnityEditor.EditorUtility.SetDirty(this);
    }

    [UnityEditor.CustomEditor(typeof(DomeImageFader))]
    private class DomeImageFaderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var fader = (DomeImageFader)target;

            // Ordner per Drag & Drop zuweisen; bei Änderung sofort neu laden
            var currentFolder = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.DefaultAsset>(fader.imageFolder ?? "");
            UnityEditor.EditorGUI.BeginChangeCheck();
            var newFolder = UnityEditor.EditorGUILayout.ObjectField(
                "Bilder-Ordner", currentFolder, typeof(UnityEditor.DefaultAsset), false);
            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                UnityEditor.Undo.RecordObject(fader, "Bilder-Ordner ändern");
                fader.imageFolder = newFolder != null ? UnityEditor.AssetDatabase.GetAssetPath(newFolder) : "";
                if (newFolder != null) fader.ReloadImagesFromFolder();
                UnityEditor.EditorUtility.SetDirty(fader);
            }

            if (GUILayout.Button("Bilder neu laden")) fader.ReloadImagesFromFolder();

            DrawDefaultInspector();

            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.LabelField("Debug", UnityEditor.EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                UnityEditor.EditorGUILayout.HelpBox("Bildwechsel nur im Play Mode möglich.", UnityEditor.MessageType.Info);
                return;
            }

            using (new UnityEditor.EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("◀ Vorheriges")) fader.ShowPrevious();
                if (GUILayout.Button("Nächstes ▶")) fader.ShowNext();
            }

            if (fader.images == null) return;

            for (int i = 0; i < fader.images.Length; i++)
            {
                string label = fader.images[i] != null ? $"{i}: {fader.images[i].name}" : $"{i}: (leer)";
                using (new UnityEditor.EditorGUI.DisabledScope(i == fader.CurrentIndex))
                {
                    if (GUILayout.Button(label)) fader.ShowImage(i);
                }
            }
        }
    }
#endif
}

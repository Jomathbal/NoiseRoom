using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Funktioniert sowohl mit einem UI-Image (Canvas) als auch mit einem
// SpriteRenderer (3D-Welt) – je nachdem, was auf dem GameObject liegt.
public class EyeCycler : MonoBehaviour
{
    [Tooltip("Ordner (relativ zum Projekt, z.B. Assets/Images/right_eyes), aus dem die Sprites im Editor automatisch geladen werden.")]
    public string spriteFolder = "Assets/Images/right_eyes";

    [Tooltip("Die Sprites, durch die durchgewechselt wird (Reihenfolge = Anzeige-Reihenfolge). Wird im Editor automatisch aus dem Ordner befüllt.")]
    public Sprite[] eyeSprites;

    [Tooltip("Anzeigedauer pro Bild in Sekunden.")]
    public float interval = 0.5f;

    [Tooltip("Bilder in zufälliger Reihenfolge statt der Reihe nach anzeigen.")]
    public bool randomOrder = false;

    Image image;
    SpriteRenderer spriteRenderer;
    int index;
    float timer;

    void Awake()
    {
        image = GetComponent<Image>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (image == null && spriteRenderer == null)
            Debug.LogWarning($"{nameof(EyeCycler)} auf \"{name}\" benötigt ein Image oder einen SpriteRenderer.", this);
    }

    void OnEnable()
    {
        timer = 0f;
        ShowCurrent();
    }

    void Update()
    {
        if (eyeSprites == null || eyeSprites.Length == 0)
            return;

        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer -= interval;
            Advance();
        }
    }

    void Advance()
    {
        if (randomOrder && eyeSprites.Length > 1)
        {
            // Zufälliges Bild wählen, aber nicht zweimal dasselbe hintereinander.
            int next = Random.Range(0, eyeSprites.Length - 1);
            if (next >= index)
                next++;
            index = next;
        }
        else
        {
            index = (index + 1) % eyeSprites.Length;
        }

        ShowCurrent();
    }

    void ShowCurrent()
    {
        if (eyeSprites == null || eyeSprites.Length == 0 || eyeSprites[index] == null)
            return;

        if (image != null)
            image.sprite = eyeSprites[index];
        else if (spriteRenderer != null)
            spriteRenderer.sprite = eyeSprites[index];
    }

#if UNITY_EDITOR
    // Befüllt das Array automatisch, sobald sich im Inspector etwas ändert
    // (z.B. der Ordnerpfad). Läuft nur im Editor; im Build wird das
    // serialisierte Array verwendet.
    void OnValidate()
    {
        LoadSpritesFromFolder();
    }

    [ContextMenu("Sprites aus Ordner laden")]
    void LoadSpritesFromFolder()
    {
        if (string.IsNullOrEmpty(spriteFolder) || !AssetDatabase.IsValidFolder(spriteFolder))
            return;

        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { spriteFolder });
        var sprites = new Sprite[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        // FindAssets garantiert keine Reihenfolge, daher nach Pfad sortieren.
        System.Array.Sort(sprites, (a, b) => string.CompareOrdinal(
            AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)));

        eyeSprites = sprites;
    }
#endif
}

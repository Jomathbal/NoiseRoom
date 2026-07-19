using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Reine Anzeige-Komponente für ein einzelnes Auge (UI-Image oder SpriteRenderer).
// Das Durchschalten übernimmt der EyeController auf dem übergeordneten
// Eyes-Objekt, damit linkes und rechtes Auge immer das zusammengehörige
// Bildpaar zeigen (gleicher Index in den nach Dateinamen sortierten Ordnern).
public class EyeCycler : MonoBehaviour
{
    [Tooltip("Ordner, aus dem die Sprites im Editor geladen werden. Wird vom EyeController auf dem Eltern-Objekt gesetzt.")]
    public string spriteFolder = "Assets/Images/right_eyes";

    [Tooltip("Die Sprites dieses Auges, nach Dateiname sortiert. Wird im Editor automatisch aus dem Ordner befüllt.")]
    public Sprite[] eyeSprites;

    public int SpriteCount => eyeSprites != null ? eyeSprites.Length : 0;

    Image image;
    SpriteRenderer spriteRenderer;
    bool lookedUp;

    void Awake()
    {
        EnsureRenderer();

        if (image == null && spriteRenderer == null)
            Debug.LogWarning($"{nameof(EyeCycler)} auf \"{name}\" benötigt ein Image oder einen SpriteRenderer.", this);
    }

    // Wird vom EyeController aufgerufen; kann vor Awake passieren, daher lazy lookup.
    public void Show(int index)
    {
        if (eyeSprites == null || index < 0 || index >= eyeSprites.Length || eyeSprites[index] == null)
            return;

        EnsureRenderer();

        if (image != null)
            image.sprite = eyeSprites[index];
        else if (spriteRenderer != null)
            spriteRenderer.sprite = eyeSprites[index];
    }

    void EnsureRenderer()
    {
        if (lookedUp)
            return;
        lookedUp = true;
        image = GetComponent<Image>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        LoadSpritesFromFolder();
    }

    [ContextMenu("Sprites aus Ordner laden")]
    public void LoadSpritesFromFolder()
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
        // FindAssets garantiert keine Reihenfolge, daher nach Pfad sortieren –
        // so bekommt eye_01 links und eye_01 rechts denselben Index.
        System.Array.Sort(sprites, (a, b) => string.CompareOrdinal(
            AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)));

        eyeSprites = sprites;
    }
#endif
}

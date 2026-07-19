using UnityEngine;

// Steuert beide Augen eines Augenpaars synchron: linkes und rechtes Auge
// zeigen immer das zusammengehörige Bildpaar (gleicher Index in den nach
// Dateinamen sortierten Ordnern) – auch bei zufälliger Reihenfolge.
// Das Interval kommt zentral vom EyesController-GO in der Szene.
public class EyeController : MonoBehaviour
{
    [Tooltip("Ordner mit den Bildern für das linke Auge.")]
    public string leftSpriteFolder = "Assets/Images/left_eyes";

    [Tooltip("Ordner mit den Bildern für das rechte Auge.")]
    public string rightSpriteFolder = "Assets/Images/right_eyes";

    [Tooltip("Bildpaare in zufälliger Reihenfolge statt der Reihe nach anzeigen.")]
    public bool randomOrder = false;

    EyeCycler[] cyclers;
    int index;
    float timer;

    void Awake()
    {
        cyclers = GetComponentsInChildren<EyeCycler>(true);
    }

    void OnEnable()
    {
        // Zufällige Startphase und zufälliges Startbild, damit nicht alle
        // Augenpaare gleichzeitig (und mit demselben Bild) wechseln.
        timer = Random.Range(0f, Mathf.Max(0.02f, EyesController.Interval));
        int count = PairCount();
        if (count > 1)
            index = Random.Range(0, count);
        ShowCurrent();
    }

    void Update()
    {
        int count = PairCount();
        if (count == 0)
            return;

        float interval = Mathf.Max(0.02f, EyesController.Interval);
        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer -= interval;
            Advance(count);
        }
    }

    // Beide Augen können nur so viele Paare zeigen wie der kleinere Ordner hergibt.
    int PairCount()
    {
        if (cyclers == null || cyclers.Length == 0)
            return 0;

        int count = int.MaxValue;
        foreach (var cycler in cyclers)
            count = Mathf.Min(count, cycler.SpriteCount);
        return count == int.MaxValue ? 0 : count;
    }

    void Advance(int count)
    {
        if (randomOrder && count > 1)
        {
            // Zufälliges Paar wählen, aber nicht zweimal dasselbe hintereinander.
            int next = Random.Range(0, count - 1);
            if (next >= index)
                next++;
            index = next;
        }
        else
        {
            index = (index + 1) % count;
        }

        ShowCurrent();
    }

    void ShowCurrent()
    {
        if (cyclers == null)
            return;

        foreach (var cycler in cyclers)
            cycler.Show(index);
    }

#if UNITY_EDITOR
    // Überträgt die Ordner aus dem Inspector sofort auf die beiden Augen.
    void OnValidate()
    {
        if (gameObject.scene.IsValid())
            ApplyFoldersToChildren();
    }

    [ContextMenu("Ordner auf Augen übertragen")]
    void ApplyFoldersToChildren()
    {
        foreach (var cycler in GetComponentsInChildren<EyeCycler>(true))
        {
            string folder = IsLeft(cycler) ? leftSpriteFolder : rightSpriteFolder;
            if (cycler.spriteFolder == folder)
                continue;

            cycler.spriteFolder = folder;
            cycler.LoadSpritesFromFolder();
            // Damit der übertragene Wert auch mit der Szene gespeichert wird.
            UnityEditor.EditorUtility.SetDirty(cycler);
        }
    }

    // Seite über den GameObject-Namen ("Left Eye"/"Right Eye") bestimmen,
    // notfalls über den bisher eingetragenen Ordnerpfad.
    static bool IsLeft(EyeCycler cycler)
    {
        string goName = cycler.gameObject.name.ToLowerInvariant();
        if (goName.Contains("left"))
            return true;
        if (goName.Contains("right"))
            return false;
        return cycler.spriteFolder != null && cycler.spriteFolder.ToLowerInvariant().Contains("left");
    }
#endif
}

using System.Collections;
using UnityEngine;

/// Überblendet vorab geladene Bilder als Base Albedo auf dem Dome.
/// Erwartet ein Material mit dem Shader "Custom/DomeCrossfade" (_TexA, _TexB, _Blend).
public class DomeImageFader : MonoBehaviour
{
    [SerializeField] private Renderer domeRenderer;
    [SerializeField] private Texture2D[] images;
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
}

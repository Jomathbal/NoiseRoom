using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// Teleportiert den Spieler beim Betreten sanft zurück zum resetPoint:
/// Eine zweite Kamera rendert während der Überblendung live die Sicht vom
/// Zielpunkt, die als Vollbild-Overlay langsam eingeblendet wird. Erst wenn
/// sie voll deckt, wird wirklich teleportiert – es gibt keinen sichtbaren
/// Schnitt, die aktuelle Sicht geht weich in die Zielsicht über.
[RequireComponent(typeof(Collider))]
public class ResetZone : MonoBehaviour
{
    [SerializeField] private Transform resetPoint;
    [SerializeField] private CameraController player;
    [SerializeField] private float blendDuration = 2.5f;

    private bool blending;

    void Awake()
    {
        // OnTriggerEnter feuert nur bei Triggern; ein MeshCollider muss dafür
        // convex sein. Hier erzwingen, damit die Zone auch nach Duplizieren
        // oder Collider-Tausch im Editor funktioniert.
        var col = GetComponent<Collider>();
        if (col is MeshCollider mesh) mesh.convex = true;
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (blending || player == null || resetPoint == null) return;
        if (other.transform != player.transform) return;

        StartCoroutine(BlendTeleport());
    }

    private IEnumerator BlendTeleport()
    {
        blending = true;

        Camera mainCam = player.GetComponent<Camera>();

        // Zweite Kamera rendert die Sicht vom Zielpunkt in eine RenderTexture
        var rt = new RenderTexture(Screen.width, Screen.height, 24);
        var camGo = new GameObject("ResetBlendCamera");
        var blendCam = camGo.AddComponent<Camera>();
        blendCam.CopyFrom(mainCam);
        blendCam.targetTexture = rt;
        camGo.transform.SetPositionAndRotation(resetPoint.position, resetPoint.rotation);

        // Post Processing der Hauptkamera übernehmen, sonst ändert sich der
        // Look sichtbar, sobald nach dem Teleport wieder die Hauptkamera rendert
        blendCam.GetUniversalAdditionalCameraData().renderPostProcessing =
            mainCam.GetUniversalAdditionalCameraData().renderPostProcessing;

        // Vollbild-Overlay, das die Zielsicht über die aktuelle Sicht blendet
        var canvasGo = new GameObject("ResetBlendOverlay");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        var imageGo = new GameObject("BlendImage");
        imageGo.transform.SetParent(canvasGo.transform, false);
        var image = imageGo.AddComponent<RawImage>();
        image.texture = rt;
        image.color = new Color(1f, 1f, 1f, 0f);
        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        for (float t = 0f; t < blendDuration; t += Time.deltaTime)
        {
            image.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, t / blendDuration));
            yield return null;
        }
        image.color = Color.white;

        // Das Overlay deckt jetzt vollständig -> der Sprung ist unsichtbar
        player.TeleportTo(resetPoint.position, resetPoint.rotation);

        // Nach dem Reset dreht sich die Sicht langsam um 180° nach hinten
        player.StartResetSpin();

        // Einen Frame warten, damit die Hauptkamera das neue Bild bereits
        // gerendert hat, bevor das Overlay verschwindet
        yield return null;

        // Destroy() greift erst am Frame-Ende, rt.Release() sofort: Kamera vorher
        // deaktivieren und vom RT trennen, sonst rendert sie noch einen Frame auf
        // ein freigegebenes Target ("Screen position out of view frustum"-Warnung).
        blendCam.enabled = false;
        blendCam.targetTexture = null;

        Destroy(canvasGo);
        Destroy(camGo);
        rt.Release();
        Destroy(rt);

        blending = false;
    }
}

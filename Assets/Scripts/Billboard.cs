using UnityEngine;

// Läuft nach LookAtPlayer (Standard-Order 0), damit die geeaste Drehung des
// Parents die hier gesetzte Welt-Rotation nicht mehr überschreibt.
[DefaultExecutionOrder(100)]
public class Billboard : MonoBehaviour
{
    [Tooltip("Kamera, zu der das Billboard ausgerichtet wird. Leer lassen, um automatisch die Main Camera zu verwenden.")]
    public Camera targetCamera;

    [Tooltip("An der Bildebene der Kamera ausrichten (ruhig, klassisches Billboard). Aus = direkt zur Kameraposition drehen.")]
    public bool alignToCameraPlane = true;

    void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
                return;
        }

        if (alignToCameraPlane)
        {
            // Welt-Rotation = Kamera-Rotation: Sprite steht parallel zur
            // Bildebene, die sichtbare Seite (-Z) zeigt zur Kamera.
            transform.rotation = targetCamera.transform.rotation;
        }
        else
        {
            // Von der Kamera weg schauen, damit die sichtbare Sprite-Seite
            // (-Z) zur Kamera zeigt.
            Vector3 direction = transform.position - targetCamera.transform.position;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}

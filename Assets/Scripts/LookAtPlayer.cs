using UnityEngine;

public class LookAtPlayer : MonoBehaviour
{
    [Tooltip("Ziel, das angeschaut werden soll. Leer lassen, um automatisch die Main Camera zu verwenden.")]
    public Transform target;

    [Tooltip("Maximale Rotationsgeschwindigkeit in Grad pro Sekunde. 0 = sofort hindrehen ohne Glättung.")]
    public float rotationSpeed = 360f;

    [Tooltip("Restwinkel in Grad, unterhalb dessen die Drehung per Cubic Easing abgebremst wird.")]
    public float easeAngle = 60f;

    [Tooltip("Nur um die Y-Achse drehen (Kopf neigt sich nicht nach oben/unten).")]
    public bool onlyYAxis = false;

    void Start()
    {
        if (target == null && Camera.main != null)
        {
            target = Camera.main.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 direction = target.position - transform.position;
        if (onlyYAxis)
        {
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        if (rotationSpeed <= 0f)
        {
            transform.rotation = targetRotation;
            return;
        }

        // Cubic Ease-Out: volle Geschwindigkeit bei großem Restwinkel,
        // weiches Abbremsen unterhalb von easeAngle.
        float remainingAngle = Quaternion.Angle(transform.rotation, targetRotation);
        float t = easeAngle > 0f ? Mathf.Clamp01(remainingAngle / easeAngle) : 1f;
        float eased = 1f - Mathf.Pow(1f - t, 3f);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRotation, rotationSpeed * eased * Time.deltaTime);
    }
}

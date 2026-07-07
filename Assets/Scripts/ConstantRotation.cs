using UnityEngine;

[ExecuteAlways]
public class ConstantRotation : MonoBehaviour
{
    [Tooltip("Rotationsgeschwindigkeit um die Y-Achse in Grad pro Sekunde.")]
    public float rotationSpeed = 90f;

    float lastTime;

    void OnEnable()
    {
        lastTime = Time.realtimeSinceStartup;
    }

    // Update is called once per frame
    void Update()
    {
        float deltaTime;
        if (Application.isPlaying)
        {
            deltaTime = Time.deltaTime;
        }
        else
        {
            // Im Editor ist Time.deltaTime unzuverlässig, daher selbst berechnen.
            float now = Time.realtimeSinceStartup;
            deltaTime = now - lastTime;
            lastTime = now;
        }

        transform.Rotate(0f, rotationSpeed * deltaTime, 0f, Space.Self);
    }
}

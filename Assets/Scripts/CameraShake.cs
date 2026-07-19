using UnityEngine;

// Rotational camera shake driven by Perlin noise. Runs in LateUpdate so the offset
// lands on top of whatever CameraController wrote in Update; the offset is removed
// again at the start of the next LateUpdate (or discarded automatically when mouse
// look overwrites the rotation), so nothing accumulates.
// Positional shake is deliberately left out: the CharacterController on this object
// would bake the offset into its collision-resolved position and the camera would drift.
public class CameraShake : MonoBehaviour
{
    [Header("Distance Source")]
    public Transform shakeSource; // shake strength is driven by the distance to this object
    public float maxDistance = 20f; // at this distance (and beyond) the shake sits at minShake

    [Range(0f, 1f)]
    public float minShake = 0f; // strength far away from the source
    [Range(0f, 1f)]
    public float maxShake = 1f; // strength right at the source

    // X: normalized distance (0 = at the source, 1 = maxDistance), Y: blend between
    // minShake (0) and maxShake (1). Default falls off linearly with distance.
    public AnimationCurve distanceFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Strength at Full Shake")]
    public float maxPitchDegrees = 4f;
    public float maxYawDegrees = 4f;
    public float maxRollDegrees = 3f;
    public float frequency = 11f; // noise speed; higher = more nervous

    private float seed;
    private Quaternion appliedOffset = Quaternion.identity;
    private Quaternion rotationAfterShake;
    private bool offsetApplied;

    void Awake()
    {
        seed = Random.value * 1000f;
    }

    void OnDisable()
    {
        RemoveOffsetIfStillApplied();
    }

    void LateUpdate()
    {
        RemoveOffsetIfStillApplied();

        if (shakeSource == null)
            return;

        float normalizedDistance = Mathf.Clamp01(
            Vector3.Distance(transform.position, shakeSource.position) / maxDistance);
        float amount = Mathf.Lerp(minShake, maxShake, distanceFalloff.Evaluate(normalizedDistance));

        if (amount <= 0f)
            return;

        float t = Time.time * frequency;

        // Perlin instead of per-frame random: consecutive frames stay coherent, which
        // reads as real handheld movement instead of flicker.
        float pitch = maxPitchDegrees * amount * Noise(t, 0f);
        float yaw = maxYawDegrees * amount * Noise(t, 13.7f);
        float roll = maxRollDegrees * amount * Noise(t, 27.1f);

        appliedOffset = Quaternion.Euler(pitch, yaw, roll);
        transform.rotation = transform.rotation * appliedOffset;
        rotationAfterShake = transform.rotation;
        offsetApplied = true;
    }

    // Maps Perlin noise to -1..1; channel separates the three axes on the noise field.
    private float Noise(float t, float channel)
    {
        return Mathf.PerlinNoise(seed + channel, t) * 2f - 1f;
    }

    // Only removes last frame's offset if nobody else has written the rotation since.
    // While mouse look is active, CameraController overwrites the rotation every frame
    // and the offset is already gone; while paused (or under an external driver that
    // stopped writing), the rotation is untouched and has to be reverted here.
    private void RemoveOffsetIfStillApplied()
    {
        if (offsetApplied && transform.rotation == rotationAfterShake)
            transform.rotation = transform.rotation * Quaternion.Inverse(appliedOffset);

        offsetApplied = false;
    }
}

using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class CameraController : MonoBehaviour
{
    [Header("Movement")]
    public float maxSpeed = 9f;
    public float acceleration = 20f;
    public float deceleration = 10f;

    [Header("Vertical Movement")]
    public float maxVerticalSpeed = 9f;
    public float verticalAcceleration = 20f;
    public float verticalDeceleration = 10f;

    [Header("Overall Speed Limit")]
    public float maxOverallSpeed = 12f; // limits the combined total speed (horizontal + vertical), independent of the individual limits

    [Header("Mouse")]
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 90f;

    [Header("Pause")]
    public bool paused = false; // if true, the mouse no longer moves the camera

    private CharacterController controller;
    private float yaw;
    private float pitch;
    private Vector3 currentVelocity;   // horizontal movement (W/A/S/D), relative to look direction
    private float verticalVelocity;    // vertical movement (E/Q), always along world Y, independent of look direction


    // Awake, not Start: an external driver (SliderKeyframes) may disable this component
    // on the first frame, and Unity defers Start() until a component is first enabled.
    // The cursor would then get locked at some arbitrary point mid-timeline.
    void Awake()
    {
        controller = GetComponent<CharacterController>();

        SyncFromTransform();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Re-reads yaw/pitch from the transform. Anything that writes transform.rotation from
    // the outside has to call this, otherwise the next mouse input snaps the camera back
    // to the angles this controller still holds internally.
    public void SyncFromTransform()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = NormalizeAngle(euler.x);
    }

    // Hands control of the transform over to an external driver, or takes it back.
    // The CharacterController goes along: left enabled, it keeps depenetrating the camera
    // out of colliders while something else is writing the position.
    public void SetPlayerControlEnabled(bool value)
    {
        if (value)
            SyncFromTransform();
        else
        {
            // Velocity survives the disable, so without this the camera keeps drifting
            // at its old speed the moment control comes back.
            currentVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }

        enabled = value;

        if (controller != null)
            controller.enabled = value;
    }

    // Position/rotation writes have to go through here: assigning transform.position while
    // the CharacterController is enabled gets undone by its own collision resolution.
    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled)
            controller.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        if (controllerWasEnabled)
            controller.enabled = true;

        SyncFromTransform();
    }

    // eulerAngles reports 0..360; pitch is clamped against a symmetric range around 0.
    private static float NormalizeAngle(float degrees)
    {
        degrees %= 360f;
        return degrees > 180f ? degrees - 360f : degrees;
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
    }

    void HandleMouseLook()
    {
        // In pause mode the mouse should no longer be able to look around with the camera
        if (paused) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void HandleMovement()
    {
        Quaternion lookRotation = Quaternion.Euler(pitch, yaw, 0f);

        // Full look direction including pitch -> you fly in the direction you're looking
        Vector3 forward = lookRotation * Vector3.forward;
        Vector3 right = lookRotation * Vector3.right;

        // Sum up all pressed keys into one input direction -> multiple keys at once are possible (e.g. W+A = forward left)
        Vector3 inputDir = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) inputDir += forward;
        if (Input.GetKey(KeyCode.S)) inputDir -= forward;
        if (Input.GetKey(KeyCode.D)) inputDir += right;
        if (Input.GetKey(KeyCode.A)) inputDir -= right;

        if (inputDir.sqrMagnitude > 0f)
        {
            // Normalize so that diagonal movement (e.g. W+A) doesn't accelerate faster than a single direction
            inputDir.Normalize();

            // Like a particle: acceleration acts on velocity in the direction of movement
            currentVelocity += inputDir * acceleration * Time.deltaTime;

            // Clamp to maxSpeed so it doesn't keep getting infinitely faster
            if (currentVelocity.magnitude > maxSpeed)
            {
                currentVelocity = currentVelocity.normalized * maxSpeed;
            }
        }
        else
        {
            // No key pressed -> velocity decays towards 0 (drag/deceleration), no hard stop
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, deceleration * Time.deltaTime);
        }

        // Vertical movement (E up, Q down) -> always along world Y, independent of look direction/pitch
        float verticalInput = 0f;
        if (Input.GetKey(KeyCode.E)) verticalInput += 1f;
        if (Input.GetKey(KeyCode.Q)) verticalInput -= 1f;

        if (verticalInput != 0f)
        {
            verticalVelocity += verticalInput * verticalAcceleration * Time.deltaTime;
            verticalVelocity = Mathf.Clamp(verticalVelocity, -maxVerticalSpeed, maxVerticalSpeed);
        }
        else
        {
            verticalVelocity = Mathf.MoveTowards(verticalVelocity, 0f, verticalDeceleration * Time.deltaTime);
        }

        Vector3 motion = currentVelocity + Vector3.up * verticalVelocity;

        // Clamp overall speed: horizontal + vertical can add up (e.g. looking steeply down + W + Q),
        // so the combined magnitude can exceed maxSpeed or maxVerticalSpeed individually.
        // This only clamps the actual movement for this frame, not currentVelocity/verticalVelocity themselves,
        // so that acceleration/deceleration of the individual axes keeps working independently of each other.
        if (motion.magnitude > maxOverallSpeed)
        {
            motion = motion.normalized * maxOverallSpeed;
        }

        controller.Move(motion * Time.deltaTime);
    }



}
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class CameraController : MonoBehaviour
{
    [Header("Movement")]
    public float maxSpeed = 9f;
    public float acceleration = 20f;
    public float deceleration = 10f;

    [Header("Vertical")]
    public float verticalDeceleration = 10f; // how fast leftover suction velocity decays outside the suction radius

    [Header("Mouse")]
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 90f;

    [Header("Touch")]
    public float touchLookSensitivity = 0.2f;

    [Header("Suction")]
    public float suctionRadius = 5f;      // horizontal (x/z) distance to the world origin within which the suction pulls
    public float suctionHeight = 20f;     // height at which the suction force reaches its maximum
    public float minSuctionForce = 5f;    // upward acceleration at ground level (y = 0)
    public float maxSuctionForce = 30f;   // upward acceleration at suctionHeight and above
    public float maxSuctionSpeed = 15f;   // upper limit for the velocity the suction can build up
    public float suctionPitchSpeed = 45f; // degrees per second the camera pitches upward at full suction speed
    public float centerPullForce = 10f;   // horizontal acceleration towards the center axis (0, y, 0)
    public float maxCenterPullSpeed = 8f; // upper limit for the horizontal pull velocity
    public float centerPullDamping = 12f; // how fast sideways pull momentum decays (prevents orbiting/overshooting past the axis)

    [Header("Reset Spin")]
    public float resetSpinDegrees = 180f; // yaw rotation played after a reset teleport (turns the view around)
    public float resetSpinDuration = 5f;  // seconds the reset spin takes

    [Header("Debug (read-only)")]
    public float distanceToCenter;        // current horizontal (x/z) distance to the world origin, updated every frame

    private CharacterController controller;
    private float yaw;
    private float pitch;
    private Vector3 currentVelocity;   // horizontal movement (W/A/S/D), relative to look direction (yaw only)
    private float suctionVelocity;     // upward velocity built up by the suction around the world origin
    private Vector3 centerPullVelocity; // horizontal velocity pulling towards the center axis (0, y, 0)

    // Reset spin: eased 180° turn played after a reset. Applied as a per-frame yaw
    // delta so mouse/touch look keeps working on top of it instead of being frozen.
    private float spinElapsed = -1f;   // < 0 = no spin running
    private float spinPrevOffset;      // eased offset already applied, to derive this frame's delta

    // Touch: single-touch scheme. Holding a finger anywhere moves forward (like holding W),
    // and dragging that same finger looks around at the same time. Only the first finger counts.
    private int activeTouchFingerId = -1;


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
            suctionVelocity = 0f;
            centerPullVelocity = Vector3.zero;
            spinElapsed = -1f;
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

        // A teleport is a hard cut: any built-up momentum (including the suction pull)
        // must not carry over, otherwise the player keeps drifting upward and the
        // suction keeps pitching the view after landing at the reset point.
        currentVelocity = Vector3.zero;
        suctionVelocity = 0f;
        centerPullVelocity = Vector3.zero;
        spinElapsed = -1f;
    }

    // Slowly turns the view around by resetSpinDegrees (ease-in-out over
    // resetSpinDuration). Called by ResetZone right after the reset teleport.
    public void StartResetSpin()
    {
        spinElapsed = 0f;
        spinPrevOffset = 0f;
    }

    // eulerAngles reports 0..360; pitch is clamped against a symmetric range around 0.
    private static float NormalizeAngle(float degrees)
    {
        degrees %= 360f;
        return degrees > 180f ? degrees - 360f : degrees;
    }

    void Update()
    {
        // Suction runs before mouse look so its pitch change gets applied (and clamped)
        // together with the mouse input in the same frame.
        HandleTouches();
        HandleSuction();
        HandleResetSpin();
        HandleMouseLook();
        HandleMovement();
    }

    // Claims the first finger that touches down (anywhere on the screen) and
    // releases it again when that touch ends.
    void HandleTouches()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    if (activeTouchFingerId == -1)
                        activeTouchFingerId = touch.fingerId;
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (touch.fingerId == activeTouchFingerId)
                        activeTouchFingerId = -1;
                    break;
            }
        }
    }

    // Feeds the eased turn into yaw as per-frame deltas. Runs before HandleMouseLook
    // so the offset ends up in the same rotation write as the look input.
    void HandleResetSpin()
    {
        if (spinElapsed < 0f) return;

        spinElapsed += Time.deltaTime;
        float t = resetSpinDuration > 0f ? Mathf.Clamp01(spinElapsed / resetSpinDuration) : 1f;
        float offset = Mathf.SmoothStep(0f, resetSpinDegrees, t);

        yaw += offset - spinPrevOffset;
        spinPrevOffset = offset;

        if (t >= 1f)
            spinElapsed = -1f;
    }

    void HandleMouseLook()
    {
        // Skip the mouse axes while touching: Unity simulates mouse input from touches
        // by default, which would apply the drag a second time on top of the touch look.
        if (Input.touchCount == 0)
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        }

        // Dragging the active finger looks around, same as the mouse
        if (activeTouchFingerId != -1)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.fingerId != activeTouchFingerId) continue;

                yaw += touch.deltaPosition.x * touchLookSensitivity;
                pitch -= touch.deltaPosition.y * touchLookSensitivity;
                break;
            }
        }

        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void HandleMovement()
    {
        // Yaw only, no pitch: user movement stays on the x/z plane no matter where
        // the camera looks. The suction is the only thing that moves the player vertically.
        Quaternion lookRotation = Quaternion.Euler(0f, yaw, 0f);

        Vector3 forward = lookRotation * Vector3.forward;
        Vector3 right = lookRotation * Vector3.right;

        // Sum up all pressed keys into one input direction -> multiple keys at once are possible (e.g. W+A = forward left)
        Vector3 inputDir = Vector3.zero;

        // Any held touch moves forward, like holding W (looking around while touching keeps moving)
        if (Input.GetKey(KeyCode.W) || activeTouchFingerId != -1) inputDir += forward;
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

        // Suction is an external force on top of the user's horizontal movement —
        // the only source of vertical motion.
        Vector3 motion = currentVelocity + Vector3.up * suctionVelocity + centerPullVelocity;

        controller.Move(motion * Time.deltaTime);
    }

    // Inside suctionRadius (horizontal x/z distance to the world origin) an upward force
    // accelerates the player. The force grows with height and peaks at suctionHeight.
    void HandleSuction()
    {
        Vector3 pos = transform.position;
        distanceToCenter = new Vector2(pos.x, pos.z).magnitude;

        if (distanceToCenter < suctionRadius)
        {
            float t = Mathf.Clamp01(pos.y / suctionHeight);
            float force = Mathf.Lerp(minSuctionForce, maxSuctionForce, t);

            suctionVelocity += force * Time.deltaTime;
            suctionVelocity = Mathf.Min(suctionVelocity, maxSuctionSpeed);

            // Horizontal pull towards the center axis (0, y, 0)
            if (distanceToCenter > 0.001f)
            {
                Vector3 dirToCenter = new Vector3(-pos.x, 0f, -pos.z) / distanceToCenter;

                centerPullVelocity += dirToCenter * centerPullForce * Time.deltaTime;
                centerPullVelocity = Vector3.ClampMagnitude(centerPullVelocity, maxCenterPullSpeed);

                // The pull is a central force with momentum: the acceleration always points at the
                // current center direction, but the accumulated velocity keeps its old direction.
                // Without damping that sideways leftover makes the player orbit / shoot past the
                // axis instead of settling on it. Split off everything that no longer points at
                // the axis (including any away-pointing part) and damp it away.
                float radialSpeed = Mathf.Max(Vector3.Dot(centerPullVelocity, dirToCenter), 0f);
                Vector3 radialVelocity = dirToCenter * radialSpeed;
                Vector3 staleVelocity = centerPullVelocity - radialVelocity;
                staleVelocity = Vector3.MoveTowards(staleVelocity, Vector3.zero, centerPullDamping * Time.deltaTime);

                // Never faster towards the axis than the remaining distance allows, otherwise
                // the pull overshoots past the center within a single frame.
                radialVelocity = Vector3.ClampMagnitude(radialVelocity, distanceToCenter / Time.deltaTime);

                centerPullVelocity = radialVelocity + staleVelocity;
            }
            else
            {
                // Practically on the axis: any leftover pull momentum would only carry past it.
                centerPullVelocity = Vector3.zero;
            }
        }
        else
        {
            // Outside the radius the built-up suction velocity decays instead of stopping abruptly
            suctionVelocity = Mathf.MoveTowards(suctionVelocity, 0f, verticalDeceleration * Time.deltaTime);
            centerPullVelocity = Vector3.MoveTowards(centerPullVelocity, Vector3.zero, deceleration * Time.deltaTime);
        }

        // The suction also drags the view upward: the camera pitches towards looking up,
        // scaled by how strong the current pull is (same velocity that drives the position).
        if (suctionVelocity > 0f && maxSuctionSpeed > 0f)
        {
            float pull = suctionVelocity / maxSuctionSpeed;
            pitch = Mathf.MoveTowards(pitch, -maxLookAngle, suctionPitchSpeed * pull * Time.deltaTime);
        }
    }



}
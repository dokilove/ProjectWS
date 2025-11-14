using UnityEngine;
using UnityEngine.InputSystem;

public class ManualFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Orbit Settings")]
    [SerializeField] private float distance = 14f;
    [SerializeField] private float yawSpeed = 120f;
    [SerializeField] private float pitchSpeed = 120f;
    [SerializeField] private Vector2 pitchLimits = new Vector2(-30f, 80f);
    [SerializeField] private Vector2 yawLimits = new Vector2(-90f, 90f); // Yaw (horizontal) rotation limits

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothSpeed = 0.125f;

    private InputSystem_Actions inputActions;
    private Vector2 cameraInput;

    // Current camera angles
    private float yaw = 0f;
    private float pitch = 0f;

    // Stored initial camera angles for reset
    private float initialYaw = 0f;
    private float initialPitch = 0f;

    void Awake()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Camera.CameraMove.performed += OnCameraMove;
        inputActions.Camera.CameraMove.canceled += OnCameraMove;
        inputActions.Camera.CameraReset.performed += OnCameraReset;
    }

    private void OnCameraMove(InputAction.CallbackContext context)
    {
        cameraInput = context.ReadValue<Vector2>();
    }

    private void OnCameraReset(InputAction.CallbackContext context)
    {
        // Instantly snap to the initial state
        yaw = initialYaw;
        pitch = initialPitch;
        cameraInput = Vector2.zero;

        // Calculate final rotation and position using sequential rotations
        Quaternion yawRotation = Quaternion.AngleAxis(yaw, Vector3.up);
        Quaternion pitchRotation = Quaternion.AngleAxis(pitch, Vector3.right);
        Quaternion finalRotation = yawRotation * pitchRotation;
        Vector3 finalPosition = target.position - (finalRotation * Vector3.forward * distance);

        // Apply instantly, bypassing any smoothing
        transform.position = finalPosition;
        transform.rotation = finalRotation;
    }

    void OnEnable()
    {
        inputActions.Camera.Enable();
    }

    void OnDisable()
    {
        inputActions.Camera.Disable();
    }

    void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                Debug.LogWarning("ManualFollowCamera: No target assigned and no GameObject with 'Player' tag found.");
                return;
            }
        }

        Vector3 angles = transform.rotation.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y); // Clamp initial pitch

        initialYaw = yaw;
        initialPitch = pitch;
        initialPitch = Mathf.Clamp(initialPitch, pitchLimits.x, pitchLimits.y); // Clamp initial pitch for reset
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Manual rotation is now the only logic here
        HandleManualRotation();

        Quaternion yawRotation = Quaternion.AngleAxis(yaw, Vector3.up);
        Quaternion pitchRotation = Quaternion.AngleAxis(pitch, Vector3.right); // Rotate around local right axis
        Quaternion desiredRotation = yawRotation * pitchRotation; // Apply yaw then pitch
        Vector3 desiredPosition = target.position - (desiredRotation * Vector3.forward * distance);

        // If there is active camera rotation input, snap the position for responsiveness.
        // Otherwise, smooth the position to follow the target.
        if (cameraInput.sqrMagnitude > 0.01f)
        {
            transform.position = desiredPosition; // Snap instantly
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionSmoothSpeed); // Smooth follow
        }

        transform.rotation = desiredRotation;
    }

    private void HandleManualRotation()
    {
        yaw += cameraInput.x * yawSpeed * Time.deltaTime;
        pitch -= cameraInput.y * pitchSpeed * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}

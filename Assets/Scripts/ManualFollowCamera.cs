using UnityEngine;

public class ManualFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Position Settings")]
    [Tooltip("The desired offset from the target. This is now set in the Inspector.")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 10, -10);
    [SerializeField] private float positionSmoothTime = 0.2f;

    private Vector3 velocity = Vector3.zero;

    void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("ManualFollowCamera: Target is not assigned at start. It will be assigned dynamically.");
            // Disable the update loop until a target is set.
            enabled = false; 
        }
        else
        {
            SnapToTarget();
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // The target position is the target's current position plus the defined offset
        Vector3 targetPosition = target.position + cameraOffset;

        // Smoothly move the camera towards the target position
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, positionSmoothTime);
    }

    /// <summary>
    /// Instantly moves the camera to the target's position + offset, bypassing smoothing.
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null) return;

        transform.position = target.position + cameraOffset;
        // Reset velocity to prevent a sudden jump in the next SmoothDamp call
        velocity = Vector3.zero; 
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            // If the camera was disabled, re-enable its update loop.
            if (!enabled)
            {
                enabled = true;
            }
        }
        else
        {
            // If target is set to null, disable the update loop.
            enabled = false;
        }
    }
}


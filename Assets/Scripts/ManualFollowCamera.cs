using UnityEngine;

public class ManualFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Position Settings")]
    [SerializeField] private float positionSmoothTime = 0.2f;

    // This will store the initial offset from the target
    private Vector3 cameraOffset;
    private Vector3 velocity = Vector3.zero;

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
                enabled = false;
                return;
            }
        }

        // Calculate and store the initial offset from the target
        cameraOffset = transform.position - target.position;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // The target position is the target's current position plus the initial offset
        Vector3 targetPosition = target.position + cameraOffset;

        // Smoothly move the camera towards the target position without changing rotation
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, positionSmoothTime);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            // Recalculate offset if the target changes
            cameraOffset = transform.position - target.position;
            if (!enabled)
            {
                enabled = true;
            }
        }
    }
}


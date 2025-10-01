using UnityEngine;
using System.Collections; // Required for Coroutines

public class ManualFollowCamera : MonoBehaviour
{
    [SerializeField] private Transform target; // 플레이어 Transform
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -10f); // 플레이어로부터의 상대적 위치
    [SerializeField] private float smoothSpeed = 0.125f; // 카메라 추적 부드러움
    [SerializeField] private float evadeDampingDuration = 0.5f; // 회피 댐핑 지속 시간
    [SerializeField] private float evadeDampingFactor = 0.5f; // 회피 시 카메라 추적 부드러움 (느리게 반응)

    private float currentDampingFactor = 1f; // 현재 적용되는 댐핑 값 (1f = normal, evadeDampingFactor = evade)
    private float evadeDampingTimer = 0f; // 회피 댐핑 타이머

    void Start()
    {
        // Ensure target is set, if not, try to find the player
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player"); // Assuming player has "Player" tag
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                Debug.LogWarning("ManualFollowCamera: No target assigned and no GameObject with 'Player' tag found.");
            }
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * currentDampingFactor);
        transform.position = smoothedPosition;

        // Update damping factor
        if (evadeDampingTimer > 0)
        {
            evadeDampingTimer -= Time.deltaTime;
            currentDampingFactor = Mathf.Lerp(evadeDampingFactor, 1f, (evadeDampingDuration - evadeDampingTimer) / evadeDampingDuration);
        }
        else
        {
            currentDampingFactor = 1f;
        }
    }

    public void TriggerEvadeDamping()
    {
        evadeDampingTimer = evadeDampingDuration;
        currentDampingFactor = evadeDampingFactor;
    }

    // New method to set the camera target
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
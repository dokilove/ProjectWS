using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using ProjectWS.Utility; // [NEW] BulletTimeManager 사용을 위해 추가

// This component requires other components to function correctly.
// Unit.cs: To get references to other components like UnitAnimator and UnitVisuals.
// Rigidbody: For physics-based movement.
[RequireComponent(typeof(Rigidbody))]
public class UnitMove : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f; // Base move speed from Inspector
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private float rangedMoveSpeedPenalty = 0.5f; // e.g., 0.5 means 50% speed

    [Header("Evade")]
    [SerializeField] private EvadeData evadeData;
    [SerializeField] private GameObject justDodgeTriggerObject; // 저스트 회피 트리거 오브젝트

    // --- Dependencies ---
    private Unit _unit;

    // --- State ---
    private Rigidbody rb;
    private int originalLayer;
    private int currentEvadeCharges;
    private float lastEvadeTime;
    private bool isEvading = false;
    private bool isStrafeMovementEnabled = false; // New state for strafe movement
    private float baseMoveSpeed; // Stores the initial moveSpeed from Inspector
    private float currentEffectiveMoveSpeed; // The speed currently used for movement
    private bool isAutoAiming = false; // New flag to indicate if auto-aim is active

    // --- Public Properties ---
    public EvadeData EvadeData => evadeData;
    public int CurrentEvadeCharges => currentEvadeCharges;
    public float LastEvadeTime => lastEvadeTime;
    public bool IsAutoAiming => isAutoAiming; // Public getter for the flag

    public void Init(Unit unit)
    {
        _unit = unit;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        originalLayer = gameObject.layer;

        if (evadeData != null)
        {
            currentEvadeCharges = evadeData.maxEvadeCharges;
        }
        lastEvadeTime = -evadeData.evadeChargeRegenTime; // Start with full charges

        baseMoveSpeed = moveSpeed; // Store the initial Inspector value
        currentEffectiveMoveSpeed = baseMoveSpeed; // Start with base speed

        // 게임 시작 시 저스트 회피 트리거를 비활성화
        if (justDodgeTriggerObject != null)
        {
            justDodgeTriggerObject.SetActive(false);
        }
    }

    private void Update()
    {
        HandleEvadeChargeRegen();
    }

    // --- Attack Mode Callbacks ---
    public void OnEnterRangedMode()
    {
        // Apply movement speed penalty
        currentEffectiveMoveSpeed = baseMoveSpeed * rangedMoveSpeedPenalty;
        isStrafeMovementEnabled = true;
    }

    public void OnEnterMeleeMode()
    {
        // Normalize movement speed
        currentEffectiveMoveSpeed = baseMoveSpeed; // Revert to base speed
        isStrafeMovementEnabled = false;
    }

    /// <summary>
    /// Rotates the unit to face a specific target position.
    /// </summary>
    /// <param name="targetPosition">The world position to face.</param>
    public void RotateTowards(Vector3 targetPosition)
    {
        isAutoAiming = true; // Set flag when auto-aiming
        Vector3 directionToTarget = (targetPosition - transform.position).normalized;
        directionToTarget.y = 0; // Keep rotation on XZ plane

        if (directionToTarget.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            rb.rotation = targetRotation; // Instantaneous rotation
        }
    }

    /// <summary>
    /// Resets the auto-aiming flag.
    /// </summary>
    public void ResetAutoAim()
    {
        isAutoAiming = false;
    }

    /// <summary>
    /// Sets the auto-aiming flag.
    /// </summary>
    public void SetIsAutoAiming(bool value)
    {
        isAutoAiming = value;
    }

    /// <summary>
    /// Handles the physics-based movement and rotation of the unit.
    /// This is intended to be called from FixedUpdate.
    /// </summary>
    /// <param name="moveInput">The raw move input vector (e.g., from a gamepad stick).</param>
    /// <param name="aimDirection">The current world-space direction the unit is aiming.</param>
    public void HandleMovement(Vector2 moveInput, Vector3 aimDirection)
    {
        if (isEvading) return;

        rb.angularVelocity = Vector3.zero;

        // --- Velocity Calculation ---
        Vector3 moveVector;
        // Always use camera-relative movement
        Vector3 forward = Camera.main.transform.forward;
        Vector3 right = Camera.main.transform.right;
        forward.y = 0; // Keep movement on XZ plane
        right.y = 0; // Keep movement on XZ plane
        forward.Normalize();
        right.Normalize();

        moveVector = (forward * moveInput.y + right * moveInput.x);
        
        // [MODIFIED] 불릿 타임 중 플레이어 이동 속도 보정
        float currentSpeed = currentEffectiveMoveSpeed;
        if (BulletTimeManager.Instance != null && BulletTimeManager.Instance.IsBulletTimeActive)
        {
            // Time.timeScale이 0에 가까울 경우를 대비한 예외 처리
            if (Time.timeScale > 0.01f)
            {
                currentSpeed *= (1f / Time.timeScale); // 역산 보정
            }
        }
        rb.linearVelocity = moveVector.normalized * currentSpeed; // 보정된 속도 적용

        // --- Body Rotation (Aiming) ---
        if (aimDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(aimDirection);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// Applies a forward dash force for melee attacks.
    /// </summary>
    /// <param name="direction">The direction of the dash.</param>
    /// <param name="force">The impulse force of the dash.</param>
    public void ApplyMeleeDash(Vector3 direction, float force)
    {
        if (isEvading || force <= 0) return;

        rb.AddForce(direction * force, ForceMode.Impulse);
    }

    /// <summary>
    /// Initiates an evade action.
    /// </summary>
    /// <param name="moveInput">The current move input to determine evade direction.</param>
    public void PerformEvade(Vector2 moveInput)
    {
        if (evadeData == null || isEvading || currentEvadeCharges <= 0) return;

        // [NEW] 회피 시작을 Unit 코디네이터에게 알림
        _unit.OnEvadeStart();

        isEvading = true;
        currentEvadeCharges--;
        lastEvadeTime = Time.time;

        _unit.IsInvincible = true; // Set invincibility at the start of evade
        if (justDodgeTriggerObject != null) justDodgeTriggerObject.SetActive(true);


        // --- Animation ---
        _unit.UnitAnimator.TriggerEvade();

        // --- Direction ---
        Vector3 evadeDirection;
        if (moveInput.sqrMagnitude > 0.1f)
        {
            Transform camTransform = Camera.main.transform;
            Vector3 camForward = camTransform.forward;
            Vector3 camRight = camTransform.right;
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();
            evadeDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;
        }
        else
        {
            evadeDirection = transform.forward;
        }

        // --- Physics ---
        rb.AddForce(evadeDirection * evadeData.evadeForce, ForceMode.Impulse);
        gameObject.layer = evadeData.dodgingPlayerLayer;
        gameObject.layer = evadeData.dodgingPlayerLayer;

        // --- Visuals ---
        _unit.UnitVisuals.SetEvadeTrail(true);
        
        // --- Reset ---
        Invoke(nameof(ForceIdleAnimation), evadeData.dodgeDuration / 2f);
        Invoke(nameof(ResetPlayerState), evadeData.dodgeDuration);

        // --- Enemy Push ---
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, evadeData.pushRadius);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Enemy"))
            {
                if (hitCollider.TryGetComponent<NavMeshAgent>(out var enemyAgent))
                {
                    Vector3 pushDir = (hitCollider.transform.position - transform.position).normalized;
                    pushDir.y = 0;
                    StartCoroutine(SmoothPushEnemy(enemyAgent, pushDir.normalized * evadeData.pushForce, evadeData.pushSmoothTime));
                }
            }
        }
    }

    private void HandleEvadeChargeRegen()
    {
        if (evadeData == null || isEvading) return;
        if (currentEvadeCharges < evadeData.maxEvadeCharges)
        {
            float timeSinceLastEvade = Time.time - lastEvadeTime;
            int chargesToRegen = Mathf.FloorToInt(timeSinceLastEvade / evadeData.evadeChargeRegenTime);

            if (chargesToRegen > 0)
            {
                currentEvadeCharges = Mathf.Min(evadeData.maxEvadeCharges, currentEvadeCharges + chargesToRegen);
                lastEvadeTime += chargesToRegen * evadeData.evadeChargeRegenTime;
            }
        }
    }

    private void ResetPlayerState()
    {
        isEvading = false;
        gameObject.layer = originalLayer; // Reverted: Set layer back to original
        _unit.UnitVisuals.SetEvadeTrail(false);
        _unit.IsInvincible = false; // Reset invincibility at the end of evade
        if (justDodgeTriggerObject != null) justDodgeTriggerObject.SetActive(false);
    }

    private void ForceIdleAnimation()
    {
        _unit.UnitAnimator.ForceIdle();
    }

    private IEnumerator SmoothPushEnemy(NavMeshAgent agentToPush, Vector3 pushVector, float duration)
    {
        if (agentToPush == null || !agentToPush.isActiveAndEnabled || !agentToPush.isOnNavMesh)
        {
            yield break;
        }

        Vector3 startPosition = agentToPush.transform.position;
        Vector3 targetPosition = startPosition + pushVector;
        float elapsedTime = 0f;
        agentToPush.isStopped = true;

        while (elapsedTime < duration)
        {
            if (!agentToPush.isActiveAndEnabled) yield break;
            Vector3 currentTargetPosition = Vector3.Lerp(startPosition, targetPosition, elapsedTime / duration);
            agentToPush.Move(currentTargetPosition - agentToPush.transform.position);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (agentToPush.isActiveAndEnabled)
        {
            agentToPush.isStopped = false;
        }
    }
}

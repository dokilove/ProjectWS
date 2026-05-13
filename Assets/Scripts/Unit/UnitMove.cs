using UnityEngine;
using UnityEngine.AI;
using System.Collections;

// This component requires other components to function correctly.
// Unit.cs: To get references to other components like UnitAnimator and UnitVisuals.
// Rigidbody: For physics-based movement.
[RequireComponent(typeof(Rigidbody))]
public class UnitMove : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("Evade")]
    [SerializeField] private EvadeData evadeData;

    // --- Dependencies ---
    private Unit _unit;

    [Header("Component References")]

    // --- State ---
    private Rigidbody rb;
    private int originalLayer;
    private int currentEvadeCharges;
    private float lastEvadeTime;
    private bool isEvading = false;

    // --- Public Properties ---
    public EvadeData EvadeData => evadeData;
    public int CurrentEvadeCharges => currentEvadeCharges;
    public float LastEvadeTime => lastEvadeTime;

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
    }

    private void Update()
    {
        HandleEvadeChargeRegen();
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
        Vector3 cameraRight = Camera.main.transform.right;
        Vector3 cameraRightFlat = new Vector3(cameraRight.x, 0, cameraRight.z).normalized;
        Vector3 cameraForwardFlat = Vector3.Cross(Vector3.up, cameraRightFlat);
        Vector3 moveForward = -cameraForwardFlat;
        Vector3 moveRight = cameraRightFlat;

        Vector3 moveVector = (moveForward * moveInput.y + moveRight * moveInput.x);
        rb.linearVelocity = moveVector * moveSpeed;

        // --- Body Rotation (Aiming) ---
        if (aimDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(aimDirection);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// Initiates an evade action.
    /// </summary>
    /// <param name="moveInput">The current move input to determine evade direction.</param>
    public void PerformEvade(Vector2 moveInput)
    {
        if (evadeData == null || isEvading || currentEvadeCharges <= 0) return;

        isEvading = true;
        currentEvadeCharges--;
        lastEvadeTime = Time.time;

        _unit.IsInvincible = true; // Set invincibility at the start of evade

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
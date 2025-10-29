using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody))]
public class UnitController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("Evade")]
    [SerializeField] private float evadeForce = 10f;
    [SerializeField] private float dodgeDuration = 0.5f;
    [SerializeField] private int dodgingPlayerLayer;
    [SerializeField] private float pushRadius = 2f;
    [SerializeField] private float pushForce = 0.05f;
    [SerializeField] private float pushSmoothTime = 0.2f;

    [Header("Attacks - Auto Fire & Targeting")]
    [SerializeField] private Transform turretTransform;
    [SerializeField] private float turretRotationSpeed = 10f;
    [SerializeField, Tooltip("How much closer a new target must be to switch automatically.")] private float targetStickiness = 2f;
    [SerializeField] private Transform firePoint;
    
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private Color defaultTargetColor = Color.red;
    [SerializeField] private Color lockOnTargetColor = Color.yellow;

    [Header("Visuals")]
    [SerializeField] private LineRenderer radiusVisualizer;
    
    [SerializeField] private LineRenderer targetLineRenderer; // For the line to the target
    [SerializeField] private FieldOfViewMesh attackRangeVisualizer;

    private float nextFireTime = 0f;
    private List<GameObject> projectilePool = new List<GameObject>();
    private int poolSize = 20;

    private Rigidbody rb;
    private InputSystem_Actions playerActions;
    private Vector2 moveInput;
    private int originalLayer;

    // --- Targeting Fields ---
    private Transform currentTarget;
    private List<Transform> potentialTargets = new List<Transform>();
    private bool isTargetLockOn = false; // New state for manual lock-on
    private float lastSwitchValue = 0f; // To detect button press from axis value

    private bool isFireHeld = false;

    public bool IsControlledByPlayer { get; private set; } = false;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) // Check if the object has not been destroyed
            {
                UpdateRadiusVisualizer();
                if (attackRangeVisualizer != null)
                {
                    attackRangeVisualizer.GenerateMesh(weaponData.attackAngle, weaponData.lockOnRadius);
                }
            }
        };
    }
#endif

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerActions = new InputSystem_Actions();
        originalLayer = gameObject.layer;

        // Initialize LineRenderers if they exist
        if (radiusVisualizer != null)
        {
            radiusVisualizer.startWidth = 0.1f;
            radiusVisualizer.endWidth = 0.1f;
            radiusVisualizer.enabled = true;
        }
        
        if (targetLineRenderer != null)
        {
            targetLineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
            targetLineRenderer.startWidth = 0.05f;
            targetLineRenderer.endWidth = 0.05f;
            targetLineRenderer.positionCount = 2;
            targetLineRenderer.enabled = false;
        }

        
    }

    private void Start()
    {
        if (weaponData == null || weaponData.projectileData == null || weaponData.projectileData.projectilePrefab == null)
        {
            Debug.LogError("WeaponData, ProjectileData, or ProjectilePrefab is not assigned in the inspector!", this);
            return;
        }

        for (int i = 0; i < poolSize; i++)
        {
            GameObject proj = Instantiate(weaponData.projectileData.projectilePrefab);
            proj.SetActive(false);
            projectilePool.Add(proj);
        }

        UpdateRadiusVisualizer();
        if (attackRangeVisualizer != null)
        {
            attackRangeVisualizer.GenerateMesh(weaponData.attackAngle, weaponData.lockOnRadius);
        }
    }

    public void EnableControl()
    {
        IsControlledByPlayer = true;
        this.enabled = true;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        gameObject.SetActive(true);
        playerActions.Player.Enable();
        playerActions.Player.Evade.performed += OnEvade;
        playerActions.Player.Interact.performed += OnInteract;

        playerActions.Player.Fire.performed += OnFire;
        playerActions.Player.Fire_Hold.started += OnFireHoldStarted;
        playerActions.Player.Fire_Hold.canceled += OnFireHoldCanceled;
    }

    public void DisableControl()
    {
        IsControlledByPlayer = false;
        this.enabled = false;
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeAll;
        gameObject.SetActive(false);

        if (playerActions != null)
        {
            playerActions.Player.Disable();
            playerActions.Player.Evade.performed -= OnEvade;
            playerActions.Player.Interact.performed -= OnInteract;

            playerActions.Player.Fire.performed -= OnFire;
            playerActions.Player.Fire_Hold.started -= OnFireHoldStarted;
            playerActions.Player.Fire_Hold.canceled -= OnFireHoldCanceled;
        }
        rb.linearVelocity = Vector3.zero;
        moveInput = Vector2.zero;
    }

    private void Update()
    {
        if (!IsControlledByPlayer) return;

        moveInput = playerActions.Player.Move.ReadValue<Vector2>();

        UpdateAndSelectTarget();
        HandleTargetSwitching();

        RotateTurret(currentTarget);

        

        // Update filled cone visualizer
        if (attackRangeVisualizer != null)
        {
            bool shouldShow = currentTarget != null;
            attackRangeVisualizer.SetActive(shouldShow);

            if (shouldShow)
            {
                attackRangeVisualizer.transform.position = firePoint.position;
                attackRangeVisualizer.transform.rotation = turretTransform.rotation;
            }
        }

        // Update target line visualizer
        if (targetLineRenderer != null)
        {
            if (currentTarget != null)
            {
                targetLineRenderer.enabled = true;
                Color lineColor = isTargetLockOn ? lockOnTargetColor : defaultTargetColor;
                targetLineRenderer.startColor = lineColor;
                targetLineRenderer.endColor = lineColor;
                targetLineRenderer.SetPosition(0, firePoint.position);
                targetLineRenderer.SetPosition(1, currentTarget.position);
            }
            else
            {
                targetLineRenderer.enabled = false;
            }
        }

        if (isFireHeld && CanFire())
        {
            nextFireTime = Time.time + 1f / weaponData.fireRate;
            Fire();
        }
    }

    private bool CanFire()
    {
        if (currentTarget == null || Time.time < nextFireTime || weaponData == null)
        {
            return false;
        }

        // Check distance
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
        if (distanceToTarget > weaponData.lockOnRadius)
        {
            return false;
        }

        // Check angle
        Vector3 directionToTarget = (currentTarget.position - turretTransform.position).normalized;
        float angleToTarget = Vector3.Angle(turretTransform.forward, directionToTarget);

        if (angleToTarget > weaponData.attackAngle / 2)
        {
            return false;
        }

        return true;
    }

    private void HandleTargetSwitching()
    {
        float switchValue = playerActions.Player.SwitchTarget.ReadValue<float>();

        // Detect a "press" by checking for a transition from near-zero to a significant value
        if (Mathf.Abs(lastSwitchValue) < 0.1f && Mathf.Abs(switchValue) >= 0.1f)
        {
            if (potentialTargets.Count > 0) // Make sure there's at least one target
            {
                if (!isTargetLockOn)
                {
                    // FIRST press: Just enable lock-on.
                    // The currentTarget is already the closest one thanks to UpdateAndSelectTarget().
                    isTargetLockOn = true;
                }
                else
                {
                    // SUBSEQUENT presses: Switch the target.
                    if (potentialTargets.Count > 1)
                    {
                        int direction = switchValue > 0 ? 1 : -1;
                        SwitchTarget(direction);
                    }
                }
            }
        }

        lastSwitchValue = switchValue;
    }

    private void SwitchTarget(int direction) // 1 for clockwise, -1 for counter-clockwise
    {
        if (potentialTargets.Count <= 1 || currentTarget == null) return;

        Vector3 referenceVec = currentTarget.position - transform.position;
        referenceVec.y = 0;

        var candidatesInDirection = new List<Transform>();

        // Find all candidates in the desired angular direction
        foreach (var candidate in potentialTargets)
        {
            if (candidate == currentTarget) continue;

            Vector3 candidateVec = candidate.position - transform.position;
            candidateVec.y = 0;

            float angle = Vector3.SignedAngle(referenceVec, candidateVec, Vector3.up);

            if (Mathf.Sign(angle) == direction)
            {
                candidatesInDirection.Add(candidate);
            }
        }

        // If there are any candidates, find the one closest by distance
        if (candidatesInDirection.Count > 0)
        {
            Transform bestCandidate = null;
            float minDistance = float.MaxValue;
            foreach (var candidate in candidatesInDirection)
            {
                float distance = Vector3.Distance(transform.position, candidate.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestCandidate = candidate;
                }
            }
            currentTarget = bestCandidate;
        }
        // If no candidates are in the desired direction, do nothing.
    }

    private void UpdateAndSelectTarget()
    {
        // Step 1: Find all potential targets within lockOnRadius
        List<Transform> allTargetsInRadius = Physics.OverlapSphere(transform.position, weaponData.lockOnRadius, enemyLayer)
                                .Select(col => col.transform)
                                .ToList();

        // Step 2: Filter targets by attackAngle
        potentialTargets.Clear();
        foreach (Transform target in allTargetsInRadius)
        {
            Vector3 directionToTarget = (target.position - turretTransform.position).normalized;
            float angleToTarget = Vector3.Angle(turretTransform.forward, directionToTarget);

            if (angleToTarget <= weaponData.attackAngle / 2)
            {
                potentialTargets.Add(target);
            }
        }

        if (potentialTargets.Count == 0)
        {
            currentTarget = null;
            isTargetLockOn = false;
            return;
        }

        // Step 2: Handle targeting mode
        if (isTargetLockOn)
        {
            // Check if the locked target is still valid
            if (!potentialTargets.Contains(currentTarget))
            {
                // Locked target is gone, revert to auto mode
                isTargetLockOn = false;
                // Fall-through to auto mode logic below
            }
            else
            {
                // Target is still valid, do nothing and keep it.
                return;
            }
        }

        // Automatic mode (or if lock-on was just lost)
        // Find the closest one.
        Transform closestTarget = null;
        float minDistance = float.MaxValue;
        foreach (var target in potentialTargets)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestTarget = target;
            }
        }

        // If we have a current target, and it's still valid...
        if (currentTarget != null && potentialTargets.Contains(currentTarget))
        {
            float currentTargetDistance = Vector3.Distance(transform.position, currentTarget.position);
            // Switch only if the new closest target is significantly closer (by 'targetStickiness' amount).
            if (minDistance < currentTargetDistance - targetStickiness)
            {
                currentTarget = closestTarget;
            }
            // Otherwise, do nothing and stick with the current target.
        }
        else
        {
            // If we don't have a target or the old one is invalid, just switch to the closest.
            currentTarget = closestTarget;
        }
    }

    

    private void FixedUpdate()
    {
        if (!IsControlledByPlayer) return;

        rb.angularVelocity = Vector3.zero;

        Vector3 moveForward;
        Vector3 moveRight;

        if (Mathf.Abs(Vector3.Dot(Camera.main.transform.forward, Vector3.up)) > 0.99f)
        {
            moveForward = Vector3.forward;
            moveRight = Vector3.right;
        }
        else
        {
            moveForward = Camera.main.transform.forward;
            moveRight = Camera.main.transform.right;
            moveForward.y = 0;
            moveRight.y = 0;
            moveForward.Normalize();
            moveRight.Normalize();
        }

        Vector3 moveVector = (moveForward * moveInput.y + moveRight * moveInput.x);
        rb.linearVelocity = moveVector * moveSpeed;

        if (moveVector != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveVector);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private void OnFire(InputAction.CallbackContext context)
    {
        if (CanFire())
        {
            nextFireTime = Time.time + 1f / weaponData.fireRate;
            Fire();
        }
    }

    private void OnFireHoldStarted(InputAction.CallbackContext context)
    {
        isFireHeld = true;
    }

    private void OnFireHoldCanceled(InputAction.CallbackContext context)
    {
        isFireHeld = false;
    }

    private void RotateTurret(Transform target)
    {
        if (turretTransform == null || weaponData == null) return;

        Quaternion desiredRotation;
        if (target != null)
        {
            Vector3 direction = target.position - turretTransform.position;
            direction.y = 0;
            desiredRotation = Quaternion.LookRotation(direction);
        }
        else
        {
            desiredRotation = transform.rotation; // Align with player's forward if no target
        }

        // Calculate the angle difference from the player's forward direction
        Quaternion playerForwardRotation = transform.rotation;
        Quaternion rotationDelta = Quaternion.Inverse(playerForwardRotation) * desiredRotation;
        float angle = 0;
        Vector3 axis = Vector3.zero;
        rotationDelta.ToAngleAxis(out angle, out axis);

        // Ensure the angle is within -180 to 180 range for clamping
        if (angle > 180f) angle -= 360f;
        if (angle < -180f) angle += 360f;

        // Clamp the angle
        float clampedAngle = Mathf.Clamp(angle, -weaponData.attackAngle / 2, weaponData.attackAngle / 2);

        // Reconstruct the clamped rotation
        Quaternion clampedRotation = playerForwardRotation * Quaternion.AngleAxis(clampedAngle, Vector3.up);

        // Smoothly rotate the turret
        turretTransform.rotation = Quaternion.Slerp(turretTransform.rotation, clampedRotation, turretRotationSpeed * Time.deltaTime);
    }

    private void Fire()
    {
        if (currentTarget == null) return;

        Debug.Log("Unit Fire action triggered!");
        GameObject projectile = GetPooledProjectile();
        if (projectile != null)
        {
            Vector3 directionToTarget = (currentTarget.position - firePoint.position).normalized;
            projectile.transform.position = firePoint.position;
            projectile.transform.rotation = Quaternion.LookRotation(directionToTarget);
            projectile.SetActive(true);
            projectile.GetComponent<Projectile>().Initialize(projectile.transform.forward);
        }
    }

    private GameObject GetPooledProjectile()
    {
        if (projectilePool.Count == 0) return null;

        for (int i = 0; i < projectilePool.Count; i++)
        {
            if (!projectilePool[i].activeInHierarchy)
            {
                return projectilePool[i];
            }
        }
        return null;
    }

    private void OnEvade(InputAction.CallbackContext context)
    {
        if (gameObject.layer == dodgingPlayerLayer) return;

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
            evadeDirection = -transform.forward;
        }

        rb.AddForce(evadeDirection * evadeForce, ForceMode.Impulse);
        gameObject.layer = dodgingPlayerLayer;
        Invoke(nameof(ResetPlayerLayer), dodgeDuration);

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, pushRadius);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Enemy"))
            {
                if (hitCollider.TryGetComponent<NavMeshAgent>(out var enemyAgent))
                {
                    Vector3 pushDir = (hitCollider.transform.position - transform.position).normalized;
                    pushDir.y = 0;
                    StartCoroutine(SmoothPushEnemy(enemyAgent, pushDir.normalized * pushForce, pushSmoothTime));
                }
            }
        }
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        Debug.Log("Unit Interact button pressed!");
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
            if (!agentToPush.isActiveAndEnabled)
            {
                yield break;
            }
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

    private void ResetPlayerLayer()
    {
        gameObject.layer = originalLayer;
    }

    

    private void UpdateRadiusVisualizer()
    {
        if (radiusVisualizer == null) return;

        int segments = 36;
        radiusVisualizer.positionCount = segments + 1;
        radiusVisualizer.loop = true;
        radiusVisualizer.useWorldSpace = false; // Draw relative to the unit

        float radius = weaponData.lockOnRadius;
        float angle = 0f;
        float angleStep = 360f / segments;

        for (int i = 0; i <= segments; i++)
        {
            float x = Mathf.Sin(Mathf.Deg2Rad * angle) * radius;
            float z = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;
            radiusVisualizer.SetPosition(i, new Vector3(x, 0.01f, z)); // 0.01f to avoid z-fighting with ground
            angle += angleStep;
        }
    }
}
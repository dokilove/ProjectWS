using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(LineRenderer))]
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
    [SerializeField] private Transform firePoint;
    [SerializeField] private float lockOnRadius = 15f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private Color defaultTargetColor = Color.red;
    [SerializeField] private Color lockOnTargetColor = Color.yellow;
    [SerializeField] private LineRenderer lockOnRadiusVisualizer;

    private float nextFireTime = 0f;
    private List<GameObject> projectilePool = new List<GameObject>();
    private int poolSize = 20;

    private Rigidbody rb;
    private InputSystem_Actions playerActions;
    private Vector2 moveInput;
    private int originalLayer;
    private LineRenderer lineRenderer;

    // --- Targeting Fields ---
    private Transform currentTarget;
    private List<Transform> potentialTargets = new List<Transform>();
    private bool isTargetLockOn = false; // New state for manual lock-on
    private float lastSwitchValue = 0f; // To detect button press from axis value

    private bool isFireHeld = false;

    public bool IsControlledByPlayer { get; private set; } = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerActions = new InputSystem_Actions();
        originalLayer = gameObject.layer;

        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
        lineRenderer.startColor = defaultTargetColor;
        lineRenderer.endColor = defaultTargetColor;
        lineRenderer.enabled = false;
    }

    private void Start()
    {
        if (weaponData == null || weaponData.projectilePrefab == null)
        {
            Debug.LogError("WeaponData or ProjectilePrefab is not assigned in the inspector!", this);
            return;
        }

        for (int i = 0; i < poolSize; i++)
        {
            GameObject proj = Instantiate(weaponData.projectilePrefab);
            proj.SetActive(false);
            projectilePool.Add(proj);
        }

        UpdateRadiusVisualizer();
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

        if (currentTarget != null)
        {
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, firePoint.position);
            lineRenderer.SetPosition(1, currentTarget.position);

            // Set color based on lock-on state
            Color lineColor = isTargetLockOn ? lockOnTargetColor : defaultTargetColor;
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;
        }
        else
        {
            lineRenderer.enabled = false;
        }

        if (isFireHeld && currentTarget != null && Time.time >= nextFireTime)
        {
            if (weaponData != null)
            {
                nextFireTime = Time.time + 1f / weaponData.fireRate;
                Fire();
            }
        }
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
        // Step 1: Find all potential targets
        potentialTargets = Physics.OverlapSphere(transform.position, lockOnRadius, enemyLayer)
                                .Select(col => col.transform)
                                .ToList();

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
        // Find the closest one and set it as the current target.
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
        currentTarget = closestTarget;
    }

    private void UpdateRadiusVisualizer()
    {
        if (lockOnRadiusVisualizer == null) return;

        int segments = 50;
        lockOnRadiusVisualizer.positionCount = segments + 1;
        lockOnRadiusVisualizer.useWorldSpace = false; // Draw relative to the player
        lockOnRadiusVisualizer.loop = true;

        float angle = 0f;
        for (int i = 0; i < (segments + 1); i++)
        {
            float x = Mathf.Sin(Mathf.Deg2Rad * angle) * lockOnRadius;
            float z = Mathf.Cos(Mathf.Deg2Rad * angle) * lockOnRadius;

            lockOnRadiusVisualizer.SetPosition(i, new Vector3(x, 0, z));

            angle += (360f / segments);
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
        if (currentTarget != null && Time.time >= nextFireTime)
        {
            if (weaponData != null)
            {
                nextFireTime = Time.time + 1f / weaponData.fireRate;
                Fire();
            }
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
        if (turretTransform == null) return;

        Quaternion targetRotation;
        if (target != null)
        {
            Vector3 direction = target.position - turretTransform.position;
            direction.y = 0;
            targetRotation = Quaternion.LookRotation(direction);
        }
        else
        {
            targetRotation = transform.rotation;
        }
        turretTransform.rotation = Quaternion.Slerp(turretTransform.rotation, targetRotation, turretRotationSpeed * Time.deltaTime);
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
}

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI; // Required for NavMeshAgent
using System.Collections; // Required for Coroutines
using System.Collections.Generic; // Required for List
using System.Linq; // Required for OrderBy

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(LineRenderer))] // Ensure LineRenderer is attached
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
    [SerializeField] private float detectionRadius = 15f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private float targetSwitchRate = 4f; // Switches per second for hold

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
    private int targetIndex = -1;
    private float nextSwitchTime = 0f; // Cooldown for target switching
    
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
        lineRenderer.startColor = Color.red;
        lineRenderer.endColor = Color.red;
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
        // No longer subscribing to SwitchTarget event
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
            // No longer unsubscribing from SwitchTarget event
        }
        rb.linearVelocity = Vector3.zero;
        moveInput = Vector2.zero;
    }

    private void Update()
    {
        if (!IsControlledByPlayer) return;

        moveInput = playerActions.Player.Move.ReadValue<Vector2>();

        UpdateTargets();
        HandleTargetSwitching(); // New method for polling

        RotateTurret(currentTarget);

        if (currentTarget != null)
        {
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, firePoint.position);
            lineRenderer.SetPosition(1, currentTarget.position);
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
        if (Mathf.Abs(switchValue) > 0.1f) // Input detected
        {
            if (potentialTargets.Count <= 1 || Time.time < nextSwitchTime) return;

            nextSwitchTime = Time.time + 1f / targetSwitchRate;

            if (switchValue > 0) // Cycle right
            {
                targetIndex++;
                if (targetIndex >= potentialTargets.Count) targetIndex = 0;
            }
            else // Cycle left
            {
                targetIndex--;
                if (targetIndex < 0) targetIndex = potentialTargets.Count - 1;
            }
            currentTarget = potentialTargets[targetIndex];
        }
    }

    private void UpdateTargets()
    {
        potentialTargets = Physics.OverlapSphere(transform.position, detectionRadius, enemyLayer)
                                .Select(col => col.transform)
                                .OrderBy(t => Vector3.SignedAngle(transform.forward, t.position - transform.position, Vector3.up))
                                .ToList();

        if (potentialTargets.Count == 0)
        {
            targetIndex = -1;
            currentTarget = null;
            return;
        }

        if (targetIndex != -1)
        {
            int oldTargetNewIndex = potentialTargets.IndexOf(currentTarget);
            if (oldTargetNewIndex == -1)
            {
                targetIndex = -1; // Mark for re-targeting
                currentTarget = null;
            }
            else
            {
                targetIndex = oldTargetNewIndex;
            }
        }

        if (targetIndex == -1)
        {
            float minAngle = float.MaxValue;
            for (int i = 0; i < potentialTargets.Count; i++)
            {
                float angle = Vector3.Angle(transform.forward, potentialTargets[i].position - transform.position);
                if (angle < minAngle)
                {
                    minAngle = angle;
                    targetIndex = i;
                }
            }
        }
        
        currentTarget = potentialTargets[targetIndex];
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

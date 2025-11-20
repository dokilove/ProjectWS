using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshAgent))]
public class VehicleController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f; // Vehicle might be faster
    [SerializeField] private float rotationSpeed = 15f;

    [Header("AI Follow")]
    [SerializeField] private VehicleAIBehaviour aiBehaviour;

    [Header("Attacks")]
    [SerializeField] private Transform turretTransform;
    [SerializeField] private float turretRotationSpeed = 10f;
    [SerializeField] private Transform firePoint;
    
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private WeaponData weaponData;

    [Header("Visuals")][SerializeField] private LineRenderer radiusVisualizer;
    [SerializeField] private LineRenderer targetLineRenderer;
    [SerializeField] private FieldOfViewMesh attackRangeVisualizer;
    [SerializeField] private Color defaultTargetColor = Color.red;
    [SerializeField] private Color lockOnTargetColor = Color.yellow;

    private float nextFireTime = 0f;
    private List<GameObject> projectilePool = new List<GameObject>();
    private int poolSize = 20;

    // --- Targeting Fields ---
    private Transform currentTarget;
    private List<Transform> potentialTargets = new List<Transform>();

    private Rigidbody rb;
    private NavMeshAgent agent;
    private InputSystem_Actions playerActions; // Will use player's input actions
    private Vector2 moveInput;
    private bool isFireHeld = false;
    private bool isBraking = false;

    // --- Ammo & Reloading ---
    private int currentAmmo;
    private bool isReloading = false;

    public bool IsControlledByPlayer { get; private set; } = false;

    public int CurrentAmmo => currentAmmo;
    public WeaponData WeaponData => weaponData;
    public bool IsReloading => isReloading;


#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
            {
                UpdateRadiusVisualizer();
                if (attackRangeVisualizer != null)
                {
                    if (weaponData != null) // Add null check for weaponData
                    {
                        attackRangeVisualizer.GenerateMesh(weaponData.attackAngle, weaponData.lockOnRadius);
                    }
                }
            }
        };
    }
#endif

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();
        playerActions = new InputSystem_Actions(); // Initialize here, but enable/disable externally

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
        if (weaponData == null || weaponData.projectileData == null || weaponData.projectileData.projectilePrefab == null) return;

        currentAmmo = weaponData.magazineSize;

        for (int i = 0; i < poolSize; i++)
        {
            GameObject proj = Instantiate(weaponData.projectileData.projectilePrefab);
            proj.SetActive(false);
            projectilePool.Add(proj);
        }
        UpdateRadiusVisualizer();
        if (attackRangeVisualizer != null && weaponData != null)
        {
            attackRangeVisualizer.GenerateMesh(weaponData.attackAngle, weaponData.lockOnRadius);
        }
    }

    public void EnableControl()
    {
        IsControlledByPlayer = true;
        agent.enabled = false;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        playerActions.Vehicle.Enable();
        playerActions.Vehicle.Fire.performed += OnFire;
        playerActions.Vehicle.Fire_Hold.started += OnFireHoldStarted;
        playerActions.Vehicle.Fire_Hold.canceled += OnFireHoldCanceled;
        playerActions.Vehicle.Brake.started += OnBrakeStarted;
        playerActions.Vehicle.Brake.canceled += OnBrakeCanceled;
    }

    public void DisableControl()
    {
        IsControlledByPlayer = false;
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeAll;

        agent.enabled = true;
        if (aiBehaviour != null)
        {
            agent.speed = aiBehaviour.followSpeed;
            agent.stoppingDistance = aiBehaviour.followStopDistance;
        }

        if (playerActions != null)
        {
            playerActions.Vehicle.Disable();
            playerActions.Vehicle.Fire.performed -= OnFire;
            playerActions.Vehicle.Fire_Hold.started -= OnFireHoldStarted;
            playerActions.Vehicle.Fire_Hold.canceled -= OnFireHoldCanceled;
            playerActions.Vehicle.Brake.started -= OnBrakeStarted;
            playerActions.Vehicle.Brake.canceled -= OnBrakeCanceled;
        }
        rb.linearVelocity = Vector3.zero;
        moveInput = Vector2.zero;
    }

    private void Update()
    {
        if (IsControlledByPlayer)
        {
            moveInput = playerActions.Vehicle.Move.ReadValue<Vector2>();

            UpdateAndSelectTarget();
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
                    Color lineColor = defaultTargetColor; // Vehicle AI doesn't have lock-on state
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
            if (isFireHeld && currentAmmo <= 0 && !isReloading)
            {
                StartCoroutine(Reload());
            }
        }
        else
        {
            // AI Follow & Attack Logic
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                if (PlayerPawnManager.ActivePlayerTransform != null)
                {
                    agent.SetDestination(PlayerPawnManager.ActivePlayerTransform.position);
                }
            }

            UpdateAndSelectTarget();
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
                    Color lineColor = defaultTargetColor; // Vehicle AI doesn't have lock-on state
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

            // Check if we can fire
            if (CanFire())
            {
                nextFireTime = Time.time + 1f / weaponData.fireRate;
                Fire();
            }
            else if (currentAmmo <= 0 && !isReloading)
            {
                StartCoroutine(Reload());
            }
        }
    }

    private bool CanFire()
    {
        if (currentTarget == null || Time.time < nextFireTime || weaponData == null || isReloading || currentAmmo <= 0)
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

    private System.Collections.IEnumerator Reload()
    {
        isReloading = true;
        Debug.Log("Vehicle reloading...");

        yield return new WaitForSeconds(weaponData.reloadTime);

        currentAmmo = weaponData.magazineSize;
        isReloading = false;
        Debug.Log("Vehicle reload complete.");
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
            return;
        }

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

    private void FixedUpdate()
    {
        if (!IsControlledByPlayer) return;

        rb.angularVelocity = Vector3.zero;

        // Calculate camera-relative forward and right vectors on the XZ plane (from UnitController)
        Vector3 cameraRight = Camera.main.transform.right;
        Vector3 cameraRightFlat = new Vector3(cameraRight.x, 0, cameraRight.z).normalized;
        
        Vector3 cameraForwardFlat = Vector3.Cross(Vector3.up, cameraRightFlat); // Forward is perpendicular to up and right

        Vector3 moveForward = -cameraForwardFlat; // Invert forward direction
        Vector3 moveRight = cameraRightFlat;

        Vector3 moveVector = (moveForward * moveInput.y + moveRight * moveInput.x);

        if (isBraking)
        {
            // Stop movement
            rb.linearVelocity = Vector3.zero;

            // Rotate in place
            if (moveVector.sqrMagnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveVector);
                rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
            }
        }
        else
        {
            // Normal movement
            rb.linearVelocity = moveVector * moveSpeed;

            if (moveVector != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveVector);
                rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
            }
        }
    }
    
    private void OnFire(InputAction.CallbackContext context)
    {
        if (CanFire())
        {
            nextFireTime = Time.time + 1f / weaponData.fireRate;
            Fire();
        }
        else if (currentAmmo <= 0 && !isReloading)
        {
            StartCoroutine(Reload());
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

    private void OnBrakeStarted(InputAction.CallbackContext context)
    {
        isBraking = true;
    }

    private void OnBrakeCanceled(InputAction.CallbackContext context)
    {
        isBraking = false;
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
            desiredRotation = transform.rotation; // Align with vehicle's forward if no target
        }

        // Calculate the angle difference from the vehicle's forward direction
        Quaternion vehicleForwardRotation = transform.rotation;
        Quaternion rotationDelta = Quaternion.Inverse(vehicleForwardRotation) * desiredRotation;
        float angle = 0;
        Vector3 axis = Vector3.zero;
        rotationDelta.ToAngleAxis(out angle, out axis);

        // Ensure the angle is within -180 to 180 range for clamping
        if (angle > 180f) angle -= 360f;
        if (angle < -180f) angle += 360f;

        // Clamp the angle
        float clampedAngle = Mathf.Clamp(angle, -weaponData.attackAngle / 2, weaponData.attackAngle / 2);

        // Reconstruct the clamped rotation
        Quaternion clampedRotation = vehicleForwardRotation * Quaternion.AngleAxis(clampedAngle, Vector3.up);

        // Smoothly rotate the turret
        turretTransform.rotation = Quaternion.Slerp(turretTransform.rotation, clampedRotation, turretRotationSpeed * Time.deltaTime);
    }

    private void Fire()
    {
        if (currentTarget == null) return;

        currentAmmo--;
        Debug.Log($"Vehicle Fire action triggered! Ammo left: {currentAmmo}");

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


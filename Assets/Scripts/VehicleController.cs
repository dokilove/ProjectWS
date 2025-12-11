using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshAgent))]
public class VehicleController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("AI Follow")]
    [SerializeField] private VehicleAIBehaviour aiBehaviour;

    [Header("Attacks")]
    [SerializeField] private Transform turretTransform;
    [SerializeField] private float turretRotationSpeed = 10f;
    [SerializeField] private Transform firePoint;
    
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private WeaponData weaponData;

    [Header("Visuals")]
    [SerializeField] private LineRenderer radiusVisualizer;
    [SerializeField] private FieldOfViewMesh attackRangeVisualizer;
    [SerializeField] private FieldOfViewMesh spreadAngleVisualizer;
    [SerializeField] private LineRenderer targetLineRenderer; // AI Only
    [SerializeField] private Color defaultTargetColor = Color.cyan;

    private float nextFireTime = 0f;
    private List<GameObject> projectilePool = new List<GameObject>();
    private int poolSize = 20;

    // --- Targeting Fields ---
    private Transform currentTarget; // For AI only
    private List<Transform> potentialTargets = new List<Transform>(); // For AI only

    private Rigidbody rb;
    private NavMeshAgent agent;
    private InputSystem_Actions playerActions;
    private Vector2 moveInput;
    private Vector2 lookInput; // For Player Control
    private Vector2 mousePositionInput; // For mouse aiming
    private bool isFireHeld = false;
    private bool isBraking = false;
    private Vector3 currentAimDirection; // For body rotation

    private enum InputDeviceType { None, Gamepad, MouseKeyboard }
    private InputDeviceType lastUsedInputDevice = InputDeviceType.None;

    // Delegates for input actions to allow proper unsubscribe
    private System.Action<InputAction.CallbackContext> onMovePerformed;
    private System.Action<InputAction.CallbackContext> onMoveCanceled;
    private System.Action<InputAction.CallbackContext> onLookPerformed;
    private System.Action<InputAction.CallbackContext> onLookCanceled;
    private System.Action<InputAction.CallbackContext> onMousePositionPerformed;
    private System.Action<InputAction.CallbackContext> onMousePositionCanceled;
    private System.Action<InputAction.CallbackContext> onFirePerformed;
    private System.Action<InputAction.CallbackContext> onFireHoldStarted;
    private System.Action<InputAction.CallbackContext> onFireHoldCanceled;
    private System.Action<InputAction.CallbackContext> onBrakeStarted;
    private System.Action<InputAction.CallbackContext> onBrakeCanceled;
    private System.Action<InputAction.CallbackContext> onReloadPerformed;

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
            if (this != null && weaponData != null)
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
        agent = GetComponent<NavMeshAgent>();
        playerActions = new InputSystem_Actions();

        // Initialize delegates
        onMovePerformed = ctx => { moveInput = ctx.ReadValue<Vector2>(); if (ctx.control.device is Gamepad) lastUsedInputDevice = InputDeviceType.Gamepad; else lastUsedInputDevice = InputDeviceType.MouseKeyboard; };
        onMoveCanceled = ctx => moveInput = Vector2.zero;
        onLookPerformed = ctx => { lookInput = ctx.ReadValue<Vector2>(); if (ctx.control.device is Gamepad) lastUsedInputDevice = InputDeviceType.Gamepad; };
        onLookCanceled = ctx => lookInput = Vector2.zero;
        onMousePositionPerformed = ctx => { mousePositionInput = ctx.ReadValue<Vector2>(); if (ctx.control.device is Mouse) lastUsedInputDevice = InputDeviceType.MouseKeyboard; };
        onMousePositionCanceled = ctx => mousePositionInput = Vector2.zero;
        onFirePerformed = ctx => { OnFire(ctx); if (ctx.control.device is Gamepad) lastUsedInputDevice = InputDeviceType.Gamepad; else lastUsedInputDevice = InputDeviceType.MouseKeyboard; };
        onFireHoldStarted = ctx => { OnFireHoldStarted(ctx); if (ctx.control.device is Gamepad) lastUsedInputDevice = InputDeviceType.Gamepad; else lastUsedInputDevice = InputDeviceType.MouseKeyboard; };
        onFireHoldCanceled = ctx => OnFireHoldCanceled(ctx);
        onBrakeStarted = ctx => { OnBrakeStarted(ctx); if (ctx.control.device is Gamepad) lastUsedInputDevice = InputDeviceType.Gamepad; else lastUsedInputDevice = InputDeviceType.MouseKeyboard; };
        onBrakeCanceled = ctx => OnBrakeCanceled(ctx);
        onReloadPerformed = ctx => { OnReload(ctx); if (ctx.control.device is Gamepad) lastUsedInputDevice = InputDeviceType.Gamepad; else lastUsedInputDevice = InputDeviceType.MouseKeyboard; };

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
            attackRangeVisualizer.SetActive(true);
        }

        if (spreadAngleVisualizer != null)
        {
            if (weaponData.spreadAngle > 0)
            {
                spreadAngleVisualizer.GenerateMesh(weaponData.spreadAngle, weaponData.lockOnRadius);
                spreadAngleVisualizer.SetColor(new Color(1f, 0.5f, 0f, 0.15f)); // Orange, semi-transparent
                spreadAngleVisualizer.SetActive(true);
                spreadAngleVisualizer.transform.SetParent(turretTransform);
                spreadAngleVisualizer.transform.localPosition = Vector3.zero;
                spreadAngleVisualizer.transform.localRotation = Quaternion.identity;
            }
            else
            {
                spreadAngleVisualizer.SetActive(false);
            }
        }
    }

    public void EnableControl()
    {
        IsControlledByPlayer = true;
        agent.enabled = false;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        
        playerActions.Vehicle.Enable();
        playerActions.Vehicle.Move.performed += onMovePerformed;
        playerActions.Vehicle.Move.canceled += onMoveCanceled;
        playerActions.Vehicle.Look.performed += onLookPerformed;
        playerActions.Vehicle.Look.canceled += onLookCanceled;
        playerActions.Vehicle.MousePosition.performed += onMousePositionPerformed;
        playerActions.Vehicle.MousePosition.canceled += onMousePositionCanceled;
        playerActions.Vehicle.Fire.performed += onFirePerformed;
        playerActions.Vehicle.Fire_Hold.started += onFireHoldStarted;
        playerActions.Vehicle.Fire_Hold.canceled += onFireHoldCanceled;
        playerActions.Vehicle.Brake.started += onBrakeStarted;
        playerActions.Vehicle.Brake.canceled += onBrakeCanceled;
        playerActions.Vehicle.Reload.performed += onReloadPerformed;
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
            playerActions.Vehicle.Move.performed -= onMovePerformed;
            playerActions.Vehicle.Move.canceled -= onMoveCanceled;
            playerActions.Vehicle.Look.performed -= onLookPerformed;
            playerActions.Vehicle.Look.canceled -= onLookCanceled;
            playerActions.Vehicle.MousePosition.performed -= onMousePositionPerformed;
            playerActions.Vehicle.MousePosition.canceled -= onMousePositionCanceled;
            playerActions.Vehicle.Fire.performed -= onFirePerformed;
            playerActions.Vehicle.Fire_Hold.started -= onFireHoldStarted;
            playerActions.Vehicle.Fire_Hold.canceled -= onFireHoldCanceled;
            playerActions.Vehicle.Brake.started -= onBrakeStarted;
            playerActions.Vehicle.Brake.canceled -= onBrakeCanceled;
            playerActions.Vehicle.Reload.performed -= onReloadPerformed;
        }
        rb.linearVelocity = Vector3.zero;
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
    }

    private void Update()
    {
        if (IsControlledByPlayer)
        {
            HandlePlayerControl();
        }
        else
        {
            HandleAIControl();
        }
    }

    private void FixedUpdate()
    {
        if (!IsControlledByPlayer) return;

        HandlePlayerMovementAndRotation();
    }

    private void HandlePlayerControl()
    {
        HandlePlayerAiming();

        if (isFireHeld && CanFirePlayer())
        {
            nextFireTime = Time.time + 1f / weaponData.fireRate;
            Fire();
        }
        if (isFireHeld && currentAmmo <= 0 && !isReloading)
        {
            StartCoroutine(Reload());
        }
        
        // if (attackRangeVisualizer != null)
        // {
        //     attackRangeVisualizer.SetActive(false); // Hide visual as it's misleading
        // }
        
        if (targetLineRenderer != null)
        {
            targetLineRenderer.enabled = true;
            targetLineRenderer.startColor = defaultTargetColor;
            targetLineRenderer.endColor = defaultTargetColor;
            targetLineRenderer.SetPosition(0, firePoint.position);
            targetLineRenderer.SetPosition(1, firePoint.position + turretTransform.forward * weaponData.lockOnRadius);
        }
    }

    private void HandleAIControl()
    {
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            if (PlayerPawnManager.ActivePlayerTransform != null)
            {
                agent.SetDestination(PlayerPawnManager.ActivePlayerTransform.position);
            }
        }

        UpdateAndSelectTargetAI();
        RotateTurretAI(currentTarget);

        if (attackRangeVisualizer != null)
        {
            attackRangeVisualizer.SetActive(true); // Show for AI
            attackRangeVisualizer.transform.position = firePoint.position;
            attackRangeVisualizer.transform.rotation = transform.rotation;
        }

        if (targetLineRenderer != null)
        {
            if (currentTarget != null)
            {
                targetLineRenderer.enabled = true;
                targetLineRenderer.startColor = defaultTargetColor;
                targetLineRenderer.endColor = defaultTargetColor;
                targetLineRenderer.SetPosition(0, firePoint.position);
                targetLineRenderer.SetPosition(1, currentTarget.position);
            }
            else
            {
                targetLineRenderer.enabled = false;
            }
        }

        if (CanFireAI())
        {
            nextFireTime = Time.time + 1f / weaponData.fireRate;
            Fire();
        }
        else if (currentAmmo <= 0 && !isReloading)
        {
            StartCoroutine(Reload());
        }
    }

    private void HandlePlayerAiming()
    {
        Vector3 previousAimDirection = currentAimDirection; // Store previous aimDirection to check if it changes
        Vector3 calculatedAimDirection = Vector3.zero;

        if (lastUsedInputDevice == InputDeviceType.Gamepad)
        {
            // Gamepad aiming (right stick)
            if (lookInput.sqrMagnitude > 0.1f * 0.1f) // Deadzone check
            {
                // Convert look input to a camera-relative direction
                Vector3 camForward = Camera.main.transform.forward;
                Vector3 camRight = Camera.main.transform.right;
                camForward.y = 0;
                camRight.y = 0;
                camForward.Normalize();
                camRight.Normalize();

                Vector3 lookDirection = (camForward * lookInput.y + camRight * lookInput.x).normalized;

                if (lookDirection != Vector3.zero)
                {
                    calculatedAimDirection = lookDirection;
                }
            }
            // If gamepad is active but lookInput is not significant, maintain current aimDirection
        }
        else if (lastUsedInputDevice == InputDeviceType.MouseKeyboard)
        {
            // Mouse aiming
            Ray ray = Camera.main.ScreenPointToRay(mousePositionInput);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("Ground"))) // Assuming a "Ground" layer for raycasting
            {
                Vector3 targetPoint = hit.point;
                Vector3 directionToMouse = targetPoint - transform.position;
                directionToMouse.y = 0; // Flatten the direction to aim on the XZ plane
                if (directionToMouse.sqrMagnitude > 0.01f) // Avoid zero vector
                {
                    calculatedAimDirection = directionToMouse.normalized;
                }
            }
        }

        if (calculatedAimDirection.sqrMagnitude > 0.01f)
        {
            currentAimDirection = calculatedAimDirection;
        }

        Quaternion desiredRotation = turretTransform.rotation; // Default to current rotation
        if (currentAimDirection.sqrMagnitude > 0.01f)
        {
            desiredRotation = Quaternion.LookRotation(currentAimDirection);
        }
        
        // --- Apply Clamping to desiredRotation's World Y-angle ---
        float halfAttackAngle = weaponData.attackAngle / 2;
        float vehicleForwardYAngle = transform.rotation.eulerAngles.y;
        float desiredYAngle = desiredRotation.eulerAngles.y;

        // Normalize desiredYAngle to be relative to vehicleForwardYAngle for easier clamping
        float relativeDesiredYAngle = desiredYAngle - vehicleForwardYAngle;
        if (relativeDesiredYAngle > 180f) relativeDesiredYAngle -= 360f;
        if (relativeDesiredYAngle < -180f) relativeDesiredYAngle += 360f;

        float clampedRelativeYAngle = Mathf.Clamp(relativeDesiredYAngle, -halfAttackAngle, halfAttackAngle);
        
        // Convert back to world angle
        float finalClampedWorldYAngle = vehicleForwardYAngle + clampedRelativeYAngle;
        
        // Construct the final desired rotation from the clamped world Y-angle
        desiredRotation = Quaternion.Euler(0, finalClampedWorldYAngle, 0);

        // Slerp to the desired rotation (now clamped)
        turretTransform.rotation = Quaternion.Slerp(turretTransform.rotation, desiredRotation, turretRotationSpeed * Time.deltaTime);

        // If aimDirection changed, reset lookInput to prevent residual gamepad input from interfering
        // This is less critical now with lastUsedInputDevice, but good for immediate feedback.
        if (calculatedAimDirection.sqrMagnitude > 0.01f)
        {
            lookInput = Vector2.zero;
        }
    }
    
    private void HandlePlayerMovementAndRotation()
    {
        rb.angularVelocity = Vector3.zero;

        if (isBraking)
        {
            // --- Braking Logic: Rotate in place to face aim direction ---
            rb.linearVelocity = Vector3.zero; // Stop movement

            // Rotate body to face the current aim direction (controlled by look/mouse)
            if (currentAimDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(currentAimDirection);
                rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
            }
        }
        else
        {
            // --- Normal Movement Logic ---
            // Velocity Calculation
            Vector3 cameraRight = Camera.main.transform.right;
            Vector3 cameraRightFlat = new Vector3(cameraRight.x, 0, cameraRight.z).normalized;
            Vector3 cameraForwardFlat = Vector3.Cross(Vector3.up, cameraRightFlat);
            Vector3 moveForward = -cameraForwardFlat;
            Vector3 moveRight = cameraRightFlat;
            Vector3 moveVector = (moveForward * moveInput.y + moveRight * moveInput.x);

            // Body Rotation (follows movement direction)
            if (moveVector.sqrMagnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveVector);
                rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
            }

            // Velocity Application
            rb.linearVelocity = moveVector * moveSpeed;
        }
    }

    private bool CanFirePlayer()
    {
        return Time.time >= nextFireTime && !isReloading && currentAmmo > 0;
    }

    private bool CanFireAI()
    {
        if (currentTarget == null || Time.time < nextFireTime || isReloading || currentAmmo <= 0)
        {
            return false;
        }

        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
        if (distanceToTarget > weaponData.lockOnRadius)
        {
            return false;
        }

        Vector3 directionToTarget = (currentTarget.position - turretTransform.position).normalized;
        float angleToTarget = Vector3.Angle(turretTransform.forward, directionToTarget);
        return angleToTarget <= weaponData.attackAngle / 2;
    }

    private IEnumerator Reload()
    {
        isReloading = true;
        Debug.Log("Vehicle reloading...");
        yield return new WaitForSeconds(weaponData.reloadTime);
        currentAmmo = weaponData.magazineSize;
        isReloading = false;
        Debug.Log("Vehicle reload complete.");
    }

    private void UpdateAndSelectTargetAI()
    {
        List<Transform> allTargetsInRadius = Physics.OverlapSphere(transform.position, weaponData.lockOnRadius, enemyLayer)
                                .Select(col => col.transform)
                                .ToList();

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

        currentTarget = potentialTargets.OrderBy(t => Vector3.Distance(transform.position, t.position)).FirstOrDefault();
    }
    
    private void OnFire(InputAction.CallbackContext context)
    {
        if (IsControlledByPlayer && CanFirePlayer())
        {
            nextFireTime = Time.time + 1f / weaponData.fireRate;
            Fire();
        }
        else if (IsControlledByPlayer && currentAmmo <= 0 && !isReloading)
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

    private void OnReload(InputAction.CallbackContext context)
    {
        if (IsControlledByPlayer && !isReloading && currentAmmo < weaponData.magazineSize)
        {
            StartCoroutine(Reload());
        }
    }

    private void RotateTurretAI(Transform target)
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
            desiredRotation = transform.rotation;
        }

        Quaternion vehicleForwardRotation = transform.rotation;
        Quaternion rotationDelta = Quaternion.Inverse(vehicleForwardRotation) * desiredRotation;
        float angle;
        Vector3 axis;
        rotationDelta.ToAngleAxis(out angle, out axis);

        if (angle > 180f) angle -= 360f;
        if (angle < -180f) angle += 360f;

        float clampedAngle = Mathf.Clamp(angle, -weaponData.attackAngle / 2, weaponData.attackAngle / 2);
        Quaternion clampedRotation = vehicleForwardRotation * Quaternion.AngleAxis(clampedAngle, Vector3.up);

        turretTransform.rotation = Quaternion.Slerp(turretTransform.rotation, clampedRotation, turretRotationSpeed * Time.deltaTime);
    }

    private void Fire()
    {
        currentAmmo--;
        Debug.Log($"Vehicle Fire! Ammo left: {currentAmmo}");

        // --- Base Fire Direction Calculation (with Aim-Assist) ---
        Vector3 baseFireDirection = turretTransform.forward; // Default horizontal direction
        if (IsControlledByPlayer)
        {
            // Find the closest enemy in the aim direction to adjust height
            RaycastHit[] hits = Physics.SphereCastAll(firePoint.position, 1f, turretTransform.forward, weaponData.lockOnRadius, enemyLayer);
            
            Transform closestEnemy = null;
            float minDistance = float.MaxValue;

            if (hits.Length > 0)
            {
                foreach (var hit in hits)
                {
                    if (hit.transform.CompareTag("Enemy")) // Assuming enemies have this tag
                    {
                        float distance = Vector3.Distance(firePoint.position, hit.transform.position);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            closestEnemy = hit.transform;
                        }
                    }
                }
            }

            // If an enemy was found, adjust the fire direction to aim at its center
            if (closestEnemy != null)
            {
                baseFireDirection = (closestEnemy.position - firePoint.position).normalized;
            }
        }
        else // AI adjusts for target height
        {
            if (currentTarget != null)
            {
                baseFireDirection = (currentTarget.position - firePoint.position).normalized;
            }
        }
        // --- End of Base Fire Direction Calculation ---

        for (int i = 0; i < weaponData.projectilesPerShot; i++)
        {
            GameObject projectile = GetPooledProjectile();
            if (projectile != null)
            {
                // Calculate spread for each projectile
                Vector3 finalFireDirection = baseFireDirection;
                if (weaponData.spreadAngle > 0)
                {
                    float randomAngle = Random.Range(-weaponData.spreadAngle / 2, weaponData.spreadAngle / 2);
                    finalFireDirection = Quaternion.Euler(0, randomAngle, 0) * finalFireDirection;
                }

                projectile.transform.position = firePoint.position;
                projectile.transform.rotation = Quaternion.LookRotation(finalFireDirection);
                projectile.SetActive(true);
                projectile.GetComponent<Projectile>().Initialize(finalFireDirection);
            }
        }
    }

    private GameObject GetPooledProjectile()
    {
        foreach (var proj in projectilePool)
        {
            if (!proj.activeInHierarchy)
            {
                return proj;
            }
        }
        return null;
    }

    private void UpdateRadiusVisualizer()
    {
        if (radiusVisualizer == null || weaponData == null) return;

        int segments = 36;
        radiusVisualizer.positionCount = segments + 1;
        radiusVisualizer.loop = true;
        radiusVisualizer.useWorldSpace = false;

        float radius = weaponData.lockOnRadius;
        float angle = 0f;
        float angleStep = 360f / segments;

        for (int i = 0; i <= segments; i++)
        {
            float x = Mathf.Sin(Mathf.Deg2Rad * angle) * radius;
            float z = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;
            radiusVisualizer.SetPosition(i, new Vector3(x, 0.01f, z));
            angle += angleStep;
        }
    }
}


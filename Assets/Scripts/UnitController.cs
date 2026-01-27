using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class UnitController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("Evade")]
    [SerializeField] private EvadeData evadeData;

    [Header("Attacks")]
    [SerializeField] private Transform turretTransform;
    [SerializeField] private float turretRotationSpeed = 20f;
    [SerializeField] private Transform firePoint;
    [SerializeField] private WeaponData weaponData;

    [Header("Visuals")]
    [SerializeField] private LineRenderer radiusVisualizer;
    [SerializeField] private FieldOfViewMesh attackRangeVisualizer;
    [SerializeField] private FieldOfViewMesh spreadAngleVisualizer;
    [SerializeField] private LineRenderer targetLineRenderer; // For aiming direction
    [SerializeField] private Color aimLineColor = Color.yellow;

    [Header("Visual Effects")]
    [SerializeField] private TrailRenderer evadeTrailRenderer;

    // --- Animation ---
    [SerializeField] private Animator _animator;

    private float nextFireTime = 0f;
    private List<GameObject> projectilePool = new List<GameObject>();
    private int poolSize = 20;

    private Rigidbody rb;
    private InputSystem_Actions playerActions;
    private Vector2 moveInput;
    private Vector2 lookInput; // For twin-stick aiming
    private Vector2 mousePositionInput; // For mouse aiming
    private int originalLayer;

    private enum InputDeviceType { None, Gamepad, MouseKeyboard }
    private InputDeviceType lastUsedInputDevice = InputDeviceType.None;

    // Delegates for input actions to allow proper unsubscribe
    private System.Action<InputAction.CallbackContext> onMovePerformed;
    private System.Action<InputAction.CallbackContext> onMoveCanceled;
    private System.Action<InputAction.CallbackContext> onLookPerformed;
    private System.Action<InputAction.CallbackContext> onLookCanceled;
    private System.Action<InputAction.CallbackContext> onMousePositionPerformed;
    private System.Action<InputAction.CallbackContext> onMousePositionCanceled;
    private System.Action<InputAction.CallbackContext> onEvadePerformed;
    private System.Action<InputAction.CallbackContext> onInteractPerformed;
    private System.Action<InputAction.CallbackContext> onFirePerformed;
    private System.Action<InputAction.CallbackContext> onFireHoldStarted;
    private System.Action<InputAction.CallbackContext> onFireHoldCanceled;
    private System.Action<InputAction.CallbackContext> onReloadPerformed;

    private bool isFireHeld = false;
    private Vector3 aimDirection;

    // --- Evade Charges ---
    private int currentEvadeCharges;
    private float lastEvadeTime;

    // --- Ammo & Reloading ---
    private int currentAmmo;
    private bool isReloading = false;

    public bool IsControlledByPlayer { get; private set; } = false;

    public int CurrentAmmo => currentAmmo;
    public WeaponData WeaponData => weaponData;
    public bool IsReloading => isReloading;

    public EvadeData EvadeData => evadeData;
    public int CurrentEvadeCharges => currentEvadeCharges;
    public float LastEvadeTime => lastEvadeTime;


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
                    // Attack angle is now 360 degrees for the turret, but the visual can still show the data value.
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
        _animator = GetComponentInChildren<Animator>(); // Assuming Animator is on a child object
        if (_animator == null)
        {
            Debug.LogWarning("Animator component not found on UnitController or its children.", this);
        }

        // Initialize delegates
        onMovePerformed = ctx => { moveInput = ctx.ReadValue<Vector2>(); if (ctx.control.device is Gamepad) lastUsedInputDevice = InputDeviceType.Gamepad; else lastUsedInputDevice = InputDeviceType.MouseKeyboard; };
        onMoveCanceled = ctx => moveInput = Vector2.zero;
        onLookPerformed = ctx => { lookInput = ctx.ReadValue<Vector2>(); if (ctx.control.device is Gamepad) lastUsedInputDevice = InputDeviceType.Gamepad; };
        onLookCanceled = ctx => lookInput = Vector2.zero;
        onMousePositionPerformed = ctx => { mousePositionInput = ctx.ReadValue<Vector2>(); if (ctx.control.device is Mouse) lastUsedInputDevice = InputDeviceType.MouseKeyboard; };
        onMousePositionCanceled = ctx => mousePositionInput = Vector2.zero;
        onEvadePerformed = ctx => { OnEvade(ctx); if (ctx.control.device is Gamepad) lastUsedInputDevice = InputDeviceType.Gamepad; else lastUsedInputDevice = InputDeviceType.MouseKeyboard; };
        onInteractPerformed = ctx => { OnInteract(ctx); if (ctx.control.device is Gamepad) lastUsedInputDevice = InputDeviceType.Gamepad; else lastUsedInputDevice = InputDeviceType.MouseKeyboard; };
        onFirePerformed = ctx => { OnFire(ctx); if (ctx.control.device is Gamepad) lastUsedInputDevice = InputDeviceType.Gamepad; else lastUsedInputDevice = InputDeviceType.MouseKeyboard; };
        onFireHoldStarted = ctx => { OnFireHoldStarted(ctx); if (ctx.control.device is Gamepad) lastUsedInputDevice = InputDeviceType.Gamepad; else lastUsedInputDevice = InputDeviceType.MouseKeyboard; };
        onFireHoldCanceled = ctx => OnFireHoldCanceled(ctx);
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

        if (evadeTrailRenderer != null)
        {
            evadeTrailRenderer.enabled = false;
            if (evadeData != null)
            {
                evadeTrailRenderer.time = evadeData.dodgeDuration + evadeData.trailOffset;
            }
        }
    }

    private void Start()
    {
        if (weaponData == null || weaponData.projectileData == null || weaponData.projectileData.projectilePrefab == null)
        {
            Debug.LogError("WeaponData, ProjectileData, or ProjectilePrefab is not assigned in the inspector!", this);
            return;
        }

        currentAmmo = weaponData.magazineSize;
        currentEvadeCharges = evadeData.maxEvadeCharges;
        lastEvadeTime = Time.time;
        aimDirection = transform.forward;

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
            attackRangeVisualizer.SetActive(true); // Show attack angle visual for player
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
        this.enabled = true;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        gameObject.SetActive(true);

        playerActions.Player.Enable();
        playerActions.Player.Move.performed += onMovePerformed;
        playerActions.Player.Move.canceled += onMoveCanceled;
        playerActions.Player.Look.performed += onLookPerformed;
        playerActions.Player.Look.canceled += onLookCanceled;
        playerActions.Player.MousePosition.performed += onMousePositionPerformed;
        playerActions.Player.MousePosition.canceled += onMousePositionCanceled;
        playerActions.Player.Evade.performed += onEvadePerformed;
        playerActions.Player.Interact.performed += onInteractPerformed;
        playerActions.Player.Fire.performed += onFirePerformed;
        playerActions.Player.Fire_Hold.started += onFireHoldStarted;
        playerActions.Player.Fire_Hold.canceled += onFireHoldCanceled;
        playerActions.Player.Reload.performed += onReloadPerformed;
    }

    public void DisableControl()
    {
        IsControlledByPlayer = false;
        this.enabled = false;
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeAll;
        gameObject.SetActive(false);

        if (targetLineRenderer != null)
        {
            targetLineRenderer.enabled = false;
        }

        if (playerActions != null)
        {
            playerActions.Player.Disable();
            if (playerActions != null)
        {
            playerActions.Player.Disable();
            playerActions.Player.Move.performed -= onMovePerformed;
            playerActions.Player.Move.canceled -= onMoveCanceled;
            playerActions.Player.Look.performed -= onLookPerformed;
            playerActions.Player.Look.canceled -= onLookCanceled;
            playerActions.Player.MousePosition.performed -= onMousePositionPerformed;
            playerActions.Player.MousePosition.canceled -= onMousePositionCanceled;
            playerActions.Player.Evade.performed -= onEvadePerformed;
            playerActions.Player.Interact.performed -= onInteractPerformed;
            playerActions.Player.Fire.performed -= onFirePerformed;
            playerActions.Player.Fire_Hold.started -= onFireHoldStarted;
            playerActions.Player.Fire_Hold.canceled -= onFireHoldCanceled;
            playerActions.Player.Reload.performed -= onReloadPerformed;
        }
        }
        rb.linearVelocity = Vector3.zero;
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        isFireHeld = false;
    }

    private void Update()
    {
        if (!IsControlledByPlayer) return;

        HandleAimingAndFiring();
        HandleEvadeChargeRegen();
        
        if (targetLineRenderer != null)
        {
            targetLineRenderer.enabled = true;
            targetLineRenderer.startColor = aimLineColor;
            targetLineRenderer.endColor = aimLineColor;
            targetLineRenderer.SetPosition(0, firePoint.position);
            targetLineRenderer.SetPosition(1, firePoint.position + turretTransform.forward * weaponData.lockOnRadius);
        }
    }

    private void FixedUpdate()
    {
        if (!IsControlledByPlayer) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        rb.angularVelocity = Vector3.zero;

        // --- Velocity Calculation ---
        Vector3 cameraRight = Camera.main.transform.right;
        Vector3 cameraRightFlat = new Vector3(cameraRight.x, 0, cameraRight.z).normalized;
        Vector3 cameraForwardFlat = Vector3.Cross(Vector3.up, cameraRightFlat);
        Vector3 moveForward = -cameraForwardFlat;
        Vector3 moveRight = cameraRightFlat;

        Vector3 moveVector = (moveForward * moveInput.y + moveRight * moveInput.x);
        rb.linearVelocity = moveVector * moveSpeed;

        // --- Animation ---
        if (_animator != null)
        {
            // Get the aiming direction as the reference "forward"
            Vector3 aimForward = aimDirection;
            aimForward.y = 0;
            aimForward.Normalize();

            // Get the perpendicular right vector from the aim direction
            Vector3 aimRight = Vector3.Cross(Vector3.up, aimForward).normalized;

            // Get the raw world-space move direction from input, but use the calculated velocity for accuracy
            Vector3 currentMoveDirection = rb.linearVelocity.normalized;

            // Project the move direction onto the aim-relative axes
            float runX = Vector3.Dot(currentMoveDirection, aimRight);
            float runY = Vector3.Dot(currentMoveDirection, aimForward);

            // Update animator parameters
            _animator.SetFloat("Run_x", runX);
            _animator.SetFloat("Run_y", runY);
        }

        // --- Body Rotation (Aiming) ---
        // Body now rotates to face the aim direction, allowing for strafing.
        if (aimDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(aimDirection);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private void HandleAimingAndFiring()
    {
        // --- Aiming ---
        // Determine aiming based on last used input device
        Vector3 currentAimDirection = aimDirection; // Store current aimDirection to check if it changes

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
                    aimDirection = lookDirection;
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
                    aimDirection = directionToMouse.normalized;
                }
            }
        }

        // Rotate turret to face the aim direction
        if (aimDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(aimDirection);
            turretTransform.rotation = Quaternion.Slerp(turretTransform.rotation, targetRotation, turretRotationSpeed * Time.deltaTime);
        }

        // If aimDirection changed, reset lookInput to prevent residual gamepad input from interfering
        // This is less critical now with lastUsedInputDevice, but good for immediate feedback.
        if (currentAimDirection != aimDirection)
        {
            lookInput = Vector2.zero;
        }


        // --- Firing ---
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

    private bool CanFire()
    {
        // No longer checks angle, as firing is always aligned with the turret
        return Time.time >= nextFireTime && !isReloading && currentAmmo > 0;
    }

    private void Fire()
    {
        currentAmmo--;
        Debug.Log($"Unit Fire! Ammo left: {currentAmmo}");

        if (_animator != null)
        {
            _animator.SetTrigger("Attack");
        }

        for (int i = 0; i < weaponData.projectilesPerShot; i++)
        {
            GameObject projectileGO = GetPooledProjectile();
            if (projectileGO != null)
            {
                projectileGO.transform.position = firePoint.position;

                // Calculate spread
                Vector3 fireDirection = turretTransform.forward;
                if (weaponData.spreadAngle > 0)
                {
                    float randomAngle = Random.Range(-weaponData.spreadAngle / 2, weaponData.spreadAngle / 2);
                    fireDirection = Quaternion.Euler(0, randomAngle, 0) * fireDirection;
                }
                
                projectileGO.transform.rotation = Quaternion.LookRotation(fireDirection);
                projectileGO.SetActive(true);

                Projectile projectile = projectileGO.GetComponent<Projectile>();
                if (projectile != null)
                {
                    projectile.Initialize(fireDirection);
                }
            }
        }
    }

    private IEnumerator Reload()
    {
        isReloading = true;
        Debug.Log("Reloading...");
        yield return new WaitForSeconds(weaponData.reloadTime);
        currentAmmo = weaponData.magazineSize;
        isReloading = false;
        Debug.Log("Reload complete.");
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

    private void HandleEvadeChargeRegen()
    {
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

    private void OnReload(InputAction.CallbackContext context)
    {
        if (!isReloading && currentAmmo < weaponData.magazineSize)
        {
            StartCoroutine(Reload());
        }
    }

    private void OnEvade(InputAction.CallbackContext context)
    {
        if (gameObject.layer == evadeData.dodgingPlayerLayer || currentEvadeCharges <= 0) return;

        currentEvadeCharges--;
        lastEvadeTime = Time.time;

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

        rb.AddForce(evadeDirection * evadeData.evadeForce, ForceMode.Impulse);
        gameObject.layer = evadeData.dodgingPlayerLayer;
        if (evadeTrailRenderer != null)
        {
            evadeTrailRenderer.Clear();
            evadeTrailRenderer.enabled = true;
        }
        Invoke(nameof(ResetPlayerLayer), evadeData.dodgeDuration);

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

    private void ResetPlayerLayer()
    {
        gameObject.layer = originalLayer;
        if (evadeTrailRenderer != null)
        {
            evadeTrailRenderer.enabled = false;
        }
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

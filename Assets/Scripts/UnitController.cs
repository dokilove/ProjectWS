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
    [SerializeField] private LineRenderer targetLineRenderer; // For aiming direction
    [SerializeField] private Color aimLineColor = Color.yellow;

    [Header("Visual Effects")]
    [SerializeField] private TrailRenderer evadeTrailRenderer;

    private float nextFireTime = 0f;
    private List<GameObject> projectilePool = new List<GameObject>();
    private int poolSize = 20;

    private Rigidbody rb;
    private InputSystem_Actions playerActions;
    private Vector2 moveInput;
    private Vector2 lookInput; // For twin-stick aiming
    private int originalLayer;

    private bool isFireHeld = false;
    private bool isBraking = false; // Added for player brake functionality

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
    }

    public void EnableControl()
    {
        IsControlledByPlayer = true;
        this.enabled = true;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        gameObject.SetActive(true);

        playerActions.Player.Enable();
        playerActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        playerActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;
        playerActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        playerActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;
        playerActions.Player.Evade.performed += OnEvade;
        playerActions.Player.Interact.performed += OnInteract;
        playerActions.Player.Fire.performed += OnFire;
        playerActions.Player.Fire_Hold.started += OnFireHoldStarted;
        playerActions.Player.Fire_Hold.canceled += OnFireHoldCanceled;
        playerActions.Player.Brake.started += OnBrakeStarted; // Added brake input
        playerActions.Player.Brake.canceled += OnBrakeCanceled; // Added brake input
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
            playerActions.Player.Move.performed -= ctx => moveInput = ctx.ReadValue<Vector2>();
            playerActions.Player.Move.canceled -= ctx => moveInput = Vector2.zero;
            playerActions.Player.Look.performed -= ctx => lookInput = ctx.ReadValue<Vector2>();
            playerActions.Player.Look.canceled -= ctx => lookInput = Vector2.zero;
            playerActions.Player.Evade.performed -= OnEvade;
            playerActions.Player.Interact.performed -= OnInteract;
            playerActions.Player.Fire.performed -= OnFire;
            playerActions.Player.Fire_Hold.started -= OnFireHoldStarted;
            playerActions.Player.Fire_Hold.canceled -= OnFireHoldCanceled;
            playerActions.Player.Brake.started -= OnBrakeStarted; // Removed brake input
            playerActions.Player.Brake.canceled -= OnBrakeCanceled; // Removed brake input
        }
        rb.linearVelocity = Vector3.zero;
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
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

        // --- Body Rotation (Movement Only) ---
        // Body rotation is now ONLY determined by movement input. Aiming does not affect it.
        if (!isBraking && moveVector.sqrMagnitude > 0.1f) // Only rotate based on movement if not braking
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveVector);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
        else if (isBraking && moveInput.sqrMagnitude > 0.1f) // New: Rotate in place when braking, using moveInput
        {
            // Use moveInput to determine rotation direction
            // Reuse cameraRightFlat and cameraForwardFlat from outer scope
            Vector3 moveForwardForRotation = -cameraForwardFlat; // cameraForwardFlat is already defined
            Vector3 moveRightForRotation = cameraRightFlat; // cameraRightFlat is already defined

            Vector3 rotationVector = (moveForwardForRotation * moveInput.y + moveRightForRotation * moveInput.x);
            
            if (rotationVector != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(rotationVector);
                rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
            }
        }
    }

    private void HandleAimingAndFiring()
    {
        // --- Aiming ---
        Quaternion desiredRotation = turretTransform.rotation; // Default to current rotation
        if (lookInput.sqrMagnitude > 0.1f * 0.1f) // Deadzone check
        {
            // Convert look input to a camera-relative direction
            Vector3 camForward = Camera.main.transform.forward;
            Vector3 camRight = Camera.main.transform.right;
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            // Vertical aim is NOT inverted, unlike movement
            Vector3 aimDirection = (camForward * lookInput.y + camRight * lookInput.x).normalized;

            if (aimDirection != Vector3.zero)
            {
                desiredRotation = Quaternion.LookRotation(aimDirection);
            }
        }
        
        // --- Apply Clamping to desiredRotation's World Y-angle ---
        float halfAttackAngle = weaponData.attackAngle / 2;
        float playerForwardYAngle = transform.rotation.eulerAngles.y;
        float desiredYAngle = desiredRotation.eulerAngles.y;

        // Normalize desiredYAngle to be relative to playerForwardYAngle for easier clamping
        float relativeDesiredYAngle = desiredYAngle - playerForwardYAngle;
        if (relativeDesiredYAngle > 180f) relativeDesiredYAngle -= 360f;
        if (relativeDesiredYAngle < -180f) relativeDesiredYAngle += 360f;

        float clampedRelativeYAngle = Mathf.Clamp(relativeDesiredYAngle, -halfAttackAngle, halfAttackAngle);
        
        // Convert back to world angle
        float finalClampedWorldYAngle = playerForwardYAngle + clampedRelativeYAngle;
        
        // Construct the final desired rotation from the clamped world Y-angle
        desiredRotation = Quaternion.Euler(0, finalClampedWorldYAngle, 0);

        // Slerp to the desired rotation (now clamped)
        turretTransform.rotation = Quaternion.Slerp(turretTransform.rotation, desiredRotation, turretRotationSpeed * Time.deltaTime);

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

        GameObject projectileGO = GetPooledProjectile();
        if (projectileGO != null)
        {
            projectileGO.transform.position = firePoint.position;
            projectileGO.transform.rotation = turretTransform.rotation; // Fire in the direction turret is facing
            projectileGO.SetActive(true);

            Projectile projectile = projectileGO.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.Initialize(turretTransform.forward);
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

    private void OnBrakeStarted(InputAction.CallbackContext context)
    {
        isBraking = true;
    }

    private void OnBrakeCanceled(InputAction.CallbackContext context)
    {
        isBraking = false;
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

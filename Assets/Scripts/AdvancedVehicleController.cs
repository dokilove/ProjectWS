using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshAgent))]
public class AdvancedVehicleController : MonoBehaviour, IVehicle
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float rotationSpeed = 3f;   // 낮을수록 영상처럼 회전 반경이 넓어집니다 (추천: 2~5)
    [SerializeField] private float acceleration = 5f;    // 출발 시 가속도
    [SerializeField] private float deceleration = 8f;    // 정지 시 감속도

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
    private RaycastHit[] hitResults = new RaycastHit[10]; // GC 최적화를 위한 배열

    private Rigidbody rb;
    private NavMeshAgent agent;
    private InputSystem_Actions playerActions;
    private Vector2 moveInput;
    private Vector2 lookInput; // For Player Control
    private Vector2 mousePositionInput; // For mouse aiming
    private bool isFireHeld = false;
    private bool isBraking = false;
    private Vector3 currentAimDirection; // For body rotation

    // --- Physics State ---
    private float currentSpeed = 0f; // 현재 속도 캐싱

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

        // ★중요: Agent가 직접 위치를 조작하지 못하게 하고 물리 엔진(Rigidbody)에 맡김
        agent.updatePosition = false;
        agent.updateRotation = false;

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
                spreadAngleVisualizer.SetColor(new Color(1f, 0.5f, 0f, 0.15f));
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
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeAll;

        agent.enabled = false;

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
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        isBraking = false;
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

            // ★ AI의 길찾기 위치를 실제 물리(Rigidbody) 위치로 계속 동기화
            if (agent.enabled)
            {
                agent.nextPosition = transform.position;
            }
        }
    }

    private void FixedUpdate()
    {
        // 이제 플레이어와 AI 모두 동일한 물리 법칙을 따름
        HandleVehiclePhysics();
    }

    private void HandleVehiclePhysics()
    {
        Vector3 targetDirection = Vector3.zero;
        float inputMagnitude = 0f;

        // 1. 방향 입력 결정 (플레이어 or AI)
        if (IsControlledByPlayer)
        {
            if (isBraking)
            {
                // 브레이크 중일 때: 제자리에서 포신 방향으로 차체 회전 (원래 로직 유지)
                currentSpeed = Mathf.Lerp(currentSpeed, 0, deceleration * 2f * Time.fixedDeltaTime);

                if (currentAimDirection.sqrMagnitude > 0.01f && currentSpeed < 1f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(currentAimDirection);
                    rb.rotation = Quaternion.Slerp(rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
                }

                rb.linearVelocity = transform.forward * currentSpeed;
                return;
            }

            Vector3 cameraRight = Camera.main.transform.right;
            Vector3 cameraRightFlat = new Vector3(cameraRight.x, 0, cameraRight.z).normalized;
            Vector3 cameraForwardFlat = Vector3.Cross(Vector3.up, cameraRightFlat);

            Vector3 moveForward = -cameraForwardFlat;
            Vector3 moveVector = (moveForward * moveInput.y + cameraRightFlat * moveInput.x);

            targetDirection = moveVector.normalized;
            inputMagnitude = Mathf.Clamp01(moveVector.magnitude);
        }
        else
        {
            // AI 조작일 때
            if (agent.enabled && agent.hasPath)
            {
                targetDirection = agent.desiredVelocity.normalized;
                inputMagnitude = 1f; // AI는 경로가 있으면 항상 가속
            }
        }

        // 2. 물리 적용 (회전 후 전진)
        if (targetDirection.sqrMagnitude > 0.01f)
        {
            // [조향] 입력 방향으로 부드럽게 회전 (Arc 궤적 생성)
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);

            // [가속]
            currentSpeed = Mathf.Lerp(currentSpeed, moveSpeed * inputMagnitude, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            // [감속] 입력이 없으면 서서히 정지
            currentSpeed = Mathf.Lerp(currentSpeed, 0, deceleration * Time.fixedDeltaTime);
        }

        // [이동] 목표 방향이 아닌, "차체가 바라보는 방향(forward)"으로 전진
        rb.linearVelocity = transform.forward * currentSpeed;

        // 회전 관성 초기화
        rb.angularVelocity = Vector3.zero;
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
        if (PlayerPawnManager.ActivePlayerTransform == null || aiBehaviour == null)
        {
            if (agent.enabled) agent.enabled = false;
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, PlayerPawnManager.ActivePlayerTransform.position);

        if (distanceToTarget > aiBehaviour.followStopDistance)
        {
            if (!agent.enabled)
            {
                agent.enabled = true;
                agent.speed = aiBehaviour.followSpeed;
                agent.stoppingDistance = aiBehaviour.followStopDistance;
            }
            if (agent.isOnNavMesh)
            {
                agent.SetDestination(PlayerPawnManager.ActivePlayerTransform.position);
            }
        }
        else
        {
            if (agent.enabled)
            {
                if (agent.isOnNavMesh) agent.ResetPath();
                agent.enabled = false; // 물리 엔진 감속을 위해 길찾기 끔
            }
        }

        UpdateAndSelectTargetAI();
        RotateTurretAI(currentTarget);

        if (attackRangeVisualizer != null)
        {
            attackRangeVisualizer.SetActive(true);
            attackRangeVisualizer.transform.position = firePoint.position;
            attackRangeVisualizer.transform.rotation = transform.rotation;
        }

        if (targetLineRenderer != null)
        {
            if (currentTarget != null)
            {
                targetLineRenderer.enabled = true;
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
        Vector3 calculatedAimDirection = Vector3.zero;

        if (lastUsedInputDevice == InputDeviceType.Gamepad)
        {
            if (lookInput.sqrMagnitude > 0.01f)
            {
                Vector3 camForward = Camera.main.transform.forward;
                Vector3 camRight = Camera.main.transform.right;
                camForward.y = 0;
                camRight.y = 0;
                camForward.Normalize();
                camRight.Normalize();

                calculatedAimDirection = (camForward * lookInput.y + camRight * lookInput.x).normalized;
            }
        }
        else if (lastUsedInputDevice == InputDeviceType.MouseKeyboard)
        {
            Ray ray = Camera.main.ScreenPointToRay(mousePositionInput);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("Ground")))
            {
                Vector3 directionToMouse = hit.point - transform.position;
                directionToMouse.y = 0;
                if (directionToMouse.sqrMagnitude > 0.01f)
                {
                    calculatedAimDirection = directionToMouse.normalized;
                }
            }
        }

        if (calculatedAimDirection.sqrMagnitude > 0.01f)
        {
            currentAimDirection = calculatedAimDirection;
        }

        // ★개선된 포탑 클램핑 로직 (0/360도 경계에서 튀는 현상 방지)
        if (currentAimDirection.sqrMagnitude > 0.01f)
        {
            // 월드 방향을 차체 기준 로컬 방향으로 변환
            Vector3 localTargetDir = transform.InverseTransformDirection(currentAimDirection);

            // 로컬 Y축 회전 각도 계산 (Atan2 사용)
            float targetAngle = Mathf.Atan2(localTargetDir.x, localTargetDir.z) * Mathf.Rad2Deg;

            // 각도 제한 적용
            float clampedAngle = Mathf.Clamp(targetAngle, -weaponData.attackAngle / 2f, weaponData.attackAngle / 2f);

            // 최종 로컬 로테이션 적용
            Quaternion desiredLocalRotation = Quaternion.Euler(0, clampedAngle, 0);
            turretTransform.localRotation = Quaternion.Slerp(turretTransform.localRotation, desiredLocalRotation, turretRotationSpeed * Time.deltaTime);

            lookInput = Vector2.zero;
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
        yield return new WaitForSeconds(weaponData.reloadTime);
        currentAmmo = weaponData.magazineSize;
        isReloading = false;
    }

    private void UpdateAndSelectTargetAI()
    {
        // 최적화를 위해 OverlapSphereNonAlloc 사용 고려 (일단 기존 로직 유지)
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

    private void OnFireHoldStarted(InputAction.CallbackContext context) { isFireHeld = true; }
    private void OnFireHoldCanceled(InputAction.CallbackContext context) { isFireHeld = false; }
    private void OnBrakeStarted(InputAction.CallbackContext context) { isBraking = true; }
    private void OnBrakeCanceled(InputAction.CallbackContext context) { isBraking = false; }

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

        if (target != null)
        {
            Vector3 direction = target.position - turretTransform.position;
            direction.y = 0;

            // AI 포탑도 InverseTransformDirection으로 클램핑 최적화
            Vector3 localTargetDir = transform.InverseTransformDirection(direction.normalized);
            float targetAngle = Mathf.Atan2(localTargetDir.x, localTargetDir.z) * Mathf.Rad2Deg;
            float clampedAngle = Mathf.Clamp(targetAngle, -weaponData.attackAngle / 2f, weaponData.attackAngle / 2f);

            Quaternion desiredLocalRotation = Quaternion.Euler(0, clampedAngle, 0);
            turretTransform.localRotation = Quaternion.Slerp(turretTransform.localRotation, desiredLocalRotation, turretRotationSpeed * Time.deltaTime);
        }
    }

    private void Fire()
    {
        currentAmmo--;

        Vector3 baseFireDirection = turretTransform.forward;

        if (IsControlledByPlayer)
        {
            // ★최적화: GC Alloc 발생을 막기 위해 NonAlloc 사용
            int hitCount = Physics.SphereCastNonAlloc(firePoint.position, 1f, turretTransform.forward, hitResults, weaponData.lockOnRadius, enemyLayer);

            Transform closestEnemy = null;
            float minDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                if (hitResults[i].transform.CompareTag("Enemy"))
                {
                    float distance = Vector3.Distance(firePoint.position, hitResults[i].transform.position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestEnemy = hitResults[i].transform;
                    }
                }
            }

            if (closestEnemy != null)
            {
                baseFireDirection = (closestEnemy.position - firePoint.position).normalized;
            }
        }
        else
        {
            if (currentTarget != null)
            {
                baseFireDirection = (currentTarget.position - firePoint.position).normalized;
            }
        }

        for (int i = 0; i < weaponData.projectilesPerShot; i++)
        {
            GameObject projectile = GetPooledProjectile();
            if (projectile != null)
            {
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
            if (!proj.activeInHierarchy) return proj;
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
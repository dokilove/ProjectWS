using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshAgent))]
public class AdvancedVehicleControllerPlus : MonoBehaviour, IVehicle
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

    // --- ★ 추가된 피칭(Pitching) 설정 ---
    [Header("Body Tilt Settings (Visual)")]
    [SerializeField] private Transform modelRoot;
    [SerializeField] private float pitchSensitivity = 0.3f; // 가속/감속 민감도 (앞뒤)
    [SerializeField] private float rollSensitivity = 0.15f;  // 회전 민감도 (좌우)
    [SerializeField] private float tiltSmoothTime = 0.15f;  // 복원 속도

    // 상태 저장용 변수
    private float currentPitch;
    private float currentRoll;
    private float pitchVelocity;
    private float rollVelocity;
    private Vector3 lastVelocity;
    private float lastEulerY; // 회전량(Yaw) 측정을 위한 이전 프레임 Y각도
    // -----------------------------------

    private float nextFireTime = 0f;
    private List<GameObject> projectilePool = new List<GameObject>();
    private int poolSize = 20;

    // --- Targeting Fields ---
    private Transform currentTarget; // For AI only
    private List<Transform> potentialTargets = new List<Transform>(); // For AI only
    private RaycastHit[] hitResults = new RaycastHit[10]; // GC 최적화를 위한 배열 (Player용)
    private Collider[] aiTargetColliders = new Collider[20]; // GC 최적화를 위한 배열 (AI용)

    private Rigidbody rb;
    private NavMeshAgent agent;
    private InputSystem_Actions playerActions;
    private Vector2 moveInput;
    private Vector2 lookInput; // For Player Control
    private Vector2 mousePositionInput; // For mouse aiming
    private bool isFireHeld = false;
    private bool isNeutralTurning = false; // NeutralTurn 입력 상태 겸용
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
    private System.Action<InputAction.CallbackContext> onNeutralTurnStarted;
    private System.Action<InputAction.CallbackContext> onNeutralTurnCanceled;
    private System.Action<InputAction.CallbackContext> onReloadPerformed;

    // --- Ammo & Reloading ---
    private int currentAmmo;
    private bool isReloading = false;
    private Coroutine reloadCoroutine; // 코루틴 누수 방지용 참조

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
        onNeutralTurnStarted = ctx => { OnNeutralTurnStarted(ctx); if (ctx.control.device is Gamepad) lastUsedInputDevice = InputDeviceType.Gamepad; else lastUsedInputDevice = InputDeviceType.MouseKeyboard; };
        onNeutralTurnCanceled = ctx => OnNeutralTurnCanceled(ctx);
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
        playerActions.Vehicle.NeutralTurn.started += onNeutralTurnStarted;
        playerActions.Vehicle.NeutralTurn.canceled += onNeutralTurnCanceled;
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
            playerActions.Vehicle.NeutralTurn.started -= onNeutralTurnStarted;
            playerActions.Vehicle.NeutralTurn.canceled -= onNeutralTurnCanceled;
            playerActions.Vehicle.Reload.performed -= onReloadPerformed;
        }

        // 코루틴 및 입력 상태 초기화
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
            isReloading = false;
        }

        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        isNeutralTurning = false;
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

            // AI의 길찾기 위치를 실제 물리(Rigidbody) 위치로 계속 동기화
            if (agent.enabled)
            {
                agent.nextPosition = transform.position;
            }
        }

        // ★ 차체 앞뒤/좌우 기울임 시각 효과 업데이트
        HandleBodyTilt();
    }

    private void FixedUpdate()
    {
        HandleVehiclePhysics();
    }

    private void HandleVehiclePhysics()
    {
        Vector3 targetDirection = Vector3.zero;
        float inputMagnitude = 0f;

        if (IsControlledByPlayer)
        {
            // 1. 카메라 기준 이동 방향(스틱 입력) 먼저 계산
            Vector3 cameraRight = Camera.main.transform.right;
            Vector3 cameraRightFlat = new Vector3(cameraRight.x, 0, cameraRight.z).normalized;
            Vector3 cameraForwardFlat = Vector3.Cross(Vector3.up, cameraRightFlat);

            Vector3 moveForward = -cameraForwardFlat;
            Vector3 moveVector = (moveForward * moveInput.y + cameraRightFlat * moveInput.x);

            targetDirection = moveVector.normalized;
            inputMagnitude = Mathf.Clamp01(moveVector.magnitude);

            // 2. [제자리 회전 모드] NeutralTurn 입력을 누르고 있을 때
            if (isNeutralTurning)
            {
                // 속도를 빠르게 줄임
                currentSpeed = Mathf.Lerp(currentSpeed, 0, deceleration * 2f * Time.fixedDeltaTime);
                rb.linearVelocity = transform.forward * currentSpeed;

                // 왼쪽 스틱 입력(targetDirection)을 회전으로 사용. 조준(오른쪽 스틱)과는 별개로 작동.
                if (targetDirection.sqrMagnitude > 0.1f) // 스틱이 중앙이 아닐 때만
                {
                    Quaternion targetRot = Quaternion.LookRotation(targetDirection);
                    rb.rotation = Quaternion.Slerp(rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
                }
                
                rb.angularVelocity = Vector3.zero;
                return; // 제자리 회전 모드에서는 아래의 일반 주행 로직을 실행하지 않음
            }
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

        // --- ★ 통합된 Arc 물리 및 스마트 후진 적용 로직 ---
        float finalSpeedTarget = moveSpeed * inputMagnitude; // 기본 목표 속도 (전진)
        Vector3 finalSteerDirection = targetDirection;       // 기본 조향 방향 (입력 방향)

        // [스마트 후진/전진 모드] 스틱 입력이 있을 때 항상 적용
        if (IsControlledByPlayer && targetDirection.sqrMagnitude > 0.01f)
        {
            float dot = Vector3.Dot(transform.forward, targetDirection);

            // 입력 방향이 차체 기준 뒤쪽(-0.1f 이하)일 경우 후진 모드로 전환
            if (dot < -0.1f)
            {
                finalSpeedTarget = -moveSpeed * inputMagnitude; // 목표 속도를 음수(후진)로 뒤집음
                finalSteerDirection = -targetDirection;         // 엉덩이가 타겟을 향하게 하려면, 앞부분은 타겟의 반대를 봐야 함
            }
        }

        // [조향 및 가속 적용] (스마트 후진이든 일반 주행이든 동일한 Arc 로직 사용)
        if (targetDirection.sqrMagnitude > 0.01f)
        {
            // finalSteerDirection으로 부드럽게 회전
            Quaternion targetRotation = Quaternion.LookRotation(finalSteerDirection);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);

            // finalSpeedTarget으로 가속 (후진일 경우 자연스럽게 마이너스로 Lerp됨)
            currentSpeed = Mathf.Lerp(currentSpeed, finalSpeedTarget, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            // [감속] 입력이 없으면 서서히 정지
            currentSpeed = Mathf.Lerp(currentSpeed, 0, deceleration * Time.fixedDeltaTime);
        }

        // [이동] 계산된 속도로 차체 앞방향(forward) 이동
        rb.linearVelocity = transform.forward * currentSpeed;

        // 회전 관성 초기화
        rb.angularVelocity = Vector3.zero;
    }

    private void HandleBodyTilt()
    {
        if (modelRoot == null) return;
        if (Time.deltaTime > 0)
        {
            // --- 1. 앞뒤 기울임 (Pitch: 가속/감속) ---
            Vector3 currentMoveVelocity = rb.linearVelocity;
            Vector3 localVelocity = transform.InverseTransformDirection(currentMoveVelocity);
            Vector3 localLastVelocity = transform.InverseTransformDirection(lastVelocity);

            float pitchAcceleration = (localVelocity.z - localLastVelocity.z) / Time.deltaTime;
            float targetPitch = -pitchAcceleration * pitchSensitivity;

            // --- 2. 좌우 기울임 (Roll: 회전 원심력) ---
            float currentEulerY = transform.eulerAngles.y;
            // 이전 프레임 대비 현재 얼마나 회전했는지(회전 속도) 계산
            float yawRate = Mathf.DeltaAngle(lastEulerY, currentEulerY) / Time.deltaTime;

            // 우회전(yawRate > 0) 시 차체는 왼쪽으로 쏠림(+Z 회전)
            float targetRoll = yawRate * rollSensitivity;

            // [추가 구현] NeutralTurn을 밟고 있을 때는 회전 쏠림 효과를 1.5배 극대화 (드리프트 느낌)
            if (isNeutralTurning)
            {
                targetRoll *= 1.5f;
            }

            // --- 3. 제한 및 부드러운 적용 ---
            targetPitch = Mathf.Clamp(targetPitch, -15f, 15f);
            targetRoll = Mathf.Clamp(targetRoll, -20f, 20f); // 좌우는 20도까지 허용

            currentPitch = Mathf.SmoothDampAngle(currentPitch, targetPitch, ref pitchVelocity, tiltSmoothTime);
            currentRoll = Mathf.SmoothDampAngle(currentRoll, targetRoll, ref rollVelocity, tiltSmoothTime);

            // 최종적으로 X축(앞뒤)과 Z축(좌우)에 회전 적용
            modelRoot.localRotation = Quaternion.Euler(currentPitch, 0, currentRoll);

            // --- 4. 다음 프레임을 위한 데이터 저장 ---
            lastVelocity = currentMoveVelocity;
            lastEulerY = currentEulerY;
        }
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
            reloadCoroutine = StartCoroutine(Reload());
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
                agent.enabled = false;
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
            reloadCoroutine = StartCoroutine(Reload());
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

        // 입력이 있는 프레임에만 포탑 회전 로직을 실행
        if (calculatedAimDirection.sqrMagnitude > 0.01f)
        {
            currentAimDirection = calculatedAimDirection; // 마지막 조준 방향 저장

            Vector3 localTargetDir = transform.InverseTransformDirection(currentAimDirection);
            float targetAngle = Mathf.Atan2(localTargetDir.x, localTargetDir.z) * Mathf.Rad2Deg;
            float clampedAngle = Mathf.Clamp(targetAngle, -weaponData.attackAngle / 2f, weaponData.attackAngle / 2f);

            Quaternion desiredLocalRotation = Quaternion.Euler(0, clampedAngle, 0);
            turretTransform.localRotation = Quaternion.Slerp(turretTransform.localRotation, desiredLocalRotation, turretRotationSpeed * Time.deltaTime);
        }
        // 입력이 없으면 아무것도 하지 않음 -> 포탑은 현재의 localRotation을 유지한 채 부모를 따라감

        lookInput = Vector2.zero;
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
        reloadCoroutine = null;
    }

    private void UpdateAndSelectTargetAI()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, weaponData.lockOnRadius, aiTargetColliders, enemyLayer);

        potentialTargets.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            Transform target = aiTargetColliders[i].transform;
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
            reloadCoroutine = StartCoroutine(Reload());
        }
    }

    private void OnFireHoldStarted(InputAction.CallbackContext context) { isFireHeld = true; }
    private void OnFireHoldCanceled(InputAction.CallbackContext context) { isFireHeld = false; }

    private void OnNeutralTurnStarted(InputAction.CallbackContext context)
    {
        isNeutralTurning = true;
        // 제자리 회전 시 조준 방향을 초기화하지 않도록 변경
    }
    private void OnNeutralTurnCanceled(InputAction.CallbackContext context) { isNeutralTurning = false; }

    private void OnReload(InputAction.CallbackContext context)
    {
        if (IsControlledByPlayer && !isReloading && currentAmmo < weaponData.magazineSize)
        {
            reloadCoroutine = StartCoroutine(Reload());
        }
    }

    private void RotateTurretAI(Transform target)
    {
        if (turretTransform == null || weaponData == null) return;

        if (target != null)
        {
            Vector3 direction = target.position - turretTransform.position;
            direction.y = 0;

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
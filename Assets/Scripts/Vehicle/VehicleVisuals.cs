using UnityEngine;

public class VehicleVisuals : MonoBehaviour
{
    [Header("Visual Components")]
    [SerializeField] private LineRenderer radiusVisualizer;
    [SerializeField] private FieldOfViewMesh attackRangeVisualizer;
    [SerializeField] private FieldOfViewMesh spreadAngleVisualizer;
    [SerializeField] private LineRenderer targetLineRenderer;
    [SerializeField] private Color defaultTargetColor = Color.cyan;

    [Header("Body Tilt Settings")]
    [SerializeField] private Transform modelRoot;
    [SerializeField] private float pitchSensitivity = 0.3f;
    [SerializeField] private float rollSensitivity = 0.15f;
    [SerializeField] private float tiltSmoothTime = 0.15f;

    [Header("Neutral Turn Shake Settings")]
    [SerializeField] private float shakeIntensity = 3.0f;
    [SerializeField] private float shakeSpeed = 45f;

    [Header("Run-Over Bump Settings (적 밟기 덜컹거림 연출)")]
    [Tooltip("적을 밟았을 때 위로 튀어오르는 높이 (Y축 오프셋)")]
    [SerializeField] private float bumpHeight = 0.18f;

    [Tooltip("적을 밟았을 때 앞뒤로 흔들리는 피치 각도")]
    [SerializeField] private float bumpPitchAngle = 6.0f;

    [Tooltip("적을 밟았을 때 좌우로 흔들리는 롤 각도")]
    [SerializeField] private float bumpRollAngle = 4.5f;

    [Tooltip("서스펜션 복원 스프링 강도 (높을수록 빠르게 덜컹거림)")]
    [SerializeField] private float suspensionSpring = 160f;

    [Tooltip("서스펜션 감쇠 계수 (높을수록 출렁거림이 빠르게 멈춤)")]
    [SerializeField] private float suspensionDamping = 14f;

    // --- Dependencies ---
    private Vehicle _vehicle;
    private Rigidbody rb;

    // --- State for Tilt/Shake ---
    private float currentPitch;
    private float currentRoll;
    private float pitchVelocity;
    private float rollVelocity;
    private Vector3 lastVelocity;
    private float lastEulerY;

    // --- State for Run-Over Bump ---
    private Vector3 initialModelLocalPos;
    private float bumpOffsetY = 0f;
    private float bumpVelocityY = 0f;
    private float bumpOffsetPitch = 0f;
    private float bumpVelocityPitch = 0f;
    private float bumpOffsetRoll = 0f;
    private float bumpVelocityRoll = 0f;

    public void Init(Vehicle vehicle)
    {
        _vehicle = vehicle;
        rb = GetComponent<Rigidbody>(); // Assumes Rigidbody is on the same root object
        if (modelRoot != null && initialModelLocalPos == Vector3.zero)
        {
            initialModelLocalPos = modelRoot.localPosition;
        }
    }

    private void Awake()
    {
        if (modelRoot != null && initialModelLocalPos == Vector3.zero)
        {
            initialModelLocalPos = modelRoot.localPosition;
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
        if (_vehicle == null) return;
        var weaponSystem = _vehicle.PlayerWeaponSystem != null ? _vehicle.PlayerWeaponSystem : _vehicle.VehicleWeaponSystem;
        if (weaponSystem == null) return;
        var weaponData = weaponSystem.WeaponData;
        if (weaponData == null) return;

        UpdateRadiusVisualizer(weaponData.lockOnRadius);

        if (attackRangeVisualizer != null)
        {
            float angle = weaponData.vehicleMountedAttackAngle > 0 ? weaponData.vehicleMountedAttackAngle : weaponData.attackAngle;
            attackRangeVisualizer.GenerateMesh(angle, weaponData.lockOnRadius);
            attackRangeVisualizer.SetColor(new Color(0f, 1f, 0.5f, 0.15f));
            attackRangeVisualizer.SetActive(false);
        }

        if (spreadAngleVisualizer != null)
        {
            if (weaponData.spreadAngle > 0)
            {
                spreadAngleVisualizer.GenerateMesh(weaponData.spreadAngle, weaponData.lockOnRadius);
                spreadAngleVisualizer.SetColor(new Color(1f, 0.5f, 0f, 0.15f));
                spreadAngleVisualizer.SetActive(true);
                spreadAngleVisualizer.transform.SetParent(weaponSystem.transform);
                spreadAngleVisualizer.transform.localPosition = Vector3.zero;
                spreadAngleVisualizer.transform.localRotation = Quaternion.identity;
            }
            else
            {
                spreadAngleVisualizer.SetActive(false);
            }
        }
    }

    private void Update()
    {
        HandleBodyTilt();
        HandleTargetLine();
        HandleAttackRangeVisualizer();
    }

    private void HandleAttackRangeVisualizer()
    {
        if (attackRangeVisualizer == null || _vehicle == null) return;

        if (!_vehicle.IsControlledByPlayer)
        {
            attackRangeVisualizer.SetActive(false);
            return;
        }

        var weaponSystem = _vehicle.PlayerWeaponSystem != null ? _vehicle.PlayerWeaponSystem : _vehicle.VehicleWeaponSystem;
        var weaponData = weaponSystem != null ? weaponSystem.WeaponData : null;

        if (weaponData == null)
        {
            attackRangeVisualizer.SetActive(false);
            return;
        }

        float angle = weaponData.vehicleMountedAttackAngle > 0 ? weaponData.vehicleMountedAttackAngle : weaponData.attackAngle;
        float radius = weaponData.lockOnRadius;

        attackRangeVisualizer.GenerateMesh(angle, radius);
        attackRangeVisualizer.SetActive(true);

        attackRangeVisualizer.transform.position = _vehicle.transform.position + Vector3.up * 0.05f;
        attackRangeVisualizer.transform.rotation = _vehicle.transform.rotation;
    }

    private void HandleTargetLine()
    {
        if (targetLineRenderer == null || _vehicle == null) return;

        if (!_vehicle.IsControlledByPlayer)
        {
            targetLineRenderer.enabled = false;
            return;
        }

        var weaponSystem = _vehicle.PlayerWeaponSystem != null ? _vehicle.PlayerWeaponSystem : _vehicle.VehicleWeaponSystem;
        if (weaponSystem == null || weaponSystem.FirePoint == null || weaponSystem.TurretTransform == null || weaponSystem.WeaponData == null)
        {
            targetLineRenderer.enabled = false;
            return;
        }

        float lockOnRadius = weaponSystem.WeaponData != null ? weaponSystem.WeaponData.lockOnRadius : 15f;

        targetLineRenderer.enabled = true;
        targetLineRenderer.startColor = defaultTargetColor;
        targetLineRenderer.endColor = defaultTargetColor;
        targetLineRenderer.SetPosition(0, weaponSystem.FirePoint.position);
        targetLineRenderer.SetPosition(1, weaponSystem.FirePoint.position + weaponSystem.TurretTransform.forward * lockOnRadius);
    }

    private void HandleBodyTilt()
    {
        if (modelRoot == null || rb == null || Time.deltaTime <= 0) return;

        Vector3 currentMoveVelocity = rb.linearVelocity;
        Vector3 localVelocity = transform.InverseTransformDirection(currentMoveVelocity);
        Vector3 localLastVelocity = transform.InverseTransformDirection(lastVelocity);

        float pitchAcceleration = (localVelocity.z - localLastVelocity.z) / Time.deltaTime;
        float targetPitch = -pitchAcceleration * pitchSensitivity;

        float currentEulerY = transform.eulerAngles.y;
        float yawRate = Mathf.DeltaAngle(lastEulerY, currentEulerY) / Time.deltaTime;
        float targetRoll = yawRate * rollSensitivity;

        float currentShake = 0f;
        bool isNeutralTurning = _vehicle.VehicleInput.IsNeutralTurning;
        if (isNeutralTurning)
        {
            targetRoll = 0f;
            if (Mathf.Abs(yawRate) > 0.1f)
            {
                currentShake = Mathf.Sin(Time.time * shakeSpeed) * shakeIntensity;
            }
        }

        targetPitch = Mathf.Clamp(targetPitch, -15f, 15f);
        targetRoll = Mathf.Clamp(targetRoll, -20f, 20f);

        currentPitch = Mathf.SmoothDampAngle(currentPitch, targetPitch, ref pitchVelocity, tiltSmoothTime);
        currentRoll = Mathf.SmoothDampAngle(currentRoll, targetRoll, ref rollVelocity, tiltSmoothTime);

        // 덜컹거림(Bump) 물리 스프링 시뮬레이션
        UpdateBumpSpring(Time.deltaTime);

        modelRoot.localPosition = initialModelLocalPos + new Vector3(0f, bumpOffsetY, 0f);
        modelRoot.localRotation = Quaternion.Euler(currentPitch + bumpOffsetPitch, 0f, currentRoll + currentShake + bumpOffsetRoll);

        lastVelocity = currentMoveVelocity;
        lastEulerY = currentEulerY;
    }

    /// <summary>
    /// 작은 적을 밟고 지나갈 때 차량 서스펜션이 덜컹거리는 연출을 발동합니다.
    /// </summary>
    /// <param name="hitPoint">충돌 위치 (좌/우 롤링 계산용)</param>
    /// <param name="bumperType">전방/후방 범퍼 구분 (앞뒤 피칭 계산용)</param>
    public void TriggerCrushBump(Vector3 hitPoint, BumperType bumperType)
    {
        float currentSpeed = (_vehicle != null && _vehicle.VehicleMove != null) ? _vehicle.VehicleMove.CurrentSpeed : 0f;
        float speedFactor = Mathf.Clamp(Mathf.Abs(currentSpeed) / 10f, 0.8f, 1.4f);

        // 1. 수직 튀어오름 (Vertical Jump)
        bumpVelocityY += (bumpHeight * 18f) * speedFactor;
        bumpOffsetY += bumpHeight * 0.4f * speedFactor;

        // 2. 피치 충격 (전방 범퍼는 노즈업, 후방 범퍼는 노즈다운)
        float pitchDir = (bumperType == BumperType.Front) ? -1f : 1f;
        bumpVelocityPitch += (pitchDir * bumpPitchAngle * 12f) * speedFactor;

        // 3. 롤 충격 (충돌한 지점이 위로 들리는 방향으로 롤링)
        Vector3 localHit = transform.InverseTransformPoint(hitPoint);
        float rollDir = (localHit.x >= 0f) ? -1f : 1f; // 우측 충돌 시 좌측으로 기울어짐
        bumpVelocityRoll += (rollDir * bumpRollAngle * 12f) * speedFactor;
    }

    private void UpdateBumpSpring(float dt)
    {
        if (dt <= 0f) return;

        // Y축 수직 스프링
        float forceY = -suspensionSpring * bumpOffsetY - suspensionDamping * bumpVelocityY;
        bumpVelocityY += forceY * dt;
        bumpOffsetY += bumpVelocityY * dt;

        // 피치(앞뒤) 스프링
        float forcePitch = -suspensionSpring * bumpOffsetPitch - suspensionDamping * bumpVelocityPitch;
        bumpVelocityPitch += forcePitch * dt;
        bumpOffsetPitch += bumpVelocityPitch * dt;

        // 롤(좌우) 스프링
        float forceRoll = -suspensionSpring * bumpOffsetRoll - suspensionDamping * bumpVelocityRoll;
        bumpVelocityRoll += forceRoll * dt;
        bumpOffsetRoll += bumpVelocityRoll * dt;

        // 한계값 클램핑 (과도한 튕김 방지)
        bumpOffsetY = Mathf.Clamp(bumpOffsetY, -0.08f, bumpHeight * 1.5f);
        bumpVelocityY = Mathf.Clamp(bumpVelocityY, -15f, 15f);
        bumpOffsetPitch = Mathf.Clamp(bumpOffsetPitch, -15f, 15f);
        bumpOffsetRoll = Mathf.Clamp(bumpOffsetRoll, -15f, 15f);

        // 미세 진동 정지 (Sleep Check)
        if (Mathf.Abs(bumpOffsetY) < 0.001f && Mathf.Abs(bumpVelocityY) < 0.01f)
        {
            bumpOffsetY = 0f;
            bumpVelocityY = 0f;
        }
        if (Mathf.Abs(bumpOffsetPitch) < 0.01f && Mathf.Abs(bumpVelocityPitch) < 0.05f)
        {
            bumpOffsetPitch = 0f;
            bumpVelocityPitch = 0f;
        }
        if (Mathf.Abs(bumpOffsetRoll) < 0.01f && Mathf.Abs(bumpVelocityRoll) < 0.05f)
        {
            bumpOffsetRoll = 0f;
            bumpVelocityRoll = 0f;
        }
    }

    public void UpdateRadiusVisualizer(float radius)
    {
        if (radiusVisualizer == null) return;
        int segments = 36;
        radiusVisualizer.positionCount = segments + 1;
        radiusVisualizer.loop = true;
        radiusVisualizer.useWorldSpace = false;

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
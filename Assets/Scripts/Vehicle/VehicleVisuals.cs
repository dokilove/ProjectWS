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

    public void Init(Vehicle vehicle)
    {
        _vehicle = vehicle;
        rb = GetComponent<Rigidbody>(); // Assumes Rigidbody is on the same root object
    }

    private void Awake()
    {
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

        modelRoot.localRotation = Quaternion.Euler(currentPitch, 0, currentRoll + currentShake);

        lastVelocity = currentMoveVelocity;
        lastEulerY = currentEulerY;
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
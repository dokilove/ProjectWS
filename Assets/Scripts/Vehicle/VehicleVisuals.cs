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
        var weaponData = _vehicle.VehicleWeaponSystem.WeaponData;
        if (weaponData == null) return;

        UpdateRadiusVisualizer(weaponData.lockOnRadius);

        if (attackRangeVisualizer != null)
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
                // This assumes the weapon system is on a child object that represents the turret
                spreadAngleVisualizer.transform.SetParent(_vehicle.VehicleWeaponSystem.transform);
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
    }

    private void HandleTargetLine()
    {
        if (targetLineRenderer == null) return;
        var weaponSystem = _vehicle.VehicleWeaponSystem;
        if (weaponSystem == null || weaponSystem.FirePoint == null || weaponSystem.TurretTransform == null) return;


        if (_vehicle.IsControlledByPlayer)
        {
            targetLineRenderer.enabled = true;
            targetLineRenderer.startColor = defaultTargetColor;
            targetLineRenderer.endColor = defaultTargetColor;
            targetLineRenderer.SetPosition(0, weaponSystem.FirePoint.position);
            targetLineRenderer.SetPosition(1, weaponSystem.FirePoint.position + weaponSystem.TurretTransform.forward * weaponSystem.WeaponData.lockOnRadius);
        }
        else // AI
        {
            var ai = _vehicle.VehicleAI;
            if (ai != null && ai.CurrentTarget != null)
            {
                targetLineRenderer.enabled = true;
                targetLineRenderer.SetPosition(0, weaponSystem.FirePoint.position);
                targetLineRenderer.SetPosition(1, ai.CurrentTarget.position);
            }
            else
            {
                targetLineRenderer.enabled = false;
            }
        }
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
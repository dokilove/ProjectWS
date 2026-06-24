using UnityEngine;
using System.Collections;

public class UnitVisuals : MonoBehaviour
{
    [Header("Weapon Visualizers")]
    [SerializeField] private LineRenderer radiusVisualizer;
    [SerializeField] private FieldOfViewMesh attackRangeVisualizer;
    [SerializeField] private FieldOfViewMesh spreadAngleVisualizer;
    [SerializeField] private LineRenderer targetLineRenderer;
    [SerializeField] private Color aimLineColor = Color.yellow;

    [Header("Melee Visualizers")]
    [SerializeField] private FieldOfViewMesh meleeRangeVisualizer;

    [Header("VFX")]
    [SerializeField] private TrailRenderer evadeTrailRenderer;

    // --- Dependencies ---
    private Unit _unit;

    public void Init(Unit unit)
    {
        _unit = unit;
    }

    private void Awake()
    {
        if (radiusVisualizer != null)
        {
            radiusVisualizer.startWidth = 0.1f;
            radiusVisualizer.endWidth = 0.1f;
            radiusVisualizer.enabled = false; // Set to false initially
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
        }
        if (meleeRangeVisualizer != null)
        {
            meleeRangeVisualizer.SetActive(false);
        }
        if (spreadAngleVisualizer != null)
        {
            spreadAngleVisualizer.SetActive(false); // Ensure it's off by default
        }
    }

    private void Start()
    {
        // Initial setup for evade trail
        if (evadeTrailRenderer != null && _unit.UnitMove.EvadeData != null)
        {
            evadeTrailRenderer.time = _unit.UnitMove.EvadeData.dodgeDuration + _unit.UnitMove.EvadeData.trailOffset;
        }
    }

    // --- Attack Mode Callbacks ---
    public void OnEnterRangedMode()
    {
        var weaponData = _unit.UnitWeaponSystem.WeaponData;
        if (weaponData != null)
        {
            UpdateRadiusVisualizerBasedOnMode(); // Update radius visualizer for ranged
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
                    spreadAngleVisualizer.transform.SetParent(_unit.UnitWeaponSystem.TurretTransform); // Use TurretTransform for correct rotation
                    spreadAngleVisualizer.transform.localPosition = Vector3.zero;
                    spreadAngleVisualizer.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    spreadAngleVisualizer.SetActive(false);
                }
            }
        }
        // Ensure target line is active in ranged mode
        SetAimLineActive(true);
    }

    public void OnEnterMeleeMode()
    {
        var meleeData = _unit.UnitMeleeSystem.MeleeData;
        if (meleeData != null)
        {
            UpdateRadiusVisualizerBasedOnMode(); // Update radius visualizer for melee
        }
        else
        {
            // If no melee data, just disable the radius visualizer
            if (radiusVisualizer != null) radiusVisualizer.enabled = false;
        }

        if (spreadAngleVisualizer != null)
        {
            spreadAngleVisualizer.SetActive(false);
        }
        if (attackRangeVisualizer != null)
        {
            attackRangeVisualizer.SetActive(false);
        }
        // Ensure target line is inactive in melee mode
        SetAimLineActive(false);
    }

    /// <summary>
    /// Updates the main lock-on radius visualizer based on the current attack mode.
    /// </summary>
    private void UpdateRadiusVisualizerBasedOnMode()
    {
        if (radiusVisualizer == null) return;

        float currentRadius = 0f;
        if (_unit.CurrentAttackMode == AttackMode.Ranged && _unit.UnitWeaponSystem.WeaponData != null)
        {
            currentRadius = _unit.UnitWeaponSystem.WeaponData.lockOnRadius;
        }
        else if (_unit.CurrentAttackMode == AttackMode.Melee && _unit.UnitMeleeSystem.MeleeData != null)
        {
            currentRadius = _unit.UnitMeleeSystem.MeleeData.meleeLockOnRadius;
        }

        if (currentRadius > 0)
        {
            UpdateLockOnRadiusVisualizer(currentRadius);
            radiusVisualizer.enabled = true;
        }
        else
        {
            radiusVisualizer.enabled = false;
        }
    }

    private void Update()
    {
        if (_unit.IsControlledByPlayer)
        {
            // Update aim line based on current mode
            if (_unit.CurrentAttackMode == AttackMode.Ranged)
            {
                var weaponSystem = _unit.UnitWeaponSystem;
                if (weaponSystem != null && weaponSystem.WeaponData != null)
                {
                    UpdateAimLine(weaponSystem.FirePoint.position, weaponSystem.TurretTransform.forward, weaponSystem.WeaponData.lockOnRadius);
                }
            }
            else // Melee Mode
            {
                // In melee mode, aim line might not be needed or could use meleeLockOnRadius
                // For now, let's keep it simple and disable it if not explicitly needed.
                SetAimLineActive(false);
            }
        }
        else
        {
            SetAimLineActive(false);
        }
    }

    public void UpdateAimLine(Vector3 startPoint, Vector3 direction, float length)
    {
        if (targetLineRenderer != null)
        {
            targetLineRenderer.enabled = true;
            targetLineRenderer.startColor = aimLineColor;
            targetLineRenderer.endColor = aimLineColor;
            targetLineRenderer.SetPosition(0, startPoint);
            targetLineRenderer.SetPosition(1, startPoint + direction * length);
        }
    }

    public void SetAimLineActive(bool isActive)
    {
        if (targetLineRenderer != null)
        {
            targetLineRenderer.enabled = isActive;
        }
    }

    public void UpdateLockOnRadiusVisualizer(float radius)
    {
        if (radiusVisualizer == null) return;

        int segments = 36;
        radiusVisualizer.positionCount = segments + 1;
        radiusVisualizer.loop = true;
        radiusVisualizer.useWorldSpace = false; // Assuming it's a child of the unit

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

    public IEnumerator ShowMeleeVisualizer(float radius, float angle, Material overrideMaterial = null)
    {
        if (meleeRangeVisualizer == null) yield break;

        if (overrideMaterial != null)
        {
            meleeRangeVisualizer.SetMaterial(overrideMaterial);
        }

        meleeRangeVisualizer.GenerateMesh(angle, radius);
        meleeRangeVisualizer.SetActive(true);
        yield return new WaitForSeconds(0.2f);
        meleeRangeVisualizer.SetActive(false);

        if (overrideMaterial != null)
        {
            meleeRangeVisualizer.RevertMaterial();
        }
    }

    public void SetEvadeTrail(bool isActive)
    {
        if (evadeTrailRenderer == null) return;
        if (isActive)
        {
            evadeTrailRenderer.Clear();
        }
        evadeTrailRenderer.enabled = isActive;
    }
}
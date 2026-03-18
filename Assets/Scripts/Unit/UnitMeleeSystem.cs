using UnityEngine;
using System.Collections;

public class UnitMeleeSystem : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private MeleeData meleeData;
    [SerializeField] private LayerMask enemyLayerMask;
    [SerializeField] private Material chargeAttackMaterial; // Material for charge attack visual

    // --- Dependencies ---
    private Unit _unit;

    // --- State ---
    private Vector3 aimDirection;
    private int comboCounter = 0;
    private float lastMeleeTime = -1f;
    private bool isMeleeChargePrimed = false;

    public void Init(Unit unit)
    {
        _unit = unit;
    }

    private void Start()
    {
        if (meleeData == null)
        {
            Debug.LogError("MeleeData is not assigned in the inspector!", this);
        }
        aimDirection = transform.forward;
    }

    private void Update()
    {
        HandleComboTimeout();
    }

    /// <summary>
    /// Sets the aiming direction. Called by UnitInput.
    /// </summary>
    public void SetAim(Vector3 newAimDirection)
    {
        aimDirection = newAimDirection;
    }

    /// <summary>
    /// Handles the combo timeout logic.
    /// </summary>
    private void HandleComboTimeout()
    {
        if (meleeData == null) return;

        if (comboCounter > 0)
        {
            int timeIndex = comboCounter - 1;
            if (timeIndex < meleeData.comboResetTimes.Count)
            {
                if (Time.time - lastMeleeTime > meleeData.comboResetTimes[timeIndex])
                {
                    comboCounter = 0;
                }
            }
        }
    }

    /// <summary>
    /// Handles input for a standard melee combo step.
    /// </summary>
    public void HandleMeleeComboInput()
    {
        if (meleeData == null || isMeleeChargePrimed) return;

        comboCounter++;
        
        if (comboCounter > meleeData.comboDamages.Count)
        {
            comboCounter = 1;
        }

        _unit.UnitAnimator.TriggerMelee(comboCounter);

        int comboIndex = comboCounter - 1;
        StartCoroutine(_unit.UnitVisuals.ShowMeleeVisualizer(
            meleeData.comboAttackRadii[comboIndex],
            meleeData.comboAttackAngles[comboIndex]
        ));
        PerformMeleeAttack(
            meleeData.comboAttackRadii[comboIndex],
            meleeData.comboAttackAngles[comboIndex],
            meleeData.comboDamages[comboIndex]
        );

        lastMeleeTime = Time.time;

        if (comboCounter >= meleeData.comboDamages.Count)
        {
            comboCounter = 0;
        }
    }

    /// <summary>
    /// Handles the start of a charge attack.
    /// </summary>
    public void HandleMeleeChargeInput()
    {
        if (meleeData == null) return;
        isMeleeChargePrimed = true;
        comboCounter = 0; 
    }

    /// <summary>
    /// Handles the release of a charge attack.
    /// </summary>
    public void HandleMeleeChargeReleaseInput()
    {
        if (meleeData == null || !isMeleeChargePrimed) return;
        
        _unit.UnitAnimator.TriggerChargeMelee();

        StartCoroutine(_unit.UnitVisuals.ShowMeleeVisualizer(
            meleeData.chargeAttackRadius,
            meleeData.chargeAttackAngle,
            chargeAttackMaterial
        ));
        PerformMeleeAttack(
            meleeData.chargeAttackRadius,
            meleeData.chargeAttackAngle,
            meleeData.chargeAttackDamage
        );
        
        isMeleeChargePrimed = false;
        comboCounter = 0;
        lastMeleeTime = -1f; 
    }

    private void PerformMeleeAttack(float radius, float angle, float damage)
    {
        // Use the body's forward direction for melee, not the turret aim.
        Vector3 forward = transform.forward; 
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, enemyLayerMask);
        int hitCount = 0;

        foreach (Collider hit in hits)
        {
            Vector3 directionToTarget = (hit.transform.position - transform.position).normalized;
            float angleToTarget = Vector3.Angle(forward, directionToTarget);

            if (angleToTarget < angle / 2)
            {
                // Assuming enemies have a component that can take damage.
                if (hit.TryGetComponent<EnemyHealth>(out EnemyHealth enemyHealth))
                {
                    enemyHealth.TakeDamage(damage);
                    hitCount++;
                }
            }
        }
        if (hitCount > 0)
        {
            Debug.Log($"Melee attack hit {hitCount} enemies for {damage} damage.");
        }
    }
}

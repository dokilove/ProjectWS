using UnityEngine;
using System.Collections;
using System;

public class UnitMeleeSystem : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private MeleeData _meleeData; // Renamed to avoid conflict with public property

    public MeleeData MeleeData => _meleeData; // Public property to access MeleeData
    [SerializeField] private LayerMask enemyLayerMask;
    [SerializeField] private Material chargeAttackMaterial; // Material for charge attack visual

    // --- Dependencies ---
    private Unit _unit;

    // --- State ---
    private Vector3 aimDirection;
    private int comboCounter = 0;
    private float lastMeleeTime = -1f;
    private bool isMeleeChargePrimed = false;
    private float chargeStartTime = 0f;
    public Vector3 AutoAimTargetPosition { get; private set; } = Vector3.zero; // New property to store auto-aim target

    // --- Events ---
    public event Action<float> OnChargeProgressChanged;

    public void Init(Unit unit)
    {
        _unit = unit;
    }

    private void Awake()
    {
        if (_meleeData == null)
        {
            Debug.LogError("MeleeData is not assigned in the inspector!", this);
        }
        aimDirection = transform.forward;
    }

    private void Start()
    {
        // Start is now empty as initialization moved to Awake
    }

    private void Update()
    {
        HandleComboTimeout();
        HandleChargeProgress();
        if (_unit.CurrentAttackMode == AttackMode.Melee)
        {
            PerformAutoAim(); // Continuously find target in Melee mode
        }
    }

    private void HandleChargeProgress()
    {
        if (isMeleeChargePrimed)
        {
            float progress = 0f;
            if (_meleeData.chargeTimeThreshold > 0)
            {
                progress = (Time.time - chargeStartTime) / _meleeData.chargeTimeThreshold;
            }
            OnChargeProgressChanged?.Invoke(Mathf.Clamp01(progress));
        }
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
        if (_meleeData == null) return;

        if (comboCounter > 0)
        {
            int timeIndex = comboCounter - 1;
            if (timeIndex < _meleeData.comboResetTimes.Count)
            {
                if (Time.time - lastMeleeTime > _meleeData.comboResetTimes[timeIndex])
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
        if (_meleeData == null) return;

        // Auto-aim before attack
        PerformAutoAim();

        comboCounter++;
        
        if (comboCounter > _meleeData.comboDamages.Count)
        {
            comboCounter = 1;
        }

        _unit.UnitAnimator.TriggerMelee(comboCounter);

        int comboIndex = comboCounter - 1;
        StartCoroutine(_unit.UnitVisuals.ShowMeleeVisualizer(
            _meleeData.comboAttackRadii[comboIndex],
            _meleeData.comboAttackAngles[comboIndex]
        ));
        PerformMeleeAttack(
            _meleeData.comboAttackRadii[comboIndex],
            _meleeData.comboAttackAngles[comboIndex],
            _meleeData.comboDamages[comboIndex]
        );

        lastMeleeTime = Time.time;

        if (comboCounter >= _meleeData.comboDamages.Count)
        {
            comboCounter = 0;
        }
    }

    /// <summary>
    /// Handles the start of a charge attack. Records the start time.
    /// </summary>
    public void HandleMeleeChargeInput()
    {
        if (_meleeData == null) return;
        isMeleeChargePrimed = true;
        chargeStartTime = Time.time;

        // Auto-aim before charge
        PerformAutoAim();
    }

    /// <summary>
    /// Finds the closest enemy within the melee lock-on radius and rotates the player towards it.
    /// </summary>
    private void PerformAutoAim()
    {
        if (_meleeData == null || _meleeData.meleeLockOnRadius <= 0)
        {
            _unit.UnitMove.SetIsAutoAiming(false); // Ensure flag is reset if conditions not met
            AutoAimTargetPosition = Vector3.zero;
            return;
        }

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, _meleeData.meleeLockOnRadius, enemyLayerMask);
        Transform closestEnemy = null;
        float minDistance = Mathf.Infinity;

        foreach (Collider hit in hitColliders)
        {
            float distance = Vector3.Distance(transform.position, hit.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestEnemy = hit.transform;
            }
        }

        if (closestEnemy != null)
        {
            AutoAimTargetPosition = closestEnemy.position; // Store the target position
            _unit.UnitMove.SetIsAutoAiming(true); // Inform UnitMove that auto-aim is active
        }
        else
        {
            AutoAimTargetPosition = Vector3.zero; // No target, reset
            _unit.UnitMove.SetIsAutoAiming(false); // Inform UnitMove that auto-aim is not active
        }
    }

    /// <summary>
    /// Cancels any ongoing melee charge.
    /// </summary>
    public void CancelCharge()
    {
        if (isMeleeChargePrimed)
        {
            isMeleeChargePrimed = false;
            chargeStartTime = 0f;
            OnChargeProgressChanged?.Invoke(0f); // Reset UI
            AutoAimTargetPosition = Vector3.zero; // Reset auto-aim target
            Debug.Log("Melee charge canceled.");
        }
    }

    /// <summary>
    /// Handles the release of a charge attack. Performs charge or combo attack based on hold duration.
    /// </summary>
    public void HandleMeleeChargeReleaseInput()
    {
        if (_meleeData == null || !isMeleeChargePrimed)
        {
            return;
        }

        float chargeDuration = Time.time - chargeStartTime;

        if (chargeDuration >= _meleeData.chargeTimeThreshold)
        {
            // Perform Charge Attack
            _unit.UnitAnimator.TriggerChargeMelee();

            StartCoroutine(_unit.UnitVisuals.ShowMeleeVisualizer(
                _meleeData.chargeAttackRadius,
                _meleeData.chargeAttackAngle,
                chargeAttackMaterial
            ));
            PerformMeleeAttack(
                _meleeData.chargeAttackRadius,
                _meleeData.chargeAttackAngle,
                _meleeData.chargeAttackDamage
            );
            
            // After a charge attack, always reset the combo state.
            comboCounter = 0;
            lastMeleeTime = -1f; 
        }
        else
        {
            // If held for less than the threshold, treat it as a normal combo attack.
            // HandleMeleeComboInput will manage the comboCounter and lastMeleeTime itself.
            HandleMeleeComboInput();
        }
        
        // Reset charge-specific state regardless of the outcome.
        CancelCharge();
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

using UnityEngine;

// This script acts as the central coordinator for all Unit components.
// It holds references to the other components and handles the high-level logic
// of enabling/disabling the unit.
[RequireComponent(typeof(UnitInput))]
[RequireComponent(typeof(UnitMove))]
[RequireComponent(typeof(UnitAnimator))]
[RequireComponent(typeof(UnitWeaponSystem))]
[RequireComponent(typeof(UnitMeleeSystem))]
[RequireComponent(typeof(UnitVisuals))]
public class Unit : MonoBehaviour
{
    // --- Public Component References ---
    // Other systems can get references to components through this central script
    public UnitInput UnitInput { get; private set; }
    public UnitMove UnitMove { get; private set; }
    public UnitAnimator UnitAnimator { get; private set; }
    public UnitWeaponSystem UnitWeaponSystem { get; private set; }
    public UnitMeleeSystem UnitMeleeSystem { get; private set; }
    public UnitVisuals UnitVisuals { get; private set; }

    public PlayerHealthData playerHealthData;
    public string hitEffectPoolTag;
    public string guardEffectPoolTag; // New field for guard effect
    public float CurrentHealth { get; private set; }
    public bool IsInvincible { get; set; } = false; // New field for invincibility
    public bool IsDead => CurrentHealth <= 0;

    public bool IsControlledByPlayer { get; private set; } = false;

    private void Awake()
    {
        // Get all the components on this GameObject
        UnitInput = GetComponent<UnitInput>();
        UnitMove = GetComponent<UnitMove>();
        UnitAnimator = GetComponent<UnitAnimator>();
        UnitWeaponSystem = GetComponent<UnitWeaponSystem>();
        UnitMeleeSystem = GetComponent<UnitMeleeSystem>();
        UnitVisuals = GetComponent<UnitVisuals>();

        if (playerHealthData != null)
        {
            CurrentHealth = playerHealthData.maxHealth;
        }
        else
        {
            Debug.LogWarning("PlayerHealthData is not assigned to Unit. CurrentHealth will not be initialized.");
            CurrentHealth = 100f; // Default if not assigned
        }

        // Initialize components that need a reference to the coordinator
        UnitInput.Init(this);
        UnitMove.Init(this);
        UnitWeaponSystem.Init(this);
        UnitMeleeSystem.Init(this);
        UnitVisuals.Init(this);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        if (IsInvincible) return; // If invincible, do not take damage

        Debug.Log($"TakeDamage called on {gameObject.name} for {amount} damage.");

        CurrentHealth -= amount;
        CurrentHealth = Mathf.Max(CurrentHealth, 0); // Ensure health doesn't go below 0

        if (hitEffectPoolTag != null && EffectPoolManager.Instance != null)
        {
            EffectPoolManager.Instance.GetPooledObject(hitEffectPoolTag, transform.position, Quaternion.identity);
        }
        else if (hitEffectPoolTag == null)
        {
            Debug.LogWarning("hitEffectPoolTag is NOT assigned.");
        }
        else if (EffectPoolManager.Instance == null)
        {
            Debug.LogError("EffectPoolManager.Instance is NULL. Cannot get pooled object.");
        }

        if (IsDead)
        {
            Debug.Log($"{gameObject.name} has been defeated!");
            // TODO: Add death logic (e.g., disable unit, play death animation)
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;

        CurrentHealth += amount;
        CurrentHealth = Mathf.Min(CurrentHealth, playerHealthData.maxHealth); // Ensure health doesn't exceed maxHealth
    }

    public void EnableControl()
    {
        IsControlledByPlayer = true;
        this.enabled = true;
        gameObject.SetActive(true);

        // Enable input
        if (UnitInput != null)
        {
            UnitInput.EnableInput();
        }
    }

    public void DisableControl()
    {
        IsControlledByPlayer = false;
        this.enabled = false;
        gameObject.SetActive(false);

        // Disable input
        if (UnitInput != null)
        {
            UnitInput.DisableInput();
        }
    }
}
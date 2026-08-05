using UnityEngine;

// This script will act as the coordinator for all other Vehicle components.
[RequireComponent(typeof(VehicleInput))]
[RequireComponent(typeof(VehicleMove))]
[RequireComponent(typeof(VehicleVisuals))]
// Not requiring VehicleAI as it might not be on player vehicles
public class Vehicle : MonoBehaviour, IVehicle
{
    [Header("Weapon Systems")]
    [SerializeField] private VehicleWeaponSystem autoWeaponSystem;
    [SerializeField] private VehicleWeaponSystem playerWeaponSystem;

    public VehicleInput VehicleInput { get; private set; }
    public VehicleMove VehicleMove { get; private set; }
    public VehicleWeaponSystem AutoWeaponSystem => autoWeaponSystem;
    public VehicleWeaponSystem PlayerWeaponSystem => playerWeaponSystem;
    public VehicleWeaponSystem VehicleWeaponSystem => playerWeaponSystem != null ? playerWeaponSystem : (autoWeaponSystem != null ? autoWeaponSystem : GetComponentInChildren<VehicleWeaponSystem>());
    public VehicleVisuals VehicleVisuals { get; private set; }
    public VehicleAI VehicleAI { get; private set; }

    public VehicleHealthData vehicleHealthData;
    public string hitEffectPoolTag;
    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;

    public bool IsControlledByPlayer { get; private set; } = false;

    // IVehicle properties
    public int CurrentAmmo => VehicleWeaponSystem != null ? VehicleWeaponSystem.CurrentAmmo : 0;
    public WeaponData WeaponData => VehicleWeaponSystem != null ? VehicleWeaponSystem.WeaponData : null;
    public bool IsReloading => VehicleWeaponSystem != null ? VehicleWeaponSystem.IsReloading : false;

    private void Awake()
    {
        VehicleInput = GetComponent<VehicleInput>();
        VehicleMove = GetComponent<VehicleMove>();
        VehicleVisuals = GetComponent<VehicleVisuals>();
        VehicleAI = GetComponent<VehicleAI>();

        var weaponSystems = GetComponentsInChildren<VehicleWeaponSystem>();
        foreach (var ws in weaponSystems)
        {
            if (ws.IsAutoWeapon)
            {
                if (autoWeaponSystem == null) autoWeaponSystem = ws;
            }
            else
            {
                if (playerWeaponSystem == null) playerWeaponSystem = ws;
            }
        }

        if (autoWeaponSystem == null && playerWeaponSystem == null && weaponSystems.Length > 0)
        {
            if (weaponSystems[0].IsAutoWeapon) autoWeaponSystem = weaponSystems[0];
            else playerWeaponSystem = weaponSystems[0];
        }

        if (vehicleHealthData == null)
        {
            Debug.LogWarning("VehicleHealthData is not assigned to Vehicle. CurrentHealth will not be initialized.");
            CurrentHealth = 200f;
        }

        if (VehicleInput != null) VehicleInput.Init(this);
        if (VehicleMove != null) VehicleMove.Init(this);
        if (autoWeaponSystem != null) autoWeaponSystem.Init(this);
        if (playerWeaponSystem != null) playerWeaponSystem.Init(this);
        if (VehicleVisuals != null) VehicleVisuals.Init(this);
        if (VehicleAI != null) VehicleAI.Init(this);
    }

    private void Start()
    {
        if (vehicleHealthData != null)
        {
            CurrentHealth = vehicleHealthData.maxHealth;
        }

        UpdatePlayerTurretVisibility();
    }

    public void UpdatePlayerTurretVisibility()
    {
        var pWeapon = playerWeaponSystem != null ? playerWeaponSystem : (VehicleWeaponSystem != null && !VehicleWeaponSystem.IsAutoWeapon ? VehicleWeaponSystem : null);
        if (pWeapon != null)
        {
            Transform tTransform = pWeapon.TurretTransform != null ? pWeapon.TurretTransform : pWeapon.transform;
            if (tTransform != null)
            {
                tTransform.gameObject.SetActive(IsControlledByPlayer);
            }
        }
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

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
            Debug.Log($"{gameObject.name} has been destroyed!");
            // TODO: Add destruction logic (e.g., disable vehicle, play explosion effect)
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;

        CurrentHealth += amount;
        CurrentHealth = Mathf.Min(CurrentHealth, vehicleHealthData.maxHealth); // Ensure health doesn't exceed maxHealth
    }

    public void EnableControl()
    {
        IsControlledByPlayer = true;
        if (VehicleInput != null) VehicleInput.EnableInput();
        if (VehicleAI != null) VehicleAI.enabled = false;
        if (VehicleMove != null) VehicleMove.EnableControl();

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        UpdatePlayerTurretVisibility();
    }

    public void DisableControl()
    {
        IsControlledByPlayer = false;
        if (VehicleInput != null) VehicleInput.DisableInput();
        if (VehicleMove != null) VehicleMove.DisableControl();

        Rigidbody rb = GetComponent<Rigidbody>(); // Get Rigidbody reference here

        // If AI exists, enable it and ensure Rigidbody is dynamic for AI control
        if (VehicleAI != null)
        {
            VehicleAI.enabled = true;
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
        }
        else // If no AI, then the vehicle should be frozen (kinematic)
        {
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }
        }

        UpdatePlayerTurretVisibility();
    }
}

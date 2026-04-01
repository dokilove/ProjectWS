using UnityEngine;

// This script will act as the coordinator for all other Vehicle components.
[RequireComponent(typeof(VehicleInput))]
[RequireComponent(typeof(VehicleMove))]
[RequireComponent(typeof(VehicleWeaponSystem))]
[RequireComponent(typeof(VehicleVisuals))]
// Not requiring VehicleAI as it might not be on player vehicles
public class Vehicle : MonoBehaviour, IVehicle
{
    public VehicleInput VehicleInput { get; private set; }
    public VehicleMove VehicleMove { get; private set; }
    public VehicleWeaponSystem VehicleWeaponSystem { get; private set; }
    public VehicleVisuals VehicleVisuals { get; private set; }
    public VehicleAI VehicleAI { get; private set; }

    public bool IsControlledByPlayer { get; private set; } = false;

    // IVehicle properties
    public int CurrentAmmo => VehicleWeaponSystem != null ? VehicleWeaponSystem.CurrentAmmo : 0;
    public WeaponData WeaponData => VehicleWeaponSystem != null ? VehicleWeaponSystem.WeaponData : null;
    public bool IsReloading => VehicleWeaponSystem != null ? VehicleWeaponSystem.IsReloading : false;

    private void Awake()
    {
        VehicleInput = GetComponent<VehicleInput>();
        VehicleMove = GetComponent<VehicleMove>();
        VehicleWeaponSystem = GetComponent<VehicleWeaponSystem>();
        VehicleVisuals = GetComponent<VehicleVisuals>();
        VehicleAI = GetComponent<VehicleAI>(); // This can be null

        // Initialize components
        if(VehicleInput != null) VehicleInput.Init(this);
        if(VehicleMove != null) VehicleMove.Init(this);
        if(VehicleWeaponSystem != null) VehicleWeaponSystem.Init(this);
        if(VehicleVisuals != null) VehicleVisuals.Init(this);
        if(VehicleAI != null) VehicleAI.Init(this);
        // Other inits will go here
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
    }
}

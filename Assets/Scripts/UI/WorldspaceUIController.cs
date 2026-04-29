using UnityEngine;
using UnityEngine.UIElements;

public class WorldspaceUIController : MonoBehaviour
{
    // Health UI Elements
    private ProgressBar healthProgressBar;
    private Label healthLabel;

    private Label ammoLabel;
    private Label reloadStatusLabel;

    private Vehicle vehicle; // Use the new coordinator
    private VehicleHealthData vehicleHealthData;

    private Camera mainCamera;

    private void Awake()
    {
        vehicle = GetComponentInParent<Vehicle>();
        mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        // Query Health UI Elements
        healthProgressBar = root.Q<ProgressBar>("health-progressbar");
        healthLabel = root.Q<Label>("health-label");

        if (healthProgressBar == null) Debug.LogError("health-progressbar not found in UXML for WorldspaceUIController.");
        if (healthLabel == null) Debug.LogError("health-label not found in UXML for WorldspaceUIController.");

        ammoLabel = root.Q<Label>("ammo-label");
        reloadStatusLabel = root.Q<Label>("reload-status");

        if (vehicle != null)
        {
            vehicleHealthData = vehicle.vehicleHealthData;
        }

        if (vehicleHealthData == null)
        {
            Debug.LogError("VehicleHealthData not found or assigned to vehicle. Health UI will not function correctly.");
        }
    }

    private void LateUpdate()
    {
        if (mainCamera == null) return;

        // Make the UI always face the camera's forward direction
        transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);

        if (vehicle != null)
        {
            UpdateVehicleUI();
        }
        else
        {
            // Hide UI if no controller is active
            if (ammoLabel != null) ammoLabel.style.display = DisplayStyle.None;
            if (reloadStatusLabel != null) reloadStatusLabel.style.display = DisplayStyle.None;
        }
    }

    private void UpdateVehicleUI()
    {
        // --- Update Health UI ---
        if (vehicleHealthData != null && healthProgressBar != null && healthLabel != null)
        {
            healthProgressBar.style.display = DisplayStyle.Flex;
            healthLabel.style.display = DisplayStyle.Flex;

            float healthPercentage = vehicle.CurrentHealth / vehicleHealthData.maxHealth;
            healthProgressBar.value = healthPercentage;
            healthLabel.text = $"{Mathf.CeilToInt(vehicle.CurrentHealth)}/{Mathf.CeilToInt(vehicleHealthData.maxHealth)}";
        }
        else if (healthProgressBar != null && healthLabel != null)
        {
            healthProgressBar.style.display = DisplayStyle.None;
            healthLabel.style.display = DisplayStyle.None;
        }

        var weaponSystem = vehicle.VehicleWeaponSystem;
        if (weaponSystem == null || weaponSystem.WeaponData == null)
        {
            if (ammoLabel != null) ammoLabel.style.display = DisplayStyle.None;
            if (reloadStatusLabel != null) reloadStatusLabel.style.display = DisplayStyle.None;
            return;
        }

        ammoLabel.style.display = DisplayStyle.Flex;
        ammoLabel.text = $"{weaponSystem.CurrentAmmo}/{weaponSystem.WeaponData.magazineSize}";

        bool isReloading = weaponSystem.IsReloading;
        reloadStatusLabel.style.display = isReloading ? DisplayStyle.Flex : DisplayStyle.None;
    }
}

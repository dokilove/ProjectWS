using UnityEngine;
using UnityEngine.UIElements;

public class WorldspaceUIController : MonoBehaviour
{
    private Label ammoLabel;
    private Label reloadStatusLabel;

    private Vehicle vehicle; // Use the new coordinator

    private Camera mainCamera;

    private void Awake()
    {
        vehicle = GetComponentInParent<Vehicle>();
        mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        ammoLabel = root.Q<Label>("ammo-label");
        reloadStatusLabel = root.Q<Label>("reload-status");
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

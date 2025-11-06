using UnityEngine;
using UnityEngine.UIElements;

public class WorldspaceUIController : MonoBehaviour
{
    private Label ammoLabel;
    private Label reloadStatusLabel;

    private UnitController unitController;
    private VehicleController vehicleController;

    private Camera mainCamera;

    private void Awake()
    {
        // Attempt to get both controller types. Only one should be active.
        unitController = GetComponentInParent<UnitController>();
        vehicleController = GetComponentInParent<VehicleController>();
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

        if (unitController != null && unitController.IsControlledByPlayer)
        {
            UpdateUnitUI();
        }
        else if (vehicleController != null && (vehicleController.IsControlledByPlayer || !vehicleController.IsControlledByPlayer)) // Show for both player and AI vehicle
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

    private void UpdateUnitUI()
    {
        if (unitController.WeaponData == null) return;

        ammoLabel.style.display = DisplayStyle.Flex;
        ammoLabel.text = $"{unitController.CurrentAmmo}/{unitController.WeaponData.magazineSize}";

        bool isReloading = unitController.IsReloading;
        reloadStatusLabel.style.display = isReloading ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void UpdateVehicleUI()
    {
        if (vehicleController.WeaponData == null) return;

        ammoLabel.style.display = DisplayStyle.Flex;
        ammoLabel.text = $"{vehicleController.CurrentAmmo}/{vehicleController.WeaponData.magazineSize}";

        bool isReloading = vehicleController.IsReloading;
        reloadStatusLabel.style.display = isReloading ? DisplayStyle.Flex : DisplayStyle.None;
    }
}

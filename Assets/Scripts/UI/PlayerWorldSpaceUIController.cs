using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

[RequireComponent(typeof(UIDocument))]
public class PlayerWorldSpaceUIController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    // Player Unit and Data
    private UnitController playerUnit;
    private EvadeData evadeData;

    // Ammo UI Elements
    private Label ammoLabel;
    private Label reloadStatusLabel;

    // Evade UI Elements
    private VisualElement evadeChargesContainer;
    private ProgressBar evadeRechargeProgressBar;
    private List<VisualElement> chargeIcons = new List<VisualElement>();

    private Camera mainCamera;

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }
        mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        // Query Ammo UI Elements
        ammoLabel = root.Q<Label>("ammo-label");
        reloadStatusLabel = root.Q<Label>("reload-status");

        // Query Evade UI Elements
        evadeChargesContainer = root.Q<VisualElement>("evade-charges-container");
        evadeRechargeProgressBar = root.Q<ProgressBar>("evade-recharge-progressbar");

        // Find the player unit and evade data
        playerUnit = GetComponentInParent<UnitController>(); // Assuming this UI is a child of the player unit
        if (playerUnit != null)
        {
            evadeData = playerUnit.EvadeData;
        }

        if (evadeData == null)
        {
            Debug.LogError("EvadeData not found or assigned to playerUnit. Evade UI will not function correctly.");
            return;
        }

        // Initialize evade charge icons
        CreateChargeIcons();
        UpdateUI(); // Initial update
    }

    private void LateUpdate()
    {
        if (mainCamera == null) return;

        // Make the UI always face the camera's forward direction
        transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);

        if (playerUnit == null) return;

        UpdateUI();
    }

    private void UpdateUI()
    {
        // --- Update Ammo UI ---
        if (playerUnit.WeaponData != null)
        {
            ammoLabel.style.display = DisplayStyle.Flex;
            ammoLabel.text = $"{playerUnit.CurrentAmmo}/{playerUnit.WeaponData.magazineSize}";

            bool isReloading = playerUnit.IsReloading;
            reloadStatusLabel.style.display = isReloading ? DisplayStyle.Flex : DisplayStyle.None;
        }
        else
        {
            ammoLabel.style.display = DisplayStyle.None;
            reloadStatusLabel.style.display = DisplayStyle.None;
        }


        // --- Update Evade UI ---
        // Update charge icons
        for (int i = 0; i < evadeData.maxEvadeCharges; i++)
        {
            if (i < chargeIcons.Count)
            {
                if (i < playerUnit.CurrentEvadeCharges)
                {
                    chargeIcons[i].AddToClassList("available");
                }
                else
                {
                    chargeIcons[i].RemoveFromClassList("available");
                }
            }
        }

        // Update progress bar for regeneration
        if (playerUnit.CurrentEvadeCharges < evadeData.maxEvadeCharges)
        {
            evadeRechargeProgressBar.style.display = DisplayStyle.Flex;

            float timeSinceLastEvade = Time.time - playerUnit.LastEvadeTime;
            float progress = (timeSinceLastEvade % evadeData.evadeChargeRegenTime) / evadeData.evadeChargeRegenTime;
            
            // Ensure progress is always between 0 and 1
            progress = Mathf.Clamp01(progress);

            evadeRechargeProgressBar.value = progress;
            evadeRechargeProgressBar.title = $"Recharging ({Mathf.CeilToInt(evadeData.evadeChargeRegenTime - (timeSinceLastEvade % evadeData.evadeChargeRegenTime))}s)";
        }
        else
        {
            evadeRechargeProgressBar.style.display = DisplayStyle.None;
        }
    }

    private void CreateChargeIcons()
    {
        evadeChargesContainer.Clear();
        chargeIcons.Clear();

        for (int i = 0; i < evadeData.maxEvadeCharges; i++)
        {
            VisualElement icon = new VisualElement();
            icon.AddToClassList("evade-charge-icon");
            evadeChargesContainer.Add(icon);
            chargeIcons.Add(icon);
        }
    }
}
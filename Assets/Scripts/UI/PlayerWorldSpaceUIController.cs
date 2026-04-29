using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

[RequireComponent(typeof(UIDocument))]
public class PlayerWorldSpaceUIController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    // Player Unit and Data
    private Unit playerUnit;
    private EvadeData evadeData;
    private PlayerHealthData playerHealthData;

    // Health UI Elements
    private ProgressBar healthProgressBar;
    private Label healthLabel;

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

        // Query Health UI Elements
        healthProgressBar = root.Q<ProgressBar>("health-progressbar");
        healthLabel = root.Q<Label>("health-label");

        // Query Ammo UI Elements
        ammoLabel = root.Q<Label>("ammo-label");
        reloadStatusLabel = root.Q<Label>("reload-status");

        // Query Evade UI Elements
        evadeChargesContainer = root.Q<VisualElement>("evade-charges-container");
        evadeRechargeProgressBar = root.Q<ProgressBar>("evade-recharge-progressbar");

        // Find the player unit and evade data
        playerUnit = GetComponentInParent<Unit>(); // Assuming this UI is a child of the player unit
        if (playerUnit != null)
        {
            if (playerUnit.UnitMove != null)
            {
                evadeData = playerUnit.UnitMove.EvadeData;
            }
            playerHealthData = playerUnit.playerHealthData;
        }

        if (evadeData == null)
        {
            Debug.LogError("EvadeData not found or assigned to playerUnit. Evade UI will not function correctly.");
            // return; // Don't return, as health UI might still work
        }
        if (playerHealthData == null)
        {
            Debug.LogError("PlayerHealthData not found or assigned to playerUnit. Health UI will not function correctly.");
            // return; // Don't return, as other UIs might still work
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
        var weaponSystem = playerUnit.UnitWeaponSystem;
        var moveSystem = playerUnit.UnitMove;

        // --- Update Health UI ---
        if (playerHealthData != null)
        {
            healthProgressBar.style.display = DisplayStyle.Flex;
            healthLabel.style.display = DisplayStyle.Flex;

            float healthPercentage = playerUnit.CurrentHealth / playerHealthData.maxHealth;
            healthProgressBar.value = healthPercentage;
            healthLabel.text = $"{Mathf.CeilToInt(playerUnit.CurrentHealth)}/{Mathf.CeilToInt(playerHealthData.maxHealth)}";
        }
        else
        {
            healthProgressBar.style.display = DisplayStyle.None;
            healthLabel.style.display = DisplayStyle.None;
        }

        // --- Update Ammo UI ---
        if (weaponSystem != null && weaponSystem.WeaponData != null)
        {
            ammoLabel.style.display = DisplayStyle.Flex;
            ammoLabel.text = $"{weaponSystem.CurrentAmmo}/{weaponSystem.WeaponData.magazineSize}";

            bool isReloading = weaponSystem.IsReloading;
            reloadStatusLabel.style.display = isReloading ? DisplayStyle.Flex : DisplayStyle.None;
        }
        else
        {
            ammoLabel.style.display = DisplayStyle.None;
            reloadStatusLabel.style.display = DisplayStyle.None;
        }


        // --- Update Evade UI ---
        if (moveSystem != null && evadeData != null)
        {
            // Update charge icons
            for (int i = 0; i < evadeData.maxEvadeCharges; i++)
            {
                if (i < chargeIcons.Count)
                {
                    if (i < moveSystem.CurrentEvadeCharges)
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
            if (moveSystem.CurrentEvadeCharges < evadeData.maxEvadeCharges)
            {
                evadeRechargeProgressBar.style.display = DisplayStyle.Flex;

                float timeSinceLastEvade = Time.time - moveSystem.LastEvadeTime;
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

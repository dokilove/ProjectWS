using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

[RequireComponent(typeof(UIDocument))]
public class PlayerWorldSpaceUIController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    // Player Unit and Data
    private Unit playerUnit;
    private UnitMeleeSystem playerMeleeSystem;
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

    // Melee UI Elements
    private VisualElement meleeChargeContainer;
    private ProgressBar meleeChargeProgressBar;

    // Attack Mode UI Elements
    private Label attackModeLabel;

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

        // Query Melee UI Elements
        meleeChargeContainer = root.Q<VisualElement>("melee-charge-container");
        meleeChargeProgressBar = root.Q<ProgressBar>("melee-charge-progressbar");

        // Query Attack Mode UI Elements
        attackModeLabel = root.Q<Label>("attack-mode-label");


        // Find the player unit and its components
        playerUnit = GetComponentInParent<Unit>(); 
        if (playerUnit != null)
        {
            if (playerUnit.UnitMove != null)
            {
                evadeData = playerUnit.UnitMove.EvadeData;
            }
            playerHealthData = playerUnit.playerHealthData;
            playerMeleeSystem = playerUnit.UnitMeleeSystem;

            if (playerMeleeSystem != null)
            {
                playerMeleeSystem.OnChargeProgressChanged += UpdateMeleeChargeUI;
            }
            playerUnit.OnAttackModeChanged += UpdateAttackModeUI;
        }

        if (evadeData == null)
        {
            Debug.LogWarning("EvadeData not found or assigned to playerUnit. Evade UI will not function correctly.");
        }
        if (playerHealthData == null)
        {
            Debug.LogWarning("PlayerHealthData not found or assigned to playerUnit. Health UI will not function correctly.");
        }
        if (playerMeleeSystem == null)
        {
            Debug.LogWarning("UnitMeleeSystem not found on playerUnit. Melee Charge UI will not function.");
        }
        if (playerUnit == null)
        {
            Debug.LogWarning("Player Unit not found. Attack Mode UI will not function.");
        }


        // Initialize evade charge icons
        if(evadeData != null) CreateChargeIcons();
        UpdateUI(); // Initial update
        UpdateMeleeChargeUI(0); // Initial hide
        if (playerUnit != null) UpdateAttackModeUI(playerUnit.CurrentAttackMode); // Initial mode display
    }

    private void OnDisable()
    {
        if (playerMeleeSystem != null)
        {
            playerMeleeSystem.OnChargeProgressChanged -= UpdateMeleeChargeUI;
        }
        if (playerUnit != null)
        {
            playerUnit.OnAttackModeChanged -= UpdateAttackModeUI;
        }
    }

    private void LateUpdate()
    {
        if (mainCamera == null) return;

        // Make the UI always face the camera's forward direction
        transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);

        if (playerUnit == null) return;

        UpdateUI();
    }

    private void UpdateMeleeChargeUI(float progress)
    {
        if (meleeChargeContainer == null) return;

        if (progress > 0.01f) // Use a small threshold to avoid flickering
        {
            meleeChargeContainer.style.display = DisplayStyle.Flex;
            meleeChargeProgressBar.value = progress;
        }
        else
        {
            meleeChargeContainer.style.display = DisplayStyle.None;
        }
    }

    private void UpdateAttackModeUI(AttackMode mode)
    {
        if (attackModeLabel == null) return;
        attackModeLabel.text = mode.ToString() + " Mode";
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

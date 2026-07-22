using System;
using UnityEngine;
using ProjectWS.Utility; // [NEW] BulletTimeManager 사용을 위해 추가
using System.Collections;


// Enum to define the attack modes
public enum AttackMode
{
    Melee,
    Ranged
}

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
    public string justDodgeEffectPoolTag; // [MODIFIED] Name changed from guardEffectPoolTag

    [Header("Just Dodge Settings")]
    [SerializeField] private float justDodgeWindow = 0.15f; // 저스트 회피 판정 시간
    [SerializeField] private float bulletTimeDuration = 2f;   // 불릿 타임 지속 시간
    [SerializeField] private float bulletTimeScale = 0.2f;    // 불릿 타임 속도
    [SerializeField] private float justDodgeAttackBuffMultiplier = 2f; // 다음 공격 데미지 배율

    public bool IsJustDodgeWindowActive { get; private set; }
    public bool IsNextAttackBuffed { get; private set; }
    public float CurrentHealth { get; private set; }
    public bool IsInvincible { get; set; } = false; // New field for invincibility
    public bool IsDead => CurrentHealth <= 0;

    public bool IsControlledByPlayer { get; private set; } = false;

    // --- Attack Mode State ---
    public AttackMode CurrentAttackMode { get; private set; } = AttackMode.Melee;
    public event Action<AttackMode> OnAttackModeChanged;


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

    private void OnEnable()
    {
        // [NEW] 불릿 타임 종료 시 버프 해제 이벤트 구독
        if (BulletTimeManager.Instance != null)
        {
            BulletTimeManager.Instance.OnBulletTimeEnd += ConsumeAttackBuff;
        }
    }

    private void OnDisable()
    {
        // [NEW] 오브젝트 비활성화 시 이벤트 구독 해제
        if (BulletTimeManager.Instance != null)
        {
            BulletTimeManager.Instance.OnBulletTimeEnd -= ConsumeAttackBuff;
        }
    }

    private void Start()
    {
        // Set initial attack mode after all components are initialized and started
        SetAttackMode(CurrentAttackMode);
    }

    /// <summary>
    /// Sets the current attack mode and triggers associated logic in other components.
    /// </summary>
    public void SetAttackMode(AttackMode newMode)
    {
        AttackMode previousMode = CurrentAttackMode;
        CurrentAttackMode = newMode;

        if (previousMode != newMode)
        {
            OnAttackModeChanged?.Invoke(CurrentAttackMode);
            // Reset any active hold states in UnitInput when mode changes
            UnitInput.ResetHoldStates();
        }

        // Trigger mode-specific logic in other components
        if (CurrentAttackMode == AttackMode.Ranged)
        {
            UnitWeaponSystem.OnEnterRangedMode();
            UnitMove.OnEnterRangedMode();
            UnitAnimator.OnEnterRangedMode();
            UnitVisuals.OnEnterRangedMode();
        }
        else // Melee Mode
        {
            UnitWeaponSystem.OnEnterMeleeMode();
            UnitMove.OnEnterMeleeMode();
            UnitAnimator.OnEnterMeleeMode();
            UnitVisuals.OnEnterMeleeMode();
        }
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        // [MODIFIED] 저스트 회피 판정 로직 추가
        if (IsJustDodgeWindowActive)
        {
            Debug.Log("Just Dodge Successful!");
            ActivateJustDodgeEffects();
            return; // 데미지를 받지 않고 함수 종료
        }

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

    // [NEW] 회피 시작 시 호출될 함수
    public void OnEvadeStart()
    {
        StartCoroutine(JustDodgeWindowCoroutine());
    }

    // [NEW] 저스트 회피 판정 시간을 관리하는 코루틴
    private IEnumerator JustDodgeWindowCoroutine()
    {
        IsJustDodgeWindowActive = true;
        yield return new WaitForSeconds(justDodgeWindow);
        IsJustDodgeWindowActive = false;
    }

    // [NEW] 저스트 회피 성공 효과를 발동시키는 함수
    public void ActivateJustDodgeEffects()
    {
        // [NEW] Play the visual effect for a successful just dodge
        if (!string.IsNullOrEmpty(justDodgeEffectPoolTag) && EffectPoolManager.Instance != null)
        {
            EffectPoolManager.Instance.GetPooledObject(justDodgeEffectPoolTag, transform.position, Quaternion.identity);
        }

        IsNextAttackBuffed = true;
        if (BulletTimeManager.Instance != null)
        {
            BulletTimeManager.Instance.StartBulletTime(bulletTimeDuration, bulletTimeScale);
        }
        else
        {
            Debug.LogError("BulletTimeManager 인스턴스를 찾을 수 없습니다!");
        }
    }

    // [NEW] 공격력 버프를 사용(소모)하거나 해제하는 함수
    public void ConsumeAttackBuff()
    {
        if (IsNextAttackBuffed)
        {
            Debug.Log("Just Dodge attack buff consumed or expired.");
            IsNextAttackBuffed = false;
        }
    }

    // [NEW] 현재 공격 데미지에 버프를 적용하는 함수
    public float GetBuffedDamage(float originalDamage)
    {
        if (IsNextAttackBuffed)
        {
            ConsumeAttackBuff(); // 버프를 사용했으므로 즉시 소모
            return originalDamage * justDodgeAttackBuffMultiplier;
        }
        return originalDamage;
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

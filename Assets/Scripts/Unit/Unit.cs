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

    // 저스트 회피 판정 종료 시각 (실제 시간 기준: Time.unscaledTime)
    // - 불릿 타임(timeScale 감소) 중에도 판정 시간이 늘어나지 않음
    // - 코루틴이 아니므로 회피 도중 비활성화(차량 탑승)되어도 판정 상태가 true로 남지 않음
    private float justDodgeWindowEndTime = -1f;
    public bool IsJustDodgeWindowActive => Time.unscaledTime < justDodgeWindowEndTime;
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
        SetAttackMode(CurrentAttackMode, force: true);
    }

    /// <summary>
    /// Sets the current attack mode and triggers associated logic in other components.
    /// 모드가 바뀌지 않았으면 아무것도 하지 않습니다 (게임패드 입력은 매 프레임 호출하므로).
    /// force가 true이면 같은 모드라도 모드별 초기화 로직을 다시 실행합니다.
    /// </summary>
    public void SetAttackMode(AttackMode newMode, bool force = false)
    {
        AttackMode previousMode = CurrentAttackMode;
        if (!force && previousMode == newMode) return;

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
            CombatLog.Log("Just Dodge Successful!");
            ActivateJustDodgeEffects();
            return; // 데미지를 받지 않고 함수 종료
        }

        if (IsInvincible) return; // If invincible, do not take damage

        CombatLog.Log($"TakeDamage called on {gameObject.name} for {amount} damage.");

        CurrentHealth -= amount;
        CurrentHealth = Mathf.Max(CurrentHealth, 0); // Ensure health doesn't go below 0

        // 직렬화된 string은 null이 아니라 ""이므로 IsNullOrEmpty로 검사 (빈 태그면 이펙트 생략)
        if (!string.IsNullOrEmpty(hitEffectPoolTag) && EffectPoolManager.Instance != null)
        {
            EffectPoolManager.Instance.GetPooledObject(hitEffectPoolTag, transform.position, Quaternion.identity);
        }

        if (IsDead)
        {
            CombatLog.Log($"{gameObject.name} has been defeated!");
            // TODO: Add death logic (e.g., disable unit, play death animation)
        }
    }

    // [NEW] 회피 시작 시 호출될 함수
    public void OnEvadeStart()
    {
        justDodgeWindowEndTime = Time.unscaledTime + justDodgeWindow;
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
            CombatLog.Log("Just Dodge attack buff consumed or expired.");
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
        float maxHealth = playerHealthData != null ? playerHealthData.maxHealth : 100f;
        CurrentHealth = Mathf.Min(CurrentHealth, maxHealth); // Ensure health doesn't exceed maxHealth
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

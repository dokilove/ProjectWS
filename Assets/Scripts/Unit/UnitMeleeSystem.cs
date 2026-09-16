using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MeleeState
{
    Idle,       // 대기 상태 (언제든 공격/차징 가능)
    Windup,     // 모션 시작 및 대시 발동 (타격 판정 전 선딜레이)
    Active,     // 타격 판정 및 이펙트 발생
    Recovery    // 후딜레이 및 캔슬 윈도우 (다음 콤보 선입력 또는 회피로 즉시 캔슬 가능)
}

/// <summary>
/// Unit의 근접 전투 시스템.
/// 애니메이터 상태머신과 독립적으로 C# 코루틴/타이머를 통해
/// [선딜레이 ➔ 타격 판정 ➔ 캔슬/선입력 윈도우 ➔ 리셋] 라이프사이클을 정밀하게 제어합니다.
/// </summary>
public class UnitMeleeSystem : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private MeleeData _meleeData;
    public MeleeData MeleeData => _meleeData;

    [Header("Targeting & Visuals")]
    [SerializeField] private LayerMask enemyLayerMask;
    [SerializeField] private Material chargeAttackMaterial;

    [Header("Projectile Destruction (발사체 파괴)")]
    [Tooltip("근접 공격으로 적 발사체를 베어 파괴/삭제할 수 있는지 여부")]
    [SerializeField] private bool canDestroyProjectiles = true;
    [Tooltip("파괴 가능한 적 발사체 레이어 (미지정 시 EnemyAttackLayer 기본 할당)")]
    [SerializeField] private LayerMask projectileLayerMask;
    [Tooltip("발사체 파괴 시 재생할 이펙트 태그 (EffectPoolManager)")]
    [SerializeField] private string projectileDestroyEffectTag = "hit02";

    [Header("Input Buffer Settings")]
    [Tooltip("선입력 유효 시간 (초)")]
    [SerializeField] private float inputBufferWindow = 0.25f;

    // --- Dependencies ---
    private Unit _unit;

    // --- State ---
    private MeleeState _currentState = MeleeState.Idle;
    public MeleeState CurrentState => _currentState;

    private Vector3 aimDirection;
    private int comboCounter = 0;
    public int ComboCounter => comboCounter;
    private float _lastAttackStartTime = -1f;
    private bool isMeleeChargePrimed = false;
    public bool IsMeleeChargePrimed => isMeleeChargePrimed;
    private float chargeStartTime = 0f;

    private bool _hasBufferedComboInput = false;
    private float _bufferedInputTimestamp = -1f;
    private Coroutine _currentAttackCoroutine = null;

    private readonly HashSet<Collider> _hitTargetsThisSwing = new HashSet<Collider>();

    public Vector3 AutoAimTargetPosition { get; private set; } = Vector3.zero;
    public float MeleeLockOnRadius => _meleeData != null ? _meleeData.meleeLockOnRadius : 6f;

    // --- Events ---
    public event Action<float> OnChargeProgressChanged;
    public event Action<MeleeState> OnStateChanged;

    public void Init(Unit unit)
    {
        _unit = unit;
    }

    private void Awake()
    {
        if (_meleeData == null)
        {
            Debug.LogError("MeleeData is not assigned in the inspector!", this);
        }
        aimDirection = transform.forward;

        // 발사체 레이어가 인스펙터에서 미할당(0)된 경우 EnemyAttackLayer(15) 자동 설정
        if (projectileLayerMask.value == 0)
        {
            projectileLayerMask = LayerMask.GetMask("EnemyAttackLayer");
            if (projectileLayerMask.value == 0)
            {
                projectileLayerMask = 1 << 15;
            }
        }
    }

    private void Update()
    {
        HandleComboTimeout();
        HandleChargeProgress();
        CheckBufferedInput();

        // 콤보 2타 이상 대기/진행 중이거나 차지 중일 때만 적 탐색 수행
        if (_unit != null && _unit.CurrentAttackMode == AttackMode.Melee && (comboCounter > 0 || isMeleeChargePrimed))
        {
            PerformAutoAim();
        }
        else if (comboCounter == 0 && !isMeleeChargePrimed)
        {
            AutoAimTargetPosition = Vector3.zero;
        }
    }

    private void HandleChargeProgress()
    {
        if (isMeleeChargePrimed && _meleeData != null)
        {
            float threshold = _meleeData.chargeAttack.chargeTimeThreshold > 0
                ? _meleeData.chargeAttack.chargeTimeThreshold
                : 1.0f;

            float progress = (Time.time - chargeStartTime) / threshold;
            OnChargeProgressChanged?.Invoke(Mathf.Clamp01(progress));
        }
    }

    private void CheckBufferedInput()
    {
        if (!_hasBufferedComboInput) return;

        // 선입력 유효 시간 초과 체크
        if (Time.time - _bufferedInputTimestamp > inputBufferWindow)
        {
            _hasBufferedComboInput = false;
            return;
        }

        // 캔슬 가능 구간(Recovery) 또는 대기(Idle) 상태에 도달하면 버퍼 소비
        if (_currentState == MeleeState.Recovery || _currentState == MeleeState.Idle)
        {
            _hasBufferedComboInput = false;
            TriggerNextComboStep();
        }
    }

    /// <summary>
    /// 조준 방향을 설정합니다. (UnitInput에서 호출)
    /// </summary>
    public void SetAim(Vector3 newAimDirection)
    {
        aimDirection = newAimDirection;
    }

    /// <summary>
    /// 현재 우선순위(1. 이동 입력, 2. 반경 내 적, 3. 캐릭터 전방)가 반영된 유효 공격 방향을 반환합니다.
    /// allowAutoAim이 false인 경우(Combo 1 등) 적 자동 조준을 무시하고 입력/전방만 반영합니다.
    /// </summary>
    public Vector3 GetCurrentAttackDirection(bool allowAutoAim = true)
    {
        // 1순위: 이동 입력 방향
        if (_unit != null && _unit.UnitInput != null && _unit.UnitInput.MoveInput.sqrMagnitude > 0.01f)
        {
            Vector2 moveInput = _unit.UnitInput.MoveInput;
            Vector3 moveDirection;
            if (Camera.main != null)
            {
                Vector3 cameraRight = Camera.main.transform.right;
                Vector3 cameraRightFlat = new Vector3(cameraRight.x, 0, cameraRight.z).normalized;
                Vector3 cameraForwardFlat = Vector3.Cross(Vector3.up, cameraRightFlat);
                Vector3 moveForward = -cameraForwardFlat;
                Vector3 moveRight = cameraRightFlat;
                moveDirection = (moveForward * moveInput.y + moveRight * moveInput.x).normalized;
            }
            else
            {
                moveDirection = new Vector3(moveInput.x, 0, moveInput.y).normalized;
            }

            if (moveDirection.sqrMagnitude > 0.001f)
            {
                return moveDirection;
            }
        }

        // 2순위: 반경 내 적 방향 (allowAutoAim이 true인 경우만)
        if (allowAutoAim && AutoAimTargetPosition != Vector3.zero)
        {
            Vector3 dirToEnemy = AutoAimTargetPosition - transform.position;
            dirToEnemy.y = 0;
            if (dirToEnemy.sqrMagnitude > 0.001f)
            {
                return dirToEnemy.normalized;
            }
        }

        // 3순위: 수동 조준 방향 또는 캐릭터 전방 방향
        if (aimDirection.sqrMagnitude > 0.001f)
        {
            Vector3 dir = aimDirection;
            dir.y = 0;
            return dir.normalized;
        }

        Vector3 fwd = transform.forward;
        fwd.y = 0;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        return fwd.normalized;
    }

    /// <summary>
    /// 콤보 입력 대기 시간 초과 시 콤보 카운터를 0으로 초기화합니다.
    /// </summary>
    private void HandleComboTimeout()
    {
        if (_meleeData == null || _meleeData.comboSteps == null || _meleeData.comboSteps.Count == 0) return;

        if (comboCounter > 0 && _currentState == MeleeState.Idle && !isMeleeChargePrimed)
        {
            int lastIndex = Mathf.Clamp(comboCounter - 1, 0, _meleeData.comboSteps.Count - 1);
            float resetTime = _meleeData.comboSteps[lastIndex].comboResetTime;

            if (Time.time - _lastAttackStartTime > resetTime)
            {
                comboCounter = 0;
            }
        }
    }

    /// <summary>
    /// 일반 콤보 공격 입력 수신
    /// </summary>
    public void HandleMeleeComboInput()
    {
        if (_meleeData == null || _meleeData.comboSteps == null || _meleeData.comboSteps.Count == 0) return;

        // 1. 유휴(Idle) 상태이거나 캔슬 윈도우(Recovery)에 진입한 경우 -> 즉시 다음 콤보 발동
        if (_currentState == MeleeState.Idle || _currentState == MeleeState.Recovery)
        {
            TriggerNextComboStep();
            return;
        }

        // 2. 선딜(Windup) 또는 타격(Active) 중인 경우 -> 선입력 버퍼에 저장
        if (_currentState == MeleeState.Windup || _currentState == MeleeState.Active)
        {
            _hasBufferedComboInput = true;
            _bufferedInputTimestamp = Time.time;
        }
    }

    /// <summary>
    /// 다음 콤보 스텝을 실행합니다.
    /// </summary>
    private void TriggerNextComboStep()
    {
        if (_currentAttackCoroutine != null)
        {
            StopCoroutine(_currentAttackCoroutine);
            _currentAttackCoroutine = null;
        }

        int stepIndex = comboCounter;
        if (stepIndex >= _meleeData.comboSteps.Count)
        {
            stepIndex = 0; // 최대 콤보 도달 후 재입력 시 1타로 순환
        }

        comboCounter = stepIndex + 1;
        _lastAttackStartTime = Time.time;
        _hasBufferedComboInput = false;

        _currentAttackCoroutine = StartCoroutine(ComboStepCoroutine(stepIndex));
    }

    /// <summary>
    /// 단일 콤보 스텝의 전체 라이프사이클을 관리하는 코루틴
    /// [1. 시작/대시/애니메이션 ➔ 2. 선딜/다단히트 순차 타격 ➔ 3. 캔슬 윈도우(Recovery) ➔ 4. 종료(Idle)]
    /// </summary>
    private IEnumerator ComboStepCoroutine(int stepIndex)
    {
        var step = _meleeData.comboSteps[stepIndex];
        _hitTargetsThisSwing.Clear();
        float motionStartTime = Time.time;

        // Combo 1(stepIndex == 0)일 때는 적 자동 추적을 끄고, Combo 2 이상(stepIndex > 0)일 때만 자동 추적 활성화
        bool allowAutoAim = (stepIndex > 0);
        if (allowAutoAim)
        {
            PerformAutoAim();
        }
        else
        {
            AutoAimTargetPosition = Vector3.zero;
            _unit?.UnitMove?.SetIsAutoAiming(false);
        }

        // [Phase 1: 시작]
        Vector3 attackDir = GetCurrentAttackDirection(allowAutoAim);
        if (attackDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(attackDir);
        }

        _unit.UnitAnimator.TriggerMelee(stepIndex + 1);

        // 초기 대시 적용
        if (step.dashForce != 0)
        {
            _unit.UnitMove.ApplyMeleeDash(attackDir, step.dashForce);
        }

        if (step.useMultiHit && step.subHits != null && step.subHits.Count > 0)
        {
            // ===== [다단히트 모드]: 임의 시점(subHit.hitTime)별 순차 타격 =====
            for (int i = 0; i < step.subHits.Count; i++)
            {
                var subHit = step.subHits[i];
                if (subHit == null) continue;

                // 목표 발생 시점까지 대기
                float targetTime = motionStartTime + subHit.hitTime;
                float waitTime = targetTime - Time.time;
                if (waitTime > 0f)
                {
                    SetState(MeleeState.Windup);
                    yield return new WaitForSeconds(waitTime);
                }

                SetState(MeleeState.Active);
                _hitTargetsThisSwing.Clear(); // 매 타격마다 피격 목록 리셋 (재타격 허용)

                // 서브히트 순간 추가 대시
                if (subHit.dashForce != 0)
                {
                    if (allowAutoAim) PerformAutoAim();
                    Vector3 dashDir = GetCurrentAttackDirection(allowAutoAim);
                    if (dashDir != Vector3.zero)
                    {
                        transform.rotation = Quaternion.LookRotation(dashDir);
                    }
                    _unit.UnitMove.ApplyMeleeDash(dashDir, subHit.dashForce);
                }

                // 형태 파라미터 적용 (서브히트 자체 형태 및 범위 사용)
                MeleeHitboxShape shape = subHit.shape;
                float radius = subHit.attackRadius;
                float angle = subHit.attackAngle;
                float boxWidth = subHit.boxWidth;
                float boxLength = subHit.boxLength;
                float offset = subHit.forwardOffset;
                Material visualMat = subHit.overrideMaterial != null ? subHit.overrideMaterial : step.overrideMaterial;

                if (_unit?.UnitVisuals != null)
                {
                    StartCoroutine(_unit.UnitVisuals.ShowMeleeVisualizer(
                        shape, radius, angle, boxWidth, boxLength, offset, visualMat, 0.15f
                    ));
                }

                PerformMeleeAttack(shape, radius, angle, boxWidth, boxLength, offset, subHit.damage);
            }
        }
        else
        {
            // ===== [단발 모드]: 기본 선딜레이 및 타격 =====
            SetState(MeleeState.Windup);
            if (step.hitDelay > 0f)
            {
                yield return new WaitForSeconds(step.hitDelay);
            }

            SetState(MeleeState.Active);
            Material visualMat = step.overrideMaterial != null ? step.overrideMaterial : null;

            if (_unit?.UnitVisuals != null)
            {
                StartCoroutine(_unit.UnitVisuals.ShowMeleeVisualizer(
                    step.shape,
                    step.attackRadius,
                    step.attackAngle,
                    step.boxWidth,
                    step.boxLength,
                    step.forwardOffset,
                    visualMat
                ));
            }

            PerformMeleeAttack(
                step.shape,
                step.attackRadius,
                step.attackAngle,
                step.boxWidth,
                step.boxLength,
                step.forwardOffset,
                step.damage
            );
        }

        // [Phase 4: 캔슬 윈도우 대기]
        float cancelWait = (motionStartTime + step.cancelWindowTime) - Time.time;
        if (cancelWait > 0f)
        {
            yield return new WaitForSeconds(cancelWait);
        }

        // [Phase 5: 캔슬 가능 구간 (Recovery)]
        SetState(MeleeState.Recovery);

        // 선입력된 공격이 있다면 대기 없이 즉시 다음 콤보로 전이
        if (_hasBufferedComboInput && (Time.time - _bufferedInputTimestamp <= inputBufferWindow))
        {
            _hasBufferedComboInput = false;
            TriggerNextComboStep();
            yield break;
        }

        // [Phase 6: 후딜레이 완료 대기]
        float totalWait = (motionStartTime + step.totalDuration) - Time.time;
        if (totalWait > 0f)
        {
            yield return new WaitForSeconds(totalWait);
        }

        SetState(MeleeState.Idle);
        _currentAttackCoroutine = null;
    }

    /// <summary>
    /// 차지 공격 시작 (버튼 누름 시작)
    /// </summary>
    public void HandleMeleeChargeInput()
    {
        if (_meleeData == null) return;

        isMeleeChargePrimed = true;
        chargeStartTime = Time.time;
        PerformAutoAim();
        Vector3 attackDir = GetCurrentAttackDirection(allowAutoAim: true);
        if (attackDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(attackDir);
        }
    }

    /// <summary>
    /// 차지 공격 해제 (버튼 뗌) -> 차지 시간 충족 시 차지 공격 실행
    /// </summary>
    public void HandleMeleeChargeReleaseInput()
    {
        if (!isMeleeChargePrimed || _meleeData == null) return;

        float chargeDuration = Time.time - chargeStartTime;
        float threshold = _meleeData.chargeAttack.chargeTimeThreshold > 0
            ? _meleeData.chargeAttack.chargeTimeThreshold
            : 1.0f;

        CancelCharge(); // UI를 0으로 닫고 isMeleeChargePrimed = false 처리

        if (chargeDuration >= threshold)
        {
            // 진행 중이던 일반 콤보 공격을 즉시 중단하고 차지 공격 실행
            if (_currentAttackCoroutine != null)
            {
                StopCoroutine(_currentAttackCoroutine);
                _currentAttackCoroutine = null;
            }

            _hasBufferedComboInput = false;
            _currentAttackCoroutine = StartCoroutine(ChargeAttackCoroutine());
        }
    }

    /// <summary>
    /// 차지 공격의 전체 시퀀스를 실행하는 코루틴
    /// </summary>
    private IEnumerator ChargeAttackCoroutine()
    {
        var charge = _meleeData.chargeAttack;
        _hitTargetsThisSwing.Clear();
        float motionStartTime = Time.time;

        PerformAutoAim();
        Vector3 attackDir = GetCurrentAttackDirection(allowAutoAim: true);
        if (attackDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(attackDir);
        }

        _unit.UnitAnimator.TriggerChargeMelee();

        // 초기 차지 대시 적용
        if (charge.dashForce != 0)
        {
            _unit.UnitMove.ApplyMeleeDash(attackDir, charge.dashForce);
        }

        if (charge.useMultiHit && charge.subHits != null && charge.subHits.Count > 0)
        {
            // ===== [다단히트 모드]: 차지 서브히트 순차 실행 =====
            for (int i = 0; i < charge.subHits.Count; i++)
            {
                var subHit = charge.subHits[i];
                if (subHit == null) continue;

                float targetTime = motionStartTime + subHit.hitTime;
                float waitTime = targetTime - Time.time;
                if (waitTime > 0f)
                {
                    SetState(MeleeState.Windup);
                    yield return new WaitForSeconds(waitTime);
                }

                SetState(MeleeState.Active);
                _hitTargetsThisSwing.Clear();

                if (subHit.dashForce != 0)
                {
                    PerformAutoAim();
                    Vector3 dashDir = GetCurrentAttackDirection(allowAutoAim: true);
                    if (dashDir != Vector3.zero)
                    {
                        transform.rotation = Quaternion.LookRotation(dashDir);
                    }
                    _unit.UnitMove.ApplyMeleeDash(dashDir, subHit.dashForce);
                }

                MeleeHitboxShape shape = subHit.shape;
                float radius = subHit.attackRadius;
                float angle = subHit.attackAngle;
                float boxWidth = subHit.boxWidth;
                float boxLength = subHit.boxLength;
                float offset = subHit.forwardOffset;
                Material visualMat = subHit.overrideMaterial != null ? subHit.overrideMaterial : chargeAttackMaterial;

                if (_unit?.UnitVisuals != null)
                {
                    StartCoroutine(_unit.UnitVisuals.ShowMeleeVisualizer(
                        shape, radius, angle, boxWidth, boxLength, offset, visualMat, 0.15f
                    ));
                }

                PerformMeleeAttack(shape, radius, angle, boxWidth, boxLength, offset, subHit.damage);
            }
        }
        else
        {
            // ===== [단발 모드]: 기본 선딜레이 및 타격 =====
            SetState(MeleeState.Windup);
            if (charge.hitDelay > 0f)
            {
                yield return new WaitForSeconds(charge.hitDelay);
            }

            SetState(MeleeState.Active);
            Material visualMat = charge.overrideMaterial != null ? charge.overrideMaterial : chargeAttackMaterial;

            if (_unit?.UnitVisuals != null)
            {
                StartCoroutine(_unit.UnitVisuals.ShowMeleeVisualizer(
                    charge.shape,
                    charge.attackRadius,
                    charge.attackAngle,
                    charge.boxWidth,
                    charge.boxLength,
                    charge.forwardOffset,
                    visualMat
                ));
            }

            PerformMeleeAttack(
                charge.shape,
                charge.attackRadius,
                charge.attackAngle,
                charge.boxWidth,
                charge.boxLength,
                charge.forwardOffset,
                charge.damage
            );
        }

        // 후딜레이
        float totalWait = (motionStartTime + charge.totalDuration) - Time.time;
        if (totalWait > 0f)
        {
            yield return new WaitForSeconds(totalWait);
        }

        SetState(MeleeState.Idle);
        _currentAttackCoroutine = null;
        comboCounter = 0;
        _lastAttackStartTime = -1f;
    }

    /// <summary>
    /// 진행 중인 차징 상태를 취소합니다.
    /// </summary>
    public void CancelCharge()
    {
        if (isMeleeChargePrimed)
        {
            isMeleeChargePrimed = false;
            chargeStartTime = 0f;
            OnChargeProgressChanged?.Invoke(0f);
            AutoAimTargetPosition = Vector3.zero;
        }
    }

    /// <summary>
    /// 회피(Evade)나 피격 시 현재 진행 중인 모든 공격 및 차징을 즉시 취소합니다.
    /// </summary>
    public void CancelCurrentAttack()
    {
        if (_currentAttackCoroutine != null)
        {
            StopCoroutine(_currentAttackCoroutine);
            _currentAttackCoroutine = null;
        }

        _hasBufferedComboInput = false;
        comboCounter = 0;
        _lastAttackStartTime = -1f;
        CancelCharge();
        SetState(MeleeState.Idle);
        _hitTargetsThisSwing.Clear();
        AutoAimTargetPosition = Vector3.zero;
        _unit?.UnitMove?.SetIsAutoAiming(false);
    }

    private void SetState(MeleeState newState)
    {
        if (_currentState != newState)
        {
            _currentState = newState;
            OnStateChanged?.Invoke(_currentState);
        }
    }

    /// <summary>
    /// 근접 락온 반경 내 가장 가까운 적을 탐색하고 자동 조준 목표를 갱신합니다.
    /// </summary>
    private void PerformAutoAim()
    {
        float radius = MeleeLockOnRadius;
        if (radius <= 0)
        {
            _unit?.UnitMove?.SetIsAutoAiming(false);
            AutoAimTargetPosition = Vector3.zero;
            return;
        }

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, radius, enemyLayerMask);
        Transform closestEnemy = null;
        float minDistance = Mathf.Infinity;

        foreach (Collider hit in hitColliders)
        {
            float distance = Vector3.Distance(transform.position, hit.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestEnemy = hit.transform;
            }
        }

        if (closestEnemy != null)
        {
            AutoAimTargetPosition = closestEnemy.position;
            _unit?.UnitMove?.SetIsAutoAiming(true);
        }
        else
        {
            AutoAimTargetPosition = Vector3.zero;
            _unit?.UnitMove?.SetIsAutoAiming(false);
        }
    }

    /// <summary>
    /// 지정된 히트박스 형태(부채꼴 / 박스 / 원형)에 따라 적 충돌 판정 및 데미지를 적용합니다.
    /// 적 발사체가 범위 내에 있으면 함께 베어 파괴/삭제합니다.
    /// 1회 휘두름당 동일 적/발사체 중복 타격을 방지합니다.
    /// </summary>
    private void PerformMeleeAttack(
        MeleeHitboxShape shape,
        float radius,
        float angle,
        float boxWidth,
        float boxLength,
        float forwardOffset,
        float damage)
    {
        if (_unit == null) return;

        float finalDamage = _unit.GetBuffedDamage(damage);
        Vector3 forward = transform.forward;
        forward.y = 0;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        LayerMask targetMask = enemyLayerMask;
        if (canDestroyProjectiles)
        {
            targetMask |= projectileLayerMask;
        }

        Collider[] hits = null;

        switch (shape)
        {
            case MeleeHitboxShape.Sector:
            {
                // 부채꼴: 플레이어 중심 OverlapSphere + 전방 각도 검사
                hits = Physics.OverlapSphere(transform.position, radius, targetMask, QueryTriggerInteraction.Collide);
                foreach (Collider hit in hits)
                {
                    Vector3 directionToTarget = hit.transform.position - transform.position;
                    directionToTarget.y = 0;
                    float angleToTarget = directionToTarget.sqrMagnitude > 0.001f
                        ? Vector3.Angle(forward, directionToTarget.normalized)
                        : 0f;

                    if (angleToTarget <= angle * 0.5f)
                    {
                        ProcessHitCollider(hit, finalDamage);
                    }
                }
                return;
            }

            case MeleeHitboxShape.Box:
            {
                // 직사각형 박스: 플레이어 전방 forwardOffset ~ (forwardOffset + boxLength)
                Vector3 center = transform.position + forward * (forwardOffset + boxLength * 0.5f) + Vector3.up * 0.5f;
                Vector3 halfExtents = new Vector3(boxWidth * 0.5f, 1.5f, boxLength * 0.5f);
                Quaternion orientation = Quaternion.LookRotation(forward, Vector3.up);

                hits = Physics.OverlapBox(center, halfExtents, orientation, targetMask, QueryTriggerInteraction.Collide);
                break;
            }

            case MeleeHitboxShape.Circle:
            {
                // 전방 원형/구형: 플레이어 전방 forwardOffset 지점 중심 OverlapSphere
                Vector3 center = transform.position + forward * forwardOffset + Vector3.up * 0.5f;
                hits = Physics.OverlapSphere(center, radius, targetMask, QueryTriggerInteraction.Collide);
                break;
            }
        }

        if (hits != null)
        {
            foreach (Collider hit in hits)
            {
                ProcessHitCollider(hit, finalDamage);
            }
        }
    }

    /// <summary>
    /// 감지된 충돌체에 대해 적 유닛 피격(데미지) 또는 발사체 파괴를 수행합니다.
    /// </summary>
    private void ProcessHitCollider(Collider hit, float finalDamage)
    {
        if (hit == null || !hit.gameObject.activeInHierarchy || _hitTargetsThisSwing.Contains(hit)) return;

        // 1. 적 유닛 피격 처리
        EnemyHealth enemyHealth = hit.GetComponentInParent<EnemyHealth>();
        if (enemyHealth != null)
        {
            _hitTargetsThisSwing.Add(hit);
            enemyHealth.TakeDamage(finalDamage);
            return;
        }

        // 2. 적 발사체 파괴 처리
        if (canDestroyProjectiles)
        {
            Projectile projectile = hit.GetComponentInParent<Projectile>();
            if (projectile != null)
            {
                _hitTargetsThisSwing.Add(hit);
                projectile.DestroyByMelee(projectileDestroyEffectTag);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_meleeData == null) return;

        Vector3 forward = transform.forward;
        forward.y = 0;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 pos = transform.position + Vector3.up * 0.1f;

        // 락온 반경
        Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, MeleeLockOnRadius);

        // 콤보 스텝별 히트박스 기즈모 표시
        if (_meleeData.comboSteps != null)
        {
            Color[] stepColors = { Color.yellow, new Color(1f, 0.5f, 0f), Color.red, Color.magenta, Color.cyan };

            for (int i = 0; i < _meleeData.comboSteps.Count; i++)
            {
                var step = _meleeData.comboSteps[i];
                if (step == null) continue;

                Gizmos.color = stepColors[i % stepColors.Length];

                if (step.useMultiHit && step.subHits != null && step.subHits.Count > 0)
                {
                    foreach (var sub in step.subHits)
                    {
                        if (sub == null) continue;
                        DrawHitboxGizmo(sub.shape, pos, forward, sub.attackRadius, sub.attackAngle, sub.boxWidth, sub.boxLength, sub.forwardOffset);
                    }
                }
                else
                {
                    DrawHitboxGizmo(step.shape, pos, forward, step.attackRadius, step.attackAngle, step.boxWidth, step.boxLength, step.forwardOffset);
                }
            }
        }

        // 차지 공격 기즈모
        if (_meleeData.chargeAttack != null)
        {
            Gizmos.color = new Color(1f, 0.2f, 0f, 0.8f);
            var charge = _meleeData.chargeAttack;

            if (charge.useMultiHit && charge.subHits != null && charge.subHits.Count > 0)
            {
                foreach (var sub in charge.subHits)
                {
                    if (sub == null) continue;
                    DrawHitboxGizmo(sub.shape, pos, forward, sub.attackRadius, sub.attackAngle, sub.boxWidth, sub.boxLength, sub.forwardOffset);
                }
            }
            else
            {
                DrawHitboxGizmo(charge.shape, pos, forward, charge.attackRadius, charge.attackAngle, charge.boxWidth, charge.boxLength, charge.forwardOffset);
            }
        }
    }

    private void DrawHitboxGizmo(MeleeHitboxShape shape, Vector3 pos, Vector3 forward, float radius, float angle, float boxWidth, float boxLength, float offset)
    {
        switch (shape)
        {
            case MeleeHitboxShape.Sector:
            {
                int segments = 16;
                float startAngle = -angle * 0.5f;
                float angleStep = angle / segments;
                Vector3 prevPoint = pos + Quaternion.Euler(0, startAngle, 0) * forward * radius;
                Gizmos.DrawLine(pos, prevPoint);

                for (int s = 1; s <= segments; s++)
                {
                    Vector3 nextPoint = pos + Quaternion.Euler(0, startAngle + s * angleStep, 0) * forward * radius;
                    Gizmos.DrawLine(prevPoint, nextPoint);
                    prevPoint = nextPoint;
                }
                Gizmos.DrawLine(pos, prevPoint);
                break;
            }

            case MeleeHitboxShape.Box:
            {
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                float halfW = boxWidth * 0.5f;
                float z0 = offset;
                float z1 = offset + boxLength;

                Vector3 bl = pos - right * halfW + forward * z0;
                Vector3 br = pos + right * halfW + forward * z0;
                Vector3 tl = pos - right * halfW + forward * z1;
                Vector3 tr = pos + right * halfW + forward * z1;

                Gizmos.DrawLine(bl, br);
                Gizmos.DrawLine(br, tr);
                Gizmos.DrawLine(tr, tl);
                Gizmos.DrawLine(tl, bl);
                break;
            }

            case MeleeHitboxShape.Circle:
            {
                Vector3 center = pos + forward * offset;
                int segments = 24;
                float angleStep = 360f / segments;
                Vector3 prev = center + new Vector3(Mathf.Sin(0) * radius, 0, Mathf.Cos(0) * radius);
                for (int s = 1; s <= segments; s++)
                {
                    float rad = Mathf.Deg2Rad * (s * angleStep);
                    Vector3 next = center + new Vector3(Mathf.Sin(rad) * radius, 0, Mathf.Cos(rad) * radius);
                    Gizmos.DrawLine(prev, next);
                    prev = next;
                }
                break;
            }
        }
    }
#endif
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 차량의 전진/후진 시 전/후방 범퍼 트리거를 통해 적을 들이받아 파괴하고,
/// 차량에 반동 피해를 적용하는 몸통박치기(Ramming) 시스템입니다.
/// </summary>
public class VehicleRamSystem : MonoBehaviour
{
    [Header("Bumper References")]
    [Tooltip("전방 범퍼 충돌 판정 트리거")]
    [SerializeField] private VehicleRamHitbox frontBumper;
    [Tooltip("후방 범퍼 충돌 판정 트리거")]
    [SerializeField] private VehicleRamHitbox rearBumper;

    [Header("Ramming Conditions (박치기 발동 조건)")]
    [Tooltip("박치기가 발동되는 최소 주행 속도 (이 속도 이상으로 달릴 때만 충돌 판정)")]
    [SerializeField] private float minRamSpeed = 4f;

    public float MinRamSpeed => minRamSpeed;

    [Header("Damage Settings (공격 피해량)")]
    [Tooltip("적에게 가하는 기본 충돌 피해")]
    [SerializeField] private float baseRamDamage = 50f;

    [Tooltip("현재 속도에 비례한 추가 피해 배율 (최종 피해 = 기본 피해 + 현재속도 * 배율)")]
    [SerializeField] private float speedDamageMultiplier = 3f;

    [Tooltip("적에게 가하는 경직(스턴) 시간")]
    [SerializeField] private float hitStunDuration = 0.6f;

    [Tooltip("적을 밀쳐내는 넉백 거리")]
    [SerializeField] private float knockbackDistance = 2.5f;

    [Header("Vehicle Recoil & Feedback (차량 반동 및 감속)")]
    [Tooltip("적 충돌 시 차량이 받는 고정 반동 피해")]
    [SerializeField] private float baseSelfDamage = 3f;

    [Tooltip("적에게 가한 피해 중 차량이 받는 반동 피해 비율 (예: 0.05 = 5%)")]
    [Range(0f, 1f)]
    [SerializeField] private float selfDamageRatio = 0.05f;

    [Tooltip("차량이 반동 피해를 받을 수 있는 최소 간격 (초 단위, 다수 적 관통 시 연타 자폭 방지)")]
    [SerializeField] private float vehicleDamageCooldown = 0.15f;

    [Tooltip("충돌 시 차량 속도 감속 비율 (0 = 감속 없음, 0.15 = 15% 감속)")]
    [Range(0f, 1f)]
    [SerializeField] private float impactSlowdown = 0.15f;

    [Header("Target & Cooldown")]
    [Tooltip("충돌 대상 레이어 (기본값: Enemy)")]
    [SerializeField] private LayerMask targetLayer;

    [Tooltip("동일 적 재타격 방지 쿨다운 시간")]
    [SerializeField] private float hitCooldownPerEnemy = 0.5f;

    [Header("Visual Effects")]
    [Tooltip("충돌 시 재생할 이펙트 풀 태그")]
    [SerializeField] private string ramHitEffectPoolTag = "hit01";

    private Vehicle _vehicle;
    private float _lastVehicleDamageTime = -Mathf.Infinity;
    private readonly Dictionary<EnemyHealth, float> _enemyHitCooldowns = new Dictionary<EnemyHealth, float>();

    public void Init(Vehicle vehicle)
    {
        _vehicle = vehicle;
        EnsureTargetLayer();
        SetupBumpers();
    }

    private void Awake()
    {
        if (_vehicle == null)
        {
            _vehicle = GetComponent<Vehicle>();
        }

        EnsureTargetLayer();
        SetupBumpers();
    }

    private void EnsureTargetLayer()
    {
        if (targetLayer == 0)
        {
            int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            targetLayer = enemyLayerIndex != -1 ? (1 << enemyLayerIndex) : (1 << 10);
        }
    }

    /// <summary>
    /// 전/후방 범퍼가 없을 경우 자동으로 생성하거나 기존 자식을 찾아 연결합니다.
    /// </summary>
    public void SetupBumpers()
    {
        var existingHitboxes = GetComponentsInChildren<VehicleRamHitbox>();
        foreach (var hb in existingHitboxes)
        {
            if (hb.Type == BumperType.Front && frontBumper == null) frontBumper = hb;
            if (hb.Type == BumperType.Rear && rearBumper == null) rearBumper = hb;
        }

        BoxCollider mainBox = GetComponent<BoxCollider>();
        float halfLength = mainBox != null ? (mainBox.size.z * 0.5f) : 1.87f;
        float width = mainBox != null ? (mainBox.size.x * 1.05f) : 2.1f;
        float height = mainBox != null ? (mainBox.size.y * 0.7f) : 1.2f;
        float yCenter = mainBox != null ? mainBox.center.y : 0.8f;
        float bumperThickness = 0.6f;

        // 1. 전방 범퍼 자동 생성/연결
        if (frontBumper == null)
        {
            GameObject frontGO = new GameObject("FrontBumper");
            frontGO.transform.SetParent(transform, false);
            frontGO.layer = gameObject.layer;
            frontGO.transform.localPosition = new Vector3(0f, yCenter, halfLength + (bumperThickness * 0.2f));

            BoxCollider col = frontGO.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(width, height, bumperThickness);

            frontBumper = frontGO.AddComponent<VehicleRamHitbox>();
        }

        // 2. 후방 범퍼 자동 생성/연결
        if (rearBumper == null)
        {
            GameObject rearGO = new GameObject("RearBumper");
            rearGO.transform.SetParent(transform, false);
            rearGO.layer = gameObject.layer;
            rearGO.transform.localPosition = new Vector3(0f, yCenter, -halfLength - (bumperThickness * 0.2f));

            BoxCollider col = rearGO.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(width, height, bumperThickness);

            rearBumper = rearGO.AddComponent<VehicleRamHitbox>();
        }

        if (frontBumper != null) frontBumper.Init(this, BumperType.Front);
        if (rearBumper != null) rearBumper.Init(this, BumperType.Rear);
    }

    /// <summary>
    /// 범퍼 트리거에 충돌체가 감지되었을 때 호출됩니다.
    /// </summary>
    public void OnBumperTrigger(BumperType bumperType, Collider other)
    {
        if (_vehicle == null || _vehicle.IsDead) return;

        // 자기 자신 및 차량 자식 콜라이더 무시
        if (other.transform.root == transform.root) return;

        // 타겟 레이어 검사
        if (((1 << other.gameObject.layer) & targetLayer) == 0) return;

        // 차량 이동 컴포넌트 및 속도 확인
        float currentSpeed = _vehicle.VehicleMove != null ? _vehicle.VehicleMove.CurrentSpeed : 0f;

        // 전방 범퍼: 전진 속도가 minRamSpeed 이상일 때만 발동
        if (bumperType == BumperType.Front && currentSpeed < minRamSpeed)
        {
            return;
        }

        // 후방 범퍼: 후진 속도가 -minRamSpeed 이하일 때만 발동
        if (bumperType == BumperType.Rear && currentSpeed > -minRamSpeed)
        {
            return;
        }

        // 적 체력 컴포넌트 탐색
        EnemyHealth enemyHealth = other.GetComponentInParent<EnemyHealth>();
        if (enemyHealth == null) return;

        // 동일 적 쿨다운 검사
        if (_enemyHitCooldowns.TryGetValue(enemyHealth, out float lastHitTime))
        {
            if (Time.time < lastHitTime + hitCooldownPerEnemy)
            {
                return;
            }
        }
        _enemyHitCooldowns[enemyHealth] = Time.time;

        // 쿨다운 딕셔너리 정리 (메모리 누수 방지)
        CleanupCooldownDictionary();

        // 박치기 공격 실행
        ExecuteRam(enemyHealth, other, currentSpeed, bumperType);
    }

    private void ExecuteRam(EnemyHealth enemyHealth, Collider other, float currentSpeed, BumperType bumperType)
    {
        float absSpeed = Mathf.Abs(currentSpeed);

        // 1. 적 피해량 계산 및 적용
        float finalEnemyDamage = baseRamDamage + (absSpeed * speedDamageMultiplier);
        enemyHealth.TakeDamage(finalEnemyDamage, hitStunDuration);

        // 2. 적 넉백 및 경직 처리
        EnemyAI enemyAI = enemyHealth.GetComponent<EnemyAI>();
        if (enemyAI == null) enemyAI = enemyHealth.GetComponentInParent<EnemyAI>();
        if (enemyAI != null)
        {
            Vector3 pushDir = other.transform.position - transform.position;
            pushDir.y = 0f;
            Vector3 moveDirection = transform.forward * Mathf.Sign(currentSpeed);
            Vector3 ramDirection = (moveDirection + pushDir.normalized * 0.5f).normalized;
            enemyAI.ApplyKnockback(ramDirection, knockbackDistance);
        }

        // 3. 차량 반동(Self-Damage) 피해 처리 (최소 쿨다운 적용으로 자폭 방지)
        if (Time.time >= _lastVehicleDamageTime + vehicleDamageCooldown)
        {
            float selfDamage = baseSelfDamage + (finalEnemyDamage * selfDamageRatio);
            _vehicle.TakeDamage(selfDamage);
            _lastVehicleDamageTime = Time.time;
        }

        // 4. 충돌 시 차량 감속 피드백
        if (_vehicle.VehicleMove != null && impactSlowdown > 0f)
        {
            _vehicle.VehicleMove.ApplyImpactSlowdown(impactSlowdown);
        }

        // 5. 충돌 피격 이펙트 생성
        Vector3 impactPoint = other.ClosestPoint(transform.position);
        if (!string.IsNullOrEmpty(ramHitEffectPoolTag) && EffectPoolManager.Instance != null)
        {
            EffectPoolManager.Instance.GetPooledObject(ramHitEffectPoolTag, impactPoint, Quaternion.identity);
        }

        Debug.Log($"[VehicleRam] {bumperType} bumper rammed {enemyHealth.name}! Damage: {finalEnemyDamage:F0}, Speed: {currentSpeed:F1}");
    }

    private void CleanupCooldownDictionary()
    {
        if (_enemyHitCooldowns.Count > 30)
        {
            List<EnemyHealth> keysToRemove = null;
            float currentTime = Time.time;

            foreach (var kvp in _enemyHitCooldowns)
            {
                if (kvp.Key == null || !kvp.Key.gameObject.activeInHierarchy || currentTime >= kvp.Value + hitCooldownPerEnemy * 2f)
                {
                    if (keysToRemove == null) keysToRemove = new List<EnemyHealth>();
                    keysToRemove.Add(kvp.Key);
                }
            }

            if (keysToRemove != null)
            {
                for (int i = 0; i < keysToRemove.Count; i++)
                {
                    _enemyHitCooldowns.Remove(keysToRemove[i]);
                }
            }
        }
    }
}

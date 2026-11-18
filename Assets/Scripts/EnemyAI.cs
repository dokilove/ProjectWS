using UnityEngine;
using UnityEngine.AI; // Required for NavMeshAgent
using System.Collections; // Required for IEnumerator

public class EnemyAI : MonoBehaviour
{
    [SerializeField] private EnemyData enemyData;
    [SerializeField] private WeaponData weaponData; // Add WeaponData
    [SerializeField] private Transform firePoint; // Where projectiles will spawn

    [Header("Targeting Settings")]
    [Tooltip("생존 중인 차량이 있을 때 플레이어보다 차량을 우선 공격할지 여부")]
    [SerializeField] private bool prioritizeVehicle = true;

    [Tooltip("차량을 우선 공격할 최대 탐지 거리 (0 이하이면 맵 전역의 차량 대상)")]
    [SerializeField] private float vehicleDetectionRange = 0f;

    private NavMeshAgent agent;
    private Animator animator;
    private float lastFireTime;
    private float stunTimer = 0f;
    private Coroutine pushCoroutine;

    /// <summary>
    /// 현재 경직 상태인지 여부
    /// </summary>
    public bool IsStunned => stunTimer > 0f;

    /// <summary>
    /// 현재 밀려나는 중인지 여부
    /// </summary>
    public bool IsBeingPushed => pushCoroutine != null;

    /// <summary>
    /// 발사 간격(초). WeaponData.fireRate는 다른 무기와 동일하게 "초당 발사 수"로 해석합니다.
    /// </summary>
    private float FireInterval => (weaponData != null && weaponData.fireRate > 0f) ? 1f / weaponData.fireRate : float.MaxValue;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        if (enemyData != null)
        {
            agent.speed = enemyData.moveSpeed;
        }
        if (weaponData != null)
        {
            lastFireTime = -FireInterval; // Allow immediate firing
        }
    }

    private void OnDisable()
    {
        // 비활성화 시 코루틴은 자동으로 중단되므로 상태 플래그도 함께 정리 (풀 재사용 대비)
        pushCoroutine = null;
        if (stunTimer > 0f)
        {
            ResetStun();
        }
    }

    /// <summary>
    /// 대상에게 피격 경직(Hit Stun)을 부여합니다.
    /// </summary>
    public void ApplyStun(float duration)
    {
        if (enemyData != null)
        {
            if (!enemyData.canBeStunned) return; // 슈퍼아머 상태
            duration *= enemyData.stunResistance;
        }

        if (duration <= 0f) return;

        // 경직 시작 시 에이전트 정지 및 애니메이션 일시정지
        if (stunTimer <= 0f)
        {
            if (agent != null && agent.isOnNavMesh && agent.isActiveAndEnabled)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            if (animator != null)
            {
                animator.speed = 0f;
            }
        }

        // 경직 지속시간 갱신 (남은 시간과 새 경직 시간 중 더 큰 값으로 유지)
        stunTimer = Mathf.Max(stunTimer, duration);

        // 경직 종료 즉시 기습 사격 방지를 위한 쿨타임 미세 지연 (최소 0.2초 여유)
        if (weaponData != null && Time.time >= lastFireTime + FireInterval)
        {
            lastFireTime = Time.time - FireInterval + 0.2f;
        }
    }

    /// <summary>
    /// 대상에게 진행 방향으로 넉백을 적용합니다.
    /// </summary>
    public void ApplyKnockback(Vector3 direction, float distance)
    {
        if (enemyData != null && !enemyData.canBeStunned) return; // 슈퍼아머 상태 시 무시

        if (agent != null && agent.isOnNavMesh && agent.isActiveAndEnabled && distance > 0f)
        {
            Vector3 pushOffset = direction.normalized * distance;
            agent.Move(pushOffset);
        }
    }

    /// <summary>
    /// 지정한 시간 동안 부드럽게 밀어냅니다. (플레이어 회피 밀치기 등)
    /// 코루틴을 적 자신에서 실행하므로, 밀친 쪽이 도중에 비활성화되어도 적이 멈춘 채로 남지 않습니다.
    /// </summary>
    public void ApplySmoothPush(Vector3 pushVector, float duration)
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

        if (pushCoroutine != null)
        {
            StopCoroutine(pushCoroutine);
        }
        pushCoroutine = StartCoroutine(SmoothPushRoutine(pushVector, duration));
    }

    private IEnumerator SmoothPushRoutine(Vector3 pushVector, float duration)
    {
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + pushVector;
        float elapsedTime = 0f;
        agent.isStopped = true;

        while (elapsedTime < duration)
        {
            if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) break;
            Vector3 currentTargetPosition = Vector3.Lerp(startPosition, targetPosition, elapsedTime / duration);
            agent.Move(currentTargetPosition - transform.position);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        pushCoroutine = null;
        RestoreAgentMovement();
    }

    /// <summary>
    /// 경직과 밀려나기가 모두 끝났을 때만 이동을 재개합니다.
    /// </summary>
    private void RestoreAgentMovement()
    {
        if (stunTimer > 0f || pushCoroutine != null) return;

        if (agent != null && agent.isOnNavMesh && agent.isActiveAndEnabled)
        {
            agent.isStopped = false;
        }
    }

    private void ResetStun()
    {
        stunTimer = 0f;

        if (animator != null)
        {
            animator.speed = 1f;
        }

        RestoreAgentMovement();
    }

    /// <summary>
    /// 적의 현재 공격 대상을 결정합니다. (차량 우선 순위 적용)
    /// </summary>
    public Transform DetermineTarget()
    {
        if (prioritizeVehicle)
        {
            float maxRange = vehicleDetectionRange > 0f ? vehicleDetectionRange : Mathf.Infinity;
            Vehicle closestVehicle = Vehicle.GetClosestLivingVehicle(transform.position, maxRange);
            if (closestVehicle != null)
            {
                return closestVehicle.transform;
            }
        }

        // 차량이 없거나 파괴되었거나 범위 밖인 경우 플레이어를 타겟팅
        return PlayerPawnManager.ActivePlayerTransform;
    }

    void Update()
    {
        // 1. 피격 경직 처리: 경직 중에는 이동, 조준, 사격 중단
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                ResetStun();
            }
            return;
        }

        Transform target = DetermineTarget();

        // Add checks here to ensure the agent is active, enabled, and on NavMesh
        if (target != null && agent != null && agent.isOnNavMesh && agent.isActiveAndEnabled)
        {
            // 풀 재사용 등으로 정지 상태가 남아 있으면 복구 (경직/밀려나기 중이 아닐 때만 여기 도달)
            if (agent.isStopped && pushCoroutine == null)
            {
                agent.isStopped = false;
            }

            agent.SetDestination(target.position);

            // Aim at the target (수평 회전 유지)
            Vector3 targetDirection = target.position - transform.position;
            targetDirection.y = 0f;
            if (targetDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f); // Smooth rotation
            }

            // Fire if target is in range and cooldown is ready
            if (weaponData != null && firePoint != null && Time.time >= lastFireTime + FireInterval)
            {
                FireProjectile();
                lastFireTime = Time.time;
            }
        }
    }

    private void FireProjectile()
    {
        if (weaponData.projectileData == null || weaponData.projectileData.projectilePrefab == null)
        {
            Debug.LogWarning("Enemy WeaponData or ProjectileData is missing a prefab!");
            return;
        }

        // 풀에서 발사체를 가져와 재사용 (매 발사마다 Instantiate 시 비활성 오브젝트가 무한히 누적됨)
        GameObject projectileGO = ProjectilePool.Get(weaponData.projectileData.projectilePrefab, firePoint.position, firePoint.rotation);
        if (projectileGO == null) return;

        Projectile projectile = projectileGO.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.Init(weaponData.projectileData, firePoint.forward, gameObject.layer, weaponData.projectileData.damage, null, transform.root); // Pass projectileData, forward direction, enemy's layer, damage, and shooter root
        }

        projectileGO.SetActive(true);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (prioritizeVehicle && vehicleDetectionRange > 0f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, vehicleDetectionRange);
        }
    }
#endif
}
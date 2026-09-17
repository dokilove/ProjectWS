using UnityEngine;
using UnityEngine.AI; // Required for NavMeshAgent
using System.Collections; // Required for IEnumerator

public class EnemyAI : MonoBehaviour
{
    [SerializeField] private EnemyData enemyData;
    [SerializeField] private WeaponData weaponData; // Add WeaponData
    [SerializeField] private Transform firePoint; // Where projectiles will spawn

    private NavMeshAgent agent;
    private Animator animator;
    private float lastFireTime;
    private float stunTimer = 0f;

    /// <summary>
    /// 현재 경직 상태인지 여부
    /// </summary>
    public bool IsStunned => stunTimer > 0f;

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
            lastFireTime = -weaponData.fireRate; // Allow immediate firing
        }
    }

    private void OnDisable()
    {
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
        if (weaponData != null && Time.time >= lastFireTime + weaponData.fireRate)
        {
            lastFireTime = Time.time - weaponData.fireRate + 0.2f;
        }
    }

    private void ResetStun()
    {
        stunTimer = 0f;

        if (animator != null)
        {
            animator.speed = 1f;
        }

        if (agent != null && agent.isOnNavMesh && agent.isActiveAndEnabled)
        {
            agent.isStopped = false;
        }
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

        // Add checks here to ensure the agent is active, enabled, and on NavMesh
        if (PlayerPawnManager.ActivePlayerTransform != null && agent != null && agent.isOnNavMesh && agent.isActiveAndEnabled)
        {
            agent.SetDestination(PlayerPawnManager.ActivePlayerTransform.position);

            // Aim at the target
            Vector3 targetDirection = (PlayerPawnManager.ActivePlayerTransform.position - transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f); // Smooth rotation

            // Fire if target is in range and cooldown is ready
            if (weaponData != null && firePoint != null && Time.time >= lastFireTime + weaponData.fireRate)
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

        // Instantiate projectile
        GameObject projectileGO = Instantiate(weaponData.projectileData.projectilePrefab, firePoint.position, firePoint.rotation);
        Projectile projectile = projectileGO.GetComponent<Projectile>();

        if (projectile != null)
        {
            projectile.Init(weaponData.projectileData, firePoint.forward, gameObject.layer, weaponData.projectileData.damage); // Pass projectileData, forward direction, enemy's layer, and damage
        }
    }
}
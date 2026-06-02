using UnityEngine;
using UnityEngine.AI; // Required for NavMeshAgent
using System.Collections; // Required for IEnumerator

public class EnemyAI : MonoBehaviour
{
    [SerializeField] private EnemyData enemyData;
    [SerializeField] private WeaponData weaponData; // Add WeaponData
    [SerializeField] private Transform firePoint; // Where projectiles will spawn

    private NavMeshAgent agent;
    private float lastFireTime;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (enemyData != null)
        {
            agent.speed = enemyData.moveSpeed;
        }
        lastFireTime = -weaponData.fireRate; // Allow immediate firing
    }

    

    void Update()
    {
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
            projectile.Init(weaponData.projectileData, firePoint.forward, gameObject.layer); // Pass projectileData, forward direction, and enemy's layer
        }
    }
}
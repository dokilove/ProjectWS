using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class VehicleWeaponSystem : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Transform turretTransform;
    [SerializeField] private float turretRotationSpeed = 10f;
    [SerializeField] private Transform firePoint;

    [Header("Data")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private WeaponData weaponData;

    // --- Dependencies ---
    private Vehicle _vehicle;

    // --- State ---
    private float nextFireTime = 0f;
    private int currentAmmo;
    private bool isReloading = false;
    private Coroutine reloadCoroutine;
    private Vector3 currentAimDirection;

    // --- Projectile Pool ---
    private List<GameObject> projectilePool = new List<GameObject>();
    private int poolSize = 20;
    
    // --- Targeting ---
    private RaycastHit[] hitResults = new RaycastHit[10];

    // --- Public Properties ---
    public Transform TurretTransform => turretTransform;
    public Transform FirePoint => firePoint;
    public int CurrentAmmo => currentAmmo;
    public WeaponData WeaponData => weaponData;
    public bool IsReloading => isReloading;

    public void Init(Vehicle vehicle)
    {
        _vehicle = vehicle;
    }

    private void Start()
    {
        if (weaponData == null) return;
        currentAmmo = weaponData.magazineSize;

        if (weaponData.projectileData != null && weaponData.projectileData.projectilePrefab != null)
        {
            for (int i = 0; i < poolSize; i++)
            {
                GameObject proj = Instantiate(weaponData.projectileData.projectilePrefab);
                proj.SetActive(false);
                projectilePool.Add(proj);
            }
        }
    }

    public void SetAim(Vector3 aimDirection, Transform vehicleTransform)
    {
        if (aimDirection.sqrMagnitude > 0.01f)
        {
            currentAimDirection = aimDirection;

            Vector3 localTargetDir = vehicleTransform.InverseTransformDirection(currentAimDirection);
            float targetAngle = Mathf.Atan2(localTargetDir.x, localTargetDir.z) * Mathf.Rad2Deg;
            float clampedAngle = Mathf.Clamp(targetAngle, -weaponData.attackAngle / 2f, weaponData.attackAngle / 2f);

            Quaternion desiredLocalRotation = Quaternion.Euler(0, clampedAngle, 0);
            turretTransform.localRotation = Quaternion.Slerp(turretTransform.localRotation, desiredLocalRotation, turretRotationSpeed * Time.deltaTime);
        }
    }
    
    public void SetAimAI(Transform target, Transform vehicleTransform)
    {
        if (target != null)
        {
            Vector3 direction = target.position - turretTransform.position;
            direction.y = 0;

            Vector3 localTargetDir = vehicleTransform.InverseTransformDirection(direction.normalized);
            float targetAngle = Mathf.Atan2(localTargetDir.x, localTargetDir.z) * Mathf.Rad2Deg;
            float clampedAngle = Mathf.Clamp(targetAngle, -weaponData.attackAngle / 2f, weaponData.attackAngle / 2f);

            Quaternion desiredLocalRotation = Quaternion.Euler(0, clampedAngle, 0);
            turretTransform.localRotation = Quaternion.Slerp(turretTransform.localRotation, desiredLocalRotation, turretRotationSpeed * Time.deltaTime);
        }
    }

    public void HandleFireInput()
    {
        if (CanFire())
        {
            Fire();
        }
        else if (currentAmmo <= 0 && !isReloading)
        {
            reloadCoroutine = StartCoroutine(Reload());
        }
    }

    public void HandleFireHold(bool isHeld)
    {
        if (isHeld && CanFire())
        {
            Fire();
        }
        if (isHeld && currentAmmo <= 0 && !isReloading)
        {
            reloadCoroutine = StartCoroutine(Reload());
        }
    }

    public void HandleReloadInput()
    {
        if (!isReloading && currentAmmo < weaponData.magazineSize)
        {
            reloadCoroutine = StartCoroutine(Reload());
        }
    }

    public bool CanFire(Transform aiTarget = null)
    {
        if (weaponData == null || Time.time < nextFireTime || isReloading || currentAmmo <= 0)
        {
            return false;
        }

        if (!_vehicle.IsControlledByPlayer) // AI fire condition
        {
            if (aiTarget == null) return false;
            
            float distanceToTarget = Vector3.Distance(transform.position, aiTarget.position);
            if (distanceToTarget > weaponData.lockOnRadius) return false;

            Vector3 directionToTarget = (aiTarget.position - turretTransform.position).normalized;
            float angleToTarget = Vector3.Angle(turretTransform.forward, directionToTarget);
            return angleToTarget <= weaponData.attackAngle / 2;
        }
        
        return true; // Player can always fire if not reloading/on cooldown
    }

    public void Fire(Transform aiTarget = null)
    {
        if (!CanFire(aiTarget)) return;

        nextFireTime = Time.time + 1f / weaponData.fireRate;
        currentAmmo--;
        Vector3 baseFireDirection = turretTransform.forward;

        if (_vehicle.IsControlledByPlayer)
        {
            int hitCount = Physics.SphereCastNonAlloc(firePoint.position, 1f, turretTransform.forward, hitResults, weaponData.lockOnRadius, enemyLayer);
            Transform closestEnemy = null;
            float minDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                if (hitResults[i].transform.CompareTag("Enemy"))
                {
                    float distance = Vector3.Distance(firePoint.position, hitResults[i].transform.position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestEnemy = hitResults[i].transform;
                    }
                }
            }
            if (closestEnemy != null) baseFireDirection = (closestEnemy.position - firePoint.position).normalized;
        }
        else
        {
            if (aiTarget != null) baseFireDirection = (aiTarget.position - firePoint.position).normalized;
        }

        for (int i = 0; i < weaponData.projectilesPerShot; i++)
        {
            GameObject projectile = GetPooledProjectile();
            if (projectile != null)
            {
                Vector3 finalFireDirection = baseFireDirection;
                if (weaponData.spreadAngle > 0)
                {
                    float randomAngle = Random.Range(-weaponData.spreadAngle / 2, weaponData.spreadAngle / 2);
                    finalFireDirection = Quaternion.Euler(0, randomAngle, 0) * finalFireDirection;
                }
                projectile.transform.position = firePoint.position;
                projectile.transform.rotation = Quaternion.LookRotation(finalFireDirection);
                projectile.SetActive(true);
                projectile.GetComponent<Projectile>().Init(weaponData.projectileData, finalFireDirection, _vehicle.gameObject.layer, weaponData.projectileData.damage);
            }
        }
    }

    private IEnumerator Reload()
    {
        isReloading = true;
        yield return new WaitForSeconds(weaponData.reloadTime);
        currentAmmo = weaponData.magazineSize;
        isReloading = false;
        reloadCoroutine = null;
    }

    private GameObject GetPooledProjectile()
    {
        // Find an inactive projectile in the pool
        foreach (var proj in projectilePool)
        {
            if (proj != null && !proj.activeInHierarchy)
            {
                return proj;
            }
        }

        // If no inactive projectile is found, create a new one and add it to the pool.
        Debug.LogWarning($"[{gameObject.name}] Projectile pool exhausted! Instantiating a new one. Consider increasing the default pool size.", this);
        if (weaponData != null && weaponData.projectileData != null && weaponData.projectileData.projectilePrefab != null)
        {
            GameObject newProj = Instantiate(weaponData.projectileData.projectilePrefab);
            projectilePool.Add(newProj);
            return newProj;
        }

        return null;
    }
}
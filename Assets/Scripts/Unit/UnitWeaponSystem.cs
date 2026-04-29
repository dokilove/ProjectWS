using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class UnitWeaponSystem : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Transform turretTransform;
    [SerializeField] private Transform firePoint;

    [Header("Data")]
    [SerializeField] private WeaponData weaponData;

    // --- Dependencies ---
    private Unit _unit;

    // --- State ---
    private Vector3 aimDirection;
    private float nextFireTime = 0f;
    private int currentAmmo;
    private bool isReloading = false;

    // --- Projectile Pool ---
    private List<GameObject> projectilePool = new List<GameObject>();
    private int poolSize = 20;

    // --- Public Properties ---
    public Transform FirePoint => firePoint;
    public Transform TurretTransform => turretTransform;
    public int CurrentAmmo => currentAmmo;
    public WeaponData WeaponData => weaponData;
    public bool IsReloading => isReloading;

    public void Init(Unit unit)
    {
        _unit = unit;
    }

    private void Start()
    {
        if (weaponData == null)
        {
            Debug.LogWarning("WeaponData is not assigned in the inspector!", this);
            return;
        }

        currentAmmo = weaponData.magazineSize;
        aimDirection = transform.forward;

        // Initialize projectile pool
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

    /// <summary>
    /// Sets the aiming direction for the turret. Called by UnitInput.
    /// </summary>
    public void SetAim(Vector3 newAimDirection)
    {
        aimDirection = newAimDirection;
        if (turretTransform != null && aimDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(aimDirection);
            turretTransform.rotation = Quaternion.Slerp(turretTransform.rotation, targetRotation, 20f * Time.deltaTime);
        }
    }

    /// <summary>
    /// Handles a single fire input event (e.g., button press).
    /// </summary>
    public void HandleFireInput()
    {
        if (CanFire())
        {
            Fire();
        }
        else if (currentAmmo <= 0 && !isReloading)
        {
            StartCoroutine(Reload());
        }
    }

    /// <summary>
    /// Handles continuous fire input (e.g., button held down).
    /// </summary>
    public void HandleFireHold(bool isHeld)
    {
        if (isHeld && CanFire())
        {
            Fire();
        }
        if (isHeld && currentAmmo <= 0 && !isReloading)
        {
            StartCoroutine(Reload());
        }
    }

    /// <summary>
    /// Handles the reload input event.
    /// </summary>
    public void HandleReloadInput()
    {
        if (!isReloading && currentAmmo < weaponData.magazineSize)
        {
            StartCoroutine(Reload());
        }
    }

    private bool CanFire()
    {
        if (weaponData == null) return false;
        return Time.time >= nextFireTime && !isReloading && currentAmmo > 0;
    }

    private void Fire()
    {
        if (!CanFire()) return;

        nextFireTime = Time.time + 1f / weaponData.fireRate;
        currentAmmo--;
        
        _unit.UnitAnimator.TriggerAttack();

        for (int i = 0; i < weaponData.projectilesPerShot; i++)
        {
            GameObject projectileGO = GetPooledProjectile();
            if (projectileGO != null)
            {
                projectileGO.transform.position = firePoint.position;

                Vector3 fireDirection = turretTransform.forward;
                if (weaponData.spreadAngle > 0)
                {
                    float randomAngle = Random.Range(-weaponData.spreadAngle / 2, weaponData.spreadAngle / 2);
                    fireDirection = Quaternion.Euler(0, randomAngle, 0) * fireDirection;
                }
                
                projectileGO.transform.rotation = Quaternion.LookRotation(fireDirection);
                projectileGO.SetActive(true);

                Projectile projectile = projectileGO.GetComponent<Projectile>();
                if (projectile != null)
                {
                    projectile.Init(weaponData.projectileData, fireDirection, _unit.gameObject.layer);
                }
            }
        }
    }

    private IEnumerator Reload()
    {
        isReloading = true;
        _unit.UnitAnimator.TriggerReload();
        Debug.Log("Reloading...");
        yield return new WaitForSeconds(weaponData.reloadTime);
        currentAmmo = weaponData.magazineSize;
        isReloading = false;
        Debug.Log("Reload complete.");
    }

    private GameObject GetPooledProjectile()
    {
        if (projectilePool == null) return null;
        
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